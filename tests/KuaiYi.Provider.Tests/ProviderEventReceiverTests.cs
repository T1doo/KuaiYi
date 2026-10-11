using KuaiYi.Core;
using KuaiYi.Providers;
using Xunit;

namespace KuaiYi.Provider.Tests;

public sealed class ProviderEventReceiverTests
{
    [Theory]
    [InlineData(ModelEventKind.Started)] [InlineData(ModelEventKind.TextDelta)] [InlineData(ModelEventKind.Usage)]
    [InlineData(ModelEventKind.Completed)] [InlineData(ModelEventKind.Failed)]
    public async Task EveryQueuedOldEventIsDiscardedAfterReplacement(ModelEventKind kind)
    {
        var (gate, identity) = Prepare(); var destination = new QueuedContext(); var receiver = new ProviderEventReceiver(gate, destination);
        int writes = 0; Task<bool> pending = receiver.ReceiveAsync(new(identity, new(kind, Text: "public fixture")), _ => writes++);
        Assert.True(gate.TryStartRequest(Guid.NewGuid(), Guid.NewGuid(), null, out var next, out _));
        destination.Drain(); Assert.False(await pending); Assert.Equal(0, writes); Assert.True(gate.CanApply(next));
    }
    [Theory]
    [InlineData("session")] [InlineData("version")] [InlineData("request")] [InlineData("epoch")]
    public async Task CompleteIdentityIsRequiredAtDestination(string field)
    {
        var (gate, identity) = Prepare(); var destination = new QueuedContext(); var receiver = new ProviderEventReceiver(gate, destination);
        var forged = field switch
        {
            "session" => identity with { SessionId = Guid.NewGuid() }, "version" => identity with { VersionId = Guid.NewGuid() },
            "request" => identity with { RequestId = Guid.NewGuid() }, _ => identity with { Epoch = identity.Epoch + 1 }
        };
        int writes = 0; Task<bool> pending = receiver.ReceiveAsync(new(forged, new(ModelEventKind.Failed)), _ => writes++);
        destination.Drain(); Assert.False(await pending); Assert.Equal(0, writes);
    }
    [Theory]
    [InlineData("off")] [InlineData("stop")] [InlineData("lock")] [InlineData("cancel")]
    public async Task InvalidationAfterQueueingPreventsApplication(string action)
    {
        var (gate, identity) = Prepare(); var destination = new QueuedContext(); var receiver = new ProviderEventReceiver(gate, destination);
        using var cancellation = new CancellationTokenSource(); int writes = 0;
        Task<bool> pending = receiver.ReceiveAsync(new(identity, new(ModelEventKind.Failed)), _ => writes++, cancellation.Token);
        if (action == "off") gate.Disable(); else if (action == "stop") gate.StopGeneration(); else if (action == "lock") gate.SetSessionLocked(true); else cancellation.Cancel();
        destination.Drain(); Assert.False(await pending); Assert.Equal(0, writes);
    }
    [Fact]
    public async Task CurrentEventAppliesOnceOnDestinationAndExceptionsPropagate()
    {
        var (gate, identity) = Prepare(); var destination = new QueuedContext(); var receiver = new ProviderEventReceiver(gate, destination);
        int writes = 0; Task<bool> success = receiver.ReceiveAsync(new(identity, new(ModelEventKind.Completed)), _ => { Assert.True(destination.Draining); writes++; });
        destination.Drain(); Assert.True(await success); Assert.Equal(1, writes);
        Task<bool> error = receiver.ReceiveAsync(new(identity, new(ModelEventKind.TextDelta)), _ => throw new InvalidOperationException("test callback failure"));
        destination.Drain(); await Assert.ThrowsAsync<InvalidOperationException>(async () => await error);
    }
    private static (RuntimeGate, RequestIdentity) Prepare()
    {
        var gate = new RuntimeGate(); var origin = new EndpointOrigin(new Uri("https://fixture.invalid"));
        gate.ConsentTo(origin); Assert.True(gate.TryEnable(origin, true));
        Assert.True(gate.TryStartRequest(Guid.NewGuid(), Guid.NewGuid(), null, out var identity, out _)); return (gate, identity);
    }
    private sealed class QueuedContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> queue = new();
        public bool Draining { get; private set; }
        public override void Post(SendOrPostCallback callback, object? state) => queue.Enqueue((callback, state));
        public void Drain() { Draining = true; try { while (queue.TryDequeue(out var work)) work.Callback(work.State); } finally { Draining = false; } }
    }
}
