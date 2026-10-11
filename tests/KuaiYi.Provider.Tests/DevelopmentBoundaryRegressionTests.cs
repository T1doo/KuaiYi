using System.Collections.Immutable;
using System.Net.Http.Json;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.MockProvider;
using KuaiYi.Providers;
using Xunit;
using SettingsProfile=KuaiYi.Core.Settings.ApiProfile;
namespace KuaiYi.Provider.Tests;
public sealed class DevelopmentBoundaryRegressionTests(ServerFixture fixture):IClassFixture<ServerFixture>
{
    private sealed class Destination:SynchronizationContext{public override void Post(SendOrPostCallback action,object? state)=>action(state);}
    private sealed class Rig:IAsyncDisposable
    {
        public RuntimeGate Gate=new(requireProfileBinding:true);public ProviderClient Provider=new();public SettingsSnapshot Settings;
        public TextTranslationCoordinator Flow;public string Run;public Uri Prefix;public int SecretReads;public List<TextTranslationUpdate> Updates=[];
        public Rig(Uri root,SynchronizationContext? destination=null)
        {
            Run=Guid.NewGuid().ToString("N");Prefix=new(root,$"runs/{Run}/fragmented/");Settings=new(DevelopmentTranslationProfile.Create(Prefix),"start",SettingsLoadState.Current);
            Gate.ObserveSettingsMetadata(Settings);Gate.ConsentTo(new(Prefix));Gate.TryEnable(new(Prefix),true);
            Flow=new(Gate,()=>Settings,(_,_)=>true,Provider,destination??new Destination(),new(Gate),Updates.Add,
                (_,_,_)=>{SecretReads++;throw new InvalidOperationException("Synthetic test must not read a secret.");},allowDevelopmentLoopback:true);
        }
        public SelectionSourceSnapshot Source()=>new(new(91,92342,132000,@"C:\Fixtures\editor.exe"),"PUBLIC_BOUNDARY_FIXTURE","PUBLIC_BOUNDARY_FIXTURE",[],[1],Settings.Version,23,false);
        public void Edit(string edit)
        {Settings=Settings with{Data=Settings.Data with{Profiles=[Changed(Settings.Data.Profiles[0],edit)]},Version=Guid.NewGuid().ToString("N")};Gate.ObserveSettingsMetadata(Settings);}
        public async ValueTask DisposeAsync(){Gate.Disable();await Flow.DisposeAsync();Provider.Dispose();}
    }
    private static SettingsProfile Changed(SettingsProfile profile,string field)=>field switch
    {
        "remote"=>profile with{BaseUrl="https://external.invalid/v1/"},
        "other-port"=>profile with{BaseUrl=new UriBuilder(profile.BaseUrl){Port=new Uri(profile.BaseUrl).Port==65535?65534:new Uri(profile.BaseUrl).Port+1}.Uri.AbsoluteUri},
        "other-run"=>profile with{BaseUrl=profile.BaseUrl.Replace(new Uri(profile.BaseUrl).AbsolutePath.Split('/',StringSplitOptions.RemoveEmptyEntries)[1],"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",StringComparison.Ordinal)},
        "bearer"=>profile with{Authentication=ApiAuthentication.Bearer},
        "api-key"=>profile with{Authentication=ApiAuthentication.ApiKey},
        "secret-reference"=>profile with{SecretRef="key/"+new string('a',32)},
        "model"=>profile with{Model="REAL_MODEL_MUST_NOT_BE_USED"},
        "header"=>profile with{Headers=ImmutableDictionary<string,string>.Empty.Add("OpenAI-Beta","assistants=v2")},
        _=>throw new ArgumentException("Unknown public mutation.")
    };
    private async Task<CapturedRequest[]> Requests(string run)
    {using var http=new HttpClient();return (await http.GetFromJsonAsync<CapturedRequest[]>(new Uri(fixture.BaseUri,$"requests/{run}")))!;}
    [Theory] [InlineData("remote")] [InlineData("bearer")] [InlineData("api-key")] [InlineData("secret-reference")] [InlineData("model")] [InlineData("header")]
    public void ProductionProfileMapperRejectsUnsafeTestModeProfile(string edit)
    {
        var prefix=new Uri(fixture.BaseUri,$"runs/{Guid.NewGuid():N}/fragmented/");var profile=DevelopmentTranslationProfile.Create(prefix).Profiles[0];
        Assert.Throws<ArgumentException>(()=>TextTranslationCoordinator.ProfileTransport(Changed(profile,edit),true));
    }
    [Theory] [InlineData("remote")] [InlineData("other-port")] [InlineData("other-run")] [InlineData("bearer")] [InlineData("api-key")] [InlineData("secret-reference")] [InlineData("model")] [InlineData("header")]
    public async Task SettingsEditCannotFreezeAnUnsafeTextOffer(string edit)
    {
        await using var rig=new Rig(fixture.BaseUri);rig.Edit(edit);Assert.Null(rig.Flow.Prepare(rig.Source()));Assert.Equal(0,rig.SecretReads);Assert.Empty(await Requests(rig.Run));
    }
    [Theory] [InlineData("remote")] [InlineData("bearer")] [InlineData("secret-reference")]
    public async Task LateFrozenOfferIsRejectedWithoutSecretOrHttp(string edit)
    {
        await using var rig=new Rig(fixture.BaseUri);var offer=rig.Flow.Prepare(rig.Source())!;Assert.NotNull(offer);rig.Edit(edit);
        Assert.Equal(TextSubmission.Rejected,rig.Flow.Submit(offer));Assert.Equal(0,rig.SecretReads);Assert.Empty(await Requests(rig.Run));
    }
    [Theory] [InlineData("remote")] [InlineData("other-port")] [InlineData("model")] [InlineData("header")]
    public async Task RetainedSnapshotCannotRegenerateWhileTestSettingsEscapeBoundary(string edit)
    {
        await using var rig=new Rig(fixture.BaseUri);var offer=rig.Flow.Prepare(rig.Source())!;Assert.Equal(TextSubmission.Started,rig.Flow.Submit(offer));await rig.Flow.Running;
        Assert.Equal(MockProviderHost.FixtureOutput,rig.Flow.Results.Text);Assert.Single(await Requests(rig.Run));rig.Edit(edit);
        Assert.Null(rig.Flow.PrepareRegeneration());Assert.Null(rig.Flow.PrepareRegeneration(true));Assert.Single(await Requests(rig.Run));Assert.Equal(0,rig.SecretReads);
    }
    [Theory] [InlineData("remote")] [InlineData("auth")]
    public void FrozenTransportCannotBeClonedOutsideItsTestAuthorization(string edit)
    {
        var prefix=new Uri(fixture.BaseUri,$"runs/{Guid.NewGuid():N}/fragmented/");var transport=TextTranslationCoordinator.ProfileTransport(DevelopmentTranslationProfile.Create(prefix).Profiles[0],true);
        var changed=edit=="remote"?transport with{BaseUrl=new("https://external.invalid/"),Endpoint="chat/completions"}:transport with{Authentication=Authentication.Bearer};
        Assert.Throws<ArgumentException>(()=>changed.ResolveEndpoint());
    }
    [Theory] [InlineData("other-run")] [InlineData("bearer")]
    public async Task RealTransportRejectsClonedTestProfileBeforeAnyRequest(string edit)
    {
        string original=Guid.NewGuid().ToString("N"),target=Guid.NewGuid().ToString("N");var prefix=new Uri(fixture.BaseUri,$"runs/{original}/fragmented/");
        var transport=TextTranslationCoordinator.ProfileTransport(DevelopmentTranslationProfile.Create(prefix).Profiles[0],true);
        transport=edit=="other-run"?transport with{BaseUrl=new(fixture.BaseUri,$"runs/{target}/fragmented/")}:transport with{Authentication=Authentication.Bearer};
        var gate=new RuntimeGate();gate.ConsentTo(new(prefix));Assert.True(gate.TryEnable(new(prefix),true));Assert.True(gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var ct));
        using var provider=new ProviderClient();using var secret=new KuaiYi.Providers.SecretLease(System.Text.Encoding.UTF8.GetBytes(MockProviderHost.FixtureCredential));var events=new List<ModelEvent>();
        await foreach(var item in provider.GenerateAsync(new(transport,PromptBuilder.Build("faithful"),"PUBLIC_BOUNDARY_FIXTURE"),gate,id,edit=="bearer"?secret:null,ct))events.Add(item);
        Assert.Contains(events,e=>e.Kind==ModelEventKind.Failed);Assert.DoesNotContain(events,e=>e.Kind is ModelEventKind.Started or ModelEventKind.TextDelta or ModelEventKind.Completed);
        Assert.Empty(await Requests(original));Assert.Empty(await Requests(target));
    }
    [Theory] [InlineData("marker-remove")] [InlineData("marker-change")] [InlineData("secret")] [InlineData("auth")]
    public async Task StartupBoundClientCannotBeWidenedByAProfileClone(string edit)
    {
        string original=Guid.NewGuid().ToString("N"),target=Guid.NewGuid().ToString("N");var prefix=new Uri(fixture.BaseUri,$"runs/{original}/fragmented/");
        var boundary=DevelopmentTranslationBoundary.FromPrefix(prefix);var profile=TextTranslationCoordinator.ProfileTransport(DevelopmentTranslationProfile.Create(prefix).Profiles[0],true,boundary);
        var targetPrefix=new Uri(fixture.BaseUri,$"runs/{target}/fragmented/");
        profile=edit switch
        {
            "marker-remove"=>profile with{BaseUrl=targetPrefix,DevelopmentBoundary=null},
            "marker-change"=>profile with{BaseUrl=targetPrefix,DevelopmentBoundary=DevelopmentTranslationBoundary.FromPrefix(targetPrefix)},
            "auth"=>profile with{Authentication=Authentication.Bearer,DevelopmentBoundary=null},
            _=>profile
        };
        var gate=new RuntimeGate();gate.ConsentTo(new(prefix));gate.TryEnable(new(prefix),true);gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var ct);
        using var provider=new ProviderClient(boundary);using var secret=new KuaiYi.Providers.SecretLease(System.Text.Encoding.UTF8.GetBytes(MockProviderHost.FixtureCredential));var events=new List<ModelEvent>();
        await foreach(var item in provider.GenerateAsync(new(profile,PromptBuilder.Build("faithful"),"PUBLIC_BOUNDARY_FIXTURE"),gate,id,edit is "secret" or "auth"?secret:null,ct))events.Add(item);
        Assert.Equal("TEST_BOUNDARY_REJECTED",Assert.Single(events).Reason);Assert.Empty(await Requests(original));Assert.Empty(await Requests(target));
    }
    [Theory] [InlineData(false,KuaiYi.Core.Settings.ApiProtocol.OpenAIChat)] [InlineData(true,KuaiYi.Core.Settings.ApiProtocol.OpenAIChat)]
    [InlineData(false,KuaiYi.Core.Settings.ApiProtocol.AnthropicMessages)] [InlineData(true,KuaiYi.Core.Settings.ApiProtocol.AnthropicMessages)]
    public async Task AuthorizedTestEndpointBothProtocolsWorksWithoutCredentials(bool exact,KuaiYi.Core.Settings.ApiProtocol protocol)
    {
        await using var rig=new Rig(fixture.BaseUri);var profile=rig.Settings.Data.Profiles[0] with{Protocol=protocol};
        if(exact)profile=profile with{AddressMode=ApiAddressMode.ExactEndpoint,BaseUrl=new Uri(rig.Prefix,protocol==KuaiYi.Core.Settings.ApiProtocol.OpenAIChat?"chat/completions":"messages").AbsoluteUri};
        rig.Settings=rig.Settings with{Data=rig.Settings.Data with{Profiles=[profile]},Version="valid-protocol"};rig.Gate.ObserveSettingsMetadata(rig.Settings);
        var offer=rig.Flow.Prepare(rig.Source())!;Assert.NotNull(offer);Assert.True(offer.Snapshot.Transport.IsDevelopmentFixture);rig.Flow.Submit(offer);await rig.Flow.Running;
        Assert.Equal(MockProviderHost.FixtureOutput,rig.Flow.Results.Text);Assert.True(rig.Flow.SelectedSnapshot!.Transport.IsDevelopmentFixture);Assert.Equal(0,rig.SecretReads);
        var actual=Assert.Single(await Requests(rig.Run));Assert.Null(actual.AuthScheme);Assert.False(actual.HasApiKey);Assert.False(actual.CredentialInBody);
    }
    [Fact] public void LocalTestLabelRequiresAuthorizationProofRatherThanHttpPermission()
    {
        var prefix=new Uri(fixture.BaseUri,$"runs/{Guid.NewGuid():N}/fragmented/");var transport=TextTranslationCoordinator.ProfileTransport(DevelopmentTranslationProfile.Create(prefix).Profiles[0],true);
        Assert.True(transport.IsDevelopmentFixture);Assert.False((transport with{DevelopmentBoundary=null}).IsDevelopmentFixture);
        Assert.False((transport with{BaseUrl=new("https://external.invalid/"),Endpoint="chat/completions"}).IsDevelopmentFixture);
        Assert.False((transport with{Authentication=Authentication.ApiKey}).IsDevelopmentFixture);
    }
    private sealed class PrivateDirectory:ISecretDirectoryPolicy{public void EnsurePrivate(string path)=>Directory.CreateDirectory(path);}
    private sealed class NoSecrets:ISecretProtector
    {public byte[] Protect(ReadOnlySpan<byte> bytes)=>throw new InvalidOperationException("No fixture key configured.");public byte[] Unprotect(ReadOnlySpan<byte> bytes)=>throw new InvalidOperationException("No secret may be opened.");}
    [Theory] [InlineData("committed")] [InlineData("recovered")]
    public async Task RealSettingsCommitAndBackupRecoveryCannotWidenStartupAuthorization(string phase)
    {
        string root=Path.Combine(Path.GetTempPath(),"kuaiyi-boundary-settings-"+Guid.NewGuid().ToString("N"));string run=Guid.NewGuid().ToString("N");var prefix=new Uri(fixture.BaseUri,$"runs/{run}/fragmented/");
        var boundary=DevelopmentTranslationBoundary.FromPrefix(prefix);var store=new SettingsStore(Path.Combine(root,"config"));var coordinator=new SettingsCoordinator(store,new(Path.Combine(root,"secrets"),new NoSecrets(),new PrivateDirectory()));
        var gate=new RuntimeGate(requireProfileBinding:true);using var link=new SettingsRuntimeLink(coordinator,gate);using var provider=new ProviderClient(boundary);int secretReads=0;
        await using var flow=new TextTranslationCoordinator(gate,()=>coordinator.Effective,(_,_)=>true,provider,new Destination(),new(gate),_=>{},(_,_,_)=>{secretReads++;throw new InvalidOperationException("No secret may be opened.");},developmentBoundary:boundary);
        void Observe(SettingsSnapshot value,SettingsChange change)=>flow.ObserveSettings(value);coordinator.Committed+=Observe;
        try
        {
            using(var draft=await coordinator.OpenDraftAsync()){draft.Update(_=>DevelopmentTranslationProfile.Create(prefix));await coordinator.SaveAsync(draft);}
            link.Initialize(coordinator.Effective!);gate.ConsentTo(new(prefix));Assert.True(gate.TryEnable(new(prefix),true));
            using(var draft=await coordinator.OpenDraftAsync()){draft.Update(s=>s with{Profiles=[Changed(s.Profiles[0],"remote")]});await coordinator.SaveAsync(draft);}
            Assert.Equal(Activation.Off,gate.Snapshot.Activation);
            if(phase=="recovered")
            {
                using(var draft=await coordinator.OpenDraftAsync()){draft.Update(s=>s with{Profiles=[s.Profiles[0] with{Name="public recovery fixture"}]});await coordinator.SaveAsync(draft);}
                await File.WriteAllTextAsync(store.PrimaryPath,"PUBLIC_CORRUPT_FIXTURE");using var draftRecovery=await coordinator.OpenDraftAsync();Assert.True(draftRecovery.Basis.RequiresRecovery);
                await coordinator.SaveAsync(draftRecovery,SettingsRecovery.UseBackup);Assert.StartsWith("https://external.invalid",coordinator.Effective!.Data.Profiles[0].BaseUrl);
            }
            // Even independently granting broad Gate consent cannot relax the client's/flow's startup boundary.
            var origin=new EndpointOrigin(new Uri("https://external.invalid"));gate.ConsentTo(origin);gate.TryEnable(origin,true);
            var source=new SelectionSourceSnapshot(new(91,92342,132000,@"C:\Fixtures\editor.exe"),"PUBLIC_BOUNDARY_FIXTURE","PUBLIC_BOUNDARY_FIXTURE",[],[1],coordinator.Effective!.Version,23,false);
            Assert.Null(flow.Prepare(source));Assert.Equal(Activation.Off,gate.Snapshot.Activation);Assert.Equal(0,secretReads);Assert.Empty(await Requests(run));
        }
        finally{gate.Disable();coordinator.Committed-=Observe;if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    private sealed class QueuedDestination:SynchronizationContext
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> items=new();public TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override void Post(SendOrPostCallback action,object? state){items.Enqueue(()=>action(state));Entered.TrySetResult();}
        public void Drain(){while(items.TryDequeue(out var action))action();}
    }
    [Theory] [InlineData("remote")] [InlineData("bearer")]
    public async Task CommittedUnsafeSettingsCancelOldRealHttpBeforeQueuedUiDelivery(string edit)
    {
        var queue=new QueuedDestination();await using var rig=new Rig(fixture.BaseUri,queue);rig.Flow.Submit(rig.Flow.Prepare(rig.Source())!);
        await queue.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));rig.Edit(edit);rig.Flow.ObserveSettings(rig.Settings);
        Assert.Equal(Activation.Off,rig.Gate.Snapshot.Activation);await rig.Flow.Running.WaitAsync(TimeSpan.FromSeconds(5));queue.Drain();
        Assert.Empty(rig.Flow.Results.Text);Assert.DoesNotContain(rig.Updates,u=>u.Presentation.Kind!=OverlayResultEventKind.Begin);Assert.Single(await Requests(rig.Run));Assert.Equal(0,rig.SecretReads);
    }
    [Fact] public async Task LocalhostTestUsesPinnedLoopbackAndNoCredentials()
    {
        var localhost=new UriBuilder(fixture.BaseUri){Host="localhost"}.Uri;await using var rig=new Rig(localhost);var offer=rig.Flow.Prepare(rig.Source())!;
        rig.Flow.Submit(offer);await rig.Flow.Running;Assert.Equal(MockProviderHost.FixtureOutput,rig.Flow.Results.Text);Assert.True(rig.Flow.SelectedSnapshot!.Transport.IsDevelopmentFixture);
        Assert.Null(Assert.Single(await Requests(rig.Run)).AuthScheme);Assert.Equal(0,rig.SecretReads);
    }

}
