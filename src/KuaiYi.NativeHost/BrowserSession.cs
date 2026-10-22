using System.Security.Cryptography;
using System.Text.Json;
using KuaiYi.Contracts;
using KuaiYi.Core;
using KuaiYi.Imaging;
namespace KuaiYi.NativeHost;

public enum BrowserFailure { UnsupportedPlatform, HostNotConfigured, OriginRejected, DesktopUnavailable, DesktopUntrusted, ActivationRequired, Stale, InvalidMessage, InvalidImage, Cancelled, Disconnected, Timeout, InternalFailure }
public sealed class BrowserException(BrowserFailure code) : Exception(code.ToString()) { public BrowserFailure Code { get; } = code; }
// Issued only by the verified desktop connection; no activation, identity or grant is accepted from a webpage/native input frame.
public sealed record BrowserGrant(Guid OperationId, RequestIdentity Identity, Guid Nonce);
public interface IDesktopImageChannel : IAsyncDisposable
{
    ValueTask<BrowserGrant> OfferAsync(Guid operation, CancellationToken cancellation);
    ValueTask SubmitAsync(BrowserGrant grant, ReceivedImage image, ImageSize size, CancellationToken cancellation);
    ValueTask CancelAsync(BrowserGrant grant, CancellationToken cancellation);
}
public interface IDesktopConfirmationChannel:IDesktopImageChannel
{
    ValueTask<BrowserGrant> OfferWithConfirmationAsync(Guid operation,Func<CancellationToken,ValueTask> waiting,CancellationToken cancellation);
}
public static class BrowserEnvelope
{
    public static void Fields(JsonElement root, params string[] names)
    {
        if(root.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Object required.");
        var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var f in root.EnumerateObject())if(!names.Contains(f.Name,StringComparer.Ordinal)||!seen.Add(f.Name))throw new InvalidDataException("Unknown/duplicate field.");
        if(seen.Count!=names.Length)throw new InvalidDataException("Missing field.");
    }
    public static Guid Id(JsonElement root,string name)
    {
        var f=root.GetProperty(name);
        if(f.ValueKind!=JsonValueKind.String||f.GetString() is not {Length:36} s||!Guid.TryParseExact(s,"D",out var id)||id==Guid.Empty)throw new InvalidDataException("Invalid identifier.");
        return id;
    }
    public static bool Valid(BrowserGrant g, Guid operation)=>g.OperationId==operation&&g.Nonce!=Guid.Empty&&g.Identity.SessionId!=Guid.Empty&&g.Identity.VersionId!=Guid.Empty&&g.Identity.RequestId!=Guid.Empty&&g.Identity.Epoch>0;
}
/// <summary>Actual Native Messaging loop. A single explicit operation per connection; no network, files, OCR or models.</summary>
public sealed class BrowserSession(IDesktopImageChannel desktop, TimeSpan? budget = null, TimeProvider? timeProvider = null)
{
    private Task? lateReadObserver;
    public string? LateReadFailureCode { get; private set; }
    internal Func<Stream,CancellationToken,Task<JsonDocument?>>? ReadForTest { get; init; }
    internal Func<Task<JsonDocument?>,Task>? BeforeLateReadObserverForTest { get; init; }
    internal Action<Task<JsonDocument?>>? LateReadObserverStartedForTest { get; init; }
    internal Task? LateReadObserverForTest => lateReadObserver;
    private async ValueTask<JsonDocument?> ReadBounded(Stream input,CancellationToken cancellation)
    {
        // Console stdin may ignore native read cancellation. Bound the waiter, then observe/dispose a late result.
        Task<JsonDocument?> pending=ReadForTest?.Invoke(input,cancellation)??NativeMessagingCodec.ReadAsync(input,cancellation).AsTask();
        return await AwaitRead(pending,cancellation).ConfigureAwait(false);
    }
    private async ValueTask<JsonDocument?> AwaitRead(Task<JsonDocument?> pending,CancellationToken cancellation)
    {
        try{return await pending.WaitAsync(cancellation).ConfigureAwait(false);}
        catch
        {
            if(BeforeLateReadObserverForTest!=null)await BeforeLateReadObserverForTest(pending).ConfigureAwait(false);
            lateReadObserver=ObserveLateRead(pending); // Even a result/fault that completed between WaitAsync failure and catch owns cleanup.
            throw;
        }
    }
    private async Task ObserveLateRead(Task<JsonDocument?> pending)
    {
        LateReadObserverStartedForTest?.Invoke(pending);
        try{using var document=await pending.ConfigureAwait(false);}
        catch(Exception error){LateReadFailureCode=error.GetType().Name;} // Finite diagnostic; never echo late bytes or leave a fault unobserved.
    }
    public async Task<int> RunAsync(Stream input,Stream output,CancellationToken cancellation=default)
    {
        var time=timeProvider??TimeProvider.System;TimeSpan totalBudget=budget??BrowserTiming.Total,phaseBudget=BrowserTiming.Handshake;
        long started=time.GetTimestamp(),phaseStarted=started;
        using var deadline=new CancellationTokenSource(totalBudget,time);
        using var phaseDeadline=new CancellationTokenSource(BrowserTiming.Handshake,time);
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,deadline.Token,phaseDeadline.Token);
        using var assembler=new ImageTransferAssembler(timeProvider);BrowserGrant? grant=null;bool begun=false,committed=false;
        Task<JsonDocument?>? nextRead=null;bool desktopDisposed=false;
        try
        {
            while(true)
            {
                var queued=nextRead;nextRead=null;
                using var doc=queued!=null?await AwaitRead(queued,linked.Token).ConfigureAwait(false):await ReadBounded(input,linked.Token).ConfigureAwait(false);
                CheckPhase(); // Successful read fast paths and delayed timer callbacks still cannot deliver expired content.
                if(doc==null)throw new BrowserException(BrowserFailure.Disconnected);
                var root=doc.RootElement;
                string? type=root.TryGetProperty("type",out var t)&&t.ValueKind==JsonValueKind.String?t.GetString():null;
                if(type=="OfferImage")
                {
                    BrowserEnvelope.Fields(root,"type","operationId");if(grant!=null)throw new InvalidDataException("Duplicate offer.");
                    Guid id=BrowserEnvelope.Id(root,"operationId");
                    using var offerStop=CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
                    var offering=desktop is IDesktopConfirmationChannel confirming
                        ?confirming.OfferWithConfirmationAsync(id,async token=>
                        {
                            ChangePhase(BrowserTiming.Confirmation);
                            await NativeMessagingCodec.WriteAsync(output,new{type="ConfirmationPending",operationId=id,protocol=1,timeoutMs=BrowserTiming.ConfirmationMilliseconds},token);
                        },offerStop.Token).AsTask()
                        :desktop.OfferAsync(id,offerStop.Token).AsTask();
                    // Observe browser cancellation/EOF during the actual desktop prompt, not only after its decision.
                    nextRead=NativeMessagingCodec.ReadAsync(input,linked.Token).AsTask();
                    try
                    {
                        await Task.WhenAny(offering,nextRead).WaitAsync(linked.Token).ConfigureAwait(false);
                        if(nextRead.IsCompleted)
                        {
                            var control=nextRead;nextRead=null;using var early=await control.ConfigureAwait(false);
                            if(early==null)throw new BrowserException(BrowserFailure.Disconnected);
                            BrowserEnvelope.Fields(early.RootElement,"type","operationId");
                            if(early.RootElement.GetProperty("type").GetString()=="CancelImage"&&BrowserEnvelope.Id(early.RootElement,"operationId")==id)throw new BrowserException(BrowserFailure.Cancelled);
                            throw new InvalidDataException("Input before Ready.");
                        }
                        grant=await offering.ConfigureAwait(false);
                    }
                    catch
                    {
                        offerStop.Cancel();await DisposeDesktop();_ = ObserveRejectedOffer(offering);throw;
                    }
                    if(!BrowserEnvelope.Valid(grant,id))throw new BrowserException(BrowserFailure.DesktopUntrusted);
                    ChangePhase(BrowserTiming.Transfer);
                    await NativeMessagingCodec.WriteAsync(output,new{type="Ready",operationId=id,protocol=1},linked.Token);
                }
                else if(type=="BeginImage")
                {
                    BrowserEnvelope.Fields(root,"type","transferId","totalLength","mime","sha256");
                    Guid id=BrowserEnvelope.Id(root,"transferId");if(grant==null||grant.OperationId!=id||begun)throw new InvalidDataException("Offer required.");
                    if(!root.GetProperty("totalLength").TryGetInt32(out int length)||root.GetProperty("mime").GetString() is not {} mime||root.GetProperty("sha256").GetString() is not {} hash)throw new InvalidDataException("Invalid declaration.");
                    assembler.Begin(id,length,mime,hash);begun=true;
                    await NativeMessagingCodec.WriteAsync(output,new{type="Begun",operationId=id},linked.Token);
                }
                else if(type=="Chunk")
                {
                    if(!begun)throw new InvalidDataException("Begin required.");
                    ImageChunkWire.Append(root,assembler);
                    await NativeMessagingCodec.WriteAsync(output,new{type="ChunkAccepted",operationId=grant!.OperationId,index=root.GetProperty("index").GetInt32()},linked.Token);
                }
                else if(type=="CommitImage")
                {
                    BrowserEnvelope.Fields(root,"type","transferId");Guid id=BrowserEnvelope.Id(root,"transferId");
                    if(!begun||grant==null||grant.OperationId!=id)throw new InvalidDataException("Begin required.");
                    using var image=assembler.Commit(id);ImageSize size;
                    try{size=BrowserEncodedImagePolicy.Inspect(image.Bytes.Span,image.Mime,linked.Token);}catch(ImageJobException){throw new BrowserException(BrowserFailure.InvalidImage);}
                    await desktop.SubmitAsync(grant,image,size,linked.Token).ConfigureAwait(false);committed=true;
                    await NativeMessagingCodec.WriteAsync(output,new{type="Committed",operationId=id},linked.Token);return 0;
                }
                else if(type=="CancelImage")
                {
                    BrowserEnvelope.Fields(root,"type","operationId");Guid id=BrowserEnvelope.Id(root,"operationId");
                    if(grant==null||id!=grant.OperationId)throw new InvalidDataException("Unknown cancellation.");
                    assembler.Dispose();await desktop.CancelAsync(grant,linked.Token).ConfigureAwait(false);grant=null;
                    await NativeMessagingCodec.WriteAsync(output,new{type="Cancelled",operationId=id},linked.Token);return 0;
                }
                else throw new InvalidDataException("Unsupported browser command.");
            }
        }
        catch(Exception error)
        {
            BrowserFailure code=error switch{BrowserException b=>b.Code,OperationCanceledException=>deadline.IsCancellationRequested||phaseDeadline.IsCancellationRequested?BrowserFailure.Timeout:BrowserFailure.Cancelled,IOException=>BrowserFailure.Disconnected,JsonException or InvalidDataException or InvalidOperationException or FormatException=>BrowserFailure.InvalidMessage,_=>BrowserFailure.InternalFailure};
            try{using var report=new CancellationTokenSource(TimeSpan.FromMilliseconds(500));await NativeMessagingCodec.WriteAsync(output,new{type="Error",code=code.ToString()},report.Token);}catch(Exception e)when(e is IOException or OperationCanceledException or ObjectDisposedException){Console.Error.WriteLine("NativeHost response unavailable.");}
            return 3;
        }
        finally
        {
            assembler.Dispose();
            if(grant!=null&&!committed)
                try{using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(1));await desktop.CancelAsync(grant,stop.Token).ConfigureAwait(false);}catch(Exception e)when(e is IOException or OperationCanceledException or BrowserException){Console.Error.WriteLine("NativeHost desktop cleanup unavailable.");}
            linked.Cancel();if(nextRead!=null)lateReadObserver=ObserveLateRead(nextRead);
            await DisposeDesktop();
            _=lateReadObserver; // No second read/operation after deadline; process entry returns and releases OS stdio.
        }
        async ValueTask DisposeDesktop(){if(!desktopDisposed){desktopDisposed=true;await desktop.DisposeAsync();}}
        void CheckPhase()
        {
            linked.Token.ThrowIfCancellationRequested();
            if(time.GetElapsedTime(started)>=totalBudget||time.GetElapsedTime(phaseStarted)>=phaseBudget)throw new BrowserException(BrowserFailure.Timeout);
        }
        void ChangePhase(TimeSpan next){CheckPhase();phaseStarted=time.GetTimestamp();phaseBudget=next;phaseDeadline.CancelAfter(next);}
    }
    private async Task ObserveRejectedOffer(Task<BrowserGrant> pending)
    {
        try{var late=await pending.ConfigureAwait(false);using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(1));await desktop.CancelAsync(late,stop.Token);}
        catch(Exception error){LateReadFailureCode??=error.GetType().Name;} // Finite observation; a late grant never becomes Ready/image permission.
    }
}
