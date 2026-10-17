using System.Text.Json;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Imaging;
using KuaiYi.Providers;
using System.Windows.Controls;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests;
public sealed class ScreenshotRecaptureRecoveryTests
{
    private sealed class Immediate:SynchronizationContext{public override void Post(SendOrPostCallback action,object? state)=>action(state);}
    private sealed class HeldContext:SynchronizationContext
    {private readonly Queue<Action> queue=[];public override void Post(SendOrPostCallback callback,object? state)=>queue.Enqueue(()=>callback(state));public void Drain(){while(queue.TryDequeue(out var action))action();}}
    private static async Task WithHeldCapture(ShellWindow window,MenuItem menu,Func<Task,Action,Task> body)
    {
        var queue=new HeldContext();Task original=window.CaptureTaskForTest;
        try
        {
            var previous=SynchronizationContext.Current;
            try{SynchronizationContext.SetSynchronizationContext(queue);menu.Invoke();}
            finally{SynchronizationContext.SetSynchronizationContext(previous);original=window.CaptureTaskForTest;}
            await body(original,queue.Drain);
        }
        finally
        {
            try{queue.Drain();}
            finally{await original.WaitAsync(TimeSpan.FromSeconds(6));}
        }
    }
    private sealed class Rig:IAsyncDisposable
    {
        public readonly PhaseHttpFixture Server=new();public PhaseHttpFixture.Script Script=null!;public readonly ShellWindow Window=new();public readonly ProviderClient Provider=new();public ProductionScreenshotCoordinator Flow=null!;
        public Protocol Protocol;public OwnedSourceImage Original=null!;public byte[] OriginalRaw=[];
        public static async Task<Rig> Create(Protocol protocol=Protocol.OpenAIChat)
        {
            var r=new Rig{Protocol=protocol};await r.Server.InitializeAsync();r.Script=r.Server.Add("recovery");r.Window.InitializeResultFlow();r.Window.WithGeometry();
            var config=new SettingsSnapshot(new(){ActiveProfileId="v",VisionProfileId="v",Profiles=[new(){Id="v",Protocol=protocol==Protocol.OpenAIChat?KuaiYi.Core.Settings.ApiProtocol.OpenAIChat:KuaiYi.Core.Settings.ApiProtocol.AnthropicMessages,BaseUrl=new Uri(r.Server.BaseUri,"recovery/").AbsoluteUri,AllowLoopbackHttp=true,Model="PUBLIC_RECOVERY_VISION",Authentication=ApiAuthentication.None,Streaming=true,Capabilities=new(){Vision=Capability.Supported}}]},"public-recovery-config",SettingsLoadState.Current);
            r.Flow=r.Window.ConfigureScreenshotForTest(config,r.Provider,new Immediate());var origin=new EndpointOrigin(new("https://text.public.invalid"));r.Window.MarkdownGate.ConsentTo(origin);Assert.True(r.Window.MarkdownGate.TryEnable(origin,true));
            var source=NewCrop(31);r.Original=source.Image;r.OriginalRaw=source.Raw;r.Accept(source.Image);return r;
        }
        public static (OwnedSourceImage Image,byte[] Raw) NewCrop(byte color)
        {var raw=Enumerable.Range(0,256).Select(i=>(byte)(i%4==3?255:color+i%3)).ToArray();return(new(new ScreenshotPixels(new(0,0,8,8),raw)),raw);}
        public void Accept(OwnedSourceImage image)
        {var input=Window.ImageInputForTest();using(input.Intent)using(input.Cancellation){Assert.True(input.Commit(image));Window.EndRestoreForTest(input.Cancellation);}}
        public string ImagePayload(string body)
        {using var json=JsonDocument.Parse(body);var content=json.RootElement.GetProperty("messages")[Protocol==Protocol.OpenAIChat?1:0].GetProperty("content");return Protocol==Protocol.OpenAIChat?content[1].GetProperty("image_url").GetProperty("url").GetString()!:content[0].GetProperty("source").GetProperty("data").GetString()!;}
        public async Task Requests(int count){using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(Script.Requests<count)await Task.Delay(5,limit.Token);}
        public async Task Body(string value,bool finish)
        {
            string text=JsonSerializer.Serialize(value);
            if(Protocol==Protocol.AnthropicMessages)await Script.Write("data: {\"type\":\"message_start\",\"message\":{}}\n\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n");
            await Script.Write(Protocol==Protocol.OpenAIChat?"data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":"+text+"}}]}\n\n":"data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":"+text+"}}\n\n");
            if(finish)await Script.Write(Protocol==Protocol.OpenAIChat?"data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n":"data: {\"type\":\"content_block_stop\",\"index\":0}\n\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"}}\n\ndata: {\"type\":\"message_stop\"}\n\n",true);
        }
        public async Task<RequestIdentity> Stopped()
        {
            Assert.True(Window.SendScreenshotForTest(ImageOutputMode.ExplainImage,_=>true));await Requests(1);await Body(ImageClassificationTests.Json(),true);await Requests(2);await Body("A actual partial body",false);
            using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(Window.MarkdownResults.Text!="A actual partial body")await Task.Delay(5,limit.Token);
            var id=Window.MarkdownResults.Identity!.Value;Window.StopForTest();await Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Drain();return id;
        }
        public MenuItem Menu()=>Assert.IsType<MenuItem>(Assert.Single(Window.ScreenshotRecoveryMenuForTest().Items));
        public void Drain(){for(int i=0;i<8&&Window.FlushPending;i++)Window.TickForTest();Assert.False(Window.FlushPending);}
        public async ValueTask DisposeAsync(){try{await Window.ClearContentForTest();await Flow.DisposeAsync();}finally{Provider.Dispose();await Server.DisposeAsync();}}
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task StoppedImageMenuAcquiresDifferentCropLocallyThenRequiresANewTwoStageDecision(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol);var w=r.Window;var old=await r.Stopped();var next=Rig.NewCrop(173);Assert.NotEqual(r.Original.SourceId,next.Image.SourceId);Assert.NotEqual(r.OriginalRaw,next.Raw);
        w.CapturePort=(intent,ct)=>{Assert.True(intent.IsCurrent);Assert.Equal(Suspension.Capture,w.MarkdownGate.Snapshot.Suspension);return Task.FromResult<OwnedSourceImage?>(next.Image);};
        var item=r.Menu();Assert.True(item.IsEnabled);Assert.Equal("重新截图",item.Header);item.Invoke();await w.CaptureTaskForTest.WaitAsync(TimeSpan.FromSeconds(6));r.Drain();
        Assert.Equal(1,w.CaptureCalls);Assert.Same(next.Image,w.SelectedImageForTest);Assert.True(w.LocalPreviewForTest);Assert.Empty(w.MarkdownResults.Versions);Assert.Equal(2,r.Script.Requests);Assert.True(r.Original.IsDisposed);Assert.All(r.OriginalRaw,b=>Assert.Equal(0,b));Assert.False(next.Image.IsDisposed);
        Assert.False(await w.ReceivePresentationAsync(old,new(OverlayResultEventKind.Delta,"late A")));Assert.False(w.SendScreenshotForTest(ImageOutputMode.TranslateText,_=>false));Assert.Equal(2,r.Script.Requests);
        Assert.True(w.SendScreenshotForTest(ImageOutputMode.TranslateText,offer=>{Assert.Equal(ImageOutputMode.TranslateText,offer.Snapshot.RequestedMode);Assert.Contains("两次",offer.ConfirmationText);return true;}));await r.Requests(3);await r.Body(ImageClassificationTests.Json(),true);await r.Requests(4);await r.Body("B actual new crop output",true);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));r.Drain();
        Assert.Equal("B actual new crop output",w.MarkdownResults.Text);Assert.Equal(next.Image.SourceId,r.Flow.SelectedSnapshot!.SourceId);Assert.Same(next.Image,w.SelectedImageForTest);Assert.False(w.SendScreenshotForTest(ImageOutputMode.ExplainImage,_=>true));Assert.Equal(4,r.Script.Requests);Assert.NotEqual(r.ImagePayload(r.Script.Bodies.ElementAt(0)),r.ImagePayload(r.Script.Bodies.ElementAt(2)));Assert.Empty(w.ScreenshotRecoveryMenuForTest().Items);
        await w.ClearContentForTest();Assert.True(next.Image.IsDisposed);Assert.All(next.Raw,b=>Assert.Equal(0,b));
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task CancellingTheNewSelectionKeepsTheStoppedImageSessionAndDoesNotSend(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol);var w=r.Window;var id=await r.Stopped();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        w.CapturePort=async(intent,ct)=>{entered.TrySetResult();await Task.Delay(Timeout.Infinite,ct);return null;};r.Menu().Invoke();var task=w.CaptureTaskForTest;await entered.Task.WaitAsync(TimeSpan.FromSeconds(6));w.CancelCropForTest();await task.WaitAsync(TimeSpan.FromSeconds(6));r.Drain();
        Assert.Equal(id,w.MarkdownResults.Identity);Assert.Equal("A actual partial body",w.MarkdownResults.Text);Assert.Same(r.Original,w.SelectedImageForTest);Assert.False(r.Original.IsDisposed);Assert.Equal(2,r.Script.Requests);Assert.True(r.Menu().IsEnabled);Assert.False(w.SendScreenshotForTest(ImageOutputMode.ExplainImage,_=>true));
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task ActualHttpFailureExposesTheRecoveryMenuButMerelyOpeningItDoesNothing(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol);var w=r.Window;r.Script.ResponseForRequest=_=>new(401);Assert.True(w.SendScreenshotForTest(ImageOutputMode.Automatic,_=>true));await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));r.Drain();Assert.Equal(TranslationFailureReason.Authentication,Assert.Single(w.MarkdownResults.Versions).Failure);
        Assert.True(r.Menu().IsEnabled);Assert.Equal(0,w.CaptureCalls);Assert.Equal(1,r.Script.Requests);Assert.False(w.SendScreenshotForTest(ImageOutputMode.Automatic,_=>true));Assert.False(r.Original.IsDisposed);
    }
    [Theory][InlineData("off")][InlineData("lock")][InlineData("hide")][InlineData("clear")][InlineData("exit")][InlineData("replacement")]
    public async Task LateCropAfterRevocationCannotReplaceOrSendAndClearExitJoinTheOriginalCapture(string change)
    {
        await using var r=await Rig.Create();var w=r.Window;await r.Stopped();var entered=new TaskCompletionSource<ScreenshotIntent>(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var late=Rig.NewCrop(117);OwnedSourceImage? replacement=null;
        w.CapturePort=async(intent,ct)=>{entered.TrySetResult(intent);await release.Task;return late.Image;};r.Menu().Invoke();var running=w.CaptureTaskForTest;
        try
        {
            var intent=await entered.Task.WaitAsync(TimeSpan.FromSeconds(6));Task? cleanup=null;
            switch(change)
            {
                case "off":w.MarkdownGate.Disable();w.ObserveImageGateForTest();break;
                case "lock":w.ApplyLockForTest();break;
                case "hide":w.HideImageWindowForTest();break;
                case "clear":cleanup=w.ClearContentForTest();break;
                case "exit":cleanup=w.ExitContentForTest();break;
                case "replacement":intent.Dispose();replacement=Rig.NewCrop(206).Image;r.Accept(replacement);break;
            }
            if(cleanup!=null)Assert.False(cleanup.IsCompleted);
            release.TrySetResult();await running.WaitAsync(TimeSpan.FromSeconds(6));if(cleanup!=null)await cleanup.WaitAsync(TimeSpan.FromSeconds(6));r.Drain();
            Assert.True(late.Image.IsDisposed);Assert.All(late.Raw,b=>Assert.Equal(0,b));Assert.Equal(2,r.Script.Requests);Assert.Equal(1,w.CaptureCalls);
            if(change=="replacement")Assert.Same(replacement,w.SelectedImageForTest);else if(change is "off" or "hide"){Assert.Same(r.Original,w.SelectedImageForTest);Assert.Equal("A actual partial body",w.MarkdownResults.Text);}else Assert.Null(w.SelectedImageForTest);
        }
        finally{release.TrySetResult();await running.WaitAsync(TimeSpan.FromSeconds(6));}
    }
    [Theory][InlineData("lock")][InlineData("hide")][InlineData("clear")][InlineData("exit")][InlineData("settings")]
    public async Task AStaleRecoveryMenuCannotBeginCaptureAfterItsContextIsRevoked(string change)
    {
        await using var r=await Rig.Create();var w=r.Window;await r.Stopped();var menu=r.Menu();
        switch(change){case "lock":w.ApplyLockForTest();break;case "hide":w.HideImageWindowForTest();break;case "clear":await w.ClearContentForTest();break;case "exit":await w.ExitContentForTest();break;case "settings":w.MarkdownGate.SetSuspension(Suspension.Settings,true);break;}
        menu.Invoke();await w.CaptureTaskForTest.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(0,w.CaptureCalls);Assert.Equal(2,r.Script.Requests);
    }
    [Fact]public async Task OffAllowsOnlyNewLocalPreviewUntilExplicitlyEnabledAndConfirmed()
    {
        await using var r=await Rig.Create();var w=r.Window;await r.Stopped();w.MarkdownGate.Disable();var next=Rig.NewCrop(88);w.CapturePort=(_,_)=>Task.FromResult<OwnedSourceImage?>(next.Image);Assert.True(r.Menu().IsEnabled);r.Menu().Invoke();await w.CaptureTaskForTest.WaitAsync(TimeSpan.FromSeconds(6));Assert.Same(next.Image,w.SelectedImageForTest);Assert.Equal(Activation.Off,w.MarkdownGate.Snapshot.Activation);Assert.False(w.SendScreenshotForTest(ImageOutputMode.Automatic,_=>true));Assert.Equal(2,r.Script.Requests);
        var origin=new EndpointOrigin(new Uri(r.Server.BaseUri,"recovery/"));Assert.True(w.MarkdownGate.TryEnable(origin,true));Assert.False(w.SendScreenshotForTest(ImageOutputMode.Automatic,_=>false));Assert.Equal(2,r.Script.Requests);Assert.True(w.SendScreenshotForTest(ImageOutputMode.Automatic,_=>true));await r.Requests(3);await r.Body(ImageClassificationTests.Json(),true);await r.Requests(4);await r.Body("public enabled new crop",true);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal("public enabled new crop",w.MarkdownResults.Text);Assert.Equal(4,r.Script.Requests);
    }
    [Fact]public async Task AChangedDisplayContextBeforeTheYieldContinuationNeverStartsNativeCapture()
    {
        await using var r=await Rig.Create();var w=r.Window;var id=await r.Stopped();var menu=r.Menu();w.CapturePort=(_,_)=>Task.FromResult<OwnedSourceImage?>(null);
        await WithHeldCapture(w,menu,async(original,drain)=>
        {
            Assert.False(original.IsCompleted);Assert.Equal(0,w.CaptureCalls);long epoch=w.MarkdownGate.Snapshot.Epoch,revision=w.ResultRevision;
            w.SelectScreenshotVersionForTest(id.VersionId);Assert.True(w.ResultRevision>revision);Assert.Equal(epoch,w.MarkdownGate.Snapshot.Epoch);
            drain();await original.WaitAsync(TimeSpan.FromSeconds(6));r.Drain();Assert.Equal(0,w.CaptureCalls);Assert.Equal(id,w.MarkdownResults.Identity);Assert.Equal("A actual partial body",w.MarkdownResults.Text);Assert.Same(r.Original,w.SelectedImageForTest);Assert.False(r.Original.IsDisposed);Assert.Equal(2,r.Script.Requests);
        });
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task HeldCaptureFinallyDrainsAndSettlesWhenAPreconditionAssertionThrows(bool selectVersion)
    {
        await using var r=await Rig.Create();var w=r.Window;var id=await r.Stopped();Task? original=null;w.CapturePort=(_,_)=>Task.FromResult<OwnedSourceImage?>(null);
        var failure=await Record.ExceptionAsync(()=>WithHeldCapture(w,r.Menu(),(task,drain)=>
        {
            original=task;Assert.False(task.IsCompleted);Assert.Equal(0,w.CaptureCalls);
            if(selectVersion)w.SelectScreenshotVersionForTest(id.VersionId);
            Assert.True(task.IsCompleted,"PUBLIC_CONTROLLED_PRECONDITION_FAILURE");return Task.CompletedTask;
        }));
        Assert.NotNull(failure);Assert.Contains("PUBLIC_CONTROLLED_PRECONDITION_FAILURE",failure.Message);Assert.NotNull(original);Assert.Same(original,w.CaptureTaskForTest);Assert.True(original.IsCompletedSuccessfully);
        Assert.Equal(selectVersion?0:1,w.CaptureCalls);Assert.Equal(Suspension.None,w.MarkdownGate.Snapshot.Suspension);r.Drain();Assert.Equal(id,w.MarkdownResults.Identity);Assert.Equal("A actual partial body",w.MarkdownResults.Text);Assert.Same(r.Original,w.SelectedImageForTest);Assert.False(r.Original.IsDisposed);Assert.Equal(2,r.Script.Requests);
    }
    [Fact]public async Task AStaleMenuCannotCaptureAfterADifferentValidSourceWasAdmitted()
    {
        await using var r=await Rig.Create();var w=r.Window;await r.Stopped();var stale=r.Menu();var replacement=Rig.NewCrop(81);r.Accept(replacement.Image);stale.Invoke();Assert.Equal(0,w.CaptureCalls);Assert.Same(replacement.Image,w.SelectedImageForTest);Assert.True(w.LocalPreviewForTest);Assert.Equal(2,r.Script.Requests);
    }
    [Fact]public async Task TheSharedHotkeyCommandStillRejectsStaleInputAndUsesAFreshRealGateAdmission()
    {
        await using var r=await Rig.Create();var w=r.Window;await r.Stopped();Assert.True(w.MarkdownGate.TryAdmitHotkeyInput(unchecked((uint)Environment.TickCount64+1),out var stale));w.MarkdownGate.Disable();await w.HotkeyCaptureForTest(stale);Assert.Equal(0,w.CaptureCalls);
        Assert.True(w.MarkdownGate.TryAdmitHotkeyInput(unchecked((uint)Environment.TickCount64+1),out var fresh));w.CapturePort=(intent,ct)=>{Assert.True(intent.IsCurrent);return Task.FromResult<OwnedSourceImage?>(null);};await w.HotkeyCaptureForTest(fresh).WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(1,w.CaptureCalls);Assert.Same(r.Original,w.SelectedImageForTest);Assert.Equal(2,r.Script.Requests);
    }
    [Fact]public async Task AStillOwnedOrUnconfirmedCleanupCannotBeReplacedByTheRecoveryCommand()
    {
        await using var r=await Rig.Create();var w=r.Window;await r.Stopped();var stale=r.Menu();var owner=new CaptureHostClient{Finalize=()=>ValueTask.FromResult(false)};w.ScreenshotForTest(Task.CompletedTask,owner:owner);Assert.False(r.Menu().IsEnabled);stale.Invoke();Assert.Equal(0,w.CaptureCalls);
        await Assert.ThrowsAsync<ScreenshotCleanupException>(()=>w.FinishScreenshotForTest());Assert.True(w.HasScreenshotOwner);Assert.False(r.Menu().IsEnabled);owner.Finalize=()=>ValueTask.FromResult(true);await w.FinishScreenshotForTest();Assert.False(w.HasScreenshotOwner);Assert.True(r.Menu().IsEnabled);Assert.Equal(2,owner.Calls);Assert.Equal(2,r.Script.Requests);
    }
}
