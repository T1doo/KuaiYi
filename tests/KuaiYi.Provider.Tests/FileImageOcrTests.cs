extern alias ImageProduction;
using System.Collections.Concurrent;
using System.Text.Json;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
using ApiProtocol=KuaiYi.Core.Settings.ApiProtocol;
using P=ImageProduction::KuaiYi.Imaging;
namespace KuaiYi.Provider.Tests;

// Synthetic prepared file owners and OCR port; these tests do not claim native decoding/OCR or WPF.
public sealed class FileImageOcrTests
{
    private sealed class Immediate:SynchronizationContext{public override void Post(SendOrPostCallback callback,object? state)=>callback(state);}
    private sealed class Deferred:SynchronizationContext
    {
        private readonly ConcurrentQueue<Action> queue=new();public readonly TaskCompletionSource Posted=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override void Post(SendOrPostCallback callback,object? state){queue.Enqueue(()=>callback(state));Posted.TrySetResult();}
        public void Drain(){while(queue.TryDequeue(out var callback))callback();}
    }
    private sealed class SyntheticWorker:P.ILocalOcrCapabilitiesWorker
    {
        public uint Maximum=2560;public int Calls;public bool Hold,Finalized=true,FinalizeThrows,Disposed;public string Fault="";
        public ReadOnlyMemory<byte> Borrowed;public CancellationToken Token;
        public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<P.OcrDiagnosticReply> DiagnoseAsync(P.OcrDiagnosticJob job,CancellationToken cancellation=default)
        {Token=cancellation;Entered.TrySetResult();if(Hold)await Release.Task;return new(job,OcrCapabilitiesDiscoveryTests.Good(max:Maximum));}
        public async Task<P.ImageReply> RunAsync(ReadOnlyMemory<byte> bytes,P.ImageJob job,CancellationToken cancellation=default)
        {
            Calls++;Borrowed=bytes;Token=cancellation;var size=P.ImagePolicy.InspectPng(bytes.Span);Entered.TrySetResult();if(Hold)await Release.Task;
            var word=new P.OcrWord("PUBLIC",1,0,1,1);
            if(Fault=="nan")word=word with{X=double.NaN};if(Fault=="bounds")word=word with{X=size.Width,Width=double.Epsilon};
            var output=new P.OcrOutput(size.Width,size.Height,job.Language,"PUBLIC FILE OCR TEXT",[new("PUBLIC FILE OCR TEXT",[word])]);
            if(Fault=="size")output=output with{Width=output.Width+1};if(Fault=="language")output=output with{Language="fr-FR"};
            if(Fault=="words")output=output with{Lines=[new("PUBLIC",Enumerable.Repeat(word,50001).ToArray())]};
            if(Fault=="empty")output=output with{Text=" "};
            if(Fault=="max-words")output=output with{Lines=[new("PUBLIC",Enumerable.Repeat(word,50000).ToArray())]};
            return new(Fault=="job"?job with{Epoch=job.Epoch+1}:job,P.ImageFailure.None,output);
        }
        public ValueTask<bool> TryFinalizeAsync()=>FinalizeThrows?ValueTask.FromException<bool>(new IOException("SYNTHETIC FINALIZE")):ValueTask.FromResult(Finalized);
        public ValueTask DisposeAsync(){Disposed=true;return ValueTask.CompletedTask;}
    }
    private static P.OwnedFileImage File(P.ImageSize? original=null,P.ImageOrientation orientation=P.ImageOrientation.ExifTopLeft,int limit=2560,bool gif=false)
    {
        var transform=P.ImageTransform.PlanEncoded(original??new(8,8),orientation,limit);int width=(int)transform.NormalizedSize.Width,height=(int)transform.NormalizedSize.Height;
        byte[] raw=Enumerable.Repeat((byte)255,width*height*4).ToArray();using var pixels=new P.ScreenshotPixels(new(0,0,width,height),raw.ToArray());
        return new(new(gif?P.EncodedRasterFormat.Gif:P.EncodedRasterFormat.Png,1,new string('a',64),transform,limit,gif?new(2,0,0,(int)transform.OriginalSize.Width,(int)transform.OriginalSize.Height):null),raw,P.ScreenshotPngEncoder.Encode(pixels));
    }
    private sealed class Rig:IAsyncDisposable
    {
        public readonly RuntimeGate Gate=new(requireProfileBinding:true);public readonly PhaseHttpFixture Server=new();public readonly ProviderClient Provider=new();
        public SettingsSnapshot Settings=null!;public PhaseHttpFixture.Script Script=null!;public P.OcrOnlyCoordinator Local=null!;public TextTranslationCoordinator Text=null!;public OverlayResults Results=null!;
        public P.ImageSourceOwner Image=File();public Guid Version=Guid.NewGuid();public bool Current=true;public int Factories;
        public SyntheticWorker Worker=new();public Func<P.ILocalOcrWorker>? Factory;
        public static async Task<Rig> Create(ApiProtocol protocol=ApiProtocol.OpenAIChat,SynchronizationContext? destination=null)
        {
            var r=new Rig();await r.Server.InitializeAsync();r.Script=r.Server.Add("runs","application/json");var prefix=new Uri(r.Server.BaseUri,$"runs/{Guid.NewGuid():N}/fragmented/");var boundary=DevelopmentTranslationBoundary.FromPrefix(prefix);
            r.Settings=new(new(){ActiveProfileId="file-ocr",Profiles=[new(){Id="file-ocr",Protocol=protocol,BaseUrl=prefix.AbsoluteUri,Model=DevelopmentTranslationBoundary.FixtureModel,Authentication=ApiAuthentication.None,Streaming=false,Capabilities=new(){Vision=Capability.Unsupported}}]},"file-ocr-synthetic",SettingsLoadState.Current);
            r.Gate.ObserveSettingsMetadata(r.Settings);r.Gate.ConsentTo(new(prefix));Assert.True(r.Gate.TryEnable(new(prefix),true));r.Results=new(r.Gate);
            r.Local=new(r.Gate,()=>r.Settings,(version,id)=>r.Current&&version==r.Version&&id==r.Image.SourceId&&!r.Image.IsDisposed,destination??new Immediate(),()=>{r.Factories++;return r.Factory?.Invoke()??r.Worker;});
            r.Text=new(r.Gate,()=>r.Settings,(_,_)=>true,r.Provider,new Immediate(),r.Results,_=>{},developmentBoundary:boundary,ocrSourceCurrent:r.Local.IsApprovedSourceCurrent);return r;
        }
        public async Task<P.OcrCapabilitiesSnapshot> Discover(){Assert.True(Local.Discover(Version,Image));await Local.Running.WaitAsync(TimeSpan.FromSeconds(6));return Assert.IsType<P.OcrCapabilitiesSnapshot>(Local.Capabilities);}
        public async Task<P.OcrTextPreview> Recognize(){Assert.True(Local.Start(Version,Image,"en-US"));await Local.Running.WaitAsync(TimeSpan.FromSeconds(6));return Assert.IsType<P.OcrTextPreview>(Local.Preview);}
        public async ValueTask DisposeAsync(){Worker.Release.TrySetResult();Worker.Finalized=true;Worker.FinalizeThrows=false;Gate.Disable();await Local.DisposeAsync();await Text.DisposeAsync();Image.Dispose();Provider.Dispose();await Server.DisposeAsync();}
    }
    [Theory][InlineData(ApiProtocol.OpenAIChat)][InlineData(ApiProtocol.AnthropicMessages)]
    public async Task FileGifUsesInstalledLanguageThenExactPreviewAndConfirmedTextOnlyDispatch(ApiProtocol protocol)
    {
        await using var r=await Rig.Create(protocol);r.Image.Dispose();r.Image=File(gif:true);var caps=await r.Discover();Assert.Equal(P.ImageSourceKind.LocalFile,caps.SourceKind);Assert.Equal("使用首帧",caps.SourceNotice);Assert.False(r.Local.Start(caps with{},"en-US"));
        r.Worker=new();Assert.True(r.Local.Start(caps,"zh-Hans"));await r.Local.Running;var preview=Assert.IsType<P.OcrTextPreview>(r.Local.Preview);Assert.Equal("zh-Hans",preview.Language);Assert.Equal(P.ImageSourceKind.LocalFile,preview.SourceKind);Assert.Equal("使用首帧",preview.SourceNotice);Assert.False(preview.Transform!.HasDesktopAnchor);Assert.Single(preview.WordRegions);Assert.Equal(0,r.Script.Requests);
        Assert.Null(r.Local.Approve(preview with{}));var source=Assert.IsType<SelectionSourceSnapshot>(r.Local.Approve(preview));Assert.True(source.Ocr!.FromFile);Assert.True(source.Ocr.UsedFirstFrame);Assert.Empty(source.Ranges);Assert.Equal(0,source.Window.Handle);
        var offer=Assert.IsType<TextTranslationOffer>(r.Text.PrepareOcr(source,TranslationPreset.Explain));Assert.Equal(TextSubmission.ConfirmationRequired,r.Text.Submit(offer));Assert.Equal(0,r.Script.Requests);
        // Accepted text remains a text source after the image owner is cleared, as in screenshot OCR.
        r.Image.Dispose();Assert.Equal(TextSubmission.Started,r.Text.Submit(offer,true));await r.Script.Received.Task.WaitAsync(TimeSpan.FromSeconds(6));
        using var json=JsonDocument.Parse(r.Script.Body);var content=json.RootElement.GetProperty("messages")[protocol==ApiProtocol.OpenAIChat?1:0].GetProperty("content");Assert.Equal(preview.Text,content.ValueKind==JsonValueKind.String?content.GetString():content[0].GetProperty("text").GetString());Assert.DoesNotContain("base64",r.Script.Body);Assert.DoesNotContain("image_url",r.Script.Body);Assert.DoesNotContain("使用首帧",r.Script.Body);Assert.DoesNotContain(new string('a',64),r.Script.Body);
        await r.Script.Write(protocol==ApiProtocol.OpenAIChat?"{\"choices\":[{\"index\":0,\"message\":{\"content\":\"PUBLIC FILE TRANSLATION\"},\"finish_reason\":\"stop\"}]}":"{\"type\":\"message\",\"content\":[{\"type\":\"text\",\"text\":\"PUBLIC FILE TRANSLATION\"}],\"stop_reason\":\"end_turn\"}",true);await r.Text.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal("PUBLIC FILE TRANSLATION",r.Results.Text);Assert.True(r.Text.SelectedSnapshot?.Source.Ocr?.UsedFirstFrame);
        var label=typeof(KuaiYi.App.ShellWindow).GetMethod("SourceLabel",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
        Assert.Equal("文件图片 · 使用首帧 · OCR-only · 图形关系未分析",label.Invoke(null,[r.Text.SelectedSnapshot]));
    }
    [Theory][InlineData(7,false)][InlineData(8,true)][InlineData(4000,true)]
    public async Task NativeCapUsesActualFileRasterAndRejectsBeforeCreatingRecognitionWorker(uint maximum,bool accepted)
    {
        await using var r=await Rig.Create();r.Worker.Maximum=maximum;var caps=await r.Discover();r.Worker=new();Assert.Equal(accepted,r.Local.Start(caps,"en-US"));if(accepted){await r.Local.Running;Assert.NotNull(r.Local.Preview);Assert.Equal(2,r.Factories);}else{Assert.Equal("OcrDimensionExceeded",r.Local.Failure);Assert.Equal(1,r.Factories);Assert.Equal(0,r.Worker.Calls);}Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData(P.ImageOrientation.ExifTopLeft)][InlineData(P.ImageOrientation.ExifTopRight)][InlineData(P.ImageOrientation.ExifBottomRight)][InlineData(P.ImageOrientation.ExifBottomLeft)][InlineData(P.ImageOrientation.ExifLeftTop)][InlineData(P.ImageOrientation.ExifRightTop)][InlineData(P.ImageOrientation.ExifRightBottom)][InlineData(P.ImageOrientation.ExifLeftBottom)]
    public async Task FileOcrMapsAllExifOrientationsAndRoundedAxesWithoutDesktop(P.ImageOrientation orientation)
    {
        await using var r=await Rig.Create();r.Image.Dispose();r.Image=File(new(6001,3003),orientation,16);r.Worker.Maximum=16;var caps=await r.Discover();Assert.True(r.Image.Width<=16&&r.Image.Height<=16);r.Worker=new();Assert.True(r.Local.Start(caps,"en-US"));await r.Local.Running;var preview=Assert.IsType<P.OcrTextPreview>(r.Local.Preview);var transform=Assert.IsType<P.OwnedFileImage>(r.Image).Metadata.Transform;Assert.Equal(transform,preview.Transform);Assert.True(transform.Downscaled);Assert.NotEqual(transform.ScaleX,transform.ScaleY);var word=Assert.Single(preview.WordRegions);Assert.Equal(0,word.LineIndex);Assert.Equal(0,word.WordIndex);Assert.Equal(new P.NormalizedImageRectangle(1,0,1,1),word.Normalized);double xStep=6001d/16,yStep=3003d/8;
        var expected=orientation switch
        {
            P.ImageOrientation.ExifTopLeft=>new P.OriginalImageRectangle(xStep,0,xStep,yStep),
            P.ImageOrientation.ExifTopRight=>new(6001-2*xStep,0,xStep,yStep),
            P.ImageOrientation.ExifBottomRight=>new(6001-2*xStep,3003-yStep,xStep,yStep),
            P.ImageOrientation.ExifBottomLeft=>new(xStep,3003-yStep,xStep,yStep),
            P.ImageOrientation.ExifLeftTop=>new(0,yStep,xStep,yStep),
            P.ImageOrientation.ExifRightTop=>new(0,3003-2*yStep,xStep,yStep),
            P.ImageOrientation.ExifRightBottom=>new(6001-xStep,3003-2*yStep,xStep,yStep),
            _=>new(6001-xStep,yStep,xStep,yStep)
        };Assert.Equal(expected,word.Original);Assert.Throws<InvalidOperationException>(()=>transform.NormalizedToDesktop(word.Normalized));Assert.True(r.Worker.Borrowed.Span.IndexOfAnyExcept((byte)0)<0);Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData("job")][InlineData("size")][InlineData("language")][InlineData("nan")][InlineData("bounds")][InlineData("words")][InlineData("empty")]
    public async Task InvalidFileOcrOutputNeverMakesMappingPreviewOrHttp(string fault)
    {await using var r=await Rig.Create();r.Worker.Fault=fault;Assert.True(r.Local.Start(r.Version,r.Image,"en-US"));await r.Local.Running;Assert.Equal("OCR_FAILED",r.Local.Failure);Assert.Null(r.Local.Preview);Assert.False(r.Local.CleanupPending);Assert.Equal(0,r.Script.Requests);Assert.True(r.Worker.Borrowed.Span.IndexOfAnyExcept((byte)0)<0);}
    [Theory][InlineData("clear")][InlineData("dispose")][InlineData("replace")][InlineData("off")]
    public async Task FileRequestLoanOutlivesSourceAndCancellationUntilOriginalTaskReturns(string change)
    {
        await using var r=await Rig.Create();r.Worker.Hold=true;Assert.True(r.Local.Start(r.Version,r.Image,"en-US"));var original=r.Local.Running;await r.Worker.Entered.Task.WaitAsync(TimeSpan.FromSeconds(6));
        if(change=="dispose")r.Image.Dispose();if(change=="replace"){r.Image.Dispose();r.Image=File();r.Version=Guid.NewGuid();}if(change=="off")r.Gate.Disable();
        var clear=r.Local.ClearAsync();Assert.False(clear.IsCompleted);Assert.False(original.IsCompleted);Assert.True(r.Worker.Token.IsCancellationRequested);Assert.Contains(r.Worker.Borrowed.ToArray(),value=>value!=0);Assert.False(r.Local.Start(r.Version,r.Image,"en-US"));r.Worker.Release.SetResult();await clear.WaitAsync(TimeSpan.FromSeconds(6));Assert.True(r.Worker.Borrowed.Span.IndexOfAnyExcept((byte)0)<0);Assert.Null(r.Local.Preview);Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task FileCleanupUnconfirmedKeepsOriginalOwnerAndBlocksReplacementUntilSameWorkerRetry(bool throws)
    {
        await using var r=await Rig.Create();var original=r.Worker;original.Finalized=false;original.FinalizeThrows=throws;Assert.True(r.Local.Start(r.Version,r.Image,"en-US"));await r.Local.Running;Assert.Equal("CLEANUP_UNCONFIRMED",r.Local.Failure);Assert.True(r.Local.CleanupPending);Assert.False(original.Disposed);r.Image.Dispose();r.Image=File();r.Version=Guid.NewGuid();Assert.False(r.Local.Start(r.Version,r.Image,"en-US"));Assert.Equal(1,r.Factories);
        if(throws)await Assert.ThrowsAsync<IOException>(()=>r.Local.ClearAsync());else await Assert.ThrowsAsync<P.ImageJobException>(()=>r.Local.ClearAsync());Assert.True(r.Local.CleanupPending);original.Finalized=true;original.FinalizeThrows=false;await r.Local.ClearAsync();Assert.True(original.Disposed);r.Worker=new();Assert.NotNull(await r.Recognize());Assert.Equal(2,r.Factories);Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task QueuedFileDiscoveryOrRecognitionAfterReplacementCannotRestoreOldOffer(bool discover)
    {
        var context=new Deferred();await using var r=await Rig.Create(destination:context);Assert.True(discover?r.Local.Discover(r.Version,r.Image):r.Local.Start(r.Version,r.Image,"en-US"));var original=r.Local.Running;await context.Posted.Task.WaitAsync(TimeSpan.FromSeconds(6));r.Image.Dispose();r.Image=File();r.Version=Guid.NewGuid();r.Local.Clear();context.Drain();await original.WaitAsync(TimeSpan.FromSeconds(6));Assert.Null(r.Local.Capabilities);Assert.Null(r.Local.Preview);Assert.False(r.Gate.HasRunningGeneration);Assert.Equal(0,r.Script.Requests);
    }
    [Fact] public async Task LateFileModalRejectionCannotStopOrRemoveNewerRecognitionOrApprovedText()
    {
        await using var r=await Rig.Create();var old=await r.Recognize();r.Local.RejectPreview(old with{});Assert.Same(old,r.Local.Preview);r.Local.RejectPreview(old);Assert.Null(r.Local.Preview);
        r.Worker=new(){Hold=true};Assert.True(r.Local.Start(r.Version,r.Image,"en-US"));await r.Worker.Entered.Task.WaitAsync(TimeSpan.FromSeconds(6));r.Local.RejectPreview(old);Assert.False(r.Worker.Token.IsCancellationRequested);Assert.True(r.Gate.HasRunningGeneration);r.Worker.Release.SetResult();await r.Local.Running;var next=Assert.IsType<P.OcrTextPreview>(r.Local.Preview);r.Local.RejectPreview(old);Assert.Same(next,r.Local.Preview);Assert.Null(r.Local.Approve(old));var approved=Assert.IsType<SelectionSourceSnapshot>(r.Local.Approve(next));r.Local.RejectPreview(old);Assert.True(r.Local.IsApprovedSourceCurrent(approved));Assert.Equal(0,r.Script.Requests);
    }
    [Fact] public async Task DirectLegacyRecognitionStillRejectsOversizeScreenshotBeforeWorkerCreation()
    {await using var r=await Rig.Create();r.Image.Dispose();r.Image=new P.OwnedSourceImage(new P.ScreenshotPixels(new(0,0,2561,1),Enumerable.Repeat((byte)255,2561*4).ToArray()));Assert.False(r.Local.Start(r.Version,r.Image,"en-US"));Assert.Equal("SMALLER_SCREENSHOT_REQUIRED",r.Local.Failure);Assert.Equal(0,r.Factories);Assert.Equal(0,r.Script.Requests);}
    [Fact] public async Task MaximumValidatedWordCountProducesAnImmutableBoundedNumericMap()
    {await using var r=await Rig.Create();r.Worker.Fault="max-words";var preview=await r.Recognize();Assert.Equal(50000,preview.WordRegions.Length);Assert.Equal(49999,preview.WordRegions[^1].WordIndex);Assert.Equal(new P.NormalizedImageRectangle(1,0,1,1),preview.WordRegions[^1].Normalized);Assert.Equal(0,r.Script.Requests);}
    [Theory][InlineData(2560,true)][InlineData(2559,false)]
    public async Task NativeSizeBoundaryAcceptsNormalized2560FileWithoutComparingItsLargerOriginal(uint maximum,bool accepted)
    {await using var r=await Rig.Create();r.Image.Dispose();r.Image=File(new(6001,1));Assert.Equal(2560,r.Image.Width);r.Worker.Maximum=maximum;var caps=await r.Discover();r.Worker=new();Assert.Equal(accepted,r.Local.Start(caps,"en-US"));if(accepted){await r.Local.Running;Assert.NotNull(r.Local.Preview);}else Assert.Equal(1,r.Factories);Assert.Equal(0,r.Script.Requests);}
    [Fact] public async Task ActualLinuxWorkerRejectsFileRecognitionWithoutNativeSuccessOrHttp()
    {Assert.False(OperatingSystem.IsWindows());await using var r=await Rig.Create();r.Factory=()=>new P.ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,OcrCapabilitiesDiscoveryTests.WorkerAssembly(),new OcrCapabilitiesDiscoveryTests.Starter());Assert.True(r.Local.Start(r.Version,r.Image,"en-US"));await r.Local.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal("UnsupportedPlatform",r.Local.Failure);Assert.Null(r.Local.Preview);Assert.False(r.Local.CleanupPending);Assert.False(r.Gate.HasRunningGeneration);Assert.Equal(0,r.Script.Requests);}
}
