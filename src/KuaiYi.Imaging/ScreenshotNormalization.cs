using System.Security.Cryptography;
using KuaiYi.Core;
namespace KuaiYi.Imaging;

public readonly record struct OriginalImagePoint(double X,double Y);
public readonly record struct OriginalImageRectangle(double X,double Y,double Width,double Height);
public readonly record struct NormalizedImagePoint(double X,double Y);
public readonly record struct NormalizedImageRectangle(double X,double Y,double Width,double Height);
public readonly record struct DesktopImagePoint(double X,double Y);
public readonly record struct DesktopImageRectangle(double X,double Y,double Width,double Height);
public readonly record struct UnitImageRectangle(double X,double Y,double Width,double Height);
public enum ImageOrientation { ScreenshotIdentity }
public enum ScreenshotNormalizationProof { NativeSizeWhiteComposite, ConstantCompositeDownscale }
public sealed class ScreenshotNormalizationException():Exception("SMALLER_SCREENSHOT_REQUIRED") {}

/// <summary>Separate spaces for continuous pixel edges, not centers or DIP. Original is crop-local;
/// Normalized is prepared-raster/OCR pixels [0,W]x[0,H]; Unit is a classifier rectangle [0,1]x[0,1].
/// Desktop adds the physical OriginalCrop origin. No EXIF decoding or OCR execution.</summary>
public sealed record ImageTransform
{
    public const int LongEdgeLimit=2560;
    public PixelRect OriginalCrop {get;}
    public ImageSize OriginalSize {get;}
    public ImageSize NormalizedSize {get;}
    public ImageOrientation Orientation=>ImageOrientation.ScreenshotIdentity;
    public double ScaleX=>(double)NormalizedSize.Width/OriginalSize.Width;
    public double ScaleY=>(double)NormalizedSize.Height/OriginalSize.Height;
    public bool Downscaled=>NormalizedSize!=OriginalSize;
    private ImageTransform(PixelRect crop,ImageSize original,ImageSize normalized)
    {OriginalCrop=crop;OriginalSize=original;NormalizedSize=normalized;}
    // Aspect fit rounds the shorter raster edge to nearest integer, never below one pixel.
    // ScaleX/Y record the actual rounded raster mapping instead of claiming identical axis ratios.
    public static ImageTransform Plan(PixelRect crop)
    {
        if(crop.Width<=0||crop.Height<=0)throw new ArgumentOutOfRangeException(nameof(crop));
        ImagePolicy.ValidateDimensions((uint)crop.Width,(uint)crop.Height);int longest=Math.Max(crop.Width,crop.Height);
        int width=crop.Width,height=crop.Height;
        if(longest>LongEdgeLimit){width=Math.Max(1,(int)(((long)width*LongEdgeLimit+longest/2)/longest));height=Math.Max(1,(int)(((long)height*LongEdgeLimit+longest/2)/longest));}
        return new(crop,new((uint)crop.Width,(uint)crop.Height),new((uint)width,(uint)height));
    }
    public NormalizedImagePoint OriginalToNormalized(OriginalImagePoint point)
    {Check(point.X,point.Y,OriginalSize);return new(Map(point.X,OriginalSize.Width,NormalizedSize.Width),Map(point.Y,OriginalSize.Height,NormalizedSize.Height));}
    public OriginalImagePoint NormalizedToOriginal(NormalizedImagePoint point)
    {Check(point.X,point.Y,NormalizedSize);return new(Map(point.X,NormalizedSize.Width,OriginalSize.Width),Map(point.Y,NormalizedSize.Height,OriginalSize.Height));}
    public DesktopImagePoint NormalizedToDesktop(NormalizedImagePoint point)
    {var local=NormalizedToOriginal(point);return new(local.X+OriginalCrop.Left,local.Y+OriginalCrop.Top);}
    public NormalizedImagePoint DesktopToNormalized(DesktopImagePoint point)
    {CheckDesktop(point.X,point.Y);return OriginalToNormalized(new OriginalImagePoint(point.X-OriginalCrop.Left,point.Y-OriginalCrop.Top));}
    public NormalizedImageRectangle OriginalToNormalized(OriginalImageRectangle region)
    {Check(region.X,region.Y,region.Width,region.Height,OriginalSize);var r=Map(region.X,region.Y,region.Width,region.Height,OriginalSize,NormalizedSize);return new(r.X,r.Y,r.Width,r.Height);}
    public OriginalImageRectangle NormalizedToOriginal(NormalizedImageRectangle region)
    {Check(region.X,region.Y,region.Width,region.Height,NormalizedSize);var r=Map(region.X,region.Y,region.Width,region.Height,NormalizedSize,OriginalSize);return new(r.X,r.Y,r.Width,r.Height);}
    public DesktopImageRectangle NormalizedToDesktop(NormalizedImageRectangle region)
    {
        var local=NormalizedToOriginal(region);
        double left=local.X+OriginalCrop.Left,top=local.Y+OriginalCrop.Top;
        double right=End(local.X,local.Width,OriginalSize.Width)+OriginalCrop.Left,bottom=End(local.Y,local.Height,OriginalSize.Height)+OriginalCrop.Top;
        return new(left,top,right-left,bottom-top);
    }
    public NormalizedImageRectangle DesktopToNormalized(DesktopImageRectangle region)
    {
        // Validate in the supplied physical space before subtraction can erase an outward neighbor.
        CheckDesktop(region.X,region.Y);
        if(!double.IsFinite(region.Width)||!double.IsFinite(region.Height)||region.Width<0||region.Height<0||region.Width>OriginalCrop.Right-region.X||region.Height>OriginalCrop.Bottom-region.Y)throw new ArgumentOutOfRangeException(nameof(region));
        double left=region.X-OriginalCrop.Left,top=region.Y-OriginalCrop.Top;
        // Only validated inputs reach these endpoint additions; min absorbs their rounding alone.
        double right=End(region.X,region.Width,OriginalCrop.Right)-OriginalCrop.Left,bottom=End(region.Y,region.Height,OriginalCrop.Bottom)-OriginalCrop.Top;
        return OriginalToNormalized(new OriginalImageRectangle(left,top,right-left,bottom-top));
    }
    public NormalizedImageRectangle RelativeToNormalized(UnitImageRectangle region)
    {
        if(!UnitInterval.Contains(region.X,region.Width)||!UnitInterval.Contains(region.Y,region.Height))throw new ArgumentOutOfRangeException(nameof(region));
        var r=Map(region.X,region.Y,region.Width,region.Height,new(1,1),NormalizedSize);return new(r.X,r.Y,r.Width,r.Height);
    }
    public OriginalImageRectangle RelativeToOriginal(UnitImageRectangle region)=>NormalizedToOriginal(RelativeToNormalized(region));
    private static double Map(double edge,uint from,uint to)=>from==to?edge:edge==from?to:edge/from*to;
    // For validated extents the maximal accepted width is exactly the rounded subtraction.
    // Preserve its known endpoint: adding that width back may round to either side of the edge.
    private static double End(double start,double length,double limit)=>length==limit-start?limit:Math.Min(limit,start+length);
    private static (double X,double Y,double Width,double Height) Map(double x,double y,double width,double height,ImageSize from,ImageSize to)
    {
        double left=Map(x,from.Width,to.Width),top=Map(y,from.Height,to.Height);
        // Inputs already validated; min only absorbs floating-point edge-addition roundoff.
        double right=Map(End(x,width,from.Width),from.Width,to.Width),bottom=Map(End(y,height,from.Height),from.Height,to.Height);
        return (left,top,right-left,bottom-top);
    }
    private static void Check(double x,double y,ImageSize size)
    {if(!double.IsFinite(x)||!double.IsFinite(y)||x<0||y<0||x>size.Width||y>size.Height)throw new ArgumentOutOfRangeException(nameof(x));}
    private static void Check(double x,double y,double width,double height,ImageSize size)
    {
        Check(x,y,size);
        if(!double.IsFinite(width)||!double.IsFinite(height)||width<0||height<0||width>size.Width-x||height>size.Height-y)throw new ArgumentOutOfRangeException(nameof(width));
    }
    private void CheckDesktop(double x,double y)
    {if(!double.IsFinite(x)||!double.IsFinite(y)||x<OriginalCrop.Left||y<OriginalCrop.Top||x>OriginalCrop.Right||y>OriginalCrop.Bottom)throw new ArgumentOutOfRangeException(nameof(x));}
}

