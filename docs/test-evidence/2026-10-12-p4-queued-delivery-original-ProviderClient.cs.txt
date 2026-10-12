using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using KuaiYi.Core;

namespace KuaiYi.Providers;

/// <summary>Real HTTP transport with per-request credentials, redirect rejection, bounded parsing and late-event checks.</summary>
public sealed class ProviderClient : IDisposable
{
    public const int MaximumOutputCodeUnits = 120_000;
    private sealed class ConnectionObservation{public int Attempted,Ready;public ProviderRequestBudget.Deadline? Total;}
    private static readonly HttpRequestOptionsKey<ConnectionObservation> ConnectionKey=new("KuaiYi.ConnectObservation");
    private readonly DevelopmentTranslationBoundary? developmentBoundary;
    private readonly TimeProvider clock;
    internal TimeProvider PhaseClock{get;}
    private readonly Dictionary<int,HttpClient> clients=[];private readonly object clientsSync=new();private bool disposed;
    private readonly Func<SocketsHttpConnectionContext,CancellationToken,ValueTask<Stream>>? testConnector;
    public ProviderClient(DevelopmentTranslationBoundary? developmentBoundary=null,TimeProvider? clock=null,TimeProvider? phaseClock=null)
     :this(developmentBoundary,clock,phaseClock,null){}
    internal ProviderClient(DevelopmentTranslationBoundary? developmentBoundary,TimeProvider? clock,TimeProvider? phaseClock,Func<SocketsHttpConnectionContext,CancellationToken,ValueTask<Stream>>? testConnector)
    {this.developmentBoundary=developmentBoundary;this.clock=clock??TimeProvider.System;PhaseClock=phaseClock??TimeProvider.System;this.testConnector=testConnector;}
    public ProviderRequestBudget FreezeBudget(ApiProfile profile)=>new(profile.TotalTimeoutSeconds,clock);
    private HttpClient Client(int seconds)
    {lock(clientsSync){ObjectDisposedException.ThrowIf(disposed,this);if(!clients.TryGetValue(seconds,out var client)){client=CreateClient(null,seconds);clients.Add(seconds,client);}return client;}}
    private HttpClient CreateClient(DevelopmentTranslationBoundary? boundary,int connectSeconds)
    {
        var handler=new SocketsHttpHandler{AllowAutoRedirect=false,ConnectTimeout=TimeSpan.FromSeconds(connectSeconds),MaxResponseHeadersLength=64};
        if(boundary!=null){handler.UseProxy=false;handler.UseCookies=false;}
        handler.ConnectCallback=async(context,cancellation)=>
        {
            context.InitialRequestMessage.Options.TryGetValue(ConnectionKey,out var observation);if(observation!=null)Volatile.Write(ref observation.Attempted,1);
            using var deadline=new ProviderPhaseDeadline(TimeSpan.FromSeconds(connectSeconds),PhaseClock,cancellation,"CONNECT_TIMEOUT");
            try
            {
                observation?.Total?.Check();deadline.Check();
                Stream connected=await (boundary!=null?boundary.ConnectAsync(context,deadline.Token):testConnector!=null?testConnector(context,deadline.Token):ConnectSocket(context,deadline.Token)).ConfigureAwait(false);
                try{observation?.Total?.Check();deadline.Check();return connected;}catch{await connected.DisposeAsync().ConfigureAwait(false);throw;}
            }
            catch(Exception error)when(deadline.Expired&&!cancellation.IsCancellationRequested&&error is OperationCanceledException or IOException){throw new PhaseTimeoutException("CONNECT_TIMEOUT");}
        };
        handler.PlaintextStreamFilter=(context,_)=>{if(context.InitialRequestMessage.Options.TryGetValue(ConnectionKey,out var observation))Volatile.Write(ref observation.Ready,1);return ValueTask.FromResult(context.PlaintextStream);};
        return new(handler){Timeout=Timeout.InfiniteTimeSpan};
    }
    private static async ValueTask<Stream> ConnectSocket(SocketsHttpConnectionContext context,CancellationToken cancellation)
    {
        var socket=new Socket(SocketType.Stream,ProtocolType.Tcp){NoDelay=true};
        try{await socket.ConnectAsync(context.DnsEndPoint,cancellation).ConfigureAwait(false);return new NetworkStream(socket,ownsSocket:true);}catch{socket.Dispose();throw;}
    }

