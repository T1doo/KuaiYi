using System.Diagnostics;
using KuaiYi.Core;

namespace KuaiYi.App;

// Same monotonic clock for commands and rendered ticks. Measuring content before
// Begin must not spend the new animation's time; real stalls after Begin still do.
internal sealed class OverlayFrameClock(Func<TimeSpan>? read=null)
{
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private TimeSpan last;
    private long sequence,epoch;
    private bool beginPending;
    internal TimeSpan Now=>read?.Invoke()??clock.Elapsed;
    internal long AnimationEpoch=>epoch;
    internal TimeSpan AnimationStartedAt{get;private set;}
    internal OverlayFrameTick? LastTick{get;private set;}
    internal void Resume(){if(!beginPending)last=Now;}
    internal void Begin(){AnimationStartedAt=last=Now;epoch++;beginPending=true;}
    internal OverlayFrameTick Tick(OverlayFrames frames,bool reduceMotion)
    {
        var now=Now;var delta=now-last;var before=frames.Transition;
        frames.Tick(delta,reduceMotion);
        var tick=new OverlayFrameTick(++sequence,epoch,AnimationStartedAt,last,now,delta,before,frames.Transition);
        last=now;beginPending=false;LastTick=tick;return tick;
    }
}

internal sealed record OverlayFrameTick(long Sequence,long AnimationEpoch,TimeSpan AnimationStartedAt,
    TimeSpan From,TimeSpan Now,TimeSpan Delta,OverlayTransition Before,OverlayTransition After);
