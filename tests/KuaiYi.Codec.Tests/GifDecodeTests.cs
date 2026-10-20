using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using KuaiYi.Imaging;
using KuaiYi.ImageWorker;
using Xunit;
namespace KuaiYi.Codec.Tests;

public sealed class GifDecodeTests
{
    private static string Worker()
    {for(var p=new DirectoryInfo(AppContext.BaseDirectory);p!=null;p=p.Parent)if(File.Exists(Path.Combine(p.FullName,"SPEC.md")))return Path.Combine(p.FullName,"tools/KuaiYi.ImageDecodeFixture/bin/Release/net10.0/KuaiYi.ImageDecodeFixture.dll");throw new IOException("No synthetic worker.");}
    private static ImageDecodeJob Job(byte[] bytes)=>new(Guid.NewGuid(),1,1,bytes.Length,ImageDecodePolicy.Hash(bytes));
    // Four explicit RGB palette entries; fixture LZW emits Clear before each pixel (constant3-bit codes).
    private static readonly byte[] Palette=[255,0,0,0,255,0,0,0,255,255,255,255];
    private static byte[] U16(int n)=>[(byte)n,(byte)(n>>8)];
    internal static byte[] Image(int left,int top,int width,int height,byte[] indices,bool local=false,bool interlaced=false,int transparent=-1,int disposal=0)
    {
        var data=new List<byte>();int bits=0,buffer=0;
        void Code(int code){buffer|=code<<bits;bits+=3;while(bits>=8){data.Add((byte)buffer);buffer>>=8;bits-=8;}}
        foreach(byte i in indices){Code(4);Code(i);}Code(5);if(bits>0)data.Add((byte)buffer);
        var blocks=new List<byte>();for(int at=0;at<data.Count;){int n=Math.Min(255,data.Count-at);blocks.Add((byte)n);blocks.AddRange(data.GetRange(at,n));at+=n;}blocks.Add(0);
        return [..(transparent>=0||disposal!=0?new byte[]{0x21,0xf9,4,(byte)(disposal<<2|(transparent>=0?1:0)),0,0,(byte)Math.Max(0,transparent),0}:[]),0x2c,..U16(left),..U16(top),..U16(width),..U16(height),(byte)((local?0x81:0)|(interlaced?0x40:0)),..(local?Palette:[]),2,..blocks];
    }
    internal static byte[] Gif(int width,int height,byte[][] images,bool global=true,bool version87=false,byte background=0,byte[]? extra=null)
        =>[..System.Text.Encoding.ASCII.GetBytes(version87?"GIF87a":"GIF89a"),..U16(width),..U16(height),(byte)(global?0x81:0),background,0,..(global?Palette:[]),..(extra??[]),..images.SelectMany(b=>b),0x3b];
    private static byte[] Bgra(params byte[] indices)=>indices.SelectMany(i=>i==4?new byte[4]:new byte[]{Palette[i*3+2],Palette[i*3+1],Palette[i*3],255}).ToArray();
    [Fact]public async Task ActualGifFirstFrameUsesTheWholeLogicalCanvas()
    {
        byte[] input=Gif(3,2,[Image(0,0,3,2,[0,1,2,3,0,1]),Image(0,0,3,2,[2,2,2,2,2,2])]);
        await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var raster=await client.DecodeAsync(input,Job(input));
        Assert.Equal("Gif",raster.Metadata.Format.ToString());Assert.Equal(3,raster.Metadata.Width);Assert.Equal(2,raster.Metadata.Height);Assert.Equal(1,raster.Metadata.Frames);Assert.Equal(Bgra(0,1,2,3,0,1),raster.Bytes.ToArray());Assert.True(client.ObserveExit());
    }
    private sealed class Starter(string? fault=null):IImageProcessStarter
    {public int Starts;public Process Start(ProcessStartInfo info){Starts++;if(fault!=null)info.Environment["KUAIYI_CODEC_FIXTURE_FAULT"]=fault;return Process.Start(info)??throw new IOException("No child.");}}
    [Theory][InlineData(false,false)][InlineData(false,true)][InlineData(true,false)][InlineData(true,true)]
    public async Task GlobalLocalPalettesAnd87Or89AlwaysHaveAnExplicitFirstFrameNotice(bool local,bool old)
    {
        byte[] input=Gif(3,2,[Image(0,0,3,2,[0,1,2,3,0,1],local)],!local,old),original=input.ToArray();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var png=await client.PreparePngAsync(input,Job(input));
        Assert.Equal("使用首帧",png.Notice);Assert.Equal(new GifFirstFrameInfo(1,0,0,3,2),png.SourceMetadata.GifInfo);Assert.False(png.Transform.HasDesktopAnchor);Assert.Equal(original,input);
        using var compressed=new MemoryStream();int at=8;var names=new List<string>();while(at<png.Bytes.Length){int n=(int)BinaryPrimitives.ReadUInt32BigEndian(png.Bytes.Span[at..]);string name=System.Text.Encoding.ASCII.GetString(png.Bytes.Span.Slice(at+4,4));names.Add(name);if(name=="IDAT")compressed.Write(png.Bytes.Span.Slice(at+8,n));at+=n+12;}
        Assert.Equal(new[]{"IHDR","IDAT","IEND"},names);compressed.Position=0;using var zip=new ZLibStream(compressed,CompressionMode.Decompress);using var rows=new MemoryStream();zip.CopyTo(rows);Assert.Equal(new byte[]{0,255,0,0,0,255,0,0,0,255,0,255,255,255,255,0,0,0,255,0},rows.ToArray());Assert.True(client.ObserveExit());
    }
    [Theory][InlineData(0,-1)][InlineData(1,-1)][InlineData(2,-1)][InlineData(3,-1)][InlineData(0,0)][InlineData(1,0)][InlineData(2,0)][InlineData(3,0)]
    public async Task OffsetFirstFrameHasTransparentOutsideAndWhitePreparationRegardlessOfBackgroundOrDisposal(int disposal,int transparent)
    {
        byte[] input=Gif(4,3,[Image(1,1,2,1,[0,2],transparent:transparent,disposal:disposal),Image(0,0,4,3,Enumerable.Repeat((byte)1,12).ToArray())],background:3);byte[] expected=Bgra(4,4,4,4,4,(byte)(transparent==0?4:0),2,4,4,4,4,4);
        await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var raw=await client.DecodeAsync(input,Job(input));Assert.Equal(expected,raw.Bytes.ToArray());Assert.Equal("使用首帧",raw.Notice);Assert.Equal(new GifFirstFrameInfo(2,1,1,2,1),raw.Metadata.GifInfo);
        for(int i=0;i<expected.Length;i+=4)if(expected[i+3]==0)expected[i]=expected[i+1]=expected[i+2]=expected[i+3]=255;
        await using var second=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var prepared=await second.PrepareAsync(input,Job(input));Assert.Equal(expected,prepared.Bytes.ToArray());Assert.Equal(raw.Metadata.GifInfo,prepared.Metadata.GifInfo);
    }
    [Fact]public async Task InterlacedImageRowsBecomeLogicalTopDownAndAreaUsesTheWholeCanvas()
    {
        // GIF interlace storage row order for height5 is0,4,2,1,3.
        byte[] input=Gif(2,5,[Image(0,0,2,5,[0,0,0,0,2,2,1,1,3,3],interlaced:true)]);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var raw=await client.DecodeAsync(input,Job(input));Assert.Equal(Bgra(0,0,1,1,2,2,3,3,0,0),raw.Bytes.ToArray());
        byte[] checker=Gif(4,4,[Image(0,0,4,4,[0,3,0,3,3,0,3,0,0,3,0,3,3,0,3,0])]);await using var second=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var small=await second.PrepareAsync(checker,Job(checker),2);Assert.Equal(new ImageSize(2,2),small.Transform!.NormalizedSize);Assert.Equal(Enumerable.Repeat(new byte[]{128,128,255,255},4).SelectMany(b=>b).ToArray(),small.Bytes.ToArray());
    }
    [Fact]public async Task LaterUndecodableEntropyIsNotPassedToNativeButItsStructureIsChecked()
    {
        byte[] second=Image(0,0,1,1,[0]);second[^3]=255;byte[] input=Gif(2,1,[Image(0,0,2,1,[0,1]),second]),original=input.ToArray(),expectedNative=Gif(2,1,[Image(0,0,2,1,[0,1])]);byte[]? copy=null;
        using(var raw=ImageRasterDecoder.Decode(input,Job(input),default,b=>{copy=b;Assert.Equal(expectedNative,b);})){Assert.Equal(Bgra(0,1),raw.Bytes.ToArray());Assert.Equal(2,raw.Metadata.GifInfo!.SourceFrames);}Assert.NotNull(copy);Assert.True(copy.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.Equal(original,input);
        await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var prepared=await client.PrepareAsync(input,Job(input));Assert.Equal(Bgra(0,1),prepared.Bytes.ToArray());
        // The identical entropy in frame0 is rejected before native allocation.
        byte[] firstBad=Gif(1,1,[second]);int allocations=0;Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(firstBad,Job(firstBad),default,_=>allocations++));Assert.Equal(0,allocations);
    }
    private static byte[] Valid()=>Gif(2,1,[Image(0,0,2,1,[0,1])]);
    private static byte[] Broken(string kind)
    {
        byte[] b=Valid();int descriptor=25;switch(kind)
        {
            case "header-short":return b[..10];case "version":b[4]=(byte)'8';break;
            case "no-image":return Gif(2,1,[]);case "no-trailer":return b[..^1];case "tail":return [..b,0];case "duplicate-trailer":return [..b,0x3b];
            case "width-zero":b[6]=b[7]=0;break;case "side-limit":b[6]=1;b[7]=64;break;case "pixel-limit":b[6]=0x70;b[7]=0x17;b[8]=0xa1;b[9]=0x0f;break;
            case "aspect":b[12]=1;break;case "background":b[11]=4;break;case "missing-palette":return Gif(2,1,[Image(0,0,2,1,[0,1])],false);
            case "truncated-palette":return b[..20];case "local-truncated":return Gif(2,1,[Image(0,0,2,1,[0,1],true)[..16]],false);
            case "frame-zero":b[descriptor+5]=0;break;case "frame-outside":b[descriptor+1]=1;break;case "frame-overflow":b[descriptor+1]=255;b[descriptor+2]=255;break;
            case "descriptor-reserved":b[descriptor+9]|=0x10;break;case "min-one":b[descriptor+10]=1;break;case "min-nine":b[descriptor+10]=9;break;
            case "empty-data":return Gif(1,1,[[0x2c,0,0,0,0,1,0,1,0,0,2,0]]);
            case "subblock-length":b[descriptor+11]=255;break;case "no-subblock-end":return b[..^2];
            case "unknown-block":b[descriptor]=0;break;
            case "unknown-extension":return Gif(2,1,[Image(0,0,2,1,[0,1])],extra:[0x21,0xfd,0]);
            case "plain-text":return Gif(2,1,[Image(0,0,2,1,[0,1])],extra:[0x21,1,0]);
            case "unknown-application":return Gif(2,1,[Image(0,0,2,1,[0,1])],extra:[0x21,0xff,11,.."ICCRGBG1012"u8.ToArray(),3,1,0,0,0]);
            case "comment-truncated":return Gif(2,1,[Image(0,0,2,1,[0,1])],extra:[0x21,0xfe,255]);
            case "gce-size":case "gce-reserved":case "gce-user-input":case "gce-disposal":case "gce-index":case "gce-terminator":
                b=[0x21,0xf9,4,1,0,0,0,0];if(kind=="gce-size")b[2]=3;if(kind=="gce-reserved")b[3]|=0x80;if(kind=="gce-user-input")b[3]|=2;if(kind=="gce-disposal")b[3]|=16;if(kind=="gce-index")b[6]=4;if(kind=="gce-terminator")b[7]=1;return Gif(2,1,[Image(0,0,2,1,[0,1])],extra:b);
            case "gce-duplicate":return Gif(2,1,[Image(0,0,2,1,[0,1],transparent:0)],extra:[0x21,0xf9,4,0,0,0,0,0]);
            case "gce-dangling":return [..b[..^1],0x21,0xf9,4,0,0,0,0,0,0x3b];
            case "87-extension":return Gif(2,1,[Image(0,0,2,1,[0,1])],version87:true,extra:[0x21,0xfe,0]);
            case "loop-bad-shape":return Gif(2,1,[Image(0,0,2,1,[0,1])],extra:[0x21,0xff,11,.."NETSCAPE2.0"u8.ToArray(),3,2,0,0,0]);
            case "later-rect":return Gif(2,1,[Image(0,0,2,1,[0,1]),Image(2,0,1,1,[0])]);
            case "later-min":b=Image(0,0,1,1,[0]);b[10]=1;return Gif(2,1,[Image(0,0,2,1,[0,1]),b]);
            case "frames":return Gif(1,1,Enumerable.Repeat(Image(0,0,1,1,[0]),257).ToArray());
            case "blocks":return Gif(1,1,[Image(0,0,1,1,[0])],extra:Enumerable.Repeat(new byte[]{0x21,0xfe,0},4096).SelectMany(x=>x).ToArray());
            case "subblocks":return Gif(1,1,[Image(0,0,1,1,[0])],extra:[0x21,0xfe,..Enumerable.Repeat(new byte[]{1,0},131072).SelectMany(x=>x),0]);
            default:throw new ArgumentException(kind);
        }return b;
    }
    public static IEnumerable<object[]> Rejections()
    {
        foreach(string kind in new[]{"header-short","version","no-image","no-trailer","tail","duplicate-trailer","width-zero","side-limit","pixel-limit","aspect","background","missing-palette","truncated-palette","local-truncated","frame-zero","frame-outside","frame-overflow","descriptor-reserved","min-one","min-nine","empty-data","subblock-length","no-subblock-end","unknown-block","unknown-extension","plain-text","unknown-application","comment-truncated","gce-size","gce-reserved","gce-user-input","gce-disposal","gce-index","gce-terminator","gce-duplicate","gce-dangling","87-extension","loop-bad-shape","later-rect","later-min","frames","blocks","subblocks"})yield return [kind];
    }
    [Theory][MemberData(nameof(Rejections))]public async Task ContainerRejectsBeforeChildOrNativeCopy(string kind)
    {
        byte[] input=Broken(kind);var starter=new Starter();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),starter);await Assert.ThrowsAsync<ImageJobException>(()=>client.PrepareAsync(input,Job(input)));Assert.Equal(0,starter.Starts);int copies=0;Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,Job(input),default,_=>copies++));Assert.Equal(0,copies);
    }
    private static byte[] Entropy(byte[] payload,int width=1,int height=1,int minimum=2)
        =>Gif(width,height,[[0x2c,0,0,0,0,..U16(width),..U16(height),0,(byte)minimum,(byte)payload.Length,..payload,0]]);
    private static byte[] Codes(params (int Code,int Width)[] codes)
    {var output=new List<byte>();uint value=0;int bits=0;foreach(var (code,width) in codes){value|=(uint)code<<bits;bits+=width;while(bits>=8){output.Add((byte)value);value>>=8;bits-=8;}}if(bits>0)output.Add((byte)value);return output.ToArray();}
    [Theory][InlineData("initial-clear")][InlineData("eoi-missing")][InlineData("short")][InlineData("too-many")][InlineData("dictionary")][InlineData("palette")][InlineData("padding")][InlineData("extra-payload")]
    public async Task FirstFrameEntropyMustBeCompleteBeforeNative(string kind)
    {
        byte[] data=kind switch{"initial-clear"=>Codes((0,3),(5,3)),"eoi-missing"=>Codes((4,3),(0,3)),"short"=>Codes((4,3),(5,3)),"too-many"=>Codes((4,3),(0,3),(0,3),(5,3)),"dictionary"=>Codes((4,3),(7,3),(5,3)),"palette"=>Codes((8,4),(4,4),(9,4)),"padding"=>[0x44,0x81],"extra-payload"=>[0x44,1,0],_=>throw new ArgumentException(kind)};
        byte[] input=Entropy(data,minimum:kind=="palette"?3:2);int copies=0;Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,Job(input),default,_=>copies++));Assert.Equal(0,copies);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());var error=await Assert.ThrowsAsync<ImageJobException>(()=>client.DecodeAsync(input,Job(input)));Assert.Equal(ImageFailure.InvalidImage,error.Code);Assert.True(client.ObserveExit());
    }
    [Fact]public async Task KwKwKAndCodeWidthTransitionDecodeActualPixels()
    {byte[] input=Entropy(Codes((4,3),(0,3),(6,3),(7,3),(5,4)),6);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var raw=await client.DecodeAsync(input,Job(input));Assert.Equal(Bgra(0,0,0,0,0,0),raw.Bytes.ToArray());}
    [Theory][InlineData("success")][InlineData("cancel")][InlineData("observer")][InlineData("native-fail")]
    public void NativeCopyHasOnlyFirstFrameAndAlwaysClears(string fault)
    {
        byte[] input=Gif(2,1,[Image(0,0,2,1,[0,1]),Image(0,0,2,1,[2,3])]),unchanged=input.ToArray();byte[]? copy=null;using var cancel=new CancellationTokenSource();
        void Observe(byte[] bytes){copy=bytes;Assert.Equal(Valid(),bytes);if(fault=="cancel")cancel.Cancel();if(fault=="observer")throw new IOException("SYNTHETIC_GIF");if(fault=="native-fail")bytes[0]=0;}
        if(fault=="cancel")Assert.ThrowsAny<OperationCanceledException>(()=>ImageRasterDecoder.Decode(input,Job(input),cancel.Token,Observe));else if(fault=="observer")Assert.Throws<IOException>(()=>ImageRasterDecoder.Decode(input,Job(input),default,Observe));else if(fault=="native-fail")Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,Job(input),default,Observe));else{using var raster=ImageRasterDecoder.Decode(input,Job(input),default,Observe);Assert.Equal(Bgra(0,1),raster.Bytes.ToArray());}Assert.NotNull(copy);Assert.True(copy.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.Equal(unchanged,input);
    }
    [Theory][InlineData("gif-source")][InlineData("extra-byte")][InlineData("exit7")]
    public async Task MetadataSourceEofAndExitMustPassBeforeDeliveryOrEncoding(string fault)
    {
        byte[] input=Valid();byte[]? owned=null;int encodes=0;await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),new Starter(fault)){DecodeAllocationForTest=b=>owned=b,EncodePreparedForTest=(_,_,_)=>{encodes++;throw new InvalidOperationException("Must not encode.");}};
        await Assert.ThrowsAnyAsync<Exception>(()=>client.PreparePngAsync(input,Job(input)));Assert.Equal(0,encodes);if(fault=="gif-source")Assert.Null(owned);else{Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);}Assert.True(client.ObserveExit());
    }
    [Fact]public async Task CompleteRasterRemainsHeldUntilActualExitAndCancellationClearsIt()
    {
        byte[] input=Valid();byte[]? owned=null;using var cancel=new CancellationTokenSource();var received=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),new Starter("hold-exit")){DecodeAllocationForTest=b=>owned=b,DecodeReceivedForTest=()=>received.SetResult()};var task=client.PreparePngAsync(input,Job(input),cancellation:cancel.Token);await received.Task.WaitAsync(TimeSpan.FromSeconds(10));Assert.False(task.IsCompleted);cancel.Cancel();await Assert.ThrowsAnyAsync<Exception>(()=>task);Assert.NotNull(owned);Assert.True(owned.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.True(client.ObserveExit());
    }
    [Theory][InlineData("digest")][InlineData("length")]
    public async Task OriginalCompleteGifJobCannotBeReplaced(string kind)
    {byte[] input=Valid();var job=Job(input);job=kind=="digest"?job with{Sha256=new string('0',64)}:job with{ByteLength=input.Length+1};int copies=0;Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,job,default,_=>copies++));Assert.Equal(0,copies);var starter=new Starter();await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker(),starter);await Assert.ThrowsAsync<InvalidDataException>(()=>client.DecodeAsync(input,job));Assert.Equal(0,starter.Starts);}
    [Theory][InlineData("missing")][InlineData("nongif")][InlineData("failure")][InlineData("frames")][InlineData("rect")][InlineData("orientation")]
    public void InvalidSelectionMetadataCannotAllocate(string kind)
    {
        var job=Job(Valid()) with{PrepareLongEdge=2};var reply=new ImageDecodeReply(job,ImageFailure.None,EncodedRasterFormat.Gif,2,1,8,8,new string('0',64),"BGRA8888-opaque-white",1,1,new(2,1,ImageOrientation.ExifTopLeft),new(1,0,0,2,1));
        reply=kind switch{"missing"=>reply with{GifInfo=null},"nongif"=>reply with{Format=EncodedRasterFormat.Png},"failure"=>new(job,ImageFailure.InvalidImage,GifInfo:reply.GifInfo),"frames"=>reply with{GifInfo=reply.GifInfo! with{SourceFrames=257}},"rect"=>reply with{GifInfo=reply.GifInfo! with{Left=int.MaxValue}},_=>reply with{PreparedInfo=reply.PreparedInfo! with{SourceOrientation=ImageOrientation.ExifTopRight}}};int allocations=0;Assert.ThrowsAny<Exception>(()=>new ImageRasterReceiver(Guid.NewGuid(),job,reply,_=>allocations++));Assert.Equal(0,allocations);
    }

    private static string Fixture(string name)
    {for(var p=new DirectoryInfo(AppContext.BaseDirectory);p!=null;p=p.Parent)if(File.Exists(Path.Combine(p.FullName,"SPEC.md")))return Path.Combine(p.FullName,"tests/fixtures/images",name);throw new IOException("No repository.");}
    [Fact]public async Task OrdinaryDictionaryGrowthThroughTwelveBitsUsesIndependentOriginalIndexOracle()
    {
        byte[] input=File.ReadAllBytes(Fixture("gif-codewidth.gif")),indices=File.ReadAllBytes(Fixture("gif-codewidth.indices"));Assert.Equal(32768,indices.Length);
        await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var raw=await client.DecodeAsync(input,Job(input));Assert.Equal(256,raw.Metadata.Width);Assert.Equal(128,raw.Metadata.Height);byte[] expected=indices.SelectMany(n=>new byte[]{(byte)(n*37%256),(byte)(255-n),n,255}).ToArray();Assert.Equal(expected,raw.Bytes.ToArray());Assert.True(client.ObserveExit());
    }
    [Theory][InlineData("NETSCAPE2.0")][InlineData("ANIMEXTS1.0")]
    public async Task RecognizedLoopAndCommentsDoNotChangeTheFrameOrLeakMetadata(string name)
    {
        byte[] comment="SYNTHETIC-PATH-GPS"u8.ToArray(),input=Gif(2,1,[Image(0,0,2,1,[0,1],transparent:2)],extra:[0x21,0xff,11,..System.Text.Encoding.ASCII.GetBytes(name),3,1,0,0,0,0x21,0xfe,(byte)comment.Length,..comment,0]);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var png=await client.PreparePngAsync(input,Job(input));Assert.Equal("使用首帧",png.Notice);Assert.DoesNotContain("SYNTHETIC",System.Text.Encoding.UTF8.GetString(ImageJson.Serialize(png.SourceMetadata)));Assert.DoesNotContain("SYNTHETIC",System.Text.Encoding.Latin1.GetString(png.Bytes.Span));
    }
    [Fact]public async Task MaximumFrameCountStillDecodesOnlyTheFirstFrame()
    {byte[] input=Gif(1,1,Enumerable.Range(0,256).Select(n=>Image(0,0,1,1,[(byte)(n%4)])).ToArray());int copies=0;using(var raster=ImageRasterDecoder.Decode(input,Job(input),default,b=>{copies++;Assert.Equal(Gif(1,1,[Image(0,0,1,1,[0])]),b);})){Assert.Equal(256,raster.Metadata.GifInfo!.SourceFrames);Assert.Equal(Bgra(0),raster.Bytes.ToArray());}Assert.Equal(1,copies);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var raw=await client.DecodeAsync(input,Job(input));Assert.Equal(256,raw.Metadata.GifInfo!.SourceFrames);}
    [Fact]public async Task Actual16384SideAndSplitSubblocksPreserveAllPixels()
    {byte[] indices=Enumerable.Range(0,16384).Select(n=>(byte)(n%4)).ToArray(),input=Gif(16384,1,[Image(0,0,16384,1,indices)]);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var raw=await client.DecodeAsync(input,Job(input));Assert.Equal(Bgra(indices),raw.Bytes.ToArray());Assert.Equal(16384,raw.Metadata.Width);Assert.True(client.ObserveExit());}
    [Fact]public void ExactContainerResourceBoundsAreAcceptedAndCancellationIsObserved()
    {
        byte[] comments=Enumerable.Repeat(new byte[]{0x21,0xfe,0},4094).SelectMany(b=>b).ToArray(),input=Gif(1,1,[Image(0,0,1,1,[0])],extra:comments);Assert.Equal(1,GifDecodePolicy.Inspect(input).FirstFrame.SourceFrames);
        byte[] giant=Gif(6000,4000,[Image(0,0,1,1,[0])]);Assert.Equal(new ImageSize(6000,4000),GifDecodePolicy.Inspect(giant).Size);
        using var cancel=new CancellationTokenSource();cancel.Cancel();Assert.ThrowsAny<OperationCanceledException>(()=>GifDecodePolicy.Inspect(input,cancel.Token));int copies=0;Assert.ThrowsAny<OperationCanceledException>(()=>ImageRasterDecoder.Decode(input,Job(input),cancel.Token,_=>copies++));Assert.Equal(0,copies);
    }

    [Fact]public async Task EveryCodeWidthDictionarySaturationAndClearResetHaveExactPixels()
    {
        var codes=new List<(int Code,int Width)>{(4,3),(0,3)};
        // For literal number i>=1 the prior dictionary size is5+i; width grows at powers of2.
        for(int i=1;i<5000;i++){int width=Math.Min(12,32-System.Numerics.BitOperations.LeadingZeroCount((uint)(5+i)));codes.Add((0,width));}
        codes.Add((4095,12)); // Last dictionary entry is two red literals; dictionary remains saturated.
        codes.Add((4,12));codes.Add((0,3));codes.Add((5,3));
        byte[] data=Codes(codes.ToArray());var blocks=new List<byte>();for(int at=0;at<data.Length;){int n=Math.Min(255,data.Length-at);blocks.Add((byte)n);blocks.AddRange(data.AsSpan(at,n).ToArray());at+=n;}blocks.Add(0);
        byte[] input=Gif(5003,1,[[0x2c,0,0,0,0,..U16(5003),1,0,0,2,..blocks]]);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var raw=await client.DecodeAsync(input,Job(input));Assert.Equal(Bgra(new byte[5003]),raw.Bytes.ToArray());Assert.True(client.ObserveExit());
    }
    [Fact]public async Task EoiCanCrossSubblockBoundaryAndClearForbidsStaleDictionaryCodes()
    {
        byte[] valid=Gif(1,1,[[0x2c,0,0,0,0,1,0,1,0,0,2,1,0x44,1,1,0]]);await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var raw=await client.DecodeAsync(valid,Job(valid));Assert.Equal(Bgra(0),raw.Bytes.ToArray());
        byte[] invalid=Entropy(Codes((4,3),(0,3),(4,3),(6,3),(5,3)));int copies=0;Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(invalid,Job(invalid),default,_=>copies++));Assert.Equal(0,copies);
    }

}