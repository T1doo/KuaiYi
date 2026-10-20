using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using KuaiYi.Imaging;
using KuaiYi.ImageWorker;
using SkiaSharp;
using Xunit;
namespace KuaiYi.Codec.Tests;

public sealed class WebpDecodeTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private static string Worker()
    {for(var p=new DirectoryInfo(AppContext.BaseDirectory);p!=null;p=p.Parent)if(File.Exists(Path.Combine(p.FullName,"SPEC.md")))return Path.Combine(p.FullName,"tools/KuaiYi.ImageDecodeFixture/bin/Release/net10.0/KuaiYi.ImageDecodeFixture.dll");throw new IOException("No synthetic worker.");}
    private sealed class Starter(string? fault=null):IImageProcessStarter
    {public int Starts;public Process Start(ProcessStartInfo info){Starts++;if(fault!=null)info.Environment["KUAIYI_CODEC_FIXTURE_FAULT"]=fault;return Process.Start(info)??throw new IOException("No child.");}}
    private static ImageDecodeJob Job(byte[] bytes)=>new(Guid.NewGuid(),1,1,bytes.Length,ImageDecodePolicy.Hash(bytes));
    private static byte[] Gray(params byte[] values)=>values.SelectMany(v=>new byte[]{v,v,v,255}).ToArray();
    private static byte[] Encode(byte[] pixels,int width,int height,bool lossless=true)
    {
        using var bitmap=new SKBitmap(new SKImageInfo(width,height,SKColorType.Bgra8888,SKAlphaType.Unpremul));Marshal.Copy(pixels,0,bitmap.GetPixels(),pixels.Length);using var pixmap=bitmap.PeekPixels();using var data=pixmap.Encode(new SKWebpEncoderOptions(lossless?SKWebpEncoderCompression.Lossless:SKWebpEncoderCompression.Lossy,100))??throw new InvalidDataException("Synthetic WebP encoding failed.");return data.ToArray();
    }
    // Test fixture selection only: lossless quality controls encoder effort, never source pixels.
    // Product request encoding has no retry. Select an actual even-payload sample for this subset.
    private static byte[] EvenLossless(byte[] pixels,int width,int height)
    {
        using var bitmap=new SKBitmap(new SKImageInfo(width,height,SKColorType.Bgra8888,SKAlphaType.Unpremul));Marshal.Copy(pixels,0,bitmap.GetPixels(),pixels.Length);using var pixmap=bitmap.PeekPixels();
        foreach(int effort in new[]{100,75,50,25,0}){using var data=pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless,effort))??throw new InvalidDataException("Synthetic encoding failed.");byte[] encoded=data.ToArray();if((ImagePayload(encoded,"VP8L").Length&1)==0)return encoded;}throw new InvalidDataException("No even-payload synthetic fixture for this canvas.");
    }
    private static byte[] ReadRgb(ReadOnlyMemory<byte> png)
    {
        var bytes=png.Span;using var data=new MemoryStream();int at=8;var names=new List<string>();
        while(at<bytes.Length){int length=checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes[at..]));string type=System.Text.Encoding.ASCII.GetString(bytes.Slice(at+4,4));names.Add(type);if(type=="IDAT")data.Write(bytes.Slice(at+8,length));at+=length+12;}
        Assert.Equal(new[]{"IHDR","IDAT","IEND"},names);data.Position=0;using var zip=new ZLibStream(data,CompressionMode.Decompress);using var rows=new MemoryStream();zip.CopyTo(rows);return rows.ToArray();
    }
    private static byte[] Chunk(string name,byte[] payload)
    {byte[] b=new byte[8+payload.Length+(payload.Length&1)];System.Text.Encoding.ASCII.GetBytes(name).CopyTo(b,0);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4),(uint)payload.Length);payload.CopyTo(b,8);return b;}
    private static byte[] Riff(params byte[][] chunks)
    {byte[] b=[.."RIFF"u8.ToArray(),0,0,0,0,.."WEBP"u8.ToArray(),..chunks.SelectMany(c=>c)];BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4),(uint)b.Length-8);return b;}
    private static byte[] ImagePayload(byte[] encoded,string name)
    {for(int at=12;at<encoded.Length;){int length=(int)BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(at+4));if(encoded.AsSpan(at,4).SequenceEqual(System.Text.Encoding.ASCII.GetBytes(name)))return encoded.AsSpan(at+8,length).ToArray();at+=8+length+(length&1);}throw new InvalidDataException("Synthetic image chunk missing.");}
    private static byte[] Extended(int width,int height,byte flags)
    {byte[] b=new byte[10];b[0]=flags;for(int i=0;i<3;i++){b[4+i]=(byte)((width-1)>>(8*i));b[7+i]=(byte)((height-1)>>(8*i));}return Chunk("VP8X",b);}
    private static byte[] AlphaPixels()=>new byte[]{0,64,128,255,32,255}.SelectMany(a=>new byte[]{0,0,0,a}).ToArray();
    [Fact]public void FixedNativeAlphaHintsProbe()
    {
        byte[] expected=AlphaPixels(),payload=ImagePayload(Encode(expected,3,2),"VP8L");
        foreach(bool extended in new[]{false,true})foreach(bool hint in new[]{false,true})foreach(bool flag in new[]{false,true})
        {
            byte[] p=payload.ToArray();p[4]=(byte)((p[4]&~16)|(hint?16:0));byte[] input=extended?Riff(Extended(3,2,flag?(byte)16:(byte)0),Chunk("VP8L",p)):Riff(Chunk("VP8L",p));using var stream=new MemoryStream(input,false);using var codec=SKCodec.Create(stream,out var created);Assert.NotNull(codec);byte[] actual=new byte[24];var result=codec.GetPixels(new SKImageInfo(3,2,SKColorType.Bgra8888,SKAlphaType.Unpremul),actual);output.WriteLine($"extended={extended} hint={hint} flag={flag} create={created} decode={result} alpha={string.Join(',',actual.Where((_,i)=>i%4==3))} exact={expected.SequenceEqual(actual)}");Assert.Equal(SKCodecResult.Success,result);Assert.Equal(expected,actual);
        }
    }
    [Fact]public async Task ActualStaticWebpBecomesMetadataFreePngWithTheWholeCanvas()
    {
        byte[] input=Encode(Gray(10,50,90,130,180,230),3,2),unchanged=input.ToArray();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var png=await client.PreparePngAsync(input,Job(input));Assert.Equal("Webp",png.SourceMetadata.Format.ToString());Assert.Equal(new ImageSize(3,2),png.Transform.NormalizedSize);Assert.False(png.Transform.HasDesktopAnchor);Assert.Equal(ImageOrientation.ExifTopLeft,png.Transform.Orientation);Assert.Equal(new byte[]{0,10,10,10,50,50,50,90,90,90,0,130,130,130,180,180,180,230,230,230},ReadRgb(png.Bytes));Assert.Equal(unchanged,input);Assert.True(client.ObserveExit());
    }
    [Theory][InlineData(false,false,false)][InlineData(false,true,false)][InlineData(true,false,false)][InlineData(true,true,false)][InlineData(true,false,true)][InlineData(true,true,true)]
    public async Task ActualChildPreservesTransparencyRegardlessOfHintsAndCompositesOnWhite(bool extended,bool hint,bool flag)
    {
        byte[] expected=AlphaPixels(),payload=ImagePayload(EvenLossless(expected,3,2),"VP8L");payload[4]=(byte)((payload[4]&~16)|(hint?16:0));byte[] input=extended?Riff(Extended(3,2,flag?(byte)16:(byte)0),Chunk("VP8L",payload)):Riff(Chunk("VP8L",payload));byte[] unchanged=input.ToArray();byte[]? owned=null;await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker()){DecodeAllocationForTest=b=>owned=b};
        var raw=await client.DecodeAsync(input,Job(input));Assert.Equal(EncodedRasterFormat.Webp,raw.Metadata.Format);Assert.Equal(expected,raw.Bytes.ToArray());Assert.Null(raw.Transform);raw.Dispose();Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.True(client.ObserveExit());await using var preparation=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var prepared=await preparation.PrepareAsync(input,Job(input));Assert.Equal(new(3,2,ImageOrientation.ExifTopLeft),prepared.Metadata.PreparedInfo);Assert.Equal(Gray(255,191,127,0,223,0),prepared.Bytes.ToArray());Assert.Equal(unchanged,input);Assert.True(preparation.ObserveExit());
    }
    [Theory][InlineData(false)][InlineData(true)]public async Task ActualLossyKeyframeDecodesTheFullOpaqueCanvas(bool extended)
    {
        byte[] encoded=Encode(Gray(Enumerable.Repeat((byte)128,17*9).ToArray()),17,9,false),payload=ImagePayload(encoded,"VP8 "),input=extended?Riff(Extended(17,9,0),Chunk("VP8 ",payload)):Riff(Chunk("VP8 ",payload));await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var raster=await client.DecodeAsync(input,Job(input));Assert.Equal(17,raster.Metadata.Width);Assert.Equal(9,raster.Metadata.Height);Assert.Equal(17*9*4,raster.Bytes.Length);for(int i=0;i<raster.Bytes.Length;i++)if(i%4==3)Assert.Equal(255,raster.Bytes.Span[i]);else Assert.InRange(raster.Bytes.Span[i],127,129);Assert.True(client.ObserveExit());
    }
    [Fact]public async Task LosslessNonIntegerAreaResizeRetainsTheWholeCanvas()
    {
        byte[] input=EvenLossless(Gray(0,50,100,150,200,10,60,110,160,210,20,70,120,170,220),5,3);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var raster=await client.PrepareAsync(input,Job(input),3);Assert.Equal(new ImageSize(3,2),raster.Transform!.NormalizedSize);Assert.False(raster.Transform.HasDesktopAnchor);Assert.Equal(Gray(23,103,183,37,117,197),raster.Bytes.ToArray());Assert.True(client.ObserveExit());
    }
    private static byte[] Lossless()=>ImagePayload(Encode(Gray(10,50,90,130,180,230),3,2),"VP8L");
    private static byte[] Lossy()=>ImagePayload(Encode(Gray(Enumerable.Range(0,17*9).Select(i=>(byte)(i*37%256)).ToArray()),17,9,false),"VP8 ");
    private static void U32(byte[] b,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at),value);
    private static byte[] Broken(string kind)
    {
        byte[] p=Lossless(),b=Riff(Chunk("VP8L",p));switch(kind)
        {
            case "riff-short":return b[..7];
            case "riff-size":U32(b,4,uint.MaxValue);return b;
            case "tail":return [..b,0,0];
            case "chunk-size":U32(b,16,uint.MaxValue);return b;
            case "chunk-header":return Riff(new byte[6]);
            case "missing-image":return Riff(Extended(3,2,0));
            case "duplicate":return Riff(Chunk("VP8L",p),Chunk("VP8L",p));
            case "mixed":return Riff(Chunk("VP8L",p),Chunk("VP8 ",Lossy()));
            case "late-vp8x":return Riff(Chunk("VP8L",p),Extended(3,2,0));
            case "duplicate-vp8x":return Riff(Extended(3,2,0),Extended(3,2,0),Chunk("VP8L",p));
            case "vp8x-length":return Riff(Chunk("VP8X",new byte[9]),Chunk("VP8L",p));
            case "vp8x-reserved":b=Extended(3,2,0x80);break;
            case "vp8x-reserved-byte":b=Extended(3,2,0);b[9]=1;break;
            case "canvas-mismatch":b=Extended(4,2,0);break;
            case "canvas-side":b=Extended(16385,2,0);break;
            case "canvas-pixels":b=Extended(6000,4001,0);break;
            case "animation-flag":b=Extended(3,2,2);break;
            case "exif-flag":b=Extended(3,2,8);break;
            case "icc-flag":b=Extended(3,2,32);break;
            case "xmp-flag":b=Extended(3,2,4);break;
            case "ANIM":case "ANMF":case "EXIF":case "ICCP":case "XMP ":case "ALPH":case "JUNK":return Riff(Chunk(kind,[0,0]),Chunk("VP8L",p));
            case "late-exif":return Riff(Chunk("VP8L",p),Chunk("EXIF",[0,0]));
            case "vp8-alpha":return Riff(Extended(17,9,16),Chunk("VP8 ",Lossy()));
            case "lossless-short":return Riff(Chunk("VP8L",p[..4]));
            case "lossless-signature":p[0]=0;return Riff(Chunk("VP8L",p));
            case "lossless-version":p[4]|=32;return Riff(Chunk("VP8L",p));
            case "lossless-pixels":U32(p,1,5999u|(4000u<<14));return Riff(Chunk("VP8L",p));
            case "lossy-short":return Riff(Chunk("VP8 ",Lossy()[..9]));
            case "lossy-interframe":p=Lossy();p[0]|=1;return Riff(Chunk("VP8 ",p));
            case "lossy-version":p=Lossy();p[0]=(byte)((p[0]&~14)|8);return Riff(Chunk("VP8 ",p));
            case "lossy-hidden":p=Lossy();p[0]&=0xef;return Riff(Chunk("VP8 ",p));
            case "lossy-sync":p=Lossy();p[3]=0;return Riff(Chunk("VP8 ",p));
            case "lossy-scaling":p=Lossy();p[7]|=64;return Riff(Chunk("VP8 ",p));
            case "lossy-partition":p=Lossy();p[0]|=0xe0;p[1]=p[2]=255;return Riff(Chunk("VP8 ",p));
            case "lossy-zero-partition":p=Lossy();p[0]&=31;p[1]=p[2]=0;return Riff(Chunk("VP8 ",p));
            case "pad-nonzero":b=Riff(Chunk("VP8L",p[..5]));b[^1]=1;return b;
            case "pad-missing":b=Riff(Chunk("VP8L",p[..5]))[..^1];U32(b,4,(uint)b.Length-8);return b;
            default:throw new ArgumentOutOfRangeException(nameof(kind));
        }return Riff(b,Chunk("VP8L",p));
    }
    public static IEnumerable<object[]> Rejections()
    {
        foreach(string k in new[]{"riff-short","riff-size","tail","chunk-size","chunk-header","missing-image","duplicate","mixed","late-vp8x","duplicate-vp8x","vp8x-length","vp8x-reserved","vp8x-reserved-byte","canvas-mismatch","canvas-side","canvas-pixels","animation-flag","exif-flag","icc-flag","xmp-flag","ANIM","ANMF","EXIF","ICCP","XMP ","ALPH","JUNK","late-exif","vp8-alpha","lossless-short","lossless-signature","lossless-version","lossless-pixels","lossy-short","lossy-interframe","lossy-version","lossy-hidden","lossy-sync","lossy-scaling","lossy-partition","lossy-zero-partition","pad-nonzero","pad-missing"})
            yield return [k,k is "canvas-side" or "canvas-pixels" or "lossless-pixels"?ImageFailure.ImageTooLarge:k is "animation-flag" or "exif-flag" or "icc-flag" or "xmp-flag" or "ANIM" or "ANMF" or "EXIF" or "ICCP" or "XMP " or "ALPH" or "JUNK" or "late-exif" or "vp8-alpha" or "lossless-version" or "lossy-interframe" or "lossy-version" or "lossy-scaling"?ImageFailure.UnsupportedImage:ImageFailure.InvalidImage];
    }
    [Theory][MemberData(nameof(Rejections))]public async Task ContainerAndUnsupportedFeaturesRejectBeforeChildAndNativeAllocation(string kind,ImageFailure expected)
    {
        byte[] input=Broken(kind);var starter=new Starter();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),starter);var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.PrepareAsync(input,Job(input)));Assert.Equal(expected,error.Code);Assert.Equal(0,starter.Starts);int copies=0;var direct=Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,Job(input),default,_=>copies++));Assert.Equal(expected,direct.Code);Assert.Equal(0,copies);
    }
    [Theory][InlineData(true,1)][InlineData(true,3)][InlineData(true,10)][InlineData(false,1)][InlineData(false,3)][InlineData(false,20)]
    public async Task SynchronizedContainerLengthsDoNotHideTruncatedEntropy(bool lossless,int missing)
    {
        byte[] p=lossless?Lossless():Lossy(),input=Riff(Chunk(lossless?"VP8L":"VP8 ",p[..^missing]));output.WriteLine($"{(lossless?"VP8L":"VP8")} payload={p.Length} removed={missing}");await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.DecodeAsync(input,Job(input)));Assert.Equal(((p.Length-missing)&1)!=0?ImageFailure.UnsupportedImage:ImageFailure.InvalidImage,error.Code);Assert.True(client.ObserveExit());
    }
    [Fact]public void FixedNativeEntropyTruncationProbe()
    {
        foreach(bool lossless in new[]{true,false})
        {
            byte[] p=lossless?Lossless():Lossy();string name=lossless?"VP8L":"VP8 ";int width=lossless?3:17,height=lossless?2:9;byte[]? original=null;
            foreach(int missing in new[]{0,1,2,3,10,20,p.Length/2})
            {
                byte[] input=Riff(Chunk(name,p[..(p.Length-missing)]));using var stream=new MemoryStream(input,false);using var codec=SKCodec.Create(stream,out var created);if(codec==null){output.WriteLine($"{name} missing={missing} create={created}");continue;}byte[] actual=new byte[width*height*4];var result=codec.GetPixels(new SKImageInfo(width,height,SKColorType.Bgra8888,SKAlphaType.Unpremul),actual);if(missing==0){Assert.Equal(SKCodecResult.Success,result);original=actual;}output.WriteLine($"{name} payload={p.Length} tail={Convert.ToHexString(p[^Math.Min(20,p.Length)..])} missing={missing} result={result} exactOriginal={original!.SequenceEqual(actual)} sha={ImageDecodePolicy.Hash(actual)}");
            }
        }
    }
    [Fact]public void FixedNativePaddingBoundaryProbe()
    {
        var samples=new List<(string Name,byte[] Payload,int Width,int Height)>{("truncated46to45",Lossless()[..^1],3,2)};
        for(int width=1;width<=12;width++){byte[] p=ImagePayload(Encode(Gray(Enumerable.Range(0,width*2).Select(i=>(byte)(i*37%256)).ToArray()),width,2),"VP8L");if((p.Length&1)!=0){samples.Add(($"valid{width}x2",p,width,2));break;}}
        foreach(var sample in samples)foreach(string variant in new[]{"zero-pad","no-pad","ff-pad"})
        {
            byte[] input=Riff(Chunk("VP8L",sample.Payload));if(variant=="no-pad"){input=input[..^1];U32(input,4,(uint)input.Length-8);}else if(variant=="ff-pad")input[^1]=255;
            using var stream=new MemoryStream(input,false);using var codec=SKCodec.Create(stream,out var created);if(codec==null){output.WriteLine($"{sample.Name} payload={sample.Payload.Length} {variant} create={created}");continue;}byte[] actual=new byte[sample.Width*sample.Height*4];var result=codec.GetPixels(new SKImageInfo(sample.Width,sample.Height,SKColorType.Bgra8888,SKAlphaType.Unpremul),actual);output.WriteLine($"{sample.Name} payload={sample.Payload.Length} {variant} result={result} sha={ImageDecodePolicy.Hash(actual)}");
        }
        Assert.Equal(2,samples.Count);
    }
    [Theory][InlineData(false)][InlineData(true)]public async Task EvenALegitimateOddPayloadIsExplicitlyUnsupportedBeforeNative(bool extended)
    {
        byte[] expected=Gray(0,37),p=ImagePayload(Encode(expected,1,2),"VP8L");Assert.Equal(1,p.Length&1);byte[] input=extended?Riff(Extended(1,2,0),Chunk("VP8L",p)):Riff(Chunk("VP8L",p));using(var stream=new MemoryStream(input,false))using(var codec=SKCodec.Create(stream)){Assert.NotNull(codec);byte[] pixels=new byte[8];Assert.Equal(SKCodecResult.Success,codec.GetPixels(new SKImageInfo(1,2,SKColorType.Bgra8888,SKAlphaType.Unpremul),pixels));Assert.Equal(expected,pixels);}
        var starter=new Starter();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),starter);var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.DecodeAsync(input,Job(input)));Assert.Equal(ImageFailure.UnsupportedImage,error.Code);Assert.Equal(0,starter.Starts);int copies=0;Assert.Equal(ImageFailure.UnsupportedImage,Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,Job(input),default,_=>copies++)).Code);Assert.Equal(0,copies);
    }
    [Fact]public async Task ColoredHalfTransparencyRemainsStraightBgraAndUsesIndependentWhiteArithmetic()
    {
        byte[] pixels=[0,64,128,128,200,40,80,255,0,0,0,0,255,0,128,64,30,180,90,192,20,60,100,255],input=EvenLossless(pixels,3,2);byte[] expected=pixels.ToArray();for(int i=0;i<expected.Length;i+=4){int alpha=expected[i+3];for(int c=0;c<3;c++)expected[i+c]=(byte)((pixels[i+c]*alpha+255*(255-alpha)+127)/255);expected[i+3]=255;}
        await using var rawClient=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var raw=await rawClient.DecodeAsync(input,Job(input));Assert.Equal(pixels,raw.Bytes.ToArray());Assert.True(rawClient.ObserveExit());await using var prepareClient=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var prepared=await prepareClient.PrepareAsync(input,Job(input));Assert.Equal(expected,prepared.Bytes.ToArray());Assert.True(prepareClient.ObserveExit());
    }
    [Theory][InlineData("success")][InlineData("cancel")][InlineData("observer")][InlineData("native-fail")]
    public void ActualOwnedNativeCopyAlwaysClearsAndOriginalBytesRemainBound(string fault)
    {
        byte[] input=Riff(Chunk("VP8L",Lossless())),unchanged=input.ToArray();byte[]? actual=null;using var cancel=new CancellationTokenSource();
        void Observe(byte[] bytes){actual=bytes;Assert.Equal(input,bytes);if(fault=="cancel")cancel.Cancel();else if(fault=="observer")throw new IOException("SYNTHETIC_WEBP_COPY");else if(fault=="native-fail"){bytes[0]=bytes[1]=0;}}
        if(fault=="cancel")Assert.ThrowsAny<OperationCanceledException>(()=>ImageRasterDecoder.Decode(input,Job(input),cancel.Token,Observe));else if(fault=="observer")Assert.Throws<IOException>(()=>ImageRasterDecoder.Decode(input,Job(input),default,Observe));else if(fault=="native-fail")Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,Job(input),default,Observe));else{using var raster=ImageRasterDecoder.Decode(input,Job(input),default,Observe);Assert.Equal(Gray(10,50,90,130,180,230),raster.Bytes.ToArray());}Assert.NotNull(actual);Assert.True(actual.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.Equal(unchanged,input);
    }
    [Theory][InlineData("digest")][InlineData("length")]
    public async Task OriginalJobCannotBeReplacedBeforeTheNativeCopyOrChild(string fault)
    {
        byte[] input=Riff(Chunk("VP8L",Lossless()));var original=Job(input);var job=fault=="digest"?original with{Sha256=new string('0',64)}:original with{ByteLength=input.Length+1};int copies=0;Assert.Equal(ImageFailure.InvalidImage,Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,job,default,_=>copies++)).Code);Assert.Equal(0,copies);var starter=new Starter();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),starter);await Assert.ThrowsAsync<InvalidDataException>(()=>client.PrepareAsync(input,job));Assert.Equal(0,starter.Starts);
    }
    [Theory][InlineData("prepared-source")][InlineData("extra-byte")][InlineData("exit7")]
    public async Task WebpCannotEncodeOrDeliverBeforeExactSourceFullEofAndActualExit(string fault)
    {
        byte[] input=Riff(Chunk("VP8L",Lossless()));byte[]? owned=null;int encodes=0;await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),new Starter(fault)){DecodeAllocationForTest=b=>owned=b,EncodePreparedForTest=(_,_,_)=>{encodes++;throw new InvalidOperationException("Must not encode rejected raster.");}};await Assert.ThrowsAnyAsync<Exception>(()=>client.PreparePngAsync(input,Job(input)));Assert.Equal(0,encodes);if(fault=="prepared-source")Assert.Null(owned);else{Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);}Assert.True(client.ObserveExit());
    }
    [Fact]public async Task WebpCancellationAfterCompleteRasterWaitsForTheActualExitAndClearsPixels()
    {
        byte[] input=Riff(Chunk("VP8L",Lossless()));byte[]? owned=null;using var cancel=new CancellationTokenSource();var received=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),new Starter("hold-exit")){DecodeAllocationForTest=b=>owned=b,DecodeReceivedForTest=()=>received.SetResult()};var task=client.PreparePngAsync(input,Job(input),cancellation:cancel.Token);await received.Task.WaitAsync(TimeSpan.FromSeconds(10));Assert.False(task.IsCompleted);cancel.Cancel();await Assert.ThrowsAnyAsync<Exception>(()=>task);Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.True(client.ObserveExit());
    }
    [Fact]public async Task LongLosslessCanvasCanActuallyProduceAllPixels()
    {
        // The fixed native encoder refuses width16384. This actual fixture proves16383,
        // not the policy's generic16384 boundary or decoder acceptance at that boundary.
        byte[] expected=new byte[16383*4];byte[]? input=null;
        // Bounded, deterministic synthetic corpus selection; the asserted pixels are the exact
        // selected source. Uniform/periodic canvases generated only unsupported odd payloads.
        for(int seed=0;seed<8;seed++){new Random(seed).NextBytes(expected);for(int i=3;i<expected.Length;i+=4)expected[i]=255;byte[] candidate=Encode(expected,16383,1);int length=ImagePayload(candidate,"VP8L").Length;if((length&1)==0){input=candidate;output.WriteLine($"Long synthetic seed={seed} imagePayload={length}");break;}}
        Assert.NotNull(input);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var raw=await client.DecodeAsync(input,Job(input));Assert.Equal(16383,raw.Metadata.Width);Assert.Equal(1,raw.Metadata.Height);Assert.Equal(expected,raw.Bytes.ToArray());Assert.True(client.ObserveExit());
    }
}
