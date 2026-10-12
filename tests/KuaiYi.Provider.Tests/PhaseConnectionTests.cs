using System.Net;
using System.Net.Sockets;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests;
public sealed class PhaseConnectionTests(PhaseHttpFixture fixture):IClassFixture<PhaseHttpFixture>
{
    [Theory] [InlineData(1)] [InlineData(3)] [InlineData(7)]
    public async Task RealConnectorPathObservesFrozenConnectDeadlineBeforeDial(int seconds)
    {
        var clock=new PhaseClock();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);CancellationToken observed=default;
        async ValueTask<Stream> Dial(SocketsHttpConnectionContext context,CancellationToken cancellation){observed=cancellation;entered.TrySetResult();await Task.Delay(Timeout.InfiniteTimeSpan,cancellation);var socket=new Socket(SocketType.Stream,ProtocolType.Tcp);try{await socket.ConnectAsync(context.DnsEndPoint,cancellation);return new NetworkStream(socket,true);}catch{socket.Dispose();throw;}}
        using var provider=new ProviderClient(null,null,clock,Dial);var profile=new KuaiYi.Providers.ApiProfile(Protocol.OpenAIChat,fixture.BaseUri,"unused","PUBLIC",Authentication.None,AllowLoopbackHttp:true,Timeouts:new(){ConnectSeconds=seconds,HeadersSeconds=30});var gate=new RuntimeGate();var origin=new EndpointOrigin(profile.ResolveEndpoint());gate.ConsentTo(origin);gate.TryEnable(origin,true);gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var ct);var events=new List<ModelEvent>();
        async Task Run(){await foreach(var item in provider.GenerateAsync(new(profile,"PUBLIC","PUBLIC"),gate,id,null,ct))events.Add(item);}var job=Run();await entered.Task.WaitAsync(TimeSpan.FromSeconds(6));clock.Advance(TimeSpan.FromSeconds(seconds)-TimeSpan.FromMilliseconds(1));Assert.False(job.IsCompleted);clock.Advance(TimeSpan.FromMilliseconds(1));await job.WaitAsync(TimeSpan.FromSeconds(6));Assert.True(observed.IsCancellationRequested);Assert.Contains(events,e=>e.Reason=="CONNECT_TIMEOUT");Assert.DoesNotContain(events,e=>e.Kind==ModelEventKind.Completed);
    }
    [Fact] public async Task RealTlsHandshakeStallUsesConfiguredSystemConnectionLimit()
    {
        using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var accepted=listener.AcceptTcpClientAsync();var port=((IPEndPoint)listener.LocalEndpoint).Port;
        using var provider=new ProviderClient();var profile=new KuaiYi.Providers.ApiProfile(Protocol.OpenAIChat,new Uri($"https://127.0.0.1:{port}/"),"model","PUBLIC",Authentication.None,Timeouts:new(){ConnectSeconds=1,HeadersSeconds=5});var gate=new RuntimeGate();var origin=new EndpointOrigin(profile.ResolveEndpoint());gate.ConsentTo(origin);gate.TryEnable(origin,true);gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var ct);var events=new List<ModelEvent>();
        using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));await foreach(var item in provider.GenerateAsync(new(profile,"PUBLIC","PUBLIC"),gate,id,null,ct).WithCancellation(limit.Token))events.Add(item);using var socket=await accepted.WaitAsync(TimeSpan.FromSeconds(6));Assert.Contains(events,e=>e.Reason=="CONNECT_TIMEOUT");Assert.DoesNotContain(events,e=>e.Kind==ModelEventKind.Completed);
    }
}
