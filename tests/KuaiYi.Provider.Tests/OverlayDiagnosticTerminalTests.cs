using System.Text.Json;
using KuaiYi.Core;
using KuaiYi.GrowthHarness;
using Xunit;

namespace KuaiYi.Provider.Tests;

public sealed class OverlayDiagnosticTerminalTests
{
    [Fact]
    public void RealLongTickTerminalIsRecordedAsFailureWithoutInventingIntermediateGeometry()
    {
        var anchor=new PixelRect(488,340,536,388);var work=new PixelRect(0,0,1024,728);
        var geometry=new OverlayFrames(anchor,new("neutral",work,new(1)));
        geometry.Open(113,new(420,640));
        var delta=TimeSpan.FromMilliseconds(321.2715);geometry.Tick(delta);
        var phase=new OverlayProbePhase(1,"Timing","SeedOpen",10,anchor,OverlayTransition.Opening,OverlayTransition.Expanded,OverlayProbeGoal.Settled);
        phase.Issued();var journal=new OverlayProbeJournal();
        var sample=journal.Add(new(30,30,1,"Timing",geometry.Transition,geometry.NeedsFrame,false,1,17,
            geometry.Current,anchor,work.Inset(12),geometry.Current,anchor,geometry.ContentDirection,1,388,0,true));
        Assert.False(phase.Observe(sample));Assert.False(phase.SawIntermediate);Assert.Null(phase.CompletedQpc);
        Assert.Equal(113,geometry.Current.Height);Assert.Equal(321.2715,delta.TotalMilliseconds,6);
        var evidence=JsonSerializer.SerializeToElement(phase);
        Assert.True(evidence.TryGetProperty("FailedQpc",out var failed),"Stable terminal failure must end diagnostic waiting explicitly.");
        Assert.Equal(30,failed.GetInt64());Assert.Equal("Failed",evidence.GetProperty("Outcome").GetString());
        Assert.Equal("ChangedIntermediateNotObserved",evidence.GetProperty("FailureReason").GetString());
        Assert.Single(journal.Samples);Assert.Same(sample,journal.Samples[0]);
    }
}
