using System.Buffers.Binary;
using KuaiYi.Imaging;
using KuaiYi.ImageWorker;
using System.Security.Cryptography;
using SkiaSharp;
using Xunit;
namespace KuaiYi.Codec.Tests;

public sealed partial class RasterDecodeTests
{
    private static byte[] ProgressiveFixture(string name)=>File.ReadAllBytes(Path.Combine(Repository(),"tests/fixtures/images/progressive",name+".jpg"));
    private sealed record JpegScanPart(int Header,int Entropy,int End,int Components,int Ss,int Se,int Ah,int Al);
    // Test fixture surgery only: it neither decodes entropy nor participates in production acceptance.
    private static List<JpegScanPart> ScanParts(byte[] bytes)
    {
        List<JpegScanPart> found=[];int at=2;
        while(at<bytes.Length)
        {
            Assert.Equal(255,bytes[at++]);while(bytes[at]==255)at++;int marker=bytes[at++];if(marker==217)break;
            int length=BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(at));int end=at+length;
            if(marker!=218){at=end;continue;}
            int header=at+2,components=bytes[header],ss=bytes[end-3],se=bytes[end-2],ah=bytes[end-1]>>4,al=bytes[end-1]&15;at=end;
            while(at<bytes.Length-1)
            {if(bytes[at]!=255){at++;continue;}if(bytes[at+1]==0||bytes[at+1] is >=208 and <=215){at+=2;continue;}break;}
            found.Add(new(header,end,at,components,ss,se,ah,al));
        }
        return found;
    }
    [Theory][InlineData("gray",17,9)][InlineData("444",17,9)][InlineData("420",33,17)][InlineData("420-restart",33,17)]
    public async Task ActualNonzeroProgressiveFixtureDecodesEveryScanInOriginalWorker(string name,int width,int height)
    {
        byte[] encoded=ProgressiveFixture(name),original=encoded.ToArray();var scans=ScanParts(encoded);Assert.True(scans.Count>=6);Assert.Contains(scans,s=>s.Ah>0);Assert.Contains(scans,s=>s.Ss>0&&s.Al==0);
        using var input=new MemoryStream(encoded,writable:false);using var reference=SKCodec.Create(input,out var created);Assert.Equal(SKCodecResult.Success,created);Assert.NotNull(reference);
        byte[] expected=new byte[width*height*4];Assert.Equal(SKCodecResult.Success,reference.GetPixels(new SKImageInfo(width,height,SKColorType.Bgra8888,SKAlphaType.Unpremul),expected));Assert.True(expected.Where((_,i)=>i%4!=3).Distinct().Count()>16);
        await using var client=Client();using var raster=await client.DecodeAsync(encoded,Job(encoded));Assert.Equal(width,raster.Metadata.Width);Assert.Equal(height,raster.Metadata.Height);Assert.Equal(expected,raster.Bytes.ToArray());Assert.Equal(original,encoded);Assert.Equal(EncodedRasterFormat.Jpeg,raster.Metadata.Format);Assert.True(client.ObserveExit());
    }

    private sealed class ProgressiveBuilder
    {
        internal readonly List<byte> Bytes=[255,216];
        internal ProgressiveBuilder(int width=8,int height=8,int dcCategory=0)
        {
            Segment(219,[0,..Enumerable.Repeat((byte)1,64)]);Segment(194,[8,(byte)(height>>8),(byte)height,(byte)(width>>8),(byte)width,1,1,17,0]);
            Table(0,[1,..new byte[15]],[(byte)dcCategory]);Table(16,[1,1,1,..new byte[13]],[0,4,240]);
        }
        internal void Segment(byte marker,byte[] bytes){Bytes.AddRange([255,marker,(byte)((bytes.Length+2)>>8),(byte)(bytes.Length+2)]);Bytes.AddRange(bytes);}
        internal void Table(byte id,byte[] counts,byte[] values)=>Segment(196,[id,..counts,..values]);
        internal void Scan(int ss,int se,int ah,int al,string entropy){Segment(218,[1,1,0,(byte)ss,(byte)se,(byte)((ah<<4)|al)]);Entropy(entropy);}
        internal void Entropy(string value)
        {
            while(value.Length%8!=0)value+="1";
            for(int i=0;i<value.Length;i+=8){byte b=Convert.ToByte(value.Substring(i,8),2);Bytes.Add(b);if(b==255)Bytes.Add(0);}
        }
        internal byte[] Finish()=>[..Bytes,255,217];
    }
    // Independent, explicit Huffman streams: DC=0; AC initial k1=9<<1;
    // refinement either creates k2=+/-1 or uses ZRL/EOB while correcting k1 to19.
    private static byte[] SparseProgressive(string mode="new-positive",int dcCategory=0,string dcBits="0",int dcAl=0,int initialEnd=63,int finalEnd=63)
    {
        var b=new ProgressiveBuilder(dcCategory:dcCategory);b.Scan(0,0,0,dcAl,dcBits);
        b.Scan(1,initialEnd,0,1,(mode=="initial-zrl"?"110":"")+"10"+"1001"+"0");
        b.Table(16,[1,1,1,..new byte[13]],[0,1,240]);
        b.Scan(1,finalEnd,1,0,mode switch{"new-positive"=>"10110","new-negative"=>"10010","refine-zrl"=>"11010",_=>"01"});return b.Finish();
    }
    [Theory][InlineData("new-positive",1)][InlineData("new-negative",-1)][InlineData("refine-zrl",0)][InlineData("refine-eob",0)][InlineData("initial-zrl",0)]
    public async Task SparseProgressiveEntropyMatchesIndependentDctCoefficients(string mode,int second)
    {
        byte[] input=SparseProgressive(mode);await using var client=Client();using var raster=await client.DecodeAsync(input,Job(input));var pixel=raster.Bytes.ToArray();
        int firstIndex=mode=="initial-zrl"?17:1;var coefficients=new Dictionary<int,int>{{firstIndex,19}};if(second!=0)coefficients[2]=second;
        static (int U,int V) Zigzag(int index)
        {int at=0;for(int sum=0;sum<=14;sum++){int low=Math.Max(0,sum-7),high=Math.Min(7,sum);for(int n=low;n<=high;n++){int u=(sum&1)==0?n:high-(n-low);if(at++==index)return(u,sum-u);}}throw new InvalidOperationException();}
        for(int y=0;y<8;y++)for(int x=0;x<8;x++)
        {
            double value=128;foreach(var pair in coefficients){var(u,v)=Zigzag(pair.Key);value+=.25*(u==0?1/Math.Sqrt(2):1)*(v==0?1/Math.Sqrt(2):1)*pair.Value*Math.Cos((2*x+1)*u*Math.PI/16)*Math.Cos((2*y+1)*v*Math.PI/16);}
            int expected=(int)Math.Round(value);for(int channel=0;channel<3;channel++)Assert.InRange(pixel[(y*8+x)*4+channel],expected-1,expected+1);Assert.Equal(255,pixel[(y*8+x)*4+3]);
        }
        Assert.True(client.ObserveExit());
    }
    private static byte[] ZeroRuns(bool overrun=false,int restart=0)
    {
        int run=(restart==0?6:2)+(overrun?1:0);
        var b=new ProgressiveBuilder(17,9);if(restart>0)b.Segment(221,[0,(byte)restart]);
        void Scan(int ss,int se,int ah,int al,bool dc)
        {
            b.Segment(218,[1,1,0,(byte)ss,(byte)se,(byte)((ah<<4)|al)]);
            if(restart==0)b.Entropy(dc?"000000":"10"+Convert.ToString(run-4,2).PadLeft(2,'0'));
            else for(int group=0;group<3;group++){b.Entropy(dc?"00":"10"+(run==3?"1":"0"));if(group<2)b.Bytes.AddRange([255,(byte)(208+group)]);}
        }
        Scan(0,0,0,0,true);b.Table(16,[1,1,..new byte[14]],[0,(byte)(restart==0?32:16)]);Scan(1,63,0,1,false);Scan(1,63,1,0,false);return b.Finish();
    }
    [Theory][InlineData(0)][InlineData(2)]
    public async Task ProgressiveEobRunsEndExactlyAtScanOrRestartBoundary(int restart)
    {
        byte[] input=ZeroRuns(restart:restart);await using var client=Client();using var raster=await client.DecodeAsync(input,Job(input));var pixels=raster.Bytes.ToArray();for(int i=0;i<pixels.Length;i+=4){Assert.Equal(128,pixels[i]);Assert.Equal(128,pixels[i+1]);Assert.Equal(128,pixels[i+2]);Assert.Equal(255,pixels[i+3]);}Assert.True(client.ObserveExit());
    }
    [Fact]public async Task ProgressiveTablesBeforeFrameCannotBeMisreadAsBaselineTables()
    {
        byte[] input=ZeroRuns();int frame=input.AsSpan().IndexOf(new byte[]{255,194});var tables=new Dictionary<int,int>();
        for(int at=0;at<input.Length-1;at++)if(input[at]==255&&input[at+1]==196)tables[at]=2+BinaryPrimitives.ReadUInt16BigEndian(input.AsSpan(at+2));
        int eobTable=tables.Keys.Last();Assert.Equal(16,input[eobTable+4]);Assert.Contains((byte)32,input.AsSpan(eobTable+21,tables[eobTable]-21).ToArray());
        // Move every DHT in original order; the unused initial AC table must not overwrite the EOBRUN table.
        List<byte> bytes=[..input[..frame]];foreach(var pair in tables)bytes.AddRange(input.AsSpan(pair.Key,pair.Value).ToArray());
        for(int at=frame;at<input.Length;)if(tables.TryGetValue(at,out int length))at+=length;else bytes.Add(input[at++]);byte[] reordered=bytes.ToArray();
        var old=Assert.Throws<ImageJobException>(()=>BaselineJpegScan.Validate(reordered,new(17,9),default));Assert.Equal(ImageFailure.InvalidImage,old.Code);
        await using var client=Client();using var raster=await client.DecodeAsync(reordered,Job(reordered));Assert.Equal(17*9*4,raster.Bytes.Length);Assert.True(client.ObserveExit());
    }
    [Fact]public void CompletedNativePartialProgressiveStillCannotBypassMissingFinalRefinement()
    {
        byte[] original=ProgressiveFixture("420");var last=ScanParts(original)[^1];byte[] partial=[..original[..(last.Header-4)],255,217];
        using var codec=SKCodec.Create(new MemoryStream(partial,writable:false),out var created);Assert.Equal(SKCodecResult.Success,created);Assert.NotNull(codec);byte[] pixels=new byte[33*17*4];Assert.Equal(SKCodecResult.Success,codec.GetPixels(new SKImageInfo(33,17,SKColorType.Bgra8888,SKAlphaType.Unpremul),pixels));
        int allocations=0;var error=Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(partial,Job(partial),default,_=>allocations++));Assert.Equal(ImageFailure.InvalidImage,error.Code);Assert.Equal(0,allocations);CryptographicOperations.ZeroMemory(pixels);
    }
    private static byte[] BrokenProgressive(string fault)
    {
        if(fault=="eob-end")return ZeroRuns(overrun:true);
        if(fault=="eob-restart")return ZeroRuns(true,2);
        if(fault=="dc-positive")return SparseProgressive(dcCategory:11,dcBits:"0"+new string('1',11));
        if(fault=="dc-negative")return SparseProgressive(dcCategory:11,dcBits:new string('0',12));
        if(fault=="dc-shift")return SparseProgressive(dcCategory:1,dcBits:"01",dcAl:10);
        if(fault=="initial-zrl")return SparseProgressive("initial-zrl",initialEnd:5);
        if(fault=="refine-zrl")return SparseProgressive("refine-zrl",finalEnd:5);
        byte[] input=SparseProgressive();var scans=ScanParts(input);var last=scans[^1];int frame=input.AsSpan().IndexOf(new byte[]{255,194});
        switch(fault)
        {
            case "precision":input[last.Entropy-1]=32;break;
            case "duplicate":input[last.Entropy-1]=0;break;
            case "refine-size":
                int table=last.Header-4;while(!(input[table]==255&&input[table+1]==196))table--;// Locate the explicit replacement DHT.
                input[table+22]=2;break;
            case "ac-magnitude":
                int firstTable=input.AsSpan().IndexOf(new byte[]{255,196});int secondTable=input.AsSpan(firstTable+2).IndexOf(new byte[]{255,196})+firstTable+2;int values=secondTable+5+16;input[values+1]=10;break;
            case "padding":input[^3]&=254;break;
            case "extra":input=[..input[..^2],0,255,217];break;
            case "tail":input=[..input,0];break;
            case "missing-scan":input=[..input[..(last.Header-4)],255,217];break;
            case "entropy-eoi":input=[..input[..last.Entropy],255,217];break;
            case "sof-precision":input[frame+4]=12;break;
            case "arithmetic":input[frame+1]=202;break;
            case "stuffing":input=ProgressiveFixture("420");int stuffed=input.AsSpan().IndexOf(new byte[]{255,0});Assert.True(stuffed>0);input[stuffed+1]=1;break;
            case "restart-order":input=ProgressiveFixture("420-restart");int rst=input.AsSpan().IndexOf(new byte[]{255,208});Assert.True(rst>0);input[rst+1]=209;break;
            case "restart-missing":input=ProgressiveFixture("420-restart");int marker=input.AsSpan().IndexOf(new byte[]{255,208});Assert.True(marker>0);input=[..input[..marker],..input[(marker+2)..]];break;
            case "qt-change":byte[] qt=[255,219,0,67,0,..Enumerable.Repeat((byte)1,64)];input=[..input[..(last.Header-4)],..qt,..input[(last.Header-4)..]];break;
            default:throw new InvalidOperationException(fault);
        }
        return input;
    }
    [Theory][InlineData("eob-end")][InlineData("eob-restart")][InlineData("dc-positive")][InlineData("dc-negative")][InlineData("dc-shift")][InlineData("initial-zrl")][InlineData("refine-zrl")][InlineData("precision")][InlineData("duplicate")][InlineData("refine-size")][InlineData("ac-magnitude")][InlineData("padding")][InlineData("extra")][InlineData("tail")][InlineData("missing-scan")][InlineData("entropy-eoi")][InlineData("restart-order")][InlineData("restart-missing")][InlineData("stuffing")]
    public async Task ProgressiveCorruptionCannotReachNativeAllocationOrReturnPartialRaster(string fault)
    {
        byte[] input=BrokenProgressive(fault),original=input.ToArray();int native=0;var error=Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,Job(input),default,_=>native++));Assert.Equal(ImageFailure.InvalidImage,error.Code);Assert.Equal(0,native);Assert.Equal(original,input);
        await using var client=Client();var remote=await Assert.ThrowsAsync<ImageJobException>(()=>client.DecodeAsync(input,Job(input)));Assert.Equal(error.Code,remote.Code);Assert.True(client.ObserveExit());
    }
    [Theory][InlineData("sof-precision")][InlineData("arithmetic")][InlineData("qt-change")]
    public void OtherCodingAndLateQuantizationRemainExplicitlyUnsupportedBeforeNative(string fault)
    {byte[] input=BrokenProgressive(fault);int native=0;var error=Assert.Throws<ImageJobException>(()=>ImageRasterDecoder.Decode(input,Job(input),default,_=>native++));Assert.Equal(ImageFailure.UnsupportedImage,error.Code);Assert.Equal(0,native);}
    [Theory][InlineData(false)][InlineData(true)]
    public void OriginalNonzeroProgressiveMasksAreClearedOnCancellationOrObserverFailure(bool fail)
    {
        byte[] input=ProgressiveFixture("420"),original=input.ToArray();using var cancel=new CancellationTokenSource();List<ulong[]> owned=[];bool populated=false;var injected=new IOException("PUBLIC scan callback fault");
        var error=Record.Exception(()=>ProgressiveJpegScan.Validate(input,new(33,17),cancel.Token,owned.Add,()=>{if(!owned.Any(a=>a.Any(v=>v!=0)))return;populated=true;if(fail)throw injected;cancel.Cancel();}));
        Assert.True(populated);Assert.NotEmpty(owned);Assert.All(owned,a=>Assert.All(a,v=>Assert.Equal(0UL,v)));Assert.Equal(original,input);if(fail)Assert.Same(injected,error);else Assert.IsAssignableFrom<OperationCanceledException>(error);
    }
}
