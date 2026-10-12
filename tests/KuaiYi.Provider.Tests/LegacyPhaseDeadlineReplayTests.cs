using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests;
public sealed class LegacyPhaseDeadlineReplayTests(PhaseHttpFixture fixture):IClassFixture<PhaseHttpFixture>
{
    private sealed class Rig:IAsyncDisposable
    {
        public PhaseClock Clock=new();public RuntimeGate Gate=new();public ProviderClient Provider;public KuaiYi.Providers.ApiProfile Profile;public PhaseHttpFixture.Script Script;public List<ModelEvent> Events=[];public Task Job;public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Rig(PhaseHttpFixture fixture,Protocol protocol,string mime,bool headers=true,RequestTimeouts? timeouts=null)
        {
            string run=Guid.NewGuid().ToString("N");Script=fixture.Add(run,mime,headers);Profile=new(protocol,new(fixture.BaseUri,run+"/"),protocol==Protocol.OpenAIChat?"chat/completions":"messages","PUBLIC",Authentication.None,AllowLoopbackHttp:true,Timeouts:timeouts??new(){HeadersSeconds=2,FirstResponseSeconds=3,IdleSeconds=2});Provider=new(phaseClock:Clock);var origin=new EndpointOrigin(Profile.ResolveEndpoint());Gate.ConsentTo(origin);Gate.TryEnable(origin,true);Gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var ct);
            Job=Run();async Task Run(){await foreach(var item in Provider.GenerateAsync(new(Profile,"PUBLIC_SYSTEM","PUBLIC_SOURCE"),Gate,id,null,ct)){Events.Add(item);if(item.Kind==ModelEventKind.Started)Started.TrySetResult();}}
        }
        public async Task AssertTimedOut(int seconds,string reason){Clock.Advance(TimeSpan.FromSeconds(seconds));await Task.WhenAny(Job,Task.Delay(500));Assert.True(Job.IsCompleted,"Configured phase must terminate this real socket independently of total.");await Job;Assert.Contains(Events,e=>e.Reason==reason);Assert.DoesNotContain(Events,e=>e.Kind==ModelEventKind.Completed);}
        public async ValueTask DisposeAsync(){Gate.Disable();Provider.Dispose();await Job.WaitAsync(TimeSpan.FromSeconds(6));}
    }
    [Theory] [InlineData(Protocol.OpenAIChat)] [InlineData(Protocol.AnthropicMessages)]
    public async Task JsonBodyWithoutValidProtocolHasItsOwnFirstDeadline(Protocol protocol)
    {await using var rig=new Rig(fixture,protocol,"application/json");await rig.Started.Task.WaitAsync(TimeSpan.FromSeconds(6));await rig.Script.Write(" ");await rig.Clock.WaitDeadline(3);await rig.Clock.WaitDeadline(3);await rig.AssertTimedOut(3,"FIRST_RESPONSE_TIMEOUT");}
    [Theory] [InlineData(Protocol.OpenAIChat)] [InlineData(Protocol.AnthropicMessages)]
    public async Task KeepaliveCannotReplaceFirstEffectiveProtocolResponse(Protocol protocol)
    {await using var rig=new Rig(fixture,protocol,"text/event-stream");await rig.Started.Task.WaitAsync(TimeSpan.FromSeconds(6));await rig.Script.Write(protocol==Protocol.OpenAIChat?": keepalive\n\n":"data: {\"type\":\"ping\"}\n\n");await rig.Clock.WaitDeadline(3);await rig.AssertTimedOut(3,"FIRST_RESPONSE_TIMEOUT");}
    [Theory] [InlineData(Protocol.OpenAIChat)] [InlineData(Protocol.AnthropicMessages)]
    public async Task ResponseHeadersUseTheSavedFrozenLimit(Protocol protocol)
    {await using var rig=new Rig(fixture,protocol,"text/event-stream",headers:false);await rig.Script.Received.Task.WaitAsync(TimeSpan.FromSeconds(6));await rig.AssertTimedOut(2,"RESPONSE_HEADERS_TIMEOUT");}
}
