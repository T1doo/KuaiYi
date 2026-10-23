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
        Assert.Equal(1,phase.FailedQpc);Assert.True(phase.Finished);Assert.Equal("Failed",phase.Outcome);
    }
    [Fact]
    public void FailedTerminalIsStickyAndOnlyCurrentIssuedEpochCanEndWaiting()
    {
        var phase=new OverlayProbePhase(1,"Timing","Open",10,Anchor,OverlayTransition.Opening,OverlayTransition.Expanded,OverlayProbeGoal.Settled);
        var journal=new OverlayProbeJournal();
        Assert.False(phase.Observe(journal.Add(Frame(11,11,Full,OverlayTransition.Expanded,false))));Assert.False(phase.Finished);
        phase.Issued(20,4);
        Assert.False(phase.Observe(journal.Add(Frame(19,12,Full,OverlayTransition.Expanded,false) with{AnimationEpoch=4})));Assert.False(phase.Finished);
        Assert.False(phase.Observe(journal.Add(Frame(21,13,Full,OverlayTransition.Expanded,false) with{AnimationEpoch=3})));Assert.False(phase.Finished);
        Assert.False(phase.Observe(journal.Add(Frame(22,14,Full,OverlayTransition.Expanded,false) with{AnimationEpoch=4,MeasurementCurrent=false})));Assert.False(phase.Finished);
        Assert.False(phase.Observe(journal.Add(Frame(23,15,Full,OverlayTransition.Expanded,false) with{AnimationEpoch=4,RequestCurrent=false})));Assert.False(phase.Finished);
        Assert.False(phase.Observe(journal.Add(Frame(24,16,Full,OverlayTransition.Expanded,false) with{AnimationEpoch=4,Applying=true})));Assert.False(phase.Finished);
        Assert.False(phase.Observe(journal.Add(Frame(25,16,Full,OverlayTransition.Expanded,false) with{AnimationEpoch=4,DocumentCurrent=false})));Assert.False(phase.Finished);
        Assert.False(phase.Observe(journal.Add(Frame(26,17,Full,OverlayTransition.Expanded,false) with{AnimationEpoch=4})));
        Assert.Equal(26,phase.FailedQpc);Assert.Null(phase.CompletedQpc);
        Assert.False(phase.Observe(journal.Add(Frame(27,18,Half) with{AnimationEpoch=4})));
        Assert.False(phase.Observe(journal.Add(Frame(28,19,Full,OverlayTransition.Expanded,false) with{AnimationEpoch=4})));
        Assert.Equal(26,phase.FailedQpc);Assert.Null(phase.CompletedQpc);Assert.False(phase.SawIntermediate);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void IntermediateFromAnotherAnimationCannotAuthorizeTheCurrentTerminal(bool duringIssue)
    {
        var phase=new OverlayProbePhase(1,"Timing","Open",10,Anchor,OverlayTransition.Opening,OverlayTransition.Expanded,OverlayProbeGoal.Settled);
        var journal=new OverlayProbeJournal();if(!duringIssue)phase.Issued(20,4);
        Assert.False(phase.Observe(journal.Add(Frame(duringIssue?11:21,11,Half) with{AnimationEpoch=3})));
        if(duringIssue)phase.Issued(20,4);
        Assert.False(phase.Observe(journal.Add(Frame(22,12,Full,OverlayTransition.Expanded,false) with{AnimationEpoch=4})),"Another animation's intermediate must not turn this terminal green.");
        Assert.False(phase.SawIntermediate);Assert.Null(phase.CompletedQpc);Assert.Equal(22,phase.FailedQpc);
    }
    [Fact]
    public void MissedReverseWindowSkipsDependentCommandsButAllowsIndependentCompactStart()
    {
        var phase=new OverlayProbePhase(5,"Timing","CloseUntilReverse",0,Full,OverlayTransition.Closing,OverlayTransition.Compact,OverlayProbeGoal.Intermediate);
        var journal=new OverlayProbeJournal();
        Assert.False(phase.Observe(journal.Add(Frame(1,1,Half,OverlayTransition.Closing,true,5))));Assert.True(phase.SawIntermediate);
        phase.Issued();Assert.False(phase.Observe(journal.Add(Frame(2,2,Anchor,OverlayTransition.Compact,false,5))));
        Assert.Equal("IntermediateCommandWindowMissed",phase.FailureReason);Assert.Null(phase.CompletedQpc);
        int clicks=0;
        var reverse=new OverlayProbePhase(6,"Timing","ReverseToOpen",3,Anchor,OverlayTransition.Opening,OverlayTransition.Expanded,OverlayProbeGoal.Settled,prerequisitePhaseId:5);
        if(OverlayProbePhase.CanIssueCommand(OverlayTransition.Closing,OverlayTransition.Compact,false,Anchor,Anchor,phase,0))clicks++;else reverse.Skip(3);
        var close=new OverlayProbePhase(7,"Timing","CloseToCompact",4,Anchor,OverlayTransition.Closing,OverlayTransition.Compact,OverlayProbeGoal.Settled,prerequisitePhaseId:6);
        if(OverlayProbePhase.CanIssueCommand(OverlayTransition.Expanded,OverlayTransition.Compact,false,Anchor,Anchor,reverse,0))clicks++;else close.Skip(4);
        Assert.Equal(0,clicks);Assert.Equal("NotRun",reverse.Outcome);Assert.Equal("NotRun",close.Outcome);
        Assert.False(reverse.IssuanceComplete);Assert.Null(reverse.CompletedQpc);Assert.Equal(5,reverse.PrerequisitePhaseId);
        Assert.True(OverlayProbePhase.CanIssueCommand(OverlayTransition.Compact,OverlayTransition.Compact,false,Anchor,Anchor,close,0));
        Assert.Throws<InvalidOperationException>(()=>reverse.Skip(5));
    }
    [Fact]
    public void SuccessfulIntermediateStillRequiresALiveWindowBeforeReverseClick()
    {
        var phase=new OverlayProbePhase(1,"Timing","CloseUntilReverse",0,Full,OverlayTransition.Closing,OverlayTransition.Compact,OverlayProbeGoal.Intermediate);
        phase.Issued();Assert.True(phase.Observe(new OverlayProbeJournal().Add(Frame(1,1,Half,OverlayTransition.Closing))));
        Assert.True(OverlayProbePhase.CanIssueCommand(OverlayTransition.Closing,OverlayTransition.Closing,true,Half,Anchor,phase,0));
        Assert.False(OverlayProbePhase.CanIssueCommand(OverlayTransition.Closing,OverlayTransition.Closing,true,Half,Anchor,phase,1));
        Assert.False(OverlayProbePhase.CanIssueCommand(OverlayTransition.Closing,OverlayTransition.Compact,false,Anchor,Anchor,phase,0));
        Assert.False(OverlayProbePhase.CanIssueCommand(OverlayTransition.Closing,OverlayTransition.Closing,true,Full,Anchor,phase,0));
        Assert.False(OverlayProbePhase.CanIssueCommand(OverlayTransition.Closing,OverlayTransition.Closing,true,Half,Anchor,null,0));
    }
    [Fact]
    public void LatestMeasurementMustBindBothVersionsAndCompleteReceivedLength()
    {
        var id=Guid.NewGuid();var state=new MarkdownMeasureState(true,false,388,33,0,0,1,26);var journal=new OverlayProbeJournal();
        var data=new OverlayMeasureObservation(1,new(id,id,22,22,new(388,296),state,state,true),80,113,376,113,1);
        Assert.False(journal.HasCurrentMeasurement(id,22));journal.AddMeasurement(1,"Timing",data);
        Assert.True(journal.HasCurrentMeasurement(id,22));Assert.False(journal.HasCurrentMeasurement(id,23));
        foreach(var changed in new[]{data.Markdown with{VersionBefore=Guid.NewGuid()},data.Markdown with{VersionAfter=Guid.NewGuid()},
            data.Markdown with{TextLengthBefore=21},data.Markdown with{TextLengthAfter=23},data.Markdown with{CurrentAccepted=false}})
        {journal.AddMeasurement(2,"Timing",data with{Markdown=changed});Assert.False(journal.HasCurrentMeasurement(id,22));}
    }
    [Fact]
    public async Task FailedPhaseCanContinueDiagnosisAndStillWaitForOriginalCloseWithFailurePreserved()
    {
        var closeEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var close=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var closed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seed=new OverlayProbePhase(1,"Timing","SeedOpen",0,Anchor,OverlayTransition.Opening,OverlayTransition.Expanded,OverlayProbeGoal.Settled);
        var grow=new OverlayProbePhase(2,"Timing","LongStreamGrow",2,Half,OverlayTransition.Expanded,OverlayTransition.Expanded,OverlayProbeGoal.Settled);
        bool continued=false,cleaned=false;OverlayProbeLifetime? owner=null;
        owner=new(()=>
        {
            seed.Issued();Assert.False(seed.Observe(new OverlayProbeJournal().Add(Frame(1,1,Half,OverlayTransition.Expanded,false))));Assert.True(seed.Finished);
            owner!.Record("Stage:Timing:SeedOpen",new InvalidOperationException(seed.FailureReason));continued=true;
            grow.Issued();var journal=new OverlayProbeJournal();Assert.False(grow.Observe(journal.Add(Frame(3,3,Full,OverlayTransition.Expanded,true,2))));
            Assert.True(grow.Observe(journal.Add(Frame(4,4,Full,OverlayTransition.Expanded,false,2))));return Task.CompletedTask;
        },async()=>{closeEntered.SetResult();return await owner!.JoinClose(close.Task,closed.Task);},()=>cleaned=true);
        owner.Release();await closeEntered.Task;Assert.True(continued);Assert.False(owner.Original.IsCompleted);Assert.False(cleaned);
        close.SetResult();Assert.False(owner.Original.IsCompleted);closed.SetResult();await owner.Original;
        Assert.True(cleaned);Assert.True(owner.CleanupConfirmed);Assert.Single(owner.Failures);
        Assert.Null(seed.CompletedQpc);Assert.Equal("Failed",seed.Outcome);Assert.Equal("Passed",grow.Outcome);
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
