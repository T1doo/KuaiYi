using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class HotkeyLifecycleTests
{
    private static readonly EndpointOrigin Origin=new(new Uri("https://public.invalid"));
    public static IEnumerable<object[]> QueuedTransitions()=>from action in new[]{"translateSelection","captureRegion"} from change in new[]{"offOn","enableOnly","localEnable","modeRoundTrip","settingsRoundTrip","movingRoundTrip","captureRoundTrip","explicitCapture","lockUnlock","hideShow","requestReplacement","stopGeneration","confirmedLocal","profileMetadata","clipboardPermission"} select new object[]{action,change};
    private static void Transition(RuntimeGate gate,string change,Action<uint> time)
    {
        time(200);
        switch(change)
        {
            case "offOn":gate.Disable();time(300);Assert.True(gate.TryEnable(Origin,true));break;
            case "enableOnly":time(300);Assert.True(gate.TryEnable(Origin,true));break;
            case "localEnable":time(300);Assert.True(gate.TryEnableLocalOperation(true));break;
            case "modeRoundTrip":gate.SetMode(TriggerMode.Drop);time(300);gate.SetMode(TriggerMode.Selection);break;
            case "settingsRoundTrip":gate.SetSuspension(Suspension.Settings,true);time(300);gate.SetSuspension(Suspension.Settings,false);break;
            case "movingRoundTrip":gate.SetSuspension(Suspension.Moving,true);time(300);gate.SetSuspension(Suspension.Moving,false);break;
            case "captureRoundTrip":gate.SetSuspension(Suspension.Capture,true);time(300);gate.SetSuspension(Suspension.Capture,false);break;
            case "explicitCapture":Assert.True(gate.TryBeginExplicitCapture(out var owner,out _,out _));time(300);gate.EndExplicitCapture(owner);break;
            case "lockUnlock":gate.SetSessionLocked(true);time(300);gate.SetSessionLocked(false);Assert.True(gate.TryEnable(Origin,true));break;
            case "hideShow":gate.Disable(true);time(300);gate.Disable();Assert.True(gate.TryEnable(Origin,true));break;
            case "requestReplacement":Assert.True(gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out _,out _));time(300);Assert.True(gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out _,out _));break;
            case "stopGeneration":time(300);gate.StopGeneration();break;
            case "confirmedLocal":time(300);Assert.True(gate.TryStartConfirmedLocalRequest(gate.Snapshot.Epoch,Guid.NewGuid(),Guid.NewGuid(),out _,out _));break;
            case "profileMetadata":time(300);gate.ObserveSettingsMetadata(new(new(){Profiles=[new(){Id="public",Authentication=ApiAuthentication.None}]},"public-version",SettingsLoadState.Current));break;
            case "clipboardPermission":time(300);gate.ApplyCommittedSettings(new(new(),"public-version",SettingsLoadState.Current),new([],[]){ClipboardPermissionsChanged=true});break;
            default:throw new ArgumentException(change);
        }
    }
    [Theory][MemberData(nameof(QueuedTransitions))]
    public void NativeQueueMessageBeforeLifecycleTransitionCannotBeAssignedNewEraAtLateResolve(string action,string change)
    {
        uint now=100;var gate=new RuntimeGate(false,()=>now);gate.ConsentTo(Origin);if(change!="enableOnly")Assert.True(gate.TryEnable(Origin,true));var port=new HotkeyTests.Port{Now=100};var registry=new HotkeyRegistry(port);registry.Restore(HotkeyTests.Map((action,"Ctrl+Q")),"settings");int id=port.Owned.Single().Key;port.Calls.Clear();uint queuedMessageTick=150;
        Transition(gate,change,value=>now=value);now=400;
        Assert.NotNull(registry.Resolve(id,HotkeyPolicy.Parse("Ctrl+Q"),queuedMessageTick)); // Original mapping-only fence accepts this native front-queue backlog.
        Assert.Null(HotkeyInputAdmission.Resolve(registry,gate,id,HotkeyPolicy.Parse("Ctrl+Q"),queuedMessageTick));
        var fresh=HotkeyInputAdmission.Resolve(registry,gate,id,HotkeyPolicy.Parse("Ctrl+Q"),301);Assert.NotNull(fresh);Assert.True(HotkeyInputAdmission.Current(registry,gate,fresh));Assert.Empty(port.Calls);gate.Disable();
    }
    [Theory][InlineData("enableOnly")][InlineData("localEnable")]
    public void AlreadyAdmittedUiQueueTicketExpiresOnEnableEvenWhenLegacyEpochIsUnchanged(string transition)
    {uint now=100;var gate=new RuntimeGate(false,()=>now);gate.ConsentTo(Origin);var port=new HotkeyTests.Port();var registry=new HotkeyRegistry(port);registry.Restore(HotkeyTests.Map(("captureRegion","Ctrl+Q")),"settings");now=150;var ticket=HotkeyInputAdmission.Resolve(registry,gate,port.Owned.Single().Key,HotkeyPolicy.Parse("Ctrl+Q"),150)!;long epoch=gate.Snapshot.Epoch;Transition(gate,transition,t=>now=t);Assert.Equal(epoch,gate.Snapshot.Epoch);Assert.False(HotkeyInputAdmission.Current(registry,gate,ticket));gate.Disable();}
    [Fact]public void UnchangedLifecycleDoesNotDiscardFreshOffScreenshotOrEnableHint()
    {uint now=100;var gate=new RuntimeGate(false,()=>now);var port=new HotkeyTests.Port();var registry=new HotkeyRegistry(port);registry.Restore(HotkeyTests.Map(("captureRegion","Ctrl+Q"),("translateSelection","Ctrl+W")),"settings");now=200;foreach(var pair in port.Owned){var ticket=HotkeyInputAdmission.Resolve(registry,gate,pair.Key,pair.Value,150);Assert.NotNull(ticket);Assert.True(HotkeyInputAdmission.Current(registry,gate,ticket));Assert.Equal(ticket.Binding.Action=="captureRegion"?HotkeyActionDecision.Run:HotkeyActionDecision.HintEnable,HotkeyActionPolicy.Evaluate(ticket.Binding.Action,gate.Snapshot.Activation,gate.Snapshot.Suspension));}Assert.Equal(Activation.Off,gate.Snapshot.Activation);}
    [Theory][InlineData("settingsRoundTrip")][InlineData("movingRoundTrip")][InlineData("captureRoundTrip")][InlineData("lockUnlock")][InlineData("modeRoundTrip")]
    public void MessageBornDuringTemporaryPauseCannotRunAfterResume(string change)
    {uint now=100;var gate=new RuntimeGate(false,()=>now);gate.ConsentTo(Origin);Assert.True(gate.TryEnable(Origin,true));var port=new HotkeyTests.Port();var registry=new HotkeyRegistry(port);registry.Restore(HotkeyTests.Map(("captureRegion","Ctrl+Q")),"settings");Transition(gate,change,t=>now=t);now=400;Assert.Null(HotkeyInputAdmission.Resolve(registry,gate,port.Owned.Single().Key,HotkeyPolicy.Parse("Ctrl+Q"),250));Assert.NotNull(HotkeyInputAdmission.Resolve(registry,gate,port.Owned.Single().Key,HotkeyPolicy.Parse("Ctrl+Q"),301));gate.Disable();}
    [Theory][InlineData(0xfffffff0u,0x10u,true)][InlineData(100u,99u,false)][InlineData(100u,100u,false)][InlineData(100u,101u,true)]
    public void LifecycleEventTimeFenceHandlesWrapAndConservativelyRejectsSameTick(uint boundary,uint eventTick,bool accepted)
    {uint now=unchecked(boundary-10);var gate=new RuntimeGate(false,()=>now);var port=new HotkeyTests.Port{Now=now};var registry=new HotkeyRegistry(port);registry.Restore(HotkeyTests.Map(("captureRegion","Ctrl+Q")),"settings");now=boundary;gate.Disable();Assert.Equal(accepted,HotkeyInputAdmission.Resolve(registry,gate,port.Owned.Single().Key,HotkeyPolicy.Parse("Ctrl+Q"),eventTick)!=null);}
    [Fact]public void ConfirmedShellEnableFencesNativeAndUiMessagesWithoutChangingLegacyEpoch()
    {uint now=100;var gate=new RuntimeGate(false,()=>now);gate.ObserveSettingsMetadata(new(new(){Profiles=[new(){Id="public",Authentication=ApiAuthentication.None}]},"settings",SettingsLoadState.Current));Assert.True(gate.TryGetProfileBinding("public",out var profile));var port=new HotkeyTests.Port();var registry=new HotkeyRegistry(port);registry.Restore(HotkeyTests.Map(("captureRegion","Ctrl+Q")),"settings");now=150;var ticket=HotkeyInputAdmission.Resolve(registry,gate,port.Owned.Single().Key,HotkeyPolicy.Parse("Ctrl+Q"),150)!;long epoch=gate.Snapshot.Epoch;now=300;Assert.True(gate.TryEnableShell(new("settings",epoch,profile,Origin,TriggerMode.Selection),()=>true));Assert.Equal(epoch,gate.Snapshot.Epoch);Assert.False(HotkeyInputAdmission.Current(registry,gate,ticket));Assert.Null(HotkeyInputAdmission.Resolve(registry,gate,port.Owned.Single().Key,HotkeyPolicy.Parse("Ctrl+Q"),150));gate.Disable();}
    [Fact]public void FailedEnableCannotExpireUnchangedOffInputAndForeignGateCannotAdoptIdentity()
    {uint now=100;var gate=new RuntimeGate(false,()=>now);var other=new RuntimeGate(false,()=>now);var port=new HotkeyTests.Port();var registry=new HotkeyRegistry(port);registry.Restore(HotkeyTests.Map(("captureRegion","Ctrl+Q")),"settings");now=150;var ticket=HotkeyInputAdmission.Resolve(registry,gate,port.Owned.Single().Key,HotkeyPolicy.Parse("Ctrl+Q"),150)!;now=200;Assert.False(gate.TryEnable(Origin,true));Assert.True(HotkeyInputAdmission.Current(registry,gate,ticket));Assert.False(HotkeyInputAdmission.Current(registry,other,ticket));}
    [Theory][InlineData("offOn")][InlineData("modeRoundTrip")][InlineData("settingsRoundTrip")]
    public void AcquisitionCannotRefreshExpiredHotkeyIdentityAfterFinalUiCheck(string change)
    {uint now=100;var gate=new RuntimeGate(false,()=>now);gate.ConsentTo(Origin);Assert.True(gate.TryEnable(Origin,true));var port=new HotkeyTests.Port();var registry=new HotkeyRegistry(port);registry.Restore(HotkeyTests.Map(("captureRegion","Ctrl+Q")),"settings");now=150;var original=HotkeyInputAdmission.Resolve(registry,gate,port.Owned.Single().Key,HotkeyPolicy.Parse("Ctrl+Q"),150)!;Assert.True(HotkeyInputAdmission.Current(registry,gate,original));Transition(gate,change,t=>now=t);long epoch=gate.Snapshot.Epoch;Assert.False(gate.TryBeginHotkeyPassiveCapture(original.Input,out _));Assert.False(gate.TryBeginExplicitCapture(out _,out _,out _,original.Input));Assert.Equal(Suspension.None,gate.Snapshot.Suspension);Assert.Equal(epoch,gate.Snapshot.Epoch);var fresh=HotkeyInputAdmission.Resolve(registry,gate,port.Owned.Single().Key,HotkeyPolicy.Parse("Ctrl+Q"),301)!;Assert.True(gate.TryBeginHotkeyPassiveCapture(fresh.Input,out _));using(var intent=new KuaiYi.Imaging.ScreenshotIntent(gate,fresh.Input))Assert.True(intent.IsCurrent);Assert.Equal(Suspension.None,gate.Snapshot.Suspension);gate.Disable();}
}
