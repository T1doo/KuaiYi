using System.Buffers.Binary;
using KuaiYi.Core;
using KuaiYi.Imaging;
using Xunit;
namespace KuaiYi.Codec.Tests;
public sealed class BitmapImageOperationTests
{
    private static string Worker(){for(var p=new DirectoryInfo(AppContext.BaseDirectory);p!=null;p=p.Parent)if(File.Exists(Path.Combine(p.FullName,"SPEC.md")))return Path.Combine(p.FullName,"tools/KuaiYi.ImageDecodeFixture/bin/Release/net10.0/KuaiYi.ImageDecodeFixture.dll");throw new IOException();}
    private static byte[] Bmp(int height=2){byte[] b=new byte[78];b[14]=40;BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(18),3);BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(22),height);b[26]=1;b[28]=24;b[54]=255;OleBitmapDrop.WriteFileHeader(b);return b;}
    [Theory][InlineData(2)][InlineData(-2)]public async Task RealWorkerStartsOnlyAfterOriginalPeerSettlesAndProducesDistinctUnanchoredBitmapOwner(int height)
    {
        var peer=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);using var bytes=new OwnedOleBitmap(Bmp(height));var borrowed=bytes.Bytes;int factories=0;
        await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());var op=new BitmapImageOperation(bytes,peer.Task,()=>{factories++;return client;});var load=op.LoadAsync(Guid.NewGuid(),1,1,default);
        Assert.False(load.IsCompleted);Assert.Equal(0,factories);Assert.False(await op.TryFinalizeAsync());peer.SetResult();using var image=await load;
        Assert.True(client.ObserveExit());Assert.True(await op.TryFinalizeAsync());Assert.Equal(1,factories);Assert.All(borrowed.ToArray(),b=>Assert.Equal(0,b));Assert.Equal(ImageSourceKind.Bitmap,image.Kind);Assert.Equal(EncodedRasterFormat.Bmp,image.Metadata.Format);Assert.Equal(ImageDecodePolicy.Hash(Bmp(height)),image.Metadata.SourceSha256);Assert.Equal(3,image.Width);Assert.Equal(2,image.Height);Assert.False(image.Metadata.Transform.HasDesktopAnchor);using var request=image.PrepareRequest(4194304);Assert.Equal(ImageSourceKind.Bitmap,request.Kind);Assert.Null(request.NormalizationProof);Assert.Throws<InvalidOperationException>(()=>request.Transform.NormalizedToDesktop(new NormalizedImagePoint(0,0)));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CancelOrTransportFailureCannotClearBytesBeforeActualPeerCleanup(bool failure)
    {
        var peer=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);using var bytes=new OwnedOleBitmap(Bmp());var borrowed=bytes.Bytes;using var cancel=new CancellationTokenSource();int factories=0;var op=new BitmapImageOperation(bytes,peer.Task,()=>{factories++;throw new InvalidOperationException();});var load=op.LoadAsync(Guid.NewGuid(),1,1,cancel.Token);
        if(!failure){cancel.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>load);Assert.False(await op.TryFinalizeAsync());Assert.Contains(borrowed.ToArray(),b=>b!=0);peer.SetResult();}else{peer.SetException(new IOException("original peer"));await Assert.ThrowsAsync<IOException>(()=>load);}
        await op.ObservePeerCleanupAsync();Assert.True(await op.TryFinalizeAsync());Assert.All(borrowed.ToArray(),b=>Assert.Equal(0,b));Assert.Equal(0,factories);
    }
    [Fact]public async Task CancelledActualWorkerProjectionRetainsBytesUntilOriginalTaskAndWorkerExit()
    {
        using var bytes=new OwnedOleBitmap(Bmp());var raw=bytes.Bytes;var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int n=0;using var cancel=new CancellationTokenSource();await using var worker=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){FileAllocationForTest=_=>{if(++n==2){entered.TrySetResult();release.Task.GetAwaiter().GetResult();}}};var op=new BitmapImageOperation(bytes,Task.CompletedTask,()=>worker);var load=op.LoadAsync(Guid.NewGuid(),1,1,cancel.Token);
        try{await entered.Task.WaitAsync(TimeSpan.FromSeconds(8));cancel.Cancel();Assert.False(load.IsCompleted);Assert.False(await op.TryFinalizeAsync());Assert.Contains(raw.ToArray(),b=>b!=0);release.SetResult();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>load);Assert.True(await op.TryFinalizeAsync());Assert.True(worker.ObserveExit());Assert.All(raw.ToArray(),b=>Assert.Equal(0,b));}
        finally{release.TrySetResult();try{await load;}catch(OperationCanceledException){}}
    }
}
