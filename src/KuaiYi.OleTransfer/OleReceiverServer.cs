using System.Security.Cryptography;
using System.Threading.Channels;
using KuaiYi.Contracts;
namespace KuaiYi.OleTransfer;
public interface IOleReceiverBackend
{
    byte[] ExportPacket{get;}
    void SetEnabled(long epoch,bool enabled);
    Task ShutdownAsync();
}
/// <summary>Actual one-owned-channel loop. Synthetic fixtures supply only the backend; protocol/writer/controls are production.</summary>
public interface IOleReceiverControlBackend{void ApplyControl(OleReceiverControl control);}
public static class OleReceiverServer
{
    public static async Task<int> RunAsync(Stream input,Stream output,Func<OleReceiverHello,Action<IpcFrame>,Task<IOleReceiverBackend>> create,CancellationToken cancellation=default)
    {
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var queue=Channel.CreateBounded<IpcFrame>(new BoundedChannelOptions(64){SingleReader=true,FullMode=BoundedChannelFullMode.Wait});
        Task? writing=null;IOleReceiverBackend? backend=null;OleReceiverHello? hello=null;
        try
        {
            var frame=await IpcFrameCodec.ReadAsync(input,lifetime.Token)??throw new EndOfStreamException();
            try{if(frame.Type!=IpcMessageType.OleHello)throw new InvalidDataException();hello=OleReceiverWire.Read<OleReceiverHello>(frame.Payload);OleReceiverWire.Validate(hello);if(frame.RequestId!=hello.Instance)throw new InvalidDataException();}
            finally{CryptographicOperations.ZeroMemory(frame.Payload);}
            writing=WriteLoop();Send(new(IpcMessageType.OleHello,hello.Instance,hello.Nonce.ToArray()));CryptographicOperations.ZeroMemory(hello.Nonce);
            backend=await create(hello,Send).ConfigureAwait(false);OleReceiverWire.ValidateExport(backend.ExportPacket);Send(new(IpcMessageType.OleExport,hello.Instance,backend.ExportPacket.ToArray()));backend.SetEnabled(hello.Epoch,false);
            while(await IpcFrameCodec.ReadAsync(input,lifetime.Token) is {} control)
            {
                try
                {
                    if(control.Type!=IpcMessageType.OleControl||control.RequestId!=hello.Instance)throw new InvalidDataException();var command=OleReceiverWire.Read<OleReceiverControl>(control.Payload);
                    if(command.Instance!=hello.Instance||command.Epoch<0)throw new InvalidDataException();if(backend is IOleReceiverControlBackend controlled)controlled.ApplyControl(command);else backend.SetEnabled(command.Epoch,command.Enabled);
                }finally{CryptographicOperations.ZeroMemory(control.Payload);}
            }
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine("OleReceiver failure: "+error.GetType().Name);return 3;}
        finally
        {
            lifetime.Cancel();queue.Writer.TryComplete();if(hello!=null)CryptographicOperations.ZeroMemory(hello.Nonce);
            if(writing!=null)try{await writing.ConfigureAwait(false);}catch(Exception e)when(e is IOException or OperationCanceledException or ObjectDisposedException){/* Finite pipe failure, process returns failure or owned shutdown. */}
            while(queue.Reader.TryRead(out var abandoned))CryptographicOperations.ZeroMemory(abandoned.Payload);
            if(backend!=null)await backend.ShutdownAsync().ConfigureAwait(false); // Native cleanup stays in child; parent supervises and can terminate it.
        }
        void Send(IpcFrame frame)
        {
            if(!queue.Writer.TryWrite(frame)){CryptographicOperations.ZeroMemory(frame.Payload);lifetime.Cancel();throw new IOException("Owned OLE output queue full or closed.");}
        }
        async Task WriteLoop()
        {
            try{await foreach(var frame in queue.Reader.ReadAllAsync(lifetime.Token))try{await IpcFrameCodec.WriteAsync(output,frame,lifetime.Token);await output.FlushAsync(lifetime.Token);}finally{CryptographicOperations.ZeroMemory(frame.Payload);}}
            finally{lifetime.Cancel();}
        }
    }
}
