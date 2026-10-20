using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using KuaiYi.Imaging;
using KuaiYi.ImageWorker;
using KuaiYi.Contracts;
using Xunit;
namespace KuaiYi.Codec.Tests;

public sealed class BmpDecodeTests
{
    private static string Worker()
    {for(var p=new DirectoryInfo(AppContext.BaseDirectory);p!=null;p=p.Parent)if(File.Exists(Path.Combine(p.FullName,"SPEC.md")))return Path.Combine(p.FullName,"tools/KuaiYi.ImageDecodeFixture/bin/Release/net10.0/KuaiYi.ImageDecodeFixture.dll");throw new IOException("No synthetic worker.");}
    private sealed class Starter(string? fault=null):IImageProcessStarter
    {public int Starts;public Process Start(ProcessStartInfo info){Starts++;if(fault!=null)info.Environment["KUAIYI_CODEC_FIXTURE_FAULT"]=fault;return Process.Start(info)??throw new IOException("No child.");}}
    private static ImageDecodeJob Job(byte[] bytes)=>new(Guid.NewGuid(),1,1,bytes.Length,ImageDecodePolicy.Hash(bytes));
    private static void U16(byte[] b,int at,ushort v)=>BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(at),v);
    private static void U32(byte[] b,int at,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at),v);
    private static void I32(byte[] b,int at,int v)=>BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at),v);
    private static byte[] Bmp(int width=3,int height=2,bool topDown=false,bool explicitSize=true)
    {
        int row=(width*3+3)/4*4;byte[] bytes=new byte[54+row*height];bytes[0]=66;bytes[1]=77;U32(bytes,2,(uint)bytes.Length);U32(bytes,10,54);U32(bytes,14,40);I32(bytes,18,width);I32(bytes,22,topDown?-height:height);U16(bytes,26,1);U16(bytes,28,24);U32(bytes,34,explicitSize?(uint)(row*height):0);
        for(int y=0;y<height;y++){int at=54+(topDown?y:height-1-y)*row;for(int x=0;x<width;x++){int index=y*width+x;bytes[at+x*3]=(byte)(10+index*20%240);bytes[at+x*3+1]=(byte)(11+index*20%240);bytes[at+x*3+2]=(byte)(12+index*20%240);}for(int pad=width*3;pad<row;pad++)bytes[at+pad]=0xa5;}
        return bytes;
    }
    private static byte[] PngRows(ReadOnlyMemory<byte> png)
    {
        var bytes=png.Span;using var compressed=new MemoryStream();var names=new List<string>();int at=8;
        while(at<bytes.Length){int length=checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes[at..]));string type=System.Text.Encoding.ASCII.GetString(bytes.Slice(at+4,4));names.Add(type);if(type=="IDAT")compressed.Write(bytes.Slice(at+8,length));at+=length+12;}
        Assert.Equal(new[]{"IHDR","IDAT","IEND"},names);compressed.Position=0;using var zip=new ZLibStream(compressed,CompressionMode.Decompress);using var rows=new MemoryStream();zip.CopyTo(rows);return rows.ToArray();
    }
    [Fact]public async Task ActualBmpFinalPngUsesTheCompleteUnflippedCanvas()
    {
        byte[] input=Bmp(),original=input.ToArray();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var png=await client.PreparePngAsync(input,Job(input));Assert.Equal("Bmp",png.SourceMetadata.Format.ToString());Assert.Equal(new ImageSize(3,2),png.Transform.NormalizedSize);Assert.False(png.Transform.HasDesktopAnchor);Assert.Equal(ImageOrientation.ExifTopLeft,png.Transform.Orientation);
        Assert.Equal(new byte[]{0,12,11,10,32,31,30,52,51,50,0,72,71,70,92,91,90,112,111,110},PngRows(png.Bytes));Assert.Equal(original,input);Assert.True(client.ObserveExit());
    }
    public static IEnumerable<object[]> Layouts(){for(int width=1;width<=4;width++)foreach(bool topDown in new[]{true,false})foreach(bool explicitSize in new[]{true,false})yield return [width,topDown,explicitSize];}
    [Theory][MemberData(nameof(Layouts))]
    public async Task ActualChildNormalizesStorageRowsAndEveryPaddingWidthWithoutSwappingChannels(int width,bool topDown,bool explicitSize)
    {
        byte[] input=Bmp(width,2,topDown,explicitSize),unchanged=input.ToArray();var job=Job(input);byte[]? owned=null;await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){DecodeAllocationForTest=b=>owned=b};var raster=await client.DecodeAsync(input,job);Assert.Equal(EncodedRasterFormat.Bmp,raster.Metadata.Format);Assert.Equal(job,raster.Metadata.Job);Assert.Null(raster.Metadata.PreparedInfo);Assert.Null(raster.Transform);Assert.Equal(width*4,raster.Metadata.Stride);
        for(int i=0;i<width*2;i++)Assert.Equal(new byte[]{(byte)(10+i*20),(byte)(11+i*20),(byte)(12+i*20),255},raster.Bytes.Slice(i*4,4).ToArray());Assert.Equal(unchanged,input);Assert.True(client.ObserveExit());raster.Dispose();Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task PreparedBmpAreaUsesTheSameFullCanvasRegardlessOfStorageSign(bool topDown)
    {
        byte[] input=Bmp(5,3,topDown);int row=16;byte[] gray=[0,50,100,150,200,10,60,110,160,210,20,70,120,170,220];for(int y=0;y<3;y++)for(int x=0;x<5;x++)input.AsSpan(54+(topDown?y:2-y)*row+x*3,3).Fill(gray[y*5+x]);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var raster=await client.PrepareAsync(input,Job(input),3);
        Assert.Equal(new(5,3,ImageOrientation.ExifTopLeft),raster.Metadata.PreparedInfo);Assert.Equal(new ImageSize(3,2),raster.Transform!.NormalizedSize);Assert.False(raster.Transform.HasDesktopAnchor);Assert.Equal(new byte[]{23,103,183,37,117,197}.SelectMany(v=>new byte[]{v,v,v,255}),raster.Bytes.ToArray());Assert.True(client.ObserveExit());
    }
    private static byte[] Broken(string kind)
    {
        byte[] b=Bmp();switch(kind)
        {
            case "short-file":return b[..13];
            case "short-info":return b[..53];
            case "core":U32(b,14,12);break;
            case "v4":U32(b,14,108);break;
            case "v5":U32(b,14,124);break;
            case "header-max":U32(b,14,uint.MaxValue);break;
            case "reserved1":U16(b,6,1);break;
            case "reserved2":U16(b,8,1);break;
            case "size-zero":U32(b,2,0);break;
            case "size-large":U32(b,2,uint.MaxValue);break;
            case "size-small":U32(b,2,(uint)b.Length-1);break;
            case "width-zero":I32(b,18,0);break;
            case "width-negative":I32(b,18,-3);break;
            case "height-zero":I32(b,22,0);break;
            case "height-min":I32(b,22,int.MinValue);break;
            case "width-max":I32(b,18,int.MaxValue);break;
            case "height-max":I32(b,22,int.MaxValue);break;
            case "width-side":I32(b,18,16385);break;
            case "pixels":I32(b,18,6000);I32(b,22,4001);break;
            case "planes":U16(b,26,2);break;
            case "depth1":U16(b,28,1);break;
            case "depth8":U16(b,28,8);break;
            case "depth16":U16(b,28,16);break;
            case "depth32":U16(b,28,32);break;
            case "rle8":U32(b,30,1);break;
            case "rle4":U32(b,30,2);break;
            case "bitfields":U32(b,30,3);break;
            case "embedded-jpeg":U32(b,30,4);break;
            case "embedded-png":U32(b,30,5);break;
            case "palette":U32(b,46,1);break;
            case "palette-max":U32(b,46,uint.MaxValue);break;
            case "important":U32(b,50,1);break;
            case "offset-header":U32(b,10,50);break;
            case "offset-past":U32(b,10,uint.MaxValue);break;
            case "gap":b=[..b[..54],0,0,0,0,..b[54..]];U32(b,10,58);U32(b,2,(uint)b.Length);break;
            case "image-size-small":U32(b,34,23);break;
            case "image-size-max":U32(b,34,uint.MaxValue);break;
            case "last-row-short":b=b[..^1];U32(b,2,(uint)b.Length);break;
            case "tail":b=[..b,1];U32(b,2,(uint)b.Length);break;
            default:throw new ArgumentOutOfRangeException(nameof(kind));
        }return b;
    }
    public static IEnumerable<object[]> Rejections()
    {
        foreach(string kind in new[]{"short-file","short-info","core","v4","v5","header-max","reserved1","reserved2","size-zero","size-large","size-small","width-zero","width-negative","height-zero","height-min","width-max","height-max","width-side","pixels","planes","depth1","depth8","depth16","depth32","rle8","rle4","bitfields","embedded-jpeg","embedded-png","palette","palette-max","important","offset-header","offset-past","gap","image-size-small","image-size-max","last-row-short","tail"})yield return [kind,kind is "width-max" or "height-max" or "width-side" or "pixels"?ImageFailure.ImageTooLarge:kind is "core" or "v4" or "v5" or "header-max" or "depth1" or "depth8" or "depth16" or "depth32" or "rle8" or "rle4" or "bitfields" or "embedded-jpeg" or "embedded-png" or "palette" or "palette-max" or "important" or "gap"?ImageFailure.UnsupportedImage:ImageFailure.InvalidImage];
    }
    [Theory][MemberData(nameof(Rejections))]
    public async Task MalformedAndUnsupportedVariantsCannotStartAChildOrAllocateANativeCopy(string kind,ImageFailure failure)
    {
        byte[] input=Broken(kind);var starter=new Starter();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),starter);var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.PrepareAsync(input,Job(input)));Assert.Equal(failure,error.Code);Assert.Equal(0,starter.Starts);int copies=0;var direct=Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,Job(input) with{PrepareLongEdge=2560},default,_=>copies++));Assert.Equal(failure,direct.Code);Assert.Equal(0,copies);
    }
    [Fact]public void ForgedRawBmpAlphaCannotPassCommitEvenWithACorrectNewDigest()
    {
        byte[] input=Bmp(1,1),pixels=[1,2,3,0];var job=Job(input);var metadata=new ImageDecodeReply(job,ImageFailure.None,EncodedRasterFormat.Bmp,1,1,4,4,ImageDecodePolicy.Hash(pixels),"BGRA8888-straight",1,1);byte[]? actual=null;var id=Guid.NewGuid();using var receiver=new ImageRasterReceiver(id,job,metadata,b=>actual=b);receiver.Chunk(new(IpcMessageType.BinaryChunk,id,[0,0,0,0,..pixels]));Assert.Throws<InvalidDataException>(()=>receiver.Commit(new(IpcMessageType.ImageDecodeCommit,id,[])));Assert.NotNull(actual);Assert.True(actual.AsSpan().IndexOfAnyExcept((byte)0)<0);
    }
    [Theory][InlineData("success")][InlineData("cancel")][InlineData("observer")][InlineData("native-fail")]
    public void TheActualNativeCopyClearsOnEveryOwnedExit(string fault)
    {
        byte[] input=Bmp(),unchanged=input.ToArray();byte[]? actual=null;using var cancellation=new CancellationTokenSource();
        void Observe(byte[] b){actual=b;Assert.Equal(input,b);if(fault=="cancel")cancellation.Cancel();else if(fault=="observer")throw new IOException("SYNTHETIC_BMP_COPY");else if(fault=="native-fail"){b[0]=0;b[1]=0;}}
        if(fault=="cancel")Assert.ThrowsAny<OperationCanceledException>(()=>ImageRasterDecoder.Decode(input,Job(input),cancellation.Token,Observe));else if(fault=="observer")Assert.Throws<IOException>(()=>ImageRasterDecoder.Decode(input,Job(input),default,Observe));else if(fault=="native-fail")Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,Job(input),default,Observe));else{using var raster=ImageRasterDecoder.Decode(input,Job(input),default,Observe);Assert.Equal(EncodedRasterFormat.Bmp,raster.Metadata.Format);}
        Assert.NotNull(actual);Assert.True(actual.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.Equal(unchanged,input);
    }
    [Theory][InlineData("length")][InlineData("digest")]
    public async Task OriginalJobCannotBeSubstitutedBeforeCopyOrChild(string fault)
    {
        byte[] input=Bmp();var original=Job(input);var job=fault=="length"?original with{ByteLength=input.Length+1}:original with{Sha256=new string('0',64)};int copies=0;var error=Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,job,default,_=>copies++));Assert.Equal(ImageFailure.InvalidImage,error.Code);Assert.Equal(0,copies);var starter=new Starter();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),starter);await Assert.ThrowsAsync<InvalidDataException>(()=>client.PrepareAsync(input,job));Assert.Equal(0,starter.Starts);
    }
    [Theory][InlineData("prepared-source")][InlineData("extra-byte")][InlineData("exit7")]
    public async Task BmpStillRequiresExactSourceMetadataAndFullOriginalExit(string fault)
    {
        byte[] input=Bmp();byte[]? owned=null;int encodes=0;await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),new Starter(fault)){DecodeAllocationForTest=b=>owned=b,EncodePreparedForTest=(_,_,_)=>{encodes++;throw new InvalidOperationException("Must not encode rejected raster.");}};await Assert.ThrowsAnyAsync<Exception>(()=>client.PreparePngAsync(input,Job(input)));Assert.Equal(0,encodes);if(fault=="prepared-source")Assert.Null(owned);else{Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);}Assert.True(client.ObserveExit());
    }
    [Fact]public async Task BmpCancellationAfterCompleteRasterWaitsForChildExitAndClearsHeldPixels()
    {
        byte[] input=Bmp();byte[]? owned=null;using var cancel=new CancellationTokenSource();var received=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),new Starter("hold-exit")){DecodeAllocationForTest=b=>owned=b,DecodeReceivedForTest=()=>received.SetResult()};var task=client.PreparePngAsync(input,Job(input),cancellation:cancel.Token);await received.Task.WaitAsync(TimeSpan.FromSeconds(10));Assert.False(task.IsCompleted);cancel.Cancel();await Assert.ThrowsAnyAsync<Exception>(()=>task);Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.True(client.ObserveExit());
    }
    [Fact]public async Task IgnoredCancellationJoinsTheRealBmpDecoderAndClearsItsLateRaster()
    {
        byte[] input=Bmp();var job=Job(input) with{PrepareLongEdge=2560};var id=Guid.NewGuid();using var control=new MemoryStream();using var wire=new MemoryStream();await IpcFrameCodec.WriteAsync(control,new(IpcMessageType.ImageCancel,id,[]));control.Position=0;var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);byte[]? copy=null;ReadOnlyMemory<byte> late=default;
        var task=ImageDecodeSession.RunAsync(control,wire,id,input,job,(bytes,value,_)=>{entered.SetResult();release.Task.GetAwaiter().GetResult();var raster=ImageRasterDecoder.Decode(bytes,value,default,b=>copy=b);late=raster.Bytes;return raster;},null);await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.False(task.IsCompleted);release.SetResult();await task.WaitAsync(TimeSpan.FromSeconds(5));Assert.NotNull(copy);Assert.True(copy.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.False(late.IsEmpty);Assert.True(late.Span.IndexOfAnyExcept((byte)0)<0);
    }
    [Theory][InlineData(16384,1)][InlineData(1,16384)]
    public async Task ExactSideLimitStillProducesABoundedCompleteRawRaster(int width,int height)
    {
        byte[] input=Bmp(width,height);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var raster=await client.DecodeAsync(input,Job(input));Assert.Equal(width,raster.Metadata.Width);Assert.Equal(height,raster.Metadata.Height);Assert.Equal(width*height*4,raster.Bytes.Length);Assert.True(client.ObserveExit());
    }
}
