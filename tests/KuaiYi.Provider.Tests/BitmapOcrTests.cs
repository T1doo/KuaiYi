extern alias ImageProduction;
using System.Reflection;
using System.Text.Json;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
using ApiProtocol=KuaiYi.Core.Settings.ApiProtocol;
using P=ImageProduction::KuaiYi.Imaging;
namespace KuaiYi.Provider.Tests;
// The same production coordinator, owner and Worker ports used by file OCR; no native OCR claim.
public sealed partial class FileImageOcrTests
{
    private static P.OwnedFileImage Bitmap(P.ImageSize? original=null,int limit=2560)
    {
        var transform=P.ImageTransform.PlanEncoded(original??new(8,8),P.ImageOrientation.ExifTopLeft,limit);int width=(int)transform.NormalizedSize.Width,height=(int)transform.NormalizedSize.Height;
        byte[] raw=Enumerable.Repeat((byte)255,width*height*4).ToArray();using var pixels=new P.ScreenshotPixels(new(0,0,width,height),raw.ToArray());
        return new(new(P.EncodedRasterFormat.Bmp,54+checked(((int)transform.OriginalSize.Width*3+3)/4*4*(int)transform.OriginalSize.Height),new string('a',64),transform,limit),raw,P.ScreenshotPngEncoder.Encode(pixels),P.ImageSourceKind.Bitmap);
    }
    private static void UseBitmap(Rig r){r.Image.Dispose();r.Image=Bitmap();}
    [Theory][InlineData(ApiProtocol.OpenAIChat)][InlineData(ApiProtocol.AnthropicMessages)]
    public async Task BitmapOcrUsesChosenLanguageExplicitLineageAndOnlyConfirmedTextDispatch(ApiProtocol protocol)
    {
        await using var r=await Rig.Create(protocol);UseBitmap(r);var caps=await r.Discover();Assert.Equal(P.ImageSourceKind.Bitmap,caps.SourceKind);Assert.Null(caps.SourceNotice);Assert.Equal(0,r.Worker.Calls);Assert.Equal(0,r.Script.Requests);
        Assert.False(r.Local.Start(caps,"not-installed"));Assert.False(r.Local.Start(caps with{},"en-US"));r.Worker=new();Assert.True(r.Local.Start(caps,"zh-Hans"));await r.Local.Running;
        var preview=Assert.IsType<P.OcrTextPreview>(r.Local.Preview);Assert.Equal("zh-Hans",preview.Language);Assert.Equal(P.ImageSourceKind.Bitmap,preview.SourceKind);Assert.Null(preview.SourceNotice);Assert.False(preview.Transform!.HasDesktopAnchor);Assert.True(r.Worker.Disposed);Assert.False(r.Local.CleanupPending);Assert.Equal(0,r.Script.Requests);Assert.Null(r.Local.Approve(preview with{}));
        var source=Assert.IsType<SelectionSourceSnapshot>(r.Local.Approve(preview));var lineage=Assert.IsType<OcrTextProvenance>(source.Ocr);Assert.True(lineage.Valid);Assert.Equal(OcrImageSourceKind.Bitmap,lineage.ImageKind);Assert.True(lineage.FromBitmap);Assert.False(lineage.FromFile);Assert.False(lineage.FromBrowser);Assert.False(lineage.UsedFirstFrame);Assert.Equal(r.Version,lineage.SourceVersion);Assert.Equal(r.Image.SourceId,lineage.SourceId);Assert.Equal(preview.Id,lineage.PreviewId);Assert.Equal(new SelectionWindow(0,0,0,""),source.Window);Assert.Empty(source.Ranges);Assert.Empty(source.RuntimeId);
        var offer=Assert.IsType<TextTranslationOffer>(r.Text.PrepareOcr(source,TranslationPreset.Plain));Assert.True(offer.RequiresConfirmation);Assert.Equal(TextSubmission.ConfirmationRequired,r.Text.Submit(offer));Assert.Equal(0,r.Script.Requests);
        r.Image.Dispose();Assert.True(r.Local.IsApprovedSourceCurrent(source));Assert.Equal(TextSubmission.Started,r.Text.Submit(offer,true));await r.Script.Received.Task.WaitAsync(TimeSpan.FromSeconds(6));
        using var json=JsonDocument.Parse(r.Script.Body);var content=json.RootElement.GetProperty("messages")[protocol==ApiProtocol.OpenAIChat?1:0].GetProperty("content");Assert.Equal(preview.Text,content.ValueKind==JsonValueKind.String?content.GetString():content[0].GetProperty("text").GetString());foreach(var forbidden in new[]{"base64","image_url","image/png",new string('a',64)})Assert.DoesNotContain(forbidden,r.Script.Body);
        await r.Script.Write(protocol==ApiProtocol.OpenAIChat?"{\"choices\":[{\"index\":0,\"message\":{\"content\":\"PUBLIC BITMAP TRANSLATION\"},\"finish_reason\":\"stop\"}]}":"{\"type\":\"message\",\"content\":[{\"type\":\"text\",\"text\":\"PUBLIC BITMAP TRANSLATION\"}],\"stop_reason\":\"end_turn\"}",true);await r.Text.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal("PUBLIC BITMAP TRANSLATION",r.Results.Text);Assert.Equal(OcrImageSourceKind.Bitmap,r.Text.SelectedSnapshot!.Source.Ocr!.ImageKind);
        var label=typeof(KuaiYi.App.ShellWindow).GetMethod("SourceLabel",BindingFlags.NonPublic|BindingFlags.Static)!;Assert.Equal("投放位图 · OCR-only · 图形关系未分析",label.Invoke(null,[r.Text.SelectedSnapshot]));var window=new KuaiYi.App.ShellWindow();window.InitializeResultFlow();window.UseTranslation(r.Text,r.Settings);Assert.Contains("投放位图 · ",window.UsageVersionMenuForTest(Assert.Single(r.Results.Versions)));
    }
    [Fact]public async Task BitmapMappingUsesPreparedRasterRoundedAxesAndNeverDesktopCoordinates()
    {
        await using var r=await Rig.Create();r.Image.Dispose();r.Image=Bitmap(new(6001,3),16);r.Worker.Maximum=16;var caps=await r.Discover();r.Worker=new();Assert.True(r.Local.Start(caps,"en-US"));await r.Local.Running;var preview=Assert.IsType<P.OcrTextPreview>(r.Local.Preview);var transform=Assert.IsType<P.OwnedFileImage>(r.Image).Metadata.Transform;Assert.Equal(transform,preview.Transform);Assert.NotEqual(transform.ScaleX,transform.ScaleY);var word=Assert.Single(preview.WordRegions);Assert.Equal(new P.OriginalImageRectangle(6001d/16,0,6001d/16,3),word.Original);Assert.Throws<InvalidOperationException>(()=>transform.NormalizedToDesktop(word.Normalized));using var request=r.Image.PrepareRequest(4194304);Assert.Null(request.NormalizationProof);Assert.Equal(P.ImageSourceKind.Bitmap,request.Kind);Assert.True(r.Worker.Borrowed.Span.IndexOfAnyExcept((byte)0)<0);Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData("clear")][InlineData("replace")][InlineData("off")][InlineData("settings")][InlineData("dispose")]
    public async Task BitmapCancelledOrChangedSourceCannotPublishLateOcrAndKeepsLoanUntilOriginalReturn(string change)
    {
        await using var r=await Rig.Create();UseBitmap(r);var caps=await r.Discover();r.Worker=new(){Hold=true};Assert.True(r.Local.Start(caps,"en-US"));var original=r.Local.Running;await r.Worker.Entered.Task.WaitAsync(TimeSpan.FromSeconds(6));
        if(change=="replace"){r.Image.Dispose();r.Image=Bitmap();r.Version=Guid.NewGuid();}if(change=="off")r.Gate.Disable();if(change=="settings")r.Settings=r.Settings with{Version="changed"};if(change=="dispose")r.Image.Dispose();
        var clear=r.Local.ClearAsync();Assert.False(clear.IsCompleted);Assert.False(original.IsCompleted);Assert.True(r.Worker.Token.IsCancellationRequested);Assert.Contains(r.Worker.Borrowed.ToArray(),b=>b!=0);Assert.False(r.Local.Start(r.Version,r.Image,"en-US"));r.Worker.Release.SetResult();await clear.WaitAsync(TimeSpan.FromSeconds(6));Assert.Null(r.Local.Preview);Assert.Null(r.Local.Capabilities);Assert.False(r.Local.CleanupPending);Assert.True(r.Worker.Borrowed.Span.IndexOfAnyExcept((byte)0)<0);Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData("off")][InlineData("settings")][InlineData("replace")][InlineData("clear")]
    public async Task BitmapCompletedPreviewCannotBorrowNewUiSourceOrSettingAdmission(string change)
    {
        await using var r=await Rig.Create();UseBitmap(r);var preview=await r.Recognize();if(change=="off")r.Gate.Disable();if(change=="settings")r.Settings=r.Settings with{Version="changed"};if(change=="replace"){r.Image.Dispose();r.Image=Bitmap();r.Version=Guid.NewGuid();}if(change=="clear")r.Local.Clear();Assert.False(r.Local.IsPreviewCurrent(preview));Assert.Null(r.Local.Approve(preview));Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task BitmapCleanupFailureCannotProduceApprovalAndBlocksReplacementUntilExactWorkerFinalizes(bool throws)
    {
        await using var r=await Rig.Create();UseBitmap(r);var original=r.Worker;original.Finalized=false;original.FinalizeThrows=throws;Assert.True(r.Local.Start(r.Version,r.Image,"en-US"));await r.Local.Running;Assert.Null(r.Local.Preview);Assert.Equal("CLEANUP_UNCONFIRMED",r.Local.Failure);Assert.True(r.Local.CleanupPending);Assert.False(original.Disposed);Assert.False(r.Local.Start(r.Version,r.Image,"en-US"));Assert.Equal(0,r.Script.Requests);original.Finalized=true;original.FinalizeThrows=false;await r.Local.ClearAsync();Assert.True(original.Disposed);Assert.False(r.Local.CleanupPending);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task BitmapQueuedDiscoveryOrRecognitionCannotPublishIntoReplacementWithoutCancellation(bool discover)
    {
        var queue=new Deferred();await using var r=await Rig.Create(destination:queue);UseBitmap(r);Assert.True(discover?r.Local.Discover(r.Version,r.Image):r.Local.Start(r.Version,r.Image,"en-US"));var original=r.Local.Running;await queue.Posted.Task.WaitAsync(TimeSpan.FromSeconds(6));r.Image.Dispose();r.Image=Bitmap();r.Version=Guid.NewGuid();Assert.False(r.Local.Start(r.Version,r.Image,"en-US"));queue.Drain();await original.WaitAsync(TimeSpan.FromSeconds(6));Assert.Null(r.Local.Preview);Assert.Null(r.Local.Capabilities);Assert.False(r.Gate.HasRunningGeneration);Assert.Equal(0,r.Script.Requests);
    }
    [Fact]public async Task BitmapApprovedTextIsRevokedByClearOffAndNewRecognition()
    {
        await using var r=await Rig.Create();UseBitmap(r);var first=await r.Recognize();var approved=Assert.IsType<SelectionSourceSnapshot>(r.Local.Approve(first));Assert.True(r.Local.IsApprovedSourceCurrent(approved));r.Local.Clear();Assert.False(r.Local.IsApprovedSourceCurrent(approved));Assert.Null(r.Text.PrepareOcr(approved,TranslationPreset.Plain));
        r.Worker=new();var next=await r.Recognize();approved=Assert.IsType<SelectionSourceSnapshot>(r.Local.Approve(next));r.Gate.Disable();Assert.False(r.Local.IsApprovedSourceCurrent(approved));Assert.Null(r.Text.PrepareOcr(approved,TranslationPreset.Plain));Assert.True(r.Gate.TryEnable(new(new Uri(r.Settings.Data.Profiles[0].BaseUrl)),true));r.Worker=new();Assert.True(r.Local.Start(r.Version,r.Image,"en-US"));Assert.False(r.Local.IsApprovedSourceCurrent(approved));await r.Local.Running;Assert.Equal(0,r.Script.Requests);
    }
    [Fact]public async Task BrowserControlAlsoUsesExplicitEnumAndDoesNotBecomeBitmapOrScreenshot()
    {
        await using var r=await Rig.Create();r.Image.Dispose();byte[] raw=Enumerable.Repeat((byte)255,256).ToArray();using var pixels=new P.ScreenshotPixels(new(0,0,8,8),raw.ToArray());r.Image=new P.OwnedFileImage(new(P.EncodedRasterFormat.Png,1,new string('a',64),P.ImageTransform.PlanEncoded(new(8,8),P.ImageOrientation.ExifTopLeft),2560),raw,P.ScreenshotPngEncoder.Encode(pixels),P.ImageSourceKind.Browser);var preview=await r.Recognize();var source=Assert.IsType<SelectionSourceSnapshot>(r.Local.Approve(preview));Assert.Equal(OcrImageSourceKind.Browser,source.Ocr!.ImageKind);Assert.True(source.Ocr.FromBrowser);Assert.False(source.Ocr.FromFile);Assert.False(source.Ocr.FromBitmap);Assert.True(source.Ocr.Valid);Assert.Equal(0,r.Script.Requests);
    }
    [Fact]public void UnknownOcrEnumCannotGetAScreenshotLabelOrValidLineage()
    {
        var metadata=new OcrTextProvenance(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),(OcrImageSourceKind)99);Assert.False(metadata.Valid);var label=typeof(KuaiYi.App.ShellWindow).GetMethod("OcrSourcePrefix",BindingFlags.NonPublic|BindingFlags.Static)!;var error=Assert.Throws<TargetInvocationException>(()=>label.Invoke(null,[metadata]));Assert.IsType<InvalidOperationException>(error.InnerException);Assert.False((metadata with{ImageKind=OcrImageSourceKind.Bitmap,UsedFirstFrame=true}).Valid);
    }
    [Theory][InlineData(7,false)][InlineData(8,true)]
    public async Task BitmapNativeDimensionLimitIsCheckedBeforeRecognitionWorker(uint maximum,bool accepted)
    {
        await using var r=await Rig.Create();UseBitmap(r);r.Worker.Maximum=maximum;var caps=await r.Discover();r.Worker=new();Assert.Equal(accepted,r.Local.Start(caps,"en-US"));if(accepted){await r.Local.Running;Assert.NotNull(r.Local.Preview);}else{Assert.Equal(1,r.Factories);Assert.Equal(0,r.Worker.Calls);Assert.Equal("OcrDimensionExceeded",r.Local.Failure);}Assert.Equal(0,r.Script.Requests);
    }
    [Fact]public async Task BitmapOffCannotCreateDiscoveryOrRecognitionWorker()
    {await using var r=await Rig.Create();UseBitmap(r);r.Gate.Disable();Assert.False(r.Local.Discover(r.Version,r.Image));Assert.False(r.Local.Start(r.Version,r.Image,"en-US"));Assert.Equal(0,r.Factories);Assert.Equal(0,r.Script.Requests);}
    [Theory][InlineData(true)][InlineData(false)]
    public async Task BitmapActualLinuxWorkerStillRejectsUnavailablePlatformWithoutPretendingNativeOcr(bool discover)
    {
        Assert.False(OperatingSystem.IsWindows());await using var r=await Rig.Create();UseBitmap(r);r.Factory=()=>new P.ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,OcrCapabilitiesDiscoveryTests.WorkerAssembly(),new OcrCapabilitiesDiscoveryTests.Starter());Assert.True(discover?r.Local.Discover(r.Version,r.Image):r.Local.Start(r.Version,r.Image,"en-US"));await r.Local.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal("UnsupportedPlatform",r.Local.Failure);Assert.Null(r.Local.Capabilities);Assert.Null(r.Local.Preview);Assert.False(r.Local.CleanupPending);Assert.False(r.Gate.HasRunningGeneration);Assert.Equal(0,r.Script.Requests);
    }
}
