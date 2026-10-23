using KuaiYi.Core;
using KuaiYi.App;

namespace KuaiYi.GrowthHarness;

internal sealed record OverlayProbeFrame(long Qpc,long RenderingTicks,int Phase,string Mode,
    OverlayTransition Transition,bool NeedsFrame,bool Applying,int ObserverDepth,long Hwnd,
    PixelRect Card,PixelRect Anchor,PixelRect Work,PixelRect? Native,PixelRect? Button,
    GrowthDirection? Direction,double Dpi,double BodyWidth,double ScrollOffset,bool ForegroundUnchanged,
    bool BodyVisible=true,double BodyHeight=100,double BodyOpacity=1,bool DocumentCurrent=true,
    long AnimationEpoch=0,TimeSpan AnimationStartedAt=default,OverlayFrameTick? AppliedTick=null);
internal sealed record OverlayProbeSample(int Sequence,OverlayProbeFrame Frame,bool DuplicateRenderingTime,
    bool QpcReversed,bool RenderingTimeReversed,bool StableGeometryValid,bool DuplicateStableRenderingTime)
{
    public bool Eligible=>!DuplicateStableRenderingTime&&!QpcReversed&&!RenderingTimeReversed&&
        !Frame.Applying&&Frame.ObserverDepth==1&&StableGeometryValid;
}
internal sealed class OverlayProbeJournal(int maximum=10000)
{
    private readonly HashSet<long> renderingTimes=[];
    private readonly HashSet<long> stableRenderingTimes=[];
    private long lastQpc=long.MinValue,lastRender=long.MinValue;
    private long? hwnd;private PixelRect? anchor;
    internal List<OverlayProbeSample> Samples{get;}=[];
    internal OverlayProbeSample Add(OverlayProbeFrame frame)
    {
        if(Samples.Count>=maximum)throw new InvalidOperationException("Raw callback bound exceeded; no samples discarded.");
        hwnd??=frame.Hwnd;anchor??=frame.Anchor;
        bool valid=frame.Hwnd!=0&&frame.Hwnd==hwnd&&frame.Anchor==anchor&&frame.Card.Contains(frame.Anchor)&&
            frame.Work.Contains(frame.Card)&&frame.Native==frame.Card&&frame.Button==frame.Anchor&&frame.ForegroundUnchanged&&
            double.IsFinite(frame.BodyWidth)&&double.IsFinite(frame.BodyHeight)&&double.IsFinite(frame.BodyOpacity)&&
            double.IsFinite(frame.ScrollOffset)&&frame.Dpi>0&&double.IsFinite(frame.Dpi);
        bool stable=valid&&!frame.Applying&&frame.ObserverDepth==1&&frame.Qpc>=lastQpc&&frame.RenderingTicks>=lastRender;
        var sample=new OverlayProbeSample(Samples.Count+1,frame,!renderingTimes.Add(frame.RenderingTicks),
            frame.Qpc<lastQpc,frame.RenderingTicks<lastRender,valid,stable&&!stableRenderingTimes.Add(frame.RenderingTicks));
        Samples.Add(sample);lastQpc=Math.Max(lastQpc,frame.Qpc);lastRender=Math.Max(lastRender,frame.RenderingTicks);
        return sample;
    }
}
internal enum OverlayProbeGoal{Intermediate,Settled}
internal sealed class OverlayProbePhase(int id,string mode,string name,long started,PixelRect baseline,
    OverlayTransition intermediate,OverlayTransition terminal,OverlayProbeGoal goal,string? documentMarker=null)
{
    public int Id{get;}=id;public string Mode{get;}=mode;public string Name{get;}=name;
    public long StartedQpc{get;}=started;public PixelRect Baseline{get;}=baseline;
    public OverlayTransition Intermediate{get;}=intermediate;public OverlayTransition Terminal{get;}=terminal;
    public OverlayProbeGoal Goal{get;}=goal;
    public string? DocumentMarker{get;}=documentMarker;
    public bool IssuanceComplete{get;private set;}public bool SawIntermediate{get;private set;}
    public long? FirstGeometryChangeQpc{get;private set;}public long? CompletedQpc{get;private set;}
    public int EligibleSamples{get;private set;}public long? LastEligibleQpc{get;private set;}
    public List<long> GapsQpc{get;}=[];
    internal void Issued()=>IssuanceComplete=true;
    internal bool Observe(OverlayProbeSample sample)
    {
        var f=sample.Frame;
        if(!sample.Eligible||f.Phase!=Id||f.Mode!=Mode||f.Qpc<StartedQpc||CompletedQpc.HasValue)return false;
        if(LastEligibleQpc is {} previous)GapsQpc.Add(f.Qpc-previous);
        LastEligibleQpc=f.Qpc;EligibleSamples++;
        if(f.Card!=Baseline)FirstGeometryChangeQpc??=f.Qpc;
        bool moving=f.Transition==Intermediate&&f.NeedsFrame&&f.Card!=Baseline;
        SawIntermediate|=moving;
        bool textVisible=Terminal!=OverlayTransition.Expanded||f.BodyVisible&&f.BodyWidth>0&&f.BodyHeight>0&&f.BodyOpacity>=.99&&f.DocumentCurrent;
        bool done=IssuanceComplete&&SawIntermediate&&(Goal==OverlayProbeGoal.Intermediate?moving:
            f.Transition==Terminal&&!f.NeedsFrame&&textVisible&&(Terminal!=OverlayTransition.Compact||f.Card==f.Anchor));
        if(done)CompletedQpc=f.Qpc;
        return done;
    }
}
internal sealed class OverlayRasterBudget(int maximumFrames=24,long maximumBytes=64*1024*1024)
{
    internal int Frames{get;private set;}internal long Bytes{get;private set;}
    internal void Reserve(int width,int height)
    {
        long pixels=checked((long)width*height),bytes=checked(pixels*4);
        if(width<=0||height<=0||pixels>4_000_000||Frames>=maximumFrames||bytes>maximumBytes-Bytes)
            throw new InvalidOperationException("Original raster frame/memory bound exceeded.");
        Frames++;Bytes+=bytes;
    }
}
