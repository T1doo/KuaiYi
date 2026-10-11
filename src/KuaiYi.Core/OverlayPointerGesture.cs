namespace KuaiYi.Core;
public enum OverlayPointerAction { Ignored,Pressed,Moved,Clicked,Released }
/// <summary>Fresh down on our real hit target starts movement. Passing an external held button never starts a gesture.</summary>
public sealed class OverlayPointerGesture
{
    private bool pressed,moved;private PixelPoint began;private PixelRect anchor;
    public bool Active=>pressed;
    public OverlayPointerAction Down(PixelPoint point,PixelRect current,bool actualHwndHit)
    {
        if(pressed||!actualHwndHit||point.X<current.Left||point.X>=current.Right||point.Y<current.Top||point.Y>=current.Bottom)return OverlayPointerAction.Ignored;
        began=point;anchor=current;pressed=true;moved=false;return OverlayPointerAction.Pressed;
    }
    public OverlayPointerAction Move(PixelPoint point,out PixelPoint desired)
    {
        desired=default;if(!pressed)return OverlayPointerAction.Ignored;
        int dx=checked(point.X-began.X),dy=checked(point.Y-began.Y);if(!moved&&Math.Abs((long)dx)+Math.Abs((long)dy)<5)return OverlayPointerAction.Ignored;
        moved=true;desired=new(checked(anchor.Left+dx),checked(anchor.Top+dy));return OverlayPointerAction.Moved;
    }
    public OverlayPointerAction Up(){if(!pressed)return OverlayPointerAction.Ignored;pressed=false;return moved?OverlayPointerAction.Released:OverlayPointerAction.Clicked;}
    public void Cancel(){pressed=false;moved=false;}
}
