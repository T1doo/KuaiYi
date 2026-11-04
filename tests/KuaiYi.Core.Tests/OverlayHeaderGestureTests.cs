using KuaiYi.Core;
using Xunit;

namespace KuaiYi.Core.Tests;

public sealed class OverlayHeaderGestureTests
{
    private static readonly PixelRect Anchor=new(100,100,148,148),Header=new(148,100,520,148);
    [Fact]public void HeaderClickNeverTogglesActivation()
    {
        var gesture=new OverlayPointerGesture();
        Assert.Equal(OverlayPointerAction.Pressed,gesture.DownSurface(new(300,120),Anchor,Header,true));
        Assert.Equal(OverlayPointerAction.Ignored,gesture.Move(new(303,122),out _,5,5,new(1)));
        Assert.Equal(OverlayPointerAction.Ignored,gesture.Up());Assert.False(gesture.Active);
    }
    [Fact]public void HeaderMovementKeepsOriginalGrabOffsetAndPersistsOnlyAfterDrag()
    {
        var gesture=new OverlayPointerGesture();gesture.DownSurface(new(300,120),Anchor,Header,true);
        Assert.Equal(OverlayPointerAction.Moved,gesture.Move(new(340,145),out var desired,5,5,new(1)));
        Assert.Equal(new PixelPoint(140,125),desired);Assert.Equal(OverlayPointerAction.Released,gesture.Up());
    }
    [Fact]public void SurfaceCannotStartFromExternalHeldPointerOrOutsideItsBounds()
    {
        var gesture=new OverlayPointerGesture();
        Assert.Equal(OverlayPointerAction.Ignored,gesture.Move(new(300,120),out _));
        Assert.Equal(OverlayPointerAction.Ignored,gesture.DownSurface(new(300,120),Anchor,Header,false));
        Assert.Equal(OverlayPointerAction.Ignored,gesture.DownSurface(new(300,160),Anchor,Header,true));
        Assert.False(gesture.Active);
    }
    [Fact]public void SurfaceGrabScalesWithDpiAndCancellationCannotBecomeClick()
    {
        var gesture=new OverlayPointerGesture();gesture.DownSurface(new(300,120),Anchor,Header,true);
        Assert.True(gesture.TryDpiDesiredAnchor(new(600,240),new(2),out var desired));
        Assert.Equal(new PixelPoint(200,200),desired);
        gesture.Cancel();Assert.Equal(OverlayPointerAction.Ignored,gesture.Up());
        Assert.Equal(OverlayPointerAction.Pressed,gesture.Down(new(120,120),Anchor,true));
        Assert.Equal(OverlayPointerAction.Clicked,gesture.Up());
    }
}
