using KuaiYi.Core;
using KuaiYi.Imaging;
using System.Runtime.InteropServices;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class ScreenshotAreaRoutingTests
{
    internal static byte[] Pattern(int width=3840,int height=6)
    {
        byte[] pixels=new byte[width*height*4];byte[] tile=[0,50,100,10,60,110,20,70,120];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++){int i=(y*width+x)*4;pixels[i]=pixels[i+1]=pixels[i+2]=tile[(y%3)*3+x%3];pixels[i+3]=255;}return pixels;
    }
    [Fact]public void NonconstantScreenshotUsesFullAreaPixelsAndItsPhysicalCropAnchor()
    {
        byte[] raw=Pattern(),original=raw.ToArray();var bounds=new PixelRect(-3841,50,-1,56);using var source=new OwnedSourceImage(new(bounds,raw));using var png=source.PrepareRequest(4194304);
        Assert.Equal(source.SourceId,png.SourceId);Assert.Equal(bounds,png.Transform.OriginalCrop);Assert.Equal(new ImageSize(2560,4),png.Transform.NormalizedSize);Assert.Equal(new NormalizedImagePoint(2560,4),png.Transform.DesktopToNormalized(new DesktopImagePoint(-1,56)));
        var rows=ScreenshotNormalizationTests.DecodeRgb(png.Bytes);byte[] expected=[20,87,33,100];
        for(int y=0;y<4;y++){Assert.Equal(0,rows[y*(2560*3+1)]);for(int x=0;x<2560;x++)Assert.Equal(Enumerable.Repeat(expected[(y%2)*2+x%2],3),rows.AsSpan(y*(2560*3+1)+1+x*3,3).ToArray());}Assert.Equal(original,raw);
    }
    private static byte[] Actual(ReadOnlyMemory<byte> bytes)
    {Assert.True(MemoryMarshal.TryGetArray(bytes,out var segment));Assert.Equal(0,segment.Offset);Assert.Equal(segment.Array!.Length,segment.Count);return segment.Array;}
    [Fact]public void GeneralSourceCopyRasterAndAllPngWorkArraysClearAndOnlyTheReturnedPngTransfers()
    {
        byte[] raw=Pattern(),unchanged=raw.ToArray();var buffers=new List<byte[]>();using var source=new OwnedSourceImage(new(new(-17,-19,3823,-13),raw));var png=source.PrepareRequestForTest(4194304,default,buffers.Add);var final=Actual(png.Bytes);
        Assert.Equal(ScreenshotNormalizationProof.AreaWhiteCompositeDownscale,png.Proof);Assert.Contains(buffers,b=>ReferenceEquals(b,final));Assert.True(buffers.Count>=5);Assert.All(buffers.Where(b=>!ReferenceEquals(b,final)),b=>Assert.True(b.AsSpan().IndexOfAnyExcept((byte)0)<0));Assert.Equal(unchanged,raw);png.Dispose();Assert.All(buffers,b=>Assert.True(b.AsSpan().IndexOfAnyExcept((byte)0)<0));
    }
    [Theory][InlineData("copy",false)][InlineData("raster",false)][InlineData("row",false)][InlineData("final",false)][InlineData("copy",true)][InlineData("raster",true)][InlineData("row",true)][InlineData("final",true)]
    public void CancellationAndAllocationFailureClearActualCopyRasterAndPngArrays(string stage,bool cancel)
    {
        byte[] raw=Pattern(),unchanged=raw.ToArray();using var source=new OwnedSourceImage(new(new(0,0,3840,6),raw));int total=0;using(var probe=source.PrepareRequestForTest(4194304,default,_=>total++)){}int target=stage switch{"copy"=>1,"raster"=>2,"row"=>3,_=>total},count=0;bool triggered=false;var buffers=new List<byte[]>();using var cancellation=new CancellationTokenSource();
        void Observe(byte[] bytes){buffers.Add(bytes);if(++count!=target)return;triggered=true;if(cancel)cancellation.Cancel();else throw new InvalidOperationException("SYNTHETIC_SCREENSHOT_ALLOCATION");}
        if(cancel)Assert.ThrowsAny<OperationCanceledException>(()=>source.PrepareRequestForTest(4194304,cancellation.Token,Observe));else Assert.ThrowsAny<Exception>(()=>source.PrepareRequestForTest(4194304,cancellation.Token,Observe));
        Assert.True(triggered);Assert.True(count>=target);Assert.All(buffers,b=>Assert.True(b.AsSpan().IndexOfAnyExcept((byte)0)<0));Assert.Equal(unchanged,raw);Assert.False(source.IsDisposed);
    }
    [Fact]public void SourceCanBeDisposedAfterTheCopyLockWithoutInvalidatingOrRetainingTheIndependentWork()
    {
        byte[] raw=Pattern();var buffers=new List<byte[]>();var source=new OwnedSourceImage(new(new(0,0,3840,6),raw));int count=0;using var png=source.PrepareRequestForTest(4194304,default,b=>{buffers.Add(b);if(++count==2)source.Dispose();});
        Assert.True(source.IsDisposed);Assert.True(raw.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.Equal(new ImageSize(2560,4),ImagePolicy.InspectPng(png.Bytes.Span));Assert.All(buffers.Where(b=>!ReferenceEquals(b,Actual(png.Bytes))),b=>Assert.True(b.AsSpan().IndexOfAnyExcept((byte)0)<0));
    }
    [Fact]public void GeneralAreaAlphaRoundsOnceAndRetainsLastEdgeContribution()
    {
        byte[] raw=new byte[3840*3*4];for(int y=0;y<3;y++)for(int x=0;x<3840;x++){int i=(y*3840+x)*4;raw[i]=raw[i+1]=raw[i+2]=30;raw[i+3]=(byte)(x%3==2?3:1);}using var source=new OwnedSourceImage(new(new(0,0,3840,3),raw));using var png=source.PrepareRequest(4194304);var rows=ScreenshotNormalizationTests.DecodeRgb(png.Bytes);
        // Each second destination column mixes one alpha1 and two alpha3 contributions: 252.94 ->253;
        // the first covers alpha1 only:254.117 ->254. Neither uses a previously rounded source.
        for(int y=0;y<2;y++)for(int x=0;x<2560;x++)Assert.Equal(Enumerable.Repeat(x%2==0?(byte)254:(byte)253,3),rows.AsSpan(y*(2560*3+1)+1+x*3,3).ToArray());
    }
    [Fact]public void LegacyNativeSizeExportStillRejectsGeneralOversizeAndExactPreparedCapCannotRetry()
    {
        using var source=new OwnedSourceImage(new(new(0,0,3840,6),Pattern()));Assert.Throws<InvalidOperationException>(()=>source.CreateRequestPng(4194304));
        byte[] raw=new byte[256*256*4];new Random(561029).NextBytes(raw);for(int i=3;i<raw.Length;i+=4)raw[i]=255;using var noisy=new OwnedSourceImage(new(new(-9,-7,247,249),raw));using var probe=noisy.PrepareRequest(4194304);int cap=probe.Bytes.Length;Assert.InRange(cap,65536,4194304);using var exact=noisy.PrepareRequest(cap);Assert.Equal(probe.Bytes.ToArray(),exact.Bytes.ToArray());Assert.Throws<ScreenshotNormalizationException>(()=>noisy.PrepareRequest(cap-1));
    }
    [Theory][InlineData(3001,17)][InlineData(17,3001)]
    public void NonintegerAreaFitIncludesBothLastEdgesAndKeepsTheExactRoundedAxes(int width,int height)
    {
        byte[] raw=ScreenshotNormalizationTests.Solid(width,height,0,0,0,255);for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(x==width-1||y==height-1){int i=(y*width+x)*4;raw[i]=raw[i+1]=raw[i+2]=255;}
        var bounds=new PixelRect(-715,-923,width-715,height-923);using var source=new OwnedSourceImage(new(bounds,raw));using var png=source.PrepareRequest(4194304);var rows=ScreenshotNormalizationTests.DecodeRgb(png.Bytes);int w=(int)png.Transform.NormalizedSize.Width,h=(int)png.Transform.NormalizedSize.Height;
        Assert.Equal(width>height?new ImageSize(2560,15):new ImageSize(15,2560),png.Transform.NormalizedSize);Assert.Equal((double)w/width,png.Transform.ScaleX);Assert.Equal((double)h/height,png.Transform.ScaleY);
        decimal Overlap(decimal left,decimal right,int last)=>Math.Max(0,Math.Min(right,last+1)-Math.Max(left,last));
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            decimal fx=Overlap((decimal)x*width/w,(decimal)(x+1)*width/w,width-1)/((decimal)width/w),fy=Overlap((decimal)y*height/h,(decimal)(y+1)*height/h,height-1)/((decimal)height/h);
            byte expected=(byte)decimal.Round(255*(fx+fy-fx*fy),0,MidpointRounding.AwayFromZero);Assert.Equal(Enumerable.Repeat(expected,3),rows.AsSpan(y*(w*3+1)+1+x*3,3).ToArray());
        }
        Assert.Equal(new DesktopImageRectangle(bounds.Left,bounds.Top,width,height),png.Transform.NormalizedToDesktop(new NormalizedImageRectangle(0,0,w,h)));
    }
    [Fact]public void StrictApiCapAfterGeneralDownscaleClearsAllWorkAndNeverTriesAnotherSize()
    {
        byte[] raw=new byte[3840*96*4];new Random(716025).NextBytes(raw);for(int i=3;i<raw.Length;i+=4)raw[i]=255;var original=raw.ToArray();using var source=new OwnedSourceImage(new(new(0,0,3840,96),raw));using var success=source.PrepareRequest(4194304);Assert.Equal(new ImageSize(2560,64),success.Transform.NormalizedSize);Assert.True(success.Bytes.Length>65536);var buffers=new List<byte[]>();Assert.Throws<ScreenshotNormalizationException>(()=>source.PrepareRequestForTest(65536,default,buffers.Add));Assert.All(buffers,b=>Assert.True(b.AsSpan().IndexOfAnyExcept((byte)0)<0));Assert.Equal(original,raw);
    }
    [Theory][InlineData("dimensions")][InlineData("anchor")][InlineData("proof")][InlineData("source")]
    public void PreparedOwnerRejectsMismatchedPngMappingAndClearsTheActualTransferredArray(string fault)
    {
        using var pixels=new ScreenshotPixels(new(0,0,8,8),ScreenshotNormalizationTests.Solid(8,8,0,0,0,255));var png=ScreenshotPngEncoder.Encode(pixels);var bytes=Actual(png.Bytes);var map=fault=="anchor"?ImageTransform.PlanEncoded(new(8,8),ImageOrientation.ExifTopLeft):ImageTransform.Plan(new(0,0,fault=="dimensions"?9:8,8));
        Assert.Throws<InvalidDataException>(()=>new PreparedScreenshot(fault=="source"?Guid.Empty:Guid.NewGuid(),png,map,fault=="proof"?ScreenshotNormalizationProof.AreaWhiteCompositeDownscale:ScreenshotNormalizationProof.NativeSizeWhiteComposite));Assert.True(bytes.AsSpan().IndexOfAnyExcept((byte)0)<0);
    }
}
