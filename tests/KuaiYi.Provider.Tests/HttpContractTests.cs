using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using KuaiYi.Core;
using KuaiYi.MockProvider;
using KuaiYi.Providers;
using Xunit;

namespace KuaiYi.Provider.Tests;

public sealed class ServerFixture : IAsyncLifetime
{
    private MockProviderHost? server;
    public Uri BaseUri { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        string? external = Environment.GetEnvironmentVariable("KUAIYI_TEST_PROVIDER_URL");
        if (external is not null)
        {
            BaseUri = new Uri(external);
            if (!BaseUri.IsLoopback || BaseUri.Scheme != "http") throw new InvalidOperationException("Mock must be local HTTP.");
        }
        else { server = await MockProviderHost.StartAsync(); BaseUri = server.BaseUri; }
    }
    public async Task DisposeAsync() { if (server is not null) await server.DisposeAsync(); }
}

public sealed class HttpContractTests(ServerFixture fixture) : IClassFixture<ServerFixture>
{
    private (TranslationRequest Request, RuntimeGate Gate, RequestIdentity Identity, CancellationToken Ct, string Run) Prepare(ApiProtocol protocol, string scenario)
    {
        string run = Guid.NewGuid().ToString("N");
        var profile = new ApiProfile(protocol, new Uri(fixture.BaseUri, $"runs/{run}/{scenario}/proxy/vendor/v1"),
            protocol == ApiProtocol.OpenAIChat ? "chat/completions" : "messages", "fixture-model",
            protocol == ApiProtocol.OpenAIChat ? Authentication.Bearer : Authentication.ApiKey, AllowLoopbackHttp: true);
        var gate = new RuntimeGate();
        var origin = new EndpointOrigin(profile.ResolveEndpoint());
        gate.ConsentTo(origin); Assert.True(gate.TryEnable(origin, true));
        Assert.True(gate.TryStartRequest(Guid.NewGuid(), Guid.NewGuid(), null, out var identity, out var ct));
        return (new(profile, PromptBuilder.Build("faithful"), "Fixed public fixture source."), gate, identity, ct, run);
    }
    private async Task<CapturedRequest[]> Requests(string run)
    {
        using var client = new HttpClient();
        return await client.GetFromJsonAsync<CapturedRequest[]>(new Uri(fixture.BaseUri, "requests/" + run)) ?? [];
    }
    private static async Task<List<ModelEvent>> Collect(TranslationRequest request, RuntimeGate gate, RequestIdentity identity, CancellationToken ct)
    {
        using var client = new ProviderClient();
        using var secret = new SecretLease(Encoding.UTF8.GetBytes(MockProviderHost.FixtureCredential));
        List<ModelEvent> result = [];
        await foreach (var item in client.GenerateAsync(request, gate, identity, secret, ct)) result.Add(item);
        return result;
    }
    [Theory]
    [InlineData(ApiProtocol.OpenAIChat)]
    [InlineData(ApiProtocol.AnthropicMessages)]
    public async Task FragmentedHttpStreamUsesIndependentProtocolAndActualUsage(ApiProtocol protocol)
    {
        var p = Prepare(protocol, "fragmented");
        var events = await Collect(p.Request, p.Gate, p.Identity, p.Ct);
        Assert.Equal(MockProviderHost.FixtureOutput, string.Concat(events.Where(e => e.Kind == ModelEventKind.TextDelta).Select(e => e.Text)));
        Assert.Single(events, e => e.Kind == ModelEventKind.Completed);
        Assert.DoesNotContain(events, e => e.Kind == ModelEventKind.Failed);
        Assert.Contains(events, e => e.Usage?.OutputTokens == 8);
        var captured = Assert.Single(await Requests(p.Run));
        Assert.False(captured.CredentialInBody);
        Assert.DoesNotContain("/v1/v1", captured.Path);
        Assert.Contains("/proxy/vendor/v1/", captured.Path);
        using var document = JsonDocument.Parse(captured.Body);
        var json = document.RootElement;
        Assert.False(json.TryGetProperty("temperature", out _));
        if (protocol == ApiProtocol.OpenAIChat)
        {
            Assert.Equal("Bearer", captured.AuthScheme);
            Assert.False(captured.HasApiKey);
            Assert.Equal("system", json.GetProperty("messages")[0].GetProperty("role").GetString());
            Assert.True(json.TryGetProperty("max_completion_tokens", out _));
            Assert.False(json.TryGetProperty("max_tokens", out _));
        }
        else
        {
            Assert.Null(captured.AuthScheme);
            Assert.True(captured.HasApiKey);
            Assert.Equal("2023-06-01", captured.AnthropicVersion);
            Assert.True(json.TryGetProperty("system", out _));
            Assert.Equal("user", json.GetProperty("messages")[0].GetProperty("role").GetString());
        }
    }
    [Theory]
    [InlineData(ApiProtocol.OpenAIChat)]
    [InlineData(ApiProtocol.AnthropicMessages)]
    public async Task NonStreamingIsOneActualResultWithoutFakeTyping(ApiProtocol protocol)
    {
        var p = Prepare(protocol, "non-streaming");
        var events = await Collect(p.Request with { Profile = p.Request.Profile with { Stream = false } }, p.Gate, p.Identity, p.Ct);
        Assert.Single(events, e => e.Kind == ModelEventKind.TextDelta);
        Assert.Equal("non-streaming", events.Last().Reason);
        Assert.Equal(ModelEventKind.Completed, events.Last().Kind);
    }
    [Theory]
    [InlineData(ApiProtocol.OpenAIChat)]
    [InlineData(ApiProtocol.AnthropicMessages)]
    public async Task DisconnectRetainsPartialOutputWithoutSuccessOrRetry(ApiProtocol protocol)
    {
        var p = Prepare(protocol, "disconnect");
        var events = await Collect(p.Request, p.Gate, p.Identity, p.Ct);
        Assert.Contains(events, e => e.Kind == ModelEventKind.TextDelta);
        Assert.Equal("STREAM_INTERRUPTED", events.Last().Reason);
        Assert.DoesNotContain(events, e => e.Kind == ModelEventKind.Completed);
        Assert.Single(await Requests(p.Run));
    }
    [Theory]
    [InlineData("unauthorized", "AUTH_FAILED", 1)]
    [InlineData("forbidden", "AUTH_FAILED", 1)]
    [InlineData("rate-limit", "RATE_LIMITED", 2)]
    [InlineData("unavailable", "SERVICE_UNAVAILABLE", 2)]
    [InlineData("retry-once", "done", 2)]
    [InlineData("redirect", "REDIRECT_REJECTED", 1)]
    [InlineData("malformed", "INVALID_PROTOCOL", 1)]
    [InlineData("oversize", "INVALID_PROTOCOL", 1)]
    public async Task ErrorsAreBoundedRedactedAndRetryOnlyWhenAllowed(string scenario, string reason, int calls)
    {
        var p = Prepare(ApiProtocol.OpenAIChat, scenario);
        var events = await Collect(p.Request, p.Gate, p.Identity, p.Ct);
        Assert.Equal(reason, events.Last().Reason);
        Assert.DoesNotContain(events, e => (e.Reason ?? "").Contains(MockProviderHost.FixtureCredential) || (e.Text ?? "").Contains(MockProviderHost.FixtureCredential));
        var captured = await Requests(p.Run);
        Assert.Equal(calls, captured.Length);
        Assert.DoesNotContain(captured, r => r.Path.Contains("redirect-target"));
    }
    [Fact]
    public async Task AnthropicHttp200ErrorIsFailureAndNeverEchoesErrorBody()
    {
        var p = Prepare(ApiProtocol.AnthropicMessages, "stream-error");
        var events = await Collect(p.Request, p.Gate, p.Identity, p.Ct);
        Assert.Equal("PROVIDER_ERROR", events.Last().Reason);
        Assert.DoesNotContain(events, e => e.Kind == ModelEventKind.Completed);
        Assert.Single(await Requests(p.Run));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OffAndUnconsentedChangedOriginMakeZeroActualHttpCalls(bool changeOrigin)
    {
        var p = Prepare(ApiProtocol.OpenAIChat, "normal");
        TranslationRequest request = p.Request;
        if (changeOrigin) request = request with { Profile = request.Profile with { BaseUrl = new Uri(p.Request.Profile.BaseUrl.AbsoluteUri.Replace("127.0.0.1", "localhost")) } };
        else p.Gate.Disable();
        var events = await Collect(request, p.Gate, p.Identity, p.Ct);
        if (changeOrigin) Assert.Equal("CANCELLED_OR_UNCONSENTED", events.Last().Reason);
        else Assert.Empty(events); // Off invalidates the identity, including its errors.
        Assert.Empty(await Requests(p.Run));
    }
    [Fact]
    public async Task CancelAfterHeadersAcceptsNoLateTextAndDoesNotRetry()
    {
        var p = Prepare(ApiProtocol.OpenAIChat, "slow");
        using var provider = new ProviderClient();
        using var secret = new SecretLease(Encoding.UTF8.GetBytes(MockProviderHost.FixtureCredential));
        List<ModelEvent> seen = [];
        await foreach (var e in provider.GenerateAsync(p.Request, p.Gate, p.Identity, secret, p.Ct))
        {
            seen.Add(e);
            if (e.Kind == ModelEventKind.Started) p.Gate.Disable();
        }
        Assert.DoesNotContain(seen, e => e.Kind is ModelEventKind.TextDelta or ModelEventKind.Completed);
        Assert.Equal(ModelEventKind.Started, Assert.Single(seen).Kind);
        Assert.Single(await Requests(p.Run));
    }
    [Theory]
    [InlineData(ApiProtocol.OpenAIChat)]
    [InlineData(ApiProtocol.AnthropicMessages)]
    public async Task ImageBodiesUseTheirOwnSchema(ApiProtocol protocol)
    {
        var p = Prepare(protocol, "normal");
        byte[] bytes = [137, 80, 78, 71]; // Encoding contract fixture, not a claimed decodable image.
        await Collect(p.Request with { Image = bytes }, p.Gate, p.Identity, p.Ct);
        var captured = Assert.Single(await Requests(p.Run));
        using var doc = JsonDocument.Parse(captured.Body);
        var blocks = doc.RootElement.GetProperty("messages")[protocol == ApiProtocol.OpenAIChat ? 1 : 0].GetProperty("content");
        var image = blocks.EnumerateArray().Single(b => b.GetProperty("type").GetString() == (protocol == ApiProtocol.OpenAIChat ? "image_url" : "image"));
        if (protocol == ApiProtocol.OpenAIChat) Assert.Equal("data:image/png;base64," + Convert.ToBase64String(bytes), image.GetProperty("image_url").GetProperty("url").GetString());
        else { Assert.Equal("base64", image.GetProperty("source").GetProperty("type").GetString()); Assert.Equal(Convert.ToBase64String(bytes), image.GetProperty("source").GetProperty("data").GetString()); }
    }
}
