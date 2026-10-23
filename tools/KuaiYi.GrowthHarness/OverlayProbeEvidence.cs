using KuaiYi.Core;
using KuaiYi.App;

namespace KuaiYi.GrowthHarness;

internal sealed record OverlayProbeFrame(long Qpc,long RenderingTicks,int Phase,string Mode,
    OverlayTransition Transition,bool NeedsFrame,bool Applying,int ObserverDepth,long Hwnd,
    PixelRect Card,PixelRect Anchor,PixelRect Work,PixelRect? Native,PixelRect? Button,
    GrowthDirection? Direction,double Dpi,double BodyWidth,double ScrollOffset,bool ForegroundUnchanged,
    bool BodyVisible=true,double BodyHeight=100,double BodyOpacity=1,bool DocumentCurrent=true,
    long AnimationEpoch=0,TimeSpan AnimationStartedAt=default,OverlayFrameTick? AppliedTick=null,
    bool RequestCurrent=true,bool MeasurementCurrent=true);
internal sealed record OverlayProbeSample(int Sequence,OverlayProbeFrame Frame,bool DuplicateRenderingTime,
    bool QpcReversed,bool RenderingTimeReversed,bool StableGeometryValid,bool DuplicateStableRenderingTime)
{
    public bool Eligible=>!DuplicateStableRenderingTime&&!QpcReversed&&!RenderingTimeReversed&&
        !Frame.Applying&&Frame.ObserverDepth==1&&StableGeometryValid;
}
internal sealed record OverlayProbeMeasurement(int Sequence,int Phase,string Mode,OverlayMeasureObservation Measurement);
internal sealed class OverlayProbeJournal(int maximum=10000)
{
    private readonly HashSet<long> renderingTimes=[];
    private readonly HashSet<long> stableRenderingTimes=[];
    private long lastQpc=long.MinValue,lastRender=long.MinValue;
    private long? hwnd;private PixelRect? anchor;
    internal List<OverlayProbeSample> Samples{get;}=[];
    internal List<OverlayProbeMeasurement> Measurements{get;}=[];
    internal bool MeasurementOverflow{get;private set;}
    internal bool AddMeasurement(int phase,string mode,OverlayMeasureObservation value)
    {
        if(Measurements.Count>=512){MeasurementOverflow=true;return false;}
        Measurements.Add(new(Measurements.Count+1,phase,mode,value));return true;
    }
    internal bool HasCurrentMeasurement(Guid version,int length)
        =>Measurements.LastOrDefault()?.Measurement.Markdown is {} value&&value.CurrentAccepted&&
          value.VersionBefore==version&&value.VersionAfter==version&&value.TextLengthBefore==length&&value.TextLengthAfter==length;
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
    OverlayTransition intermediate,OverlayTransition terminal,OverlayProbeGoal goal,string? documentMarker=null,int? prerequisitePhaseId=null)
{
    public int Id{get;}=id;public string Mode{get;}=mode;public string Name{get;}=name;
    public long StartedQpc{get;}=started;public PixelRect Baseline{get;}=baseline;
    public OverlayTransition Intermediate{get;}=intermediate;public OverlayTransition Terminal{get;}=terminal;
    public OverlayProbeGoal Goal{get;}=goal;
    public string? DocumentMarker{get;}=documentMarker;
    public int? PrerequisitePhaseId{get;}=prerequisitePhaseId;
    private readonly HashSet<long> intermediateEpochs=[];
    public bool IssuanceComplete{get;private set;}
    public bool SawIntermediate=>IssuedAnimationEpoch is {} epoch?intermediateEpochs.Contains(epoch):intermediateEpochs.Count>0;
    public long? FirstGeometryChangeQpc{get;private set;}public long? CompletedQpc{get;private set;}
    public long? IssuedQpc{get;private set;}public long? IssuedAnimationEpoch{get;private set;}
    public long? FailedQpc{get;private set;}public long? SkippedQpc{get;private set;}
    public string? FailureReason{get;private set;}
    public bool Finished=>CompletedQpc.HasValue||FailedQpc.HasValue||SkippedQpc.HasValue;
    public string Outcome=>CompletedQpc.HasValue?"Passed":FailedQpc.HasValue?"Failed":SkippedQpc.HasValue?"NotRun":"Pending";
    public int EligibleSamples{get;private set;}public long? LastEligibleQpc{get;private set;}
    public List<long> GapsQpc{get;}=[];
    internal void Issued(long? qpc=null,long animationEpoch=0)
    {IssuanceComplete=true;IssuedQpc=qpc??StartedQpc;IssuedAnimationEpoch=animationEpoch;}
    internal void Skip(long qpc)
    {if(Finished||IssuanceComplete)throw new InvalidOperationException("Issued or finished phase cannot be skipped.");SkippedQpc=qpc;FailureReason="PrerequisiteInvalid";}
    internal static bool CanIssueCommand(OverlayTransition required,OverlayTransition actual,bool needs,PixelRect current,PixelRect anchor,OverlayProbePhase? prerequisite,long animationEpoch)
    {
        if(actual!=required)return false;
        if(required is OverlayTransition.Opening or OverlayTransition.Closing)
            return needs&&prerequisite is {Goal:OverlayProbeGoal.Intermediate,CompletedQpc:not null}&&
                prerequisite.Intermediate==required&&prerequisite.IssuedAnimationEpoch==animationEpoch&&current!=prerequisite.Baseline&&current!=anchor;
        return !needs&&(required!=OverlayTransition.Compact||current==anchor);
    }
    internal bool Observe(OverlayProbeSample sample)
    {
        var f=sample.Frame;
        if(!sample.Eligible||!f.RequestCurrent||!f.MeasurementCurrent||f.Phase!=Id||f.Mode!=Mode||f.Qpc<StartedQpc||Finished)return false;
        if(LastEligibleQpc is {} previous)GapsQpc.Add(f.Qpc-previous);
        LastEligibleQpc=f.Qpc;EligibleSamples++;
        if(f.Card!=Baseline)FirstGeometryChangeQpc??=f.Qpc;
        bool moving=f.Transition==Intermediate&&f.NeedsFrame&&f.Card!=Baseline;
        if(moving)intermediateEpochs.Add(f.AnimationEpoch);
        bool textVisible=Terminal!=OverlayTransition.Expanded||f.BodyVisible&&f.BodyWidth>0&&f.BodyHeight>0&&f.BodyOpacity>=.99&&f.DocumentCurrent;
        bool issued=IssuanceComplete&&IssuedQpc is {} issuedAt&&IssuedAnimationEpoch is {} issuedEpoch&&f.Qpc>=issuedAt&&f.AnimationEpoch==issuedEpoch;
        bool terminal=f.Transition==Terminal&&!f.NeedsFrame&&textVisible&&(Terminal!=OverlayTransition.Compact||f.Card==f.Anchor);
        bool done=issued&&SawIntermediate&&(Goal==OverlayProbeGoal.Intermediate?moving:terminal);
        if(done)CompletedQpc=f.Qpc;
        else if(issued&&terminal)
        {FailedQpc=f.Qpc;FailureReason=Goal==OverlayProbeGoal.Intermediate?"IntermediateCommandWindowMissed":"ChangedIntermediateNotObserved";}
        return done;
    }
}
internal sealed class OverlayRasterBudget(int maximumFrames=24,long maximumBytes=64*1024*1024)
{
    private readonly HashSet<(int Phase,long Epoch,string Kind)> intermediateClaims=[];
    internal bool ClaimIntermediate(OverlayProbePhase phase,OverlayProbeSample sample)
    {
        var f=sample.Frame;
        return sample.Eligible&&f.RequestCurrent&&f.MeasurementCurrent&&f.Phase==phase.Id&&f.Mode=="Raster"&&phase.Mode=="Raster"&&
            f.Qpc>=phase.StartedQpc&&f.Transition==phase.Intermediate&&f.NeedsFrame&&f.Card!=phase.Baseline&&
            (!phase.IssuedAnimationEpoch.HasValue||phase.IssuedAnimationEpoch==f.AnimationEpoch)&&
            intermediateClaims.Add((phase.Id,f.AnimationEpoch,"intermediate"));
    }
    internal static bool MatchesIssuedEpoch(OverlayProbePhase phase,OverlayProbeSample before,OverlayProbeFrame after)
        =>phase.IssuedAnimationEpoch is {} epoch&&before.Eligible&&before.Frame.RequestCurrent&&before.Frame.MeasurementCurrent&&
          after.RequestCurrent&&after.MeasurementCurrent&&before.Frame.Phase==phase.Id&&after.Phase==phase.Id&&
          before.Frame.Mode==phase.Mode&&after.Mode==phase.Mode&&before.Frame.AnimationEpoch==epoch&&after.AnimationEpoch==epoch;
    internal int Frames{get;private set;}internal long Bytes{get;private set;}
    internal void Reserve(int width,int height)
    {
        long pixels=checked((long)width*height),bytes=checked(pixels*4);
        if(width<=0||height<=0||pixels>4_000_000||Frames>=maximumFrames||bytes>maximumBytes-Bytes)
            throw new InvalidOperationException("Original raster frame/memory bound exceeded.");
        Frames++;Bytes+=bytes;
    }
}
