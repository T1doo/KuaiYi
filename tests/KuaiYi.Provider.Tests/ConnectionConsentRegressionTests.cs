using System.Net.Http.Json;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.MockProvider;
using KuaiYi.Providers;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class ConnectionConsentRegressionTests(ServerFixture fixture):IClassFixture<ServerFixture>
{
    private sealed class Destination:SynchronizationContext{public override void Post(SendOrPostCallback action,object? state)=>action(state);}
    private sealed class Rig:IAsyncDisposable
    {
        public RuntimeGate Gate=new(requireProfileBinding:true);public ProviderClient Provider=new();public SettingsSnapshot Settings;
        public ApiConnectionTestWorkflow Flow;public string Run;public Uri Prefix;public EndpointOrigin Origin;public int Secrets;
        public Rig(Uri root,string scenario="fragmented")
        {
            Run=Guid.NewGuid().ToString("N");Prefix=new(root,$"runs/{Run}/{scenario}/");Origin=new(Prefix);
            Settings=new(new(){ActiveProfileId="p",Profiles=[new(){Id="p",BaseUrl=Prefix.AbsoluteUri,Model="PUBLIC_FIXTURE_MODEL",Authentication=ApiAuthentication.None,AllowLoopbackHttp=true}]},"initial",SettingsLoadState.Current);
            Gate.ObserveSettingsMetadata(Settings);Flow=new(Gate,()=>Settings,Provider,new Destination(),(_,_,_)=>{Secrets++;throw new InvalidOperationException("No key configured.");});
        }
        public async ValueTask DisposeAsync(){Gate.Disable();await Flow.DisposeAsync();Provider.Dispose();}
    }
    private async Task<CapturedRequest[]> Requests(string run){using var http=new HttpClient();return(await http.GetFromJsonAsync<CapturedRequest[]>(new Uri(fixture.BaseUri,$"requests/{run}")))!;}
    private async Task Barrier(string run)
    {using var http=new HttpClient();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(true){var reply=await http.GetFromJsonAsync<System.Text.Json.JsonElement>(new Uri(fixture.BaseUri,$"barriers/{run}"),timeout.Token);if(reply.GetProperty("ready").GetBoolean())return;await Task.Delay(10,timeout.Token);}}
    private async Task Release(string run){using var http=new HttpClient();using var response=await http.PostAsync(new Uri(fixture.BaseUri,$"barriers/{run}/release"),null);response.EnsureSuccessStatusCode();}
    [Fact] public async Task SuccessfulPublicSampleNeverGrantsRealContentSessionConsent()
    {
        await using var rig=new Rig(fixture.BaseUri);Assert.False(rig.Gate.HasConsentTo(rig.Origin));var intent=rig.Flow.Prepare("p",out _)!;await rig.Flow.Start(intent,true,_=>{})!;
        Assert.Single(await Requests(rig.Run));Assert.False(rig.Gate.HasConsentTo(rig.Origin));Assert.False(rig.Gate.TryEnable(rig.Origin,true));Assert.Equal(Activation.Off,rig.Gate.Snapshot.Activation);Assert.Equal(0,rig.Secrets);
    }
    [Theory] [InlineData("epoch")] [InlineData("settings")] [InlineData("lock")] [InlineData("binding")]
    public async Task FailedStartCannotLeaveAnyContentAuthorization(string change)
    {
        await using var rig=new Rig(fixture.BaseUri);var intent=rig.Flow.Prepare("p",out _)!;
        if(change=="epoch")rig.Gate.SetSuspension(Suspension.Settings,true);else if(change=="lock")rig.Gate.SetSessionLocked(true);
        else if(change=="settings")rig.Settings=rig.Settings with{Version="changed"};else{rig.Settings=rig.Settings with{Data=rig.Settings.Data with{Profiles=[]}};rig.Gate.ObserveSettingsMetadata(rig.Settings);}
        Assert.Null(rig.Flow.Start(intent,true,_=>{}));Assert.False(rig.Gate.HasConsentTo(rig.Origin));Assert.Empty(await Requests(rig.Run));Assert.Equal(0,rig.Secrets);
    }
    [Theory] [InlineData("cancel")] [InlineData("off")] [InlineData("config")] [InlineData("endpoint")]
    public async Task InterruptedPublicSampleCannotAuthorizeLaterRealContent(string change)
    {
        await using var rig=new Rig(fixture.BaseUri,"held-headers");var intent=rig.Flow.Prepare("p",out _)!;var job=rig.Flow.Start(intent,true,_=>{})!;await Barrier(rig.Run);
        if(change=="cancel")rig.Flow.Cancel();else if(change=="off")rig.Gate.Disable();
        else{rig.Settings=rig.Settings with{Data=rig.Settings.Data with{Profiles=[rig.Settings.Data.Profiles[0] with{BaseUrl=change=="endpoint"?"https://external.invalid/v1":rig.Prefix.AbsoluteUri,Model="PUBLIC_CHANGED_MODEL"}]},Version="changed"};rig.Gate.ObserveSettingsMetadata(rig.Settings);rig.Flow.Cancel();}
        await Release(rig.Run);await job;Assert.False(rig.Gate.HasConsentTo(rig.Origin));Assert.False(rig.Gate.HasConsentTo(new(new Uri("https://external.invalid"))));Assert.False(rig.Gate.TryEnable(rig.Origin,true));Assert.Equal(Activation.Off,rig.Gate.Snapshot.Activation);Assert.Single(await Requests(rig.Run));
    }
    [Fact] public async Task UserDeclineNeverAddsAnyPermissionOrRequest()
    {await using var rig=new Rig(fixture.BaseUri);var intent=rig.Flow.Prepare("p",out _)!;Assert.Null(rig.Flow.Start(intent,false,_=>{}));Assert.False(rig.Gate.HasConsentTo(rig.Origin));Assert.Empty(await Requests(rig.Run));}
    [Fact] public async Task ExistingRealContentGrantIsPreservedWhileTestAddsNoSecondOrigin()
    {
        await using var rig=new Rig(fixture.BaseUri);var old=new EndpointOrigin(new Uri("https://previously-confirmed.invalid"));rig.Gate.ConsentTo(old);var intent=rig.Flow.Prepare("p",out _)!;await rig.Flow.Start(intent,true,_=>{})!;
        Assert.True(rig.Gate.HasConsentTo(old));Assert.False(rig.Gate.HasConsentTo(rig.Origin));Assert.False(rig.Gate.TryEnable(rig.Origin,true));
    }
    [Fact] public async Task DraftEditInvalidatesPreparedConfirmationWithoutAddingAuthorization()
    {await using var rig=new Rig(fixture.BaseUri);var intent=rig.Flow.Prepare("p",out _)!;rig.Flow.DraftEdited();Assert.Null(rig.Flow.Start(intent,true,_=>{}));Assert.False(rig.Gate.HasConsentTo(rig.Origin));Assert.Empty(await Requests(rig.Run));}
    [Fact] public async Task DraftEditCancelsInFlightOriginalTaskAndCannotReportSuccess()
    {await using var rig=new Rig(fixture.BaseUri,"held-headers");var updates=new List<ApiTestUpdate>();var job=rig.Flow.Start(rig.Flow.Prepare("p",out _)!,true,updates.Add)!;await Barrier(rig.Run);rig.Flow.DraftEdited();await Release(rig.Run);await job;Assert.DoesNotContain(updates,u=>u.Event.Kind==ModelEventKind.Completed);Assert.False(rig.Gate.HasConsentTo(rig.Origin));}
    private sealed class PrivateDirectory:ISecretDirectoryPolicy{public void EnsurePrivate(string directory)=>Directory.CreateDirectory(directory);}
    private sealed class NoSecrets:ISecretProtector
    {public byte[] Protect(ReadOnlySpan<byte> value)=>throw new InvalidOperationException("No key fixture.");public byte[] Unprotect(ReadOnlySpan<byte> value)=>throw new InvalidOperationException("No key fixture.");}
    [Fact] public async Task ActualShellEnableStillRequiresOwnContentConfirmationAfterSuccessfulSample()
    {
        string root=Path.Combine(Path.GetTempPath(),"kuaiyi-consent-shell-"+Guid.NewGuid().ToString("N"));string run=Guid.NewGuid().ToString("N");var prefix=new Uri(fixture.BaseUri,$"runs/{run}/fragmented/");var coordinator=new SettingsCoordinator(new(Path.Combine(root,"config")),new(Path.Combine(root,"secrets"),new NoSecrets(),new PrivateDirectory()));
        ShellLifecycle? shell=null;ApiConnectionTestWorkflow? test=null;using var provider=new ProviderClient();
        try
        {
            using(var draft=await coordinator.OpenDraftAsync()){draft.Update(s=>s with{ActiveProfileId="p",Profiles=[new(){Id="p",BaseUrl=prefix.AbsoluteUri,Model="PUBLIC_MODEL",Authentication=ApiAuthentication.None,AllowLoopbackHttp=true}]});await coordinator.SaveAsync(draft);}
            shell=new(coordinator);test=new(shell.Gate,()=>coordinator.Effective,provider,new Destination(),(_,_,_)=>throw new InvalidOperationException("No key fixture."));await shell.InitializeAsync();await test.Start(test.Prepare("p",out _)!,true,_=>{})!;Assert.Single(await Requests(run));Assert.True(shell.TryPrepareEnable(out var enable,out _));Assert.False(shell.Gate.HasConsentTo(enable!.Origin));Assert.False(shell.ConfirmEnable(enable,false));Assert.Equal(Activation.Off,shell.Gate.Snapshot.Activation);
            Assert.True(shell.ConfirmEnable(enable,true));Assert.True(shell.Gate.HasConsentTo(enable.Origin));Assert.Equal(Activation.On,shell.Gate.Snapshot.Activation);
        }
        finally{shell?.Gate.Disable();if(test!=null)await test.DisposeAsync();shell?.Dispose();if(Directory.Exists(root))Directory.Delete(root,true);}
    }

}
