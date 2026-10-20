using System.Buffers.Binary;
using System.Security.Cryptography;
using KuaiYi.Contracts;
using KuaiYi.Imaging;
namespace KuaiYi.ImageWorker;

internal static class ImageDecodeSession
{
    internal static async Task RunAsync(Stream input,Stream output,Guid id,ReadOnlyMemory<byte> encoded,ImageDecodeJob job,Func<ReadOnlyMemory<byte>,ImageDecodeJob,CancellationToken,DecodedRaster> decode,Action<string>? log)
    {
        using var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var operation=Task.Run(()=>decode(encoded,job,cancellation.Token));var control=ReadControl();DecodedRaster? raster=null;Exception? failure=null;bool operationObserved=false;
        try
        {
            if(await Task.WhenAny(operation,control)==control)await control;
            ImageDecodeReply reply;
            try{raster=await operation;cancellation.Token.ThrowIfCancellationRequested();reply=raster.Metadata;if(reply.Job!=job)throw new InvalidDataException("Decoded job mismatch.");}
            catch(OperationCanceledException){reply=new(job,ImageFailure.Cancelled);}
            catch(ImageJobException error){reply=new(job,error.Code);}
            catch(Exception error){log?.Invoke(error.GetType().Name);reply=new(job,ImageFailure.NativeFailure);}
            finally{operationObserved=true;}
            ImageDecodePolicy.ValidateReply(reply);
            await Send(new(IpcMessageType.ImageDecodeResult,id,ImageJson.Serialize(reply)));
            if(reply.Failure==ImageFailure.None)
            {
                int index=0;var bytes=raster!.Bytes;
                for(int offset=0;offset<bytes.Length;)
                {
                    cancellation.Token.ThrowIfCancellationRequested();int count=Math.Min(IpcFrameCodec.BinaryLimit-4,bytes.Length-offset);byte[] chunk=new byte[count+4];
                    BinaryPrimitives.WriteUInt32LittleEndian(chunk,(uint)index++);bytes.Slice(offset,count).CopyTo(chunk.AsMemory(4));await Send(new(IpcMessageType.BinaryChunk,id,chunk));offset+=count;
                }
            }
            await Send(new(IpcMessageType.ImageDecodeCommit,id,[]));await output.FlushAsync();await control;
        }
        catch(Exception error){failure=error;}
        finally
        {
            List<Exception> errors=[];if(failure!=null)errors.Add(failure);
            try{await cancellation.CancelAsync();}catch(Exception error){errors.Add(error);}
            try{var late=await operation;raster??=late;}catch(Exception error){if(!operationObserved&&error is not OperationCanceledException)errors.Add(error);}
            try{raster?.Dispose();}catch(Exception error){errors.Add(error);}
            try{await control;}catch(Exception error){if(!ReferenceEquals(error,failure))errors.Add(error);}
            if(errors.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if(errors.Count>1)throw new AggregateException("Decode cleanup failed.",errors);
        }
        async Task Send(IpcFrame frame)
        {try{await IpcFrameCodec.WriteAsync(output,frame);}finally{CryptographicOperations.ZeroMemory(frame.Payload);}}
        async Task ReadControl()
        {
            IpcFrame? frame=null;
            try{frame=await IpcFrameCodec.ReadAsync(input);if(frame!=null&&(frame.Type!=IpcMessageType.ImageCancel||frame.RequestId!=id||frame.Payload.Length!=0))throw new InvalidDataException("Invalid decode control.");}
            finally{try{if(frame!=null)CryptographicOperations.ZeroMemory(frame.Payload);}finally{await cancellation.CancelAsync();}}
        }
    }
}
