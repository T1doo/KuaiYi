using System.Net.Http.Json;
using System.Text;
using KuaiYi.Core;
using KuaiYi.MockProvider;
using KuaiYi.Providers;
using Xunit;

namespace KuaiYi.Provider.Tests;

public sealed class LateEventIsolationTests(ServerFixture fixture) : IClassFixture<ServerFixture>
{
    [Theory]
    [InlineData(ApiProtocol.OpenAIChat, false)] [InlineData(ApiProtocol.OpenAIChat, true)]
    [InlineData(ApiProtocol.AnthropicMessages, false)] [InlineData(ApiProtocol.AnthropicMessages, true)]
    public async Task ReplacementOrOffWhileWaitingForHeadersDiscardsOldCancellation(ApiProtocol protocol, bool off)
    {
        var p = Prepare(protocol, "held-headers");
        using var provider = new ProviderClient(); using var secret = Secret();
        Task<List<ModelEvent>> old = Collect(provider, p.Request, p.Gate, p.Identity, secret, p.Ct);
        await WaitForBarrier(p.Run);
        if (off) p.Gate.Disable(); else await StartAndCompleteReplacement(provider, secret, p.Request, p.Gate);
        Assert.Empty(await old.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Single(await Requests(p.Run));
    }
    [Theory]
    [InlineData(ApiProtocol.OpenAIChat)] [InlineData(ApiProtocol.AnthropicMessages)]
    public async Task ReplacementDuringNonStreamingBodyDiscardsOldReadFailure(ApiProtocol protocol)
    {
        var p = Prepare(protocol, "held-json-abort");
        using var provider = new ProviderClient(); using var secret = Secret();
        List<ModelEvent> afterReplacement = [];
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool replaced = false;
        async Task Drain()
        {
            // Deliberately do not pass the gate token: the old response really arrives after replacement.
            await foreach (var e in provider.GenerateAsync(p.Request, p.Gate, p.Identity, secret))
            { if (Volatile.Read(ref replaced)) afterReplacement.Add(e); if (e.Kind == ModelEventKind.Started) started.TrySetResult(); }
        }
        Task old = Drain(); await started.Task.WaitAsync(TimeSpan.FromSeconds(5)); await WaitForBarrier(p.Run);
        Volatile.Write(ref replaced, true); await StartAndCompleteReplacement(provider, secret, p.Request, p.Gate);
        await Release(p.Run); await old.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(afterReplacement); Assert.Single(await Requests(p.Run));
    }
    [Theory]
    [InlineData(ApiProtocol.OpenAIChat, "held-401")] [InlineData(ApiProtocol.OpenAIChat, "held-503")]
    [InlineData(ApiProtocol.AnthropicMessages, "held-401")] [InlineData(ApiProtocol.AnthropicMessages, "held-503")]
    public async Task LateHttpErrorsCannotAffectReplacementOrRetryOldIdentity(ApiProtocol protocol, string scenario)
    {
        var p = Prepare(protocol, scenario);
        using var provider = new ProviderClient(); using var secret = Secret();
        Task<List<ModelEvent>> old = Collect(provider, p.Request, p.Gate, p.Identity, secret, CancellationToken.None);
        await WaitForBarrier(p.Run); await StartAndCompleteReplacement(provider, secret, p.Request, p.Gate);
        await Release(p.Run); Assert.Empty(await old.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Single(await Requests(p.Run));
    }
    [Theory]
    [InlineData(ApiProtocol.OpenAIChat)] [InlineData(ApiProtocol.AnthropicMessages)]
    public async Task ReplacementAfterTruncatedDeltaAlsoDiscardsAdjacentLimitError(ApiProtocol protocol)
    {
        var p = Prepare(protocol, "output-limit"); using var provider = new ProviderClient(); using var secret = Secret();
        List<ModelEvent> old = [];
        await foreach (var item in provider.GenerateAsync(p.Request, p.Gate, p.Identity, secret, p.Ct))
        {
            old.Add(item);
            if (item.Kind == ModelEventKind.TextDelta)
            {
                Assert.Equal(ProviderClient.MaximumOutputCodeUnits, item.Text!.Length);
                await StartAndCompleteReplacement(provider, secret, p.Request, p.Gate);
            }
        }
        ModelEventKind[] expected = protocol == ApiProtocol.OpenAIChat ? [ModelEventKind.Started, ModelEventKind.TextDelta]
            : [ModelEventKind.Started, ModelEventKind.Usage, ModelEventKind.TextDelta];
        Assert.Equal(expected, old.Select(e => e.Kind));
        if (protocol == ApiProtocol.AnthropicMessages) Assert.Equal(12, old[1].Usage!.InputTokens);
        Assert.DoesNotContain(old, e => e.Kind is ModelEventKind.Completed or ModelEventKind.Failed); Assert.Single(await Requests(p.Run));
    }
    [Theory]
    [InlineData(ApiProtocol.OpenAIChat)] [InlineData(ApiProtocol.AnthropicMessages)]
    public async Task CurrentIdentityStillReceivesItsOwnOutputLimitFailure(ApiProtocol protocol)
    {
        var p = Prepare(protocol, "output-limit"); using var provider = new ProviderClient(); using var secret = Secret();
        var events = await Collect(provider, p.Request, p.Gate, p.Identity, secret, p.Ct);
        Assert.Equal(ProviderClient.MaximumOutputCodeUnits, Assert.Single(events, e => e.Kind == ModelEventKind.TextDelta).Text!.Length);
        Assert.Equal("LOCAL_OUTPUT_LIMIT", Assert.Single(events, e => e.Kind == ModelEventKind.Failed).Reason);
        Assert.DoesNotContain(events, e => e.Kind == ModelEventKind.Completed); Assert.Single(await Requests(p.Run));
    }
    [Theory]
    [InlineData(ApiProtocol.OpenAIChat)] [InlineData(ApiProtocol.AnthropicMessages)]
    public async Task RealHttpEnvelopesSurviveQueueingOnlyForCurrentCompleteIdentity(ApiProtocol protocol)
    {
        var p = Prepare(protocol, "normal"); using var provider = new ProviderClient(); using var secret = Secret();
        var destination = new DeferredContext(); var receiver = new ProviderEventReceiver(p.Gate, destination);
        List<ModelEvent> applied = []; List<Task<bool>> oldQueued = [];
        await foreach (var envelope in provider.GenerateEnvelopesAsync(p.Request, p.Gate, p.Identity, secret, p.Ct))
        {
            Assert.Equal(p.Identity, envelope.Identity); oldQueued.Add(receiver.ReceiveAsync(envelope, applied.Add));
        }
        Assert.NotEmpty(oldQueued); Assert.Empty(applied);
        Assert.True(p.Gate.TryStartRequest(Guid.NewGuid(), Guid.NewGuid(), null, out var next, out var ct));
        string run = Guid.NewGuid().ToString("N"); List<Task<bool>> nextQueued = [];
        await foreach (var envelope in provider.GenerateEnvelopesAsync(Request(protocol, run, "normal"), p.Gate, next, secret, ct))
        {
            Assert.Equal(next, envelope.Identity); nextQueued.Add(receiver.ReceiveAsync(envelope, applied.Add));
        }
        destination.Drain(); Assert.All(await Task.WhenAll(oldQueued), accepted => Assert.False(accepted));
        Assert.All(await Task.WhenAll(nextQueued), accepted => Assert.True(accepted));
        Assert.Single(applied, e => e.Kind == ModelEventKind.Completed); Assert.DoesNotContain(applied, e => e.Kind == ModelEventKind.Failed);
        Assert.Equal(MockProviderHost.FixtureOutput, string.Concat(applied.Where(e => e.Kind == ModelEventKind.TextDelta).Select(e => e.Text)));
        Assert.Single(await Requests(p.Run)); Assert.Single(await Requests(run));
    }
    private (TranslationRequest Request, RuntimeGate Gate, RequestIdentity Identity, CancellationToken Ct, string Run) Prepare(ApiProtocol protocol, string scenario)
    {
        string run = Guid.NewGuid().ToString("N"); var request = Request(protocol, run, scenario);
        var gate = new RuntimeGate(); var origin = new EndpointOrigin(request.Profile.ResolveEndpoint());
        gate.ConsentTo(origin); Assert.True(gate.TryEnable(origin, true));
        Assert.True(gate.TryStartRequest(Guid.NewGuid(), Guid.NewGuid(), null, out var id, out var ct));
        return (request, gate, id, ct, run);
    }
    private TranslationRequest Request(ApiProtocol protocol, string run, string scenario) => new(
        new(protocol, new Uri(fixture.BaseUri, $"runs/{run}/{scenario}/v1"), protocol == ApiProtocol.OpenAIChat ? "chat/completions" : "messages",
            "fixture-model", protocol == ApiProtocol.OpenAIChat ? Authentication.Bearer : Authentication.ApiKey, AllowLoopbackHttp: true),
        PromptBuilder.Build("faithful"), "Public late-event isolation fixture.");
    private async Task StartAndCompleteReplacement(ProviderClient provider, SecretLease secret, TranslationRequest previous, RuntimeGate gate)
    {
        string run = Guid.NewGuid().ToString("N"); var request = Request(previous.Profile.Protocol, run, "normal");
        Assert.True(gate.TryStartRequest(Guid.NewGuid(), Guid.NewGuid(), null, out var id, out var ct));
        var events = await Collect(provider, request, gate, id, secret, ct);
        Assert.Single(events, e => e.Kind == ModelEventKind.Completed); Assert.DoesNotContain(events, e => e.Kind == ModelEventKind.Failed);
        Assert.True(gate.CanApply(id)); Assert.Single(await Requests(run));
    }
    private async Task WaitForBarrier(string run)
    {
        using var client = new HttpClient(); using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            var status = await client.GetFromJsonAsync<BarrierStatus>(new Uri(fixture.BaseUri, "barriers/" + run), deadline.Token);
            if (status?.Ready == true) return;
            await Task.Delay(10, deadline.Token);
        }
    }
    private async Task Release(string run)
    {
        using var client = new HttpClient(); using var response = await client.PostAsync(new Uri(fixture.BaseUri, $"barriers/{run}/release"), null);
        response.EnsureSuccessStatusCode(); Assert.True(await response.Content.ReadFromJsonAsync<bool>());
    }
    private async Task<CapturedRequest[]> Requests(string run)
    { using var client = new HttpClient(); return await client.GetFromJsonAsync<CapturedRequest[]>(new Uri(fixture.BaseUri, "requests/" + run)) ?? []; }
    private static SecretLease Secret() => new(Encoding.UTF8.GetBytes(MockProviderHost.FixtureCredential));
    private static async Task<List<ModelEvent>> Collect(ProviderClient provider, TranslationRequest request, RuntimeGate gate, RequestIdentity identity, SecretLease secret, CancellationToken ct)
    {
        List<ModelEvent> events = []; await foreach (var e in provider.GenerateAsync(request, gate, identity, secret, ct)) events.Add(e); return events;
    }
    private sealed record BarrierStatus(bool Ready);
    private sealed class DeferredContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> queue = new();
        public override void Post(SendOrPostCallback callback, object? state) => queue.Enqueue((callback, state));
        public void Drain() { while (queue.TryDequeue(out var item)) item.Callback(item.State); }
    }
}
