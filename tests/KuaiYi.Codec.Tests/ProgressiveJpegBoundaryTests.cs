using System.Buffers.Binary;
using System.Runtime.ExceptionServices;
using KuaiYi.Imaging;
using KuaiYi.ImageWorker;
using Xunit;
namespace KuaiYi.Codec.Tests;

public sealed partial class RasterDecodeTests
{
    private static byte[] CompleteZeroProgression(int width=8,int height=8,int depth=0,int comments=0)
    {
        var b=new ProgressiveBuilder(width,height);for(int i=0;i<comments;i++)b.Segment(254,[]);
        int blocks=checked(((width+7)/8)*((height+7)/8));string zeros=new('0',blocks);b.Scan(0,0,0,0,zeros);b.Scan(1,63,0,depth,zeros);
        for(int ah=depth;ah>0;ah--)b.Scan(1,63,ah,ah-1,zeros);return b.Finish();
    }
    private static byte[] ManyProgressiveScans(int count)
    {
        var b=new ProgressiveBuilder();b.Scan(0,0,0,count==65?1:0,"0");int firstEnd=count==63?2:1;b.Scan(1,firstEnd,0,0,"0");
        for(int k=firstEnd+1;k<64;k++)b.Scan(k,k,0,0,"0");if(count==65)b.Scan(0,0,1,0,"0");return b.Finish();
    }
    [Theory][InlineData(63)][InlineData(64)]
    public async Task CompleteProgressiveScanBoundaryRemainsUsableInActualWorker(int count)
    {
        byte[] input=ManyProgressiveScans(count);Assert.Equal(count,ScanParts(input).Count);await using var client=Client();using var raster=await client.DecodeAsync(input,Job(input));Assert.Equal(8*8*4,raster.Bytes.Length);Assert.Equal(128,raster.Bytes.Span[0]);Assert.True(client.ObserveExit());
    }
    [Fact]public async Task AValid65thProgressiveScanExceedsTheBoundWithoutGrantingNativePermission()
    {
        byte[] input=ManyProgressiveScans(65);Assert.Equal(65,ScanParts(input).Count);int native=0;var error=Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,Job(input),default,_=>native++));Assert.Equal(ImageFailure.UnsupportedImage,error.Code);Assert.Equal(0,native);
        await using var client=Client();var remote=await Assert.ThrowsAsync<ImageJobException>(()=>client.DecodeAsync(input,Job(input)));Assert.Equal(error.Code,remote.Code);Assert.True(client.ObserveExit());
    }
    [Theory][InlineData(1023)][InlineData(1024)]
    public void ProgressiveLengthBearingSegmentBoundaryIsCheckedWithoutCountingRestartAsMetadata(int count)
    {
        // Constructor four segments, plus two scans; empty comments account for the remaining metadata.
        byte[] input=CompleteZeroProgression(comments:count-6);ProgressiveJpegScan.Validate(input,new(8,8),default);
    }
    [Fact]public void ProgressiveSegmentOverflowIsRejectedBothBeforeFrameAndAfterFrame()
    {
        byte[] after=CompleteZeroProgression(comments:1019);var late=Assert.Throws<ImageJobException>(()=>ProgressiveJpegScan.Validate(after,new(8,8),default));Assert.Equal(ImageFailure.UnsupportedImage,late.Code);
        byte[] original=CompleteZeroProgression();byte[] before=[..original[..2],..Enumerable.Range(0,1023).SelectMany(_=>new byte[]{255,254,0,2}),..original[2..]];int native=0;var early=Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(before,Job(before),default,_=>native++));Assert.Equal(ImageFailure.UnsupportedImage,early.Code);Assert.Equal(0,native);
        List<ulong[]> masks=[];Assert.Throws<ImageJobException>(()=>ProgressiveJpegScan.Validate(before,new(8,8),default,masks.Add));Assert.Empty(masks);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task LongHeaderKeepsLegacyBaselineAcceptanceAndNeverGrantsSof2(bool baseline)
    {
        byte[] original=baseline?BaselineJpeg(8,8,1,1,true):CompleteZeroProgression();byte[] input=[..original[..2],..Enumerable.Range(0,1025).SelectMany(_=>new byte[]{255,254,0,2}),..original[2..]];
        Assert.False(ProgressiveJpegScan.AppliesTo(input,default));int native=0;
        if(baseline)
        {
            BaselineJpegScan.Validate(input,new(8,8),default);using var direct=ImageRasterDecoder.Decode(input,Job(input),default,_=>native++);Assert.Equal(1,native);Assert.Equal(128,direct.Bytes.Span[0]);
            await using var client=Client();using var raster=await client.DecodeAsync(input,Job(input));Assert.Equal(direct.Bytes.ToArray(),raster.Bytes.ToArray());Assert.True(client.ObserveExit());
        }
        else
        {
            var old=Assert.Throws<ImageJobException>(()=>BaselineJpegScan.Validate(input,new(8,8),default));Assert.Equal(ImageFailure.UnsupportedImage,old.Code);
            var error=Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,Job(input),default,_=>native++));Assert.Equal(old.Code,error.Code);Assert.Equal(0,native);
            await using var client=Client();var remote=await Assert.ThrowsAsync<ImageJobException>(()=>client.DecodeAsync(input,Job(input)));Assert.Equal(error.Code,remote.Code);Assert.True(client.ObserveExit());
        }
    }
    [Fact]public void ProgressiveWholeImageWorkAndMaskAccountingRejectsExtraRefinementBeforeNative()
    {
        // 16000x1500 is exactly24MP. Partial edge blocks are included in accounting.
        byte[] accepted=CompleteZeroProgression(16000,1500,9);List<ulong[]> masks=[];int completed=0;
        ProgressiveJpegScan.Validate(accepted,new(16000,1500),default,masks.Add,()=>completed++);Assert.Equal(11,completed);Assert.Equal(3_008_000,masks.Sum(a=>(long)a.Length*8));Assert.All(masks,a=>Assert.All(a,v=>Assert.Equal(0UL,v)));
        byte[] rejected=CompleteZeroProgression(16000,1500,10);int native=0;var error=Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(rejected,Job(rejected),default,_=>native++));Assert.Equal(ImageFailure.UnsupportedImage,error.Code);Assert.Equal(0,native);
    }
    [Fact]public void ExistingImageDimensionsLimitPrecedesAnyProgressiveMaskAllocation()
    {
        byte[] input=CompleteZeroProgression();List<ulong[]> masks=[];var error=Assert.Throws<ImageJobException>(()=>ProgressiveJpegScan.Validate(input,new(16384,16384),default,masks.Add));Assert.Equal(ImageFailure.ImageTooLarge,error.Code);Assert.Empty(masks);
    }
    [Fact]public void PrecancelledProgressiveImageCannotAllocateNativeInput()
    {byte[] input=ProgressiveFixture("420");using var cancel=new CancellationTokenSource();cancel.Cancel();int native=0;Assert.ThrowsAny<OperationCanceledException>(()=>ImageRasterDecoder.Decode(input,Job(input),cancel.Token,_=>native++));Assert.Equal(0,native);}
    [Theory][InlineData(ImageSourceKind.LocalFile)][InlineData(ImageSourceKind.Browser)]
    public async Task PreparedProgressiveExifImageKeepsExactSourceAndNoDesktopAnchor(ImageSourceKind kind)
    {
        byte[] original=ProgressiveFixture("444");byte[] exif=[69,120,105,102,0,0,73,73,42,0,8,0,0,0,1,0,18,1,3,0,1,0,0,0,6,0,0,0,0,0,0,0];byte[] input=[..original[..2],255,225,0,34,..exif,..original[2..]];
        await using var client=Client();using var image=kind==ImageSourceKind.LocalFile?await client.PrepareFileAsync(input,Job(input)):await client.PrepareBrowserAsync(input,Job(input));Assert.Equal(kind,image.Kind);Assert.Equal(9,image.Width);Assert.Equal(17,image.Height);Assert.False(image.Metadata.Transform.HasDesktopAnchor);Assert.Equal(ImageOrientation.ExifRightTop,image.Metadata.Transform.Orientation);Assert.Null(image.Notice);
        using var request=image.PrepareRequest(4*1024*1024);Assert.Equal(kind,request.Kind);Assert.Equal(image.SourceId,request.SourceId);Assert.Null(request.NormalizationProof);Assert.Equal(new ImageSize(9,17),ImagePolicy.InspectPng(request.Bytes.Span));Assert.True(client.ObserveExit());
    }
    [Fact]public async Task ProgressiveOriginalChildMustExitBeforeCancelledRasterCanBeReleased()
    {
        byte[] input=ProgressiveFixture("420-restart");byte[]? owned=null;using var cancel=new CancellationTokenSource();using var guard=new CancellationTokenSource(TimeSpan.FromSeconds(10));var received=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),new Starter("hold-exit")){DecodeAllocationForTest=bytes=>owned=bytes,DecodeReceivedForTest=()=>received.TrySetResult()};var original=client.DecodeAsync(input,Job(input),cancel.Token);List<Exception> errors=[];Exception? observed=null;
        try
        {await received.Task.WaitAsync(guard.Token);Assert.False(original.IsCompleted);cancel.Cancel();observed=await Assert.ThrowsAnyAsync<Exception>(()=>original);Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.True(client.ObserveExit());}
        catch(Exception error){errors.Add(error);}
        finally
        {
            try{cancel.Cancel();}catch(Exception error){errors.Add(error);}try{await original;}catch(OperationCanceledException)when(cancel.IsCancellationRequested){}catch(Exception error){if(!ReferenceEquals(error,observed)&&!errors.Any(old=>ReferenceEquals(old,error)))errors.Add(error);}
            try{await client.DisposeAsync();}catch(Exception error){errors.Add(error);}
        }
        if(errors.Count==1)ExceptionDispatchInfo.Capture(errors[0]).Throw();if(errors.Count>1)throw new AggregateException("Progressive original cancellation and cleanup failed.",errors);
    }
}
