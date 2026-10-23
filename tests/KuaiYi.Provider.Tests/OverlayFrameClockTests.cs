using KuaiYi.App;
using KuaiYi.Core;
using Xunit;

namespace KuaiYi.Provider.Tests;

public sealed class OverlayFrameClockTests
{
    private static readonly LayoutPreferences Preferences=new(420,640);
    private sealed class Fixture
    {
        internal TimeSpan Now;
        internal readonly OverlayFrameClock Clock;
        internal readonly OverlayFrames Frames;
        internal Fixture()
        {
            Clock=new(()=>Now);
            Frames=new(new(488,340,536,388),new("primary",new(0,0,1024,728),new(1)))
                {AnimationStarted=Clock.Begin};
            Clock.Resume();
        }
        internal OverlayFrameTick Tick(int milliseconds){Now+=TimeSpan.FromMilliseconds(milliseconds);return Clock.Tick(Frames,false);}
    }
    [Fact]public void MeasuredOpenDoesNotSpendTimeFromTheAlreadyRunningClosingClock()
    {
        var f=new Fixture();f.Frames.Collapse();f.Tick(36);
        f.Frames.OpenMeasured(Preferences,_=>{f.Now+=TimeSpan.FromMilliseconds(409);return 113;});
        var begin=f.Now;var tick=f.Tick(20);
        Assert.Equal(TimeSpan.FromMilliseconds(20),tick.Delta);
        Assert.Equal(begin,tick.AnimationStartedAt);
        Assert.Equal(OverlayTransition.Opening,f.Frames.Transition);
        Assert.NotEqual(f.Frames.Anchor,f.Frames.Current);
        Assert.True(f.Frames.Current.Width<420);
        f.Tick(200);Assert.Equal(OverlayTransition.Expanded,f.Frames.Transition);
    }
    [Fact]public void ReversalBeginsAtTheCurrentEdgesAndDoesNotSpendThePreviousPhaseTime()
    {
        var f=new Fixture();f.Frames.Open(300,Preferences);f.Tick(40);var shown=f.Frames.Current;
        f.Now+=TimeSpan.FromMilliseconds(300);f.Frames.Collapse();
        Assert.Equal(shown,f.Frames.Current);var close=f.Tick(20);
        Assert.Equal(TimeSpan.FromMilliseconds(20),close.Delta);Assert.Equal(OverlayTransition.Closing,f.Frames.Transition);
        shown=f.Frames.Current;f.Now+=TimeSpan.FromMilliseconds(300);f.Frames.Open(320,Preferences);
        Assert.Equal(shown,f.Frames.Current);var open=f.Tick(20);
        Assert.Equal(TimeSpan.FromMilliseconds(20),open.Delta);Assert.Equal(OverlayTransition.Opening,f.Frames.Transition);
        Assert.True(open.AnimationEpoch>close.AnimationEpoch);
    }
    [Fact]public void GrowthDoesNotRestartTheClockAndRealPostBeginStallsAreNotClamped()
    {
        var f=new Fixture();f.Frames.Open(113,Preferences);var first=f.Tick(220);
        Assert.Equal(OverlayTransition.Expanded,f.Frames.Transition);
        f.Now+=TimeSpan.FromMilliseconds(30);f.Frames.Grow(250);var growth=f.Tick(20);
        Assert.Equal(TimeSpan.FromMilliseconds(50),growth.Delta);Assert.Equal(first.AnimationEpoch,growth.AnimationEpoch);
        Assert.InRange(f.Frames.Current.Height,114,249);
        f.Frames.Collapse();var stalled=f.Tick(409);
        Assert.Equal(TimeSpan.FromMilliseconds(409),stalled.Delta);Assert.Equal(OverlayTransition.Compact,f.Frames.Transition);
    }
    [Fact]public void RefusedMeasurementDoesNotResetAnExistingAnimation()
    {
        var f=new Fixture();f.Frames.Open(250,Preferences);var first=f.Tick(10);bool current=true;
        f.Frames.OpenMeasured(Preferences,_=>{f.Now+=TimeSpan.FromMilliseconds(20);current=false;return 400;},()=>current);
        var next=f.Tick(10);Assert.Equal(TimeSpan.FromMilliseconds(30),next.Delta);
        Assert.Equal(first.AnimationEpoch,next.AnimationEpoch);Assert.Equal(first.AnimationStartedAt,next.AnimationStartedAt);
    }
    [Fact]public void DelayedSubscriptionPreservesTheActualBeginTimeAndOldTickIsIdentifiable()
    {
        var f=new Fixture();var old=f.Tick(16);f.Frames.Open(113,Preferences);
        var epoch=f.Clock.AnimationEpoch;Assert.NotEqual(old.AnimationEpoch,epoch);
        Assert.Same(old,f.Clock.LastTick); // A sample before the first new tick can identify the stale epoch.
        f.Now+=TimeSpan.FromMilliseconds(80);f.Clock.Resume();var tick=f.Tick(20);
        Assert.Equal(TimeSpan.FromMilliseconds(100),tick.Delta);Assert.Equal(epoch,tick.AnimationEpoch);
        Assert.Equal(OverlayTransition.Opening,f.Frames.Transition);
    }
    [Fact]public void IdleGrowthExcludesIdleTimeWhileReducedMotionStillSettlesImmediately()
    {
        var f=new Fixture();f.Frames.Open(113,Preferences);f.Tick(220);
        f.Now+=TimeSpan.FromSeconds(5);f.Frames.Grow(250);f.Clock.Resume();var tick=f.Tick(20);
        Assert.Equal(TimeSpan.FromMilliseconds(20),tick.Delta);Assert.InRange(f.Frames.Current.Height,114,249);
        f.Frames.Collapse();f.Clock.Tick(f.Frames,true);
        Assert.Equal(OverlayTransition.Compact,f.Frames.Transition);Assert.Equal(f.Frames.Anchor,f.Frames.Current);
    }
}
