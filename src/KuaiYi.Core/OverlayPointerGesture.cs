namespace KuaiYi.Core;
public enum OverlayPointerAction { Ignored,Pressed,Moved,Clicked,Released }
/// <summary>Fresh down on our real hit target starts movement. Passing an external held button never starts a gesture.</summary>
public sealed class OverlayPointerGesture
{
    private bool pressed,moved;private PixelPoint began;private PixelRect anchor;private double grabX,grabY;
    public bool Active=>pressed;
    public bool HasMoved=>moved;
    /// <summary>Continue the same captured physical gesture after a DPI/work-area rebase. Geometry changes suppress click.</summary>
    public bool Rebase(PixelPoint cursor,PixelRect current,bool geometryChanged)
    {if(!pressed)return false;began=cursor;anchor=current;moved|=geometryChanged;return true;}
    public bool TryDpiDesiredAnchor(PixelPoint cursor,DpiScale dpi,out PixelPoint desired)
    {desired=default;if(!pressed)return false;int size=dpi.CeilingPixels(48);desired=new(checked(cursor.X-(int)Math.Round(grabX*size)),checked(cursor.Y-(int)Math.Round(grabY*size)));return true;}
    public OverlayPointerAction Down(PixelPoint point,PixelRect current,bool actualHwndHit)
    {
        if(pressed||!actualHwndHit||point.X<current.Left||point.X>=current.Right||point.Y<current.Top||point.Y>=current.Bottom)return OverlayPointerAction.Ignored;
        began=point;anchor=current;grabX=(point.X-current.Left)/(double)current.Width;grabY=(point.Y-current.Top)/(double)current.Height;pressed=true;moved=false;return OverlayPointerAction.Pressed;
    }
    public OverlayPointerAction Move(PixelPoint point,out PixelPoint desired)
        =>MoveCore(point,out desired,5,5,true);
    public OverlayPointerAction Move(PixelPoint point,out PixelPoint desired,int systemThresholdX,int systemThresholdY,DpiScale dpi)
        =>MoveCore(point,out desired,Math.Max(dpi.CeilingPixels(6),systemThresholdX),Math.Max(dpi.CeilingPixels(6),systemThresholdY),false);
    private OverlayPointerAction MoveCore(PixelPoint point,out PixelPoint desired,int thresholdX,int thresholdY,bool legacy)
    {
        desired=default;if(!pressed)return OverlayPointerAction.Ignored;
        int dx=checked(point.X-began.X),dy=checked(point.Y-began.Y);
        if(!moved&&(legacy?Math.Abs((long)dx)+Math.Abs((long)dy)<5:Math.Abs((long)dx)<thresholdX&&Math.Abs((long)dy)<thresholdY))return OverlayPointerAction.Ignored;
        moved=true;desired=new(checked(anchor.Left+dx),checked(anchor.Top+dy));return OverlayPointerAction.Moved;
    }
    public OverlayPointerAction Up(){if(!pressed)return OverlayPointerAction.Ignored;pressed=false;return moved?OverlayPointerAction.Released:OverlayPointerAction.Clicked;}
    public void Cancel(){pressed=false;moved=false;}
}
