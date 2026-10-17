using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests;
public sealed class PhaseDeadlineTests(PhaseHttpFixture fixture):IClassFixture<PhaseHttpFixture>
{
    private sealed class Rig:IAsyncDisposable
    {
        public PhaseClock Clock=new();public RuntimeGate Gate=new();public ProviderClient Provider;public KuaiYi.Providers.ApiProfile Profile;public PhaseHttpFixture.Script Script;public System.Collections.Concurrent.ConcurrentQueue<ModelEvent> Events=new();public Task Job;public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously),PublicText=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Rig(PhaseHttpFixture fixture,Protocol protocol,string mime,bool headers=true,RequestTimeouts? timeouts=null,int total=120,bool sharedClock=false)
        {
            string run=Guid.NewGuid().ToString("N");Script=fixture.Add(run,mime,headers);Profile=new(protocol,new(fixture.BaseUri,run+"/"),protocol==Protocol.OpenAIChat?"chat/completions":"messages","PUBLIC",Authentication.None,AllowLoopbackHttp:true,TotalTimeoutSeconds:total,Timeouts:timeouts??new(){HeadersSeconds=2,FirstResponseSeconds=3,IdleSeconds=2});Provider=new(clock:sharedClock?Clock:null,phaseClock:Clock);var origin=new EndpointOrigin(Profile.ResolveEndpoint());Gate.ConsentTo(origin);Gate.TryEnable(origin,true);Gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var ct);
            Job=Run();async Task Run(){await foreach(var item in Provider.GenerateAsync(new(Profile,"PUBLIC_SYSTEM","PUBLIC_SOURCE"),Gate,id,null,ct)){Events.Enqueue(item);if(item.Kind==ModelEventKind.Started)Started.TrySetResult();if(item.Kind==ModelEventKind.TextDelta&&item.Text=="PUBLIC")PublicText.TrySetResult();}}
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

