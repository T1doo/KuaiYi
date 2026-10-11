using System.Collections.Immutable;
using System.Net.Http.Json;
using System.Text.Json;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.MockProvider;
using KuaiYi.Providers;
using Xunit;
using Profile=KuaiYi.Core.Settings.ApiProfile;
using Protocol=KuaiYi.Core.Settings.ApiProtocol;
namespace KuaiYi.Provider.Tests;
public sealed class ApiProfileIntegrationTests(ServerFixture fixture):IClassFixture<ServerFixture>
{
    private sealed class Destination:SynchronizationContext{public override void Post(SendOrPostCallback action,object? state)=>action(state);}
    private sealed class Rig:IAsyncDisposable
    {
        public RuntimeGate Gate=new(requireProfileBinding:true);public ProviderClient Provider=new();public SettingsSnapshot Settings;
        public ApiConnectionTestWorkflow Test;public List<ApiTestUpdate> Updates=[];public string Run;public Uri Prefix;public int SecretReads;public string Credential=MockProviderHost.FixtureCredential;
        public Rig(Uri root,string scenario="fragmented",Protocol protocol=Protocol.OpenAIChat,ApiAuthentication auth=ApiAuthentication.None)
        {
            Run=Guid.NewGuid().ToString("N");Prefix=new(root,$"runs/{Run}/{scenario}/proxy/vendor/v1/");
            Settings=new(new(){ActiveProfileId="p",Profiles=[new(){Id="p",Model="PUBLIC_MODEL",Protocol=protocol,BaseUrl=Prefix.AbsoluteUri,AllowLoopbackHttp=true,Authentication=auth,AuthenticationHeader=auth==ApiAuthentication.CustomHeader?"X-Fixture-Credential":"",SecretRef=auth==ApiAuthentication.None?null:"key/"+new string('a',32)}]},"start",SettingsLoadState.Current);
            Gate.ObserveSettingsMetadata(Settings);Test=new(Gate,()=>Settings,Provider,new Destination(),(_,_,_)=>{SecretReads++;return Task.FromResult<KuaiYi.Providers.SecretLease?>(new(System.Text.Encoding.UTF8.GetBytes(Credential)));});
        }
        public void Edit(Func<Profile,Profile> change){Settings=Settings with{Data=Settings.Data with{Profiles=[change(Settings.Data.Profiles[0])]},Version=Guid.NewGuid().ToString("N")};Gate.ObserveSettingsMetadata(Settings);}
        public async Task Start(){var intent=Test.Prepare("p",out var reason);Assert.NotNull(intent);Assert.Equal("",reason);var job=Test.Start(intent!,true,Updates.Add);Assert.NotNull(job);await job!;}
        public async ValueTask DisposeAsync(){Gate.Disable();await Test.DisposeAsync();Provider.Dispose();}
    }
    private async Task<CapturedRequest[]> Requests(string run){using var http=new HttpClient();return(await http.GetFromJsonAsync<CapturedRequest[]>(new Uri(fixture.BaseUri,$"requests/{run}")))!;}
    private async Task Barrier(string run)
    {using var http=new HttpClient();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(true){var reply=await http.GetFromJsonAsync<JsonElement>(new Uri(fixture.BaseUri,$"barriers/{run}"),timeout.Token);if(reply.GetProperty("ready").GetBoolean())return;await Task.Delay(10,timeout.Token);}}
    private async Task Release(string run){using var http=new HttpClient();using var reply=await http.PostAsync(new Uri(fixture.BaseUri,$"barriers/{run}/release"),null);reply.EnsureSuccessStatusCode();}
    [Theory] [InlineData(Protocol.OpenAIChat,ApiAuthentication.Bearer)] [InlineData(Protocol.AnthropicMessages,ApiAuthentication.ApiKey)]
    [InlineData(Protocol.OpenAIChat,ApiAuthentication.CustomHeader)] [InlineData(Protocol.AnthropicMessages,ApiAuthentication.None)]
    public async Task SavedProfileMapsAuthBodyAndFixedSampleThroughRealHttpWhileOff(Protocol protocol,ApiAuthentication auth)
    {
        await using var rig=new Rig(fixture.BaseUri,protocol:protocol,auth:auth);await rig.Start();Assert.Equal(Activation.Off,rig.Gate.Snapshot.Activation);Assert.False(rig.Gate.TryBeginPassiveCapture(TriggerMode.Selection,out _));
        var actual=Assert.Single(await Requests(rig.Run));Assert.EndsWith(protocol==Protocol.OpenAIChat?"/proxy/vendor/v1/chat/completions":"/proxy/vendor/v1/messages",actual.Path);Assert.False(actual.CredentialInBody);
        Assert.Equal(auth==ApiAuthentication.None?0:1,rig.SecretReads);if(auth==ApiAuthentication.CustomHeader)Assert.Contains("X-Fixture-Credential",actual.FixtureCredentialHeaders!);if(auth==ApiAuthentication.Bearer)Assert.Equal("Bearer",actual.AuthScheme);if(auth==ApiAuthentication.ApiKey)Assert.True(actual.HasApiKey);
        using var body=JsonDocument.Parse(actual.Body);Assert.Equal("PUBLIC_MODEL",body.RootElement.GetProperty("model").GetString());
        string source=protocol==Protocol.OpenAIChat?body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!:body.RootElement.GetProperty("messages")[0].GetProperty("content")[0].GetProperty("text").GetString()!;
        Assert.Equal(ApiConnectionTestContent.Source,source);Assert.Contains(rig.Updates,e=>e.Event.Kind==ModelEventKind.Completed);Assert.Equal(MockProviderHost.FixtureOutput,string.Concat(rig.Updates.Where(e=>e.Event.Kind==ModelEventKind.TextDelta).Select(e=>e.Event.Text)));
    }
    [Theory] [InlineData(OutputTokenParameter.ProtocolDefault,"max_completion_tokens")] [InlineData(OutputTokenParameter.MaxCompletionTokens,"max_completion_tokens")]
    [InlineData(OutputTokenParameter.MaxTokens,"max_tokens")] [InlineData(OutputTokenParameter.Omit,null)]
    public async Task OutputParameterIsMappedExactlyOnce(OutputTokenParameter parameter,string? expected)
    {
        await using var rig=new Rig(fixture.BaseUri);rig.Edit(p=>p with{OutputLimitParameter=parameter,MaxOutputTokens=1234});await rig.Start();using var body=JsonDocument.Parse(Assert.Single(await Requests(rig.Run)).Body);
        foreach(string name in new[]{"max_completion_tokens","max_tokens"}){Assert.Equal(name==expected,body.RootElement.TryGetProperty(name,out var value));if(name==expected)Assert.Equal(1234,value.GetInt32());}
    }
    [Theory] [InlineData(Capability.Unknown,false)] [InlineData(Capability.Unsupported,false)] [InlineData(Capability.Supported,true)]
    public async Task TemperatureAndUsageRequireExplicitCapability(Capability capability,bool sent)
    {
        await using var rig=new Rig(fixture.BaseUri);rig.Edit(p=>p with{Temperature=0.3,Capabilities=p.Capabilities with{Temperature=capability,UsageInStream=capability},SendStoreFalse=true,Headers=ImmutableDictionary<string,string>.Empty.Add("X-Fixture-Region","public-eu")});await rig.Start();var request=Assert.Single(await Requests(rig.Run));using var body=JsonDocument.Parse(request.Body);
        Assert.Equal(sent,body.RootElement.TryGetProperty("temperature",out var temp));if(sent)Assert.Equal(0.3,temp.GetDouble());Assert.Equal(sent,body.RootElement.TryGetProperty("stream_options",out _));Assert.False(body.RootElement.GetProperty("store").GetBoolean());Assert.Equal("public-eu",request.FixtureRegion);
        Assert.Equal(capability,rig.Settings.Data.Profiles[0].Capabilities.Temperature);if(!sent)Assert.Contains(rig.Updates.SelectMany(e=>e.Plan.Notices),n=>n.Contains("省略温度"));
    }
    [Theory] [InlineData(Protocol.OpenAIChat)] [InlineData(Protocol.AnthropicMessages)]
    public async Task ExplicitUnsupportedStreamingSendsFalseAndDoesNotFakeTyping(Protocol protocol)
    {
        await using var rig=new Rig(fixture.BaseUri,"non-streaming",protocol);rig.Edit(p=>p with{Capabilities=p.Capabilities with{Streaming=Capability.Unsupported,UsageInStream=Capability.Supported}});await rig.Start();using var body=JsonDocument.Parse(Assert.Single(await Requests(rig.Run)).Body);Assert.False(body.RootElement.GetProperty("stream").GetBoolean());Assert.False(body.RootElement.TryGetProperty("stream_options",out _));Assert.Single(rig.Updates,e=>e.Event.Kind==ModelEventKind.TextDelta);Assert.Contains(rig.Updates.SelectMany(e=>e.Plan.Notices),n=>n.Contains("非流式"));
    }
    [Theory] [InlineData(Protocol.OpenAIChat)] [InlineData(Protocol.AnthropicMessages)]
    public async Task CompleteEndpointAndAnthropicVersionAreNotSilentlyAltered(Protocol protocol)
    {
        await using var rig=new Rig(fixture.BaseUri,protocol:protocol);var exact=new Uri(rig.Prefix,protocol==Protocol.OpenAIChat?"custom/chat/completions":"custom/messages");rig.Edit(p=>p with{AddressMode=ApiAddressMode.ExactEndpoint,BaseUrl=exact.AbsoluteUri,Headers=protocol==Protocol.AnthropicMessages?ImmutableDictionary<string,string>.Empty.Add("anthropic-version","2024-01-01"):p.Headers});
        Assert.Equal(exact,ApiProfilePolicy.Resolve(rig.Settings.Data.Profiles[0]));await rig.Start();var request=Assert.Single(await Requests(rig.Run));Assert.Equal(exact.AbsolutePath,request.Path);if(protocol==Protocol.AnthropicMessages)Assert.Equal("2024-01-01",request.AnthropicVersion);
    }
    [Theory] [InlineData("base-complete")] [InlineData("temperature")] [InlineData("http")] [InlineData("header")]
    public async Task InvalidProfilesNeverReadSecretOrSend(string kind)
    {
        await using var rig=new Rig(fixture.BaseUri,auth:ApiAuthentication.Bearer);rig.Edit(p=>kind switch{"base-complete"=>p with{BaseUrl=new Uri(rig.Prefix,"chat/completions").AbsoluteUri},"temperature"=>p with{Temperature=9},"http"=>p with{AllowLoopbackHttp=false},_=>p with{Headers=ImmutableDictionary<string,string>.Empty.Add("Authorization","PUBLIC_BAD_FIXTURE")}});
        Assert.Null(rig.Test.Prepare("p",out string reason));Assert.Equal("CONFIG_INVALID",reason);Assert.Equal(0,rig.SecretReads);Assert.Empty(await Requests(rig.Run));
    }
    [Theory] [InlineData("decline")] [InlineData("changed")] [InlineData("locked")]
    public async Task FixedTestRequiresFreshExplicitConfirmation(string action)
    {
        await using var rig=new Rig(fixture.BaseUri,auth:ApiAuthentication.Bearer);var intent=rig.Test.Prepare("p",out _)!;
        if(action=="changed")rig.Edit(p=>p with{Model="PUBLIC_NEW_MODEL"});if(action=="locked")rig.Gate.SetSessionLocked(true);
        Assert.Null(rig.Test.Start(intent,action!="decline",rig.Updates.Add));Assert.Empty(await Requests(rig.Run));Assert.Equal(0,rig.SecretReads);
    }
    [Theory] [InlineData("stop")] [InlineData("off")] [InlineData("key")]
    public async Task InFlightFixedTestCancellationDiscardsLateHeadersAndNeverEnables(string action)
    {
        await using var rig=new Rig(fixture.BaseUri,"held-headers");var intent=rig.Test.Prepare("p",out _)!;var job=rig.Test.Start(intent,true,rig.Updates.Add)!;await Barrier(rig.Run);
        if(action=="stop")rig.Test.Cancel();else if(action=="off")rig.Gate.Disable();else rig.Edit(p=>p with{SecretRef="key/"+new string('b',32)});
        await Release(rig.Run);await job;Assert.Equal(Activation.Off,rig.Gate.Snapshot.Activation);Assert.DoesNotContain(rig.Updates,e=>e.Event.Kind==ModelEventKind.Completed||e.Event.Kind==ModelEventKind.TextDelta);Assert.Single(await Requests(rig.Run));
    }
    [Theory] [InlineData(Protocol.AnthropicMessages,"stream-error","PROVIDER_ERROR")] [InlineData(Protocol.OpenAIChat,"disconnect","STREAM_INTERRUPTED")]
    [InlineData(Protocol.OpenAIChat,"unauthorized","AUTH_FAILED")] [InlineData(Protocol.OpenAIChat,"malformed","INVALID_PROTOCOL")]
    public async Task FixedTestShowsRealProtocolErrorsWithoutClaimingSuccess(Protocol protocol,string scenario,string failure)
    {
        await using var rig=new Rig(fixture.BaseUri,scenario,protocol);await rig.Start();Assert.Contains(rig.Updates,e=>e.Event.Kind==ModelEventKind.Failed&&e.Event.Reason==failure);Assert.DoesNotContain(rig.Updates,e=>e.Event.Kind==ModelEventKind.Completed);Assert.DoesNotContain(rig.Updates,e=>e.Event.Text?.Contains(MockProviderHost.FixtureCredential)==true);
    }
    [Theory] [InlineData("source")] [InlineData("system")] [InlineData("image")]
    public async Task TransportDoesNotAllowOffTestIdentityToUploadUserContent(string changed)
    {
        await using var rig=new Rig(fixture.BaseUri);var intent=rig.Test.Prepare("p",out _)!;var origin=new EndpointOrigin(intent.Plan.Transport.ResolveEndpoint());rig.Gate.ConsentTo(origin);Assert.True(rig.Gate.TryStartConnectionTest(intent.Binding,origin,rig.Gate.Snapshot.Epoch,out var id,out var ct));
        var request=new TranslationRequest(intent.Plan.Transport,changed=="system"?"USER_PRIVATE_SYSTEM":ApiConnectionTestContent.System,changed=="source"?"USER_PRIVATE_SOURCE":ApiConnectionTestContent.Source,changed=="image"?[1,2,3]:null);var events=new List<ModelEvent>();
        await foreach(var e in rig.Provider.GenerateAsync(request,rig.Gate,id,null,ct))events.Add(e);Assert.Equal("FIXED_TEST_CONTENT_REQUIRED",Assert.Single(events).Reason);Assert.Empty(await Requests(rig.Run));
    }
    [Fact] public async Task RunningGenerationIsNeverCancelledByConnectionTest()
    {
        await using var rig=new Rig(fixture.BaseUri);var origin=new EndpointOrigin(rig.Prefix);rig.Gate.ConsentTo(origin);rig.Gate.TryEnable(origin,true);rig.Gate.TryGetProfileBinding("p",out var binding);rig.Gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var ct);
        Assert.Null(rig.Test.Prepare("p",out var reason));Assert.Equal("STOP_GENERATION_FIRST",reason);Assert.True(rig.Gate.CanApply(id));Assert.False(ct.IsCancellationRequested);Assert.Empty(await Requests(rig.Run));
    }
    [Theory] [InlineData("model")] [InlineData("selection")]
    public async Task ProductionTranslationConsumerCancelsItsOwnRequestWhenConfigurationSwitches(string change)
    {
        await using var rig=new Rig(fixture.BaseUri,"held-headers");var origin=new EndpointOrigin(rig.Prefix);rig.Gate.ConsentTo(origin);rig.Gate.TryEnable(origin,true);
        await using var translation=new TextTranslationCoordinator(rig.Gate,()=>rig.Settings,(_,_)=>true,rig.Provider,new Destination(),new(rig.Gate),_=>{});
        var source=new SelectionSourceSnapshot(new(44,92345,132000,@"C:\Fixtures\editor.exe"),"PUBLIC_SELECTED_FIXTURE","PUBLIC_SELECTED_FIXTURE",[],[1],rig.Settings.Version,23,false);
        Assert.Equal(TextSubmission.Started,translation.Submit(translation.Prepare(source)!));await Barrier(rig.Run);var active=translation.Results.Identity!.Value;
        if(change=="model")rig.Edit(p=>p with{Model="NEW_PUBLIC_MODEL"});else{rig.Settings=rig.Settings with{Data=rig.Settings.Data with{ActiveProfileId=null},Version="changed-selection"};rig.Gate.ObserveSettingsMetadata(rig.Settings);}
        translation.ObserveSettings(rig.Settings);await Release(rig.Run);await translation.Running;Assert.False(rig.Gate.CanApply(active));Assert.Empty(translation.Results.Text);Assert.Single(await Requests(rig.Run));
    }
    private sealed class PrivateDirectory:ISecretDirectoryPolicy{public void EnsurePrivate(string directory)=>Directory.CreateDirectory(directory);}
    private sealed class FixtureProtector:ISecretProtector
    {
        private readonly byte[] key=Enumerable.Repeat((byte)83,32).ToArray();public int Reads;
        public byte[] Protect(ReadOnlySpan<byte> source){byte[] result=new byte[source.Length+28];System.Security.Cryptography.RandomNumberGenerator.Fill(result.AsSpan(0,12));using var cipher=new System.Security.Cryptography.AesGcm(key,16);cipher.Encrypt(result.AsSpan(0,12),source,result.AsSpan(28),result.AsSpan(12,16));return result;}
        public byte[] Unprotect(ReadOnlySpan<byte> source){Reads++;byte[] result=new byte[source.Length-28];using var cipher=new System.Security.Cryptography.AesGcm(key,16);cipher.Decrypt(source[..12],source[28..],source.Slice(12,16),result);return result;}
    }
    [Theory] [InlineData(Protocol.OpenAIChat)] [InlineData(Protocol.AnthropicMessages)]
    public async Task ActualSettingsFileEncryptedSyntheticKeyToProductionRequestAndStream(Protocol protocol)
    {
        string root=Path.Combine(Path.GetTempPath(),"kuaiyi-p4-profile-"+Guid.NewGuid().ToString("N")),run=Guid.NewGuid().ToString("N");var prefix=new Uri(fixture.BaseUri,$"runs/{run}/fragmented/proxy/v1/");
        var protector=new FixtureProtector();var coordinator=new SettingsCoordinator(new(Path.Combine(root,"config")),new(Path.Combine(root,"secrets"),protector,new PrivateDirectory()));var gate=new RuntimeGate(requireProfileBinding:true);using var link=new SettingsRuntimeLink(coordinator,gate);using var provider=new ProviderClient();var updates=new List<ApiTestUpdate>();
        await using var test=new ApiConnectionTestWorkflow(gate,()=>coordinator.Effective,provider,new Destination(),async(id,intent,ct)=>{using var lease=await coordinator.OpenRequestSecretAsync(gate,id,intent.Binding,new(intent.Plan.Transport.ResolveEndpoint()),ct);return new KuaiYi.Providers.SecretLease(lease.Bytes.Span);});
        try
        {
            using(var draft=await coordinator.OpenDraftAsync())
            {draft.Update(s=>s with{Profiles=[new(){Id="fixture",Protocol=protocol,BaseUrl=prefix.AbsoluteUri,Model="PUBLIC_SAVED_MODEL",AllowLoopbackHttp=true,Authentication=protocol==Protocol.OpenAIChat?ApiAuthentication.Bearer:ApiAuthentication.ApiKey,Temperature=0.4,Capabilities=new(){Temperature=Capability.Supported},MaxOutputTokens=256}],ActiveProfileId="fixture"});draft.SetKey("fixture",System.Text.Encoding.UTF8.GetBytes(MockProviderHost.FixtureCredential));await coordinator.SaveAsync(draft);}
            Assert.Equal(0,protector.Reads);Assert.DoesNotContain(MockProviderHost.FixtureCredential,await File.ReadAllTextAsync(Path.Combine(root,"config","settings.json")));
            link.Initialize(coordinator.Effective!);var intent=test.Prepare("fixture",out var reason);Assert.NotNull(intent);Assert.Equal("",reason);await test.Start(intent!,true,updates.Add)!;
            Assert.Equal(1,protector.Reads);Assert.Equal(Activation.Off,gate.Snapshot.Activation);Assert.Contains(updates,e=>e.Event.Kind==ModelEventKind.Completed);var request=Assert.Single(await Requests(run));Assert.False(request.CredentialInBody);Assert.Single(request.FixtureCredentialHeaders!);using var body=JsonDocument.Parse(request.Body);Assert.Equal(0.4,body.RootElement.GetProperty("temperature").GetDouble());
        }
        finally{gate.Disable();if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Theory] [InlineData(Capability.Unknown,false)] [InlineData(Capability.Unsupported,false)] [InlineData(Capability.Supported,true)]
    public async Task VisionCapabilityIsNeverInferredFromModelName(Capability capability,bool allowed)
    {
        await using var rig=new Rig(fixture.BaseUri);rig.Edit(p=>p with{Model="PUBLIC_VISION_NAMED_MODEL",Capabilities=p.Capabilities with{Vision=capability}});var transport=ApiProfileMapper.Plan(rig.Settings.Data.Profiles[0]).Transport;
        var origin=new EndpointOrigin(rig.Prefix);rig.Gate.ConsentTo(origin);rig.Gate.TryEnable(origin,true);rig.Gate.TryGetProfileBinding("p",out var binding);rig.Gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var ct);
        var events=new List<ModelEvent>();await foreach(var value in rig.Provider.GenerateAsync(new(transport,PromptBuilder.Build("faithful"),"PUBLIC_IMAGE_SCHEMA_FIXTURE",[137,80,78,71]),rig.Gate,id,null,ct))events.Add(value);
        Assert.Equal(capability,transport.VisionCapability);if(allowed){Assert.Single(await Requests(rig.Run));Assert.Contains(events,e=>e.Kind==ModelEventKind.Completed);}else{Assert.Empty(await Requests(rig.Run));Assert.Equal("VISION_UNAVAILABLE_OR_IMAGE_LIMIT",Assert.Single(events).Reason);}
    }
    [Fact] public async Task ConfiguredImageByteLimitStopsBeforeHttp()
    {
        await using var rig=new Rig(fixture.BaseUri);rig.Edit(p=>p with{ImageByteLimit=65536,Capabilities=p.Capabilities with{Vision=Capability.Supported}});var transport=ApiProfileMapper.Plan(rig.Settings.Data.Profiles[0]).Transport;
        var origin=new EndpointOrigin(rig.Prefix);rig.Gate.ConsentTo(origin);rig.Gate.TryEnable(origin,true);rig.Gate.TryGetProfileBinding("p",out var binding);rig.Gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var ct);
        var events=new List<ModelEvent>();await foreach(var value in rig.Provider.GenerateAsync(new(transport,PromptBuilder.Build("faithful"),"PUBLIC_IMAGE_SCHEMA_FIXTURE",new byte[65537]),rig.Gate,id,null,ct))events.Add(value);
        Assert.Empty(await Requests(rig.Run));Assert.Equal("VISION_UNAVAILABLE_OR_IMAGE_LIMIT",Assert.Single(events).Reason);
    }
    [Fact] public async Task NewParameterMappingCannotRelaxSignedDevelopmentBoundary()
    {
        string run=Guid.NewGuid().ToString("N");var prefix=new Uri(fixture.BaseUri,$"runs/{run}/fragmented/");var boundary=DevelopmentTranslationBoundary.FromPrefix(prefix);var profile=DevelopmentTranslationProfile.Create(prefix).Profiles[0];
        Assert.Throws<ArgumentException>(()=>ApiProfileMapper.Plan(profile with{EndpointPath="different/request"},boundary));
        Assert.Throws<ArgumentException>(()=>ApiProfileMapper.Plan(profile with{Authentication=ApiAuthentication.CustomHeader,AuthenticationHeader="X-Credential"},boundary));Assert.Empty(await Requests(run));
    }

    [Fact] public async Task CustomEncryptedAuthenticationSupportsSchemesButNeverHeaderInjection()
    {
        await using var rig=new Rig(fixture.BaseUri,auth:ApiAuthentication.CustomHeader);rig.Credential="Token "+MockProviderHost.FixtureCredential;await rig.Start();Assert.Contains("X-Fixture-Credential",Assert.Single(await Requests(rig.Run)).FixtureCredentialHeaders!);
        using var injected=new KuaiYi.Providers.SecretLease("PUBLIC\r\nHost: unsafe"u8);Assert.Throws<ArgumentException>(()=>injected.HeaderValue(true));
    }

}
