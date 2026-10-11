using System.Security.Cryptography;
using System.Text.Json;
using KuaiYi.Contracts;
using KuaiYi.Imaging;
namespace KuaiYi.NativeHost;
/// <summary>Actual internal IPC codec. Production factory supplies only an OS-verified, pinned local peer.</summary>
public sealed class DesktopPipeChannel(Stream pipe,string origin):IDesktopImageChannel
{
    private readonly Guid connection=Guid.NewGuid();
    public async ValueTask<BrowserGrant> OfferAsync(Guid operation,CancellationToken cancellation)
    {
        await Send(new{phase="OfferImage",operationId=operation,origin},cancellation);
        using var response=await Reply(cancellation);var root=response.RootElement;
        if(root.GetProperty("phase").GetString()=="Denied")throw new BrowserException(BrowserFailure.ActivationRequired);
        BrowserEnvelope.Fields(root,"phase","operationId","identity","nonce");
        if(root.GetProperty("phase").GetString()!="Ready")throw new BrowserException(BrowserFailure.DesktopUntrusted);
        var identity=JsonSerializer.Deserialize<KuaiYi.Core.RequestIdentity>(root.GetProperty("identity"));
        var grant=new BrowserGrant(BrowserEnvelope.Id(root,"operationId"),identity,BrowserEnvelope.Id(root,"nonce"));
        if(!BrowserEnvelope.Valid(grant,operation))throw new BrowserException(BrowserFailure.DesktopUntrusted);return grant;
    }
    public async ValueTask SubmitAsync(BrowserGrant grant,ReceivedImage image,ImageSize size,CancellationToken cancellation)
    {
        await Send(new{phase="BeginImage",grant,totalLength=image.Bytes.Length,mime=image.Mime,width=size.Width,height=size.Height,sha256=Convert.ToHexString(SHA256.HashData(image.Bytes.Span))},cancellation);
        using(var ready=await Reply(cancellation))
        {
            var r=ready.RootElement;BrowserEnvelope.Fields(r,"phase","operationId");
            if(r.GetProperty("phase").GetString()!="Begun"||BrowserEnvelope.Id(r,"operationId")!=grant.OperationId)throw new BrowserException(BrowserFailure.Stale);
        }
        int index=0;
        for(int offset=0;offset<image.Bytes.Length;offset+=ImageTransferAssembler.ChunkLimit)
        {
            int count=Math.Min(ImageTransferAssembler.ChunkLimit,image.Bytes.Length-offset);byte[] data=new byte[count+4];
            try{System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(data,index++);image.Bytes.Span.Slice(offset,count).CopyTo(data.AsSpan(4));await IpcFrameCodec.WriteAsync(pipe,new(IpcMessageType.BinaryChunk,connection,data),cancellation);}
            finally{CryptographicOperations.ZeroMemory(data);}
        }
        await Send(new{phase="CommitImage",grant},cancellation);
        using var reply=await Reply(cancellation);var committed=reply.RootElement;BrowserEnvelope.Fields(committed,"phase","operationId");
        if(committed.GetProperty("phase").GetString()!="Committed"||BrowserEnvelope.Id(committed,"operationId")!=grant.OperationId)throw new BrowserException(BrowserFailure.Stale);
    }
    public ValueTask CancelAsync(BrowserGrant grant,CancellationToken cancellation)=>Send(new{phase="CancelImage",grant},cancellation);
    private async ValueTask Send<T>(T data,CancellationToken cancellation)
    {
        byte[] payload=JsonSerializer.SerializeToUtf8Bytes(data);
        try{await IpcFrameCodec.WriteAsync(pipe,new(IpcMessageType.SubmitImage,connection,payload),cancellation);await pipe.FlushAsync(cancellation);}
        finally{CryptographicOperations.ZeroMemory(payload);}
    }
    private async ValueTask<JsonDocument> Reply(CancellationToken cancellation)
    {
        var f=await IpcFrameCodec.ReadAsync(pipe,cancellation)??throw new BrowserException(BrowserFailure.Disconnected);
        try
        {
            if(f.RequestId!=connection||f.Type!=IpcMessageType.SubmitImage)throw new BrowserException(BrowserFailure.DesktopUntrusted);
            // ParseValue owns a copy; Parse(ReadOnlyMemory) would still reference the payload cleared below.
            var reader=new Utf8JsonReader(f.Payload,new JsonReaderOptions{MaxDepth=8});var doc=JsonDocument.ParseValue(ref reader);
            if(reader.Read()){doc.Dispose();throw new BrowserException(BrowserFailure.DesktopUntrusted);}return doc;
        }
        finally{CryptographicOperations.ZeroMemory(f.Payload);}
    }
    public ValueTask DisposeAsync()=>pipe.DisposeAsync();
}
