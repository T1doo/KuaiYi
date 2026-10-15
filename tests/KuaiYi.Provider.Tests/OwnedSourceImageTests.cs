using KuaiYi.Core;
using KuaiYi.Imaging;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class OwnedSourceImageTests
{
    [Fact]public void RequestUsesExactCropBeyondPreviewLimitAndReturnedPngHasSeparateClearableOwnership()
    {
        const int width=1300,height=8;byte[] raw=Enumerable.Range(0,width*height*4).Select(i=>(byte)(i%251)).ToArray();using var source=new OwnedSourceImage(new(new(7,9,7+width,9+height),raw));
        using var preview=source.CreatePreview(1024,1024);Assert.Equal(1024,preview.Bounds.Width);var png=source.CreateRequestPng(4194304);var bytes=png.Bytes;Assert.Equal(new ImageSize(width,height),ImagePolicy.InspectPng(bytes.Span));Assert.InRange(bytes.Length,1,4194304);source.Dispose();Assert.All(raw,b=>Assert.Equal(0,b));Assert.Contains(bytes.ToArray(),b=>b!=0);png.Dispose();Assert.All(bytes.ToArray(),b=>Assert.Equal(0,b));
    }
    [Theory][InlineData(2561,8)][InlineData(8,2561)]
    public void OversizeRequestNeverSilentlyDownsamplesOrDisposesOriginal(int width,int height)
    {byte[] raw=Enumerable.Repeat((byte)7,width*height*4).ToArray();using var source=new OwnedSourceImage(new(new(0,0,width,height),raw));Assert.Throws<InvalidOperationException>(()=>source.CreateRequestPng(4194304));Assert.All(raw,b=>Assert.Equal(7,b));Assert.False(source.IsDisposed);}
    [Fact]public void RequestRejectsEncodedLimitAndCancellationWhileRetainingSource()
    {
        byte[] raw=new byte[256*256*4];new Random(6204).NextBytes(raw);using var source=new OwnedSourceImage(new(new(0,0,256,256),raw));Assert.Throws<InvalidOperationException>(()=>source.CreateRequestPng(65536));Assert.False(source.IsDisposed);
        using var stop=new CancellationTokenSource();stop.Cancel();Assert.Throws<OperationCanceledException>(()=>source.CreateRequestPng(4194304,stop.Token));Assert.Contains(raw,b=>b!=0);source.Dispose();Assert.Throws<ObjectDisposedException>(()=>source.CreateRequestPng(4194304));
    }
    [Theory][InlineData(8,8,8,8)][InlineData(2048,8,1024,4)][InlineData(8,2048,4,1024)][InlineData(2048,1024,1024,512)]
    public void PreviewIsBoundedAspectPreservingDerivativeAndSourceOwnershipIsSeparate(int width,int height,int expectedWidth,int expectedHeight)
    {
        byte[] raw=Enumerable.Range(0,width*height*4).Select(i=>(byte)(i%251)).ToArray();var original=new ScreenshotPixels(new(10,20,10+width,20+height),raw);var image=new OwnedSourceImage(original);using var preview=image.CreatePreview(1024,1024);Assert.Equal((expectedWidth,expectedHeight),(preview.Bounds.Width,preview.Bounds.Height));Assert.True(preview.Bytes.Length<=4*1024*1024);var copy=preview.Bytes;Assert.Equal(raw.AsSpan(0,4).ToArray(),copy.Span[..4].ToArray());Assert.Equal(raw.AsSpan(((height/expectedHeight)*width+width/expectedWidth)*4,4).ToArray(),copy.Span.Slice((expectedWidth+1)*4,4).ToArray());preview.Dispose();Assert.All(copy.ToArray(),b=>Assert.Equal(0,b));Assert.Contains(raw,b=>b!=0);image.Dispose();image.Dispose();Assert.All(raw,b=>Assert.Equal(0,b));Assert.True(image.IsDisposed);Assert.Throws<ObjectDisposedException>(()=>image.CreatePreview(8,8));
    }
    [Theory][InlineData(0,8)][InlineData(8,0)][InlineData(1025,8)][InlineData(8,1025)]
    public void InvalidPreviewBoundsCannotAllocateOrReleaseRetainedSource(int width,int height)
    {var raw=Enumerable.Repeat((byte)7,256).ToArray();using var image=new OwnedSourceImage(new(new(0,0,8,8),raw));Assert.Throws<ArgumentOutOfRangeException>(()=>image.CreatePreview(width,height));Assert.All(raw,b=>Assert.Equal(7,b));}
    [Fact]public void AlreadyCancelledPreviewLeavesOriginalOwnedAndUntouched()
    {var raw=Enumerable.Repeat((byte)7,256).ToArray();using var image=new OwnedSourceImage(new(new(0,0,8,8),raw));using var cancel=new CancellationTokenSource();cancel.Cancel();Assert.Throws<OperationCanceledException>(()=>image.CreatePreview(8,8,cancel.Token));Assert.All(raw,b=>Assert.Equal(7,b));Assert.False(image.IsDisposed);}
    [Fact]public void RejectedOversizeSourceIsConsumedAndZeroed()
    {byte[] raw=new byte[16385*4];Array.Fill(raw,(byte)9);var pixels=new ScreenshotPixels(new(0,0,16385,1),raw);Assert.Throws<ImageJobException>(()=>new OwnedSourceImage(pixels));Assert.All(raw,b=>Assert.Equal(0,b));Assert.Throws<ObjectDisposedException>(()=>pixels.Bytes);}
    [Fact]public void PixelLimitRejectsAndZerosTransferredSourceBeforeAnyPreview()
    {byte[] raw=new byte[6001*4000*4];Array.Fill(raw,(byte)9);var pixels=new ScreenshotPixels(new(0,0,6001,4000),raw);Assert.Throws<ImageJobException>(()=>new OwnedSourceImage(pixels));Assert.Equal(-1,raw.AsSpan().IndexOfAnyExcept((byte)0));}
}
