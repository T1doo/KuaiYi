using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using KuaiYi.Contracts;
using KuaiYi.Core;
namespace KuaiYi.Imaging;
/// <summary>Only the child returned by our Start is supervised/terminated. No PID lookup or descendant kill.</summary>
public sealed class CaptureHostClient(string executable,string? managedAssembly=null,IImageProcessStarter? starter=null,TimeSpan? budget=null,TimeProvider? timeProvider=null):IAsyncDisposable
{
    private readonly OwnedProcessSession process=new(starter);
    private int started;
    internal Func<Process,IOwnedHostLifetime>? LifetimeForTest {init{process.LifetimeForTest=value;}}
    internal Action<Process>? OwnershipPublishedForTest {init{process.OwnershipPublishedForTest=value;}}
    internal Task StartupSettlementForTest=>process.StartupSettlement;
    internal Func<Stream,Stream>? ReadStreamForTest {get;init;}
    internal Action<byte[]>? LateReadClearedForTest {get;init;}
    public HostExitState? ExitState=>process.ExitState;
    public string? StartupFailureCode=>process.StartupFailureCode;
    public string? FinalizationFailureCode=>process.FinalizationFailureCode;
    public bool ObserveExit()=>process.ObserveExit();
    public ValueTask<bool> TryFinalizeAsync(bool retry=false)=>process.TryFinalizeAsync(retry);
    public async ValueTask DisposeAsync(){if(!await TryFinalizeAsync())throw new CaptureException(CaptureFailure.ExitUnconfirmed);}
    public async Task<ScreenshotSnapshot> RunAsync(CaptureJob job,CancellationToken cancellation=default)
    {
        CaptureProtocol.Validate(job);if(Interlocked.Exchange(ref started,1)!=0)throw new InvalidOperationException("Single capture per host.");cancellation.ThrowIfCancellationRequested();
        var clock=timeProvider??TimeProvider.System;TimeSpan limit=budget??TimeSpan.FromSeconds(5);long began=clock.GetTimestamp();
        using var deadline=new CancellationTokenSource(limit,clock);using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,deadline.Token);
        var pending=process.Start(OwnedProcessSession.Info(executable,managedAssembly),linked.Token);var tiles=new List<ScreenshotPixels>();ScreenshotSnapshot? accepted=null;
        try
        {
            var child=await pending.WaitAsync(linked.Token).ConfigureAwait(false);Check();var input=child.StandardInput.BaseStream;var output=ReadStreamForTest?.Invoke(child.StandardOutput.BaseStream)??child.StandardOutput.BaseStream;
            byte[] nonce=RandomNumberGenerator.GetBytes(32);
            try
            {
                await IpcFrameCodec.WriteAsync(input,new(IpcMessageType.CaptureHello,job.OperationId,nonce),linked.Token);await input.FlushAsync(linked.Token);
                var hello=await Read();try{if(hello.Type!=IpcMessageType.CaptureHello||hello.RequestId!=job.OperationId||!CryptographicOperations.FixedTimeEquals(nonce,hello.Payload))throw new CaptureException(CaptureFailure.ProtocolFailure);}finally{CryptographicOperations.ZeroMemory(hello.Payload);}
            }finally{CryptographicOperations.ZeroMemory(nonce);}
            byte[] command=ImageJson.Serialize(job);try{await IpcFrameCodec.WriteAsync(input,new(IpcMessageType.CaptureBegin,job.OperationId,command),linked.Token);await input.FlushAsync(linked.Token);}finally{CryptographicOperations.ZeroMemory(command);}
            var header=await Read();CaptureReply reply;
            try{if(header.Type!=IpcMessageType.CaptureResult||header.RequestId!=job.OperationId)throw new CaptureException(CaptureFailure.ProtocolFailure);reply=ImageJson.Read<CaptureReply>(header.Payload);}finally{CryptographicOperations.ZeroMemory(header.Payload);}
            var topology=CaptureProtocol.ValidateReply(reply,job);
            for(int tileIndex=0;tileIndex<reply.Tiles.Length;tileIndex++)
            {
                var descriptor=reply.Tiles[tileIndex];byte[]? data=new byte[descriptor.ByteLength];int filled=0,index=0;
                try
                {
                    while(filled<data.Length)
                    {
                        var chunk=await Read();try
                        {
                            if(chunk.RequestId!=job.OperationId||chunk.Type!=IpcMessageType.BinaryChunk||chunk.Payload.Length<=8||BinaryPrimitives.ReadInt32LittleEndian(chunk.Payload)!=tileIndex||BinaryPrimitives.ReadInt32LittleEndian(chunk.Payload.AsSpan(4))!=index++||chunk.Payload.Length-8>data.Length-filled)throw new CaptureException(CaptureFailure.ProtocolFailure);
                            chunk.Payload.AsSpan(8).CopyTo(data.AsSpan(filled));filled+=chunk.Payload.Length-8;
                        }finally{CryptographicOperations.ZeroMemory(chunk.Payload);}
                    }
                    Check();if(!string.Equals(Convert.ToHexString(SHA256.HashData(data)),descriptor.Sha256,StringComparison.OrdinalIgnoreCase))throw new CaptureException(CaptureFailure.ProtocolFailure);
                    tiles.Add(new(descriptor.Bounds,data));data=null;
                }finally{if(data!=null)CryptographicOperations.ZeroMemory(data);}
            }
            var end=await Read();try{if(end.Type!=IpcMessageType.CaptureCommit||end.RequestId!=job.OperationId||end.Payload.Length!=0)throw new CaptureException(CaptureFailure.ProtocolFailure);}finally{CryptographicOperations.ZeroMemory(end.Payload);}
            child.StandardInput.Close();await child.WaitForExitAsync(linked.Token).ConfigureAwait(false);Check();if(child.ExitCode!=0||!process.ObserveExit())throw new CaptureException(CaptureFailure.ProtocolFailure);
            accepted=new(topology,tiles);tiles.Clear();return accepted;
            async Task<IpcFrame> Read()
            {
                var reading=IpcFrameCodec.ReadAsync(output,linked.Token).AsTask();IpcFrame? frame;
                try{frame=await reading.WaitAsync(linked.Token).ConfigureAwait(false);}
                catch{_ = ClearLateRead(reading);throw;} // Retain/observe the original read, including buffers returned after cancellation.
                if(frame==null)throw new EndOfStreamException();try{Check();return frame;}catch{CryptographicOperations.ZeroMemory(frame.Payload);throw;}
            }
        }
        catch(OperationCanceledException)when(deadline.IsCancellationRequested&&!cancellation.IsCancellationRequested){throw new CaptureException(CaptureFailure.Timeout);}
        finally
        {
            foreach(var tile in tiles)tile.Dispose();await process.StopAsync().ConfigureAwait(false);
            if(!process.ObserveExit()){accepted?.Dispose();throw new CaptureException(CaptureFailure.ExitUnconfirmed);}
            if(accepted!=null)try{Check();}catch{accepted.Dispose();throw;}
        }
        void Check(){linked.Token.ThrowIfCancellationRequested();if(clock.GetElapsedTime(began)>=limit)throw new CaptureException(CaptureFailure.Timeout);}
    }
    private async Task ClearLateRead(Task<IpcFrame?> reading)
    {
        try{var late=await reading.ConfigureAwait(false);if(late!=null){CryptographicOperations.ZeroMemory(late.Payload);LateReadClearedForTest?.Invoke(late.Payload);}}
        catch(Exception error)when(error is IOException or OperationCanceledException or ObjectDisposedException or InvalidDataException){/* Observed failed owned pipe read; never applied to UI. */}
    }
}
