using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using KuaiYi.Contracts;
using KuaiYi.Imaging;
using SkiaSharp;
using Xunit;
namespace KuaiYi.Codec.Tests;

public sealed partial class RasterDecodeTests
{
    private static string Repository()
    {for(var p=new DirectoryInfo(AppContext.BaseDirectory);p!=null;p=p.Parent)if(File.Exists(Path.Combine(p.FullName,"SPEC.md")))return p.FullName;throw new IOException("Repository unavailable.");}
    private static string Worker()=>Environment.GetEnvironmentVariable("KUAIYI_CODEC_BASELINE_WORKER")??Path.Combine(Repository(),"tools/KuaiYi.ImageDecodeFixture/bin/Release/net10.0/KuaiYi.ImageDecodeFixture.dll");
    private sealed class Starter(string? fault=null):IImageProcessStarter
    {public int Starts;public Process Start(ProcessStartInfo info){Starts++;if(fault!=null)info.Environment["KUAIYI_CODEC_FIXTURE_FAULT"]=fault;return Process.Start(info)??throw new IOException("No fixture child.");}}
    private static ImageWorkerClient Client(Starter? starter=null)=>new(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??throw new IOException("Safe dotnet host unavailable."),Worker(),starter);
    private static ImageDecodeJob Job(byte[] bytes)=>new(Guid.NewGuid(),1,1,bytes.Length,ImageDecodePolicy.Hash(bytes));
    private static byte[] Encoded(SKEncodedImageFormat format=SKEncodedImageFormat.Png,int width=16,int height=8,byte alpha=255)
    {
        using var bitmap=new SKBitmap(new SKImageInfo(width,height,SKColorType.Bgra8888,SKAlphaType.Unpremul));byte[] pixels=new byte[width*height*4];
        for(int i=0;i<pixels.Length;i+=4){pixels[i]=180;pixels[i+1]=120;pixels[i+2]=70;pixels[i+3]=alpha;}
        try{Marshal.Copy(pixels,0,bitmap.GetPixels(),pixels.Length);using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(format,95);return data.ToArray();}
        finally{CryptographicOperations.ZeroMemory(pixels);}
    }
    // Synthetic baseline JPEG: all-zero DCT coefficients, explicit tables, actual native decode.
    private static byte[] BaselineJpeg(int width,int height,int h,int v,bool gray,int restart=0)
    {
        var bytes=new List<byte>{255,216};
        void Segment(byte marker,byte[] payload){bytes.AddRange([255,marker,(byte)((payload.Length+2)>>8),(byte)(payload.Length+2)]);bytes.AddRange(payload);}
        Segment(219,[0,..Enumerable.Repeat((byte)1,64)]);
        int components=gray?1:3;var frame=new List<byte>{8,(byte)(height>>8),(byte)height,(byte)(width>>8),(byte)width,(byte)components};
        for(int i=0;i<components;i++)frame.AddRange([(byte)(i+1),(byte)(i==0?(h<<4)|v:17),0]);Segment(192,frame.ToArray());
        byte[] counts=[1,..new byte[15]];Segment(196,[0,..counts,0,16,..counts,0]);
        if(restart>0)Segment(221,[(byte)(restart>>8),(byte)restart]);
        var scan=new List<byte>{(byte)components};for(int i=0;i<components;i++)scan.AddRange([(byte)(i+1),0]);scan.AddRange([0,63,0]);Segment(218,scan.ToArray());
        int total=((width+8*h-1)/(8*h))*((height+8*v-1)/(8*v)),bits=0,current=0,nextRestart=0;
        void Bit(int bit){current=(current<<1)|bit;if(++bits==8){bytes.Add((byte)current);if(current==255)bytes.Add(0);bits=0;current=0;}}
        void Align(){while(bits!=0)Bit(1);}
        for(int mcu=0;mcu<total;mcu++)
        {
            int blocks=gray?1:h*v+2;for(int block=0;block<blocks;block++){Bit(0);Bit(0);}
            if(restart>0&&(mcu+1)%restart==0&&mcu+1<total){Align();bytes.AddRange([255,(byte)(208+nextRestart)]);nextRestart=(nextRestart+1)&7;}
        }
        Align();bytes.AddRange([255,217]);return bytes.ToArray();
    }
    [Theory][InlineData(17,9,1,1,true,0)][InlineData(17,9,1,1,false,0)][InlineData(17,9,2,2,false,0)][InlineData(33,17,2,2,false,1)]
    public async Task CompleteBaselineSamplingAndRestartVariantsDecodeInActualChild(int width,int height,int h,int v,bool gray,int restart)
    {
        byte[] input=BaselineJpeg(width,height,h,v,gray,restart);await using var client=Client();using var raster=await client.DecodeAsync(input,Job(input));
        Assert.Equal(width*height*4,raster.Bytes.Length);Assert.Equal(width,raster.Metadata.Width);Assert.Equal(height,raster.Metadata.Height);
        var pixels=raster.Bytes.ToArray();for(int i=0;i<pixels.Length;i+=4){Assert.InRange(pixels[i],127,129);Assert.InRange(pixels[i+1],127,129);Assert.InRange(pixels[i+2],127,129);Assert.Equal(255,pixels[i+3]);}Assert.True(client.ObserveExit());
    }
    [Theory][InlineData("padding")][InlineData("extra-entropy")][InlineData("huffman")][InlineData("quantization")][InlineData("restart")][InlineData("missing-quantization")]
    public async Task MalformedBaselineScanCannotUseNativeRepair(string kind)
    {
        byte[] input=BaselineJpeg(kind=="restart"?33:1,kind=="restart"?17:1,kind=="restart"?2:1,kind=="restart"?2:1,kind!="restart",kind=="restart"?1:0);
        if(kind=="padding")input[^3]&=254;
        if(kind=="extra-entropy")input=[..input[..^2],0,255,217];
        if(kind=="huffman"){int marker=input.AsSpan().IndexOf(new byte[]{255,196});input[marker+5]=2;}
        if(kind=="quantization")input[7]=0;
        if(kind=="missing-quantization"){int marker=input.AsSpan().IndexOf(new byte[]{255,192});input[marker+12]=1;}
        if(kind=="restart"){int marker=input.AsSpan().IndexOf(new byte[]{255,208});Assert.True(marker>0);input[marker+1]=209;}
        await using var client=Client();var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.DecodeAsync(input,Job(input)));Assert.Equal(ImageFailure.InvalidImage,error.Code);Assert.True(client.ObserveExit());
    }
    [Fact]public async Task BaselineEntropyWithProgressiveFrameMarkerIsInvalidBeforeNative()
    {
        byte[] input=BaselineJpeg(1,1,1,1,true);int marker=input.AsSpan().IndexOf(new byte[]{255,192});input[marker+1]=194;
        int native=0;var direct=Assert.Throws<ImageJobException>(()=>KuaiYi.ImageWorker.ImageRasterDecoder.Decode(input,Job(input),default,_=>native++));Assert.Equal(ImageFailure.InvalidImage,direct.Code);Assert.Equal(0,native);
        await using var client=Client();var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.DecodeAsync(input,Job(input)));Assert.Equal(ImageFailure.InvalidImage,error.Code);
    }
    [Theory][InlineData(SKEncodedImageFormat.Png)][InlineData(SKEncodedImageFormat.Jpeg)]
    public async Task ActualChildReturnsOnlyCompleteRasterWithExactJobLayoutAndOpaquePixels(SKEncodedImageFormat format)
    {
        byte[] input=Encoded(format),original=input.ToArray();var job=Job(input);await using var client=Client();using var raster=await client.DecodeAsync(input,job);
        Assert.Equal(job,raster.Metadata.Job);Assert.Equal(16,raster.Metadata.Width);Assert.Equal(8,raster.Metadata.Height);Assert.Equal(64,raster.Metadata.Stride);Assert.Equal(512,raster.Bytes.Length);Assert.True(client.ObserveExit());Assert.Equal(original,input);
        var pixel=raster.Bytes.Span[..4].ToArray();Assert.InRange(pixel[0],177,183);Assert.InRange(pixel[1],117,123);Assert.InRange(pixel[2],67,73);Assert.Equal(255,pixel[3]);Assert.Equal(ImageDecodePolicy.Hash(raster.Bytes.Span),raster.Metadata.Sha256);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>client.DecodeAsync(input,job));
    }
    [Fact]public async Task TransparentStraightBgraAndMultichunkRasterRetainTheirActualBuffersUntilDispose()
    {
        byte[] input=Encoded(width:1024,height:300,alpha:128);byte[]? owned=null;await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){DecodeAllocationForTest=bytes=>owned=bytes};
        var raster=await client.DecodeAsync(input,Job(input));Assert.NotNull(owned);Assert.Equal(1024*300*4,owned.Length);Assert.Equal("BGRA8888-straight",raster.Metadata.PixelFormat);Assert.Equal(1,raster.Metadata.Frames);Assert.Equal(1,raster.Metadata.Orientation);Assert.Equal(128,owned[3]);Assert.InRange(owned[2],69,71);Assert.True(raster.Bytes.Span.SequenceEqual(owned));raster.Dispose();Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.Throws<ObjectDisposedException>(()=>raster.Bytes);
    }
    [Theory][InlineData("extra-byte")][InlineData("second-commit")][InlineData("exit7")]
    public async Task CompleteRasterFollowedByExtraOutputOrFailedExitCannotEscapeAndIsActuallyCleared(string fault)
    {
        byte[] input=Encoded();byte[]? owned=null;await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),new Starter(fault)){DecodeAllocationForTest=bytes=>owned=bytes};
        await Assert.ThrowsAnyAsync<Exception>(()=>client.DecodeAsync(input,Job(input)));Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.True(client.ObserveExit());
    }
    [Fact]public async Task CancellationAfterCompleteRasterWhileChildStillLivesClearsUndeliveredPixels()
    {
        byte[] input=Encoded();byte[]? owned=null;using var cancel=new CancellationTokenSource();var received=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),new Starter("hold-exit")){DecodeAllocationForTest=bytes=>owned=bytes,DecodeReceivedForTest=()=>received.SetResult()};
        var read=client.DecodeAsync(input,Job(input),cancel.Token);await received.Task.WaitAsync(TimeSpan.FromSeconds(10));Assert.False(read.IsCompleted);cancel.Cancel();await Assert.ThrowsAnyAsync<Exception>(()=>read);Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.True(client.ObserveExit());
    }
    [Theory][InlineData("crc")][InlineData("truncated")][InlineData("tail")]
    public async Task RealWorkerRejectsMalformedPngContainerWithoutReturningPartialPixels(string kind)
    {
        byte[] bytes=Encoded();if(kind=="crc")bytes[29]^=1;if(kind=="truncated")bytes=bytes[..^12];if(kind=="tail")bytes=[..bytes,1];
        await using var client=Client();var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.DecodeAsync(bytes,Job(bytes)));Assert.Equal(ImageFailure.InvalidImage,error.Code);Assert.True(client.ObserveExit());
    }
    [Fact]public async Task TruncatedPngCompressedDataWithValidContainerCrcCannotReturnPixels()
    {
        byte[] original=Encoded();int offset=8;
        while(!original.AsSpan(offset+4,4).SequenceEqual("IDAT"u8))offset+=checked((int)BinaryPrimitives.ReadUInt32BigEndian(original.AsSpan(offset))+12);
        int length=(int)BinaryPrimitives.ReadUInt32BigEndian(original.AsSpan(offset)),kept=Math.Max(1,length/2);Assert.True(kept<length);
        byte[] chunk=new byte[kept+12];BinaryPrimitives.WriteUInt32BigEndian(chunk,(uint)kept);original.AsSpan(offset+4,kept+4).CopyTo(chunk.AsSpan(4));uint crc=uint.MaxValue;
        foreach(byte b in chunk.AsSpan(4,kept+4)){crc^=b;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0u);}BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(kept+8),~crc);
        byte[] truncated=[..original[..offset],..chunk,..original[(offset+length+12)..]];
        await using var client=Client();var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.DecodeAsync(truncated,Job(truncated)));Assert.Equal(ImageFailure.InvalidImage,error.Code);Assert.True(client.ObserveExit());
    }
    [Fact]public async Task NonDefaultJpegExifOrientationIsExplicitlyUnsupported()
    {
        byte[] jpeg=Encoded(SKEncodedImageFormat.Jpeg);byte[] exif=[.."Exif\0\0"u8.ToArray(),73,73,42,0,8,0,0,0,1,0,18,1,3,0,1,0,0,0,6,0,0,0,0,0,0,0];byte[] size=new byte[2];BinaryPrimitives.WriteUInt16BigEndian(size,(ushort)(exif.Length+2));byte[] oriented=[..jpeg[..2],255,225,..size,..exif,..jpeg[2..]];
        await using var client=Client();var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.DecodeAsync(oriented,Job(oriented)));Assert.Equal(ImageFailure.UnsupportedImage,error.Code);
    }
    [Theory][InlineData("hash")][InlineData("length")][InlineData("identity")][InlineData("oversize")][InlineData("format")]
    public async Task InvalidPreflightCannotStartAnyChild(string kind)
    {
        byte[] bytes=Encoded();var job=Job(bytes);if(kind=="hash")job=job with{Sha256=new string('0',64)};if(kind=="length")job=job with{ByteLength=bytes.Length+1};if(kind=="identity")job=job with{SessionId=Guid.Empty};if(kind=="oversize"){bytes=new byte[ImagePolicy.ByteLimit+1];job=Job(bytes);}if(kind=="format"){bytes="GIF89a"u8.ToArray();job=Job(bytes);}
        var starter=new Starter();await using var client=Client(starter);await Assert.ThrowsAnyAsync<Exception>(()=>client.DecodeAsync(bytes,job));Assert.Equal(0,starter.Starts);
    }
    [Theory][InlineData("stride")][InlineData("total")][InlineData("dimensions")][InlineData("status")][InlineData("failure-layout")][InlineData("job")]
    public void UntrustedRasterMetadataIsRejectedBeforeAnyAllocation(string kind)
    {
        byte[] bytes=[1,2,3,255];var job=Job(Encoded());var m=new ImageDecodeReply(job,ImageFailure.None,EncodedRasterFormat.Png,1,1,4,4,ImageDecodePolicy.Hash(bytes),"BGRA8888-straight",1,1);
        m=kind switch{"stride"=>m with{Stride=8},"total"=>m with{ByteLength=8},"dimensions"=>m with{Width=int.MaxValue},"status"=>m with{Failure=(ImageFailure)999},"failure-layout"=>m with{Failure=ImageFailure.InvalidImage},_=>m with{Job=job with{Version=2}}};int allocations=0;
        Assert.ThrowsAny<Exception>(()=>new ImageRasterReceiver(Guid.NewGuid(),job,m,_=>allocations++));Assert.Equal(0,allocations);
    }
    [Theory][InlineData("order")][InlineData("identity")][InlineData("incomplete")][InlineData("digest")][InlineData("extra")]
    public void RasterProtocolFailureClearsTheActualOwnedArrayAndCannotBeRetried(string kind)
    {
        byte[] bytes=[1,2,3,255];var job=Job(Encoded());var id=Guid.NewGuid();var m=new ImageDecodeReply(job,ImageFailure.None,EncodedRasterFormat.Png,1,1,4,4,ImageDecodePolicy.Hash(bytes),"BGRA8888-straight",1,1);if(kind=="digest")m=m with{Sha256=new string('0',64)};byte[]? owned=null;using var receiver=new ImageRasterReceiver(id,job,m,b=>owned=b);
        byte[] chunk=[0,0,0,0,..bytes];if(kind=="order")chunk[0]=1;if(kind=="identity")id=Guid.NewGuid();if(kind=="extra")chunk=[..chunk,1];
        Assert.ThrowsAny<Exception>(()=>{if(kind!="incomplete")receiver.Chunk(new(IpcMessageType.BinaryChunk,id,chunk));receiver.Commit(new(IpcMessageType.ImageDecodeCommit,id,[]));});Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.ThrowsAny<Exception>(()=>receiver.Commit(new(IpcMessageType.ImageDecodeCommit,id,[])));
    }
    [Fact]public async Task PngAnimationDeclarationIsExplicitlyRejected()
    {
        byte[] original=Encoded();byte[] chunk=new byte[20];BinaryPrimitives.WriteUInt32BigEndian(chunk,8);"acTL"u8.CopyTo(chunk.AsSpan(4));BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8),2);uint crc=uint.MaxValue;
        foreach(byte b in chunk.AsSpan(4,12)){crc^=b;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0u);}BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(16),~crc);
        byte[] animated=[..original[..33],..chunk,..original[33..]];await using var client=Client();var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.DecodeAsync(animated,Job(animated)));Assert.Equal(ImageFailure.UnsupportedImage,error.Code);
    }
    [Fact]public async Task TruncatedJpegScanWithValidHeaderCannotYieldAPartialRaster()
    {
        byte[] original=Encoded(SKEncodedImageFormat.Jpeg);int sos=original.AsSpan().IndexOf(new byte[]{255,218});Assert.True(sos>0);int entropy=sos+2+BinaryPrimitives.ReadUInt16BigEndian(original.AsSpan(sos+2));byte[] truncated=[..original[..(entropy+1)],255,217];
        var starter=new Starter();await using var client=Client(starter);var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.DecodeAsync(truncated,Job(truncated)));Assert.Equal(ImageFailure.InvalidImage,error.Code);Assert.Equal(1,starter.Starts);
    }
    [Theory][InlineData(16385u,1u)][InlineData(6000u,4001u)][InlineData(uint.MaxValue,uint.MaxValue)]
    public async Task EncodedDimensionsAreBoundedBeforeStartingTheWorker(uint width,uint height)
    {
        byte[] input=Encoded();BinaryPrimitives.WriteUInt32BigEndian(input.AsSpan(16),width);BinaryPrimitives.WriteUInt32BigEndian(input.AsSpan(20),height);var starter=new Starter();await using var client=Client(starter);
        var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.DecodeAsync(input,Job(input)));Assert.Equal(ImageFailure.ImageTooLarge,error.Code);Assert.Equal(0,starter.Starts);
    }
    [Theory][InlineData("alpha")][InlineData("orientation")][InlineData("frames")]
    public void PixelSemanticsCannotBeChangedByUntrustedMetadata(string kind)
    {
        byte[] bytes=[1,2,3,255];var job=Job(Encoded());var m=new ImageDecodeReply(job,ImageFailure.None,EncodedRasterFormat.Png,1,1,4,4,ImageDecodePolicy.Hash(bytes),"BGRA8888-straight",1,1);
        m=kind switch{"alpha"=>m with{PixelFormat="BGRA8888-premul"},"orientation"=>m with{Orientation=6},_=>m with{Frames=2}};int allocations=0;Assert.Throws<InvalidDataException>(()=>new ImageRasterReceiver(Guid.NewGuid(),job,m,_=>allocations++));Assert.Equal(0,allocations);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task WorkerKeepsTheSameIgnoredCancellationDecodeUntilLateRasterIsCleared(bool invalidControl)
    {
        byte[] encoded=Encoded(),owned=[7,8,9,255];var job=Job(encoded);var id=Guid.NewGuid();using var input=new MemoryStream();using var output=new MemoryStream();
        await IpcFrameCodec.WriteAsync(input,new(invalidControl?IpcMessageType.Ping:IpcMessageType.ImageCancel,id,[]));input.Position=0;
        var entered=new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource<DecodedRaster>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session=KuaiYi.ImageWorker.ImageDecodeSession.RunAsync(input,output,id,encoded,job,(_,_,ct)=>{entered.SetResult(ct);return release.Task.GetAwaiter().GetResult();},null);
        var token=await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.True(token.IsCancellationRequested);Assert.False(session.IsCompleted);
        release.SetResult(new(new(job,ImageFailure.None,EncodedRasterFormat.Png,1,1,4,4,ImageDecodePolicy.Hash(owned),"BGRA8888-straight",1,1),owned));
        if(invalidControl)await Assert.ThrowsAsync<InvalidDataException>(()=>session);else await session;
        Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);
        if(!invalidControl){output.Position=0;var result=await IpcFrameCodec.ReadAsync(output);Assert.Equal(ImageFailure.Cancelled,ImageJson.Read<ImageDecodeReply>(result!.Payload).Failure);Assert.Equal(IpcMessageType.ImageDecodeCommit,(await IpcFrameCodec.ReadAsync(output))!.Type);Assert.Null(await IpcFrameCodec.ReadAsync(output));}
    }
}
