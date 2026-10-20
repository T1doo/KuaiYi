using System.Diagnostics;
using KuaiYi.Imaging;
using Xunit;
using Xunit.Abstractions;
namespace KuaiYi.Codec.Tests;
public sealed class ImageSettlementTests(ITestOutputHelper output)
{
    private static string Worker(){for(var p=new DirectoryInfo(AppContext.BaseDirectory);p!=null;p=p.Parent)if(File.Exists(Path.Combine(p.FullName,"SPEC.md")))return Path.Combine(p.FullName,"tools/KuaiYi.ImageDecodeFixture/bin/Release/net10.0/KuaiYi.ImageDecodeFixture.dll");throw new IOException("No fixture.");}
    private sealed class Starter:IImageProcessStarter{internal Process? Child;public Process Start(ProcessStartInfo info)=>Child=Process.Start(info)!;}
    private sealed class Tail(Stream source,Func<bool> tail):Stream
    {
        internal Exception? LateError;
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously),Returned=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellation=default)
        {int n=await source.ReadAsync(buffer,CancellationToken.None);if(tail()&&buffer.Length==1){Entered.TrySetResult();await Release.Task;Returned.TrySetResult();if(LateError!=null)throw LateError;}return n;}
        public override bool CanRead=>true;public override bool CanWrite=>false;public override bool CanSeek=>false;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}public override void Flush()=>throw new NotSupportedException();public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long l)=>throw new NotSupportedException();
    }
    [Theory][InlineData(false)][InlineData(true)]public async Task CancellationAndDisposeKeepTheSameFirstFailureWhileOriginalTailIoIsHeld(bool lateFault)
    {
        using var pixels=new ScreenshotPixels(new(0,0,2,1),[1,2,3,255,4,5,6,255]);using var png=ScreenshotPngEncoder.Encode(pixels);byte[] input=png.Bytes.ToArray();var job=new ImageDecodeJob(Guid.NewGuid(),1,1,input.Length,ImageDecodePolicy.Hash(input));var starter=new Starter();Tail? tail=null;var ready=new TaskCompletionSource<Tail>(TaskCreationOptions.RunContinuationsAsynchronously);bool received=false;using var stop=new CancellationTokenSource();
        var clearEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var clearRelease=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);byte[]? raster=null;int projections=0;
        var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),starter){ReadStreamForTest=s=>{tail=new(s,()=>received);ready.SetResult(tail);return tail;},DecodeReceivedForTest=()=>received=true,DecodeAllocationForTest=b=>raster=b,FileAllocationForTest=_=>projections++,BeforeIoClearForTest=kind=>{if(kind!=ImageIoKind.TailRead)return Task.CompletedTask;clearEntered.TrySetResult();return clearRelease.Task;}};
        var task=client.PrepareFileAsync(input,job,stop.Token);
        try
        {
            // EOF has actually returned from the process pipe, but this original read task is deliberately pending.
            tail=await ready.Task.WaitAsync(TimeSpan.FromSeconds(8));
            await tail.Entered.Task.WaitAsync(TimeSpan.FromSeconds(8));stop.Cancel();
            var failure=await Assert.ThrowsAsync<ImageJobException>(()=>task);Assert.Equal(ImageFailure.SettlementUnconfirmed,failure.Code);Assert.IsAssignableFrom<OperationCanceledException>(failure.InnerException);Assert.True(starter.Child!.HasExited);Assert.False(await client.TryFinalizeAsync());
            var dispose=await Assert.ThrowsAsync<ImageJobException>(()=>client.DisposeAsync().AsTask());Assert.Equal(ImageFailure.SettlementUnconfirmed,dispose.Code);Assert.Same(failure.InnerException,dispose.InnerException);Assert.Same(failure.InnerException,client.PrimaryFailure);
            var pending=client.SettlementSnapshot;output.WriteLine("original-pending "+System.Text.Json.JsonSerializer.Serialize(pending));Assert.True(pending.ExitObserved);Assert.False(pending.OperationActive);Assert.Equal(1,pending.PendingOriginalIo);Assert.Equal(1,pending.PendingCleanup);Assert.Equal(0,projections);Assert.NotNull(raster);Assert.True(raster.AsSpan().IndexOfAnyExcept((byte)0)<0);
            if(lateFault)tail.LateError=new IOException("SYNTHETIC_BODY_MUST_NOT_BE_DIAGNOSTIC");tail.Release.SetResult();await clearEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var clearing=client.SettlementSnapshot;output.WriteLine("cleanup-pending "+System.Text.Json.JsonSerializer.Serialize(clearing));Assert.Equal(0,clearing.PendingOriginalIo);Assert.Equal(1,clearing.PendingCleanup);Assert.False(await client.TryFinalizeAsync());Assert.Same(failure.InnerException,client.PrimaryFailure);Assert.DoesNotContain("SYNTHETIC_BODY",System.Text.Json.JsonSerializer.Serialize(clearing));
            clearRelease.SetResult();await client.IoSettlementForTest.WaitAsync(TimeSpan.FromSeconds(3));Assert.True(await client.TryFinalizeAsync());await client.DisposeAsync();var done=client.SettlementSnapshot;output.WriteLine("settled "+System.Text.Json.JsonSerializer.Serialize(done));Assert.Equal(0,done.PendingOriginalIo);Assert.Equal(0,done.PendingCleanup);Assert.Equal(0,done.FaultedCleanup);Assert.Same(failure.InnerException,client.PrimaryFailure);if(lateFault)Assert.Equal(nameof(IOException),client.IoFailureCode);
            Assert.True(done.Events.Single(e=>e.Stage==ImageSettlementStage.ExitObserved).Sequence<done.Io.Single(i=>i.Kind==ImageIoKind.TailRead).CleanupObservedSequence);
        }
        finally{tail?.Release.TrySetResult();clearRelease.TrySetResult();_=await Record.ExceptionAsync(()=>task);if(tail!=null)await tail.Returned.Task.WaitAsync(TimeSpan.FromSeconds(3));await client.IoSettlementForTest.WaitAsync(TimeSpan.FromSeconds(3));Assert.True(await client.TryFinalizeAsync());await client.DisposeAsync();}
    }
    [Fact]public async Task ActualProjectionFirstFaultRetainsItsIdentityAndNeverAppearsInSafeDiagnostics()
    {
        using var pixels=new ScreenshotPixels(new(0,0,2,1),[1,2,3,255,4,5,6,255]);using var png=ScreenshotPngEncoder.Encode(pixels);byte[] input=png.Bytes.ToArray();var error=new IOException("SYNTHETIC_PRIVATE_PATH_AND_BODY");await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){FileAllocationForTest=_=>throw error};
        var thrown=await Assert.ThrowsAsync<IOException>(()=>client.PrepareFileAsync(input,new(Guid.NewGuid(),1,1,input.Length,ImageDecodePolicy.Hash(input))));Assert.Same(error,thrown);Assert.Same(error,client.PrimaryFailure);Assert.Null(client.CleanupFailure);Assert.True(await client.TryFinalizeAsync());
        for(int i=0;i<80;i++)Assert.True(await client.TryFinalizeAsync());var snapshot=client.SettlementSnapshot;Assert.Equal(nameof(IOException),snapshot.PrimaryFailureType);Assert.Equal(0,snapshot.PendingCleanup);Assert.Equal(32,snapshot.Events.Count);Assert.DoesNotContain("SYNTHETIC_PRIVATE",System.Text.Json.JsonSerializer.Serialize(snapshot));
    }
    [Fact]public async Task PreChannelValidationFailureIsAlsoPreservedWithoutStartingAProcess()
    {
        byte[] invalid=[1,2,3];var starter=new Starter();await using var client=new ImageWorkerClient("SYNTHETIC_UNUSED_EXECUTABLE",processStarter:starter);Assert.False(client.SettlementSnapshot.ExitObserved);Assert.DoesNotContain(client.SettlementSnapshot.Events,e=>e.Stage==ImageSettlementStage.ExitObserved);var error=await Assert.ThrowsAnyAsync<Exception>(()=>client.DecodeAsync(invalid,new(Guid.NewGuid(),1,1,3,ImageDecodePolicy.Hash(invalid))));Assert.Same(error,client.PrimaryFailure);Assert.Null(starter.Child);Assert.Equal(error.GetType().Name,client.SettlementSnapshot.PrimaryFailureType);Assert.True(await client.TryFinalizeAsync());
    }

    [Fact]public async Task ActualCleanupTaskFaultIsObservedSeparatelyAndCannotBeMistakenForSettlement()
    {
        using var pixels=new ScreenshotPixels(new(0,0,2,1),[1,2,3,255,4,5,6,255]);using var png=ScreenshotPngEncoder.Encode(pixels);byte[] input=png.Bytes.ToArray();var ready=new TaskCompletionSource<Tail>(TaskCreationOptions.RunContinuationsAsynchronously);bool received=false;var cleanupError=new IOException("SYNTHETIC_CLEANUP_BODY");using var stop=new CancellationTokenSource();
        var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){ReadStreamForTest=s=>{var held=new Tail(s,()=>received);ready.SetResult(held);return held;},DecodeReceivedForTest=()=>received=true,BeforeIoClearForTest=k=>k==ImageIoKind.TailRead?Task.FromException(cleanupError):Task.CompletedTask};
        var task=client.PrepareFileAsync(input,new(Guid.NewGuid(),1,1,input.Length,ImageDecodePolicy.Hash(input)),stop.Token);var tail=await ready.Task.WaitAsync(TimeSpan.FromSeconds(8));
        try
        {
            await tail.Entered.Task.WaitAsync(TimeSpan.FromSeconds(8));stop.Cancel();var error=await Assert.ThrowsAsync<ImageJobException>(()=>task);var primary=Assert.IsAssignableFrom<OperationCanceledException>(error.InnerException);Assert.Null(client.CleanupFailure);tail.Release.TrySetResult();var failed=await Assert.ThrowsAsync<IOException>(()=>client.IoSettlementForTest);Assert.Same(cleanupError,failed);Assert.Same(primary,client.PrimaryFailure);Assert.Same(cleanupError,client.CleanupFailure);Assert.False(await client.TryFinalizeAsync());
            var snapshot=client.SettlementSnapshot;output.WriteLine("cleanup-fault "+System.Text.Json.JsonSerializer.Serialize(snapshot));Assert.Equal(0,snapshot.PendingCleanup);Assert.Equal(1,snapshot.FaultedCleanup);Assert.Equal(nameof(IOException),snapshot.CleanupFailureType);Assert.DoesNotContain("SYNTHETIC_CLEANUP",System.Text.Json.JsonSerializer.Serialize(snapshot));var dispose=await Assert.ThrowsAsync<ImageJobException>(()=>client.DisposeAsync().AsTask());Assert.Equal(ImageFailure.SettlementUnconfirmed,dispose.Code);Assert.Same(primary,dispose.InnerException);Assert.True(client.ObserveExit());
        }
        finally{tail.Release.TrySetResult();_=await Record.ExceptionAsync(()=>task);_=await Record.ExceptionAsync(()=>client.IoSettlementForTest);}
        // A faulted cleanup stays owned and blocked; this synthetic injected failure is not relabelled as a successful retry.
    }

}
