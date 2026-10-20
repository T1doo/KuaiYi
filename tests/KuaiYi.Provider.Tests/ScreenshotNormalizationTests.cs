using System.Buffers.Binary;
using System.IO.Compression;
using KuaiYi.Core;
using KuaiYi.Imaging;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class ScreenshotNormalizationTests
{
    internal static byte[] DecodeRgb(ReadOnlyMemory<byte> png)
    {
        var bytes=png.Span;var size=ImagePolicy.InspectPng(bytes);using var idat=new MemoryStream();var names=new List<string>();int offset=8;
        while(offset<bytes.Length)
        {
            int count=checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes[offset..]));string name=System.Text.Encoding.ASCII.GetString(bytes.Slice(offset+4,4));names.Add(name);
            uint crc=uint.MaxValue;foreach(byte b in bytes.Slice(offset+4,count+4)){crc^=b;for(int bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0u);}Assert.Equal(~crc,BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset+8+count,4)));
            if(name=="IDAT")idat.Write(bytes.Slice(offset+8,count));offset+=count+12;
        }
        Assert.Equal(bytes.Length,offset);Assert.Equal(new[]{"IHDR","IDAT","IEND"},names);idat.Position=0;using var zip=new ZLibStream(idat,CompressionMode.Decompress);byte[] raster=new byte[checked((int)((size.Width*3+1)*size.Height))];zip.ReadExactly(raster);Assert.Equal(-1,zip.ReadByte());return raster;
    }
    internal static byte[] Solid(int width,int height,byte b,byte g,byte r,byte alpha)
    {var bytes=new byte[checked(width*height*4)];for(int i=0;i<bytes.Length;i+=4){bytes[i]=b;bytes[i+1]=g;bytes[i+2]=r;bytes[i+3]=alpha;}return bytes;}
    [Theory][InlineData(0,20,40,0,255,255,255)][InlineData(10,20,30,255,30,20,10)][InlineData(0,0,0,128,127,127,127)][InlineData(255,0,0,64,191,191,255)][InlineData(0,0,0,254,1,1,1)][InlineData(0,0,0,1,254,254,254)]
    public void RequestPngCompositesStraightBgraOntoWhiteInsteadOfDiscardingAlpha(byte b,byte g,byte red,byte alpha,byte expectedR,byte expectedG,byte expectedB)
    {
        byte[] raw=Solid(8,8,b,g,red,alpha),original=raw.ToArray();using var image=new OwnedSourceImage(new(new(-17,-9,-9,-1),raw));using var png=image.CreateRequestPng(4194304);var rgb=DecodeRgb(png.Bytes);
        for(int y=0;y<8;y++){Assert.Equal(0,rgb[y*25]);for(int x=0;x<8;x++)Assert.Equal(new[]{expectedR,expectedG,expectedB},rgb.AsSpan(y*25+1+x*3,3).ToArray());}Assert.Equal(original,raw);
    }
    [Theory][InlineData(8,8,8,8)][InlineData(1300,8,1300,8)][InlineData(2560,1024,2560,1024)][InlineData(4096,2048,2560,1280)][InlineData(2048,4096,1280,2560)][InlineData(3001,1001,2560,854)][InlineData(16384,1,2560,1)][InlineData(6000,4000,2560,1707)]
    public void PlansPreserveEntireCropAndExposeRoundedEdgeMapping(int width,int height,int expectedW,int expectedH)
    {
        var bounds=new PixelRect(-320,57,width-320,height+57);var map=ImageTransform.Plan(bounds);Assert.Equal(bounds,map.OriginalCrop);Assert.Equal(new ImageSize((uint)width,(uint)height),map.OriginalSize);Assert.Equal(new ImageSize((uint)expectedW,(uint)expectedH),map.NormalizedSize);Assert.Equal(ImageOrientation.ScreenshotIdentity,map.Orientation);
        Assert.Equal(new NormalizedImagePoint(expectedW,expectedH),map.OriginalToNormalized(new OriginalImagePoint(width,height)));Assert.Equal(new OriginalImagePoint(width,height),map.NormalizedToOriginal(new NormalizedImagePoint(expectedW,expectedH)));Assert.Equal(new DesktopImagePoint(bounds.Right,bounds.Bottom),map.NormalizedToDesktop(new NormalizedImagePoint(expectedW,expectedH)));Assert.Equal(new NormalizedImagePoint(0,0),map.DesktopToNormalized(new DesktopImagePoint(bounds.Left,bounds.Top)));
        Assert.Equal(new NormalizedImageRectangle(0,0,expectedW,expectedH),map.OriginalToNormalized(new OriginalImageRectangle(0,0,width,height)));Assert.Equal(new DesktopImageRectangle(bounds.Left,bounds.Top,width,height),map.NormalizedToDesktop(new NormalizedImageRectangle(0,0,expectedW,expectedH)));
    }
    [Fact]public void FixedSeedContinuousRectanglesRoundTripAcrossAllThreeCoordinateSpaces()
    {
        var random=new Random(703463);for(int i=0;i<1000;i++)
        {
            int width=random.Next(8,6001),height=random.Next(8,Math.Min(4001,24000000/width+1));var map=ImageTransform.Plan(new(-715,-923,width-715,height-923));double x=random.NextDouble()*width,y=random.NextDouble()*height;
            var original=new OriginalImageRectangle(x,y,random.NextDouble()*(width-x),random.NextDouble()*(height-y));var normalized=map.OriginalToNormalized(original);var back=map.NormalizedToOriginal(normalized);var desktop=map.NormalizedToDesktop(normalized);var again=map.DesktopToNormalized(desktop);
            foreach(var pair in new[]{(original.X,back.X),(original.Y,back.Y),(original.Width,back.Width),(original.Height,back.Height),(normalized.X,again.X),(normalized.Y,again.Y),(normalized.Width,again.Width),(normalized.Height,again.Height)})Assert.InRange(Math.Abs(pair.Item1-pair.Item2),0,1e-8);
        }
    }
    [Theory][InlineData(double.NaN,0)][InlineData(0,double.PositiveInfinity)][InlineData(-0.1,0)][InlineData(0,-1)][InlineData(101,1)][InlineData(1,51)]
    public void MappingRejectsInvalidPointsInsteadOfClampingOrChangingTheCrop(double x,double y)
    {var map=ImageTransform.Plan(new(-10,-20,90,30));Assert.Throws<ArgumentOutOfRangeException>(()=>map.OriginalToNormalized(new OriginalImagePoint(x,y)));Assert.Throws<ArgumentOutOfRangeException>(()=>map.NormalizedToOriginal(new NormalizedImagePoint(x,y)));}
    [Theory][InlineData(0,0,-1,1)][InlineData(0,0,1,-1)][InlineData(99,0,2,1)][InlineData(0,49,1,2)][InlineData(0,0,double.NaN,1)][InlineData(0,0,1,double.PositiveInfinity)]
    public void MappingRejectsInvalidOcrRectanglesWithoutInventingARegion(double x,double y,double width,double height)
    {var map=ImageTransform.Plan(new(-10,-20,90,30));Assert.Throws<ArgumentOutOfRangeException>(()=>map.NormalizedToOriginal(new NormalizedImageRectangle(x,y,width,height)));}
    [Theory][InlineData(4096,16)][InlineData(16,4096)][InlineData(3001,17)]
    public void ExactVisibleConstantImagesKeepTheirFastPathAndOwnedPngCarriesSourceAndTransform(int width,int height)
    {
        byte[] raw=Solid(width,height,0,0,0,128);var bounds=new PixelRect(-701,-31,width-701,height-31);var source=new OwnedSourceImage(new(bounds,raw));var prepared=source.PrepareRequest(4194304);var held=prepared.Bytes;var map=prepared.Transform;
        Assert.Equal(source.SourceId,prepared.SourceId);Assert.True(map.Downscaled);Assert.Equal(bounds,map.OriginalCrop);Assert.Equal(map.NormalizedSize,ImagePolicy.InspectPng(held.Span));Assert.Equal(ScreenshotNormalizationProof.ConstantCompositeDownscale,prepared.Proof);Assert.InRange(held.Length,1,4194304);var rgb=DecodeRgb(held);int stride=(int)map.NormalizedSize.Width*3+1;
        for(int y=0;y<map.NormalizedSize.Height;y++){Assert.Equal(0,rgb[y*stride]);Assert.All(rgb.AsSpan(y*stride+1,stride-1).ToArray(),b=>Assert.Equal(127,b));}source.Dispose();Assert.All(raw,b=>Assert.Equal(0,b));Assert.Contains(held.ToArray(),b=>b!=0);prepared.Dispose();prepared.Dispose();Assert.All(held.ToArray(),b=>Assert.Equal(0,b));Assert.Throws<ObjectDisposedException>(()=>prepared.Bytes);
    }
    [Fact]public void InvisibleRgbNoiseDoesNotBecomeVisibleAndConstancyIsProvedAfterWhiteComposite()
    {
        byte[] raw=new byte[3001*8*4];new Random(7631).NextBytes(raw);for(int i=3;i<raw.Length;i+=4)raw[i]=0;using var source=new OwnedSourceImage(new(new(0,0,3001,8),raw));using var prepared=source.PrepareRequest(4194304);var rgb=DecodeRgb(prepared.Bytes);int stride=(int)prepared.Transform.NormalizedSize.Width*3+1;
        for(int y=0;y<prepared.Transform.NormalizedSize.Height;y++)Assert.All(rgb.AsSpan(y*stride+1,stride-1).ToArray(),b=>Assert.Equal(255,b));Assert.Equal(ScreenshotNormalizationProof.ConstantCompositeDownscale,prepared.Proof);
    }
    [Theory][InlineData(0,0)][InlineData(11999,0)][InlineData(23999,0)][InlineData(12000,1)][InlineData(12000,2)][InlineData(12000,3)]
    public void OneUnitDifferencesUseIndependentAreaQuantizationInsteadOfBeingMislabelledConstant(int pixel,int channel)
    {
        byte[] raw=Solid(3000,8,0,0,0,255);raw[pixel*4+channel]=channel==3?(byte)254:(byte)1;var original=raw.ToArray();using var source=new OwnedSourceImage(new(new(0,0,3000,8),raw));using var prepared=source.PrepareRequest(4194304);Assert.Equal(ScreenshotNormalizationProof.AreaWhiteCompositeDownscale,prepared.Proof);var rows=DecodeRgb(prepared.Bytes);int sx=pixel%3000,sy=pixel/3000;
        // Independent decimal source-coordinate footprint of one changed source pixel, not production integer weights.
        decimal Overlap(decimal a,decimal b,int at)=>Math.Max(0,Math.Min(b,at+1)-Math.Max(a,at));
        for(int y=0;y<7;y++)for(int x=0;x<2560;x++)
        {
            decimal weight=Overlap((decimal)x*3000/2560,(decimal)(x+1)*3000/2560,sx)*Overlap((decimal)y*8/7,(decimal)(y+1)*8/7,sy)/((decimal)3000/2560*8/7);
            byte expected=(byte)decimal.Round(weight,0,MidpointRounding.AwayFromZero);
            for(int rgb=0;rgb<3;rgb++)Assert.Equal(channel==3||rgb==2-channel?expected:(byte)0,rows[y*(2560*3+1)+1+x*3+rgb]);
        }
        Assert.Equal(original,raw);Assert.False(source.IsDisposed);
    }
    [Fact]public void ActualNormalizedRasterIsOpaqueAndZeroedIndependentlyFromTheInput()
    {
        byte[] raw=Solid(8,8,0,0,0,128);using var input=new ScreenshotPixels(new(-8,-8,0,0),raw);var normalized=ScreenshotNormalizer.Normalize(input,true,default);var held=normalized.Pixels.Bytes;Assert.Equal(ScreenshotNormalizationProof.NativeSizeWhiteComposite,normalized.Proof);
        for(int i=0;i<held.Length;i+=4)Assert.Equal(new byte[]{127,127,127,255},held.Span.Slice(i,4).ToArray());normalized.Dispose();Assert.All(held.ToArray(),b=>Assert.Equal(0,b));Assert.Contains(raw,b=>b!=0);
    }
    [Theory][InlineData(256,65536)][InlineData(1280,4194304)]
    public void EncodedCapNeverTriggersALossyOrSmallerRetry(int side,int limit)
    {
        byte[] raw=new byte[side*side*4];new Random(7642).NextBytes(raw);for(int i=3;i<raw.Length;i+=4)raw[i]=255;var original=raw.ToArray();using var source=new OwnedSourceImage(new(new(0,0,side,side),raw));Assert.Throws<ScreenshotNormalizationException>(()=>source.PrepareRequest(limit));Assert.Equal(original,raw);Assert.False(source.IsDisposed);
    }
    [Fact]public void EncoderEnforcesItsExactByteBudgetAndCancellationWithoutConsumingCallerPixels()
    {
        using var input=new ScreenshotPixels(new(0,0,8,8),Solid(8,8,10,20,30,255));using var valid=ScreenshotPngEncoder.Encode(input);Assert.Throws<ScreenshotException>(()=>ScreenshotPngEncoder.Encode(input,maximumBytes:valid.Bytes.Length-1));using var exact=ScreenshotPngEncoder.Encode(input,maximumBytes:valid.Bytes.Length);Assert.Equal(valid.Bytes.ToArray(),exact.Bytes.ToArray());
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();Assert.ThrowsAny<OperationCanceledException>(()=>ScreenshotNormalizer.Normalize(input,true,cancelled.Token));Assert.Contains(input.Bytes.ToArray(),b=>b!=0);
    }
    [Theory][InlineData(8)][InlineData(3000)]
    public void CancelledPreparationRetainsTheSourceAndDisposedSourcesCannotPrepare(int width)
    {
        byte[] raw=Solid(width,8,0,0,0,255);var original=raw.ToArray();var source=new OwnedSourceImage(new(new(-10,2,width-10,10),raw));using var cancelled=new CancellationTokenSource();cancelled.Cancel();Assert.ThrowsAny<OperationCanceledException>(()=>source.PrepareRequest(4194304,cancelled.Token));Assert.Equal(original,raw);source.Dispose();Assert.All(raw,b=>Assert.Equal(0,b));Assert.Throws<ObjectDisposedException>(()=>source.PrepareRequest(4194304));
    }
    [Fact]public void CoordinateSpacesCannotBeSubstitutedInPublicPointOrRectangleContracts()
    {
        foreach(bool rectangle in new[]{false,true})
        {
            System.Reflection.MethodInfo Find(string name)=>typeof(ImageTransform).GetMethods().Single(m=>m.Name==name&&(m.ReturnType.GetProperty("Width")!=null)==rectangle);
            var forward=Find(nameof(ImageTransform.OriginalToNormalized));var inverse=Find(nameof(ImageTransform.NormalizedToOriginal));var toDesktop=Find(nameof(ImageTransform.NormalizedToDesktop));var fromDesktop=Find(nameof(ImageTransform.DesktopToNormalized));
            Type original=forward.GetParameters()[0].ParameterType,normalized=forward.ReturnType,desktop=toDesktop.ReturnType;
            Assert.NotEqual(original,normalized);Assert.NotEqual(original,desktop);Assert.NotEqual(normalized,desktop);Assert.Equal(normalized,inverse.GetParameters()[0].ParameterType);Assert.Equal(original,inverse.ReturnType);Assert.Equal(desktop,fromDesktop.GetParameters()[0].ParameterType);Assert.Equal(normalized,fromDesktop.ReturnType);
        }
    }
}
