using KuaiYi.Imaging;
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
    private static byte[] Fixture(string name)
    {for(var p=new DirectoryInfo(AppContext.BaseDirectory);p!=null;p=p.Parent)if(File.Exists(Path.Combine(p.FullName,"SPEC.md")))return File.ReadAllBytes(Path.Combine(p.FullName,"tests/fixtures/images",name));throw new IOException("No fixture.");}
    [Theory][InlineData("browser-raster.jpg",EncodedRasterFormat.Jpeg,3,2)][InlineData("browser-raster-exif6.jpg",EncodedRasterFormat.Jpeg,2,3)][InlineData("browser-raster.bmp",EncodedRasterFormat.Bmp,3,2)][InlineData("browser-raster.webp",EncodedRasterFormat.Webp,3,2)][InlineData("gif-codewidth.gif",EncodedRasterFormat.Gif,256,128)]
    public async Task BrowserOriginalFormatsUseActualWorkerAndKeepSourceDigestAndFirstFrameNotice(string name,EncodedRasterFormat format,int width,int height)
    {
        byte[] input=Fixture(name),original=input.ToArray();var allocations=new List<byte[]>();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){FileAllocationForTest=allocations.Add};
        var operation=new BrowserImageOperation(()=>client);var image=await operation.LoadAsync(input,Guid.NewGuid(),1,1,default);
        Assert.True(client.ObserveExit());Assert.True(await operation.TryFinalizeAsync());Assert.Equal(original,input);Assert.Equal(ImageSourceKind.Browser,image.Kind);Assert.Equal(format,image.Metadata.Format);Assert.Equal(ImageDecodePolicy.Hash(input),image.Metadata.SourceSha256);Assert.Equal(input.Length,image.Metadata.SourceByteLength);Assert.Equal(width,image.Width);Assert.Equal(height,image.Height);Assert.False(image.Metadata.Transform.HasDesktopAnchor);
        using(var request=image.PrepareRequest(4194304)){Assert.Equal(ImageSourceKind.Browser,request.Kind);Assert.Equal(new ImageSize((uint)width,(uint)height),ImagePolicy.InspectPng(request.Bytes.Span));Assert.Equal(format==EncodedRasterFormat.Gif?"使用首帧":null,request.Notice);Assert.Null(request.NormalizationProof);Assert.Throws<InvalidOperationException>(()=>request.Transform.NormalizedToDesktop(new NormalizedImagePoint(0,0)));}
        image.Dispose();Assert.All(allocations,b=>Assert.True(b.AsSpan().IndexOfAnyExcept((byte)0)<0));
    }
    [Fact]public async Task BrowserGifOffsetAndTransparentCanvasRetainFirstFrameNotice()
    {
        byte[] bytes=GifDecodeTests.Gif(4,3,[GifDecodeTests.Image(1,1,2,1,[0,2],transparent:0),GifDecodeTests.Image(0,0,4,3,Enumerable.Repeat((byte)1,12).ToArray())]);
        await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());var operation=new BrowserImageOperation(()=>client);using var image=await operation.LoadAsync(bytes,Guid.NewGuid(),1,1,default);Assert.True(await operation.TryFinalizeAsync());Assert.Equal(new GifFirstFrameInfo(2,1,1,2,1),image.Metadata.GifInfo);Assert.Equal("使用首帧",image.Notice);Assert.Equal(4,image.Width);Assert.Equal(3,image.Height);
        using var preview=image.CreatePreview(4,3);byte[] expected=Enumerable.Repeat((byte)255,48).ToArray();expected[6*4+1]=expected[6*4+2]=0;Assert.Equal(expected,preview.Bytes.ToArray());
    }
    [Fact]public async Task BrowserOddWebpPayloadStillRejectsBeforeAnyWorkerFactory()
    {
        byte[] even=Fixture("browser-raster.webp"),odd=[..even,0,0];System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(odd.AsSpan(4),(uint)odd.Length-8);System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(odd.AsSpan(16),(uint)even.Length-20+1);
        int starts=0;var operation=new BrowserImageOperation(()=>{starts++;throw new InvalidOperationException();});var error=await Assert.ThrowsAsync<ImageJobException>(()=>operation.LoadAsync(odd,Guid.NewGuid(),1,1,default));Assert.Equal(ImageFailure.UnsupportedImage,error.Code);Assert.Equal(0,starts);Assert.True(await operation.TryFinalizeAsync());
    }
    [Fact]public async Task BrowserOperationRejectsUnsupportedBytesBeforeWorkerAndRemainsSingleUse()
    {
        byte[] unsupported="<svg/>"u8.ToArray();int factories=0;var operation=new BrowserImageOperation(()=>{factories++;throw new InvalidOperationException("Must not start.");});await Assert.ThrowsAsync<ImageJobException>(()=>operation.LoadAsync(unsupported,Guid.NewGuid(),1,1,default));Assert.Equal(0,factories);Assert.True(await operation.TryFinalizeAsync());await Assert.ThrowsAsync<InvalidOperationException>(()=>operation.LoadAsync(Png(),Guid.NewGuid(),1,1,default));
    }
    [Fact]public async Task CancelledOriginalBrowserProjectionAndItsWorkerRemainOwnedUntilActualTaskSettlement()
    {
        byte[] png=Png();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var allocations=new List<byte[]>();using var cancellation=new CancellationTokenSource();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){FileAllocationForTest=bytes=>{allocations.Add(bytes);if(allocations.Count==2){entered.TrySetResult();release.Task.GetAwaiter().GetResult();}}};var operation=new BrowserImageOperation(()=>client);var original=operation.LoadAsync(png,Guid.NewGuid(),1,1,cancellation.Token);
        try{await entered.Task.WaitAsync(TimeSpan.FromSeconds(8));cancellation.Cancel();Assert.False(original.IsCompleted);Assert.False(await operation.TryFinalizeAsync());Assert.Contains(png,b=>b!=0);release.SetResult();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>original);Assert.True(await operation.TryFinalizeAsync());Assert.True(client.ObserveExit());Assert.All(allocations,b=>Assert.True(b.AsSpan().IndexOfAnyExcept((byte)0)<0));}
        finally{release.TrySetResult();try{await original;}catch(OperationCanceledException){}}
    }
}
