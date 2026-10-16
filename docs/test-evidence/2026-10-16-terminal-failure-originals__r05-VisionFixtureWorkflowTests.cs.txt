using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests;
public sealed class VisionFixtureWorkflowTests
{
    public static IEnumerable<object[]> SuccessCases()
    {foreach(var p in Enum.GetValues<Protocol>())foreach(bool stream in new[]{false,true})foreach(var cap in Enum.GetValues<Capability>())yield return [p,stream,cap];}
    [Theory][MemberData(nameof(SuccessCases))]
    public async Task DedicatedVisionPurposeUsesOnlyExactBuiltinImageOnTwoProtocolsAndNeverChangesCapabilities(Protocol protocol,bool stream,Capability capability)
    {
        await using var r=await VisionFixtureRig.Create(protocol,stream,capability);var saved=r.Coordinator.Effective;var intent=r.Prepare();Assert.Equal(ApiTestPurpose.VisionFixture,intent.Purpose);Assert.Equal(capability,intent.Plan.Transport.VisionCapability);Assert.Null(r.Flow.Start(intent,false,r.Updates.Enqueue));Assert.Equal(0,r.Script.Requests);
        _=r.Start(intent);await r.Requests(1);var id=r.Updates.First().Identity;Assert.True(r.Gate.IsVisionTest(id));Assert.False(r.Gate.IsConnectionTest(id));Assert.True(r.Gate.IsVisionTestDestination(id,intent.Plan.Transport.ResolveEndpoint(),intent.Profile.Protocol));Assert.False(r.Gate.HasRunningGeneration);Assert.Equal(Activation.Off,r.Gate.Snapshot.Activation);
        await r.Reply();var wire=r.Wire();Assert.Equal(ApiVisionTestContent.System,wire.System);Assert.Equal(ApiVisionTestContent.Source,wire.Source);Assert.Equal(ApiVisionTestContent.CreateImage(),wire.Image);Assert.Equal(ApiVisionTestContent.Sha256,Convert.ToHexString(SHA256.HashData(wire.Image!)).ToLowerInvariant());Assert.Equal(1,r.Script.Requests);Assert.Equal(0,r.SecretReads);
        Assert.Contains(r.Updates,u=>u.Event.Kind==ModelEventKind.Completed);Assert.All(r.Updates,u=>{Assert.Equal(id,u.Identity);Assert.Equal(ApiTestPurpose.VisionFixture,u.Purpose);Assert.Equal(capability,u.Plan.Transport.VisionCapability);});Assert.Same(saved,r.Coordinator.Effective);Assert.Equal(capability,r.Settings.Data.Profiles[0].Capabilities.Vision);Assert.Equal(Activation.Off,r.Gate.Snapshot.Activation);Assert.False(r.Gate.CanApply(id));Assert.Null(r.Flow.Start(intent,true,r.Updates.Enqueue));
    }
    [Fact] public void CallerCanNeverMutateTheEmbeddedSample()
    {var a=ApiVisionTestContent.CreateImage();var b=ApiVisionTestContent.CreateImage();Assert.NotSame(a,b);a[0]^=1;Assert.NotEqual(a,b);Assert.Equal(b,ApiVisionTestContent.CreateImage());Assert.Equal(2034,b.Length);}
    public static IEnumerable<object[]> TamperedRequests()
    {foreach(var p in Enum.GetValues<Protocol>())foreach(string change in new[]{"source","system","missing","length","bytes","mime","headers","authheader","authentication","version"})yield return [p,change];}
    [Theory][MemberData(nameof(TamperedRequests))]
    public async Task DedicatedIdentityRejectsAnyNonBuiltinContentOrExtraConfigurationBeforeHttp(Protocol protocol,string change)
    {
        await using var r=await VisionFixtureRig.Create(protocol);var intent=r.Prepare();Assert.True(r.Gate.TryStartVisionTest(intent.Binding,intent.Plan.Transport.ResolveEndpoint(),intent.Profile.Protocol,intent.Epoch,out var id,out var token));var request=new TranslationRequest(intent.Plan.Transport,ApiVisionTestContent.System,ApiVisionTestContent.Source,ApiVisionTestContent.CreateImage(),ApiVisionTestContent.Mime);
        request=change switch{"source"=>request with{SourceText="PRIVATE_SOURCE_NOT_ALLOWED"},"system"=>request with{SystemPrompt="PRIVATE_SYSTEM_NOT_ALLOWED"},"missing"=>request with{Image=null},"length"=>request with{Image=[1,2,3]},"mime"=>request with{ImageMime="image/jpeg"},"headers"=>request with{Profile=request.Profile with{Headers=ImmutableDictionary<string,string>.Empty.Add("X-Public-Test","not-approved")}},"authheader"=>request with{Profile=request.Profile with{AuthenticationHeader="X-Public-Test"}},"authentication"=>request with{Profile=request.Profile with{Authentication=Authentication.Bearer}},"version"=>request with{Profile=request.Profile with{AnthropicVersion="2025-01-01"}},_=>request};if(change=="bytes")request.Image![0]^=1;
        var events=new List<ModelEvent>();await foreach(var e in r.Provider.GenerateAsync(request,r.Gate,id,null,token))events.Add(e);Assert.Single(events);Assert.Equal(ModelEventKind.Failed,events[0].Kind);Assert.Equal(0,r.Script.Requests);Assert.Equal(0,r.SecretReads);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task FinalAdmissionSerializesThePrivateFixedSnapshotEvenIfCallerMutatesItsArray(Protocol protocol)
    {
        await using var r=await VisionFixtureRig.Create(protocol);var intent=r.Prepare();Assert.True(r.Gate.TryStartVisionTest(intent.Binding,intent.Plan.Transport.ResolveEndpoint(),intent.Profile.Protocol,intent.Epoch,out var id,out var token));byte[] caller=ApiVisionTestContent.CreateImage();var events=new List<ModelEvent>();
        async Task Run(){await foreach(var e in r.Provider.GenerateAsync(new(intent.Plan.Transport,ApiVisionTestContent.System,ApiVisionTestContent.Source,caller),r.Gate,id,null,token,()=>{caller[0]^=1;return true;}))events.Add(e);}r.Job=Run();await r.Requests(1);await r.Reply();Assert.NotEqual(ApiVisionTestContent.CreateImage(),caller);Assert.Equal(ApiVisionTestContent.CreateImage(),r.Wire().Image);Assert.Contains(events,e=>e.Kind==ModelEventKind.Completed);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task ChangingTheAuthorizedEndpointOrProtocolCannotReuseAVisionIdentity(Protocol protocol)
    {
        await using var r=await VisionFixtureRig.Create(protocol);var intent=r.Prepare();Assert.True(r.Gate.TryStartVisionTest(intent.Binding,intent.Plan.Transport.ResolveEndpoint(),intent.Profile.Protocol,intent.Epoch,out var id,out var token));var other=new Uri(r.Server.BaseUri,$"runs/{Guid.NewGuid():N}/fragmented/");var otherBoundary=DevelopmentTranslationBoundary.FromPrefix(other);var settingsProfile=intent.Profile with{BaseUrl=other.AbsoluteUri};var transport=ApiProfileMapper.Plan(settingsProfile,otherBoundary).Transport;
        using var independent=new ProviderClient();var events=new List<ModelEvent>();await foreach(var e in independent.GenerateAsync(new(transport,ApiVisionTestContent.System,ApiVisionTestContent.Source,ApiVisionTestContent.CreateImage()),r.Gate,id,null,token))events.Add(e);Assert.Contains(events,e=>e.Reason=="CANCELLED_OR_UNCONSENTED");Assert.Empty(r.Script.Bodies);
        var swapped=protocol==Protocol.OpenAIChat?KuaiYi.Core.Settings.ApiProtocol.AnthropicMessages:KuaiYi.Core.Settings.ApiProtocol.OpenAIChat;Assert.False(r.Gate.IsVisionTestDestination(id,intent.Plan.Transport.ResolveEndpoint(),swapped));Assert.False(r.Gate.IsVisionTest(id with{RequestId=Guid.NewGuid()}));
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task OrdinaryTextTestStillRefusesTheBuiltinImageWithZeroHttp(Protocol protocol)
    {
        await using var r=await VisionFixtureRig.Create(protocol);var intent=r.Prepare(vision:false);Assert.True(r.Gate.TryStartConnectionTest(intent.Binding,new(intent.Plan.Transport.ResolveEndpoint()),intent.Epoch,out var id,out var token));var events=new List<ModelEvent>();await foreach(var e in r.Provider.GenerateAsync(new(intent.Plan.Transport,ApiConnectionTestContent.System,ApiConnectionTestContent.Source,ApiVisionTestContent.CreateImage()),r.Gate,id,null,token))events.Add(e);
        Assert.Equal("FIXED_TEST_CONTENT_REQUIRED",Assert.Single(events).Reason);Assert.True(r.Gate.IsConnectionTest(id));Assert.False(r.Gate.IsVisionTest(id));Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData(Protocol.OpenAIChat,Capability.Unknown)][InlineData(Protocol.OpenAIChat,Capability.Unsupported)][InlineData(Protocol.AnthropicMessages,Capability.Unknown)][InlineData(Protocol.AnthropicMessages,Capability.Unsupported)]
    public async Task OrdinaryImagesRemainBlockedForUnknownOrUnsupported(Protocol protocol,Capability capability)
    {
        await using var r=await VisionFixtureRig.Create(protocol,capability:capability);r.Gate.SetSuspension(Suspension.Settings,false);r.Gate.ConsentTo(new(r.Prefix));Assert.True(r.Gate.TryEnable(new(r.Prefix),true));Assert.True(r.Gate.TryGetProfileBinding("p",out var binding));Assert.True(r.Gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var token));var plan=ApiProfileMapper.Plan(r.Settings.Data.Profiles[0],r.Boundary);var events=new List<ModelEvent>();await foreach(var e in r.Provider.GenerateAsync(new(plan.Transport,ApiVisionTestContent.System,ApiVisionTestContent.Source,ApiVisionTestContent.CreateImage()),r.Gate,id,null,token))events.Add(e);
        Assert.Equal("VISION_UNAVAILABLE_OR_IMAGE_LIMIT",Assert.Single(events).Reason);Assert.Equal(capability,plan.Transport.VisionCapability);Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task NoLocalAuthorizationBlocksPreparationAndEvenManuallyInstalledPurposeWithoutConnecting(Protocol protocol)
    {
        await using var r=await VisionFixtureRig.Create(protocol,boundary:false);Assert.Null(r.Flow.PrepareVision("p",out var reason));Assert.Equal("VISION_TEST_LOCAL_FIXTURE_ONLY",reason);Assert.Equal(0,r.SecretReads);var text=r.Prepare(false);Assert.Null(r.Flow.Start(text with{Purpose=ApiTestPurpose.VisionFixture},true,r.Updates.Enqueue));
        Assert.True(r.Gate.TryStartVisionTest(text.Binding,text.Plan.Transport.ResolveEndpoint(),text.Profile.Protocol,text.Epoch,out var id,out var token));int dials=0;using var provider=new ProviderClient(null,null,null,(_,_)=>{dials++;throw new InvalidOperationException("No connection allowed.");});var events=new List<ModelEvent>();await foreach(var e in provider.GenerateAsync(new(text.Plan.Transport,ApiVisionTestContent.System,ApiVisionTestContent.Source,ApiVisionTestContent.CreateImage()),r.Gate,id,null,token))events.Add(e);Assert.Equal("VISION_TEST_LOCAL_FIXTURE_ONLY",Assert.Single(events).Reason);Assert.Equal(0,dials);Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData("clone")][InlineData("purpose")][InlineData("transport")][InlineData("profile")][InlineData("reject")][InlineData("draft")][InlineData("epoch")]
    public async Task OnlyTheExactUnconsumedIntentCanStartAfterConfirmation(string change)
    {
        await using var r=await VisionFixtureRig.Create();var intent=r.Prepare();var selected=change switch{"clone"=>intent with{},"purpose"=>intent with{Purpose=ApiTestPurpose.TextConnection},"transport"=>intent with{Plan=intent.Plan with{Transport=intent.Plan.Transport with{Model="wrong"}}},"profile"=>intent with{Profile=intent.Profile with{Model="wrong"}},_=>intent};
        if(change=="reject")r.Flow.Reject(intent);if(change=="draft")r.Flow.DraftEdited();if(change=="epoch")r.Gate.SetMode(TriggerMode.Drop);Assert.Null(r.Flow.Start(selected,true,r.Updates.Enqueue));Assert.Equal(0,r.Script.Requests);Assert.Equal(0,r.SecretReads);
    }
    [Theory][InlineData(Protocol.OpenAIChat,429,"RATE_LIMITED")][InlineData(Protocol.OpenAIChat,503,"SERVICE_UNAVAILABLE")][InlineData(Protocol.AnthropicMessages,429,"RATE_LIMITED")][InlineData(Protocol.AnthropicMessages,503,"SERVICE_UNAVAILABLE")]
    public async Task OneExplicitProbeNeverRetriesEvenForTransientStatus(Protocol protocol,int status,string reason)
    {
        await using var r=await VisionFixtureRig.Create(protocol);r.Script.StatusCode=status;_=r.Start();await r.Job.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(1,r.Script.Requests);Assert.Contains(r.Updates,u=>u.Event.Kind==ModelEventKind.Failed&&u.Event.Reason==reason);Assert.DoesNotContain(r.Updates,u=>u.Event.Kind==ModelEventKind.Completed);Assert.Equal(Capability.Unknown,r.Settings.Data.Profiles[0].Capabilities.Vision);
    }
}
