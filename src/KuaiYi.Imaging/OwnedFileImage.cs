using System.Security.Cryptography;
namespace KuaiYi.Imaging;

public sealed record FileImageMetadata(EncodedRasterFormat Format,int SourceByteLength,string SourceSha256,ImageTransform Transform,int MaximumLongEdge,GifFirstFrameInfo? GifInfo=null);
/// <summary>Owns a prepared encoded raster and its single bounded PNG; Kind distinguishes local file from received browser bytes. No path, disk handle or desktop crop.</summary>
public sealed class OwnedFileImage:ImageSourceOwner
{
    private readonly object sync=new();private byte[]? pixels;private ScreenshotPng? png;
    public FileImageMetadata Metadata{get;}
    public override Guid SourceId{get;}=Guid.NewGuid();
    private readonly ImageSourceKind kind;
    public override ImageSourceKind Kind=>kind;
    public override string? Notice=>Metadata.Format==EncodedRasterFormat.Gif?"使用首帧":null;
    public override int Width=>(int)Metadata.Transform.NormalizedSize.Width;
    public override int Height=>(int)Metadata.Transform.NormalizedSize.Height;
    public override bool IsDisposed{get{lock(sync)return pixels==null;}}
    internal OwnedFileImage(FileImageMetadata metadata,byte[] ownedPixels,ScreenshotPng ownedPng,ImageSourceKind sourceKind=ImageSourceKind.LocalFile)
    {
        try
        {
            var t=metadata.Transform;
            if(sourceKind==ImageSourceKind.Bitmap&&metadata.Format!=EncodedRasterFormat.Bmp)throw new InvalidDataException("Bitmap source requires BMP.");
            if(sourceKind is not(ImageSourceKind.LocalFile or ImageSourceKind.Browser or ImageSourceKind.Bitmap)||sourceKind==ImageSourceKind.Browser&&metadata.Format is not(EncodedRasterFormat.Png or EncodedRasterFormat.Jpeg or EncodedRasterFormat.Bmp or EncodedRasterFormat.Webp or EncodedRasterFormat.Gif))throw new InvalidDataException("Invalid encoded source kind.");
            kind=sourceKind;
            if(!Enum.IsDefined(metadata.Format)||metadata.SourceByteLength is <=0 or >ImagePolicy.ByteLimit||metadata.SourceSha256 is not{Length:64}||metadata.SourceSha256.Any(c=>!char.IsAsciiDigit(c)&&c is not(>='a' and <='f'))||t.HasDesktopAnchor||t!=ImageTransform.PlanEncoded(t.OriginalSize,t.Orientation,metadata.MaximumLongEdge)||ownedPixels.Length!=checked((int)(t.NormalizedSize.Width*t.NormalizedSize.Height)*4)||ImagePolicy.InspectPng(ownedPng.Bytes.Span)!=t.NormalizedSize||ownedPng.Bytes.Length>4*1024*1024)throw new InvalidDataException("File image ownership mismatch.");
            if(metadata.Format==EncodedRasterFormat.Gif)
            {var g=metadata.GifInfo??throw new InvalidDataException("Missing first-frame information.");if(g.SourceFrames is <1 or >256||g.Left<0||g.Top<0||g.Width<=0||g.Height<=0||(long)g.Left+g.Width>t.OriginalSize.Width||(long)g.Top+g.Height>t.OriginalSize.Height||t.Orientation!=ImageOrientation.ExifTopLeft)throw new InvalidDataException("Invalid first frame.");}
            else if(metadata.GifInfo!=null)throw new InvalidDataException("Unexpected first frame.");
            for(int i=3;i<ownedPixels.Length;i+=4)if(ownedPixels[i]!=255)throw new InvalidDataException("File preview is not opaque.");
            Metadata=metadata;pixels=ownedPixels;png=ownedPng;
        }
        catch{CryptographicOperations.ZeroMemory(ownedPixels);ownedPng.Dispose();throw;}
    }
    public override ScreenshotPixels CreatePreview(int maximumWidth,int maximumHeight,CancellationToken cancellation=default)
    {
        if(maximumWidth is <1 or >1024||maximumHeight is <1 or >1024)throw new ArgumentOutOfRangeException(nameof(maximumWidth));
        lock(sync)
        {
            if(pixels==null)throw new ObjectDisposedException(nameof(OwnedFileImage));cancellation.ThrowIfCancellationRequested();
            double scale=Math.Min(1,Math.Min((double)maximumWidth/Width,(double)maximumHeight/Height));int width=Math.Max(1,(int)(Width*scale)),height=Math.Max(1,(int)(Height*scale));byte[]? copy=new byte[width*height*4];
            try
            {for(int y=0;y<height;y++){cancellation.ThrowIfCancellationRequested();for(int x=0;x<width;x++)pixels.AsSpan(((int)((long)y*Height/height)*Width+(int)((long)x*Width/width))*4,4).CopyTo(copy.AsSpan((y*width+x)*4,4));}cancellation.ThrowIfCancellationRequested();var result=new ScreenshotPixels(new(0,0,width,height),copy);copy=null;return result;}
            finally{if(copy!=null)CryptographicOperations.ZeroMemory(copy);}
        }
    }
    public override PreparedImageRequest PrepareRequest(int byteLimit,CancellationToken cancellation=default)
    {
        if(byteLimit is <65536 or >4194304)throw new ArgumentOutOfRangeException(nameof(byteLimit));
        lock(sync)
        {
            if(pixels==null||png==null)throw new ObjectDisposedException(nameof(OwnedFileImage));cancellation.ThrowIfCancellationRequested();
            if(png.Bytes.Length>byteLimit)throw new ScreenshotNormalizationException();byte[]? copy=new byte[png.Bytes.Length];
            try{for(int at=0;at<copy.Length;){cancellation.ThrowIfCancellationRequested();int n=Math.Min(65536,copy.Length-at);png.Bytes.Span.Slice(at,n).CopyTo(copy.AsSpan(at,n));at+=n;}cancellation.ThrowIfCancellationRequested();var result=new PreparedFileImage(SourceId,new(copy),Metadata.Transform,Notice,kind);copy=null;return result;}
            finally{if(copy!=null)CryptographicOperations.ZeroMemory(copy);}
        }
    }
    public override void Dispose(){lock(sync){if(pixels!=null)CryptographicOperations.ZeroMemory(pixels);pixels=null;png?.Dispose();png=null;}}
    private sealed class PreparedFileImage(Guid id,ScreenshotPng owned,ImageTransform transform,string? notice,ImageSourceKind sourceKind):PreparedImageRequest
    {
        private ScreenshotPng? png=owned;
        public override Guid SourceId=>id;public override ImageSourceKind Kind=>sourceKind;
        public override string? Notice=>notice;public override ImageTransform Transform=>transform;
        public override ScreenshotNormalizationProof? NormalizationProof=>null;
        public override ReadOnlyMemory<byte> Bytes=>(png??throw new ObjectDisposedException(nameof(PreparedFileImage))).Bytes;
        public override void Dispose()=>Interlocked.Exchange(ref png,null)?.Dispose();
    }
}
