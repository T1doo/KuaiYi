using System.Runtime.InteropServices;
using KuaiYi.Contracts;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class SelectionReadOwnershipTests
{
    private sealed class MonotonicClock:TimeProvider{public long Milliseconds=10000;public override long TimestampFrequency=>1000;public override long GetTimestamp()=>Milliseconds;}
    [Theory][InlineData(299,false)][InlineData(300,true)][InlineData(301,true)]
    public async Task MultipleCompletedReadsShareOneOriginWithoutRenewingBudget(int secondElapsed,bool timeout)
    {
        using var bytes=new MemoryStream();await IpcFrameCodec.WriteAsync(bytes,new(IpcMessageType.BinaryChunk,Guid.NewGuid(),Enumerable.Repeat((byte)83,64).ToArray()));var clock=new MonotonicClock();long started=clock.GetTimestamp();await using var client=new SelectionBrokerClient("unused-public-fixture",clock:clock);int observers=0;client.LateReadObserverStartedForTest=_=>observers++;
        using var first=new ControlledStream(bytes.ToArray(),false){PayloadRead=()=>clock.Milliseconds+=300};first.Release.TrySetResult();var frame=await client.ReadAsync(first,default,started);Assert.NotNull(frame);System.Security.Cryptography.CryptographicOperations.ZeroMemory(frame.Payload);Assert.Equal(0,observers);
        using var second=new ControlledStream(bytes.ToArray(),false){PayloadRead=()=>clock.Milliseconds+=secondElapsed};second.Release.TrySetResult();
        if(timeout){await Assert.ThrowsAnyAsync<TimeoutException>(async()=>await client.ReadAsync(second,default,started));await client.LateReadObserverForTest!;Assert.Equal(1,observers);Assert.All(second.CapturedPayload!,b=>Assert.Equal(0,b));}
        else{var result=await client.ReadAsync(second,default,started);Assert.NotNull(result);Assert.All(result.Payload,b=>Assert.Equal(83,b));System.Security.Cryptography.CryptographicOperations.ZeroMemory(result.Payload);Assert.Equal(0,observers);}
    }
    [Theory][InlineData(599,false)][InlineData(600,false)][InlineData(601,false)][InlineData(599,true)]
    public async Task CompletedReadCrossingAbsoluteBoundaryNeverTransfersAbandonedBuffer(int elapsed,bool cancel)
    {
        using var bytes=new MemoryStream();await IpcFrameCodec.WriteAsync(bytes,new(IpcMessageType.BinaryChunk,Guid.NewGuid(),Enumerable.Repeat((byte)83,64).ToArray()));using var cancellation=new CancellationTokenSource();var clock=new MonotonicClock();long started=clock.GetTimestamp();using var stream=new ControlledStream(bytes.ToArray(),false){PayloadRead=()=>{clock.Milliseconds+=elapsed;if(cancel)cancellation.Cancel();}};stream.Release.TrySetResult();await using var client=new SelectionBrokerClient("unused-public-fixture",clock:clock);int observers=0;client.LateReadObserverStartedForTest=_=>observers++;
        if(cancel)await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await client.ReadAsync(stream,cancellation.Token,started));
        else if(elapsed>=600)await Assert.ThrowsAnyAsync<TimeoutException>(async()=>await client.ReadAsync(stream,default,started));
        else{var frame=await client.ReadAsync(stream,default,started);Assert.NotNull(frame);Assert.All(frame.Payload,b=>Assert.Equal(83,b));Assert.Equal(0,observers);Assert.Null(client.LateReadObserverForTest);System.Security.Cryptography.CryptographicOperations.ZeroMemory(frame.Payload);return;}
        Assert.NotNull(client.LateReadObserverForTest);await client.LateReadObserverForTest!;Assert.Equal(1,observers);Assert.NotNull(stream.CapturedPayload);Assert.All(stream.CapturedPayload!,b=>Assert.Equal(0,b));
    }
    [Fact]public async Task SynchronouslyCompletedReadCannotEscapeAlreadyCancelledDeliveryToken()
    {
        using var bytes=new MemoryStream();await IpcFrameCodec.WriteAsync(bytes,new(IpcMessageType.BinaryChunk,Guid.NewGuid(),Enumerable.Repeat((byte)83,64).ToArray()));using var stream=new ControlledStream(bytes.ToArray(),false);stream.Release.TrySetResult();using var cancellation=new CancellationTokenSource();cancellation.Cancel();await using var client=new SelectionBrokerClient("unused-public-fixture");int observers=0;client.LateReadObserverStartedForTest=_=>observers++;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await client.ReadAsync(stream,cancellation.Token));Assert.NotNull(client.LateReadObserverForTest);await client.LateReadObserverForTest!;Assert.Equal(1,observers);Assert.NotNull(stream.CapturedPayload);Assert.All(stream.CapturedPayload!,b=>Assert.Equal(0,b));
    }
    private sealed class ControlledStream(byte[] bytes,bool fault):MemoryStream(bytes)
    {
        public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);public byte[]? CapturedPayload;public Action? PayloadRead;private int calls;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellation=default)
        {
            if(Interlocked.Increment(ref calls)==1){Entered.TrySetResult();await Release.Task;if(fault)throw new IOException("public read failure");}
            bool payload=Position>=IpcFrameCodec.HeaderLength;
            if(payload&&MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer,out var segment))CapturedPayload=segment.Array;
            int count=await base.ReadAsync(buffer,CancellationToken.None); // Test-only uncooperative read.
            if(payload)PayloadRead?.Invoke();return count;
        }
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CancelledWaiterMustTakeOwnershipEvenWhenUnderlyingReadCompletesBeforeFinally(bool fault)
    {
        using var bytes=new MemoryStream();await IpcFrameCodec.WriteAsync(bytes,new(IpcMessageType.BinaryChunk,Guid.NewGuid(),Enumerable.Repeat((byte)83,64).ToArray()));using var stream=new ControlledStream(bytes.ToArray(),fault);using var cancellation=new CancellationTokenSource();await using var client=new SelectionBrokerClient("unused-public-fixture");
        int observers=0;client.LateReadObserverStartedForTest=_=>observers++;client.BeforeReadSettlementForTest=async pending=>{stream.Release.TrySetResult();while(!pending.IsCompleted)await Task.Yield();};
        var read=client.ReadAsync(stream,cancellation.Token);await stream.Entered.Task;cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await read);
        Assert.NotNull(client.LateReadObserverForTest);await client.LateReadObserverForTest!;Assert.Equal(1,observers);
        if(fault)Assert.Equal(nameof(IOException),client.LateReadFailureCode);else{Assert.NotNull(stream.CapturedPayload);Assert.All(stream.CapturedPayload!,b=>Assert.Equal(0,b));}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task LateSuccessOrFaultAfterCancellationHasExactlyOneCleanupOwner(bool fault)
    {
        using var bytes=new MemoryStream();await IpcFrameCodec.WriteAsync(bytes,new(IpcMessageType.BinaryChunk,Guid.NewGuid(),Enumerable.Repeat((byte)83,64).ToArray()));using var stream=new ControlledStream(bytes.ToArray(),fault);using var cancellation=new CancellationTokenSource();await using var client=new SelectionBrokerClient("unused-public-fixture");int observers=0;client.LateReadObserverStartedForTest=_=>observers++;
        var read=client.ReadAsync(stream,cancellation.Token);await stream.Entered.Task;cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await read);Assert.NotNull(client.LateReadObserverForTest);Assert.False(client.LateReadObserverForTest!.IsCompleted);stream.Release.TrySetResult();await client.LateReadObserverForTest;Assert.Equal(1,observers);
        if(fault)Assert.Equal(nameof(IOException),client.LateReadFailureCode);else Assert.All(stream.CapturedPayload!,b=>Assert.Equal(0,b));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task SynchronousReadSuccessGoesToCallerAndSynchronousFaultIsObserved(bool fault)
    {
        using var bytes=new MemoryStream();await IpcFrameCodec.WriteAsync(bytes,new(IpcMessageType.BinaryChunk,Guid.NewGuid(),Enumerable.Repeat((byte)83,64).ToArray()));using var stream=new ControlledStream(bytes.ToArray(),fault);stream.Release.TrySetResult();await using var client=new SelectionBrokerClient("unused-public-fixture");int observers=0;client.LateReadObserverStartedForTest=_=>observers++;
        if(fault){await Assert.ThrowsAsync<IOException>(async()=>await client.ReadAsync(stream,default));await client.LateReadObserverForTest!;Assert.Equal(1,observers);Assert.Equal(nameof(IOException),client.LateReadFailureCode);}
        else{var frame=await client.ReadAsync(stream,default);Assert.NotNull(frame);Assert.Same(stream.CapturedPayload,frame.Payload);Assert.All(frame.Payload,b=>Assert.Equal(83,b));Assert.Equal(0,observers);Assert.Null(client.LateReadObserverForTest);System.Security.Cryptography.CryptographicOperations.ZeroMemory(frame.Payload);}
    }
}
