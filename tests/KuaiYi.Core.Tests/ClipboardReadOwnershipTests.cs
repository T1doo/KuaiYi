using System.Runtime.InteropServices;
using KuaiYi.Contracts;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class ClipboardReadOwnershipTests
{
    private sealed class Clock:TimeProvider{public long Now=10000;public override long TimestampFrequency=>1000;public override long GetTimestamp()=>Now;}
    private sealed class Stream(byte[] wire,bool fault=false):MemoryStream(wire)
    {
        public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);public byte[]? Payload;public Action? AfterPayload;private int reads;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellation=default)
        {if(Interlocked.Increment(ref reads)==1){Entered.TrySetResult();await Release.Task;if(fault)throw new IOException("public owned read fault");}bool payload=Position>=IpcFrameCodec.HeaderLength;if(payload&&MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer,out var data))Payload=data.Array;int count=await base.ReadAsync(buffer,CancellationToken.None);if(payload)AfterPayload?.Invoke();return count;}
    }
    private static async Task<byte[]> Wire(){using var bytes=new MemoryStream();await IpcFrameCodec.WriteAsync(bytes,new(IpcMessageType.BinaryChunk,Guid.NewGuid(),Enumerable.Repeat((byte)83,64).ToArray()));return bytes.ToArray();}
    [Theory][InlineData(false,true)][InlineData(true,true)][InlineData(false,false)][InlineData(true,false)]
    public async Task CancelledClipboardReadHasOneOwnerWhetherCompletedBeforeFinallyOrLater(bool fault,bool beforeFinally)
    {
        using var stream=new Stream(await Wire(),fault);using var cancellation=new CancellationTokenSource();await using var client=new ClipboardCaptureClient("unused-public-fixture");int observers=0;client.LateReadObserverStartedForTest=()=>observers++;if(beforeFinally)client.BeforeReadSettlementForTest=async pending=>{stream.Release.TrySetResult();while(!pending.IsCompleted)await Task.Yield();};var read=client.Read(stream,TimeProvider.System.GetTimestamp(),cancellation.Token);await stream.Entered.Task;cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await read);Assert.NotNull(client.LateReadObserverForTest);if(!beforeFinally){Assert.False(client.LateReadObserverForTest!.IsCompleted);stream.Release.TrySetResult();}await client.LateReadObserverForTest!;Assert.Equal(1,observers);if(fault)Assert.Equal(nameof(IOException),client.FailureCode);else{Assert.NotNull(stream.Payload);Assert.All(stream.Payload!,v=>Assert.Equal(0,v));}
    }
    [Fact]public async Task CompletedClipboardReadWithAlreadyCancelledTokenNeverTransfersBuffer()
    {using var stream=new Stream(await Wire());stream.Release.TrySetResult();using var cancellation=new CancellationTokenSource();cancellation.Cancel();await using var client=new ClipboardCaptureClient("unused-public-fixture");int observers=0;client.LateReadObserverStartedForTest=()=>observers++;await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await client.Read(stream,TimeProvider.System.GetTimestamp(),cancellation.Token));await client.LateReadObserverForTest!;Assert.Equal(1,observers);Assert.All(stream.Payload!,v=>Assert.Equal(0,v));}
    [Theory][InlineData(1199,false)][InlineData(1200,true)][InlineData(1201,true)]
    public async Task ClipboardServiceUsesAbsoluteBudgetBeforeReadOwnershipTransfer(int elapsed,bool timeout)
    {
        var clock=new Clock();using var stream=new Stream(await Wire()){AfterPayload=()=>clock.Now+=elapsed};stream.Release.TrySetResult();await using var client=new ClipboardCaptureClient("unused-public-fixture",provider:clock);int observers=0;client.LateReadObserverStartedForTest=()=>observers++;
        if(timeout){await Assert.ThrowsAnyAsync<TimeoutException>(async()=>await client.Read(stream,10000,default));await client.LateReadObserverForTest!;Assert.Equal(1,observers);Assert.All(stream.Payload!,v=>Assert.Equal(0,v));}else{var frame=await client.Read(stream,10000,default);Assert.NotNull(frame);Assert.Same(stream.Payload,frame.Payload);Assert.All(frame.Payload,v=>Assert.Equal(83,v));Assert.Equal(0,observers);System.Security.Cryptography.CryptographicOperations.ZeroMemory(frame.Payload);}
    }
}
