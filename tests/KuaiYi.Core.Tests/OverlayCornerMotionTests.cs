using KuaiYi.Core;
using Xunit;

namespace KuaiYi.Core.Tests;

public sealed class OverlayCornerMotionTests
{
    private static OverlayFrames Frames()=>new(new(100,100,148,148),new("public",new(0,0,1920,1080),new(1)));
    [Fact]public void CornersKeepChangingThroughTheWholeExistingOpeningAndClosing()
    {
        var f=Frames();var anchor=f.Anchor;f.Open(280,new(420,640));
        Assert.Equal(0,f.CornerProgress);f.Tick(TimeSpan.FromMilliseconds(20));Assert.InRange(f.CornerProgress,.001,.999);
        f.Tick(TimeSpan.FromMilliseconds(199));Assert.InRange(f.CornerProgress,.001,.99999999);
        f.Tick(TimeSpan.FromMilliseconds(1));Assert.Equal(1,f.CornerProgress);Assert.Equal(OverlayTransition.Expanded,f.Transition);
        f.Collapse();f.Tick(TimeSpan.FromMilliseconds(179));Assert.InRange(f.CornerProgress,.000001,.999);
        f.Tick(TimeSpan.FromMilliseconds(1));Assert.Equal(0,f.CornerProgress);Assert.Equal(anchor,f.Current);
    }
    [Fact]public void ReversingDoesNotJumpTheDisplayedCornerShape()
    {
        var f=Frames();f.Open(260,new(420,640));f.Tick(TimeSpan.FromMilliseconds(40));double opening=f.CornerProgress;
        f.Collapse();f.Tick(TimeSpan.Zero);Assert.Equal(opening,f.CornerProgress);
        f.Tick(TimeSpan.FromMilliseconds(35));double closing=f.CornerProgress;Assert.True(closing<opening);
        f.Open(400,new(420,640));f.Tick(TimeSpan.Zero);Assert.Equal(closing,f.CornerProgress);
        f.Tick(TimeSpan.FromMilliseconds(220));Assert.Equal(1,f.CornerProgress);
    }
    [Fact]public void ReducedMotionAndReanchorResetCornersWithTheSameGeometry()
    {
        var f=Frames();f.Open(280,new(420,640));f.Tick(TimeSpan.Zero,true);Assert.Equal(1,f.CornerProgress);Assert.False(f.NeedsFrame);
        f.Reanchor(new(500,500,548,548),f.Monitor,new(420,640));Assert.Equal(0,f.CornerProgress);
        f.Tick(TimeSpan.Zero,true);Assert.Equal(1,f.CornerProgress);f.Compact();Assert.Equal(0,f.CornerProgress);
    }
    [Fact]public void FirstBodyAfterWaitingRibbonUsesTheOriginalEightyMillisecondFade()
    {
        var f=Frames();f.OpenToolbar();f.Tick(TimeSpan.FromMilliseconds(220));Assert.Equal(1,f.ContentOpacity);
        f.Open(280,new(420,640));f.Tick(TimeSpan.Zero);Assert.Equal(0,f.ContentOpacity);
        f.Tick(TimeSpan.FromMilliseconds(40));Assert.Equal(.5,f.ContentOpacity,6);
        f.Tick(TimeSpan.FromMilliseconds(40));Assert.Equal(1,f.ContentOpacity);Assert.Equal(OverlayTransition.Opening,f.Transition);
    }
}
