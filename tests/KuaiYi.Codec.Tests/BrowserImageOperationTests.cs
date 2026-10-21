using KuaiYi.Imaging;
using SkiaSharp;
using Xunit;
namespace KuaiYi.Codec.Tests;
public sealed class BrowserImageOperationTests
{
    private static string Worker(){for(var p=new DirectoryInfo(AppContext.BaseDirectory);p!=null;p=p.Parent)if(File.Exists(Path.Combine(p.FullName,"SPEC.md")))return Path.Combine(p.FullName,"tools/KuaiYi.ImageDecodeFixture/bin/Release/net10.0/KuaiYi.ImageDecodeFixture.dll");throw new IOException("No fixture.");}
    private static byte[] Png(){using var pixels=new ScreenshotPixels(new(0,0,3,2),Enumerable.Repeat((byte)255,24).ToArray());using var png=ScreenshotPngEncoder.Encode(pixels);return png.Bytes.ToArray();}
    [Fact]public async Task ActualLinuxBrowserByteOperationUsesTheOriginalDecoderAndPreservesDistinctUnanchoredSourceKind()
    {
        Assert.False(OperatingSystem.IsWindows());byte[] png=Png(),original=png.ToArray();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());var operation=new BrowserImageOperation(()=>client);using var image=await operation.LoadAsync(png,Guid.NewGuid(),1,1,default);Assert.True(client.ObserveExit());Assert.True(await operation.TryFinalizeAsync());Assert.Equal(original,png);Assert.Equal(ImageSourceKind.Browser,image.Kind);Assert.Equal(EncodedRasterFormat.Png,image.Metadata.Format);Assert.Equal(ImageDecodePolicy.Hash(png),image.Metadata.SourceSha256);Assert.False(image.Metadata.Transform.HasDesktopAnchor);Assert.Equal(3,image.Width);Assert.Equal(2,image.Height);using var request=image.PrepareRequest(4194304);Assert.Equal(ImageSourceKind.Browser,request.Kind);Assert.Null(request.NormalizationProof);Assert.Null(request.Notice);Assert.Throws<InvalidOperationException>(()=>request.Transform.NormalizedToDesktop(new NormalizedImagePoint(0,0)));
    }
    [Fact]public async Task BrowserOperationCannotExpandTheExistingPngTransferSubsetOrStartAWorkerForJpeg()
    {
        using var bitmap=new SKBitmap(3,2);bitmap.Erase(SKColors.White);using var data=bitmap.Encode(SKEncodedImageFormat.Jpeg,100);int factories=0;var operation=new BrowserImageOperation(()=>{factories++;throw new InvalidOperationException("Must not start.");});await Assert.ThrowsAsync<ImageJobException>(()=>operation.LoadAsync(data.ToArray(),Guid.NewGuid(),1,1,default));Assert.Equal(0,factories);Assert.True(await operation.TryFinalizeAsync());await Assert.ThrowsAsync<InvalidOperationException>(()=>operation.LoadAsync(Png(),Guid.NewGuid(),1,1,default));
    }
    [Fact]public async Task CancelledOriginalBrowserProjectionAndItsWorkerRemainOwnedUntilActualTaskSettlement()
    {
        byte[] png=Png();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var allocations=new List<byte[]>();using var cancellation=new CancellationTokenSource();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){FileAllocationForTest=bytes=>{allocations.Add(bytes);if(allocations.Count==2){entered.TrySetResult();release.Task.GetAwaiter().GetResult();}}};var operation=new BrowserImageOperation(()=>client);var original=operation.LoadAsync(png,Guid.NewGuid(),1,1,cancellation.Token);
        try{await entered.Task.WaitAsync(TimeSpan.FromSeconds(8));cancellation.Cancel();Assert.False(original.IsCompleted);Assert.False(await operation.TryFinalizeAsync());Assert.Contains(png,b=>b!=0);release.SetResult();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>original);Assert.True(await operation.TryFinalizeAsync());Assert.True(client.ObserveExit());Assert.All(allocations,b=>Assert.True(b.AsSpan().IndexOfAnyExcept((byte)0)<0));}
        finally{release.TrySetResult();try{await original;}catch(OperationCanceledException){}}
    }
}
