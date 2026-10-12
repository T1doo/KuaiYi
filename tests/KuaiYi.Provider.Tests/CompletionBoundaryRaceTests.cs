using System.Net.Sockets;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
using Protocol = KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests;

// Real HTTP bytes complete before the clock/cancellation barrier is released.
// Deferred timer callbacks model a queued timer while monotonic time already expired.
public sealed class CompletionBoundaryRaceTests(PhaseHttpFixture fixture):IClassFixture<PhaseHttpFixture>
{
    private sealed class Rig:IAsyncDisposable
    {
        public readonly PhaseClock Clock=new();public readonly RuntimeGate Gate=new();
        public readonly CancellationTokenSource Cancel=new();public readonly PhaseHttpFixture.Script Script;
        public readonly ProviderClient Provider;public readonly IAsyncEnumerator<ModelEvent> Enumerator;
        private Action? afterRead;
        public Rig(PhaseHttpFixture fixture,Protocol protocol,string mime,int total=5,int headers=30,int first=60,int idle=60,Action? connectionCompleted=null)
        {
            string run=Guid.NewGuid().ToString("N");Script=fixture.Add(run,mime);
            var profile=new KuaiYi.Providers.ApiProfile(protocol,new(fixture.BaseUri,run+"/"),protocol==Protocol.OpenAIChat?"chat/completions":"messages","PUBLIC",Authentication.None,Stream:mime!="application/json",AllowLoopbackHttp:true,TotalTimeoutSeconds:total,Timeouts:new RequestTimeouts{ConnectSeconds=2,HeadersSeconds=headers,FirstResponseSeconds=first,IdleSeconds=idle});
            Provider=new(null,Clock,Clock,async(context,ct)=>
            {
                var socket=new Socket(SocketType.Stream,ProtocolType.Tcp){NoDelay=true};
                try{await socket.ConnectAsync(context.DnsEndPoint,ct);connectionCompleted?.Invoke();return new ReadCompletionBarrier(new NetworkStream(socket,true),()=>Interlocked.Exchange(ref afterRead,null)?.Invoke());}catch{socket.Dispose();throw;}
            });
            var origin=new EndpointOrigin(profile.ResolveEndpoint());Gate.ConsentTo(origin);Gate.TryEnable(origin,true);Gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var id,out _);
            Enumerator=Provider.GenerateAsync(new(profile,"PUBLIC_SYSTEM","PUBLIC_SOURCE"),Gate,id,null,Cancel.Token).GetAsyncEnumerator();
        }
        public void AfterActualRead(Action action)=>afterRead=action;
        public void Expire(int seconds,bool dispatch=false)=>Clock.Advance(TimeSpan.FromSeconds(seconds),dispatch);
        public async Task<ModelEvent> Next(){Assert.True(await Enumerator.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(6)));return Enumerator.Current;}
        public async Task Started()=>Assert.Equal(ModelEventKind.Started,(await Next()).Kind);
        public async Task Failed(string reason){var value=await Next();Assert.Equal(ModelEventKind.Failed,value.Kind);Assert.Equal(reason,value.Reason);Assert.False(await Enumerator.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(6)));Assert.Equal(1,Script.Requests);}
        public async ValueTask DisposeAsync(){Cancel.Cancel();Gate.Disable();await Enumerator.DisposeAsync();Provider.Dispose();Cancel.Dispose();}
    }
    private sealed class ReadCompletionBarrier(Stream inner,Action afterRead):Stream
    {
        public override bool CanRead=>inner.CanRead;public override bool CanSeek=>false;public override bool CanWrite=>inner.CanWrite;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken ct=default){int count=await inner.ReadAsync(buffer,ct);if(count>0)afterRead();return count;}
        public override Task<int> ReadAsync(byte[] buffer,int offset,int count,CancellationToken ct)=>ReadAsync(buffer.AsMemory(offset,count),ct).AsTask();
        public override int Read(byte[] buffer,int offset,int count){int result=inner.Read(buffer,offset,count);if(result>0)afterRead();return result;}
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken ct=default)=>inner.WriteAsync(buffer,ct);
        public override Task WriteAsync(byte[] buffer,int offset,int count,CancellationToken ct)=>inner.WriteAsync(buffer,offset,count,ct);
        public override void Write(byte[] buffer,int offset,int count)=>inner.Write(buffer,offset,count);
        public override void Flush()=>inner.Flush();public override Task FlushAsync(CancellationToken ct)=>inner.FlushAsync(ct);
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
        protected override void Dispose(bool disposing){if(disposing)inner.Dispose();base.Dispose(disposing);}public override ValueTask DisposeAsync()=>inner.DisposeAsync();
    }
    private static string Json(Protocol protocol)=>protocol==Protocol.OpenAIChat?"{\"choices\":[{\"index\":0,\"message\":{\"content\":\"PUBLIC\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":2}}":"{\"type\":\"message\",\"content\":[{\"type\":\"text\",\"text\":\"PUBLIC\"}],\"usage\":{\"input_tokens\":1,\"output_tokens\":2},\"stop_reason\":\"end_turn\"}";
    private static string Sse(Protocol protocol)=>protocol==Protocol.OpenAIChat?"data: {\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":2},\"choices\":[{\"index\":0,\"delta\":{\"content\":\"PUBLIC\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n":"data: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":1,\"output_tokens\":2}}}\n\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"PUBLIC\"}}\n\ndata: {\"type\":\"content_block_stop\",\"index\":0}\n\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"}}\n\ndata: {\"type\":\"message_stop\"}\n\n";

    [Theory][InlineData("headers")][InlineData("total")][InlineData("cancel")]
    public async Task CompletedHeadersCannotCrossAnExpiredOrCancelledTransition(string kind)
    {
        await using var rig=new Rig(fixture,Protocol.OpenAIChat,"application/json",headers:2);
        rig.AfterActualRead(()=>{if(kind=="cancel")rig.Cancel.Cancel();else rig.Expire(kind=="headers"?2:5);});
        await rig.Failed(kind=="cancel"?"CANCELLED":kind=="headers"?"RESPONSE_HEADERS_TIMEOUT":"REQUEST_TIMEOUT");
    }
    [Theory][InlineData(Protocol.OpenAIChat,"application/json")][InlineData(Protocol.AnthropicMessages,"application/json")][InlineData(Protocol.OpenAIChat,"text/event-stream")][InlineData(Protocol.AnthropicMessages,"text/event-stream")]
    public async Task AlreadyCompletedBodyReadStillChecksAbsoluteTotal(Protocol protocol,string mime)
    {
        await using var rig=new Rig(fixture,protocol,mime);await rig.Started();rig.AfterActualRead(()=>rig.Expire(5));
        var next=rig.Next();await rig.Script.Write(mime=="application/json"?Json(protocol):Sse(protocol),end:true);var value=await next;
        Assert.Equal(ModelEventKind.Failed,value.Kind);Assert.Equal("REQUEST_TIMEOUT",value.Reason);Assert.False(await rig.Enumerator.MoveNextAsync());
    }
    [Theory][InlineData(Protocol.OpenAIChat,"total")][InlineData(Protocol.AnthropicMessages,"total")][InlineData(Protocol.OpenAIChat,"deferred-total")][InlineData(Protocol.AnthropicMessages,"deferred-total")][InlineData(Protocol.OpenAIChat,"cancel")][InlineData(Protocol.AnthropicMessages,"cancel")][InlineData(Protocol.OpenAIChat,"idle")][InlineData(Protocol.AnthropicMessages,"idle")]
    public async Task JsonEventDeliveryAfterConsumerPauseCannotComplete(Protocol protocol,string kind)
    {
        await using var rig=new Rig(fixture,protocol,"application/json",total:kind=="idle"?120:5,idle:2);await rig.Started();
        var next=rig.Next();await rig.Script.Write(Json(protocol),end:true);Assert.Equal(ModelEventKind.TextDelta,(await next).Kind);
        if(kind=="cancel")rig.Cancel.Cancel();else rig.Expire(kind=="idle"?2:5,kind=="total");
        await rig.Failed(kind=="cancel"?"CANCELLED":kind=="idle"?"READ_IDLE_TIMEOUT":"REQUEST_TIMEOUT");
    }
    [Theory][InlineData(Protocol.OpenAIChat,"total")][InlineData(Protocol.AnthropicMessages,"total")][InlineData(Protocol.OpenAIChat,"deferred-total")][InlineData(Protocol.AnthropicMessages,"deferred-total")][InlineData(Protocol.OpenAIChat,"cancel")][InlineData(Protocol.AnthropicMessages,"cancel")][InlineData(Protocol.OpenAIChat,"idle")][InlineData(Protocol.AnthropicMessages,"idle")]
    public async Task SseBufferedEventsAfterConsumerPauseCannotComplete(Protocol protocol,string kind)
    {
        await using var rig=new Rig(fixture,protocol,"text/event-stream",total:kind=="idle"?120:5,idle:2);await rig.Started();
        var next=rig.Next();await rig.Script.Write(Sse(protocol),end:true);Assert.Equal(ModelEventKind.Usage,(await next).Kind);
        if(kind=="cancel")rig.Cancel.Cancel();else rig.Expire(kind=="idle"?2:5,kind=="total");
        await rig.Failed(kind=="cancel"?"CANCELLED":kind=="idle"?"READ_IDLE_TIMEOUT":"REQUEST_TIMEOUT");
    }
    [Theory][InlineData(Protocol.OpenAIChat,"total")][InlineData(Protocol.AnthropicMessages,"total")][InlineData(Protocol.OpenAIChat,"cancel")][InlineData(Protocol.AnthropicMessages,"cancel")][InlineData(Protocol.OpenAIChat,"first")][InlineData(Protocol.AnthropicMessages,"first")]
    public async Task PauseAfterStartedDoesNotStartANewBodyBudget(Protocol protocol,string kind)
    {
        await using var rig=new Rig(fixture,protocol,"application/json",total:kind=="first"?120:5,first:2);await rig.Started();
        if(kind=="cancel")rig.Cancel.Cancel();else rig.Expire(kind=="first"?2:5);
        var next=rig.Next();await rig.Script.Write(Json(protocol),end:true);var value=await next;
        Assert.Equal(ModelEventKind.Failed,value.Kind);Assert.Equal(kind=="cancel"?"CANCELLED":kind=="first"?"FIRST_RESPONSE_TIMEOUT":"REQUEST_TIMEOUT",value.Reason);
    }
    [Theory]
    [InlineData(Protocol.OpenAIChat,"application/json","total")][InlineData(Protocol.AnthropicMessages,"application/json","total")]
    [InlineData(Protocol.OpenAIChat,"text/event-stream","total")][InlineData(Protocol.AnthropicMessages,"text/event-stream","total")]
    [InlineData(Protocol.OpenAIChat,"application/json","deferred-total")][InlineData(Protocol.AnthropicMessages,"application/json","deferred-total")]
    [InlineData(Protocol.OpenAIChat,"text/event-stream","deferred-total")][InlineData(Protocol.AnthropicMessages,"text/event-stream","deferred-total")]
    [InlineData(Protocol.OpenAIChat,"application/json","cancel")][InlineData(Protocol.AnthropicMessages,"application/json","cancel")]
    [InlineData(Protocol.OpenAIChat,"text/event-stream","cancel")][InlineData(Protocol.AnthropicMessages,"text/event-stream","cancel")]
    public async Task CancellationOrExpiryBeforeTerminalEventCannotBecomeSuccess(Protocol protocol,string mime,string kind)
    {
        await using var rig=new Rig(fixture,protocol,mime);await rig.Started();
        var next=rig.Next();await rig.Script.Write(mime=="application/json"?Json(protocol):Sse(protocol),end:true);
        var beforeTerminal=mime=="application/json"?ModelEventKind.Usage:ModelEventKind.TextDelta;
        while((await next).Kind!=beforeTerminal)next=rig.Next();
        if(kind=="cancel")rig.Cancel.Cancel();else rig.Expire(5,kind=="total");
        await rig.Failed(kind=="cancel"?"CANCELLED":"REQUEST_TIMEOUT");
    }
    [Theory][InlineData("connect")][InlineData("total")][InlineData("cancel")]
    public async Task CompletedSocketCannotStartHttpAfterItsDeadline(string kind)
    {
        Rig rig=null!;
        await using(rig=new Rig(fixture,Protocol.OpenAIChat,"application/json",connectionCompleted:()=>{if(kind=="cancel")rig.Cancel.Cancel();else rig.Expire(kind=="connect"?2:5);}))
        {
            var value=await rig.Next();Assert.Equal(ModelEventKind.Failed,value.Kind);Assert.Equal(kind=="cancel"?"CANCELLED":kind=="connect"?"CONNECT_TIMEOUT":"REQUEST_TIMEOUT",value.Reason);
            Assert.False(await rig.Enumerator.MoveNextAsync());Assert.Equal(0,rig.Script.Requests);
        }
    }
}
