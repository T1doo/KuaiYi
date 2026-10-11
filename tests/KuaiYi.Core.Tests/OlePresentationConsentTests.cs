using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class OlePresentationConsentTests
{
    [Fact]public void AnotherLocalWorkflowOnCannotGrantFloatingDropAndExplicitDropCannotGrantNetwork()
    {
        var gate=new RuntimeGate();var consent=new OlePresentationConsent();gate.SetMode(TriggerMode.Drop);Assert.True(gate.TryEnableLocalOperation(true));Assert.False(consent.Allows(gate,true));Assert.True(consent.TryEnable(gate));Assert.True(consent.Allows(gate,true));var origin=new EndpointOrigin(new Uri("https://public.invalid"));gate.ConsentTo(origin);Assert.True(gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var identity,out _));Assert.False(gate.CanSend(identity,origin));
    }
    [Theory][InlineData("off")][InlineData("hidden")][InlineData("mode")][InlineData("revoke")]
    public void RevocationNeedsFreshOwnConsentEvenIfAnotherWorkflowTurnsOn(string reason)
    {
        var gate=new RuntimeGate();var consent=new OlePresentationConsent();Assert.True(consent.TryEnable(gate));
        if(reason=="off")gate.Disable();if(reason=="mode")gate.SetMode(TriggerMode.Selection);if(reason=="revoke")consent.Revoke();
        Assert.False(consent.Allows(gate,reason!="hidden"));gate.SetMode(TriggerMode.Drop);Assert.True(gate.TryEnableLocalOperation(true));Assert.False(consent.Allows(gate,true));Assert.True(consent.TryEnable(gate));Assert.True(consent.Allows(gate,true));
    }
    [Theory][InlineData(Suspension.Moving)][InlineData(Suspension.Capture)][InlineData(Suspension.Settings)][InlineData(Suspension.SessionLocked)]
    public void SuspensionBlocksAcquisitionAndNewExplicitGrant(Suspension suspension)
    {
        var gate=new RuntimeGate();var consent=new OlePresentationConsent();Assert.True(consent.TryEnable(gate));long before=gate.Snapshot.Epoch;
        if(suspension==Suspension.SessionLocked)gate.SetSessionLocked(true);else gate.SetSuspension(suspension,true);
        Assert.False(consent.Allows(gate,true));Assert.False(consent.TryEnable(gate));Assert.True(gate.Snapshot.Epoch>before);
        if(suspension==Suspension.SessionLocked){gate.SetSessionLocked(false);Assert.False(consent.Allows(gate,true));Assert.Equal(Activation.Off,gate.Snapshot.Activation);}else{gate.SetSuspension(suspension,false);Assert.True(consent.Allows(gate,true));}
    }
}
