using System.Security.Cryptography;
using KuaiYi.Core;
namespace KuaiYi.Imaging;

/// <summary>Owns only the final cropped BGRA pixels, without paths or metadata. Caller transfers ownership.
/// Preview rasters are independent bounded derivatives and must be disposed by their consumer.</summary>
public sealed class OwnedSourceImage:IDisposable
{
    public const int PreviewSideLimit=1024;
    private readonly object sync=new();
    private ScreenshotPixels? pixels;
    public Guid SourceId {get;}=Guid.NewGuid();
    public int Width {get;}
    public int Height {get;}
    public bool IsDisposed {get{lock(sync)return pixels==null;}}
    public OwnedSourceImage(ScreenshotPixels owned)
    {
        ArgumentNullException.ThrowIfNull(owned);
        try{ImagePolicy.ValidateDimensions((uint)owned.Bounds.Width,(uint)owned.Bounds.Height);_=owned.Bytes;Width=owned.Bounds.Width;Height=owned.Bounds.Height;pixels=owned;}
        catch{owned.Dispose();throw;}
    }
    public ScreenshotPixels CreatePreview(int maximumWidth,int maximumHeight,CancellationToken cancellation=default)
    {
        if(maximumWidth is <1 or >PreviewSideLimit||maximumHeight is <1 or >PreviewSideLimit)throw new ArgumentOutOfRangeException(nameof(maximumWidth));
        lock(sync)
        {
            if(pixels==null)throw new ObjectDisposedException(nameof(OwnedSourceImage));cancellation.ThrowIfCancellationRequested();
            double scale=Math.Min(1,Math.Min((double)maximumWidth/Width,(double)maximumHeight/Height));int width=Math.Max(1,(int)(Width*scale)),height=Math.Max(1,(int)(Height*scale));
            byte[]? output=new byte[checked(width*height*4)];
            try
            {
                var source=pixels.Bytes.Span;
                for(int y=0;y<height;y++)
                {
                    cancellation.ThrowIfCancellationRequested();int sourceY=(int)((long)y*Height/height);
                    for(int x=0;x<width;x++)source.Slice(checked((sourceY*Width+(int)((long)x*Width/width))*4),4).CopyTo(output.AsSpan((y*width+x)*4,4));
                }
                cancellation.ThrowIfCancellationRequested();var preview=new ScreenshotPixels(new PixelRect(0,0,width,height),output);output=null;return preview;
            }
            finally{if(output!=null)CryptographicOperations.ZeroMemory(output);}
        }
    }
    public void Dispose(){lock(sync){pixels?.Dispose();pixels=null;}}
}
