using System.Runtime.InteropServices;
using KuaiYi.Contracts;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class SelectionReadOwnershipTests
{
    private sealed class ControlledStream(byte[] bytes,bool fault):MemoryStream(bytes)
    {
        public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);public byte[]? CapturedPayload;private int calls;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellation=default)
        {
            if(Interlocked.Increment(ref calls)==1){Entered.TrySetResult();await Release.Task;if(fault)throw new IOException("public read failure");}
            if(Position>=IpcFrameCodec.HeaderLength&&MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer,out var segment))CapturedPayload=segment.Array;
            return await base.ReadAsync(buffer,CancellationToken.None); // Test-only uncooperative read.
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
