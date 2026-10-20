using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using KuaiYi.ImageWorker;
using KuaiYi.Contracts;
using SkiaSharp;
using KuaiYi.Imaging;
using Xunit;
namespace KuaiYi.Codec.Tests;

public sealed class PngExifTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private static string Worker()
    {for(var p=new DirectoryInfo(AppContext.BaseDirectory);p!=null;p=p.Parent)if(File.Exists(Path.Combine(p.FullName,"SPEC.md")))return Path.Combine(p.FullName,"tools/KuaiYi.ImageDecodeFixture/bin/Release/net10.0/KuaiYi.ImageDecodeFixture.dll");throw new IOException("No synthetic worker.");}
    private sealed class Starter(string? fault=null):IImageProcessStarter
    {public int Starts;public Process Start(ProcessStartInfo info){Starts++;if(fault!=null)info.Environment["KUAIYI_CODEC_FIXTURE_FAULT"]=fault;return Process.Start(info)??throw new IOException("No child.");}}
    private static ImageDecodeJob Job(byte[] bytes)=>new(Guid.NewGuid(),1,1,bytes.Length,ImageDecodePolicy.Hash(bytes));
    private static byte[] Gray(params byte[] values)=>values.SelectMany(v=>new byte[]{v,v,v,255}).ToArray();
    private static byte[] Png()
    {using var pixels=new ScreenshotPixels(new(0,0,3,2),Gray(10,50,90,130,180,230));using var png=ScreenshotPngEncoder.Encode(pixels);return png.Bytes.ToArray();}
    private static void U16(Span<byte> bytes,ushort value,bool little)
    {if(little)BinaryPrimitives.WriteUInt16LittleEndian(bytes,value);else BinaryPrimitives.WriteUInt16BigEndian(bytes,value);}
    private static void U32(Span<byte> bytes,uint value,bool little)
    {if(little)BinaryPrimitives.WriteUInt32LittleEndian(bytes,value);else BinaryPrimitives.WriteUInt32BigEndian(bytes,value);}
    private static byte[] Tiff(int orientation,bool little=true)
    {byte[] bytes=new byte[26];bytes[0]=bytes[1]=(byte)(little?'I':'M');U16(bytes.AsSpan(2),42,little);U32(bytes.AsSpan(4),8,little);U16(bytes.AsSpan(8),1,little);U16(bytes.AsSpan(10),0x112,little);U16(bytes.AsSpan(12),3,little);U32(bytes.AsSpan(14),1,little);U16(bytes.AsSpan(18),(ushort)orientation,little);return bytes;}
    private static byte[] Chunk(string name,byte[] payload)
    {
        byte[] chunk=new byte[payload.Length+12];BinaryPrimitives.WriteUInt32BigEndian(chunk,(uint)payload.Length);System.Text.Encoding.ASCII.GetBytes(name).CopyTo(chunk,4);payload.CopyTo(chunk,8);uint crc=uint.MaxValue;
        foreach(byte value in chunk.AsSpan(4,payload.Length+4)){crc^=value;for(int bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0u);}BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(payload.Length+8),~crc);return chunk;
    }
    private static byte[] WithExif(byte[] tiff)=>[..Png()[..33],..Chunk("eXIf",tiff),..Png()[33..]];
    private static byte[] ReadRgb(ReadOnlyMemory<byte> png)
    {
        var bytes=png.Span;using var data=new MemoryStream();int at=8;var names=new List<string>();
        while(at<bytes.Length){int length=checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes[at..]));string type=System.Text.Encoding.ASCII.GetString(bytes.Slice(at+4,4));names.Add(type);if(type=="IDAT")data.Write(bytes.Slice(at+8,length));at+=length+12;}
        Assert.Equal(new[]{"IHDR","IDAT","IEND"},names);data.Position=0;using var zip=new ZLibStream(data,CompressionMode.Decompress);using var rows=new MemoryStream();zip.CopyTo(rows);return rows.ToArray();
    }
    [Fact]public async Task PngExifSixBecomesMetadataFreePngWithTheRecordedCorrectMapping()
    {
        byte[] input=WithExif(Tiff(6)),unchanged=input.ToArray();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var png=await client.PreparePngAsync(input,Job(input));
        Assert.Equal(EncodedRasterFormat.Png,png.SourceMetadata.Format);Assert.Equal(ImageOrientation.ExifRightTop,png.Transform.Orientation);Assert.Equal(new ImageSize(2,3),png.Transform.NormalizedSize);Assert.False(png.Transform.HasDesktopAnchor);Assert.Equal(new NormalizedImagePoint(2,0),png.Transform.OriginalToNormalized(new OriginalImagePoint(0,0)));
        Assert.Equal(new byte[]{0,130,130,130,10,10,10,0,180,180,180,50,50,50,0,230,230,230,90,90,90},ReadRgb(png.Bytes));Assert.Equal(unchanged,input);Assert.True(client.ObserveExit());
    }
    [Fact]public void NativePngDecoderReturnsTheOriginalCanvasWithoutApplyingTheRequestedOrientation()
    {
        byte[] input=WithExif(Tiff(6));using var stream=new MemoryStream(input,false);using var codec=SKCodec.Create(stream,out var created);Assert.NotNull(codec);
        output.WriteLine($"Synthetic fixed native: result={created}; format={codec.EncodedFormat}; origin={(int)codec.EncodedOrigin}; frames={codec.FrameCount}; size={codec.Info.Width}x{codec.Info.Height}");
        if(OperatingSystem.IsLinux())Assert.Equal(SKEncodedOrigin.TopLeft,codec.EncodedOrigin); // Measured fixed Linux implementation, not a Windows acceptance claim.
        Assert.Equal(SKCodecResult.Success,created);Assert.Equal(3,codec.Info.Width);Assert.Equal(2,codec.Info.Height);byte[] pixels=new byte[24];Assert.Equal(SKCodecResult.Success,codec.GetPixels(new SKImageInfo(3,2,SKColorType.Bgra8888,SKAlphaType.Unpremul),pixels));Assert.Equal(Gray(10,50,90,130,180,230),pixels);
    }
    private static int[] Order(int o)=>o switch{1=>[0,1,2,3,4,5],2=>[2,1,0,5,4,3],3=>[5,4,3,2,1,0],4=>[3,4,5,0,1,2],5=>[0,3,1,4,2,5],6=>[3,0,4,1,5,2],7=>[5,2,4,1,3,0],8=>[2,5,1,4,0,3],_=>throw new ArgumentOutOfRangeException(nameof(o))};
    public static IEnumerable<object[]> Directions(){for(int i=1;i<=8;i++)foreach(bool little in new[]{true,false})yield return [i,little];}
    [Theory][MemberData(nameof(Directions))]
    public async Task ActualChildAppliesEveryPngExifDirectionExactlyOnceForBothByteOrders(int orientation,bool little)
    {
        byte[] input=WithExif(Tiff(orientation,little)),unchanged=input.ToArray();var job=Job(input);byte[]? owned=null;await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){DecodeAllocationForTest=b=>owned=b};var raster=await client.PrepareAsync(input,job);
        Assert.Equal(job with{PrepareLongEdge=2560},raster.Metadata.Job);Assert.Equal(new(3,2,(ImageOrientation)orientation),raster.Metadata.PreparedInfo);Assert.Equal(orientation<5?3:2,raster.Metadata.Width);Assert.Equal(orientation<5?2:3,raster.Metadata.Height);byte[] values=[10,50,90,130,180,230];Assert.Equal(Gray(Order(orientation).Select(i=>values[i]).ToArray()),raster.Bytes.ToArray());Assert.False(raster.Transform!.HasDesktopAnchor);Assert.Equal(unchanged,input);Assert.True(client.ObserveExit());raster.Dispose();Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);
    }
    private static byte[] Entries((ushort Tag,ushort Type,uint Count,uint Value)[] entries,bool little=true,uint next=0,int first=8,byte[]? tail=null)
    {
        byte[] bytes=new byte[first+2+entries.Length*12+4+(tail?.Length??0)];bytes[0]=bytes[1]=(byte)(little?'I':'M');U16(bytes.AsSpan(2),42,little);U32(bytes.AsSpan(4),(uint)first,little);U16(bytes.AsSpan(first),(ushort)entries.Length,little);
        for(int i=0;i<entries.Length;i++){var e=entries[i];var at=bytes.AsSpan(first+2+i*12);U16(at,e.Tag,little);U16(at[2..],e.Type,little);U32(at[4..],e.Count,little);if(e.Type==3&&e.Count==1)U16(at[8..],(ushort)e.Value,little);else U32(at[8..],e.Value,little);}U32(bytes.AsSpan(first+2+entries.Length*12),next,little);tail?.CopyTo(bytes,first+2+entries.Length*12+4);return bytes;
    }
    private static byte[] Profile(string kind)
    {
        byte[] b=Tiff(6);
        switch(kind)
        {
            case "empty":return [];
            case "short":return b[..7];
            case "prefix":return [.."Exif\0\0"u8.ToArray(),..b];
            case "byteorder":b[0]=0;break;
            case "magic":b[2]=43;break;
            case "first-header":U32(b.AsSpan(4),4,true);break;
            case "first-odd":U32(b.AsSpan(4),9,true);break;
            case "first-overflow":U32(b.AsSpan(4),uint.MaxValue,true);break;
            case "ifd-truncated":return b[..^1];
            case "entry-cap":U16(b.AsSpan(8),257,true);break;
            case "entry-truncated":U16(b.AsSpan(8),256,true);break;
            case "zero":b[18]=0;break;
            case "nine":b[18]=9;break;
            case "type":b[12]=4;break;
            case "count-zero":b[14]=0;break;
            case "count-two":b[14]=2;break;
            case "duplicate":return Entries([(0x112,3,1,6),(0x112,3,1,6)]);
            case "conflict":return Entries([(0x112,3,1,6),(0x112,3,1,8)]);
            case "next-self":U32(b.AsSpan(22),8,true);break;
            case "next-past":U32(b.AsSpan(22),uint.MaxValue,true);break;
            case "subifd":return Entries([(0x112,3,1,6),(0x14a,4,1,8)]);
            case "gps":return Entries([(0x112,3,1,6),(0x8825,4,1,8)]);
            case "exif":return Entries([(0x112,3,1,6),(0x8769,4,1,8)]);
            case "interop":return Entries([(0x112,3,1,6),(0xa005,4,1,8)]);
            case "unknown-type":return Entries([(0x112,3,1,6),(0x10e,13,1,0)]);
            case "value-header":return Entries([(0x112,3,1,6),(0x10e,2,5,4)]);
            case "value-directory":return Entries([(0x112,3,1,6),(0x10e,2,5,12)]);
            case "value-next-field":return Entries([(0x112,3,1,6),(0x10e,2,5,35)],tail:new byte[8]);
            case "value-offset-overflow":return Entries([(0x112,3,1,6),(0x10e,2,5,uint.MaxValue)]);
            case "value-length-overflow":return Entries([(0x112,3,1,6),(0x10e,12,uint.MaxValue,38)]);
            case "width":return Entries([(0x112,3,1,6),(0x100,4,1,4)]);
            case "height":return Entries([(0x112,3,1,6),(0x101,3,1,3)]);
            case "duplicate-width":return Entries([(0x100,4,1,3),(0x100,3,1,3)]);
            case "dimension-type":return Entries([(0x100,1,1,3)]);
            case "dimension-count":return Entries([(0x100,3,2,3)]);
            case "profile-cap":Array.Resize(ref b,65528);break;
            default:throw new ArgumentOutOfRangeException(nameof(kind));
        }
        return b;
    }
    public static IEnumerable<object[]> InvalidProfiles()
    {
        foreach(string kind in new[]{"empty","short","prefix","byteorder","magic","first-header","first-odd","first-overflow","ifd-truncated","entry-cap","entry-truncated","zero","nine","type","count-zero","count-two","duplicate","conflict","next-self","next-past","subifd","gps","exif","interop","unknown-type","value-header","value-directory","value-next-field","value-offset-overflow","value-length-overflow","width","height","duplicate-width","dimension-type","dimension-count","profile-cap"})yield return [kind,kind is "entry-cap" or "next-self" or "next-past" or "subifd" or "gps" or "exif" or "interop" or "unknown-type" or "profile-cap"?ImageFailure.UnsupportedImage:ImageFailure.InvalidImage];
    }
    [Theory][MemberData(nameof(InvalidProfiles))]
    public async Task InvalidOrUnsupportedProfilesCannotStartAChildOrAllocateNativeInput(string kind,ImageFailure failure)
    {
        byte[] input=WithExif(Profile(kind));int copies=0;var starter=new Starter();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),starter);
        var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.PrepareAsync(input,Job(input)));Assert.Equal(failure,error.Code);Assert.Equal(0,starter.Starts);var direct=Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,Job(input) with{PrepareLongEdge=2560},default,_=>copies++));Assert.Equal(failure,direct.Code);Assert.Equal(0,copies);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task MissingOrientationAndValidUnknownOrSharedValuesUseIdentityOrTheSingleDeclaredOrientation(bool little)
    {
        var profiles=new[]{Entries([],little),Entries([(0x100,3,1,3),(0x101,4,1,2)],little,first:12),Entries([(0x112,3,1,6),(0x10e,2,6,50),(0x131,2,6,50)],little,tail:"test\0\0"u8.ToArray())};
        for(int i=0;i<profiles.Length;i++){byte[] input=WithExif(profiles[i]);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var raster=await client.PrepareAsync(input,Job(input));Assert.Equal(i==2?ImageOrientation.ExifRightTop:ImageOrientation.ExifTopLeft,raster.Metadata.PreparedInfo!.SourceOrientation);Assert.True(client.ObserveExit());}
    }
    [Theory][InlineData(2)][InlineData(6)]
    public async Task RawNonidentityPngIsRejectedBeforeNativeCopyEvenIfNativeWouldIgnoreExif(int orientation)
    {
        byte[] input=WithExif(Tiff(orientation));int copies=0;var direct=Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,Job(input),default,_=>copies++));Assert.Equal(ImageFailure.UnsupportedImage,direct.Code);Assert.Equal(0,copies);int pixels=0;await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){DecodeAllocationForTest=_=>pixels++};var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.DecodeAsync(input,Job(input)));Assert.Equal(ImageFailure.UnsupportedImage,error.Code);Assert.Equal(0,pixels);Assert.True(client.ObserveExit());
    }
    private static byte[] Container(string kind)
    {
        byte[] p=Png(),exif=Chunk("eXIf",Tiff(6));int length=(int)BinaryPrimitives.ReadUInt32BigEndian(p.AsSpan(33));byte[] data=p.AsSpan(41,length).ToArray();
        return kind switch
        {
            "duplicate"=>[..p[..33],..exif,..exif,..p[33..]],
            "conflict"=>[..p[..33],..exif,..Chunk("eXIf",Tiff(8)),..p[33..]],
            "after-idat"=>[..p[..^12],..exif,..p[^12..]],
            "split-idat"=>[..p[..33],..exif,..Chunk("IDAT",data[..(length/2)]),..Chunk("tEXt","key\0value"u8.ToArray()),..Chunk("IDAT",data[(length/2)..]),..p[^12..]],
            "crc"=>[..p[..33],..exif[..^1],(byte)(exif[^1]^1),..p[33..]],
            "length"=>[..p[..33],255,255,255,255,..exif[4..],..p[33..]],
            "acTL" or "fcTL" or "fdAT"=>[..p[..33],..exif,..Chunk(kind,new byte[kind=="acTL"?8:kind=="fcTL"?26:4]),..p[33..]],
            "before-ihdr"=>[..p[..8],..exif,..p[8..]],
            "tail"=>[..WithExif(Tiff(6)),1],
            _=>throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
    [Theory][InlineData("duplicate")][InlineData("conflict")][InlineData("after-idat")][InlineData("split-idat")][InlineData("crc")][InlineData("length")][InlineData("before-ihdr")][InlineData("tail")][InlineData("acTL")][InlineData("fcTL")][InlineData("fdAT")]
    public async Task ContainerConflictsCrcAndAnimationsAreRejectedByParentAndWorker(string kind)
    {
        byte[] input=Container(kind);ImageFailure expected=kind is "acTL" or "fcTL" or "fdAT"?ImageFailure.UnsupportedImage:ImageFailure.InvalidImage;var starter=new Starter();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),starter);var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.PrepareAsync(input,Job(input)));Assert.Equal(expected,error.Code);Assert.Equal(0,starter.Starts);int copies=0;var direct=Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,Job(input) with{PrepareLongEdge=2560},default,_=>copies++));Assert.Equal(expected,direct.Code);Assert.Equal(0,copies);
    }
    [Fact]public void NativeCopyRemovesOnlyTheValidatedExifChunkAndKeepsEveryOtherByte()
    {
        byte[] p=Png(),text=Chunk("tEXt","Synthetic\0public-metadata"u8.ToArray()),expected=[..p[..33],..text,..p[33..]],input=[..p[..33],..text,..Chunk("eXIf",Tiff(6)),..p[33..]],unchanged=input.ToArray();byte[] copy=EncodedImageOrientation.CopyPngForNative(input,true,out var origin,default);
        try{Assert.Equal(ImageOrientation.ExifRightTop,origin);Assert.Equal(expected,copy);Assert.Equal(input.Length-38,copy.Length);Assert.Equal(unchanged,input);}finally{CryptographicOperations.ZeroMemory(copy);}
        copy=EncodedImageOrientation.CopyPngForNative(p,false,out origin,default);try{Assert.Equal(ImageOrientation.ExifTopLeft,origin);Assert.Equal(p,copy);}finally{CryptographicOperations.ZeroMemory(copy);}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void TheActualFilledNativeCopyClearsWhenAllocationObserverThrowsOrCancels(bool cancel)
    {
        byte[] input=WithExif(Tiff(6)),original=input.ToArray();byte[]? observed=null;using var cancellation=new CancellationTokenSource();
        void Observe(byte[] b){observed=b;Assert.Contains(b,v=>v!=0);if(cancel)cancellation.Cancel();else throw new IOException("SYNTHETIC_NATIVE_COPY_OBSERVER");}
        if(cancel)Assert.ThrowsAny<OperationCanceledException>(()=>ImageRasterDecoder.Decode(input,Job(input) with{PrepareLongEdge=2560},cancellation.Token,Observe));else Assert.Throws<IOException>(()=>ImageRasterDecoder.Decode(input,Job(input) with{PrepareLongEdge=2560},default,Observe));Assert.NotNull(observed);Assert.True(observed.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.Equal(original,input);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void ActualDecoderClearsSanitizedInputOnSuccessAndCompressedPixelFailure(bool broken)
    {
        byte[] input=WithExif(Tiff(6));if(broken){int at=33+38,length=(int)BinaryPrimitives.ReadUInt32BigEndian(input.AsSpan(at));input=[..input[..at],..Chunk("IDAT",input.AsSpan(at+8,Math.Max(1,length/2)).ToArray()),..input[^12..]];}
        byte[]? observed=null;var job=Job(input) with{PrepareLongEdge=2560};if(broken)Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,job,default,b=>observed=b));else{using var raster=ImageRasterDecoder.Decode(input,job,default,b=>observed=b);Assert.Equal(ImageOrientation.ExifRightTop,raster.Metadata.PreparedInfo!.SourceOrientation);Assert.Equal(job,raster.Metadata.Job);}
        Assert.NotNull(observed);Assert.True(observed.AsSpan().IndexOfAnyExcept((byte)0)<0);
    }
    [Fact]public async Task IgnoredCancellationStillJoinsTheOriginalDecoderAndErasesLateRasterAndNativeCopy()
    {
        byte[] input=WithExif(Tiff(6));var job=Job(input) with{PrepareLongEdge=2560};var id=Guid.NewGuid();using var control=new MemoryStream();using var wire=new MemoryStream();await IpcFrameCodec.WriteAsync(control,new(IpcMessageType.ImageCancel,id,[]));control.Position=0;var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);byte[]? copy=null;ReadOnlyMemory<byte> late=default;
        var task=ImageDecodeSession.RunAsync(control,wire,id,input,job,(bytes,value,_)=>{entered.SetResult();release.Task.GetAwaiter().GetResult();var raster=ImageRasterDecoder.Decode(bytes,value,default,b=>copy=b);late=raster.Bytes;return raster;},null);await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.False(task.IsCompleted);release.SetResult();await task.WaitAsync(TimeSpan.FromSeconds(5));Assert.NotNull(copy);Assert.True(copy.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.False(late.IsEmpty);Assert.True(late.Span.IndexOfAnyExcept((byte)0)<0);
    }
    [Fact]public void ExactMetadataAndEntryAndChunkLimitsRemainBoundedWithoutAllocatingFromDeclaredCounts()
    {
        byte[] profile=Tiff(6);Array.Resize(ref profile,65527);Assert.Equal(ImageOrientation.ExifRightTop,EncodedImageOrientation.Read(WithExif(profile),EncodedRasterFormat.Png));
        var entries=Enumerable.Range(0,256).Select(i=>((ushort)(i==0?0x112:0x2000+i),(ushort)(i==0?3:1),1u,(uint)(i==0?6:1))).ToArray();Assert.Equal(ImageOrientation.ExifRightTop,EncodedImageOrientation.Read(WithExif(Entries(entries)),EncodedRasterFormat.Png));
        byte[] p=Png();var filler=Chunk("tEXt","k\0v"u8.ToArray());using var chunks=new MemoryStream();chunks.Write(p.AsSpan(0,33));chunks.Write(Chunk("eXIf",Tiff(6)));for(int i=0;i<252;i++)chunks.Write(filler);chunks.Write(p.AsSpan(33));Assert.Equal(ImageOrientation.ExifRightTop,EncodedImageOrientation.Read(chunks.ToArray(),EncodedRasterFormat.Png));byte[] over=[..chunks.ToArray()[..^12],..filler,..p[^12..]];var error=Assert.Throws<ImageJobException>(()=>EncodedImageOrientation.Read(over,EncodedRasterFormat.Png));Assert.Equal(ImageFailure.UnsupportedImage,error.Code);
        using var canceled=new CancellationTokenSource();canceled.Cancel();Assert.ThrowsAny<OperationCanceledException>(()=>EncodedImageOrientation.Read(WithExif(profile),EncodedRasterFormat.Png,canceled.Token));
    }
    [Theory][InlineData("length")][InlineData("digest")]
    public async Task SanitizationCannotRebindTheOriginalJobLengthOrDigest(string kind)
    {
        byte[] input=WithExif(Tiff(6));var original=Job(input);var job=kind=="length"?original with{ByteLength=input.Length+1}:original with{Sha256=new string('0',64)};int copies=0;var direct=Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,job with{PrepareLongEdge=2560},default,_=>copies++));Assert.Equal(ImageFailure.InvalidImage,direct.Code);Assert.Equal(0,copies);
        var starter=new Starter();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),starter);await Assert.ThrowsAsync<InvalidDataException>(()=>client.PrepareAsync(input,job));Assert.Equal(0,starter.Starts);
    }
    [Theory][InlineData("IHDR")][InlineData("IDAT")][InlineData("tEXt")]
    public async Task AllOriginalChunkCrcsStillPassBeforeSanitizationEvenWhenMetadataCrcIsValid(string target)
    {
        byte[] input=WithExif(Tiff(6));if(target=="tEXt")input=[..input[..33],..Chunk("tEXt","k\0v"u8.ToArray()),..input[33..]];
        int at=8;while(!input.AsSpan(at+4,4).SequenceEqual(System.Text.Encoding.ASCII.GetBytes(target)))at+=checked((int)BinaryPrimitives.ReadUInt32BigEndian(input.AsSpan(at))+12);int length=(int)BinaryPrimitives.ReadUInt32BigEndian(input.AsSpan(at));input[at+8+length]^=1;
        int copies=0;var direct=Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,Job(input) with{PrepareLongEdge=2560},default,_=>copies++));Assert.Equal(ImageFailure.InvalidImage,direct.Code);Assert.Equal(0,copies);var starter=new Starter();int pixels=0;await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),starter){DecodeAllocationForTest=_=>pixels++};var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.PrepareAsync(input,Job(input)));Assert.Equal(ImageFailure.InvalidImage,error.Code);Assert.Equal(1,starter.Starts);Assert.Equal(0,pixels);Assert.True(client.ObserveExit());
    }
    [Theory][InlineData("prepared-source")][InlineData("extra-byte")][InlineData("exit7")]
    public async Task ValidPngExifStillRequiresExactSourceMetadataAndTheFullOriginalProcessExitGate(string fault)
    {
        byte[] input=WithExif(Tiff(6));byte[]? owned=null;int encodes=0;await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),new Starter(fault)){DecodeAllocationForTest=b=>owned=b,EncodePreparedForTest=(_,_,_)=>{encodes++;throw new InvalidOperationException("Must not encode rejected raster.");}};
        await Assert.ThrowsAnyAsync<Exception>(()=>client.PreparePngAsync(input,Job(input)));Assert.Equal(0,encodes);if(fault=="prepared-source")Assert.Null(owned);else{Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);}Assert.True(client.ObserveExit());
    }
    [Fact]public async Task CancellationAfterAnOrientedRasterStillWaitsForActualChildExitAndClearsTheUndeliveredBuffer()
    {
        byte[] input=WithExif(Tiff(6));byte[]? owned=null;using var cancel=new CancellationTokenSource();var received=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),new Starter("hold-exit")){DecodeAllocationForTest=b=>owned=b,DecodeReceivedForTest=()=>received.SetResult()};var task=client.PreparePngAsync(input,Job(input),cancellation:cancel.Token);await received.Task.WaitAsync(TimeSpan.FromSeconds(10));Assert.False(task.IsCompleted);cancel.Cancel();await Assert.ThrowsAnyAsync<Exception>(()=>task);Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.True(client.ObserveExit());
    }
}
