using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net.Http.Json;
using System.Text.Json;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.MockProvider;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Core.Settings.ApiProtocol;
namespace KuaiYi.Provider.Tests;
public sealed class TextTranslationTests(ServerFixture fixture):IClassFixture<ServerFixture>
{
    // Protocol fixtures intentionally exercise synthetic auth and faults. They are not the app's explicit development mode.
    private static KuaiYi.Providers.ApiProfile FixtureTransport(KuaiYi.Core.Settings.ApiProfile profile)
    {
        var protocol=profile.Protocol==Protocol.OpenAIChat?KuaiYi.Providers.ApiProtocol.OpenAIChat:KuaiYi.Providers.ApiProtocol.AnthropicMessages;
        var uri=new Uri(profile.BaseUrl);if(!uri.IsLoopback||uri.Scheme!="http")throw new InvalidOperationException("Protocol fixture must be local.");
        return new(protocol,uri,profile.AddressMode==ApiAddressMode.ExactEndpoint?uri.AbsoluteUri:protocol==KuaiYi.Providers.ApiProtocol.OpenAIChat?"chat/completions":"messages",profile.Model,
            profile.Authentication switch{ApiAuthentication.None=>Authentication.None,ApiAuthentication.ApiKey=>Authentication.ApiKey,_=>Authentication.Bearer},
            protocol==KuaiYi.Providers.ApiProtocol.AnthropicMessages?OutputLimitParameter.MaxTokens:OutputLimitParameter.MaxCompletionTokens,profile.MaxOutputTokens,
            profile.Streaming&&profile.Capabilities.Streaming!=Capability.Unsupported,AllowLoopbackHttp:true,TotalTimeoutSeconds:profile.TimeoutSeconds);
    }
    private sealed class Destination:SynchronizationContext
    {public override void Post(SendOrPostCallback action,object? state)=>action(state);}
    private sealed class Rig:IAsyncDisposable
    {
        public RuntimeGate Gate=new(requireProfileBinding:true);public ProviderClient Provider=new();
        public SettingsSnapshot Settings;public OverlayResults Results;public TextTranslationCoordinator Flow;
        public bool SourceValid=true;public ConcurrentQueue<TextTranslationUpdate> Updates=new();public int SecretReads;
        public TaskCompletionSource SecretEntered=new(TaskCreationOptions.RunContinuationsAsynchronously),SecretRelease=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool HoldSecret;public string Run;public Uri Url;
        public Rig(Uri root,string scenario="fragmented",Protocol protocol=Protocol.OpenAIChat,bool authentication=false)
        {
            Run=Guid.NewGuid().ToString("N");Url=new(root,$"runs/{Run}/{scenario}/");
            Settings=new(new(){ActiveProfileId="p",Profiles=[new(){Id="p",Protocol=protocol,BaseUrl=Url.AbsoluteUri,Model="TEST_MODEL",Authentication=authentication?ApiAuthentication.Bearer:ApiAuthentication.None,SecretRef=authentication?"key/"+new string('a',32):null}]},"initial",SettingsLoadState.Current);
            Gate.ObserveSettingsMetadata(Settings);Gate.ConsentTo(new(Url));Assert.True(Gate.TryEnable(new(Url),true));Results=new(Gate);
            Flow=new(Gate,()=>Settings,(_,_)=>SourceValid,Provider,new Destination(),Results,Updates.Enqueue,
                async(_,_,ct)=>{Interlocked.Increment(ref SecretReads);SecretEntered.TrySetResult();if(HoldSecret)await SecretRelease.Task.WaitAsync(ct);return new KuaiYi.Providers.SecretLease("TEST_ONLY_DO_NOT_USE_AS_A_REAL_KEY"u8);},transportFactory:FixtureTransport);
        }
        public SelectionSourceSnapshot Source(string text="Actual selected text",SelectionSourceKind kind=SelectionSourceKind.Uia)=>new(new(17,92823,132000,@"C:\Fixtures\fixture-editor.exe"),text,text,[new(1,2,20,22)],[7,8],Settings.Version,text.EnumerateRunes().Count(),false,kind);
        public TextSubmission Start(string text="Actual selected text")=>Flow.Submit(Flow.Prepare(Source(text))!);
        public void Change(Func<AppSettings,AppSettings> transform,SettingsChange? change=null)
        {Settings=Settings with{Data=transform(Settings.Data),Version=Guid.NewGuid().ToString("N")};Gate.ApplyCommittedSettings(Settings,change);}
        public async ValueTask DisposeAsync(){Gate.Disable();SecretRelease.TrySetResult();await Flow.DisposeAsync();Provider.Dispose();}
    }
    private async Task<CapturedRequest[]> Requests(Rig rig)
    {using var http=new HttpClient();return (await http.GetFromJsonAsync<CapturedRequest[]>(new Uri(fixture.BaseUri,$"requests/{rig.Run}")))!;}
    private async Task WaitBarrier(Rig rig)
    {
        using var http=new HttpClient();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while(true){var state=await http.GetFromJsonAsync<JsonElement>(new Uri(fixture.BaseUri,$"barriers/{rig.Run}"),timeout.Token);if(state.GetProperty("ready").GetBoolean())return;await Task.Delay(10,timeout.Token);}
    }
    private async Task Release(Rig rig)
    {using var http=new HttpClient();using var response=await http.PostAsync(new Uri(fixture.BaseUri,$"barriers/{rig.Run}/release"),null);response.EnsureSuccessStatusCode();}
    [Theory]
    [InlineData(Protocol.OpenAIChat,"fragmented",true)] [InlineData(Protocol.AnthropicMessages,"fragmented",true)]
    [InlineData(Protocol.OpenAIChat,"non-streaming",false)] [InlineData(Protocol.AnthropicMessages,"non-streaming",false)]
    public async Task ActualSourceCrossesRealHttpAndReachesResults(Protocol protocol,string scenario,bool stream)
    {
        await using var rig=new Rig(fixture.BaseUri,scenario,protocol);rig.Change(s=>s with{Profiles=[s.Profiles[0] with{Streaming=stream}]});
        Assert.Equal(TextSubmission.Started,rig.Start("Original ⛵ paragraph\n    source_code()"));await rig.Flow.Running;
        Assert.Equal(MockProviderHost.FixtureOutput,rig.Results.Text);Assert.True(Assert.Single(rig.Results.Versions).Complete);
        Assert.Contains(rig.Updates,u=>u.Presentation.Kind==OverlayResultEventKind.Delta);Assert.Contains(rig.Updates,u=>u.Presentation.Kind==OverlayResultEventKind.Complete);
        var request=Assert.Single(await Requests(rig));using var json=JsonDocument.Parse(request.Body);
        var root=json.RootElement;Assert.Equal("TEST_MODEL",root.GetProperty("model").GetString());
        string input=protocol==Protocol.OpenAIChat?root.GetProperty("messages")[1].GetProperty("content").GetString()!:root.GetProperty("messages")[0].GetProperty("content")[0].GetProperty("text").GetString()!;
        Assert.Equal("Original ⛵ paragraph\n    source_code()",input);Assert.Null(request.AuthScheme);Assert.False(request.CredentialInBody);
    }
    [Theory] [InlineData("off")] [InlineData("lock")] [InlineData("stop")] [InlineData("delete")] [InlineData("replace-key")]
    public async Task InFlightInvalidationRejectsLateHttpAndUi(string action)
    {
        await using var rig=new Rig(fixture.BaseUri,"held-headers");Assert.Equal(TextSubmission.Started,rig.Start());Task old=rig.Flow.Running;await WaitBarrier(rig);
        if(action=="off")rig.Gate.Disable();else if(action=="lock")rig.Gate.SetSessionLocked(true);else if(action=="stop")rig.Gate.StopGeneration();
        else if(action=="delete")rig.Change(s=>s with{Profiles=[],ActiveProfileId=null});
        else rig.Change(s=>s with{Profiles=[s.Profiles[0] with{SecretRef="key/"+new string('b',32)}]});
        await Release(rig);await old;Assert.Empty(rig.Results.Text);Assert.DoesNotContain(rig.Updates,u=>u.Presentation.Kind!=OverlayResultEventKind.Begin);
        Assert.Single(await Requests(rig));
    }
    [Fact] public async Task NewSourceWinsOverHeldOldHeaders()
    {
        await using var rig=new Rig(fixture.BaseUri,"held-headers");rig.Start("source A");Task old=rig.Flow.Running;await WaitBarrier(rig);
        rig.Change(s=>s with{Profiles=[s.Profiles[0] with{BaseUrl=new Uri(fixture.BaseUri,$"runs/{rig.Run}/fragmented/").AbsoluteUri}]});
        Assert.Equal(TextSubmission.Started,rig.Start("source B"));await rig.Flow.Running;await Release(rig);await old;
        Assert.Equal(MockProviderHost.FixtureOutput,rig.Results.Text);Assert.Equal("source B",rig.Flow.SelectedSnapshot!.Source.Text);
        Assert.DoesNotContain(rig.Updates,u=>u.Snapshot.Source.Text=="source A"&&u.Presentation.Kind!=OverlayResultEventKind.Begin);Assert.Equal(2,(await Requests(rig)).Length);
    }
    [Theory] [InlineData(12000,false)] [InlineData(12001,true)] [InlineData(50000,true)]
    public async Task ScalarSoftBoundaryNeedsExplicitConfirmation(int count,bool confirmation)
    {
        await using var rig=new Rig(fixture.BaseUri);string source=string.Concat(Enumerable.Repeat("😀",count));var offer=rig.Flow.Prepare(rig.Source(source))!;
        Assert.NotNull(offer);Assert.Equal(confirmation,offer.RequiresConfirmation);
        Assert.Equal(confirmation?TextSubmission.ConfirmationRequired:TextSubmission.Started,rig.Flow.Submit(offer));
        if(confirmation){Assert.Empty(await Requests(rig));Assert.Equal(TextSubmission.Started,rig.Flow.Submit(offer,true));}
        await rig.Flow.Running;var request=Assert.Single(await Requests(rig));Assert.Contains("TEST_MODEL",request.Body);Assert.True(Assert.Single(rig.Results.Versions).Complete);
    }
    [Theory] [InlineData("hard")] [InlineData("count")] [InlineData("surrogate")] [InlineData("empty")]
    public async Task InvalidOrOversizeTextNeverStartsHttp(string kind)
    {
        await using var rig=new Rig(fixture.BaseUri);
        var source=kind switch{"hard"=>rig.Source(new string('a',50001)),"count"=>rig.Source() with{Scalars=1},"surrogate"=>rig.Source() with{Text="bad\ud800",Scalars=4},_=>rig.Source(" ")};
        Assert.Null(rig.Flow.Prepare(source));Assert.Empty(await Requests(rig));Assert.Empty(rig.Results.Versions);
    }
    [Theory] [InlineData("off")] [InlineData("source")] [InlineData("settings")] [InlineData("new-offer")] [InlineData("clipboard-permission")]
    public async Task PendingConfirmationCannotOutliveItsSourceOrAuthority(string action)
    {
        await using var rig=new Rig(fixture.BaseUri);rig.Change(s=>s with{Privacy=s.Privacy with{ClipboardAllowedApplications=[@"C:\Fixtures\fixture-editor.exe"]}});
        var offer=rig.Flow.Prepare(rig.Source(new string('a',12001),SelectionSourceKind.Clipboard))!;Assert.Equal(TextSubmission.ConfirmationRequired,rig.Flow.Submit(offer));
        if(action=="off")rig.Gate.Disable();else if(action=="source")rig.SourceValid=false;
        else if(action=="settings")rig.Change(s=>s with{Translation=s.Translation with{Preset=TranslationPreset.Plain}});
        else if(action=="new-offer")Assert.NotNull(rig.Flow.Prepare(rig.Source("new source")));
        else rig.Change(s=>s with{Privacy=s.Privacy with{ClipboardAllowedApplications=[]}},new([],[]){ClipboardPermissionsChanged=true});
        Assert.Equal(TextSubmission.Rejected,rig.Flow.Submit(offer,true));Assert.Empty(await Requests(rig));
    }
    [Fact] public async Task DuplicateAndReopenAddNoCallButRegenerationDoes()
    {
        await using var rig=new Rig(fixture.BaseUri);rig.Start();await rig.Flow.Running;var original=rig.Results.Identity;
        rig.Results.Collapse();Assert.Equal(TextSubmission.Reopened,rig.Start());Assert.Equal(original,rig.Results.Identity);Assert.Single(await Requests(rig));
        Assert.Equal(TextSubmission.Started,rig.Flow.Submit(rig.Flow.PrepareRegeneration()!));await rig.Flow.Running;
        Assert.Equal(2,(await Requests(rig)).Length);Assert.Equal(original!.Value.SessionId,rig.Results.Identity!.Value.SessionId);Assert.NotEqual(original.Value.VersionId,rig.Results.Identity.Value.VersionId);
    }
    [Fact] public async Task DuplicateDuringGenerationAddsNoCall()
    {
        await using var rig=new Rig(fixture.BaseUri,"held-headers");rig.Start();await WaitBarrier(rig);Task job=rig.Flow.Running;
        Assert.Equal(TextSubmission.Reopened,rig.Start());Assert.Single(await Requests(rig));await Release(rig);await job;
    }
    [Fact] public async Task OffNeverCapturesSubmitsRegeneratesOrReadsSecrets()
    {
        await using var rig=new Rig(fixture.BaseUri,authentication:true);var offer=rig.Flow.Prepare(rig.Source())!;rig.Gate.Disable();
        Assert.Equal(TextSubmission.Rejected,rig.Flow.Submit(offer));Assert.Null(rig.Flow.Prepare(rig.Source()));Assert.Null(rig.Flow.PrepareRegeneration());Assert.Empty(await Requests(rig));Assert.Equal(0,rig.SecretReads);
    }
    [Theory] [InlineData("source")] [InlineData("off")] [InlineData("config")] [InlineData("binding")]
    public async Task DelayedSecretReadMustRecheckBeforeHttp(string action)
    {
        await using var rig=new Rig(fixture.BaseUri,authentication:true){HoldSecret=true};rig.Start();await rig.SecretEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if(action=="source")rig.SourceValid=false;else if(action=="off")rig.Gate.Disable();
        else if(action=="config")rig.Change(s=>s with{Translation=s.Translation with{Preset=TranslationPreset.Plain}});
        else rig.Change(s=>s with{Profiles=[s.Profiles[0] with{SecretRef="key/"+new string('b',32)}]});
        rig.SecretRelease.TrySetResult();await rig.Flow.Running;Assert.Empty(await Requests(rig));Assert.Equal(1,rig.SecretReads);Assert.Empty(rig.Results.Text);
    }
    [Theory] [InlineData("unauthorized")] [InlineData("disconnect")] [InlineData("malformed")]
    public async Task NewSourceFailureKeepsReadablePriorSnapshot(string scenario)
    {
        await using var rig=new Rig(fixture.BaseUri);rig.Start("source old");await rig.Flow.Running;var old=rig.Results.Identity;
        rig.Change(s=>s with{Profiles=[s.Profiles[0] with{BaseUrl=new Uri(fixture.BaseUri,$"runs/{rig.Run}/{scenario}/").AbsoluteUri}]});rig.Start("source new");await rig.Flow.Running;
        Assert.Equal(old,rig.Results.Identity);Assert.Equal(MockProviderHost.FixtureOutput,rig.Results.Text);Assert.Equal("source old",rig.Flow.SelectedSnapshot!.Source.Text);
        Assert.Contains(rig.Updates,u=>u.Failure!=null);Assert.True(rig.Results.Reopen());
    }
    [Fact] public async Task FailedRegenerationPreservesSuccessAndMarksFailedVersion()
    {
        await using var rig=new Rig(fixture.BaseUri);rig.Start();await rig.Flow.Running;Guid original=rig.Results.Identity!.Value.VersionId;
        // Current-setting explicit regeneration uses a failing endpoint; ordinary regeneration remains frozen.
        rig.Change(s=>s with{Profiles=[s.Profiles[0] with{BaseUrl=new Uri(fixture.BaseUri,$"runs/{rig.Run}/disconnect/").AbsoluteUri}]});
        Assert.Equal(TextSubmission.Started,rig.Flow.Submit(rig.Flow.PrepareRegeneration(true)!));await rig.Flow.Running;
        Assert.Equal(original,rig.Results.SelectedVersionId);Assert.Equal(MockProviderHost.FixtureOutput,rig.Results.Text);
        Assert.Contains(rig.Results.Versions,v=>v.Identity.VersionId!=original&&v.Stopped&&!v.Complete);Assert.Contains(rig.Results.Versions,v=>v.Identity.VersionId==original&&v.Complete);
    }
    [Fact] public async Task FourRegenerationsStayWithinThreeAndSelectedVersionOwnsSnapshot()
    {
        await using var rig=new Rig(fixture.BaseUri);rig.Start();await rig.Flow.Running;
        for(int i=0;i<4;i++){Assert.Equal(TextSubmission.Started,rig.Flow.Submit(rig.Flow.PrepareRegeneration()!));await rig.Flow.Running;Assert.InRange(rig.Results.Versions.Count,1,3);}
        Assert.Equal(3,rig.Results.Versions.Count);var oldest=rig.Results.Versions[0];Assert.True(rig.Results.SelectVersion(oldest.Identity.VersionId));Assert.NotNull(rig.Flow.SelectedSnapshot);
        Assert.Equal(TextSubmission.Started,rig.Flow.Submit(rig.Flow.PrepareRegeneration()!));await rig.Flow.Running;Assert.Equal(3,rig.Results.Versions.Count);Assert.Equal(6,(await Requests(rig)).Length);
    }
    [Fact] public async Task OrdinaryRegenerationUsesFrozenStyleAndCurrentSettingsIsSeparate()
    {
        await using var rig=new Rig(fixture.BaseUri);rig.Start();await rig.Flow.Running;
        rig.Change(s=>s with{Translation=s.Translation with{Preset=TranslationPreset.Explain,Domain="new domain"},Profiles=[s.Profiles[0] with{Model="NEW_MODEL"}]});
        rig.Flow.Submit(rig.Flow.PrepareRegeneration()!);await rig.Flow.Running;Assert.Equal("TEST_MODEL",rig.Flow.SelectedSnapshot!.Profile.Model);Assert.Equal("base@1+faithful@1",rig.Flow.SelectedSnapshot.TemplateVersion);
        rig.Flow.Submit(rig.Flow.PrepareRegeneration(true)!);await rig.Flow.Running;Assert.Equal("NEW_MODEL",rig.Flow.SelectedSnapshot!.Profile.Model);Assert.Equal("base@1+explain@1",rig.Flow.SelectedSnapshot.TemplateVersion);
        var requests=await Requests(rig);Assert.Equal(3,requests.Length);Assert.Contains("TEST_MODEL",requests[1].Body);Assert.DoesNotContain("new domain",requests[1].Body);Assert.Contains("NEW_MODEL",requests[2].Body);
    }
    [Theory] [InlineData(TranslationPreset.Faithful,"faithful")] [InlineData(TranslationPreset.Plain,"plain")] [InlineData(TranslationPreset.Professional,"professional")] [InlineData(TranslationPreset.Explain,"explain")]
    public async Task PromptSnapshotsRecordActualTemplate(TranslationPreset preset,string template)
    {
        await using var rig=new Rig(fixture.BaseUri);rig.Change(s=>s with{Translation=s.Translation with{Preset=preset}});var offer=rig.Flow.Prepare(rig.Source())!;
        Assert.Equal($"base@1+{template}@1",offer.Snapshot.TemplateVersion);Assert.Equal(PromptBuilder.Build(template),offer.Snapshot.Prompt);
        rig.Flow.Submit(offer);await rig.Flow.Running;Assert.Equal(offer.Snapshot.Prompt,rig.Flow.SelectedSnapshot!.Prompt);
    }
    [Theory] [InlineData("https://example.org/runs/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/fragmented/")]
    [InlineData("http://192.0.2.1/runs/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/fragmented/")]
    [InlineData("http://127.0.0.1:5555/v1/")] [InlineData("http://name:password@127.0.0.1/runs/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/fragmented/")]
    public void DevelopmentModeRejectsNonFixtureOrRemote(string uri)=>Assert.Throws<ArgumentException>(()=>DevelopmentTranslationProfile.Create(new(uri)));
    [Fact] public void ProductionNeverImplicitlyEnablesLoopbackTestTransport()
    {
        var config=DevelopmentTranslationProfile.Create(new("http://127.0.0.1:5555/runs/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/fragmented/"));
        Assert.Equal(ApiAuthentication.None,Assert.Single(config.Profiles).Authentication);Assert.Null(config.Profiles[0].SecretRef);
        Assert.Throws<ArgumentException>(()=>TextTranslationCoordinator.ProfileTransport(config.Profiles[0]));Assert.True(TextTranslationCoordinator.ProfileTransport(config.Profiles[0],true).AllowLoopbackHttp);
    }
    private sealed class CapturePlatform:ISelectionCapturePlatform
    {
        public bool IsCurrent(SelectionWindow source)=>true;public SelectionCaptureResult Result=new(SelectionCaptureStatus.Success,"  Selected\r\n    code()  ",[new(1,2,20,22)],[7,8]);
        public Task<SelectionCaptureResult> CaptureAsync(SelectionGesture gesture,CancellationToken cancellation)=>Task.FromResult(Result);
    }
    [Fact] public async Task ActualCaptureCoordinatorToHttpUsesNormalizedCapturedSource()
    {
        await using var rig=new Rig(fixture.BaseUri);var platform=new CapturePlatform();SelectionSourceSnapshot? captured=null;
        await using var acquisition=new SelectionCaptureCoordinator(rig.Gate,()=>rig.Settings,platform,new(4,4,500,4,4),999,new Destination(),(source,_)=>{captured=source;Assert.Equal(TextSubmission.Started,rig.Flow.Submit(rig.Flow.Prepare(source)!));},_=>{},delay:(_,_)=>Task.CompletedTask);
        await acquisition.ExplicitAsync(rig.Source().Window,new(2,3));await rig.Flow.Running;
        Assert.Equal("  Selected\r\n    code()  ",captured!.RawText);Assert.Equal("Selected\n    code()",rig.Flow.SelectedSnapshot!.Source.Text);
        var request=Assert.Single(await Requests(rig));using var body=JsonDocument.Parse(request.Body);Assert.Equal("Selected\n    code()",body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
        var before=rig.Results.Text;platform.Result=new(SelectionCaptureStatus.EmptyOrUnsupported);await acquisition.ExplicitAsync(rig.Source().Window,new(2,3));Assert.Equal(before,rig.Results.Text);Assert.Single(await Requests(rig));
    }
    [Fact] public async Task ActualDropCoordinatorToHttpUsesDroppedCopyWithoutRecapture()
    {
        await using var rig=new Rig(fixture.BaseUri);rig.Gate.SetMode(TriggerMode.Drop);int accepted=0;
        await using var drop=new DropCaptureCoordinator(rig.Gate,()=>rig.Settings,_=>true,()=>true,999,new Destination(),(source,_)=>{accepted++;Assert.Equal(SelectionSourceKind.OleDrop,source.SourceKind);Assert.Equal(TextSubmission.Started,rig.Flow.Submit(rig.Flow.Prepare(source)!));},_=>{});
        var target=new DropTargetGeometry(new(0,0,48,48),new(0,0,48,48),24);var lease=drop.Open(Guid.NewGuid(),rig.Gate.Snapshot.Epoch,target)!;Guid transaction=Guid.NewGuid();var point=new PixelPoint(24,24);
        Assert.True(drop.Enter(lease,transaction,rig.Source().Window,point,OleEffect.Copy));Assert.True(drop.Begin(lease,transaction,rig.Source().Window,point,OleEffect.Copy));
        await drop.ReceiveAsync(lease,transaction,System.Text.Encoding.UTF8.GetBytes("Dropped actual text"),apply=>{apply();return true;});await rig.Flow.Running;
        Assert.Equal(1,accepted);Assert.Equal("Dropped actual text",rig.Flow.SelectedSnapshot!.Source.Text);Assert.Single(await Requests(rig));
    }
    [Theory] [InlineData(Protocol.OpenAIChat)] [InlineData(Protocol.AnthropicMessages)]
    public async Task PacedHttpDeltasDriveTheRealFrameAndResultControllers(Protocol protocol)
    {
        await using var rig=new Rig(fixture.BaseUri,"paced",protocol);var monitor=new MonitorGeometry("fixture",new(0,0,1920,1040),new(1));var anchor=new PixelRect(30,30,78,78);var frames=new OverlayFrames(anchor,monitor);
        var preferences=new LayoutPreferences(420,640);var heights=new List<int>();
        rig.Flow=new(rig.Gate,()=>rig.Settings,(_,_)=>rig.SourceValid,rig.Provider,new Destination(),rig.Results,update=>
        {
            rig.Updates.Enqueue(update);if(update.Presentation.Kind!=OverlayResultEventKind.Delta)return;
            if(!frames.IsExpanded)frames.OpenMeasured(preferences,_=>128+rig.Results.Text.Length*2);else frames.Grow(128+rig.Results.Text.Length*2);
            for(int i=0;i<20;i++){frames.Tick(TimeSpan.FromMilliseconds(16),false);Assert.True(frames.Current.Contains(anchor));Assert.True(monitor.WorkArea.Inset(12).Contains(frames.Current));}
            heights.Add(frames.Current.Height);
        },transportFactory:FixtureTransport);
        rig.Start();await rig.Flow.Running;Assert.Equal(6,heights.Count);Assert.True(heights.Zip(heights.Skip(1)).All(pair=>pair.First<=pair.Second));Assert.True(heights[^1]>heights[0]);
        Assert.Equal(string.Concat(Enumerable.Range(0,6).Select(i=>"公开片段"+i+"\n")),rig.Results.Text);Assert.True(Assert.Single(rig.Results.Versions).Complete);
        Assert.Single(await Requests(rig));
    }
    [Fact] public async Task ConfiguredTimeoutProducesFailureAndNoRetry()
    {
        await using var rig=new Rig(fixture.BaseUri,"held-headers");rig.Change(s=>s with{Profiles=[s.Profiles[0] with{TimeoutSeconds=1}]});rig.Start();await rig.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.Contains(rig.Updates,u=>u.Failure=="REQUEST_TIMEOUT");Assert.Single(await Requests(rig));Assert.False(Assert.Single(rig.Results.Versions).Complete);
    }
    [Fact] public async Task RapidReplacementBoundsSnapshotsAndKeepsLastSuccessfulSource()
    {
        await using var rig=new Rig(fixture.BaseUri,authentication:true);rig.Start("successful source");await rig.Flow.Running;var success=rig.Results.Identity;
        rig.HoldSecret=true;rig.SecretEntered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        for(int i=0;i<12;i++){Assert.Equal(TextSubmission.Started,rig.Start("replacement "+i));Assert.InRange(rig.Flow.RetainedSnapshotCount,1,4);}
        await rig.SecretEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));rig.SourceValid=false;rig.SecretRelease.TrySetResult();await rig.Flow.Running;
        Assert.Equal(success,rig.Results.Identity);Assert.Equal("successful source",rig.Flow.SelectedSnapshot!.Source.Text);Assert.Equal(MockProviderHost.FixtureOutput,rig.Results.Text);Assert.InRange(rig.Flow.RetainedSnapshotCount,1,3);
    }
    [Fact] public async Task SemanticProfileEqualityDeduplicatesAfterSettingsReload()
    {
        await using var rig=new Rig(fixture.BaseUri);rig.Start();await rig.Flow.Running;
        var reloaded=System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(rig.Settings.Data,SettingsJson.Options),SettingsJson.Options)!;
        rig.Change(_=>reloaded);Assert.Equal(TextSubmission.Reopened,rig.Start());Assert.Single(await Requests(rig));
    }
    [Fact] public async Task MaximumValidReaderContextHasARealPrompt()
    {
        await using var rig=new Rig(fixture.BaseUri);rig.Change(s=>s with{Translation=s.Translation with{Preset=TranslationPreset.Explain,ReaderBackground=new string('a',2048)}});
        var offer=rig.Flow.Prepare(rig.Source());Assert.NotNull(offer);Assert.Contains(new string('a',2048),offer!.Snapshot.Prompt);rig.Flow.Submit(offer);await rig.Flow.Running;Assert.Single(await Requests(rig));
    }

    private sealed class QueuedDestination:SynchronizationContext
    {
        public TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);private readonly ConcurrentQueue<Action> queue=new();
        public override void Post(SendOrPostCallback action,object? state){queue.Enqueue(()=>action(state));Entered.TrySetResult();}
        public void Drain(){while(queue.TryDequeue(out var action))action();}
    }
    [Theory] [InlineData("off")] [InlineData("replacement")]
    public async Task RealHttpDestinationQueueCannotRelabelAnOldRequest(string action)
    {
        await using var rig=new Rig(fixture.BaseUri);await rig.Flow.DisposeAsync();var destination=new QueuedDestination();
        rig.Flow=new(rig.Gate,()=>rig.Settings,(_,_)=>rig.SourceValid,rig.Provider,destination,rig.Results,rig.Updates.Enqueue,transportFactory:FixtureTransport);
        rig.Start();Task old=rig.Flow.Running;await destination.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if(action=="off")rig.Gate.Disable();else{Assert.True(rig.Gate.TryGetProfileBinding("p",out var binding));rig.Gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out _,out _);}
        await old.WaitAsync(TimeSpan.FromSeconds(5));destination.Drain();Assert.Empty(rig.Results.Text);Assert.DoesNotContain(rig.Updates,u=>u.Presentation.Kind!=OverlayResultEventKind.Begin);Assert.Single(await Requests(rig));
    }

}
