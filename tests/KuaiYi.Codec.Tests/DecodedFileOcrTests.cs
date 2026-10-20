using System.Diagnostics;
using System.Runtime.InteropServices;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Imaging;
using SkiaSharp;
using Xunit;
namespace KuaiYi.Codec.Tests;

public sealed class DecodedFileOcrTests
{
    private sealed class Immediate:SynchronizationContext{public override void Post(SendOrPostCallback callback,object? state)=>callback(state);}
    // Explicit synthetic OCR port after an actual Linux native decode/private worker preparation.
    private sealed class SyntheticOcr:ILocalOcrCapabilitiesWorker
    {
        public ReadOnlyMemory<byte> Borrowed;public ImageSize Seen;public bool Disposed;
        public Task<OcrDiagnosticReply> DiagnoseAsync(OcrDiagnosticJob job,CancellationToken cancellation=default)=>Task.FromResult(new OcrDiagnosticReply(job,new(new(true,true,"KuaiYi.Development_synthetic","KuaiYi.Development_synthetic!ImageWorker",ImageFailure.None),["en-US"],2560,ImageFailure.None)));
        public Task<ImageReply> RunAsync(ReadOnlyMemory<byte> image,ImageJob job,CancellationToken cancellation=default)
        {Borrowed=image;Seen=ImagePolicy.InspectPng(image.Span);return Task.FromResult(new ImageReply(job,ImageFailure.None,new(Seen.Width,Seen.Height,job.Language,"PUBLIC DECODED FILE OCR",[new("PUBLIC",[new("PUBLIC",0,0,Seen.Width,Seen.Height)])])));}
        public ValueTask<bool> TryFinalizeAsync()=>ValueTask.FromResult(true);
        public ValueTask DisposeAsync(){Disposed=true;return ValueTask.CompletedTask;}
    }
    private static string Worker(){for(var path=new DirectoryInfo(AppContext.BaseDirectory);path!=null;path=path.Parent)if(File.Exists(Path.Combine(path.FullName,"SPEC.md")))return Path.Combine(path.FullName,"tools/KuaiYi.ImageDecodeFixture/bin/Release/net10.0/KuaiYi.ImageDecodeFixture.dll");throw new IOException("No fixture.");}
    private static byte[] Input(string kind)
    {
        if(kind=="gif")return GifDecodeTests.Gif(3,2,[GifDecodeTests.Image(1,0,2,1,[0,1]),GifDecodeTests.Image(0,0,3,2,[2,2,2,2,2,2])]);
        using var bitmap=new SKBitmap(new SKImageInfo(3,2,SKColorType.Bgra8888,SKAlphaType.Unpremul));Marshal.Copy(Enumerable.Range(0,24).Select(i=>(byte)(i%4==3?255:i*9)).ToArray(),0,bitmap.GetPixels(),24);using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(kind=="exif6"?SKEncodedImageFormat.Jpeg:SKEncodedImageFormat.Png,100);var encoded=data.ToArray();if(kind!="exif6")return encoded;
        byte[] tiff=new byte[26];tiff[0]=tiff[1]=(byte)'I';tiff[2]=42;tiff[4]=8;tiff[8]=1;tiff[10]=0x12;tiff[11]=1;tiff[12]=3;tiff[14]=1;tiff[18]=6;return [255,216,255,225,0,34,69,120,105,102,0,0,..tiff,..encoded[2..]];
    }
    [Theory][InlineData("png")][InlineData("exif6")][InlineData("gif")]
    public async Task ActualDecodedFileFeedsExistingOcrCoordinatorWithExactNormalizedSizeAndOriginalMapping(string kind)
    {
        Assert.False(OperatingSystem.IsWindows());byte[] bytes=Input(kind);await using var decoder=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,Worker());using var file=await decoder.PrepareFileAsync(bytes,new(Guid.NewGuid(),1,1,bytes.Length,ImageDecodePolicy.Hash(bytes)));Assert.True(decoder.ObserveExit());
        var gate=new RuntimeGate(requireProfileBinding:true);var origin=new EndpointOrigin(new Uri("https://synthetic.invalid/"));var config=new SettingsSnapshot(new(){ActiveProfileId="decoded-ocr",Profiles=[new(){Id="decoded-ocr",BaseUrl="https://synthetic.invalid/",Model="synthetic",Authentication=ApiAuthentication.None}]},"synthetic-decoded-ocr",SettingsLoadState.Current);gate.ObserveSettingsMetadata(config);gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));var version=Guid.NewGuid();var diagnostic=new SyntheticOcr();var recognition=new SyntheticOcr();int created=0;
        await using var coordinator=new OcrOnlyCoordinator(gate,()=>config,(v,id)=>v==version&&id==file.SourceId&&!file.IsDisposed,new Immediate(),()=>++created==1?diagnostic:recognition);
        Assert.True(coordinator.Discover(version,file));await coordinator.Running;var caps=Assert.IsType<OcrCapabilitiesSnapshot>(coordinator.Capabilities);Assert.True(coordinator.Start(caps,"en-US"));await coordinator.Running.WaitAsync(TimeSpan.FromSeconds(6));var preview=Assert.IsType<OcrTextPreview>(coordinator.Preview);
        Assert.Equal(kind=="exif6"?new ImageSize(2,3):new ImageSize(3,2),recognition.Seen);Assert.Equal(file.Metadata.Transform,preview.Transform);Assert.Equal(ImageSourceKind.LocalFile,preview.SourceKind);Assert.Equal(kind=="gif"?"使用首帧":null,preview.SourceNotice);var region=Assert.Single(preview.WordRegions);Assert.Equal(new OriginalImageRectangle(0,0,3,2),region.Original);Assert.False(preview.Transform!.HasDesktopAnchor);Assert.Throws<InvalidOperationException>(()=>preview.Transform.NormalizedToDesktop(region.Normalized));Assert.True(recognition.Borrowed.Span.IndexOfAnyExcept((byte)0)<0);Assert.True(recognition.Disposed);Assert.False(coordinator.CleanupPending);
        var approved=Assert.IsType<SelectionSourceSnapshot>(coordinator.Approve(preview));Assert.True(approved.Ocr!.FromFile);Assert.Equal(kind=="gif",approved.Ocr.UsedFirstFrame);Assert.Empty(approved.Ranges);Assert.Equal(0,approved.Window.Handle);Assert.False(gate.HasRunningGeneration);
    }
}