    /// <summary>Low-level transport. Caller owns cancellation; product consumption uses GenerateEnvelopesAsync and ProviderEventReceiver.</summary>
    public async IAsyncEnumerable<ModelEvent> GenerateAsync(TranslationRequest request, RuntimeGate gate,
        RequestIdentity identity, SecretLease? secret, [EnumeratorCancellation] CancellationToken cancellation = default, Func<bool>? sendAdmission = null,ProviderRequestBudget? requestBudget=null)
    {
        ObjectDisposedException.ThrowIf(disposed,this);ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(gate);
        if (!gate.CanApply(identity)) yield break;
        if(gate.IsConnectionTest(identity)&&(request.SourceText!=ApiConnectionTestContent.Source||request.SystemPrompt!=ApiConnectionTestContent.System||request.Image!=null))
        {yield return new(ModelEventKind.Failed,Reason:"FIXED_TEST_CONTENT_REQUIRED");yield break;}
        if(request.Image is {} image&&(image.Length>request.Profile.ImageByteLimit||request.Profile.VisionCapability is not(null or KuaiYi.Core.Settings.Capability.Supported)))
        {yield return new(ModelEventKind.Failed,Reason:"VISION_UNAVAILABLE_OR_IMAGE_LIMIT");yield break;}
        // The client boundary is fixed at startup. Removing/changing a record marker cannot widen it.
        var boundary=developmentBoundary??request.Profile.DevelopmentBoundary;
        if(boundary!=null&&!boundary.Allows(request.Profile,secret))
        {if(gate.CanApply(identity))yield return new(ModelEventKind.Failed,Reason:"TEST_BOUNDARY_REJECTED");yield break;}
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
        using var pinnedTransport=boundary!=null?CreateClient(boundary,request.Profile.PhaseTimeouts.ConnectSeconds):null;
        var transport=pinnedTransport??Client(request.Profile.PhaseTimeouts.ConnectSeconds);
        var budget=requestBudget??FreezeBudget(request.Profile);
        using (message)
        using (var total = budget.Link(cancellation))
        {
            if(budget.Seconds!=request.Profile.TotalTimeoutSeconds){if(gate.CanApply(identity))yield return new(ModelEventKind.Failed,Reason:"CONFIG_INVALID");yield break;}
            HttpResponseMessage? response = null;
            ProviderReadActivity? acceptedBodyActivity=null;
            string? sendError = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if(boundary!=null&&!boundary.Allows(request.Profile,secret)){sendError="TEST_BOUNDARY_REJECTED";break;}
                using var headersDeadline=new ProviderPhaseDeadline(TimeSpan.FromSeconds(request.Profile.PhaseTimeouts.HeadersSeconds),PhaseClock,total.Token,"RESPONSE_HEADERS_TIMEOUT");
                var connection=new ConnectionObservation{Total=total};
                try
                {
                    total.Check();headersDeadline.Check();
                    // Each attempt obtains fresh content and headers, with no credentials on DefaultRequestHeaders.
                    using HttpRequestMessage outgoing = CreateMessage(request, adapter, endpoint, secret);outgoing.Options.Set(ConnectionKey,connection);
                    bool FinalAdmission()
                    {
                        bool eligible=(boundary==null||boundary.Allows(request.Profile,secret))&&(sendAdmission?.Invoke()??true)&&(boundary==null||boundary.Allows(request.Profile,secret));
                        total.Check();headersDeadline.Check();return eligible;
                    }
                    if (!gate.TryBeginSend(identity, new EndpointOrigin(endpoint), FinalAdmission,
                        () => transport.SendAsync(outgoing, HttpCompletionOption.ResponseHeadersRead, headersDeadline.Token), out var pending))
                    { sendError = "CANCELLED_OR_UNCONSENTED"; break; }
                    response = await pending!.ConfigureAwait(false);
                    total.Check();headersDeadline.Check();
                    acceptedBodyActivity=new(request.Profile.PhaseTimeouts,PhaseClock);
                    if (!gate.CanApply(identity)) { sendError = "STALE_IDENTITY"; break; }
                    if (response.StatusCode is not (HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable) || attempt == 1) break;
                    TimeSpan? retry = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
                    if (retry is { } delay && (delay < TimeSpan.Zero || delay > TimeSpan.FromSeconds(15))) break;
                    response.Dispose(); response = null;
                    await Task.Delay(retry ?? TimeSpan.FromSeconds(1), total.Token).ConfigureAwait(false);
                    total.Check();
                }
                catch (OperationCanceledException error) { sendError = DeadlineError(error,cancellation,total,headersDeadline);if(sendError=="REQUEST_TIMEOUT"&&!total.IsCancellationRequested&&!cancellation.IsCancellationRequested&&Volatile.Read(ref connection.Attempted)==1&&Volatile.Read(ref connection.Ready)==0)sendError="CONNECT_TIMEOUT"; break; }
                catch (HttpRequestException e) { sendError = DeliveryError(cancellation,total)??PhaseReason(e)??(headersDeadline.Expired?headersDeadline.Reason:e.HttpRequestError == HttpRequestError.SecureConnectionError ? "TLS_ERROR" : "NETWORK_ERROR"); break; }
            }
            if (sendError is not null) { response?.Dispose(); if (gate.CanApply(identity)) yield return new(ModelEventKind.Failed, Reason: DeliveryError(cancellation,total)??sendError); yield break; }
            using (response!)
            {
                string? transitionError=DeliveryError(cancellation,total);
                if(transitionError!=null){if(gate.CanApply(identity))yield return new(ModelEventKind.Failed,Reason:transitionError);yield break;}
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
                    if (gate.CanApply(identity)) yield return new(ModelEventKind.Failed, Reason: DeliveryError(cancellation,total)??reason);
                    yield break;
                }
                if (!gate.CanApply(identity)) yield break;
                // Origin is frozen at accepted headers, before the consumer can suspend Started.
                var activity=acceptedBodyActivity!;
                transitionError=DeliveryError(cancellation,total,activity);
                if(transitionError!=null){yield return new(ModelEventKind.Failed,Reason:transitionError);yield break;}
                yield return new(ModelEventKind.Started);
                if (!gate.CanApply(identity)) yield break;
                Stream? openedBody = null;
                string? openError = null;
                try { total.Check();using var first=activity.Link(total.Token);openedBody = await response.Content.ReadAsStreamAsync(first.Token).ConfigureAwait(false);total.Check();first.Check(); }
                catch (Exception e) when (IsProtocolOrReadFailure(e)) { openError = ErrorFor(e, cancellation,total); }
                if (openError is not null)
                {
                    if(openedBody!=null)await openedBody.DisposeAsync().ConfigureAwait(false);
                    if (gate.CanApply(identity)) yield return new(ModelEventKind.Failed, Reason: DeliveryError(cancellation,total,activity)??openError);
                    yield break;
                }
                await using Stream body = openedBody!;
                string? mime = response.Content.Headers.ContentType?.MediaType;
                if (mime == "application/json")
                {
                    IReadOnlyList<ModelEvent>? events = null;
                    string? failure = null;
                    try { events = adapter.ParseNonStreaming(await ReadBoundedJsonAsync(body,request.Profile.Protocol,activity,total).ConfigureAwait(false));total.Check();activity.Check(); }
                    catch (Exception e) when (IsProtocolOrReadFailure(e)) { failure = ErrorFor(e, cancellation,total); }
                    if (failure is not null) { if (gate.CanApply(identity)) yield return new(ModelEventKind.Failed, Reason: DeliveryError(cancellation,total,activity)??failure); yield break; }
                    int output = 0;
                    foreach (var item in events!)
                    {
                        if (!gate.CanApply(identity)) yield break;
                        string? deliveryError=DeliveryError(cancellation,total,activity);
                        if(deliveryError!=null){yield return new(ModelEventKind.Failed,Reason:deliveryError);yield break;}
                        if (item.Kind == ModelEventKind.TextDelta && (output += item.Text!.Length) > MaximumOutputCodeUnits)
                        { yield return new(ModelEventKind.Failed, Reason: "LOCAL_OUTPUT_LIMIT"); yield break; }
                        yield return item;
                    }
                    yield break;
                }
                transitionError=DeliveryError(cancellation,total,activity);
                if(transitionError!=null){if(gate.CanApply(identity))yield return new(ModelEventKind.Failed,Reason:transitionError);yield break;}
                if (mime != "text/event-stream") { if (gate.CanApply(identity)) yield return new(ModelEventKind.Failed, Reason: "UNSUPPORTED_CONTENT_TYPE"); yield break; }
                CancellationToken currentReadToken=total.Token;
                await using var enumerator = SseReader.ReadAsync(body,total.Token,()=>currentReadToken).GetAsyncEnumerator(total.Token);
                int outputLength = 0;

                while (true)
                {
                    IReadOnlyList<ModelEvent>? events = null;
                    bool more = false;
                    string? failure = null;
                    ProviderPhaseDeadline? idle=null;
                    try
                    {
                        total.Check();
                        idle=activity.Link(total.Token);currentReadToken=idle.Token;
                        // Cancelling the underlying body read is required; WaitAsync alone leaves a blocked enumerator.
                        using var readCancellation = idle.Token.Register(() => body.Dispose());
                        more = await enumerator.MoveNextAsync().ConfigureAwait(false);
                        total.Check();idle.Check();
                        if (more){events=adapter.ParseEvent(enumerator.Current.Data);total.Check();idle.Check();if(adapter.LastEventWasEffective)activity.Effective();}
                    }
                    catch (Exception e) when (IsProtocolOrReadFailure(e)) { failure = DeadlineError(e,cancellation,total,idle); }
                    finally{idle?.Dispose();}
                    if (!gate.CanApply(identity)) yield break;
                    if (failure is not null) { yield return new(ModelEventKind.Failed, Reason: DeliveryError(cancellation,total,activity)??failure); yield break; }
                    if (!more) { yield return new(ModelEventKind.Failed, Reason: DeliveryError(cancellation,total,activity)??"STREAM_INTERRUPTED"); yield break; }
                    foreach (var item in events!)
                    {
                        if (!gate.CanApply(identity)) yield break;
                        string? deliveryError=DeliveryError(cancellation,total,activity);
                        if(deliveryError!=null){yield return new(ModelEventKind.Failed,Reason:deliveryError);yield break;}
                        if (item.Kind == ModelEventKind.TextDelta)
                        {
                            int remaining = MaximumOutputCodeUnits - outputLength;
                            if (item.Text!.Length > remaining)
                            {
                                if (remaining > 0 && char.IsHighSurrogate(item.Text[remaining - 1])) remaining--;
                                if (remaining > 0) yield return item with { Text = item.Text[..remaining] };
                                if (gate.CanApply(identity)) yield return new(ModelEventKind.Failed, Reason: DeliveryError(cancellation,total,activity)??"LOCAL_OUTPUT_LIMIT");
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
        RequestIdentity identity, SecretLease? secret, [EnumeratorCancellation] CancellationToken cancellation = default, Func<bool>? sendAdmission = null,ProviderRequestBudget? requestBudget=null)
    {
        ArgumentNullException.ThrowIfNull(gate);
        if (!gate.TryLinkRequestCancellation(identity, cancellation, out var linked)) yield break;
        using var requestLifetime = linked!;
        var budget=requestBudget??(request.Profile.TotalTimeoutSeconds is >=1 and <=600?FreezeBudget(request.Profile):null);
        await foreach (var item in GenerateAsync(request, gate, identity, secret, requestLifetime.Token,sendAdmission,budget).ConfigureAwait(false))
        {
            if (!gate.CanApply(identity)) yield break;
            string? deliveryError=null;
            try{requestLifetime.Token.ThrowIfCancellationRequested();budget?.Check();}
            catch(OperationCanceledException){deliveryError=requestLifetime.IsCancellationRequested?"CANCELLED":"REQUEST_TIMEOUT";}
            if(deliveryError!=null){yield return new(identity,new(ModelEventKind.Failed,Reason:deliveryError));yield break;}
            yield return new(identity, item);
        }
    }

    private static HttpRequestMessage CreateMessage(TranslationRequest request, IProtocolAdapter adapter, Uri endpoint, SecretLease? secret)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = JsonContent.Create(adapter.BuildBody(request)) };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(request.Profile.Stream ? "text/event-stream" : "application/json"));
        if (request.Profile.Authentication != Authentication.None)
        {
            string key = secret?.HeaderValue(request.Profile.Authentication==Authentication.CustomHeader) ?? throw new ArgumentException("Missing secret.");
            if (request.Profile.Authentication == Authentication.Bearer) message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            else message.Headers.Add(request.Profile.Authentication==Authentication.CustomHeader?request.Profile.AuthenticationHeader:"x-api-key", key);
        }
        if(request.Profile.Headers is {} headers)foreach(var entry in headers)message.Headers.Add(entry.Key,entry.Value);
        if (request.Profile.Protocol == ApiProtocol.AnthropicMessages) message.Headers.Add("anthropic-version", request.Profile.AnthropicVersion);
        return message;
    }

    private static async Task<string> ReadBoundedJsonAsync(Stream body,ApiProtocol protocol,ProviderReadActivity activity,ProviderRequestBudget.Deadline total)
    {
        using var output=new MemoryStream();byte[] bytes=new byte[4096];var progress=new JsonProtocolProgress(protocol);
        while(true)
        {
            total.Check();using var idle=activity.Link(total.Token);using var close=idle.Token.Register(()=>body.Dispose());
            int n;
            try{n=await body.ReadAsync(bytes,idle.Token).ConfigureAwait(false);total.Check();idle.Check();}
            catch(Exception error)when(IsProtocolOrReadFailure(error)&&idle.Expired&&!total.IsCancellationRequested){throw new PhaseTimeoutException(idle.Reason);}
            if(output.Length+n>SseReader.MaximumEventBytes)throw new InvalidDataException("JSON response exceeds local limit.");output.Write(bytes,0,n);
            bool effective=progress.Scan(output.GetBuffer().AsSpan(0,checked((int)output.Length)),n==0);total.Check();idle.Check();if(effective)activity.Effective();
            if(n==0)break;
        }
        return new UTF8Encoding(false,true).GetString(output.GetBuffer(),0,checked((int)output.Length));
    }
    private static string? PhaseReason(Exception error)
    {for(Exception? current=error;current!=null;current=current.InnerException)if(current is PhaseTimeoutException phase)return phase.Reason;return null;}
    private static string DeadlineError(Exception error,CancellationToken cancellation,ProviderRequestBudget.Deadline total,ProviderPhaseDeadline? phase)
        =>cancellation.IsCancellationRequested?"CANCELLED":total.IsCancellationRequested?"REQUEST_TIMEOUT":phase?.Expired==true?phase.Reason:PhaseReason(error)??ErrorFor(error,cancellation);
    private static string? DeliveryError(CancellationToken cancellation,ProviderRequestBudget.Deadline total,ProviderReadActivity? activity=null)
    {try{total.Check();activity?.Check();return null;}catch(OperationCanceledException error){return ErrorFor(error,cancellation,total);}}
    private static bool IsProtocolOrReadFailure(Exception e) => e is JsonException or InvalidDataException or DecoderFallbackException or
        IOException or HttpRequestException or OperationCanceledException or InvalidOperationException or ObjectDisposedException;
    private static string ErrorFor(Exception e, CancellationToken cancellation,ProviderRequestBudget.Deadline? total=null) => cancellation.IsCancellationRequested ? "CANCELLED" : total?.IsCancellationRequested==true?"REQUEST_TIMEOUT":PhaseReason(e) is {} reason?reason:
        e is OperationCanceledException ? "REQUEST_TIMEOUT" : e is IOException or HttpRequestException ? "STREAM_INTERRUPTED" : "INVALID_PROTOCOL";
    public void Dispose(){lock(clientsSync){if(disposed)return;disposed=true;foreach(var client in clients.Values)client.Dispose();clients.Clear();}}
}
