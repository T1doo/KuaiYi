using System.Reflection;
using KuaiYi.Core;
using KuaiYi.GrowthHarness;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class UiPolishOpeningFrameTests
{
    private static OverlayFrames Frames()=>new(new(30,30,78,78),new("public",new(0,0,1200,900),new(1)));
    private static PixelRect Target(OverlayFrames f)=>(PixelRect)typeof(OverlayFrames).GetField("target",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f)!;
    [Theory][InlineData(40)][InlineData(120)][InlineData(180)]
    public void ActualGeometryIntermediateRemainsRequiredWhenOpacityAlreadySettled(int milliseconds)
    {
        var f=Frames();f.Open(360,new(480,640));f.Tick(TimeSpan.FromMilliseconds(milliseconds));
        Assert.Equal(OverlayTransition.Opening,f.Transition);Assert.True(f.NeedsFrame);Assert.NotEqual(f.Anchor,f.Current);Assert.NotEqual(Target(f),f.Current);
        if(milliseconds>=80){Assert.Equal(1,f.ContentOpacity);Assert.False(f.ContentOpacity>0&&f.ContentOpacity<1);}
        var observed=UiPolishOpeningFrame.Observe(f,Target(f),2,1);Assert.NotNull(observed);Assert.Equal(f.Current,observed.Card);Assert.Equal(f.ContentOpacity,observed.Opacity);
    }
    [Fact]public void ReopeningFromOpaqueToolbarStillRequiresActualIntermediateGeometry()
    {
        var f=Frames();f.OpenToolbar();f.Tick(TimeSpan.FromMilliseconds(220));f.Open(360,new(480,640));f.Tick(TimeSpan.FromMilliseconds(20));
        Assert.Equal(1,f.ContentOpacity);Assert.NotNull(UiPolishOpeningFrame.Observe(f,Target(f),3,2));
    }
    [Fact]public void AnchorAndQuantizedTargetCannotBecomeIntermediateEvidence()
    {
        var f=Frames();f.Open(360,new(480,640));Assert.Null(UiPolishOpeningFrame.Observe(f,Target(f),2,1));
        f.Tick(TimeSpan.FromMilliseconds(219.99));Assert.Equal(Target(f),f.Current);Assert.Null(UiPolishOpeningFrame.Observe(f,Target(f),2,1));
    }
    [Fact]public void NoFrameAndReducedMotionTerminalDoNotPass()
    {
        var f=Frames();Assert.Null(UiPolishOpeningFrame.Observe(f,f.Current,2,1));f.Open(360,new(480,640));f.Tick(TimeSpan.FromMilliseconds(16),reduceMotion:true);
        Assert.Equal(OverlayTransition.Expanded,f.Transition);Assert.Null(UiPolishOpeningFrame.Observe(f,Target(f),2,1));
    }
    [Fact]public void OldOrUnchangedEpochCannotBeReplayedAsNewOpening()
    {
        var f=Frames();f.Open(360,new(480,640));f.Tick(TimeSpan.FromMilliseconds(40));Assert.Null(UiPolishOpeningFrame.Observe(f,Target(f),1,1));Assert.Null(UiPolishOpeningFrame.Observe(f,Target(f),0,1));
    }
    [Fact]public void FullRequiresActualFadeAndGeometryInSameEpoch()
    {
        var f=Frames();f.Open(360,new(480,640));f.Tick(TimeSpan.FromMilliseconds(40));var e=new UiPolishOpeningEvidence(1);
        e.Observe(f,Target(f),2,bodyReady:true);Assert.True(e.Complete);e.RequireComplete();Assert.Equal(e.Geometry!.Epoch,e.Fade!.Epoch);
    }
    [Fact]public void LegalOpaqueGeometryAloneStillFailsFullEvidence()
    {
        var f=Frames();f.Open(360,new(480,640));f.Tick(TimeSpan.FromMilliseconds(120));var e=new UiPolishOpeningEvidence(1);
        e.Observe(f,Target(f),2,bodyReady:true);Assert.NotNull(e.Geometry);Assert.Null(e.Fade);Assert.False(e.Complete);Assert.Throws<InvalidOperationException>(e.RequireComplete);
    }
    [Fact]public void UnacceptedBodyAndNoFramesCannotFillEvidenceOrPass()
    {
        var f=Frames();f.Open(360,new(480,640));f.Tick(TimeSpan.FromMilliseconds(40));var e=new UiPolishOpeningEvidence(1);
        e.Observe(f,Target(f),2,bodyReady:false);Assert.Null(e.Geometry);Assert.Null(e.Fade);Assert.Throws<InvalidOperationException>(e.RequireComplete);
    }
    [Fact]public void AnotherOpeningCannotCompletePriorEvidence()
    {
        var f=Frames();f.Open(360,new(480,640));f.Tick(TimeSpan.FromMilliseconds(120));var e=new UiPolishOpeningEvidence(1);e.Observe(f,Target(f),2,true);
        f.Open(420,new(480,640));f.Tick(TimeSpan.FromMilliseconds(20));Assert.Throws<InvalidOperationException>(()=>e.Observe(f,Target(f),3,true));Assert.False(e.Complete);
    }

}