    private static string TextEvent(Protocol protocol,string text)=>protocol==Protocol.OpenAIChat?"data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\""+text+"\"}}]}\n\n":"data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\""+text+"\"}}\n\n";
    private static string StartEvents(Protocol protocol)=>protocol==Protocol.OpenAIChat?"":"data: {\"type\":\"message_start\",\"message\":{}}\n\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n";
    private static string JsonPrefix(Protocol protocol)=>protocol==Protocol.OpenAIChat?"{\"choices\":[{\"index\":0,\"message\":{\"content\":\"":"{\"type\":\"message\",\"content\":[{\"type\":\"text\",\"text\":\"";
    private static string JsonSuffix(Protocol protocol)=>protocol==Protocol.OpenAIChat?"\"},\"finish_reason\":\"stop\"}]}":"\"}],\"stop_reason\":\"end_turn\"}";
    [Theory] [InlineData(Protocol.OpenAIChat,"comment")] [InlineData(Protocol.OpenAIChat,"empty")] [InlineData(Protocol.OpenAIChat,"fragment")] [InlineData(Protocol.AnthropicMessages,"ping")] [InlineData(Protocol.AnthropicMessages,"unknown")] [InlineData(Protocol.AnthropicMessages,"empty")]
    public async Task IdleDeadlineKeepsPartialTextAndIgnoresNoContentActivity(Protocol protocol,string activity)
    {
        await using var rig=new Rig(fixture,protocol,"text/event-stream");await rig.Started.Task.WaitAsync(TimeSpan.FromSeconds(6));await rig.Script.Write(StartEvents(protocol)+TextEvent(protocol,"PUBLIC"));await rig.PublicText.Task.WaitAsync(TimeSpan.FromSeconds(6));await rig.Clock.WaitDeadline(2);rig.Clock.Advance(TimeSpan.FromSeconds(1));
        string ignored=activity switch{"comment"=>": keepalive\n\n","ping"=>"data: {\"type\":\"ping\"}\n\n","unknown"=>"data: {\"type\":\"PUBLIC_UNKNOWN\"}\n\n","fragment"=>"data: ",_=>TextEvent(protocol,"")};await rig.Script.Write(ignored);await Task.Delay(20);await rig.AssertTimedOut(1,"READ_IDLE_TIMEOUT");Assert.Contains(rig.Events,e=>e.Kind==ModelEventKind.TextDelta&&e.Text=="PUBLIC");Assert.Equal(1,rig.Script.Requests);
    }
    [Fact]public async Task MessageStartIdleTimerCanPrecedeTheActualPublicTextAnchor()
    {
        const Protocol protocol=Protocol.AnthropicMessages;await using var rig=new Rig(fixture,protocol,"text/event-stream");await rig.Started.Task.WaitAsync(TimeSpan.FromSeconds(6));
        await rig.Script.Write(StartEvents(protocol));await rig.Clock.WaitDeadline(2);Assert.False(rig.PublicText.Task.IsCompleted);Assert.DoesNotContain(rig.Events,e=>e.Kind==ModelEventKind.TextDelta&&e.Text=="PUBLIC");
        rig.Clock.Advance(TimeSpan.FromSeconds(1));await rig.Script.Write(TextEvent(protocol,"PUBLIC"));await rig.PublicText.Task.WaitAsync(TimeSpan.FromSeconds(6));Assert.Contains(rig.Events,e=>e.Kind==ModelEventKind.TextDelta&&e.Text=="PUBLIC");await rig.Clock.WaitDeadline(2);
        await rig.Script.Write(TextEvent(protocol,""));rig.Clock.Advance(TimeSpan.FromSeconds(1));await Task.WhenAny(rig.Job,Task.Delay(500));Assert.False(rig.Job.IsCompleted);
        await rig.AssertTimedOut(1,"READ_IDLE_TIMEOUT");Assert.Contains(rig.Events,e=>e.Kind==ModelEventKind.TextDelta&&e.Text=="PUBLIC");Assert.Equal(1,rig.Script.Requests);
    }
    [Theory] [InlineData(Protocol.OpenAIChat)] [InlineData(Protocol.AnthropicMessages)]
    public async Task ActualTextExtendsOnlyIdleDeadline(Protocol protocol)
    {
        await using var rig=new Rig(fixture,protocol,"text/event-stream");await rig.Started.Task.WaitAsync(TimeSpan.FromSeconds(6));await rig.Script.Write(StartEvents(protocol)+TextEvent(protocol,"PUBLIC_ONE"));await rig.Clock.WaitDeadline(2);rig.Clock.Advance(TimeSpan.FromMilliseconds(1500));await rig.Script.Write(TextEvent(protocol,"PUBLIC_TWO"));await rig.Clock.WaitDeadline(2);rig.Clock.Advance(TimeSpan.FromMilliseconds(500));Assert.False(rig.Job.IsCompleted);await rig.AssertTimedOut(2,"READ_IDLE_TIMEOUT");Assert.Equal(2,rig.Events.Count(e=>e.Kind==ModelEventKind.TextDelta));
    }
    [Theory] [InlineData(Protocol.OpenAIChat)] [InlineData(Protocol.AnthropicMessages)]
    public async Task RecognizableJsonTextCanProgressAcrossSlowUtf8AndEscapes(Protocol protocol)
    {
        await using var rig=new Rig(fixture,protocol,"application/json");await rig.Started.Task.WaitAsync(TimeSpan.FromSeconds(6));await rig.Script.Write(JsonPrefix(protocol));await rig.Clock.WaitDeadline(2);
        foreach(var piece in new[]{"PUBLIC", "\\n", "🌍", "\\u4F60", "好"}){rig.Clock.Advance(TimeSpan.FromMilliseconds(1500));await rig.Script.Write(piece);await rig.Clock.WaitDeadline(2);Assert.False(rig.Job.IsCompleted);}
        await rig.Script.Write(JsonSuffix(protocol),end:true);await rig.Job.WaitAsync(TimeSpan.FromSeconds(6));Assert.Contains(rig.Events,e=>e.Kind==ModelEventKind.Completed);Assert.Equal("PUBLIC\n🌍你好",Assert.Single(rig.Events,e=>e.Kind==ModelEventKind.TextDelta).Text);Assert.Single(rig.Events,e=>e.Kind==ModelEventKind.Started);
    }
    [Theory] [InlineData(Protocol.OpenAIChat)] [InlineData(Protocol.AnthropicMessages)]
    public async Task JsonWhitespaceAndUnknownFieldsCannotBuyAnEffectiveDeadline(Protocol protocol)
    {await using var rig=new Rig(fixture,protocol,"application/json");await rig.Started.Task.WaitAsync(TimeSpan.FromSeconds(6));await rig.Script.Write("{\"PUBLIC_UNKNOWN\":\"x");rig.Clock.Advance(TimeSpan.FromSeconds(1));await rig.Script.Write("yyyy");await Task.Delay(20);await rig.AssertTimedOut(2,"FIRST_RESPONSE_TIMEOUT");}
    [Theory] [InlineData(Protocol.OpenAIChat)] [InlineData(Protocol.AnthropicMessages)]
    public async Task IncompleteUtf8OrUnicodeEscapeCannotResetJsonTextClock(Protocol protocol)
    {await using var rig=new Rig(fixture,protocol,"application/json");await rig.Started.Task.WaitAsync(TimeSpan.FromSeconds(6));await rig.Script.Write(JsonPrefix(protocol)+"PUBLIC");await rig.Clock.WaitDeadline(2);rig.Clock.Advance(TimeSpan.FromSeconds(1));await rig.Script.Write(new byte[]{0xf0,0x9f});await Task.Delay(20);await rig.AssertTimedOut(1,"READ_IDLE_TIMEOUT");}
    [Theory] [InlineData(Protocol.OpenAIChat,"text/event-stream")] [InlineData(Protocol.AnthropicMessages,"text/event-stream")] [InlineData(Protocol.OpenAIChat,"application/json")] [InlineData(Protocol.AnthropicMessages,"application/json")]
    public async Task EffectiveTrafficCannotExtendTheFrozenTotalBudget(Protocol protocol,string mime)
    {
        await using var rig=new Rig(fixture,protocol,mime,total:3,sharedClock:true);await rig.Started.Task.WaitAsync(TimeSpan.FromSeconds(6));await rig.Script.Write(mime=="application/json"?JsonPrefix(protocol)+"PUBLIC":StartEvents(protocol)+TextEvent(protocol,"PUBLIC"));await rig.Clock.WaitDeadline(2);rig.Clock.Advance(TimeSpan.FromMilliseconds(1500));await rig.Script.Write(mime=="application/json"?"MORE":TextEvent(protocol,"MORE"));await rig.Clock.WaitDeadline(2);await rig.AssertTimedOut(2,"REQUEST_TIMEOUT");Assert.Equal(1,rig.Script.Requests);
    }
    [Theory] [InlineData("off")] [InlineData("stop")] [InlineData("replace")]
    public async Task CancellationAtBodyReadDropsEveryLateEvent(string action)
    {await using var rig=new Rig(fixture,Protocol.OpenAIChat,"application/json");await rig.Started.Task.WaitAsync(TimeSpan.FromSeconds(6));await rig.Script.Write(JsonPrefix(Protocol.OpenAIChat)+"PUBLIC");await rig.Clock.WaitDeadline(2);if(action=="off")rig.Gate.Disable();else if(action=="stop")rig.Gate.StopGeneration();else rig.Gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out _,out _);await rig.Job.WaitAsync(TimeSpan.FromSeconds(6));Assert.DoesNotContain(rig.Events,e=>e.Kind==ModelEventKind.TextDelta||e.Kind==ModelEventKind.Completed||e.Kind==ModelEventKind.Failed);Assert.Equal(1,rig.Script.Requests);}
}
