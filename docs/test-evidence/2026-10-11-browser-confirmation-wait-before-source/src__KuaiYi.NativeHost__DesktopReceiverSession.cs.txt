using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using KuaiYi.Contracts;
using KuaiYi.Core;
using KuaiYi.Imaging;
namespace KuaiYi.NativeHost;

internal sealed record VerifiedNativeClient(int ProcessId,int SessionId,string UserSid,string Path,long StartTime);
/// <summary>Internal IPC receiving role only. OS verification must precede construction in production.
/// One offer, explicit decision, one grant and one commit per connection. No endpoint consent.</summary>
internal sealed class DesktopReceiverSession(RuntimeGate gate,ILocalImageConfirmation confirmation,LocalImageInbox inbox,
    IReadOnlyList<string> origins,VerifiedNativeClient peer,Func<bool> verifyPeer,TimeProvider? timeProvider=null)
{
    private readonly TimeProvider time=timeProvider??TimeProvider.System;
    private readonly Guid session=Guid.NewGuid();
    internal string? FailureCode {get;private set;}
    internal async Task<bool> RunAsync(Stream pipe,CancellationToken cancellation=default)
    {
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30),time);
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(cancellation,deadline.Token);
        using var assembler=new ImageTransferAssembler(time);
        long connected=time.GetTimestamp();
        CancellationTokenSource? requestLink=null;BrowserGrant? grant=null;Guid connection=Guid.Empty;
        Task<IpcFrame?>? pending=null;bool begun=false,committed=false;ImageSize size=default;
        long issued=0;
        try
        {
            while(true)
            {
                var token=requestLink?.Token??lifetime.Token;
                pending??=IpcFrameCodec.ReadAsync(pipe,token).AsTask();
                var frame=await pending.WaitAsync(token).ConfigureAwait(false);pending=null;
                if(frame==null)throw new EndOfStreamException();
                try
                {
                    token.ThrowIfCancellationRequested();
                    if(connection==Guid.Empty)connection=frame.RequestId;
                    if(frame.RequestId!=connection)throw new InvalidDataException("Connection changed.");
                    if(frame.Type==IpcMessageType.BinaryChunk)
                    {
                        CheckGrant();if(!begun||frame.Payload.Length<=4)throw new InvalidDataException("Begin required.");
                        assembler.Chunk(grant!.OperationId,BinaryPrimitives.ReadInt32LittleEndian(frame.Payload),frame.Payload.AsSpan(4));continue;
                    }
                    if(frame.Type!=IpcMessageType.SubmitImage)throw new InvalidDataException("Unsupported receiving role.");
                    using var doc=JsonDocument.Parse(frame.Payload,new JsonDocumentOptions{MaxDepth=8});var root=doc.RootElement;
                    string? phase=root.GetProperty("phase").GetString();
                    if(phase=="OfferImage")
                    {
                        if(!verifyPeer())throw new BrowserException(BrowserFailure.DesktopUntrusted);
                        BrowserEnvelope.Fields(root,"phase","operationId","origin");
                        if(grant!=null)throw new InvalidDataException("Offer already consumed.");
                        var origin=root.GetProperty("origin").GetString();
                        if(!InstalledHostPolicy.IsOrigin(origin)||!origins.Contains(origin,StringComparer.Ordinal))throw new BrowserException(BrowserFailure.OriginRejected);
                        var snapshot=gate.Snapshot;if(snapshot.Suspension!=Suspension.None)throw new BrowserException(BrowserFailure.ActivationRequired);
                        Guid operation=BrowserEnvelope.Id(root,"operationId");
                        // Monitor the connection while the desktop decision is pending. EOF or early input revokes the prompt.
                        pending=IpcFrameCodec.ReadAsync(pipe,lifetime.Token).AsTask();
                        var decision=confirmation.ConfirmAsync(new(connection,operation,origin!,peer.ProcessId),lifetime.Token);
                        bool observedDecision=false;
                        try
                        {
                            if(await Task.WhenAny(decision,pending).ConfigureAwait(false)!=decision||pending.IsCompleted)
                                throw new BrowserException(BrowserFailure.Disconnected);
                            bool accepted=await decision.ConfigureAwait(false);observedDecision=true;
                            if(!accepted)throw new BrowserException(BrowserFailure.ActivationRequired);
                        }
                        finally
                        {
                            if(!observedDecision){lifetime.Cancel();_ = ObserveDecision(decision);}
                        }
                        lifetime.Token.ThrowIfCancellationRequested();
                        if(time.GetElapsedTime(connected)>=TimeSpan.FromSeconds(30))throw new BrowserException(BrowserFailure.Timeout);
                        if(!gate.TryStartConfirmedLocalRequest(snapshot.Epoch,session,Guid.NewGuid(),out var identity,out _))throw new BrowserException(BrowserFailure.Stale);
                        grant=new(operation,identity,Guid.NewGuid());issued=time.GetTimestamp();
                        if(!gate.TryLinkRequestCancellation(identity,lifetime.Token,out requestLink))throw new BrowserException(BrowserFailure.Stale);
                        CheckGrant();await Reply(new{phase="Ready",operationId=operation,identity,nonce=grant.Nonce});
                    }
                    else if(phase=="BeginImage")
                    {
                        if(!verifyPeer())throw new BrowserException(BrowserFailure.DesktopUntrusted);
                        BrowserEnvelope.Fields(root,"phase","grant","totalLength","mime","width","height","sha256");CheckGrant(root);
                        if(begun)throw new InvalidDataException("Repeated Begin.");
                        size=new(root.GetProperty("width").GetUInt32(),root.GetProperty("height").GetUInt32());ImagePolicy.ValidateDimensions(size.Width,size.Height);
                        if(root.GetProperty("mime").GetString()!="image/png")throw new InvalidDataException("PNG required.");
                        assembler.Begin(grant!.OperationId,root.GetProperty("totalLength").GetInt32(),"image/png",root.GetProperty("sha256").GetString()??"");begun=true;
                        await Reply(new{phase="Begun",operationId=grant.OperationId});
                    }
                    else if(phase=="CommitImage")
                    {
                        if(!verifyPeer())throw new BrowserException(BrowserFailure.DesktopUntrusted);
                        BrowserEnvelope.Fields(root,"phase","grant");CheckGrant(root);
                        if(!begun)throw new InvalidDataException("Begin required.");
                        using var image=assembler.Commit(grant!.OperationId);
                        if(ImagePolicy.InspectPng(image.Bytes.Span)!=size)throw new InvalidDataException("Dimensions changed.");
                        // Monitor EOF while a destination-thread application is queued.
                        pending=IpcFrameCodec.ReadAsync(pipe,requestLink!.Token).AsTask();
                        var publish=inbox.PublishAsync(grant.Identity,image,size,requestLink.Token,
                            ()=>time.GetElapsedTime(connected)<TimeSpan.FromSeconds(30)&&time.GetElapsedTime(issued)<TimeSpan.FromSeconds(30));
                        if(await Task.WhenAny(publish,pending).ConfigureAwait(false)!=publish||pending.IsCompleted)
                        {
                            lifetime.Cancel();await publish.ConfigureAwait(false);throw new EndOfStreamException();
                        }
                        if(!await publish.ConfigureAwait(false))throw new BrowserException(BrowserFailure.Stale);
                        CheckGrant();await Reply(new{phase="Committed",operationId=grant.OperationId});committed=true;return true;
                    }
                    else if(phase=="CancelImage")
                    {
                        BrowserEnvelope.Fields(root,"phase","grant");CheckGrant(root);assembler.Dispose();return false;
                    }
                    else throw new InvalidDataException("Unsupported phase.");
                }
                finally{CryptographicOperations.ZeroMemory(frame.Payload);}
            }
        }
        catch(Exception error)
        {
            FailureCode=error switch{BrowserException b=>b.Code.ToString(),OperationCanceledException=>deadline.IsCancellationRequested?"Timeout":"Cancelled",IOException=>"Disconnected",InvalidDataException or JsonException or InvalidOperationException or FormatException or ImageJobException=>"InvalidMessage",_=>"InternalFailure"};
            // No extra read, raw errors or content echo; closing the connection is the rejection signal.
            return false;
        }
        finally
        {
            lifetime.Cancel();assembler.Dispose();pipe.Dispose(); // Owning receiver closes real async pipe to release any outstanding read.
            if(pending!=null)await ObserveFrame(pending).ConfigureAwait(false);
            if(grant!=null&&!committed){inbox.Clear(grant.Identity);gate.StopRequest(grant.Identity);}
            requestLink?.Dispose();
        }
        void CheckGrant(JsonElement? root=null)
        {
            lifetime.Token.ThrowIfCancellationRequested();requestLink?.Token.ThrowIfCancellationRequested();
            if(time.GetElapsedTime(connected)>=TimeSpan.FromSeconds(30))throw new BrowserException(BrowserFailure.Timeout);
            if(grant==null||time.GetElapsedTime(issued)>=TimeSpan.FromSeconds(30)||!gate.CanApply(grant.Identity)||
                (root.HasValue&&ReadGrant(root.Value.GetProperty("grant"))!=grant))throw new BrowserException(BrowserFailure.Stale);
        }
        async ValueTask Reply<T>(T value)
        {
            byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value);
            try{await IpcFrameCodec.WriteAsync(pipe,new(IpcMessageType.SubmitImage,connection,bytes),requestLink?.Token??lifetime.Token);await pipe.FlushAsync(requestLink?.Token??lifetime.Token);}
            finally{CryptographicOperations.ZeroMemory(bytes);}
        }
    }
    private static BrowserGrant ReadGrant(JsonElement root)
    {
        BrowserEnvelope.Fields(root,"OperationId","Identity","Nonce");var identity=root.GetProperty("Identity");
        BrowserEnvelope.Fields(identity,"SessionId","VersionId","RequestId","Epoch");
        return new(BrowserEnvelope.Id(root,"OperationId"),new(BrowserEnvelope.Id(identity,"SessionId"),BrowserEnvelope.Id(identity,"VersionId"),BrowserEnvelope.Id(identity,"RequestId"),identity.GetProperty("Epoch").GetInt64()),BrowserEnvelope.Id(root,"Nonce"));
    }
    private async Task ObserveFrame(Task<IpcFrame?> pending)
    {try{var frame=await pending.ConfigureAwait(false);if(frame!=null)CryptographicOperations.ZeroMemory(frame.Payload);}catch(Exception e){FailureCode??=e.GetType().Name;}}
    private async Task ObserveDecision(Task<bool> pending)
    {try{await pending.ConfigureAwait(false);}catch(Exception e){FailureCode??=e.GetType().Name;}}
}
