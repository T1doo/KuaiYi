using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using KuaiYi.Contracts;
using KuaiYi.Core;
namespace KuaiYi.Imaging;

/// <summary>Owned one-shot process. Private inherited stdio is a P0 transport, not the production named-pipe ACL endpoint.</summary>
public sealed class ImageWorkerClient(string executable, string? managedAssembly = null, IImageProcessStarter? processStarter = null) : ILocalOcrCapabilitiesWorker
{
    private readonly OwnedProcessSession process=new(processStarter);
    private int started;
    private readonly object ioSync=new();
    private readonly List<Task> ioSettlements=[];
    public string? IoFailureCode {get;private set;}
    private bool IoSettled {get{lock(ioSync)return ioSettlements.All(task=>task.IsCompleted);}}
    private void Track(Task task){lock(ioSync)ioSettlements.Add(task);}
    public string? StartupFailureCode=>process.StartupFailureCode;
    public string? FinalizationFailureCode=>process.FinalizationFailureCode;
    internal Action<Process>? OwnershipPublishedForTest {init{process.OwnershipPublishedForTest=value;}}
    internal Task StartupSettlementForTest=>process.StartupSettlement;
    public HostExitState? ExitState=>process.ExitState;
    public bool ObserveExit()=>process.ObserveExit();
    internal Func<Stream,Stream>? ReadStreamForTest {get;init;}
    internal Func<Stream,Stream>? WriteStreamForTest {get;init;}
    public Task<ImageReply> RunAsync(ReadOnlyMemory<byte> image,ImageJob job,CancellationToken cancellation=default)
        =>RunOnceAsync(()=>RunOwnedAsync(image,job,cancellation));
    public Task<OcrDiagnosticReply> DiagnoseAsync(OcrDiagnosticJob job,CancellationToken cancellation=default)
        =>RunOnceAsync(async()=>
        {
            cancellation.ThrowIfCancellationRequested();OcrCapabilitiesPolicy.ValidateJob(job);
            return await UseChannelAsync(async(send,receive,id,token)=>
            {
                await Write(send,new(IpcMessageType.ImageDiagnose,id,ImageJson.Serialize(job)),token,true);
                var frame=await Read(receive,token);
                try
                {
                    if(frame.Type!=IpcMessageType.ImageDiagnostic||frame.RequestId!=id)throw new InvalidDataException("Worker diagnostic identity/type mismatch.");
                    var reply=ImageJson.Read<OcrDiagnosticReply>(frame.Payload);
                    if(reply.Job!=job||reply.Diagnostic==null)throw new InvalidDataException("Worker diagnostic job mismatch.");
                    OcrCapabilitiesPolicy.ValidateDiagnostic(reply.Diagnostic);return reply;
                }
                finally{CryptographicOperations.ZeroMemory(frame.Payload);}
            },cancellation).ConfigureAwait(false);
        });
    private async Task<T> RunOnceAsync<T>(Func<Task<T>> run)
    {
        if(Interlocked.CompareExchange(ref started,1,0)!=0)throw new InvalidOperationException("Image client is single use.");
        try{return await run().ConfigureAwait(false);}finally{Volatile.Write(ref started,2);}
    }
    private async Task<ImageReply> RunOwnedAsync(ReadOnlyMemory<byte> image,ImageJob job,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();ImagePolicy.InspectPng(image.Span);
        if(image.Length!=job.ByteLength)throw new InvalidDataException("Image length mismatch.");
        return await UseChannelAsync(async(send,receive,id,token)=>
        {
            await Write(send,new(IpcMessageType.ImageBegin,id,ImageJson.Serialize(job)),token);
            int index=0;
            for(int offset=0;offset<image.Length;)
            {
                int size=Math.Min(IpcFrameCodec.BinaryLimit-4,image.Length-offset);byte[] chunk=new byte[size+4];
                BinaryPrimitives.WriteUInt32LittleEndian(chunk,(uint)index++);image.Span.Slice(offset,size).CopyTo(chunk.AsSpan(4));
                await Write(send,new(IpcMessageType.BinaryChunk,id,chunk),token);offset+=size;
            }
            await Write(send,new(IpcMessageType.ImageCommit,id,[]),token,true);var frame=await Read(receive,token);
            try
            {
                if(frame.Type!=IpcMessageType.BinaryChunk||frame.RequestId!=id)throw new InvalidDataException("Worker reply mismatch.");
                var reply=ImageJson.Read<ImageReply>(frame.Payload);
                if(reply.Job!=job||!Enum.IsDefined(reply.Failure)||(reply.Failure==ImageFailure.None)!=(reply.Output!=null))throw new InvalidDataException("Worker result identity/status mismatch.");
                if(reply.Output is {} result)ValidateOutput(result,ImagePolicy.InspectPng(image.Span),job.Language);return reply;
            }
            finally{CryptographicOperations.ZeroMemory(frame.Payload);}
        },cancellation).ConfigureAwait(false);
    }
    private async Task<T> UseChannelAsync<T>(Func<Stream,Stream,Guid,CancellationToken,Task<T>> exchange,CancellationToken cancellation)
    {
        var info=OwnedProcessSession.Info(executable,managedAssembly);
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,deadline.Token);
        Task<Process> pending=process.Start(info,linked.Token);Guid id=Guid.NewGuid();
        try
        {
            Process child=await pending.WaitAsync(linked.Token).ConfigureAwait(false);linked.Token.ThrowIfCancellationRequested();
            var send=WriteStreamForTest?.Invoke(child.StandardInput.BaseStream)??child.StandardInput.BaseStream;
            var receive=ReadStreamForTest?.Invoke(child.StandardOutput.BaseStream)??child.StandardOutput.BaseStream;
            byte[] nonce=RandomNumberGenerator.GetBytes(32);
            try
            {
                await Write(send,new(IpcMessageType.ImageHello,id,nonce.ToArray()),linked.Token,true);
                var hello=await Read(receive,linked.Token);
                try{if(hello.Type!=IpcMessageType.ImageHello||hello.RequestId!=id||!CryptographicOperations.FixedTimeEquals(nonce,hello.Payload))throw new InvalidDataException("Worker hello mismatch.");}
                finally{CryptographicOperations.ZeroMemory(hello.Payload);}
            }
            finally{CryptographicOperations.ZeroMemory(nonce);}
            T result=await exchange(send,receive,id,linked.Token).ConfigureAwait(false);
            child.StandardInput.Close();await child.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            if(child.ExitCode!=0)throw new ImageJobException(ImageFailure.ProtocolFailure);
            if(!ObserveExit())throw new ImageJobException(ImageFailure.ExitUnconfirmed);
            cancellation.ThrowIfCancellationRequested();return result;
        }
        catch(OperationCanceledException)when(deadline.IsCancellationRequested&&!cancellation.IsCancellationRequested){throw new ImageJobException(ImageFailure.Timeout);}
        finally
        {
            await process.StopAsync().ConfigureAwait(false);
            if(!ObserveExit())throw new ImageJobException(ImageFailure.ExitUnconfirmed);
            if(!IoSettled)throw new ImageJobException(ImageFailure.SettlementUnconfirmed);
        }
    }
    private async Task<IpcFrame> Read(Stream stream,CancellationToken cancellation)
    {
        var reading=IpcFrameCodec.ReadAsync(stream,cancellation).AsTask();bool delivered=false;
        try
        {
            var frame=await reading.WaitAsync(cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
            delivered=true;return frame??throw new EndOfStreamException();
        }
        finally{if(!delivered)Track(ClearLateRead(reading));}
    }
    private async Task ClearLateRead(Task<IpcFrame?> reading)
    {
        try{var frame=await reading.ConfigureAwait(false);if(frame!=null)CryptographicOperations.ZeroMemory(frame.Payload);}
        catch(Exception error){IoFailureCode=error.GetType().Name;} // Observe every original fault; no late result can be delivered.
    }
    private async Task Write(Stream stream,IpcFrame frame,CancellationToken cancellation,bool flush=false)
    {
        Task writing=WriteOriginal();Task settlement=ClearAfterWrite();Track(settlement);
        try{await writing.WaitAsync(cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();}
        finally{if(writing.IsCompleted)await settlement.ConfigureAwait(false);}
        async Task WriteOriginal(){await IpcFrameCodec.WriteAsync(stream,frame,cancellation).ConfigureAwait(false);if(flush)await stream.FlushAsync(cancellation).ConfigureAwait(false);}
        async Task ClearAfterWrite()
        {
            try{await writing.ConfigureAwait(false);}catch(Exception error){IoFailureCode=error.GetType().Name;}
            finally{CryptographicOperations.ZeroMemory(frame.Payload);}
        }
    }
    public static void ValidateOutput(OcrOutput result, ImageSize expected, string language)
    {
        if (result.Width != expected.Width || result.Height != expected.Height || !string.Equals(result.Language, language, StringComparison.OrdinalIgnoreCase) ||
            result.Text == null || result.Text.Length > 50_000 || result.Lines == null || result.Lines.Length > 10_000) throw new InvalidDataException("Invalid OCR output.");
        int count = 0;
        foreach (var line in result.Lines)
        {
            if (line == null || line.Text == null || line.Text.Length > 50_000 || line.Words == null || line.Words.Length > 50_000 - count) throw new InvalidDataException("Invalid OCR line.");
            count += line.Words.Length;
            foreach (var word in line.Words)
                if (word == null || word.Text == null || word.Text.Length > 50_000 || !double.IsFinite(word.X) || !double.IsFinite(word.Y) || !double.IsFinite(word.Width) || !double.IsFinite(word.Height) ||
                    word.X < 0 || word.Y < 0 || word.Width < 0 || word.Height < 0 || word.X > expected.Width || word.Y > expected.Height ||
                    word.Width > expected.Width-word.X || word.Height > expected.Height-word.Y) throw new InvalidDataException("Invalid OCR word bounds.");
        }
    }
    public async ValueTask<bool> TryFinalizeAsync()
    {
        await process.StopAsync().ConfigureAwait(false);
        if(Volatile.Read(ref started)==1||!IoSettled||!process.ObserveExit())return false;
        return await process.TryFinalizeAsync().ConfigureAwait(false);
    }
    public async ValueTask DisposeAsync()
    {
        if (!await TryFinalizeAsync().ConfigureAwait(false)) throw new ImageJobException(process.ObserveExit()?ImageFailure.SettlementUnconfirmed:ImageFailure.ExitUnconfirmed);
    }
}
