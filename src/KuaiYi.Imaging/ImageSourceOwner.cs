namespace KuaiYi.Imaging;

public enum ImageSourceKind{Screenshot,LocalFile,Browser,Bitmap}
public enum EncodedRasterFormat{Png,Jpeg,Bmp,Webp,Gif}
public sealed record GifFirstFrameInfo(int SourceFrames,int Left,int Top,int Width,int Height);
// The closed hierarchy prevents external adapters from minting arbitrary trusted image owners.
public abstract class ImageSourceOwner:IDisposable
{
    internal ImageSourceOwner(){}
    public abstract Guid SourceId{get;}
    public abstract ImageSourceKind Kind{get;}
    public abstract string? Notice{get;}
    public abstract int Width{get;}
    public abstract int Height{get;}
    public abstract bool IsDisposed{get;}
    public abstract ScreenshotPixels CreatePreview(int maximumWidth,int maximumHeight,CancellationToken cancellation=default);
    public abstract PreparedImageRequest PrepareRequest(int byteLimit,CancellationToken cancellation=default);
    public abstract void Dispose();
}
public abstract class PreparedImageRequest:IDisposable
{
    internal PreparedImageRequest(){}
    public abstract Guid SourceId{get;}
    public abstract ImageSourceKind Kind{get;}
    public abstract string? Notice{get;}
    public abstract ImageTransform Transform{get;}
    public abstract ScreenshotNormalizationProof? NormalizationProof{get;}
    public abstract ReadOnlyMemory<byte> Bytes{get;}
    public abstract void Dispose();
}
