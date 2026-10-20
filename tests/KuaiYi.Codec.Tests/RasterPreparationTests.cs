using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using KuaiYi.Contracts;
using KuaiYi.ImageWorker;
using KuaiYi.Imaging;
using SkiaSharp;
using Xunit;
namespace KuaiYi.Codec.Tests;

public sealed class RasterPreparationTests
{
    private static string Worker()
    {
        if(Environment.GetEnvironmentVariable("KUAIYI_PREP_BASELINE_WORKER") is {} path)return path;
        for(var p=new DirectoryInfo(AppContext.BaseDirectory);p!=null;p=p.Parent)
            if(File.Exists(Path.Combine(p.FullName,"SPEC.md")))return Path.Combine(p.FullName,"tools/KuaiYi.ImageDecodeFixture/bin/Release/net10.0/KuaiYi.ImageDecodeFixture.dll");
        throw new IOException("Repository unavailable.");
    }
    private sealed class Starter(string? fault=null):IImageProcessStarter
    {public int Starts;public Process Start(ProcessStartInfo info){Starts++;if(fault!=null)info.Environment["KUAIYI_CODEC_FIXTURE_FAULT"]=fault;return Process.Start(info)??throw new IOException("No synthetic worker.");}}
    private static ImageWorkerClient Client(Starter? starter=null)=>new(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),starter);
    private static ImageDecodeJob Job(byte[] bytes)=>new(Guid.NewGuid(),1,1,bytes.Length,ImageDecodePolicy.Hash(bytes));
    private static byte[] Encode(int width,int height,byte[] pixels,SKEncodedImageFormat format=SKEncodedImageFormat.Png)
    {
        using var bitmap=new SKBitmap(new SKImageInfo(width,height,SKColorType.Bgra8888,SKAlphaType.Unpremul));Marshal.Copy(pixels,0,bitmap.GetPixels(),pixels.Length);
        using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(format,100);return data.ToArray();
    }
    private static byte[] Gray(params byte[] values)=>values.SelectMany(value=>new byte[]{value,value,value,255}).ToArray();
    private static byte[] Exif(byte[] jpeg,int orientation,bool little=true)
    {
        byte[] tiff=new byte[26];tiff[0]=tiff[1]=(byte)(little?'I':'M');
        void U16(int offset,ushort value){if(little)BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(offset),value);else BinaryPrimitives.WriteUInt16BigEndian(tiff.AsSpan(offset),value);}
        void U32(int offset,uint value){if(little)BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(offset),value);else BinaryPrimitives.WriteUInt32BigEndian(tiff.AsSpan(offset),value);}
        U16(2,42);U32(4,8);U16(8,1);U16(10,0x112);U16(12,3);U32(14,1);U16(18,(ushort)orientation);
        return AddApp1(jpeg,[.."Exif\0\0"u8.ToArray(),..tiff]);
    }
    private static byte[] AddApp1(byte[] jpeg,byte[] payload)=>[..jpeg[..2],255,225,(byte)((payload.Length+2)>>8),(byte)(payload.Length+2),..payload,..jpeg[2..]];
    private static int[] Order(int orientation)=>orientation switch
    {1=>[0,1,2,3,4,5],2=>[2,1,0,5,4,3],3=>[5,4,3,2,1,0],4=>[3,4,5,0,1,2],5=>[0,3,1,4,2,5],6=>[3,0,4,1,5,2],7=>[5,2,4,1,3,0],8=>[2,5,1,4,0,3],_=>throw new ArgumentOutOfRangeException(nameof(orientation))};
    [Theory][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)][InlineData(8)]
    public async Task ActualJpegChildAppliesEveryExifDirectionToAsymmetricPixelsAndRetainsExactMapping(int orientation)
    {
        byte[] original=Encode(3,2,Gray(10,50,90,130,180,230),SKEncodedImageFormat.Jpeg);
        await using var rawClient=Client();using var raw=await rawClient.DecodeAsync(original,Job(original));Assert.NotEqual(raw.Bytes.Span[0],raw.Bytes.Span[20]);
        byte[] encoded=Exif(original,orientation,orientation%2==1),unchanged=encoded.ToArray();var job=Job(encoded);
        await using var client=Client();using var raster=await client.PrepareAsync(encoded,job,3);
        Assert.Equal(job with{PrepareLongEdge=3},raster.Metadata.Job);Assert.Equal("BGRA8888-opaque-white",raster.Metadata.PixelFormat);Assert.Equal(new(3,2,(ImageOrientation)orientation),raster.Metadata.PreparedInfo);
        Assert.Equal(orientation<5?3:2,raster.Metadata.Width);Assert.Equal(orientation<5?2:3,raster.Metadata.Height);Assert.Equal(raster.Metadata.Width*4,raster.Metadata.Stride);Assert.Equal(24,raster.Bytes.Length);
        for(int i=0;i<6;i++)Assert.Equal(raw.Bytes.Slice(Order(orientation)[i]*4,4).ToArray(),raster.Bytes.Slice(i*4,4).ToArray());
        Assert.Equal(encoded,unchanged);Assert.True(client.ObserveExit());Assert.NotNull(raster.Transform);Assert.False(raster.Transform.HasDesktopAnchor);Assert.False(raster.Transform.Downscaled);Assert.Equal((ImageOrientation)orientation,raster.Transform.Orientation);
    }
    [Theory][InlineData(1,0,0)][InlineData(2,3,0)][InlineData(3,3,2)][InlineData(4,0,2)][InlineData(5,0,0)][InlineData(6,2,0)][InlineData(7,2,3)][InlineData(8,0,3)]
    public void EdgeMappingsAndInversesUseOrientationWithoutInventingADesktopAnchor(int orientation,int zeroX,int zeroY)
    {
        var transform=ImageTransform.PlanEncoded(new(3,2),(ImageOrientation)orientation,3);Assert.Contains("HasDesktopAnchor = False",transform.ToString());Assert.Equal(new NormalizedImagePoint(zeroX,zeroY),transform.OriginalToNormalized(new OriginalImagePoint(0,0)));
        Assert.Equal(new OriginalImagePoint(0,0),transform.NormalizedToOriginal(new NormalizedImagePoint(zeroX,zeroY)));
        Assert.Equal(new NormalizedImageRectangle(0,0,orientation<5?3:2,orientation<5?2:3),transform.OriginalToNormalized(new OriginalImageRectangle(0,0,3,2)));
        Assert.Equal(new OriginalImageRectangle(0,0,3,2),transform.RelativeToOriginal(new(0,0,1,1)));
        Assert.Throws<InvalidOperationException>(()=>transform.OriginalCrop);Assert.Throws<InvalidOperationException>(()=>transform.NormalizedToDesktop(new NormalizedImagePoint(0,0)));Assert.Throws<InvalidOperationException>(()=>transform.DesktopToNormalized(new DesktopImagePoint(0,0)));
        Assert.Throws<InvalidOperationException>(()=>transform.NormalizedToDesktop(new NormalizedImageRectangle(0,0,1,1)));Assert.Throws<InvalidOperationException>(()=>transform.DesktopToNormalized(new DesktopImageRectangle(0,0,1,1)));
        Assert.Throws<ArgumentOutOfRangeException>(()=>transform.OriginalToNormalized(new OriginalImagePoint(Math.BitIncrement(3d),0)));Assert.Throws<ArgumentOutOfRangeException>(()=>transform.OriginalToNormalized(new OriginalImageRectangle(0,0,3,Math.BitIncrement(2d))));
    }
    [Fact]public void FractionalScaleAndReflectedRectangleRoundTripsPreserveEntireImage()
    {
        var random=new Random(617031);
        for(int o=1;o<=8;o++)
        {
            var transform=ImageTransform.PlanEncoded(new(3001,1001),(ImageOrientation)o);Assert.True(transform.Downscaled);Assert.Equal(o<5?new ImageSize(2560,854):new ImageSize(854,2560),transform.NormalizedSize);
            for(int i=0;i<40;i++)
            {
                double x=random.NextDouble()*3001,y=random.NextDouble()*1001;var region=new OriginalImageRectangle(x,y,random.NextDouble()*(3001-x),random.NextDouble()*(1001-y));var back=transform.NormalizedToOriginal(transform.OriginalToNormalized(region));
                Assert.InRange(Math.Abs(back.X-region.X),0,1e-9);Assert.InRange(Math.Abs(back.Y-region.Y),0,1e-9);Assert.InRange(Math.Abs(back.Width-region.Width),0,1e-9);Assert.InRange(Math.Abs(back.Height-region.Height),0,1e-9);
            }
        }
    }
    [Fact]public async Task ActualPngNonIntegerAreaAveragePreservesBothEdgesAndVariedContent()
    {
        byte[] encoded=Encode(5,3,Gray(0,50,100,150,200,10,60,110,160,210,20,70,120,170,220));await using var client=Client();using var result=await client.PrepareAsync(encoded,Job(encoded),3);
        Assert.Equal(3,result.Metadata.Width);Assert.Equal(2,result.Metadata.Height);Assert.Equal(Gray(23,103,183,37,117,197),result.Bytes.ToArray());Assert.Equal(3d/5,result.Transform!.ScaleX);Assert.Equal(2d/3,result.Transform.ScaleY);
    }
    [Fact]public async Task AreaSamplingRetainsThinEdgeContributionAndDoesNotAliasACheckerboard()
    {
        byte[] edge=Encode(5,1,Gray(0,0,0,0,255));await using var edgeClient=Client();using var first=await edgeClient.PrepareAsync(edge,Job(edge),3);Assert.Equal(Gray(0,0,153),first.Bytes.ToArray());
        byte[] checker=Encode(4,4,Gray(0,255,0,255,255,0,255,0,0,255,0,255,255,0,255,0));await using var checkerClient=Client();using var second=await checkerClient.PrepareAsync(checker,Job(checker),2);Assert.Equal(Gray(128,128,128,128),second.Bytes.ToArray());
    }
    [Fact]public async Task AlphaIsCompositedBeforeAreaSamplingAndOnlyNumericMetadataSurvives()
    {
        byte[] encoded=Encode(3,1,[0,0,0,0,0,0,0,128,0,0,0,255]);await using var client=Client();using var raster=await client.PrepareAsync(encoded,Job(encoded),1);Assert.Equal(Gray(127),raster.Bytes.ToArray());
        byte[] jpeg=AddApp1(Exif(Encode(3,2,Gray(10,50,90,130,180,230),SKEncodedImageFormat.Jpeg),6),"SYNTHETIC-METADATA-PATH-GPS"u8.ToArray());await using var secondClient=Client();using var second=await secondClient.PrepareAsync(jpeg,Job(jpeg),3);Assert.DoesNotContain("SYNTHETIC",System.Text.Encoding.UTF8.GetString(ImageJson.Serialize(second.Metadata)));Assert.Equal(1,second.Metadata.Orientation);Assert.Equal(1,second.Metadata.Frames);
    }
    [Fact]public void AlphaAndAreaWeightsRoundOnceAndTheActualOwnedOutputClearsOnDispose()
    {
        byte[] source=[30,30,30,1,30,30,30,1,30,30,30,3],unchanged=source.ToArray();byte[]? owned=null;var job=Job([1]) with{PrepareLongEdge=1};
        var raster=ImageRasterPreparer.Prepare(source,job,EncodedRasterFormat.Png,new(3,1,ImageOrientation.ExifTopLeft),default,bytes=>owned=bytes);
        // (64800+64800+64350)/(3*255)=253.529...; rounding each input first gives253.
        Assert.Equal(Gray(254),raster.Bytes.ToArray());Assert.Equal(unchanged,source);raster.Dispose();Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);
    }
    [Fact]public async Task DefaultLongEdgeIs2560AndTheEntireNonconstantSourceContributes()
    {
        byte[] pixels=Gray(new byte[3001]);pixels[0]=pixels[1]=pixels[2]=pixels[^4]=pixels[^3]=pixels[^2]=255;byte[] encoded=Encode(3001,1,pixels);byte[]? owned=null;var job=Job(encoded);
        await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){DecodeAllocationForTest=bytes=>owned=bytes};var raster=await client.PrepareAsync(encoded,job);
        Assert.Null(job.PrepareLongEdge);Assert.Equal(2560,raster.Metadata.Job.PrepareLongEdge);Assert.Equal(2560,raster.Metadata.Width);Assert.Equal(1,raster.Metadata.Height);Assert.Equal(218,raster.Bytes.Span[0]);Assert.Equal(218,raster.Bytes.Span[^4]);Assert.Equal(0,raster.Bytes.Span[4]);Assert.Equal(new ImageSize(3001,1),raster.Transform!.OriginalSize);raster.Dispose();Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);
    }
    [Fact]public async Task RealisticNonconstant3840By2160RasterReturnsAllBoundedChunksAndPreservesItsBorder()
    {
        const int width=3840,height=2160;byte[] pixels=new byte[width*height*4];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++){int at=(y*width+x)*4;byte value=(byte)(x==0||x==width-1||y==0||y==height-1?255:0);pixels[at]=pixels[at+1]=pixels[at+2]=value;pixels[at+3]=255;}
        byte[] encoded=Encode(width,height,pixels);await using var client=Client();using var raster=await client.PrepareAsync(encoded,Job(encoded));Assert.Equal(2560,raster.Metadata.Width);Assert.Equal(1440,raster.Metadata.Height);Assert.Equal(14_745_600,raster.Bytes.Length);Assert.Equal(227,raster.Bytes.Span[0]);Assert.Equal(170,raster.Bytes.Span[4]);Assert.Equal(227,raster.Bytes.Span[^4]);Assert.Equal(0,raster.Bytes.Span[(720*2560+1280)*4]);Assert.True(client.ObserveExit());
    }
    [Fact]public async Task UnsupportedPngExifCannotBeSilentlyDiscardedAsIdentity()
    {
        byte[] png=Encode(3,2,Gray(10,50,90,130,180,230)),jpeg=Exif(Encode(3,2,Gray(10,50,90,130,180,230),SKEncodedImageFormat.Jpeg),6);byte[] tiff=jpeg.AsSpan(12,26).ToArray();byte[] chunk=new byte[tiff.Length+12];BinaryPrimitives.WriteUInt32BigEndian(chunk,(uint)tiff.Length);"eXIf"u8.CopyTo(chunk.AsSpan(4));tiff.CopyTo(chunk,8);uint crc=uint.MaxValue;
        foreach(byte b in chunk.AsSpan(4,tiff.Length+4)){crc^=b;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0u);}BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(tiff.Length+8),~crc);
        byte[] encoded=[..png[..33],..chunk,..png[33..]];var starter=new Starter();await using var client=Client(starter);var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.PrepareAsync(encoded,Job(encoded)));Assert.Equal(ImageFailure.UnsupportedImage,error.Code);Assert.Equal(0,starter.Starts);
    }
    [Fact]public async Task CancelledWorkerPreparationHoldsTheOriginalTaskUntilLatePixelsAreCleared()
    {
        byte[] encoded=[1],owned=Gray(70);var job=Job(encoded) with{PrepareLongEdge=1};var id=Guid.NewGuid();using var input=new MemoryStream();using var output=new MemoryStream();await IpcFrameCodec.WriteAsync(input,new(IpcMessageType.ImageCancel,id,[]));input.Position=0;
        var entered=new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource<DecodedRaster>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task=ImageDecodeSession.RunAsync(input,output,id,encoded,job,(_,_,ct)=>{entered.SetResult(ct);return release.Task.GetAwaiter().GetResult();},null);var cancellation=await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.True(cancellation.IsCancellationRequested);Assert.False(task.IsCompleted);
        release.SetResult(new(new(job,ImageFailure.None,EncodedRasterFormat.Png,1,1,4,4,ImageDecodePolicy.Hash(owned),"BGRA8888-opaque-white",1,1,new(3,2,ImageOrientation.ExifRightTop)),owned));await task;Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);
        output.Position=0;var frame=await IpcFrameCodec.ReadAsync(output);var reply=ImageJson.Read<ImageDecodeReply>(frame!.Payload);Assert.Equal(ImageFailure.Cancelled,reply.Failure);Assert.Null(reply.PreparedInfo);Assert.Null(reply.PixelFormat);Assert.Equal(0,reply.ByteLength);Assert.Equal(IpcMessageType.ImageDecodeCommit,(await IpcFrameCodec.ReadAsync(output))!.Type);Assert.Null(await IpcFrameCodec.ReadAsync(output));
    }
    [Theory][InlineData(0)][InlineData(2561)][InlineData(int.MaxValue)]
    public async Task InvalidPreparationLimitsCannotStartAChild(int limit)
    {
        byte[] encoded=Encode(1,1,Gray(30));var starter=new Starter();await using var client=Client(starter);await Assert.ThrowsAsync<InvalidDataException>(()=>client.PrepareAsync(encoded,Job(encoded),limit));Assert.Equal(0,starter.Starts);
    }
    [Fact]public async Task CallerOptionsCannotBeSilentlyOverwrittenOrSentAsRawDecode()
    {
        byte[] encoded=Encode(1,1,Gray(30));var starter=new Starter();await using var client=Client(starter);var job=Job(encoded) with{PrepareLongEdge=2};await Assert.ThrowsAsync<InvalidDataException>(()=>client.PrepareAsync(encoded,job,1));await Assert.ThrowsAsync<InvalidDataException>(()=>client.DecodeAsync(encoded,job));Assert.Equal(0,starter.Starts);
    }
    [Theory][InlineData("source")][InlineData("direction")][InlineData("missing")][InlineData("raw-leak")][InlineData("options")][InlineData("dimensions")][InlineData("stride")][InlineData("semantics")]
    public void InconsistentPreparedMetadataIsRejectedBeforeAllocation(string kind)
    {
        var job=Job([1]) with{PrepareLongEdge=1};var metadata=new ImageDecodeReply(job,ImageFailure.None,EncodedRasterFormat.Png,1,1,4,4,ImageDecodePolicy.Hash(Gray(3)),"BGRA8888-opaque-white",1,1,new(2,1,ImageOrientation.ExifTopLeft));
        metadata=kind switch{"source"=>metadata with{PreparedInfo=metadata.PreparedInfo! with{SourceWidth=int.MaxValue}},"direction"=>metadata with{PreparedInfo=metadata.PreparedInfo! with{SourceOrientation=0}},"missing"=>metadata with{PreparedInfo=null},"raw-leak"=>metadata with{Job=job with{PrepareLongEdge=null}},"options"=>metadata with{Job=job with{PrepareLongEdge=2}},"dimensions"=>metadata with{Width=2},"stride"=>metadata with{Stride=8},_=>metadata with{PixelFormat="BGRA8888-straight"}};int allocations=0;if(kind=="raw-leak")Assert.Throws<InvalidDataException>(()=>ImageDecodePolicy.ValidateReply(metadata));Assert.ThrowsAny<Exception>(()=>new ImageRasterReceiver(Guid.NewGuid(),job,metadata,_=>allocations++));Assert.Equal(0,allocations);
    }
    [Theory][InlineData("prepared-source",false)][InlineData("prepared-alpha",true)][InlineData("extra-byte",true)][InlineData("second-commit",true)][InlineData("exit7",true)]
    public async Task UntrustedPreparedOutputCannotEscapeOrLeaveItsActualParentArray(string fault,bool allocated)
    {
        byte[] encoded=Encode(3,2,Gray(10,50,90,130,180,230));byte[]? owned=null;await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),new Starter(fault)){DecodeAllocationForTest=bytes=>owned=bytes};
        await Assert.ThrowsAnyAsync<Exception>(()=>client.PrepareAsync(encoded,Job(encoded),3));Assert.Equal(allocated,owned!=null);if(owned!=null)Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.True(client.ObserveExit());
    }
    [Fact]public async Task CompletedPreparedRasterStillWaitsForOwnedChildExitAndCancellationClearsIt()
    {
        byte[] encoded=Encode(3,2,Gray(10,50,90,130,180,230));byte[]? owned=null;using var cancel=new CancellationTokenSource();var received=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),new Starter("hold-exit")){DecodeAllocationForTest=bytes=>owned=bytes,DecodeReceivedForTest=()=>received.SetResult()};
        var task=client.PrepareAsync(encoded,Job(encoded),3,cancel.Token);await received.Task.WaitAsync(TimeSpan.FromSeconds(10));Assert.False(task.IsCompleted);cancel.Cancel();await Assert.ThrowsAnyAsync<Exception>(()=>task);Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.True(client.ObserveExit());
    }
    [Fact]public void CancellationOwnsAndClearsTheActualTemporaryDestinationWithoutChangingSource()
    {
        byte[] source=Gray(10,20,30,40,50,60),unchanged=source.ToArray();byte[]? output=null;using var cancellation=new CancellationTokenSource();var job=Job([1]) with{PrepareLongEdge=2};
        Assert.ThrowsAny<OperationCanceledException>(()=>ImageRasterPreparer.Prepare(source,job,EncodedRasterFormat.Png,new(3,2,ImageOrientation.ExifRightTop),cancellation.Token,bytes=>{output=bytes;cancellation.Cancel();}));Assert.NotNull(output);Assert.True(output.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.Equal(unchanged,source);
    }
    [Theory][InlineData("byte-order")][InlineData("magic")][InlineData("offset")][InlineData("type")][InlineData("count")][InlineData("value")][InlineData("duplicate-block")][InlineData("duplicate-tag")][InlineData("ifd-limit")][InlineData("short-ifd")]
    public async Task MalformedExifIsRejectedBeforeNativeChildStart(string kind)
    {
        byte[] jpeg=Encode(3,2,Gray(10,50,90,130,180,230),SKEncodedImageFormat.Jpeg),encoded=Exif(jpeg,6);int tiff=12;
        if(kind=="byte-order")encoded[tiff]=0;if(kind=="magic")encoded[tiff+2]=0;if(kind=="offset")BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(tiff+4),uint.MaxValue);if(kind=="type")encoded[tiff+12]=4;if(kind=="count")encoded[tiff+14]=2;if(kind=="value")encoded[tiff+18]=9;if(kind=="duplicate-block")encoded=Exif(encoded,1);
        if(kind=="ifd-limit")BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(tiff+8),257);
        if(kind=="short-ifd")BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(tiff+8),2);
        if(kind=="duplicate-tag")
        {
            byte[] payload=[..encoded.AsSpan(6,28).ToArray(),..encoded.AsSpan(tiff+10,12).ToArray(),0,0,0,0];BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(14),2);encoded=AddApp1(jpeg,payload);
        }
        var starter=new Starter();await using var client=Client(starter);var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.PrepareAsync(encoded,Job(encoded)));Assert.Equal(kind=="ifd-limit"?ImageFailure.UnsupportedImage:ImageFailure.InvalidImage,error.Code);Assert.Equal(0,starter.Starts);
    }
}
