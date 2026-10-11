using System.Runtime.InteropServices;
using KuaiYi.Contracts;
using KuaiYi.OleTransfer;
using KuaiYi.Core;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class OleReadOwnershipTests
{
    private sealed class Registrar:IOleTargetRegistrar{public bool IsSettledAndRevoked=>true;public Task RegisterAsync(long hwnd,byte[] bytes,CancellationToken token)=>Task.CompletedTask;public ValueTask<bool> TryFinalizeAsync()=>ValueTask.FromResult(true);}
    private sealed class Activation:IOleActivationLease{public string Name=>"public";public bool Disposed;public void Set(long epoch,bool enabled){}public void Dispose()=>Disposed=true;}
    private static OleReceiverClient Client(Activation? activation=null)=>new("unused-public-fixture",new Registrar(),activation??new());
    private sealed class Clock:TimeProvider{public long Now=10000;public override long TimestampFrequency=>1000;public override long GetTimestamp()=>Now;}
    private sealed class Stream(byte[] wire,bool fault=false):MemoryStream(wire)
    {
        public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);public byte[]? Payload;private int reads;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellation=default)
        {if(Interlocked.Increment(ref reads)==1){Entered.TrySetResult();await Release.Task;if(fault)throw new IOException("public owned read fault");}bool payload=Position>=IpcFrameCodec.HeaderLength;if(payload&&MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer,out var data))Payload=data.Array;int count=await base.ReadAsync(buffer,CancellationToken.None);return count;}
    }
    private static async Task<byte[]> Wire(){using var bytes=new MemoryStream();await IpcFrameCodec.WriteAsync(bytes,new(IpcMessageType.BinaryChunk,Guid.NewGuid(),Enumerable.Repeat((byte)83,64).ToArray()));return bytes.ToArray();}
    [Theory][InlineData(false,true)][InlineData(true,true)][InlineData(false,false)][InlineData(true,false)]
    public async Task CancelledOleReadHasOneOwnerWhetherCompletedBeforeFinallyOrLater(bool fault,bool beforeFinally)
    {
        using var stream=new Stream(await Wire(),fault);using var cancellation=new CancellationTokenSource();await using var client=Client();int observers=0;client.LateReadObserverStartedForTest=()=>observers++;if(beforeFinally)client.BeforeReadSettlementForTest=async pending=>{stream.Release.TrySetResult();while(!pending.IsCompleted)await Task.Yield();};var read=client.ReadOwnedAsync(stream,cancellation.Token);await stream.Entered.Task;cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await read);Assert.NotNull(client.LateReadObserverForTest);if(!beforeFinally){Assert.False(client.LateReadObserverForTest!.IsCompleted);stream.Release.TrySetResult();}await client.LateReadObserverForTest!;Assert.Equal(1,observers);if(fault)Assert.Equal(nameof(IOException),client.ReadFailureCode);else{Assert.NotNull(stream.Payload);Assert.All(stream.Payload!,v=>Assert.Equal(0,v));}
    }
    [Fact]public async Task CompletedOleReadWithAlreadyCancelledTokenNeverTransfersBuffer()
    {using var stream=new Stream(await Wire());stream.Release.TrySetResult();using var cancellation=new CancellationTokenSource();cancellation.Cancel();await using var client=Client();int observers=0;client.LateReadObserverStartedForTest=()=>observers++;await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await client.ReadOwnedAsync(stream,cancellation.Token));await client.LateReadObserverForTest!;Assert.Equal(1,observers);Assert.All(stream.Payload!,v=>Assert.Equal(0,v));}
    [Fact]public async Task LateOwnedReadBlocksActivationFinalizationUntilOriginalReadSettles()
    {
        using var stream=new Stream(await Wire());using var cancellation=new CancellationTokenSource();var activation=new Activation();var client=Client(activation);var read=client.ReadOwnedAsync(stream,cancellation.Token);await stream.Entered.Task;cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await read);Assert.False(await client.TryFinalizeAsync());Assert.False(activation.Disposed);stream.Release.TrySetResult();await client.LateReadObserverForTest!;Assert.All(stream.Payload!,b=>Assert.Equal(0,b));Assert.True(await client.TryFinalizeAsync(true));Assert.True(activation.Disposed);await client.DisposeAsync();
    }
}
