using KuaiYi.Core;
using KuaiYi.App;
using KuaiYi.GrowthHarness;
using Xunit;

namespace KuaiYi.Provider.Tests;

public sealed class OverlayProbeEvidenceTests
{
    [Fact]
    public void MeasurementBoundIsExplicitAndCannotChangeFrameOrStageEvidence()
    {
        var journal=new OverlayProbeJournal();var state=new MarkdownMeasureState(true,false,388,296,629,296,1,25);
        var observation=new OverlayMeasureObservation(19,new(Guid.NewGuid(),null,21,21,new(388,296),state,state,true),80,376,376,376,1);
        for(int i=0;i<512;i++)Assert.True(journal.AddMeasurement(10,"Raster",observation));
        Assert.False(journal.MeasurementOverflow);Assert.Empty(journal.Samples);
        Assert.False(journal.AddMeasurement(11,"Raster",observation));Assert.True(journal.MeasurementOverflow);
        Assert.False(journal.AddMeasurement(11,"Raster",observation));Assert.Equal(512,journal.Measurements.Count);
        Assert.Equal(512,journal.Measurements[^1].Sequence);Assert.Equal(10,journal.Measurements[^1].Phase);
        Assert.Same(observation,journal.Measurements[0].Measurement);Assert.Equal("Raster",journal.Measurements[0].Mode);
    }
    private static readonly PixelRect Anchor=new(20,20,68,68),Work=new(0,0,1000,800),Half=new(20,20,180,160),Full=new(20,20,420,300);
    private static OverlayProbeFrame Frame(long qpc,long render,PixelRect card,OverlayTransition transition=OverlayTransition.Opening,bool needs=true,int phase=1)
        =>new(qpc,render,phase,"Timing",transition,needs,false,1,17,card,Anchor,Work,card,Anchor,GrowthDirection.RightDown,1,300,0,true);

