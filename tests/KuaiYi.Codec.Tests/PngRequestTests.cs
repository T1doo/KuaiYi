using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using KuaiYi.Imaging;
using SkiaSharp;
using Xunit;
namespace KuaiYi.Codec.Tests;

public sealed class PngRequestTests
{
    private static string Worker()
    {for(var p=new DirectoryInfo(AppContext.BaseDirectory);p!=null;p=p.Parent)if(File.Exists(Path.Combine(p.FullName,"SPEC.md")))return Path.Combine(p.FullName,"tools/KuaiYi.ImageDecodeFixture/bin/Release/net10.0/KuaiYi.ImageDecodeFixture.dll");throw new IOException("No synthetic worker.");}
    private sealed class Starter(string? fault=null):IImageProcessStarter
    {public int Starts;public Process Start(ProcessStartInfo info){Starts++;if(fault!=null)info.Environment["KUAIYI_CODEC_FIXTURE_FAULT"]=fault;return Process.Start(info)??throw new IOException("No child.");}}
    private static ImageDecodeJob Job(byte[] bytes)=>new(Guid.NewGuid(),1,1,bytes.Length,ImageDecodePolicy.Hash(bytes));
    private static byte[] Gray(params byte[] values)=>values.SelectMany(v=>new byte[]{v,v,v,255}).ToArray();
    private static byte[] Noise(int side)
    {var pixels=new byte[side*side*4];new Random(730419).NextBytes(pixels);for(int i=3;i<pixels.Length;i+=4)pixels[i]=255;return pixels;}
    private static byte[] Encode(byte[] pixels,int width,int height,SKEncodedImageFormat format=SKEncodedImageFormat.Png)
    {using var bitmap=new SKBitmap(new SKImageInfo(width,height,SKColorType.Bgra8888,SKAlphaType.Unpremul));Marshal.Copy(pixels,0,bitmap.GetPixels(),pixels.Length);using var image=SKImage.FromBitmap(bitmap);using var encoded=image.Encode(format,100);return encoded.ToArray();}
    private static DecodedRaster Raster(byte[] pixels,int width,int height)
    {var job=Job([1]) with{PrepareLongEdge=Math.Max(width,height)};return new(new(job,ImageFailure.None,EncodedRasterFormat.Png,width,height,width*4,pixels.Length,ImageDecodePolicy.Hash(pixels),"BGRA8888-opaque-white",1,1,new(width,height,ImageOrientation.ExifTopLeft)),pixels);}
    private static byte[] ActualArray(ReadOnlyMemory<byte> bytes)
    {Assert.True(MemoryMarshal.TryGetArray(bytes,out var segment));Assert.Equal(0,segment.Offset);Assert.Equal(segment.Array!.Length,segment.Count);return segment.Array;}
    // Independent container/CRC and lossless pixel reader: only the intentionally emitted RGB/filter0 form.
    private static byte[] ReadPng(ReadOnlyMemory<byte> png,int width,int height)
    {
        var bytes=png.Span;Assert.True(bytes[..8].SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}));using var compressed=new MemoryStream();var names=new List<string>();int offset=8;
        while(offset<bytes.Length)
        {
            int count=checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes[offset..]));Assert.InRange(count,0,bytes.Length-offset-12);var type=bytes.Slice(offset+4,4);string name=System.Text.Encoding.ASCII.GetString(type);names.Add(name);uint crc=uint.MaxValue;
            foreach(byte value in bytes.Slice(offset+4,count+4)){crc^=value;for(int bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0u);}Assert.Equal(~crc,BinaryPrimitives.ReadUInt32BigEndian(bytes[(offset+8+count)..]));
            if(name=="IHDR"){Assert.Equal(13,count);Assert.Equal((uint)width,BinaryPrimitives.ReadUInt32BigEndian(bytes[(offset+8)..]));Assert.Equal((uint)height,BinaryPrimitives.ReadUInt32BigEndian(bytes[(offset+12)..]));Assert.Equal(new byte[]{8,2,0,0,0},bytes.Slice(offset+16,5).ToArray());}
            if(name=="IDAT")compressed.Write(bytes.Slice(offset+8,count));if(name=="IEND")Assert.Equal(0,count);offset+=count+12;
        }
        Assert.Equal(bytes.Length,offset);Assert.Equal(new[]{"IHDR","IDAT","IEND"},names);compressed.Position=0;using var zip=new ZLibStream(compressed,CompressionMode.Decompress);byte[] rows=new byte[(width*3+1)*height];zip.ReadExactly(rows);Assert.Equal(-1,zip.ReadByte());
        byte[] rgb=new byte[width*height*3];for(int y=0;y<height;y++){Assert.Equal(0,rows[y*(width*3+1)]);rows.AsSpan(y*(width*3+1)+1,width*3).CopyTo(rgb.AsSpan(y*width*3));}return rgb;
    }
    [Fact]public async Task ActualWorkerAreaWhitePreparationBecomesAnOwnedBoundedMetadataFreePng()
    {
        byte[] input=Encode(Gray(0,50,100,150,200,10,60,110,160,210,20,70,120,170,220),5,3),original=input.ToArray();byte[]? raster=null;
        await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){DecodeAllocationForTest=bytes=>raster=bytes};var job=Job(input);var request=await client.PreparePngAsync(input,job,maximumLongEdge:3);
        Assert.Equal(job with{PrepareLongEdge=3},request.SourceMetadata.Job);Assert.Equal("image/png",request.ContentType);Assert.Equal(4194304,request.MaximumBytes);Assert.Equal(request.Bytes.Length,request.ByteLength);Assert.Equal(ImageDecodePolicy.Hash(request.Bytes.Span),request.Sha256);Assert.Equal(new ImageSize(3,2),request.Transform.NormalizedSize);Assert.False(request.Transform.HasDesktopAnchor);
        Assert.Equal(new byte[]{23,23,23,103,103,103,183,183,183,37,37,37,117,117,117,197,197,197},ReadPng(request.Bytes,3,2));Assert.Equal(original,input);Assert.NotNull(raster);Assert.True(raster.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.True(client.ObserveExit());
        var final=ActualArray(request.Bytes);request.Dispose();Assert.True(final.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.Throws<ObjectDisposedException>(()=>request.Bytes);
    }
    [Fact]public async Task ActualJpegExifMappingSurvivesTheNewPngEncodingStage()
    {
        byte[] jpeg=Encode(Gray(10,50,90,130,180,230),3,2,SKEncodedImageFormat.Jpeg);
        await using var rawClient=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var raw=await rawClient.DecodeAsync(jpeg,Job(jpeg));
        byte[] exif=[.."Exif\0\0"u8.ToArray(),73,73,42,0,8,0,0,0,1,0,18,1,3,0,1,0,0,0,6,0,0,0,0,0,0,0];byte[] input=[..jpeg[..2],255,225,0,(byte)(exif.Length+2),..exif,..jpeg[2..]];
        await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var request=await client.PreparePngAsync(input,Job(input));var rgb=ReadPng(request.Bytes,2,3);int[] expectedOrder=[3,0,4,1,5,2];
        for(int i=0;i<6;i++){var pixel=raw.Bytes.Slice(expectedOrder[i]*4,4).Span;Assert.Equal(new byte[]{pixel[2],pixel[1],pixel[0]},rgb.AsSpan(i*3,3).ToArray());}
        Assert.Equal(EncodedRasterFormat.Jpeg,request.SourceMetadata.Format);Assert.Equal(ImageOrientation.ExifRightTop,request.Transform.Orientation);Assert.Equal(new NormalizedImagePoint(2,0),request.Transform.OriginalToNormalized(new OriginalImagePoint(0,0)));
    }
    [Fact]public async Task TransparentPixelsBecomeWhiteBeforeLosslessEncoding()
    {
        byte[] input=Encode([0,0,0,0,0,0,0,128,0,0,0,255],3,1);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var request=await client.PreparePngAsync(input,Job(input),maximumLongEdge:1);Assert.Equal(new byte[]{127,127,127},ReadPng(request.Bytes,1,1));
    }
    [Theory][InlineData(1024,true)][InlineData(1280,false)]
    public async Task HighEntropyImagesRespectTheActualDefaultFourMiBCapWithoutChangingDimensions(int side,bool fits)
    {
        byte[] input;
        using(var pixels=new ScreenshotPixels(new(0,0,side,side),Noise(side)))using(var png=ScreenshotPngEncoder.Encode(pixels))input=png.Bytes.ToArray();
        byte[]? owned=null;await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){DecodeAllocationForTest=bytes=>owned=bytes};
        if(fits){using var request=await client.PreparePngAsync(input,Job(input));Assert.InRange(request.ByteLength,3_000_000,4194304);Assert.Equal(new ImageSize((uint)side,(uint)side),request.Transform.NormalizedSize);ReadPng(request.Bytes,side,side);}
        else await Assert.ThrowsAsync<ScreenshotNormalizationException>(()=>client.PreparePngAsync(input,Job(input)));
        Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.True(client.ObserveExit());
    }
    [Fact]public void ExactCapIncludesEveryPngByteAndOneLessFailsWithoutAQualityOrResizeRetry()
    {
        byte[] source=Noise(256),original=source.ToArray();using var raster=Raster(source,256,256);using var probe=ImageRequestPngEncoder.Encode(raster,4194304,default);int cap=probe.ByteLength;Assert.InRange(cap,65536,4194304);
        using var exact=ImageRequestPngEncoder.Encode(raster,cap,default);Assert.Equal(cap,exact.ByteLength);Assert.Equal(cap,exact.MaximumBytes);Assert.Equal(probe.Bytes.ToArray(),exact.Bytes.ToArray());var allocated=new List<byte[]>();Assert.Throws<ScreenshotNormalizationException>(()=>ImageRequestPngEncoder.Encode(raster,cap-1,default,allocated.Add));Assert.NotEmpty(allocated);Assert.All(allocated,bytes=>Assert.True(bytes.AsSpan().IndexOfAnyExcept((byte)0)<0));Assert.Equal(original,source);
    }
    [Theory][InlineData(0)][InlineData(65535)][InlineData(4194305)][InlineData(int.MaxValue)]
    public async Task InvalidByteCapsCannotStartAChild(int cap)
    {
        byte[] input=Encode(Gray(70),1,1);var starter=new Starter();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),starter);await Assert.ThrowsAsync<ArgumentOutOfRangeException>(()=>client.PreparePngAsync(input,Job(input),cap));Assert.Equal(0,starter.Starts);
    }
    [Fact]public void EveryActualWorkingBufferClearsOnSuccessAndTheTransferredArrayClearsOnDispose()
    {
        using var raster=Raster(Noise(256),256,256);var allocated=new List<byte[]>();var png=ImageRequestPngEncoder.Encode(raster,4194304,default,allocated.Add);var final=ActualArray(png.Bytes);Assert.Contains(allocated,bytes=>ReferenceEquals(bytes,final));Assert.All(allocated.Where(bytes=>!ReferenceEquals(bytes,final)),bytes=>Assert.True(bytes.AsSpan().IndexOfAnyExcept((byte)0)<0));png.Dispose();Assert.All(allocated,bytes=>Assert.True(bytes.AsSpan().IndexOfAnyExcept((byte)0)<0));
    }
    [Theory][InlineData("row",false)][InlineData("compression",false)][InlineData("final",false)][InlineData("row",true)][InlineData("compression",true)][InlineData("final",true)]
    public void CancellationOrObserverFailureAtActualAllocationClearsEveryOwnedWorkingArray(string stage,bool cancel)
    {
        byte[] source=Noise(256),unchanged=source.ToArray();using var raster=Raster(source,256,256);int total=0;using(var probe=ImageRequestPngEncoder.Encode(raster,4194304,default,_=>total++)){}int target=stage switch{"row"=>1,"compression"=>2,_=>total},count=0;bool triggered=false;var buffers=new List<byte[]>();using var cancellation=new CancellationTokenSource();
        void Observe(byte[] bytes){buffers.Add(bytes);if(++count!=target)return;triggered=true;if(cancel)cancellation.Cancel();else throw new InvalidOperationException("SYNTHETIC_ALLOCATION_FAULT");}
        if(cancel)Assert.ThrowsAny<OperationCanceledException>(()=>ImageRequestPngEncoder.Encode(raster,4194304,cancellation.Token,Observe));else Assert.ThrowsAny<Exception>(()=>ImageRequestPngEncoder.Encode(raster,4194304,cancellation.Token,Observe));
        Assert.True(triggered);Assert.True(count>=target);Assert.All(buffers,bytes=>Assert.True(bytes.AsSpan().IndexOfAnyExcept((byte)0)<0));Assert.Equal(unchanged,source);
    }
    [Fact]public void CancellationDuringBoundedCompressionFlushCannotBeMisreportedAsASmallerCrop()
    {
        using var raster=Raster(Noise(256),256,256);using var cancellation=new CancellationTokenSource();var buffers=new List<byte[]>();
        Assert.ThrowsAny<OperationCanceledException>(()=>ImageRequestPngEncoder.Encode(raster,65536,cancellation.Token,bytes=>{buffers.Add(bytes);if(bytes.Length==65536)cancellation.Cancel();}));Assert.True(cancellation.IsCancellationRequested);Assert.All(buffers,bytes=>Assert.True(bytes.AsSpan().IndexOfAnyExcept((byte)0)<0));
    }
    [Theory][InlineData("raw")][InlineData("alpha")][InlineData("digest")]
    public void SourceContractsAreValidatedBeforeAllocatingPngWorkBuffers(string kind)
    {
        byte[] pixels=Gray(70);var job=Job([1]);ImageDecodeReply metadata=kind=="raw"?new(job,ImageFailure.None,EncodedRasterFormat.Png,1,1,4,4,ImageDecodePolicy.Hash(pixels),"BGRA8888-straight",1,1):new(job with{PrepareLongEdge=1},ImageFailure.None,EncodedRasterFormat.Png,1,1,4,4,ImageDecodePolicy.Hash(pixels),"BGRA8888-opaque-white",1,1,new(1,1,ImageOrientation.ExifTopLeft));
        if(kind=="alpha"){pixels[3]=0;metadata=metadata with{Sha256=ImageDecodePolicy.Hash(pixels)};}using var raster=new DecodedRaster(metadata,pixels);if(kind=="digest")pixels[0]^=1;int allocations=0;Assert.Throws<InvalidDataException>(()=>ImageRequestPngEncoder.Encode(raster,4194304,default,_=>allocations++));Assert.Equal(0,allocations);
    }
    [Theory][InlineData("extra-byte")][InlineData("exit7")]
    public async Task EncodingCannotBeginBeforeTheEntireOriginalWorkerGatePasses(string fault)
    {
        byte[] input=Encode(Gray(70),1,1);int calls=0;byte[]? owned=null;await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),new Starter(fault)){DecodeAllocationForTest=bytes=>owned=bytes,EncodePreparedForTest=(_,_,_)=>{calls++;throw new InvalidOperationException("Must not encode.");}};
        await Assert.ThrowsAnyAsync<Exception>(()=>client.PreparePngAsync(input,Job(input)));Assert.Equal(0,calls);Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.True(client.ObserveExit());
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task SameOriginalEncodingTaskOwnsRasterAndLatePngThroughCancellationOrWholePipelineDeadline(bool deadline)
    {
        byte[] input=Encode(Gray(10,20,30,40,50,60),3,2);byte[]? rasterBytes=null,lateBytes=null;using var cancellation=new CancellationTokenSource();var entered=new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker())
        {
            DecodeAllocationForTest=bytes=>rasterBytes=bytes,
            EncodePreparedForTest=async(raster,cap,token)=>{entered.SetResult(token);await release.Task;var png=ImageRequestPngEncoder.Encode(raster,cap,default);lateBytes=ActualArray(png.Bytes);return png;}
        };
        var task=client.PreparePngAsync(input,Job(input),cancellation:cancellation.Token);var token=await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));Assert.True(client.ObserveExit());Assert.False(await client.TryFinalizeAsync());Assert.False(task.IsCompleted);Assert.NotNull(rasterBytes);Assert.True(rasterBytes.AsSpan().IndexOfAnyExcept((byte)0)>=0);
        if(deadline){var canceled=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);using var registration=token.Register(()=>canceled.SetResult());await canceled.Task.WaitAsync(TimeSpan.FromSeconds(20));}else cancellation.Cancel();
        Assert.True(token.IsCancellationRequested);Assert.False(task.IsCompleted);Assert.True(rasterBytes.AsSpan().IndexOfAnyExcept((byte)0)>=0);release.SetResult();
        if(deadline){var error=await Assert.ThrowsAsync<ImageJobException>(()=>task);Assert.Equal(ImageFailure.Timeout,error.Code);}else await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>task);
        Assert.NotNull(lateBytes);Assert.True(lateBytes.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.True(rasterBytes.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.True(await client.TryFinalizeAsync());
    }
    [Fact]public async Task AReturnedEncoderOwnerCannotSubstituteAnotherByteBudget()
    {
        byte[] input=Encode(Gray(70),1,1);byte[]? output=null;await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){EncodePreparedForTest=(raster,_,_)=>{var png=ImageRequestPngEncoder.Encode(raster,65536,default);output=ActualArray(png.Bytes);return Task.FromResult(png);}};
        await Assert.ThrowsAsync<InvalidDataException>(()=>client.PreparePngAsync(input,Job(input)));Assert.NotNull(output);Assert.True(output.AsSpan().IndexOfAnyExcept((byte)0)<0);
    }
}