/// <summary>Only successful, bounded preparations can construct this owner. Mapping is local metadata,
/// never serialized into an image request. Dispose zeros the actual returned PNG buffer.</summary>
public sealed class PreparedScreenshot:IDisposable
{
    private ScreenshotPng? png;
    public Guid SourceId {get;}
    public ImageTransform Transform {get;}
    public ScreenshotNormalizationProof Proof {get;}
    public ReadOnlyMemory<byte> Bytes=>(png??throw new ObjectDisposedException(nameof(PreparedScreenshot))).Bytes;
    internal PreparedScreenshot(Guid sourceId,ScreenshotPng owned,ImageTransform transform,ScreenshotNormalizationProof proof)
    {SourceId=sourceId;png=owned;Transform=transform;Proof=proof;}
    internal ScreenshotPng TakePng()=>Interlocked.Exchange(ref png,null)??throw new ObjectDisposedException(nameof(PreparedScreenshot));
    public void Dispose()=>Interlocked.Exchange(ref png,null)?.Dispose();
}

internal sealed class NormalizedScreenshotPixels(ScreenshotPixels owned,ImageTransform transform,ScreenshotNormalizationProof proof):IDisposable
{
    public ScreenshotPixels Pixels {get;}=owned;
    public ImageTransform Transform {get;}=transform;
    public ScreenshotNormalizationProof Proof {get;}=proof;
    public void Dispose()=>Pixels.Dispose();
}
internal static class ScreenshotNormalizer
{
    public static PreparedScreenshot Prepare(ScreenshotPixels copy,Guid sourceId,int byteLimit,bool allowConstantDownscale,CancellationToken cancellation)
    {
        using var raster=Normalize(copy,allowConstantDownscale,cancellation);ScreenshotPng? png=null;
        try
        {
            png=ScreenshotPngEncoder.Encode(raster.Pixels,cancellation,byteLimit);cancellation.ThrowIfCancellationRequested();
            var result=new PreparedScreenshot(sourceId,png,raster.Transform,raster.Proof);png=null;return result;
        }
        catch(ScreenshotException error)when(error.Code==ScreenshotFailure.RegionTooLarge)
        {cancellation.ThrowIfCancellationRequested();throw new ScreenshotNormalizationException();}
        finally{png?.Dispose();}
    }
    public static NormalizedScreenshotPixels Normalize(ScreenshotPixels input,bool allowConstantDownscale,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();var transform=ImageTransform.Plan(input.Bounds);
        if(transform.Downscaled&&!allowConstantDownscale)throw new ScreenshotNormalizationException();
        var source=input.Bytes.Span;byte blue=Composite(source[0],source[3]),green=Composite(source[1],source[3]),red=Composite(source[2],source[3]);
        // Exact full scan after compositing. No tolerance, sample, OCR guess or loss of visible structure.
        if(transform.Downscaled)
            for(int y=0;y<input.Bounds.Height;y++)
            {
                cancellation.ThrowIfCancellationRequested();int end=(y+1)*input.Bounds.Width*4;
                for(int i=y*input.Bounds.Width*4;i<end;i+=4)
                    if(Composite(source[i],source[i+3])!=blue||Composite(source[i+1],source[i+3])!=green||Composite(source[i+2],source[i+3])!=red)throw new ScreenshotNormalizationException();
            }
        int width=(int)transform.NormalizedSize.Width,height=(int)transform.NormalizedSize.Height;byte[]? output=new byte[checked(width*height*4)];
        try
        {
            for(int y=0;y<height;y++)
            {
                cancellation.ThrowIfCancellationRequested();
                for(int x=0;x<width;x++)
                {
                    int i=(y*width+x)*4;
                    output[i]=transform.Downscaled?blue:Composite(source[i],source[i+3]);output[i+1]=transform.Downscaled?green:Composite(source[i+1],source[i+3]);output[i+2]=transform.Downscaled?red:Composite(source[i+2],source[i+3]);output[i+3]=255;
                }
            }
            cancellation.ThrowIfCancellationRequested();var pixels=new ScreenshotPixels(new(0,0,width,height),output);output=null;
            return new(pixels,transform,transform.Downscaled?ScreenshotNormalizationProof.ConstantCompositeDownscale:ScreenshotNormalizationProof.NativeSizeWhiteComposite);
        }
        finally{if(output!=null)CryptographicOperations.ZeroMemory(output);}
    }
    private static byte Composite(byte channel,byte alpha)=>(byte)((channel*alpha+255*(255-alpha)+127)/255);
}