    [Fact]
    public void DuplicateAndReentrantCallbacksAreRetainedButCannotInflateCoverage()
    {
        var journal=new OverlayProbeJournal();
        Assert.True(journal.Add(Frame(1,10,Half)).Eligible);
        Assert.False(journal.Add(Frame(2,10,Full)).Eligible);
        Assert.False(journal.Add(Frame(3,11,Half) with{Applying=true,Native=Anchor,ObserverDepth=2}).Eligible);
        Assert.Equal(3,journal.Samples.Count);
        Assert.True(journal.Samples[1].DuplicateRenderingTime);
        Assert.False(journal.Samples[2].StableGeometryValid);
        Assert.False(journal.Add(Frame(4,11,Half) with{Native=Anchor}).StableGeometryValid); // Duplicate cannot hide stable mismatch.
    }
    [Fact]
    public void ReversedClocksAndOverflowNeverSilentlyDropOrAcceptEvidence()
    {
        var journal=new OverlayProbeJournal(3);journal.Add(Frame(10,20,Half));
        var reversed=journal.Add(Frame(9,19,Half));Assert.True(reversed.QpcReversed);Assert.True(reversed.RenderingTimeReversed);Assert.False(reversed.Eligible);
        journal.Add(Frame(11,21,Full));Assert.Throws<InvalidOperationException>(()=>journal.Add(Frame(12,22,Full)));
        Assert.Equal(3,journal.Samples.Count);
    }
    [Fact]
    public void ApplyingCallbackCannotConsumeTheOneStableObservationForItsTargetTime()
    {
        var journal=new OverlayProbeJournal();Assert.False(journal.Add(Frame(1,10,Half) with{Applying=true}).Eligible);
        var stable=journal.Add(Frame(2,10,Half));Assert.True(stable.DuplicateRenderingTime);Assert.True(stable.Eligible);
        Assert.False(journal.Add(Frame(3,10,Half)).Eligible);Assert.Equal(3,journal.Samples.Count);
    }
    [Fact]
    public void HiddenOrUnflushedTextCannotTurnSettledGeometryIntoAnExpandedPass()
    {
        var phase=new OverlayProbePhase(1,"Timing","Open",0,Anchor,OverlayTransition.Opening,OverlayTransition.Expanded,OverlayProbeGoal.Settled);phase.Issued();
        var journal=new OverlayProbeJournal();phase.Observe(journal.Add(Frame(1,1,Half)));
        Assert.False(phase.Observe(journal.Add(Frame(2,2,Full,OverlayTransition.Expanded,false) with{BodyVisible=false})));
        Assert.False(phase.Observe(journal.Add(Frame(3,3,Full,OverlayTransition.Expanded,false) with{BodyOpacity=0})));
        Assert.False(phase.Observe(journal.Add(Frame(4,4,Full,OverlayTransition.Expanded,false) with{BodyHeight=0})));
        Assert.False(phase.Observe(journal.Add(Frame(5,5,Full,OverlayTransition.Expanded,false) with{DocumentCurrent=false})));
        Assert.True(phase.Observe(journal.Add(Frame(6,6,Full,OverlayTransition.Expanded,false))));
    }
    [Fact]
    public void ManyTerminalCallbacksCannotReplaceAnObservedIntermediateAnimation()
    {
        var journal=new OverlayProbeJournal();var phase=new OverlayProbePhase(1,"Timing","Open",0,Anchor,OverlayTransition.Opening,OverlayTransition.Expanded,OverlayProbeGoal.Settled);phase.Issued();
        for(int i=1;i<=210;i++)Assert.False(phase.Observe(journal.Add(Frame(i,i,Full,OverlayTransition.Expanded,false))));
        Assert.False(phase.SawIntermediate);Assert.Null(phase.CompletedQpc);
    }
    [Fact]
    public void ProductionFramesMustSettleAfterIssuanceAndRealIntermediateGeometry()
    {
        var geometry=new OverlayFrames(Anchor,new("synthetic",Work,new(1)));geometry.Open(260,new(400,600));
        var phase=new OverlayProbePhase(1,"Timing","Open",0,Anchor,OverlayTransition.Opening,OverlayTransition.Expanded,OverlayProbeGoal.Settled);
        var journal=new OverlayProbeJournal();
        geometry.Tick(TimeSpan.FromMilliseconds(80));
        Assert.False(phase.Observe(journal.Add(Frame(80,80,geometry.Current,geometry.Transition,geometry.NeedsFrame))));
        Assert.True(phase.SawIntermediate);
        geometry.Tick(TimeSpan.FromMilliseconds(140));
        Assert.False(phase.Observe(journal.Add(Frame(220,220,geometry.Current,geometry.Transition,geometry.NeedsFrame))));
        phase.Issued();Assert.True(phase.Observe(journal.Add(Frame(230,230,geometry.Current,geometry.Transition,geometry.NeedsFrame))));
        Assert.Equal(80,phase.FirstGeometryChangeQpc);Assert.Equal(230,phase.CompletedQpc);
    }
    [Fact]
    public void GrowthNeedsFrameAndStalePhaseCannotSatisfyAReverseTerminal()
    {
        var phase=new OverlayProbePhase(2,"Timing","Reverse",10,Half,OverlayTransition.Opening,OverlayTransition.Expanded,OverlayProbeGoal.Settled);phase.Issued();
        var journal=new OverlayProbeJournal();
        Assert.False(phase.Observe(journal.Add(Frame(11,11,Full,phase:1))));Assert.False(phase.SawIntermediate);
        Assert.False(phase.Observe(journal.Add(Frame(12,12,new(20,20,240,200),phase:2))));Assert.True(phase.SawIntermediate);
        Assert.False(phase.Observe(journal.Add(Frame(13,13,Full,OverlayTransition.Expanded,true,2))));
        Assert.True(phase.Observe(journal.Add(Frame(14,14,Full,OverlayTransition.Expanded,false,2))));
    }
    [Fact]
    public void ReversalTriggerRequiresAnActualChangedClosingFrame()
    {
        var phase=new OverlayProbePhase(1,"Timing","CloseUntilReverse",0,Full,OverlayTransition.Closing,OverlayTransition.Compact,OverlayProbeGoal.Intermediate);phase.Issued();
        var journal=new OverlayProbeJournal();
        Assert.False(phase.Observe(journal.Add(Frame(1,1,Full,OverlayTransition.Closing))));
        Assert.False(phase.Observe(journal.Add(Frame(2,1,Half,OverlayTransition.Closing))));
        Assert.True(phase.Observe(journal.Add(Frame(3,2,Half,OverlayTransition.Closing))));
    }
    [Fact]
    public void RasterMemoryAndPixelLimitsFailBeforeReservingAnExtraFrame()
    {
        var budget=new OverlayRasterBudget(2,32);budget.Reserve(2,2);budget.Reserve(2,2);
        Assert.Throws<InvalidOperationException>(()=>budget.Reserve(1,1));Assert.Equal(2,budget.Frames);Assert.Equal(32,budget.Bytes);
        Assert.Throws<InvalidOperationException>(()=>new OverlayRasterBudget().Reserve(4001,1000));
        Assert.Throws<InvalidOperationException>(()=>new OverlayRasterBudget().Reserve(0,1));
        Assert.Throws<OverflowException>(()=>new OverlayRasterBudget().Reserve(int.MaxValue,int.MaxValue));
    }
    [Fact]
    public async Task OwnerPublishesBeforeStartAndCannotCleanUpBeforeBothOriginalCloseObservations()
    {
        var bodyEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var originalClose=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);bool cleanup=false;
        OverlayProbeLifetime? owner=null;
        owner=new(()=>{Assert.NotNull(owner!.Original);bodyEntered.SetResult();return Task.CompletedTask;},async()=>{closeEntered.SetResult();return await owner!.JoinClose(originalClose.Task,closed.Task);},()=>cleanup=true);
        Assert.False(bodyEntered.Task.IsCompleted);owner.Release();await closeEntered.Task;
        originalClose.SetResult();Assert.False(owner.Original.IsCompleted);Assert.False(cleanup);
        closed.SetResult();await owner.Original;Assert.True(cleanup);Assert.True(owner.CleanupConfirmed);Assert.Empty(owner.Failures);
    }
    [Fact]
    public async Task BodyAndBothCloseFailuresRemainDistinctAndPreventStateDeletion()
    {
        bool cleanup=false;OverlayProbeLifetime? owner=null;
        owner=new(()=>Task.FromException(new IOException("body-original")),()=>owner!.JoinClose(Task.FromException(new IOException("close-original")),Task.FromException(new IOException("closed-original"))),()=>cleanup=true);
        owner.Release();await owner.Original;
        Assert.False(cleanup);Assert.False(owner.CloseConfirmed);Assert.False(owner.CleanupConfirmed);
        Assert.Contains(owner.Failures,f=>f.Stage=="Body"&&f.Error.Contains("body-original"));
        Assert.Contains(owner.Failures,f=>f.Stage=="OriginalCloseTask"&&f.Error.Contains("close-original"));
        Assert.Contains(owner.Failures,f=>f.Stage=="ClosedEvent"&&f.Error.Contains("closed-original"));
    }
    [Fact]
    public async Task CancellationStillJoinsOriginalCloseAndCleanupFailureCannotBecomeSuccess()
    {
        var closed=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner=new OverlayProbeLifetime(()=>Task.FromCanceled(new CancellationToken(true)),()=>closed.Task,()=>throw new IOException("cleanup-original"));
        owner.Release();Assert.False(owner.Original.IsCompleted);closed.SetResult(true);await owner.Original;
        Assert.True(owner.CloseConfirmed);Assert.False(owner.CleanupConfirmed);
        Assert.Contains(owner.Failures,f=>f.Stage=="Body");Assert.Contains(owner.Failures,f=>f.Stage=="Cleanup"&&f.Error.Contains("cleanup-original"));
    }
}
