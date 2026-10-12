using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class PhaseConsumerTests(PhaseHttpFixture fixture):IClassFixture<PhaseHttpFixture>
{
    private sealed class Destination:SynchronizationContext{public override void Post(SendOrPostCallback action,object? state)=>action(state);}
    private sealed class Rig:IAsyncDisposable
    {
        public PhaseClock Clock=new();public RuntimeGate Gate=new(requireProfileBinding:true);public ProviderClient Provider;public SettingsSnapshot Settings;public OverlayResults Results;public TextTranslationCoordinator Flow;public PhaseHttpFixture.Script Script;public string Run=Guid.NewGuid().ToString("N");
        public Rig(PhaseHttpFixture fixture)
        {Script=fixture.Add(Run,"application/json");var prefix=new Uri(fixture.BaseUri,Run+"/");Settings=new(new(){ActiveProfileId="p",Profiles=[new(){Id="p",BaseUrl=prefix.AbsoluteUri,Model="PUBLIC",AllowLoopbackHttp=true,Authentication=ApiAuthentication.None,Timeouts=new(){FirstResponseSeconds=3,IdleSeconds=2}}]},"initial",SettingsLoadState.Current);Gate.ObserveSettingsMetadata(Settings);Gate.ConsentTo(new(prefix));Gate.TryEnable(new(prefix),true);Provider=new(phaseClock:Clock);Results=new(Gate);Flow=new(Gate,()=>Settings,(_,_)=>true,Provider,new Destination(),Results,_=>{});}
        public SelectionSourceSnapshot Source()=>new(new(18,93812,132000,@"C:\Fixtures\editor.exe"),"PUBLIC_SOURCE","PUBLIC_SOURCE",[],[7,8],Settings.Version,13,false);
        public void Change(RequestTimeouts value){Settings=Settings with{Version=Guid.NewGuid().ToString("N"),Data=Settings.Data with{Profiles=[Settings.Data.Profiles[0] with{Timeouts=value}]}};Gate.ApplyCommittedSettings(Settings,null);Flow.ObserveSettings(Settings);}
        public async ValueTask DisposeAsync(){Gate.Disable();await Flow.DisposeAsync();Provider.Dispose();}
    }
    [Fact] public async Task PhaseConfigurationSwitchCancelsOwnLiveRequestAndCannotCompleteOldVersion()
    {
        await using var rig=new Rig(fixture);var offer=rig.Flow.Prepare(rig.Source())!;rig.Flow.Submit(offer);await rig.Script.Received.Task.WaitAsync(TimeSpan.FromSeconds(6));await rig.Clock.WaitDeadline(3);rig.Change(new(){FirstResponseSeconds=7,IdleSeconds=5});await rig.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.False(rig.Gate.CanApply(rig.Results.Identity!.Value));Assert.Empty(rig.Results.Text);Assert.False(Assert.Single(rig.Results.Versions).Complete);Assert.Equal(3,offer.Snapshot.Transport.PhaseTimeouts.FirstResponseSeconds);Assert.Equal(1,rig.Script.Requests);
    }
    [Fact] public async Task OldSuccessfulSnapshotAndOrdinaryRegenerationKeepFrozenPhaseValues()
    {
        await using var rig=new Rig(fixture);rig.Flow.Submit(rig.Flow.Prepare(rig.Source())!);await rig.Script.Write("{\"choices\":[{\"index\":0,\"message\":{\"content\":\"PUBLIC\"},\"finish_reason\":\"stop\"}]}",true);await rig.Flow.Running;var old=rig.Flow.SelectedSnapshot!;rig.Change(new(){ConnectSeconds=4,HeadersSeconds=8,FirstResponseSeconds=9,IdleSeconds=6});var frozen=rig.Flow.PrepareRegeneration()!;Assert.Equal(old.Transport.PhaseTimeouts,frozen.Snapshot.Transport.PhaseTimeouts);Assert.Equal(3,frozen.Snapshot.Transport.PhaseTimeouts.FirstResponseSeconds);var current=rig.Flow.PrepareRegeneration(true)!;Assert.Equal(9,current.Snapshot.Transport.PhaseTimeouts.FirstResponseSeconds);Assert.Equal(6,current.Snapshot.Transport.PhaseTimeouts.IdleSeconds);Assert.Equal(1,rig.Script.Requests);
    }
    [Fact] public async Task ApiPhaseEditRevokesPreparedConfirmationWithoutSecretReadOrHttp()
    {await using var rig=new Rig(fixture);rig.Gate.Disable();int reads=0;await using var workflow=new ApiConnectionTestWorkflow(rig.Gate,()=>rig.Settings,rig.Provider,new Destination(),(_,_,_)=>{reads++;throw new InvalidOperationException("No key.");});var intent=workflow.Prepare("p",out _)!;workflow.DraftEdited();Assert.Null(workflow.Start(intent,true,_=>{}));Assert.Equal(0,reads);Assert.Equal(0,rig.Script.Requests);}
}
