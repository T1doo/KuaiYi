using System.Diagnostics;
using System.Runtime.InteropServices;
using KuaiYi.Imaging;
using SkiaSharp;
using Xunit;
namespace KuaiYi.Codec.Tests;
public sealed class FileImageOwnerTests
{
    private static string Worker(){for(var p=new DirectoryInfo(AppContext.BaseDirectory);p!=null;p=p.Parent)if(File.Exists(Path.Combine(p.FullName,"SPEC.md")))return Path.Combine(p.FullName,"tools/KuaiYi.ImageDecodeFixture/bin/Release/net10.0/KuaiYi.ImageDecodeFixture.dll");throw new IOException("No fixture.");}
    private sealed class Starter(string fault):IImageProcessStarter{public Process Start(ProcessStartInfo info){info.Environment["KUAIYI_CODEC_FIXTURE_FAULT"]=fault;return Process.Start(info)!;}}
    private static ImageDecodeJob Job(byte[] input)=>new(Guid.NewGuid(),1,1,input.Length,ImageDecodePolicy.Hash(input));
    private static byte[] Input(string kind)
    {
        if(kind=="gif")return GifDecodeTests.Gif(3,2,[GifDecodeTests.Image(1,0,2,1,[0,1]),GifDecodeTests.Image(0,0,3,2,[2,2,2,2,2,2])]);
        using var b=new SKBitmap(new SKImageInfo(3,2,SKColorType.Bgra8888,SKAlphaType.Unpremul));Marshal.Copy(Enumerable.Range(0,24).Select(i=>(byte)(i%4==3?255:i*9)).ToArray(),0,b.GetPixels(),24);using var image=SKImage.FromBitmap(b);using var data=image.Encode(kind=="jpeg"?SKEncodedImageFormat.Jpeg:SKEncodedImageFormat.Png,100);return data.ToArray();
    }
    [Theory][InlineData("png")][InlineData("jpeg")][InlineData("gif")]
    public async Task ActualPreparedFileOwnerAndRequestNeverGainDesktopCoordinates(string kind)
    {
        byte[] input=Input(kind),original=input.ToArray();var allocated=new List<byte[]>();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){FileAllocationForTest=b=>allocated.Add(b)};var owner=await client.PrepareFileAsync(input,Job(input));Assert.True(client.ObserveExit());Assert.False(owner.IsDisposed);Assert.Equal(ImageSourceKind.LocalFile,owner.Kind);Assert.Equal(3,owner.Width);Assert.Equal(2,owner.Height);Assert.Equal(ImageDecodePolicy.Hash(input),owner.Metadata.SourceSha256);Assert.Equal(input.Length,owner.Metadata.SourceByteLength);Assert.False(owner.Metadata.Transform.HasDesktopAnchor);Assert.Throws<InvalidOperationException>(()=>owner.Metadata.Transform.OriginalCrop);Assert.Throws<InvalidOperationException>(()=>owner.Metadata.Transform.NormalizedToDesktop(new NormalizedImagePoint(0,0)));Assert.Equal(kind=="gif"?"使用首帧":null,owner.Notice);
        using var preview=owner.CreatePreview(2,2);Assert.Equal(2,preview.Bounds.Width);using var request=owner.PrepareRequest(4*1024*1024);var bytes=request.Bytes;Assert.Equal(owner.SourceId,request.SourceId);Assert.Equal(ImageSourceKind.LocalFile,request.Kind);Assert.Null(request.NormalizationProof);Assert.Equal(owner.Metadata.Transform,request.Transform);Assert.Equal(owner.Notice,request.Notice);Assert.Equal(new ImageSize(3,2),ImagePolicy.InspectPng(bytes.Span));
        owner.Dispose();Assert.True(owner.IsDisposed);Assert.All(allocated,b=>Assert.True(b.AsSpan().IndexOfAnyExcept((byte)0)<0));Assert.Contains(bytes.ToArray(),b=>b!=0);request.Dispose();Assert.True(bytes.Span.IndexOfAnyExcept((byte)0)<0);Assert.Equal(original,input);Assert.Throws<ObjectDisposedException>(()=>owner.CreatePreview(2,2));Assert.Throws<ObjectDisposedException>(()=>owner.PrepareRequest(4194304));
    }
    [Theory][InlineData("observer",1)][InlineData("observer",2)][InlineData("cancel",1)][InlineData("cancel",2)]
    public async Task ActualFileCopiesClearWhenProjectionThrowsOrCancels(string fault,int at)
    {
        byte[] input=Input("gif");var allocated=new List<byte[]>();using var cancel=new CancellationTokenSource();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){FileAllocationForTest=b=>{allocated.Add(b);if(allocated.Count==at){if(fault=="cancel")cancel.Cancel();else throw new IOException("Synthetic file copy failure.");}}};await Assert.ThrowsAnyAsync<Exception>(()=>client.PrepareFileAsync(input,Job(input),cancel.Token));Assert.NotEmpty(allocated);Assert.All(allocated,b=>Assert.True(b.AsSpan().IndexOfAnyExcept((byte)0)<0));Assert.True(client.ObserveExit());
    }
    [Fact]public async Task CancelledOriginalProjectionRemainsOwnedUntilItsRealTaskSettles()
    {
        byte[] input=Input("png");var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var allocated=new List<byte[]>();using var cancel=new CancellationTokenSource();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){FileAllocationForTest=b=>{allocated.Add(b);if(allocated.Count==2){entered.SetResult();release.Task.GetAwaiter().GetResult();}}};var task=client.PrepareFileAsync(input,Job(input),cancel.Token);await entered.Task.WaitAsync(TimeSpan.FromSeconds(8));cancel.Cancel();Assert.False(task.IsCompleted);Assert.False(await client.TryFinalizeAsync());Assert.Contains(allocated[0],b=>b!=0);release.SetResult();await Assert.ThrowsAnyAsync<Exception>(()=>task);Assert.All(allocated,b=>Assert.True(b.AsSpan().IndexOfAnyExcept((byte)0)<0));Assert.True(await client.TryFinalizeAsync());
    }
    [Theory][InlineData("extra-byte")][InlineData("exit7")][InlineData("gif-source")]
    public async Task NoFileOwnerOrProjectionBeforeSourceBindingFullEofAndActualExit(string fault)
    {byte[] input=Input("gif");int allocations=0;await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),new Starter(fault)){FileAllocationForTest=_=>allocations++};await Assert.ThrowsAnyAsync<Exception>(()=>client.PrepareFileAsync(input,Job(input)));Assert.Equal(0,allocations);Assert.True(client.ObserveExit());}
    [Theory][InlineData(0)][InlineData(65535)][InlineData(4194305)]
    public async Task InvalidRequestCapsCannotMutateTheRetainedFileOwner(int cap)
    {byte[] input=Input("png");await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var owner=await client.PrepareFileAsync(input,Job(input));Assert.Throws<ArgumentOutOfRangeException>(()=>owner.PrepareRequest(cap));Assert.False(owner.IsDisposed);using var valid=owner.PrepareRequest(4194304);Assert.False(valid.Transform.HasDesktopAnchor);}
    [Fact]public async Task StricterApiCapOnlyRejectsAndNeverResizesReencodesOrConsumesTheFile()
    {
        byte[] raw=new byte[256*256*4];new Random(710024).NextBytes(raw);for(int i=3;i<raw.Length;i+=4)raw[i]=255;using var pixels=new ScreenshotPixels(new(0,0,256,256),raw);using var input=ScreenshotPngEncoder.Encode(pixels);byte[] bytes=input.Bytes.ToArray();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var owner=await client.PrepareFileAsync(bytes,Job(bytes));using var first=owner.PrepareRequest(4194304);Assert.True(first.Bytes.Length>65536);byte[] exact=first.Bytes.ToArray();Assert.Throws<ScreenshotNormalizationException>(()=>owner.PrepareRequest(65536));Assert.False(owner.IsDisposed);using var second=owner.PrepareRequest(4194304);Assert.Equal(exact,second.Bytes.ToArray());Assert.Equal(first.Transform,second.Transform);
    }
    [Fact]public async Task FinalizedUnusedFileOperationCannotStartAnotherRead()
    {
        int factories=0;var operation=new FileImageOperation(()=>{factories++;throw new InvalidOperationException();});Assert.True(await operation.TryFinalizeAsync());await Assert.ThrowsAsync<InvalidOperationException>(()=>operation.LoadAsync("PUBLIC",Guid.NewGuid(),1,1,default));Assert.Equal(0,factories);
    }

    [Fact]public async Task FileOwnerPreservesExifMappingAndNormalizedRasterDimensionsWithoutADesktopCrop()
    {
        byte[] jpeg=Input("jpeg"),tiff=new byte[26];tiff[0]=tiff[1]=(byte)'I';tiff[2]=42;tiff[4]=8;tiff[8]=1;tiff[10]=0x12;tiff[11]=1;tiff[12]=3;tiff[14]=1;tiff[18]=6;
        byte[] input=[255,216,255,225,0,34,69,120,105,102,0,0,..tiff,..jpeg[2..]];await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var owner=await client.PrepareFileAsync(input,Job(input));Assert.Equal(2,owner.Width);Assert.Equal(3,owner.Height);Assert.Equal(new ImageSize(3,2),owner.Metadata.Transform.OriginalSize);Assert.Equal(ImageOrientation.ExifRightTop,owner.Metadata.Transform.Orientation);Assert.False(owner.Metadata.Transform.HasDesktopAnchor);Assert.Equal(new NormalizedImagePoint(2,0),owner.Metadata.Transform.OriginalToNormalized(new OriginalImagePoint(0,0)));using var request=owner.PrepareRequest(4194304);Assert.Equal(new ImageSize(2,3),ImagePolicy.InspectPng(request.Bytes.Span));Assert.Equal(owner.Metadata.Transform,request.Transform);Assert.Equal(ImageDecodePolicy.Hash(input),owner.Metadata.SourceSha256);
    }

}
