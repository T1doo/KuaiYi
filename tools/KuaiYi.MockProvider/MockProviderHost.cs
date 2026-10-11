using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace KuaiYi.MockProvider;

public sealed record CapturedRequest(string Run, string Path, string Body, string? AuthScheme, bool HasApiKey,
    string? AnthropicVersion, bool CredentialInBody,string[]? FixtureCredentialHeaders=null,string? FixtureRegion=null);

/// <summary>Test-only real HTTP fault server. Never referenced by production projects; binds loopback exclusively.</summary>
public sealed class MockProviderHost : IAsyncDisposable
{
    public const string FixtureCredential = "TEST_ONLY_DO_NOT_USE_AS_A_REAL_KEY";
    public const string FixtureOutput = "你好，世界🌍\n第二段";
    private readonly WebApplication app;
    public Uri BaseUri { get; }
    private MockProviderHost(WebApplication app, Uri uri) { this.app = app; BaseUri = uri; }

    public static async Task<MockProviderHost> StartAsync(CancellationToken cancellation = default)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o => { o.Listen(IPAddress.Loopback, 0); o.Limits.MaxRequestBodySize = 8 * 1024 * 1024; });
        var app = builder.Build();
        var requests = new ConcurrentQueue<CapturedRequest>();
        var attempts = new ConcurrentDictionary<string, int>();
        var barriers = new ConcurrentDictionary<string, TaskCompletionSource>(StringComparer.Ordinal);
        app.MapGet("/requests/{run}", (string run) => requests.Where(r => r.Run == run).ToArray());
        app.MapGet("/barriers/{run}", (string run) => new { ready = barriers.ContainsKey(run) });
        app.MapPost("/barriers/{run}/release", (string run) => barriers.TryGetValue(run, out var signal) && signal.TrySetResult());
        app.MapPost("/runs/{run}/{scenario}/{**endpoint}", async (HttpContext context, string run, string scenario, string endpoint) =>
        {
            using var reader = new StreamReader(context.Request.Body, new UTF8Encoding(false, true));
            string body = await reader.ReadToEndAsync(context.RequestAborted);
            string auth = context.Request.Headers.Authorization.ToString();
            requests.Enqueue(new(run, context.Request.Path, body, auth.Length == 0 ? null : auth.Split(' ')[0],
                context.Request.Headers.ContainsKey("x-api-key"), context.Request.Headers["anthropic-version"].FirstOrDefault(), body.Contains(FixtureCredential, StringComparison.Ordinal),
                context.Request.Headers.Where(p=>p.Value.ToString()==FixtureCredential||p.Value.ToString()=="Token "+FixtureCredential||p.Key.Equals("Authorization",StringComparison.OrdinalIgnoreCase)&&p.Value.ToString()=="Bearer "+FixtureCredential).Select(p=>p.Key).ToArray(),context.Request.Headers["X-Fixture-Region"].FirstOrDefault()));
            while (requests.Count > 1024) requests.TryDequeue(out _);
            int attempt = attempts.AddOrUpdate(run, 1, (_, n) => n + 1);
            bool anthropic = endpoint.EndsWith("messages", StringComparison.Ordinal);
            if (scenario is "held-headers" or "held-401" or "held-503" or "held-json-abort")
            {
                if (scenario == "held-json-abort")
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.StartAsync(context.RequestAborted);
                    await context.Response.WriteAsync("{", context.RequestAborted);
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                }
                var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                if (barriers.Count >= 1024 || !barriers.TryAdd(run, signal)) { context.Abort(); return; }
                try { await signal.Task.WaitAsync(context.RequestAborted); }
                catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { return; }
                finally { barriers.TryRemove(run, out _); }
                if (scenario == "held-json-abort") { context.Abort(); return; }
                if (scenario is "held-401" or "held-503")
                {
                    context.Response.StatusCode = scenario == "held-401" ? 401 : 503;
                    context.Response.Headers.RetryAfter = "0";
                    await context.Response.WriteAsync("{\"error\":\"test-only-late-error\"}", context.RequestAborted);
                    return;
                }
            }
            if (scenario is "unauthorized" or "forbidden" or "rate-limit" or "unavailable")
            {
                context.Response.StatusCode = scenario switch { "unauthorized" => 401, "forbidden" => 403, "rate-limit" => 429, _ => 503 };
                context.Response.Headers.RetryAfter = "0";
                await context.Response.WriteAsync("{\"error\":\"" + FixtureCredential + "\"}");
                return;
            }
            if (scenario == "retry-once" && attempt == 1) { context.Response.StatusCode = 429; context.Response.Headers.RetryAfter = "0"; return; }
            if (scenario == "redirect")
            {
                context.Response.StatusCode = 307;
                context.Response.Headers.Location = $"http://localhost:{context.Connection.LocalPort}/runs/{run}/redirect-target/{endpoint}";
                return;
            }
            if (scenario == "non-streaming")
            {
                context.Response.ContentType = "application/json";
                object payload = anthropic ? new { type = "message", content = new[] { new { type = "text", text = FixtureOutput } }, stop_reason = "end_turn", usage = new { input_tokens = 12, output_tokens = 8 } }
                    : (object)new { choices = new[] { new { index = 0, message = new { content = FixtureOutput }, finish_reason = "stop" } }, usage = new { prompt_tokens = 12, completion_tokens = 8 } };
                await context.Response.WriteAsJsonAsync(payload);
                return;
            }
            context.Response.ContentType = "text/event-stream";
            await context.Response.StartAsync(context.RequestAborted);
            if(scenario=="paced")
            {
                if(anthropic)await context.Response.WriteAsync("data: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":12,\"output_tokens\":0}}}\n\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n",context.RequestAborted);
                for(int i=0;i<6;i++)
                {
                    string delta="公开片段"+i+"\n";
                    object item=anthropic?new{type="content_block_delta",index=0,delta=new{type="text_delta",text=delta}}:(object)new{choices=new[]{new{index=0,delta=new{content=delta},finish_reason=(string?)null}}};
                    await context.Response.WriteAsync("data: "+JsonSerializer.Serialize(item)+"\n\n",context.RequestAborted);await context.Response.Body.FlushAsync(context.RequestAborted);await Task.Delay(15,context.RequestAborted);
                }
                await context.Response.WriteAsync(anthropic?"data: {\"type\":\"content_block_stop\",\"index\":0}\n\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":8}}\n\ndata: {\"type\":\"message_stop\"}\n\n":"data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n",context.RequestAborted);
                return;
            }
            string text;
            string fixtureOutput = scenario == "output-limit" ? new string('x', ProviderOutputLimitFixture) : FixtureOutput;
            if (anthropic)
            {
                text = "event: message_start\r\ndata: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":12,\"output_tokens\":0}}}\r\n\r\n" +
                    "event: ping\ndata: {\"type\":\"ping\"}\n\n" +
                    "data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n" +
                    "data: " + JsonSerializer.Serialize(new { type = "content_block_delta", index = 0, delta = new { type = "text_delta", text = fixtureOutput } }) + "\n\n";
                if (scenario != "disconnect") text += scenario == "stream-error" ? "data: {\"type\":\"error\",\"error\":{\"message\":\"" + FixtureCredential + "\"}}\n\n" :
                    "data: {\"type\":\"content_block_stop\",\"index\":0}\n\n" +
                    "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":8}}\n\n" +
                    "data: {\"type\":\"message_stop\"}\n\n";
            }
            else
            {
                text = "\uFEFF: heartbeat\r\n\r\n" + "data: " + JsonSerializer.Serialize(new { choices = new[] { new { index = 0, delta = new { content = fixtureOutput }, finish_reason = (string?)null } } }) + "\r\n\r\n";
                if (scenario != "disconnect") text += "data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n" +
                    "data: {\"choices\":[],\"usage\":{\"prompt_tokens\":12,\"completion_tokens\":8}}\n\n" + "data: [DONE]\n\n";
            }
            if (scenario == "malformed") text = "data: {not-json}\n\n";
            if (scenario == "oversize") text = "data: " + new string('a', 1024 * 1024) + "\n\n";
            if (scenario == "slow")
            {
                await Task.Delay(1000, context.RequestAborted);
            }
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            int chunk = scenario == "fragmented" ? 1 : 4096;
            try
            {
                for (int offset = 0; offset < bytes.Length; offset += chunk)
                {
                    await context.Response.Body.WriteAsync(bytes.AsMemory(offset, Math.Min(chunk, bytes.Length - offset)), context.RequestAborted);
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                }
            }
            catch (IOException) when (context.RequestAborted.IsCancellationRequested) { }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        });
        await app.StartAsync(cancellation);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new MockProviderHost(app, new Uri(address + "/"));
    }
    public Task WaitForShutdownAsync(CancellationToken cancellation = default) => app.WaitForShutdownAsync(cancellation);
    private const int ProviderOutputLimitFixture = 120_001;
    public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }
}
