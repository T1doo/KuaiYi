using KuaiYi.Core;
using Xunit;

namespace KuaiYi.Core.Tests;

public sealed class GateTests
{
    private static EndpointOrigin Origin(string url = "https://example.test/v1") => new(new Uri(url));
    private static RuntimeGate Enabled()
    {
        var gate = new RuntimeGate();
        gate.ConsentTo(Origin());
        Assert.True(gate.TryEnable(Origin(), true));
        return gate;
    }
    [Fact]
    public void StartupAndUnconsentedOrInvalidConfigurationCannotCaptureOrSend()
    {
        var gate = new RuntimeGate();
        Assert.Equal(Activation.Off, gate.Snapshot.Activation);
        Assert.False(gate.TryBeginPassiveCapture(TriggerMode.Selection, out _));
        Assert.False(gate.TryEnable(Origin(), true));
        gate.ConsentTo(Origin());
        Assert.False(gate.TryEnable(Origin(), false));
        Assert.False(gate.TryStartRequest(Guid.NewGuid(), Guid.NewGuid(), null, out _, out _));
    }
    [Fact]
    public void DisableCancelsPendingCaptureRequestAndEveryLateEvent()
    {
        var gate = Enabled();
        Assert.True(gate.TryBeginPassiveCapture(TriggerMode.Selection, out var capture));
        Assert.True(gate.TryStartRequest(Guid.NewGuid(), Guid.NewGuid(), capture, out var request, out var ct));
        gate.Disable();
        Assert.True(ct.IsCancellationRequested);
        Assert.False(gate.IsCaptureCurrent(capture));
        Assert.False(gate.CanSend(request, Origin()));
        Assert.False(gate.CanApply(request));
        Assert.False(gate.TryRevealDelta(request));
    }
    [Fact]
    public void DropAndSuspensionsInvalidateCandidatesButSettingsDoNotCancelExistingGeneration()
    {
        var gate = Enabled();
        gate.TryBeginPassiveCapture(TriggerMode.Selection, out var capture);
        gate.SetMode(TriggerMode.Drop);
        Assert.False(gate.IsCaptureCurrent(capture));
        Assert.False(gate.TryBeginPassiveCapture(TriggerMode.Selection, out _));
        Assert.True(gate.TryBeginPassiveCapture(TriggerMode.Drop, out capture));
        gate.SetSuspension(Suspension.Settings, true);
        Assert.False(gate.TryStartRequest(Guid.NewGuid(), Guid.NewGuid(), capture, out _, out _));
        gate.SetSuspension(Suspension.Settings, false);
        gate.TryStartRequest(Guid.NewGuid(), Guid.NewGuid(), null, out var request, out var ct);
        gate.SetSuspension(Suspension.Settings, true);
        Assert.True(gate.CanSend(request, Origin()));
        Assert.False(ct.IsCancellationRequested);
    }
    [Fact]
    public void LatestWinsRejectsOldCompletionAndErrorAndCancelsPreviousChain()
    {
        var gate = Enabled();
        gate.TryStartRequest(Guid.NewGuid(), Guid.NewGuid(), null, out var a, out var ca);
        gate.TryStartRequest(Guid.NewGuid(), Guid.NewGuid(), null, out var b, out _);
        Assert.True(ca.IsCancellationRequested);
        Assert.False(gate.CanApply(a));
        Assert.True(gate.CanApply(b));
        Assert.False(gate.CanApply(b with { VersionId = Guid.NewGuid() }));
        Assert.False(gate.CanApply(b with { Epoch = b.Epoch - 1 }));
    }
    [Fact]
    public void EachActualOriginIncludingVisionAndOldSnapshotRequiresSeparateConsent()
    {
        var gate = Enabled();
        gate.TryStartRequest(Guid.NewGuid(), Guid.NewGuid(), null, out var request, out _);
        Assert.True(gate.CanSend(request, Origin()));
        Assert.False(gate.CanSend(request, Origin("https://vision.example.test/messages")));
        Assert.False(gate.CanSend(request, Origin("http://example.test/v1")));
        Assert.False(gate.CanSend(request, Origin("https://example.test:444/v1")));
        Assert.Equal(Origin(), Origin("https://EXAMPLE.test:443/other"));
    }
    [Fact]
    public void CollapseKeepsChainRunningAndReopenKeepsActivationOff()
    {
        var gate = Enabled();
        gate.TryStartRequest(Guid.NewGuid(), Guid.NewGuid(), null, out var request, out var ct);
        gate.Collapse();
        Assert.False(gate.TryRevealDelta(request));
        Assert.True(gate.CanApply(request));
        Assert.False(ct.IsCancellationRequested);
        gate.Disable();
        gate.Reopen(true);
        Assert.Equal(Activation.Off, gate.Snapshot.Activation);
        Assert.Equal(Presentation.Expanded, gate.Snapshot.Presentation);
    }
    [Fact]
    public void LockUnlockHideAndStopDoNotRestoreAutomaticGeneration()
    {
        var gate = Enabled();
        gate.TryStartRequest(Guid.NewGuid(), Guid.NewGuid(), null, out var request, out _);
        gate.SetSessionLocked(true);
        Assert.False(gate.CanApply(request));
        Assert.False(gate.TryEnable(Origin(), true));
        gate.SetSessionLocked(false);
        Assert.Equal(Activation.Off, gate.Snapshot.Activation);
        gate.TryEnable(Origin(), true);
        gate.TryStartRequest(Guid.NewGuid(), Guid.NewGuid(), null, out request, out _);
        gate.StopGeneration();
        Assert.Equal(Activation.On, gate.Snapshot.Activation);
        Assert.False(gate.CanApply(request));
        gate.Disable(hide: true);
        Assert.Equal(Presentation.Hidden, gate.Snapshot.Presentation);
    }
}
