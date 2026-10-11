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
public sealed class BrowserSession(IDesktopImageChannel desktop, TimeSpan? budget = null)
{
    private Task? lateReadObserver;
    public string? LateReadFailureCode { get; private set; }
    private async ValueTask<JsonDocument?> ReadBounded(Stream input,CancellationToken cancellation)
    {
        // Console stdin may ignore native read cancellation. Bound the waiter, then observe/dispose a late result.
        Task<JsonDocument?> pending=NativeMessagingCodec.ReadAsync(input,cancellation).AsTask();
        try{return await pending.WaitAsync(cancellation).ConfigureAwait(false);}
        catch{if(!pending.IsCompleted)lateReadObserver=ObserveLateRead(pending);throw;}
    }
    private async Task ObserveLateRead(Task<JsonDocument?> pending)
    {
        try{using var document=await pending.ConfigureAwait(false);}
        catch(Exception error){LateReadFailureCode=error.GetType().Name;} // Finite diagnostic; never echo late bytes or leave a fault unobserved.
    }
    public async Task<int> RunAsync(Stream input,Stream output,CancellationToken cancellation=default)
    {
        using var deadline=new CancellationTokenSource(budget??TimeSpan.FromSeconds(30));
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,deadline.Token);
        using var assembler=new ImageTransferAssembler();BrowserGrant? grant=null;bool begun=false,committed=false;
        try
        {
            while(true)
            {
                using var doc=await ReadBounded(input,linked.Token).ConfigureAwait(false);
                if(doc==null)throw new BrowserException(BrowserFailure.Disconnected);
                var root=doc.RootElement;
                string? type=root.TryGetProperty("type",out var t)&&t.ValueKind==JsonValueKind.String?t.GetString():null;
                if(type=="OfferImage")
                {
                    BrowserEnvelope.Fields(root,"type","operationId");if(grant!=null)throw new InvalidDataException("Duplicate offer.");
                    Guid id=BrowserEnvelope.Id(root,"operationId");
                    grant=await desktop.OfferAsync(id,linked.Token).ConfigureAwait(false);
                    if(!BrowserEnvelope.Valid(grant,id))throw new BrowserException(BrowserFailure.DesktopUntrusted);
                    await NativeMessagingCodec.WriteAsync(output,new{type="Ready",operationId=id,protocol=1},linked.Token);
                }
                else if(type=="BeginImage")
                {
                    BrowserEnvelope.Fields(root,"type","transferId","totalLength","mime","sha256");
                    Guid id=BrowserEnvelope.Id(root,"transferId");if(grant==null||grant.OperationId!=id||begun)throw new InvalidDataException("Offer required.");
                    if(!root.GetProperty("totalLength").TryGetInt32(out int length)||root.GetProperty("mime").GetString()!="image/png"||root.GetProperty("sha256").GetString() is not {} hash)throw new InvalidDataException("Invalid declaration.");
                    assembler.Begin(id,length,"image/png",hash);begun=true;
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
                    try{size=ImagePolicy.InspectPng(image.Bytes.Span);}catch(ImageJobException){throw new BrowserException(BrowserFailure.InvalidImage);}
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
            BrowserFailure code=error switch{BrowserException b=>b.Code,OperationCanceledException=>deadline.IsCancellationRequested?BrowserFailure.Timeout:BrowserFailure.Cancelled,IOException=>BrowserFailure.Disconnected,JsonException or InvalidDataException or InvalidOperationException or FormatException=>BrowserFailure.InvalidMessage,_=>BrowserFailure.InternalFailure};
            try{using var report=new CancellationTokenSource(TimeSpan.FromMilliseconds(500));await NativeMessagingCodec.WriteAsync(output,new{type="Error",code=code.ToString()},report.Token);}catch(Exception e)when(e is IOException or OperationCanceledException or ObjectDisposedException){Console.Error.WriteLine("NativeHost response unavailable.");}
            return 3;
        }
        finally
        {
            assembler.Dispose();
            if(grant!=null&&!committed)
                try{using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(1));await desktop.CancelAsync(grant,stop.Token).ConfigureAwait(false);}catch(Exception e)when(e is IOException or OperationCanceledException or BrowserException){Console.Error.WriteLine("NativeHost desktop cleanup unavailable.");}
            await desktop.DisposeAsync();
            _=lateReadObserver; // No second read/operation after deadline; process entry returns and releases OS stdio.
        }
    }
}
