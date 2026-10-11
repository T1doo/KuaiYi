using System.Buffers.Binary;
using System.Security.Cryptography;
using KuaiYi.Contracts;
using KuaiYi.Core;
namespace KuaiYi.Imaging;
public enum CaptureBackend { WindowsGraphicsCapture, GdiCompatibility }
public enum CaptureFailure { None, UnsupportedPlatform, HiddenWindowNotVerified, ParentRoleRejected, CaptureUnavailable, TopologyChanged, ProtectedOrBlackFrame, Timeout, ProtocolFailure, ExitUnconfirmed }
public sealed class CaptureException(CaptureFailure failure):Exception(failure.ToString()){public CaptureFailure Failure {get;}=failure;}
public sealed record CaptureJob(Guid OperationId,long Epoch,int AppProcessId,long[] HiddenWindows,CaptureBackend Backend);
public sealed record CaptureTile(string Id,PixelRect Bounds,double Dpi,int ByteLength,string Sha256);
public sealed record CaptureReply(CaptureJob Job,CaptureFailure Failure,CaptureTile[] Tiles);
public static class CaptureProtocol
{
    public static void Validate(CaptureJob job)
    {
        if(job.OperationId==Guid.Empty||job.Epoch<=0||job.AppProcessId<=0||job.HiddenWindows is not {Length:>=1 and <=16}||job.HiddenWindows.Any(h=>h<=0)||job.HiddenWindows.Distinct().Count()!=job.HiddenWindows.Length||!Enum.IsDefined(job.Backend))throw new CaptureException(CaptureFailure.ProtocolFailure);
    }
    public static bool SameJob(CaptureJob a,CaptureJob b)=>a.OperationId==b.OperationId&&a.Epoch==b.Epoch&&a.AppProcessId==b.AppProcessId&&a.Backend==b.Backend&&a.HiddenWindows.SequenceEqual(b.HiddenWindows);
    public static ScreenshotTopology ValidateReply(CaptureReply reply,CaptureJob job)
    {
        if(reply.Job==null||reply.Job.HiddenWindows==null||!SameJob(reply.Job,job)||!Enum.IsDefined(reply.Failure)||reply.Tiles==null||reply.Tiles.Any(t=>t==null))throw new CaptureException(CaptureFailure.ProtocolFailure);
        if(reply.Failure!=CaptureFailure.None){if(reply.Tiles.Length!=0)throw new CaptureException(CaptureFailure.ProtocolFailure);throw new CaptureException(reply.Failure);}
        var topology=new ScreenshotTopology(reply.Tiles.Select(t=>new ScreenshotMonitor(t.Id,t.Bounds,new DpiScale(t.Dpi))));
        foreach(var tile in reply.Tiles)if(tile.ByteLength!=checked(tile.Bounds.Width*tile.Bounds.Height*4)||tile.Sha256 is not {Length:64}||!tile.Sha256.All(char.IsAsciiHexDigit))throw new CaptureException(CaptureFailure.ProtocolFailure);
        return topology;
    }
}
/// <summary>Actual private CaptureHost loop, shared by Windows entry and clearly synthetic process fixtures.</summary>
public static class CaptureHostSession
{
    public static async Task<int> RunAsync(Stream input,Stream output,Func<CaptureJob,CancellationToken,Task<ScreenshotSnapshot>> capture,CancellationToken cancellation=default)
    {
        Guid operation=Guid.Empty;CaptureJob? job=null;ScreenshotSnapshot? snapshot=null;
        try
        {
            var hello=await IpcFrameCodec.ReadAsync(input,cancellation)??throw new EndOfStreamException();
            try
            {
                if(hello.Type!=IpcMessageType.CaptureHello||hello.Payload.Length!=32)throw new CaptureException(CaptureFailure.ProtocolFailure);operation=hello.RequestId;
                await IpcFrameCodec.WriteAsync(output,hello,cancellation);await output.FlushAsync(cancellation);
            }finally{CryptographicOperations.ZeroMemory(hello.Payload);}
            var begin=await IpcFrameCodec.ReadAsync(input,cancellation)??throw new EndOfStreamException();
            try{if(begin.Type!=IpcMessageType.CaptureBegin||begin.RequestId!=operation)throw new CaptureException(CaptureFailure.ProtocolFailure);job=ImageJson.Read<CaptureJob>(begin.Payload);CaptureProtocol.Validate(job);if(job.OperationId!=operation)throw new CaptureException(CaptureFailure.ProtocolFailure);}
            finally{CryptographicOperations.ZeroMemory(begin.Payload);}
            snapshot=await capture(job,cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
            var tiles=snapshot.Topology.Monitors.Select(m=>{var tile=snapshot.Tiles.Single(t=>t.Bounds==m.Bounds);return new CaptureTile(m.Id,m.Bounds,m.Dpi.Factor,tile.Bytes.Length,Convert.ToHexString(SHA256.HashData(tile.Bytes.Span)));}).ToArray();
            await Send(new(job,CaptureFailure.None,tiles));
            for(int tileIndex=0;tileIndex<tiles.Length;tileIndex++)
            {
                var data=snapshot.Tiles.Single(t=>t.Bounds==tiles[tileIndex].Bounds).Bytes;
                int index=0;for(int offset=0;offset<data.Length;)
                {
                    int count=Math.Min(IpcFrameCodec.BinaryLimit-8,data.Length-offset);byte[] chunk=new byte[count+8];
                    try{BinaryPrimitives.WriteInt32LittleEndian(chunk,tileIndex);BinaryPrimitives.WriteInt32LittleEndian(chunk.AsSpan(4),index++);data.Span.Slice(offset,count).CopyTo(chunk.AsSpan(8));await IpcFrameCodec.WriteAsync(output,new(IpcMessageType.BinaryChunk,operation,chunk),cancellation);}
                    finally{CryptographicOperations.ZeroMemory(chunk);}offset+=count;
                }
            }
            await IpcFrameCodec.WriteAsync(output,new(IpcMessageType.CaptureCommit,operation,[]),cancellation);await output.FlushAsync(cancellation);snapshot.Dispose();snapshot=null;
            // Parent closes inherited stdin after receipt; unexpected further data rejects rather than recapturing.
            var end=await IpcFrameCodec.ReadAsync(input,cancellation);if(end!=null){CryptographicOperations.ZeroMemory(end.Payload);throw new CaptureException(CaptureFailure.ProtocolFailure);}return 0;
        }
        catch(Exception error)
        {
            if(job!=null)try{await Send(new(job,error is CaptureException c?c.Failure:CaptureFailure.CaptureUnavailable,[]));}catch(Exception e)when(e is IOException or OperationCanceledException or ObjectDisposedException){Console.Error.WriteLine("CaptureHost reply unavailable.");}
            return 3;
        }
        finally{snapshot?.Dispose();}
        async Task Send(CaptureReply reply){byte[] data=ImageJson.Serialize(reply);try{await IpcFrameCodec.WriteAsync(output,new(IpcMessageType.CaptureResult,operation,data),cancellation);await output.FlushAsync(cancellation);}finally{CryptographicOperations.ZeroMemory(data);}}
    }
}
