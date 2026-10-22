extern alias ImageProduction;
using System.Text.Json;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Imaging;
using KuaiYi.Providers;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;
using P=ImageProduction::KuaiYi.Imaging;
namespace KuaiYi.Provider.Tests;
public sealed partial class ProductionScreenshotShellTests
{
    private sealed class Immediate:SynchronizationContext{public override void Post(SendOrPostCallback action,object? state)=>action(state);}
    private sealed class Rig:IAsyncDisposable
    {
        public readonly PhaseHttpFixture Server=new();public PhaseHttpFixture.Script Script=null!;public readonly ShellWindow Window=new();public readonly ProviderClient Provider=new();
        public SettingsSnapshot Config=null!;public ProductionScreenshotCoordinator Flow=null!;public OwnedSourceImage Image=null!;public byte[] Raw=[];
        public static async Task<Rig> Create()
        {
            var r=new Rig();await r.Server.InitializeAsync();r.Script=r.Server.Add("shell","application/json");var w=r.Window;w.InitializeResultFlow();w.WithGeometry();
            var config=new SettingsSnapshot(new(){ActiveProfileId="v",VisionProfileId="v",Profiles=[new(){Id="v",BaseUrl=new Uri(r.Server.BaseUri,"shell/").AbsoluteUri,AllowLoopbackHttp=true,Authentication=ApiAuthentication.None,Model="PUBLIC_SHELL_VISION",Streaming=false,Capabilities=new(){Vision=Capability.Supported}}]},"public-shell-settings",SettingsLoadState.Current);
            r.Config=config;r.Flow=w.ConfigureScreenshotForTest(config,r.Provider,new Immediate());var origin=new EndpointOrigin(new Uri("https://different-text.public.invalid"));w.MarkdownGate.ConsentTo(origin);Assert.True(w.MarkdownGate.TryEnable(origin,true));
            var input=w.ImageInputForTest();using(input.Intent)using(input.Cancellation)
            {r.Raw=Enumerable.Range(0,256).Select(i=>(byte)(i%4==3?255:i)).ToArray();r.Image=new(new ScreenshotPixels(new(0,0,8,8),r.Raw));Assert.True(input.Commit(r.Image));w.EndRestoreForTest(input.Cancellation);}
            return r;
        }
        public async Task Requests(int n){using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(Script.Requests<n)await Task.Delay(5,limit.Token);}
        public Task Reply(string text)=>Script.Write(JsonSerializer.Serialize(new{choices=new[]{new{index=0,message=new{content=text},finish_reason="stop"}},usage=new{prompt_tokens=23,completion_tokens=9}}),true);
        public void Drain(){for(int i=0;i<8&&Window.FlushPending;i++)Window.TickForTest();Assert.False(Window.FlushPending);}
        public async ValueTask DisposeAsync(){try{await Flow.DisposeAsync();await Window.ClearContentForTest();}finally{Provider.Dispose();await Server.DisposeAsync();}}
    }
    [Fact]public async Task ActualShellCommandShowsTwoCallDecisionAndRendersImageAlongsideGeneratedMarkdownUsageAndUncertainty()
    {
        await using var r=await Rig.Create();var w=r.Window;Assert.True(w.LocalPreviewForTest);Assert.True(w.ScreenshotSendEnabledForTest);Assert.Empty(r.Script.Bodies);ProductionScreenshotOffer? seen=null;
        Assert.True(w.SendScreenshotForTest(ImageOutputMode.Automatic,offer=>{seen=offer;Assert.Contains("两次",offer.ConfirmationText);Assert.Contains("PUBLIC_SHELL_VISION",offer.ConfirmationText);return true;}));
        await r.Requests(1);await r.Reply(ImageClassificationTests.Json("uncertain"));await r.Requests(2);await r.Reply("**公开图译正文**");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));r.Drain();
        Assert.NotNull(seen);Assert.False(w.LocalPreviewForTest);Assert.False(w.ScreenshotSendEnabledForTest);Assert.Equal("**公开图译正文**",w.MarkdownResults.Text);Assert.Contains(w.MarkdownModel.Blocks,b=>b.Text.Contains("公开图译正文"));Assert.Same(r.Image,w.SelectedImageForTest);Assert.False(r.Image.IsDisposed);Assert.True(w.OriginalSourceCommandEnabled);Assert.Contains("截图",w.ResultLabel);Assert.Contains("图像结构判断不确定",w.ResultLabel);Assert.Contains("分类：输入 23 / 输出 9",w.ResultTip);Assert.Contains("生成：输入 23 / 输出 9",w.ResultTip);Assert.Contains("图像结构判断不确定",w.VersionDescriptionForTest(w.MarkdownResults.SelectedVersionId!.Value));Assert.Contains("分类：输入 23",w.VersionDescriptionForTest(w.MarkdownResults.SelectedVersionId!.Value));
        w.ShowSourceForTest();var source=Assert.IsType<Expander>(Assert.IsType<BlockUIContainer>(w.MarkdownBody.Document.Blocks[^1]).Child);Assert.IsType<System.Windows.Controls.Image>(source.Content);Assert.False(w.SendScreenshotForTest(ImageOutputMode.ExplainImage,_=>true));Assert.Equal(2,r.Script.Requests);
    }
    [Theory][InlineData("decline")][InlineData("off")][InlineData("clear")]
    public async Task ActualShellDecisionCanBeRejectedOrInvalidatedWithoutSending(string action)
    {await using var r=await Rig.Create();var w=r.Window;Assert.False(w.SendScreenshotForTest(ImageOutputMode.TranslateText,offer=>{if(action=="off")w.MarkdownGate.Disable();if(action=="clear")w.ClearImagePresentationForTest();return action!="decline";}));Assert.Empty(r.Script.Bodies);Assert.False(w.MarkdownGate.HasRunningGeneration);if(action!="clear")Assert.False(r.Image.IsDisposed);}
    [Fact]public async Task ActualShellStopKeepsPartialImageResultAndOriginalAndRejectsLateBody()
    {
        await using var r=await Rig.Create();var w=r.Window;Assert.True(w.SendScreenshotForTest(ImageOutputMode.ExplainImage,_=>true));await r.Requests(1);await r.Reply(ImageClassificationTests.Json());await r.Requests(2);var id=w.MarkdownResults.Identity!.Value;
        Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"public partial")));w.StopForTest();await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.False(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"late")));
        r.Drain();Assert.Equal("public partial",w.MarkdownResults.Text);Assert.True(Assert.Single(w.MarkdownResults.Versions).Stopped);Assert.False(r.Image.IsDisposed);Assert.Same(r.Image,w.SelectedImageForTest);Assert.False(w.ProgressVisible);Assert.False(w.Waiting);
    }
    [Fact]public async Task ActualShellClearJoinsItsImageTaskAndReleasesCropAndMetadata()
    {
        await using var r=await Rig.Create();var w=r.Window;Assert.True(w.SendScreenshotForTest(ImageOutputMode.Automatic,_=>true));await r.Requests(1);var task=r.Flow.Running;Assert.True(await w.ClearContentForTest());Assert.True(task.IsCompleted);Assert.Null(r.Flow.SelectedSnapshot);Assert.Null(w.SelectedImageForTest);Assert.True(r.Image.IsDisposed);Assert.All(r.Raw,x=>Assert.Equal(0,x));Assert.Empty(w.MarkdownResults.Versions);Assert.Empty(w.MarkdownBody.Document.Blocks);
    }
    private sealed class PublicOcr:P.ILocalOcrWorker
    {
        public Task<P.ImageReply> RunAsync(ReadOnlyMemory<byte> image,P.ImageJob job,CancellationToken cancellation=default)
            =>Task.FromResult(new P.ImageReply(job,P.ImageFailure.None,new(8,8,job.Language,"PUBLIC APPROVED OCR SOURCE",[])));
        public ValueTask<bool> TryFinalizeAsync()=>ValueTask.FromResult(true);
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ApprovedOcrTextFailureKeepsTheShownImageFallbackAndVersionBindings(bool partial)
    {
        await using var r=await Rig.Create();var w=r.Window;var sourceVersion=w.SelectedSourceVersionForTest!.Value;
        Assert.True(w.SendScreenshotForTest(ImageOutputMode.Automatic,_=>true));await r.Requests(1);await r.Reply(ImageClassificationTests.Json());await r.Requests(2);await r.Reply("image A readable output");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));var a=w.MarkdownResults.Identity!.Value;
        // Public pixels cross the test project's linked-raster/aliased-Imaging type port. Both owners keep distinct SourceIds;
        // the real OCR coordinator mints/approves B's source. This is not Windows OCR or a forged approval object.
        using var projected=new P.OwnedSourceImage(new P.ScreenshotPixels(new(0,0,8,8),r.Raw.ToArray()));
        var config=r.Config with{Data=r.Config.Data with{Profiles=r.Config.Data.Profiles.SetItem(0,r.Config.Data.Profiles[0] with{Streaming=partial})},Version="public-ocr-text-settings"};
        w.MarkdownGate.ObserveSettingsMetadata(config);
        await using var local=new P.OcrOnlyCoordinator(w.MarkdownGate,()=>config,(version,id)=>version==sourceVersion&&id==projected.SourceId&&!r.Image.IsDisposed,new Immediate(),()=>new PublicOcr());
        Assert.True(local.Start(sourceVersion,projected,"en-US"));await local.Running.WaitAsync(TimeSpan.FromSeconds(6));var approved=Assert.IsType<SelectionSourceSnapshot>(local.Approve(local.Preview!));
        await using var text=new TextTranslationCoordinator(w.MarkdownGate,()=>config,(_,_)=>false,r.Provider,new Immediate(),w.MarkdownResults,w.CoordinatorUpdate,ocrSourceCurrent:local.IsApprovedSourceCurrent);w.UseTranslation(text,config);
        r.Script.Mime=partial?"text/event-stream":"application/json";r.Script.ResponseForRequest=n=>new(n>=3&&!partial?401:200);
        var offered=Assert.IsType<TextTranslationOffer>(text.PrepareOcr(approved,TranslationPreset.Faithful));Assert.Equal(TextSubmission.Started,text.Submit(offered,true));var b=w.MarkdownResults.Identity!.Value;await r.Requests(3);
        if(partial)await r.Script.Write("data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"B actual partial\"}}]}\n\ndata: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"length\"}]}\n\ndata: [DONE]\n\n",true);
        await text.Running.WaitAsync(TimeSpan.FromSeconds(6));r.Drain();var failed=w.MarkdownResults.Versions.Single(v=>v.Identity==b);Assert.Equal(partial?"B actual partial":"",failed.Text);Assert.False(failed.Complete);Assert.Equal(partial?TranslationFailureReason.OutputTruncated:TranslationFailureReason.Authentication,failed.Failure);Assert.Equal(a.VersionId,w.MarkdownResults.SelectedVersionId);Assert.False(r.Image.IsDisposed);
        w.SelectScreenshotVersionForTest(a.VersionId);r.Drain();Assert.Same(r.Image,w.SelectedImageForTest);Assert.Equal("image A readable output",w.MarkdownResults.Text);Assert.Contains("截图",w.ResultLabel);w.ShowSourceForTest();Assert.IsType<System.Windows.Controls.Image>(Assert.IsType<Expander>(Assert.IsType<BlockUIContainer>(w.MarkdownBody.Document.Blocks[^1]).Child).Content);
        w.SelectScreenshotVersionForTest(b.VersionId);r.Drain();Assert.Equal(partial?"B actual partial":"",w.MarkdownResults.Text);Assert.Null(w.SelectedImageForTest);Assert.Contains("OCR-only",w.ResultLabel);w.ShowSourceForTest();Assert.Equal("PUBLIC APPROVED OCR SOURCE",Assert.IsType<TextBox>(Assert.IsType<Expander>(Assert.IsType<BlockUIContainer>(w.MarkdownBody.Document.Blocks[^1]).Child).Content).Text);Assert.False(r.Image.IsDisposed);Assert.Equal(3,r.Script.Requests);
    }

}
