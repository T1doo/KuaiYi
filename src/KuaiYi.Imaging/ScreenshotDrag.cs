using KuaiYi.Core;
namespace KuaiYi.Imaging;
/// <summary>Physical selection gesture; terminate before releasing mouse capture so normal release is not cancellation.</summary>
public sealed class ScreenshotDrag
{
    private PixelPoint? start;
    public bool Active=>start!=null;
    public PixelRect? Region {get;private set;}
    public void Begin(PixelPoint point){start=point;Region=null;}
    public void Move(PixelPoint point)
    {if(start is {} a)Region=new(Math.Min(a.X,point.X),Math.Min(a.Y,point.Y),Math.Max(a.X,point.X),Math.Max(a.Y,point.Y));}
    public PixelRect? Finish(PixelPoint point){if(!Active)return null;Move(point);start=null;return Region;}
    public bool LostCapture(){bool cancel=Active;start=null;return cancel;}
    public void Reset(){start=null;Region=null;}
}
