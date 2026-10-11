using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using KuaiYi.Core;

namespace KuaiYi.Providers;

/// <summary>Real HTTP transport with per-request credentials, redirect rejection, bounded parsing and late-event checks.</summary>
public sealed class ProviderClient : IDisposable
{
    public const int MaximumOutputCodeUnits = 120_000;
    private readonly HttpClient client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        MaxResponseHeadersLength = 64
    }) { Timeout = Timeout.InfiniteTimeSpan };

    public async IAsyncEnumerable<ModelEvent> GenerateAsync(TranslationRequest request, RuntimeGate gate,
        RequestIdentity identity, SecretLease? secret, [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(gate);
        if (!gate.CanApply(identity)) yield break;
        Uri endpoint;
        IProtocolAdapter adapter;
        HttpRequestMessage message;
        try
        {
            endpoint = request.Profile.ResolveEndpoint();
            adapter = request.Profile.Protocol == ApiProtocol.OpenAIChat ? new OpenAiChatAdapter() : new AnthropicMessagesAdapter();
            if (request.SystemPrompt.Length > 32_000 || request.SourceText.Length > 100_002 || request.SourceText.EnumerateRunes().Count() > 50_000)
                throw new ArgumentException("Request input exceeds limit.");
            message = CreateMessage(request, adapter, endpoint, secret);
        }
        catch (ArgumentException) { endpoint = null!; adapter = null!; message = null!; }
        if (message is null) { if (gate.CanApply(identity)) yield return new(ModelEventKind.Failed, Reason: "CONFIG_INVALID"); yield break; }
        using (message)
        using (var total = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
        {
            total.CancelAfter(TimeSpan.FromSeconds(120));
            HttpResponseMessage? response = null;
            string? sendError = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var headersDeadline = CancellationTokenSource.CreateLinkedTokenSource(total.Token);
                headersDeadline.CancelAfter(TimeSpan.FromSeconds(30));
                try
                {
                    // Each attempt obtains fresh content and headers, with no credentials on DefaultRequestHeaders.
                    using HttpRequestMessage outgoing = CreateMessage(request, adapter, endpoint, secret);
                    if (!gate.TryBeginSend(identity, new EndpointOrigin(endpoint),
                        () => client.SendAsync(outgoing, HttpCompletionOption.ResponseHeadersRead, headersDeadline.Token), out var pending))
                    { sendError = "CANCELLED_OR_UNCONSENTED"; break; }
                    response = await pending!.ConfigureAwait(false);
                    if (!gate.CanApply(identity)) { sendError = "STALE_IDENTITY"; break; }
                    if (response.StatusCode is not (HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable) || attempt == 1) break;
                    TimeSpan? retry = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
                    if (retry is { } delay && (delay < TimeSpan.Zero || delay > TimeSpan.FromSeconds(15))) break;
                    response.Dispose(); response = null;
                    await Task.Delay(retry ?? TimeSpan.FromSeconds(1), total.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { sendError = cancellation.IsCancellationRequested ? "CANCELLED" : "REQUEST_TIMEOUT"; break; }
                catch (HttpRequestException e) { sendError = e.HttpRequestError == HttpRequestError.SecureConnectionError ? "TLS_ERROR" : "NETWORK_ERROR"; break; }
            }
            if (sendError is not null) { response?.Dispose(); if (gate.CanApply(identity)) yield return new(ModelEventKind.Failed, Reason: sendError); yield break; }
            using (response!)
            {
                if (!response!.IsSuccessStatusCode)
                {
                    string reason = (int)response.StatusCode is >= 300 and < 400 ? "REDIRECT_REJECTED" : response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "AUTH_FAILED",
                        HttpStatusCode.NotFound => "ENDPOINT_NOT_FOUND",
                        HttpStatusCode.TooManyRequests => "RATE_LIMITED",
                        HttpStatusCode.ServiceUnavailable => "SERVICE_UNAVAILABLE",
                        _ => "PROVIDER_HTTP_ERROR"
                    };
                    // Never surface the untrusted error body; it can echo credentials/source or contain HTML.
                    if (gate.CanApply(identity)) yield return new(ModelEventKind.Failed, Reason: reason);
                    yield break;
                }
                if (!gate.CanApply(identity)) yield break;
                yield return new(ModelEventKind.Started);
                if (!gate.CanApply(identity)) yield break;
                Stream? openedBody = null;
                string? openError = null;
                try { openedBody = await response.Content.ReadAsStreamAsync(total.Token).ConfigureAwait(false); }
                catch (Exception e) when (IsProtocolOrReadFailure(e)) { openError = ErrorFor(e, cancellation); }
                if (openError is not null)
                {
                    if (gate.CanApply(identity)) yield return new(ModelEventKind.Failed, Reason: openError);
                    yield break;
                }
                await using Stream body = openedBody!;
                string? mime = response.Content.Headers.ContentType?.MediaType;
                if (mime == "application/json")
                {
                    IReadOnlyList<ModelEvent>? events = null;
                    string? failure = null;
                    try { events = adapter.ParseNonStreaming(await ReadBoundedJsonAsync(body, total.Token).ConfigureAwait(false)); }
                    catch (Exception e) when (IsProtocolOrReadFailure(e)) { failure = ErrorFor(e, cancellation); }
                    if (failure is not null) { if (gate.CanApply(identity)) yield return new(ModelEventKind.Failed, Reason: failure); yield break; }
                    int output = 0;
                    foreach (var item in events!)
                    {
                        if (!gate.CanApply(identity)) yield break;
                        if (item.Kind == ModelEventKind.TextDelta && (output += item.Text!.Length) > MaximumOutputCodeUnits)
                        { yield return new(ModelEventKind.Failed, Reason: "LOCAL_OUTPUT_LIMIT"); yield break; }
                        yield return item;
                    }
                    yield break;
                }
                if (mime != "text/event-stream") { if (gate.CanApply(identity)) yield return new(ModelEventKind.Failed, Reason: "UNSUPPORTED_CONTENT_TYPE"); yield break; }
                await using var enumerator = SseReader.ReadAsync(body, total.Token).GetAsyncEnumerator(total.Token);
                int outputLength = 0;
                bool firstEvent = true;
                while (true)
                {
                    IReadOnlyList<ModelEvent>? events = null;
                    bool more = false;
                    string? failure = null;
                    using var idle = CancellationTokenSource.CreateLinkedTokenSource(total.Token);
                    idle.CancelAfter(TimeSpan.FromSeconds(firstEvent ? 30 : 20));
                    try
                    {
                        // Cancelling the underlying body read is required; WaitAsync alone leaves a blocked enumerator.
                        using var readCancellation = idle.Token.Register(() => body.Dispose());
                        more = await enumerator.MoveNextAsync().ConfigureAwait(false);
                        if (more) events = adapter.ParseEvent(enumerator.Current.Data);
                    }
                    catch (Exception e) when (IsProtocolOrReadFailure(e)) { failure = idle.IsCancellationRequested && !cancellation.IsCancellationRequested ? "REQUEST_TIMEOUT" : ErrorFor(e, cancellation); }
                    if (!gate.CanApply(identity)) yield break;
                    if (failure is not null) { yield return new(ModelEventKind.Failed, Reason: failure); yield break; }
                    if (!more) { yield return new(ModelEventKind.Failed, Reason: "STREAM_INTERRUPTED"); yield break; }
                    firstEvent = false;
                    foreach (var item in events!)
                    {
                        if (!gate.CanApply(identity)) yield break;
                        if (item.Kind == ModelEventKind.TextDelta)
                        {
                            int remaining = MaximumOutputCodeUnits - outputLength;
                            if (item.Text!.Length > remaining)
                            {
                                if (remaining > 0 && char.IsHighSurrogate(item.Text[remaining - 1])) remaining--;
                                if (remaining > 0) yield return item with { Text = item.Text[..remaining] };
                                if (gate.CanApply(identity)) yield return new(ModelEventKind.Failed, Reason: "LOCAL_OUTPUT_LIMIT");
                                yield break;
                            }
                            outputLength += item.Text.Length;
                        }
                        yield return item;
                        if (item.Kind is ModelEventKind.Completed or ModelEventKind.Failed) yield break;
                    }
                }
            }
        }
    }

    /// <summary>Production consumption carries the complete identity through the destination queue.</summary>
    public async IAsyncEnumerable<ModelEventEnvelope> GenerateEnvelopesAsync(TranslationRequest request, RuntimeGate gate,
        RequestIdentity identity, SecretLease? secret, [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        await foreach (var item in GenerateAsync(request, gate, identity, secret, cancellation).ConfigureAwait(false))
        {
            if (!gate.CanApply(identity)) yield break;
            yield return new(identity, item);
        }
    }

    private static HttpRequestMessage CreateMessage(TranslationRequest request, IProtocolAdapter adapter, Uri endpoint, SecretLease? secret)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = JsonContent.Create(adapter.BuildBody(request)) };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(request.Profile.Stream ? "text/event-stream" : "application/json"));
        if (request.Profile.Authentication != Authentication.None)
        {
            string key = secret?.HeaderValue() ?? throw new ArgumentException("Missing secret.");
            if (request.Profile.Authentication == Authentication.Bearer) message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            else message.Headers.Add("x-api-key", key);
        }
        if (request.Profile.Protocol == ApiProtocol.AnthropicMessages) message.Headers.Add("anthropic-version", request.Profile.AnthropicVersion);
        return message;
    }

    private static async Task<string> ReadBoundedJsonAsync(Stream body, CancellationToken cancellation)
    {
        using var output = new MemoryStream();
        byte[] bytes = new byte[4096];
        while (true)
        {
            int n = await body.ReadAsync(bytes, cancellation).ConfigureAwait(false);
            if (n == 0) break;
            if (output.Length + n > SseReader.MaximumEventBytes) throw new InvalidDataException("JSON response exceeds local limit.");
            output.Write(bytes, 0, n);
        }
        return new UTF8Encoding(false, true).GetString(output.GetBuffer(), 0, checked((int)output.Length));
    }
    private static bool IsProtocolOrReadFailure(Exception e) => e is JsonException or InvalidDataException or DecoderFallbackException or
        IOException or HttpRequestException or OperationCanceledException or InvalidOperationException or ObjectDisposedException;
    private static string ErrorFor(Exception e, CancellationToken cancellation) => cancellation.IsCancellationRequested ? "CANCELLED" :
        e is OperationCanceledException ? "REQUEST_TIMEOUT" : e is IOException or HttpRequestException ? "STREAM_INTERRUPTED" : "INVALID_PROTOCOL";
    public void Dispose() => client.Dispose();
}
