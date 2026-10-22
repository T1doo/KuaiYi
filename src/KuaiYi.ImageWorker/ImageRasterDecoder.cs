using System.Security.Cryptography;
using System.Runtime.InteropServices;
using KuaiYi.Imaging;
using SkiaSharp;
namespace KuaiYi.ImageWorker;

internal static class ImageRasterDecoder
{
    internal static DecodedRaster Decode(ReadOnlyMemory<byte> encoded,ImageDecodeJob job,CancellationToken cancellation)=>Decode(encoded,job,cancellation,null);
    internal static DecodedRaster Decode(ReadOnlyMemory<byte> encoded,ImageDecodeJob job,CancellationToken cancellation,Action<byte[]>? nativeInputAllocated)
    {
        ImageDecodePolicy.ValidateJob(job);cancellation.ThrowIfCancellationRequested();
        if(encoded.Length!=job.ByteLength||ImageDecodePolicy.Hash(encoded.Span)!=job.Sha256)throw new ImageJobException(ImageFailure.InvalidImage);
        var header=ImageDecodePolicy.Inspect(encoded.Span,cancellation);
        GifInspection? gif=null;
        if(header.Format==EncodedRasterFormat.Png)ImageDecodePolicy.ValidatePngContainer(encoded.Span,cancellation);
        else if(header.Format==EncodedRasterFormat.Jpeg)
        {
            if(ProgressiveJpegScan.AppliesTo(encoded.Span,cancellation))ProgressiveJpegScan.Validate(encoded.Span,header.Size,cancellation);
            else BaselineJpegScan.Validate(encoded.Span,header.Size,cancellation);
        }
        else if(header.Format==EncodedRasterFormat.Bmp)_=BmpDecodePolicy.Inspect(encoded.Span);
        else if(header.Format==EncodedRasterFormat.Webp)_=WebpDecodePolicy.Inspect(encoded.Span,cancellation);
        else if(header.Format==EncodedRasterFormat.Gif){gif=GifDecodePolicy.Inspect(encoded.Span,cancellation);GifFirstFrameLzw.Validate(encoded.Span,gif,cancellation);}
        else throw new ImageJobException(ImageFailure.UnsupportedImage);
        byte[]? copy=null,pixels=null;
        try
        {
            var sourceOrientation=ImageOrientation.ExifTopLeft;
            // Both raw/prepared PNG metadata and raw rejection occur before any native construction.
            copy=header.Format==EncodedRasterFormat.Gif?CopyGif(encoded.Span,gif!,cancellation):header.Format==EncodedRasterFormat.Png?EncodedImageOrientation.CopyPngForNative(encoded.Span,job.PrepareLongEdge!=null,out sourceOrientation,cancellation,nativeInputAllocated):encoded.ToArray();
            if(header.Format!=EncodedRasterFormat.Png)nativeInputAllocated?.Invoke(copy);
            cancellation.ThrowIfCancellationRequested();
            using var input=new MemoryStream(copy,writable:false);using var codec=SKCodec.Create(input,out var created);
            if(codec==null||created!=SKCodecResult.Success)throw new ImageJobException(ImageFailure.InvalidImage);
            cancellation.ThrowIfCancellationRequested();var info=codec.Info;
            var expectedFormat=header.Format switch{EncodedRasterFormat.Png=>SKEncodedImageFormat.Png,EncodedRasterFormat.Jpeg=>SKEncodedImageFormat.Jpeg,EncodedRasterFormat.Bmp=>SKEncodedImageFormat.Bmp,EncodedRasterFormat.Webp=>SKEncodedImageFormat.Webp,EncodedRasterFormat.Gif=>SKEncodedImageFormat.Gif,_=>throw new ImageJobException(ImageFailure.UnsupportedImage)};
            if((uint)info.Width!=header.Size.Width||(uint)info.Height!=header.Size.Height||codec.EncodedFormat!=expectedFormat)throw new ImageJobException(ImageFailure.InvalidImage);
            // The reviewed C API returns getFrameInfo().size(): static PNG/JPEG report0.
            // PNG acTL and WebP animation are separately rejected. Do not treat0 as an animation.
            if(gif!=null?codec.FrameCount!=1:codec.FrameCount is <0 or >1)throw new ImageJobException(ImageFailure.UnsupportedImage);
            if(header.Format!=EncodedRasterFormat.Png&&job.PrepareLongEdge!=null)sourceOrientation=EncodedImageOrientation.Read(encoded.Span,header.Format,cancellation);
            var nativeExpectedOrientation=header.Format==EncodedRasterFormat.Png?ImageOrientation.ExifTopLeft:sourceOrientation;
            if((int)codec.EncodedOrigin!=(int)nativeExpectedOrientation)throw new ImageJobException(ImageFailure.UnsupportedImage);
            int total=ImageDecodePolicy.RasterBytes(info.Width,info.Height);pixels=new byte[total];
            var target=new SKImageInfo(info.Width,info.Height,SKColorType.Bgra8888,SKAlphaType.Unpremul);
            cancellation.ThrowIfCancellationRequested();var result=gif==null?codec.GetPixels(target,pixels):DecodeGif(codec,target,pixels);cancellation.ThrowIfCancellationRequested();
            if(result!=SKCodecResult.Success)throw new ImageJobException(ImageFailure.InvalidImage);
            if(gif!=null)ClearOutside(pixels,info.Width,info.Height,gif.FirstFrame,cancellation);
            if(header.Format==EncodedRasterFormat.Bmp)
                for(int i=3;i<pixels.Length;i+=4){if((i&65535)==3)cancellation.ThrowIfCancellationRequested();if(pixels[i]!=255)throw new ImageJobException(ImageFailure.InvalidImage);}
            if(job.PrepareLongEdge!=null)return ImageRasterPreparer.Prepare(pixels,job,header.Format,new(info.Width,info.Height,sourceOrientation),cancellation,gifInfo:gif?.FirstFrame);
            var metadata=new ImageDecodeReply(job,ImageFailure.None,header.Format,info.Width,info.Height,checked(info.Width*4),total,ImageDecodePolicy.Hash(pixels),"BGRA8888-straight",1,1,GifInfo:gif?.FirstFrame);
            var raster=new DecodedRaster(metadata,pixels);pixels=null;return raster;
        }
        finally{if(pixels!=null)CryptographicOperations.ZeroMemory(pixels);if(copy!=null)CryptographicOperations.ZeroMemory(copy);}
    }
    // Native never receives later frame entropy. Original job/SHA remains bound to the complete original.
    private static byte[] CopyGif(ReadOnlySpan<byte> source,GifInspection gif,CancellationToken cancellation)
    {
        byte[]? copy=new byte[checked(gif.FirstImageEnd+1)];
        try{for(int at=0;at<gif.FirstImageEnd;){cancellation.ThrowIfCancellationRequested();int n=Math.Min(65536,gif.FirstImageEnd-at);source.Slice(at,n).CopyTo(copy.AsSpan(at,n));at+=n;}copy[^1]=0x3b;var delivery=copy;copy=null;return delivery;}
        finally{if(copy!=null)CryptographicOperations.ZeroMemory(copy);}
    }
    private static SKCodecResult DecodeGif(SKCodec codec,SKImageInfo target,byte[] pixels)
    {
        var pinned=GCHandle.Alloc(pixels,GCHandleType.Pinned);
        try{return codec.GetPixels(target,pinned.AddrOfPinnedObject(),target.RowBytes,new SKCodecOptions(0,-1){ZeroInitialized=SKZeroInitialized.Yes});}
        finally{pinned.Free();}
    }
    // Product first-frame canvas is transparent regardless of the GIF logical background index.
    private static void ClearOutside(byte[] pixels,int width,int height,GifFirstFrameInfo frame,CancellationToken cancellation)
    {
        for(int y=0;y<height;y++)
        {
            cancellation.ThrowIfCancellationRequested();var row=pixels.AsSpan(y*width*4,width*4);
            if(y<frame.Top||y>=frame.Top+frame.Height)row.Clear();
            else{row[..(frame.Left*4)].Clear();row[((frame.Left+frame.Width)*4)..].Clear();}
        }
    }

}
