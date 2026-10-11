using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using KuaiYi.Contracts;
using KuaiYi.Core;
using KuaiYi.Windows;
namespace KuaiYi.ClipboardHost;
internal static class Program
{
    [STAThread]public static int Main(string[] args)
    {
        if(args.Length!=5||!long.TryParse(args[0],out long hwnd)||!uint.TryParse(args[1],out uint pid)||!long.TryParse(args[2],out long started)||!long.TryParse(args[3],out long focus)||!Guid.TryParse(args[4],out var id)||id==Guid.Empty||hwnd==0||pid==0||started<=0||focus==0)return 2;
        var source=SelectionDesktop.Resolve(hwnd);if(source==null||source.ProcessId!=pid||source.ProcessStartUtcTicks!=started)return 3;
        var cancellation=new CancellationTokenSource();Task? watcher=null;using Stream input=Console.OpenStandardInput();using Stream output=Console.OpenStandardOutput();bool ready=false;
        void Write(IpcMessageType type,byte[] bytes){try{IpcFrameCodec.WriteAsync(output,new(type,id,bytes)).AsTask().GetAwaiter().GetResult();}finally{CryptographicOperations.ZeroMemory(bytes);}}
        ClipboardCopyStart? Issue(uint sequence)
        {
            ready=true;Write(IpcMessageType.Ping,JsonSerializer.SerializeToUtf8Bytes(new ClipboardStageMetadata("Ready",sequence,started)));
            var frame=IpcFrameCodec.ReadAsync(input).AsTask().GetAwaiter().GetResult();try
            {
                if(frame?.Type!=IpcMessageType.Ping||frame.RequestId!=id)return null;
                var command=JsonSerializer.Deserialize<ClipboardStartMetadata>(frame.Payload);if(command?.Status!="Sent"||command.Timestamp<=0||command.InputTime==0)return null;
                watcher=Task.Run(()=>{try{input.ReadByte();}catch(Exception error)when(error is IOException or ObjectDisposedException){ /* Parent stream closure revokes the transaction. */ }finally{cancellation.Cancel();}}); // EOF or unexpected control revokes; STA retains restoration ownership.
                return new(command.Timestamp,command.InputTime);
            }
            finally{if(frame!=null)CryptographicOperations.ZeroMemory(frame.Payload);}
        }
        ClipboardCapture? result=null;
        try{using var port=new NativeClipboardPort(source,focus);result=ClipboardTransaction.Run(port,Issue,cancellation.Token);}
        catch(Exception error)when(error is IOException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception){result?.Dispose();result=new(ClipboardCaptureStatus.Failure,recovery:ClipboardRecovery.Unconfirmed);}
        result??=new(ClipboardCaptureStatus.Failure,recovery:ClipboardRecovery.Unconfirmed);
        using(result)
        {
            if(!ready)Write(IpcMessageType.Ping,JsonSerializer.SerializeToUtf8Bytes(new ClipboardStageMetadata("Finished")));
            bool revoked=cancellation.IsCancellationRequested;Write(IpcMessageType.Ping,JsonSerializer.SerializeToUtf8Bytes(new ClipboardResultMetadata((revoked?ClipboardCaptureStatus.Cancelled:result.Status).ToString(),result.Recovery.ToString(),result.ObservedMs)));
            Write(IpcMessageType.BinaryChunk,revoked?[]:(byte[])result.Utf8.Clone());
        }
        if(watcher==null)cancellation.Dispose();else _=watcher.ContinueWith(task=>{_ =task.Exception;cancellation.Dispose();},TaskScheduler.Default); // Watcher owns late completion; no disposed CTS is accessed.
        return 0;
    }
}
