using System.Security.Cryptography;
using KuaiYi.Imaging;
using SkiaSharp;
namespace KuaiYi.ImageWorker;

internal static class ImageRasterDecoder
{
    internal static DecodedRaster Decode(ReadOnlyMemory<byte> encoded,ImageDecodeJob job,CancellationToken cancellation)
    {
        ImageDecodePolicy.ValidateJob(job);cancellation.ThrowIfCancellationRequested();
        if(encoded.Length!=job.ByteLength||ImageDecodePolicy.Hash(encoded.Span)!=job.Sha256)throw new ImageJobException(ImageFailure.InvalidImage);
        var header=ImageDecodePolicy.Inspect(encoded.Span);if(header.Format==EncodedRasterFormat.Png)ImageDecodePolicy.ValidatePngContainer(encoded.Span,cancellation);
        else BaselineJpegScan.Validate(encoded.Span,header.Size,cancellation);
        byte[]? copy=encoded.ToArray(),pixels=null;
        try
        {
            using var input=new MemoryStream(copy,writable:false);using var codec=SKCodec.Create(input,out var created);
            if(codec==null||created!=SKCodecResult.Success)throw new ImageJobException(ImageFailure.InvalidImage);
            cancellation.ThrowIfCancellationRequested();var info=codec.Info;
            if((uint)info.Width!=header.Size.Width||(uint)info.Height!=header.Size.Height||codec.EncodedFormat!=(header.Format==EncodedRasterFormat.Png?SKEncodedImageFormat.Png:SKEncodedImageFormat.Jpeg))throw new ImageJobException(ImageFailure.InvalidImage);
            // The reviewed C API returns getFrameInfo().size(): static PNG/JPEG report0.
            // PNG acTL is separately rejected; JPEG has one static raster. Do not treat0 as an animation.
            if(codec.FrameCount is <0 or >1)throw new ImageJobException(ImageFailure.UnsupportedImage);
            var origin=job.PrepareLongEdge!=null?EncodedImageOrientation.Read(encoded.Span,header.Format):ImageOrientation.ExifTopLeft;
            if((int)codec.EncodedOrigin!=(int)origin)throw new ImageJobException(ImageFailure.UnsupportedImage);
            int total=ImageDecodePolicy.RasterBytes(info.Width,info.Height);pixels=new byte[total];
            var target=new SKImageInfo(info.Width,info.Height,SKColorType.Bgra8888,SKAlphaType.Unpremul);
            cancellation.ThrowIfCancellationRequested();var result=codec.GetPixels(target,pixels);cancellation.ThrowIfCancellationRequested();
            if(result!=SKCodecResult.Success)throw new ImageJobException(ImageFailure.InvalidImage);
            if(job.PrepareLongEdge!=null)return ImageRasterPreparer.Prepare(pixels,job,header.Format,new(info.Width,info.Height,origin),cancellation);
            var metadata=new ImageDecodeReply(job,ImageFailure.None,header.Format,info.Width,info.Height,checked(info.Width*4),total,ImageDecodePolicy.Hash(pixels),"BGRA8888-straight",1,1);
            var raster=new DecodedRaster(metadata,pixels);pixels=null;return raster;
        }
        finally{if(pixels!=null)CryptographicOperations.ZeroMemory(pixels);if(copy!=null)CryptographicOperations.ZeroMemory(copy);}
    }
}
