using System.Collections.Immutable;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class ApiProfilePolicyTests
{
    [Theory] [InlineData("https://gateway.invalid/v1","",ApiProtocol.OpenAIChat,"https://gateway.invalid/v1/chat/completions")]
    [InlineData("https://gateway.invalid/proxy/vendor/v1/","/chat/completions",ApiProtocol.OpenAIChat,"https://gateway.invalid/proxy/vendor/v1/chat/completions")]
    [InlineData("https://gateway.invalid/proxy/vendor/v1","",ApiProtocol.AnthropicMessages,"https://gateway.invalid/proxy/vendor/v1/messages")]
    [InlineData("https://gateway.invalid/proxy/","/custom/completion",ApiProtocol.OpenAIChat,"https://gateway.invalid/proxy/custom/completion")]
    public void PreviewAndMappingSharePrefixPreservingResolver(string url,string path,ApiProtocol protocol,string result)
    {Assert.Equal(result,ApiProfilePolicy.Resolve(new(){BaseUrl=url,EndpointPath=path,Protocol=protocol}).AbsoluteUri);}
    [Theory] [InlineData("https://gateway.invalid/v1/chat/completions")] [InlineData("https://gateway.invalid/proxy/messages/")]
    public void CompleteAddressInPrefixModeIsNeverSilentlyAppended(string url)
    {var profile=new ApiProfile{BaseUrl=url};Assert.Equal("profile.baseIsCompleteEndpoint",Assert.Throws<SettingsValidationException>(()=>ApiProfilePolicy.Resolve(profile)).Field);Assert.Equal(new Uri(url),ApiProfilePolicy.Resolve(profile with{AddressMode=ApiAddressMode.ExactEndpoint}));}
    [Theory] [InlineData("http://127.0.0.1:4321/v1",false,false,false)] [InlineData("http://127.0.0.1:4321/v1",true,false,true)]
    [InlineData("http://192.0.2.1/v1",true,false,false)] [InlineData("http://192.0.2.1/v1",false,true,true)]
    public void HttpRequiresTheRightExplicitRiskSetting(string url,bool local,bool remote,bool allowed)
    {var profile=new ApiProfile{BaseUrl=url,AllowLoopbackHttp=local,AllowInsecureHttp=remote};if(allowed)Assert.Equal("http",ApiProfilePolicy.Resolve(profile).Scheme);else Assert.Throws<SettingsValidationException>(()=>ApiProfilePolicy.Resolve(profile));}
    [Theory] [InlineData("../messages")] [InlineData("https://elsewhere.invalid/v1/messages")] [InlineData("chat?key=test")]
    [InlineData("chat#fragment")] [InlineData("chat%2fcompletion")] [InlineData("chat\\completion")]
    public void AmbiguousOrOriginChangingPathIsRejected(string endpoint)=>Assert.Throws<SettingsValidationException>(()=>ApiProfilePolicy.Resolve(new(){BaseUrl="https://gateway.invalid/proxy",EndpointPath=endpoint}));
    [Theory] [InlineData("Host")] [InlineData("Content-Length")] [InlineData("Connection")] [InlineData("Cookie")]
    [InlineData("Proxy-Authorization")] [InlineData("Transfer-Encoding")] [InlineData("Sec-Fetch-Site")]
    public void TransportControlHeadersAreNeverUserOverridable(string header)
    {Assert.False(ApiProfilePolicy.HeaderNameAllowed(header,false));Assert.False(ApiProfilePolicy.HeaderNameAllowed(header,true));}
    [Theory] [InlineData("Authorization")] [InlineData("X-Api-Key")] [InlineData("X-Token")] [InlineData("X-Password")]
    public void CredentialHeadersRequireEncryptedAuthenticationRoute(string header)
    {Assert.False(ApiProfilePolicy.HeaderNameAllowed(header,false));Assert.True(ApiProfilePolicy.HeaderNameAllowed(header,true));}
    [Theory] [InlineData(-0.1,ApiProtocol.OpenAIChat)] [InlineData(2.01,ApiProtocol.OpenAIChat)] [InlineData(1.01,ApiProtocol.AnthropicMessages)]
    public void TemperatureBoundsAreProtocolSpecific(double temperature,ApiProtocol protocol)=>Assert.Throws<SettingsValidationException>(()=>ApiProfilePolicy.ValidateParameters(new(){Protocol=protocol,Temperature=temperature}));
    [Theory] [InlineData(OutputTokenParameter.Omit)] [InlineData(OutputTokenParameter.MaxCompletionTokens)]
    public void AnthropicAlwaysRequiresItsOutputTokenField(OutputTokenParameter output)=>Assert.Throws<SettingsValidationException>(()=>ApiProfilePolicy.ValidateParameters(new(){Protocol=ApiProtocol.AnthropicMessages,OutputLimitParameter=output}));
    [Fact] public void HeaderInjectionAndDuplicateCaseCannotPersist()
    {
        Assert.Throws<SettingsValidationException>(()=>ApiProfilePolicy.ValidateParameters(new(){Headers=ImmutableDictionary<string,string>.Empty.Add("X-Region","safe\r\nAuthorization: unsafe")}));
        Assert.Throws<SettingsValidationException>(()=>ApiProfilePolicy.ValidateParameters(new(){Headers=ImmutableDictionary<string,string>.Empty.Add("X-Region","a").Add("x-region","b")}));
    }
    [Fact] public void ExtendedProfileSurvivesRealSchemaSerializationWithoutPlainKey()
    {
        var settings=new AppSettings{Profiles=[new(){Id="p",Model="public-model",BaseUrl="https://public.invalid/proxy",EndpointPath="/custom",Authentication=ApiAuthentication.CustomHeader,AuthenticationHeader="X-Credential",SecretRef="key/"+new string('a',32),Temperature=0.4,OutputLimitParameter=OutputTokenParameter.MaxTokens,MaxContextTokens=10000,ImageByteLimit=65536,Capabilities=new(){Temperature=Capability.Supported}}],ActiveProfileId="p"};
        byte[] serialized=SettingsJson.Write(settings);var restored=SettingsJson.Read(serialized,out bool migrated);Assert.False(migrated);Assert.True(ApiProfilePolicy.RequestEquivalent(settings.Profiles[0],restored.Profiles[0]));Assert.Contains("temperature",SettingsJson.Schema().ToJsonString());Assert.DoesNotContain("PUBLIC_FIXTURE_SECRET",System.Text.Encoding.UTF8.GetString(serialized));
    }
    private static (RuntimeGate Gate,RequestProfileBinding Binding,EndpointOrigin Origin) Gate()
    {
        var gate=new RuntimeGate(requireProfileBinding:true);var profile=new ApiProfile{Id="p",Model="public",BaseUrl="https://public.invalid",Authentication=ApiAuthentication.None};gate.ObserveSettingsMetadata(new(new(){Profiles=[profile],ActiveProfileId="p"},"p1",SettingsLoadState.Current));gate.TryGetProfileBinding("p",out var binding);var origin=new EndpointOrigin(new Uri(profile.BaseUrl));gate.ConsentTo(origin);return(gate,binding,origin);
    }
    [Fact] public void FixedTestDoesNotEnableOrPermitPassiveCaptureFromOff()
    {
        var (gate,binding,origin)=Gate();Assert.True(gate.TryStartConnectionTest(binding,origin,gate.Snapshot.Epoch,out var id,out var ct));Assert.Equal(Activation.Off,gate.Snapshot.Activation);Assert.True(gate.IsConnectionTest(id));Assert.False(gate.TryBeginPassiveCapture(TriggerMode.Selection,out _));Assert.True(gate.CanSend(id,origin));gate.Disable();Assert.True(ct.IsCancellationRequested);Assert.False(gate.CanApply(id));
    }
    [Theory] [InlineData("running")] [InlineData("lock")] [InlineData("capture")] [InlineData("epoch")]
    public void FixedTestCannotCompeteOrUseStaleAdmission(string reason)
    {
        var (gate,binding,origin)=Gate();long expected=gate.Snapshot.Epoch;
        if(reason=="running"){gate.TryEnable(origin,true);gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out _,out _);expected=gate.Snapshot.Epoch;}
        else if(reason=="lock")gate.SetSessionLocked(true);else if(reason=="capture")gate.SetSuspension(Suspension.Capture,true);else gate.Disable();
        Assert.False(gate.TryStartConnectionTest(binding,origin,expected,out _,out _));
    }
    [Fact] public void CompletedGenerationDoesNotBlockFixedTestAndCacheIdentityBecomesStale()
    {
        var(gate,binding,origin)=Gate();gate.TryEnable(origin,true);gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out var old,out _);gate.MarkRequestTerminal(old);
        Assert.False(gate.HasRunningGeneration);Assert.True(gate.TryStartConnectionTest(binding,origin,gate.Snapshot.Epoch,out var probe,out _));Assert.False(gate.CanApply(old));Assert.True(gate.CanApply(probe));Assert.Equal(Activation.On,gate.Snapshot.Activation);
    }
}
