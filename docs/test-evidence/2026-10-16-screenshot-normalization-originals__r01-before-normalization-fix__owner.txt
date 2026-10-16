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
    /// <summary>Exact screenshot crop only; no preview reuse, resizing or file decoding. Oversize requires a smaller crop.
    /// Caller owns the PNG. A bounded pixel copy lets Clear release the original during background encoding.</summary>
    public ScreenshotPng CreateRequestPng(int byteLimit,CancellationToken cancellation=default)
    {
        if(byteLimit is <65536 or >4194304)throw new ArgumentOutOfRangeException(nameof(byteLimit));
        ScreenshotPixels copy;
        lock(sync)
        {
            if(pixels==null)throw new ObjectDisposedException(nameof(OwnedSourceImage));cancellation.ThrowIfCancellationRequested();
            if(Width>2560||Height>2560)throw new InvalidOperationException("SMALLER_SCREENSHOT_REQUIRED");
            copy=new(new PixelRect(0,0,Width,Height),pixels.Bytes.ToArray());
        }
        using(copy)
        {
            var png=ScreenshotPngEncoder.Encode(copy,cancellation);
            if(png.Bytes.Length<=byteLimit)return png;
            png.Dispose();throw new InvalidOperationException("SMALLER_SCREENSHOT_REQUIRED");
        }
    }
}
