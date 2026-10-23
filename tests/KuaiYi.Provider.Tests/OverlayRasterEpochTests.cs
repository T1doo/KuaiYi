using KuaiYi.Core;
using KuaiYi.GrowthHarness;
using Xunit;

namespace KuaiYi.Provider.Tests;

public sealed class OverlayRasterEpochTests
{
    private static readonly PixelRect Anchor=new(20,20,68,68),Half=new(20,20,180,160),Full=new(20,20,420,300),Work=new(0,0,1000,800);
    private static OverlayProbeFrame Frame(long qpc,long epoch,PixelRect card,OverlayTransition state=OverlayTransition.Opening,bool needs=true)
        =>new(qpc,qpc,10,"Raster",state,needs,false,1,17,card,Anchor,Work,card,Anchor,GrowthDirection.RightDown,1,388,0,true,AnimationEpoch:epoch);
    private static OverlayProbePhase Phase()=>new(10,"Raster","SeedOpen",0,Anchor,OverlayTransition.Opening,OverlayTransition.Expanded,OverlayProbeGoal.Settled);

    [Fact]
    public void OldEpochCandidateCannotOccupyTheCurrentEpochIntermediateSlot()
    {
        var phase=Phase();var journal=new OverlayProbeJournal();var budget=new OverlayRasterBudget();
        var old=journal.Add(Frame(1,3,Half));Assert.False(phase.Observe(old));Assert.True(budget.ClaimIntermediate(phase,old));
        phase.Issued(2,4);var current=journal.Add(Frame(3,4,Half));Assert.False(phase.Observe(current));
        Assert.True(budget.ClaimIntermediate(phase,current),"Epoch3's retained image must not consume epoch4's intermediate slot.");
        Assert.True(phase.Observe(journal.Add(Frame(4,4,Full,OverlayTransition.Expanded,false))));
        Assert.False(OverlayRasterBudget.MatchesIssuedEpoch(phase,old,old.Frame));
        Assert.True(OverlayRasterBudget.MatchesIssuedEpoch(phase,current,current.Frame));
    }
    [Theory][InlineData(3,4,false)][InlineData(4,3,false)][InlineData(4,4,true)]
    public void BothSidesOfCaptureMustBelongToTheIssuedEpoch(long beforeEpoch,long afterEpoch,bool expected)
    {
        var phase=Phase();phase.Issued(10,4);var before=new OverlayProbeJournal().Add(Frame(11,beforeEpoch,Half));
        Assert.Equal(expected,OverlayRasterBudget.MatchesIssuedEpoch(phase,before,Frame(12,afterEpoch,Half)));
    }
    [Fact]
    public void ACurrentEpochIntermediateObservedDuringIssueRemainsEligible()
    {
        var phase=Phase();var budget=new OverlayRasterBudget();var sample=new OverlayProbeJournal().Add(Frame(1,4,Half));
        phase.Observe(sample);Assert.True(budget.ClaimIntermediate(phase,sample));phase.Issued(2,4);
        Assert.True(OverlayRasterBudget.MatchesIssuedEpoch(phase,sample,sample.Frame));Assert.False(budget.ClaimIntermediate(phase,sample));
    }
    [Fact]
    public void AHistoricalIntermediateCannotLabelTheCurrentTerminalAsIntermediate()
    {
        var phase=Phase();var journal=new OverlayProbeJournal();phase.Observe(journal.Add(Frame(1,4,Half)));phase.Issued(2,4);
        var terminal=journal.Add(Frame(3,4,Full,OverlayTransition.Expanded,false));Assert.True(phase.Observe(terminal));
        Assert.False(new OverlayRasterBudget().ClaimIntermediate(phase,terminal));
    }
}
