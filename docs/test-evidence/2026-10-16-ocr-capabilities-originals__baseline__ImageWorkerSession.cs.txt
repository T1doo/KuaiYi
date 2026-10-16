using System.Security.Cryptography;
using KuaiYi.Contracts;
using KuaiYi.Imaging;
namespace KuaiYi.ImageWorker;
/// <summary>Actual private worker loop. The operation seam permits synthetic protocol/ownership tests,
/// never substitutes for native OCR/PNG/JPEG acceptance.</summary>
internal static class ImageWorkerSession
{
    internal static async Task RunAsync(Stream input,Stream output,Func<ReadOnlyMemory<byte>,string,CancellationToken,OcrOutput> recognizeOcr,Action<string>? diagnostic=null)
    {
        using var receiver=new ImageJobReceiver();
        var hello=await IpcFrameCodec.ReadAsync(input)??throw new EndOfStreamException();Guid id=hello.RequestId;
        try
        {
            if(hello.Type!=IpcMessageType.ImageHello||hello.Payload.Length!=32)throw new InvalidDataException("Private hello required.");
            await IpcFrameCodec.WriteAsync(output,new(IpcMessageType.ImageHello,id,hello.Payload));await output.FlushAsync();
        }
        finally{CryptographicOperations.ZeroMemory(hello.Payload);}
        var begin=await IpcFrameCodec.ReadAsync(input)??throw new EndOfStreamException();
        try{if(begin.RequestId!=id)throw new InvalidDataException("Begin/hello identity mismatch.");receiver.Begin(begin);}
        finally{CryptographicOperations.ZeroMemory(begin.Payload);}
        ReadOnlyMemory<byte> image;
        while(true)
        {
            var frame=await IpcFrameCodec.ReadAsync(input)??throw new EndOfStreamException();
            try{if(frame.Type==IpcMessageType.ImageCommit){image=receiver.Commit(frame);break;}receiver.Chunk(frame);}
            finally{CryptographicOperations.ZeroMemory(frame.Payload);}
        }
        using var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var recognize=Task.Run(()=>recognizeOcr(image,receiver.Job!.Language,cancellation.Token));
        // Always validate/clear this original read, including when recognition or output fails first.
        var disconnect=Task.Run(ReadControl);
        try
        {
            if(await Task.WhenAny(recognize,disconnect)==disconnect){await disconnect;cancellation.Cancel();}
            ImageReply reply;
            try
            {
                var result=await recognize;cancellation.Token.ThrowIfCancellationRequested();
                reply=new(receiver.Job!,ImageFailure.None,result);
            }
            catch(OperationCanceledException){reply=new(receiver.Job!,ImageFailure.Cancelled,null);}
            catch(ImageJobException error){reply=new(receiver.Job!,error.Code,null);}
            catch(Exception error){diagnostic?.Invoke(error.GetType().Name);reply=new(receiver.Job!,ImageFailure.NativeFailure,null);}
            byte[] response=ImageJson.Serialize(reply);
            try
            {
                if(response.Length>ImagePolicy.ResultLimit)throw new ImageJobException(ImageFailure.ImageTooLarge);
                await IpcFrameCodec.WriteAsync(output,new(IpcMessageType.BinaryChunk,id,response));await output.FlushAsync();
            }
            finally{CryptographicOperations.ZeroMemory(response);}
            // Parent closes stdin after reply. Keep this ordering to avoid a parent/child wait cycle.
            await disconnect;
        }
        finally
        {
            // A cancellation callback/control/write fault cannot bypass either original task's lease.
            // Native work which will not settle is stopped by the parent's existing owned-process gate.
            try{cancellation.Cancel();}
            finally{try{await Observe(recognize);}finally{await Observe(disconnect);}}
        }
        async Task ReadControl()
        {
            IpcFrame? frame=null;
            try
            {
                frame=await IpcFrameCodec.ReadAsync(input);
                if(frame!=null&&(frame.Type!=IpcMessageType.ImageCancel||frame.RequestId!=id||frame.Payload.Length!=0))throw new InvalidDataException("Invalid cancel/control.");
            }
            finally{try{if(frame!=null)CryptographicOperations.ZeroMemory(frame.Payload);}finally{cancellation.Cancel();}}
        }
        async Task Observe(Task original)
        {
            try{await original.ConfigureAwait(false);}
            catch(Exception error){diagnostic?.Invoke("settlement:"+error.GetType().Name);} // Bounded type only; preserve original-task lease through settlement.
        }
    }
}
