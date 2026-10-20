using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Documents;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Imaging;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Core.Settings.ApiProtocol;
namespace KuaiYi.Provider.Tests;
public sealed class FileImageSourceTests
{
    // The linked managed source type port supplies an already-prepared synthetic file owner.
    // Real codec->owner binding is separately executed by FileImageOwnerTests through the actual Linux worker.
    internal static OwnedFileImage Owner(out byte[] pixels)
    {
        pixels=Enumerable.Range(0,256).Select(i=>(byte)(i%4==3?255:i)).ToArray();using var source=new ScreenshotPixels(new(0,0,8,8),pixels.ToArray());var png=ScreenshotPngEncoder.Encode(source);
        return new(new(EncodedRasterFormat.Gif,50,new string('a',64),ImageTransform.PlanEncoded(new(8,8),ImageOrientation.ExifTopLeft),2560,new(2,0,0,8,8)),pixels,png);
    }
    private sealed class Operation(string expectedPath="PUBLIC_SYNTHETIC_FILE"): IFileImageOperation
    {
        internal readonly TaskCompletionSource<OwnedFileImage> Reply=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Finalized=true,ThrowOnFinalize;internal int Loads,Finalizations;internal CancellationToken Token;
        public async Task<OwnedFileImage> LoadAsync(string path,Guid session,long version,long epoch,CancellationToken cancellation)
        {Loads++;Token=cancellation;Assert.Equal(expectedPath,path);Assert.NotEqual(Guid.Empty,session);Assert.True(version>0&&epoch>0);Entered.TrySetResult();return await Reply.Task;}
        public ValueTask<bool> TryFinalizeAsync(){Finalizations++;if(ThrowOnFinalize)throw new IOException("Synthetic settlement failure.");return ValueTask.FromResult(Finalized);}
    }
    private static ShellWindow Window(){var w=new ShellWindow();w.InitializeResultFlow();w.WithGeometry();return w;}
    [Fact]public async Task ActualManagedOpenCommandInOffOnlyAdoptsFileOwnerAndShowsFirstFrameWithoutSending()
    {
        var w=Window();var op=new Operation();using var owner=Owner(out var pixels);var task=w.OpenFileForTest(op);await op.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.True(w.FileBusyForTest);Assert.Null(w.SelectedImageForTest);op.Reply.SetResult(owner);await task;
        Assert.Same(owner,w.SelectedImageForTest);Assert.Equal(Activation.Off,w.MarkdownGate.Snapshot.Activation);Assert.False(w.MarkdownGate.HasRunningGeneration);Assert.False(w.ScreenshotSendEnabledForTest);Assert.Contains("文件图片",w.ResultLabel);Assert.Contains("使用首帧",w.ResultLabel);Assert.False(w.FileBusyForTest);
        var preview=Assert.IsType<Expander>(Assert.IsType<BlockUIContainer>(w.MarkdownBody.Document.Blocks[^1]).Child);Assert.True(preview.IsExpanded);Assert.Contains("使用首帧",preview.Header!.ToString());Assert.IsType<System.Windows.Controls.Image>(preview.Content);Assert.True(await w.ClearContentForTest());Assert.True(owner.IsDisposed);Assert.True(pixels.AsSpan().IndexOfAnyExcept((byte)0)<0);
    }
    [Theory][InlineData("clear")][InlineData("lock")][InlineData("hide")][InlineData("stop")][InlineData("settings")][InlineData("off")][InlineData("replace")]
    public async Task LateOriginalFileTaskCannotAdoptAfterCancellationAndRemainsHeldUntilSettlement(string action)
    {
        var w=Window();var op=new Operation();var owner=Owner(out var pixels);var task=w.OpenFileForTest(op);await op.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));Task? cleanup=null;
        if(action=="clear")cleanup=w.ClearContentForTest();if(action=="lock")w.ApplyLockForTest();if(action=="hide"){w.MarkdownGate.Disable(hide:true);w.ObserveFileForTest();}if(action=="stop")w.StopForTest();if(action=="settings"){w.MarkdownGate.SetSuspension(Suspension.Settings,true);w.ObserveFileForTest();}if(action=="off"){w.MarkdownGate.Disable();w.ObserveFileForTest();}if(action=="replace")w.BeginTextSourceForTest();
        Assert.True(op.Token.IsCancellationRequested);Assert.False(task.IsCompleted);Assert.True(w.FileBusyForTest);Assert.Null(w.SelectedImageForTest);op.Reply.SetResult(owner);await task;if(cleanup!=null)await cleanup;else await w.SettleFileForTest();Assert.True(owner.IsDisposed);Assert.True(pixels.AsSpan().IndexOfAnyExcept((byte)0)<0);Assert.Null(w.SelectedImageForTest);Assert.False(w.MarkdownGate.HasRunningGeneration);
    }
    [Fact]public async Task UnconfirmedActualOperationFencesNewOpenAndExplicitRetrySettlesTheSameOwner()
    {
        var w=Window();var op=new Operation{Finalized=false};var owner=Owner(out var pixels);var task=w.OpenFileForTest(op);await op.Entered.Task;op.Reply.SetResult(owner);await task;Assert.Null(w.SelectedImageForTest);Assert.True(owner.IsDisposed);Assert.True(w.FileBusyForTest);Assert.True(pixels.AsSpan().IndexOfAnyExcept((byte)0)<0);var other=new Operation();await w.OpenFileForTest(other);Assert.Equal(0,other.Loads);await Assert.ThrowsAnyAsync<Exception>(()=>w.SettleFileForTest());op.Finalized=true;await w.SettleFileForTest();Assert.False(w.FileBusyForTest);Assert.True(op.Finalizations>=3);
    }
    [Fact]public async Task FinalizeExceptionKeepsTheOriginalOwnerAndExplicitSuccessfulRetryCanReleaseIt()
    {
        var w=Window();var op=new Operation{ThrowOnFinalize=true};var owner=Owner(out _);var task=w.OpenFileForTest(op);await op.Entered.Task;op.Reply.SetResult(owner);await task;Assert.True(owner.IsDisposed);Assert.True(w.FileBusyForTest);Assert.Null(w.SelectedImageForTest);await Assert.ThrowsAnyAsync<Exception>(()=>w.SettleFileForTest());op.ThrowOnFinalize=false;await w.SettleFileForTest();Assert.False(w.FileBusyForTest);
    }
    [Theory][InlineData("decline")][InlineData("no-enable")][InlineData("changed-source")][InlineData("accept")]
    public async Task ExplicitOffFileEnableDecisionRechecksTheExactOwnerAndVersionBeforeInvokingTheOriginalSend(string action)
    {
        var w=Window();var op=new Operation();using var owner=Owner(out _);var task=w.OpenFileForTest(op);await op.Entered.Task;op.Reply.SetResult(owner);await task;int enables=0,sends=0;
        bool accepted=w.EnableFileForTest(()=>action!="decline",()=>{enables++;if(action!="no-enable"){var origin=new EndpointOrigin(new Uri("https://public-enable.invalid"));w.MarkdownGate.ConsentTo(origin);Assert.True(w.MarkdownGate.TryEnable(origin,true));}if(action=="changed-source")w.ClearImagePresentationForTest();},()=>sends++);
        Assert.Equal(action=="accept",accepted);Assert.Equal(action=="decline"?0:1,enables);Assert.Equal(action=="accept"?1:0,sends);await w.ClearContentForTest();
    }
    private sealed class Immediate:SynchronizationContext{public override void Post(SendOrPostCallback action,object? state)=>action(state);}
    private sealed class Rig:IAsyncDisposable
    {
        internal readonly PhaseHttpFixture Server=new();internal PhaseHttpFixture.Script Script=null!;internal readonly ShellWindow Window=FileImageSourceTests.Window();internal readonly ProviderClient Provider=new();internal ProductionScreenshotCoordinator Flow=null!;internal OwnedFileImage Image=null!;internal Protocol Protocol;internal int Prepares;
        internal static async Task<Rig> Create(Protocol protocol)
        {
            var r=new Rig{Protocol=protocol};await r.Server.InitializeAsync();r.Script=r.Server.Add("file","application/json");
            var config=new SettingsSnapshot(new(){ActiveProfileId="v",VisionProfileId="v",Profiles=[new(){Id="v",Protocol=protocol,BaseUrl=new Uri(r.Server.BaseUri,"file/").AbsoluteUri,AllowLoopbackHttp=true,Authentication=ApiAuthentication.None,Model="PUBLIC_FILE_IMAGE",Streaming=false,Capabilities=new(){Vision=Capability.Supported}}]},"synthetic-file-config",SettingsLoadState.Current);
            r.Flow=r.Window.ConfigureScreenshotForTest(config,r.Provider,new Immediate(),observe:u=>{if(u.Kind==ProductionScreenshotEvent.Prepared)r.Prepares++;});var origin=new EndpointOrigin(new Uri("https://public-text.invalid"));r.Window.MarkdownGate.ConsentTo(origin);Assert.True(r.Window.MarkdownGate.TryEnable(origin,true));var op=new Operation();var load=r.Window.OpenFileForTest(op);await op.Entered.Task;r.Image=Owner(out _);op.Reply.SetResult(r.Image);await load;return r;
        }
        internal async Task Requests(int n){using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(Script.Requests<n)await Task.Delay(5,limit.Token);}
        internal Task Reply(string text)=>Script.Write(Protocol==Protocol.OpenAIChat?JsonSerializer.Serialize(new{choices=new[]{new{index=0,message=new{content=text},finish_reason="stop"}}}):JsonSerializer.Serialize(new{type="message",content=new[]{new{type="text",text}},stop_reason="end_turn"}),true);
        public async ValueTask DisposeAsync(){try{await Flow.DisposeAsync();await Window.ClearContentForTest();}finally{Provider.Dispose();await Server.DisposeAsync();}}
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task FileUsesOriginalProductionOfferAndBothHttpStagesWithBoundUnanchoredSource(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol);Assert.Empty(r.Script.Bodies);Assert.True(r.Window.SendScreenshotForTest(ImageOutputMode.Automatic,o=>{Assert.Contains("本地图片",o.ConfirmationText);Assert.Contains("使用首帧",o.ConfirmationText);Assert.DoesNotContain("裁剪的截图",o.ConfirmationText);Assert.Contains("两次",o.ConfirmationText);Assert.Equal(ImageSourceKind.LocalFile,o.Snapshot.SourceKind);return true;}));
        await r.Requests(1);await r.Reply(ImageClassificationTests.Json());await r.Requests(2);await r.Reply("PUBLIC FILE RESULT");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));var snapshot=r.Flow.SelectedSnapshot!;Assert.Equal(ImageSourceKind.LocalFile,snapshot.SourceKind);Assert.Equal("使用首帧",snapshot.SourceNotice);Assert.NotNull(snapshot.Transform);Assert.False(snapshot.Transform.HasDesktopAnchor);Assert.Null(snapshot.NormalizationProof);Assert.Same(r.Image,r.Window.SelectedImageForTest);Assert.Contains("文件图片",r.Window.ResultLabel);Assert.Contains("使用首帧",r.Window.ResultLabel);Assert.Contains("使用首帧",r.Window.VersionDescriptionForTest(r.Window.MarkdownResults.SelectedVersionId!.Value));
        var images=new List<string>();foreach(var body in r.Script.Bodies){Assert.DoesNotContain("PUBLIC_SYNTHETIC_FILE",body);using var json=JsonDocument.Parse(body);var content=json.RootElement.GetProperty("messages").EnumerateArray().Last().GetProperty("content");var image=content.EnumerateArray().Single(b=>b.GetProperty("type").GetString()==(protocol==Protocol.OpenAIChat?"image_url":"image"));images.Add(protocol==Protocol.OpenAIChat?image.GetProperty("image_url").GetProperty("url").GetString()!:image.GetProperty("source").GetProperty("data").GetString()!);}Assert.Equal(images[0],images[1]);Assert.False(r.Window.SendScreenshotForTest(ImageOutputMode.Automatic,_=>true));Assert.Equal(2,r.Script.Requests);Assert.Equal(1,r.Prepares);
    }
    [Theory][InlineData("decline")][InlineData("off")][InlineData("clear")][InlineData("settings")]
    public async Task FileCannotBypassOriginalFinalConfirmationOrSourceSettingsGates(string action)
    {await using var r=await Rig.Create(Protocol.OpenAIChat);Assert.False(r.Window.SendScreenshotForTest(ImageOutputMode.Automatic,_=>{if(action=="off")r.Window.MarkdownGate.Disable();if(action=="clear")r.Window.ClearImagePresentationForTest();if(action=="settings")r.Window.MarkdownGate.SetSuspension(Suspension.Settings,true);return action!="decline";}));Assert.Empty(r.Script.Bodies);Assert.False(r.Window.MarkdownGate.HasRunningGeneration);}
    [Theory][InlineData("disabled")][InlineData("off")][InlineData("selection")][InlineData("suspended")]
    public async Task ActualManagedFileDropCommandRejectsWithoutOptinOrOnDropAdmission(string reason)
    {
        var w=Window();Assert.False(w.ImageFileDropEnabledForTest);w.MarkdownGate.SetMode(TriggerMode.Drop);var origin=new EndpointOrigin(new Uri("https://public.invalid"));w.MarkdownGate.ConsentTo(origin);if(reason!="off")Assert.True(w.MarkdownGate.TryEnable(origin,true));if(reason!="disabled")w.EnableImageFileDropForTest(true);if(reason=="selection")w.MarkdownGate.SetMode(TriggerMode.Selection);if(reason=="suspended")w.MarkdownGate.SetSuspension(Suspension.Settings,true);var op=new Operation("C:\\public\\a.gif");await w.DropImageFileForTest("C:\\public\\a.gif",op);Assert.Equal(0,op.Loads);Assert.Null(w.SelectedImageForTest);
    }
    [Theory][InlineData("optout")][InlineData("off")][InlineData("receiver-revoked")]
    public async Task TransferredDropInstallsTheExistingOwnedFileTaskAndOnlyExplicitInvalidationCancelsIt(string change)
    {
        var w=Window();w.MarkdownGate.SetMode(TriggerMode.Drop);var origin=new EndpointOrigin(new Uri("https://public.invalid"));w.MarkdownGate.ConsentTo(origin);Assert.True(w.MarkdownGate.TryEnable(origin,true));w.EnableImageFileDropForTest(true);var op=new Operation("C:\\public\\a.gif");var task=w.DropImageFileForTest("C:\\public\\a.gif",op);await op.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.False(task.IsCompleted);Assert.True(w.FileBusyForTest);
        if(change=="optout")w.EnableImageFileDropForTest(false);if(change=="off"){w.MarkdownGate.Disable();w.ObserveFileForTest();}if(change=="receiver-revoked")w.RevokeDropForFileTest();
        Assert.Equal(change!="receiver-revoked",op.Token.IsCancellationRequested);var image=Owner(out var actual);op.Reply.SetResult(image);await task;if(change=="receiver-revoked"){Assert.Same(image,w.SelectedImageForTest);Assert.Contains("使用首帧",w.ResultLabel);}else{Assert.True(image.IsDisposed);Assert.All(actual,b=>Assert.Equal(0,b));Assert.Null(w.SelectedImageForTest);}await w.ClearContentForTest();
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task DropPathAdmissionUsesTheExistingFileOwnerAndProductionHttpConfirmation(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol);r.Window.MarkdownGate.SetMode(TriggerMode.Drop);r.Window.EnableImageFileDropForTest(true);var config=new SettingsSnapshot(new(),"public-drop-config",SettingsLoadState.Current);var source=new SelectionWindow(123,456,789,"public.exe");var target=new DropTargetGeometry(new(0,0,48,48),new(0,0,48,48),24);var op=new Operation("C:\\public\\a.gif");Task? fileTask=null;
        await using var capture=new DropCaptureCoordinator(r.Window.MarkdownGate,()=>config,s=>s==source,()=>true,999,new Immediate(),(_,_)=>throw new InvalidOperationException("File must not be text."),_=>throw new InvalidOperationException("Valid synthetic file path."));var lease=capture.Open(Guid.NewGuid(),r.Window.MarkdownGate.Snapshot.Epoch,target)!;var txn=Guid.NewGuid();Assert.True(capture.Enter(lease,txn,source,new(24,24),OleEffect.Copy));Assert.True(capture.Begin(lease,txn,source,new(24,24),OleEffect.Copy));
        await capture.ReceiveImageFileAsync(lease,txn,System.Text.Encoding.UTF8.GetBytes("C:\\public\\a.gif"),a=>{a();return true;},path=>fileTask=r.Window.DropImageFileForTest(path,op));Assert.NotNull(fileTask);await op.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.False(fileTask.IsCompleted);Assert.Empty(r.Script.Bodies);var file=Owner(out _);op.Reply.SetResult(file);await fileTask;Assert.Same(file,r.Window.SelectedImageForTest);Assert.True(r.Image.IsDisposed);Assert.Contains("使用首帧",r.Window.ResultLabel);
        Assert.True(r.Window.SendScreenshotForTest(ImageOutputMode.Automatic,o=>{Assert.Contains("本地图片",o.ConfirmationText);Assert.Contains("使用首帧",o.ConfirmationText);return true;}));await r.Requests(1);await r.Reply(ImageClassificationTests.Json());await r.Requests(2);await r.Reply("PUBLIC DROPPED FILE RESULT");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(ImageSourceKind.LocalFile,r.Flow.SelectedSnapshot!.SourceKind);Assert.False(r.Flow.SelectedSnapshot.Transform!.HasDesktopAnchor);Assert.All(r.Script.Bodies,b=>{Assert.DoesNotContain("C:",b);Assert.DoesNotContain("a.gif",b);});Assert.False(r.Window.SendScreenshotForTest(ImageOutputMode.Automatic,_=>true));Assert.Equal(2,r.Script.Requests);
    }

    [Theory][InlineData("QueryEnd")][InlineData("DropEnd")]
    public async Task RejectedImageDropShowsItsReasonAfterCollapsingHoverAndPreservesTheOldFile(string phase)
    {
        var w=Window();var op=new Operation();var task=w.OpenFileForTest(op);await op.Entered.Task;var owner=Owner(out _);op.Reply.SetResult(owner);await task;w.DropFeedbackForTest("Hover",1,OleTextFormat.ImageFile);w.DropFeedbackForTest(phase,0,OleTextFormat.ImageFile);Assert.Contains("单个本地",w.ButtonTip!.ToString());Assert.Same(owner,w.SelectedImageForTest);Assert.False(owner.IsDisposed);Assert.Equal(Activation.Off,w.MarkdownGate.Snapshot.Activation);await w.ClearContentForTest();
    }
    [Fact]public async Task OptingOutOfImageDropCannotCancelAnIndependentOpenFileTask()
    {
        var w=Window();w.EnableImageFileDropForTest(true);var op=new Operation();var task=w.OpenFileForTest(op);await op.Entered.Task;w.EnableImageFileDropForTest(false);Assert.False(op.Token.IsCancellationRequested);var owner=Owner(out _);op.Reply.SetResult(owner);await task;Assert.Same(owner,w.SelectedImageForTest);await w.ClearContentForTest();
    }

}
