using System.Text.Json;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Imaging;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Core.Settings.ApiProtocol;
namespace KuaiYi.App
{
    internal sealed partial class ShellWindow
    {
        internal Task BrowserForTest(ReadOnlyMemory<byte> bytes,IBrowserImageOperation op,CancellationToken token=default,Func<bool>? current=null,long? expected=null,Activation? initial=null)
            =>StartReceivedBrowserImage(bytes,()=>op,token,current,expected,initial);
    }
}
namespace KuaiYi.Provider.Tests
{
public sealed class BrowserImagePreparationTests
{
    // Received byte/worker ports are synthetic here. Actual decoder/owner source binding has separate Linux tests.
    private sealed class Operation:IBrowserImageOperation
    {
        public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<OwnedFileImage> Reply=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token;public ReadOnlyMemory<byte> Borrowed;public int Calls;public bool Finalized=true;
        public async Task<OwnedFileImage> LoadAsync(ReadOnlyMemory<byte> bytes,Guid session,long version,long epoch,CancellationToken cancellation)
        {Calls++;Token=cancellation;Borrowed=bytes;Entered.TrySetResult();return await Reply.Task;}
        public ValueTask<bool> TryFinalizeAsync()=>ValueTask.FromResult(Finalized);
    }
    private static OwnedFileImage Owner()
    {
        byte[] raw=Enumerable.Repeat((byte)255,256).ToArray();using var pixels=new ScreenshotPixels(new(0,0,8,8),raw.ToArray());
        return new(new(EncodedRasterFormat.Png,1,new string('a',64),ImageTransform.PlanEncoded(new(8,8),ImageOrientation.ExifTopLeft),2560),raw,ScreenshotPngEncoder.Encode(pixels),ImageSourceKind.Browser);
    }
    private static ShellWindow Window(){var w=new ShellWindow();w.InitializeResultFlow();w.WithGeometry();return w;}
    private static byte[] Input(){using var owner=Owner();using var png=owner.PrepareRequest(4194304);return png.Bytes.ToArray();}
    [Fact]public async Task OriginalBrowserPreparationAdoptsDistinctLocalPreviewInOffWithoutASend()
    {
        var w=Window();var op=new Operation();byte[] input=Input();var task=w.BrowserForTest(input,op);await op.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));var owner=Owner();op.Reply.SetResult(owner);await task;
        Assert.Same(owner,w.SelectedImageForTest);Assert.Equal(ImageSourceKind.Browser,owner.Kind);Assert.Equal(Activation.Off,w.MarkdownGate.Snapshot.Activation);Assert.False(w.MarkdownGate.HasRunningGeneration);Assert.Contains("浏览器图片",w.ResultLabel);Assert.Contains("本地接收",w.ResultLabel);Assert.DoesNotContain("文件图片",w.ResultLabel);Assert.False(w.ScreenshotSendEnabledForTest);Assert.True(await w.ClearContentForTest());Assert.True(owner.IsDisposed);
    }
    [Fact]public async Task RevokedExternalTokenCannotBeMissedWhileTheOriginalTaskInstallsItsOwnCancellationSource()
    {
        var w=Window();var op=new Operation();using var permit=new CancellationTokenSource();int checks=0;
        await w.BrowserForTest(Input(),op,permit.Token,()=>{if(++checks==1)Task.Run(permit.Cancel).GetAwaiter().GetResult();return true;});
        Assert.Equal(0,op.Calls);Assert.Null(w.SelectedImageForTest);Assert.False(w.FileBusyForTest);Assert.Equal(Suspension.None,w.MarkdownGate.Snapshot.Suspension);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ExpectedEpochAndActivationAreComparedWhenTheCaptureOwnerIsActuallyAcquired(bool activation)
    {
        var w=Window();var op=new Operation();var snapshot=w.MarkdownGate.Snapshot;int checks=0;
        await w.BrowserForTest(Input(),op,current:()=>{if(++checks==2){if(activation){var origin=new EndpointOrigin(new Uri("https://synthetic.invalid"));w.MarkdownGate.ConsentTo(origin);Assert.True(w.MarkdownGate.TryEnable(origin,true));}else w.MarkdownGate.StopGeneration();}return true;},expected:snapshot.Epoch,initial:snapshot.Activation);
        Assert.Equal(0,op.Calls);Assert.Null(w.SelectedImageForTest);Assert.False(w.FileBusyForTest);Assert.Equal(Suspension.None,w.MarkdownGate.Snapshot.Suspension);
    }
    [Fact]public async Task RawRevocationRejectsReturnedRasterEvenWhileLinkedCancellationCallbacksHaveNotRun()
    {
        var w=Window();var op=new Operation();using var permit=new CancellationTokenSource();var task=w.BrowserForTest(Input(),op,permit.Token);await op.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);using var registration=permit.Token.Register(()=>{entered.TrySetResult();release.Task.GetAwaiter().GetResult();});Task? cancellation=null;
        try
        {
            cancellation=permit.CancelAsync();await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.False(op.Token.IsCancellationRequested);Assert.True(permit.IsCancellationRequested);var owner=Owner();op.Reply.SetResult(owner);await task.WaitAsync(TimeSpan.FromSeconds(5));Assert.Null(w.SelectedImageForTest);Assert.True(owner.IsDisposed);
        }
        finally{release.TrySetResult();if(cancellation!=null)await cancellation;}
        await w.ClearContentForTest();
    }
    [Fact]public async Task BrowserPrepareUsesTheOriginalUnconfirmedOwnerFenceAndExplicitRetry()
    {
        var w=Window();var op=new Operation{Finalized=false};var task=w.BrowserForTest(Input(),op);await op.Entered.Task;var owner=Owner();op.Reply.SetResult(owner);await task;Assert.True(owner.IsDisposed);Assert.Null(w.SelectedImageForTest);Assert.True(w.FileBusyForTest);var other=new Operation();await w.BrowserForTest(Input(),other);Assert.Equal(0,other.Calls);await Assert.ThrowsAnyAsync<Exception>(()=>w.SettleFileForTest());op.Finalized=true;await w.SettleFileForTest();Assert.False(w.FileBusyForTest);
    }
    private sealed class Immediate:SynchronizationContext{public override void Post(SendOrPostCallback action,object? state)=>action(state);}
    private sealed class Rig:IAsyncDisposable
    {
        public readonly PhaseHttpFixture Server=new();public PhaseHttpFixture.Script Script=null!;public readonly ShellWindow Window=BrowserImagePreparationTests.Window();public readonly ProviderClient Provider=new();public ProductionScreenshotCoordinator Flow=null!;public Protocol Protocol;
        public readonly TaskCompletionSource SecondRequest=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public static async Task<Rig> Create(Protocol protocol)
        {
            var r=new Rig{Protocol=protocol};await r.Server.InitializeAsync();r.Script=r.Server.Add("browser","application/json");r.Script.ResponseForRequest=n=>{if(n==2)r.SecondRequest.TrySetResult();return new(200);};
            var config=new SettingsSnapshot(new(){ActiveProfileId="vision",VisionProfileId="vision",Profiles=[new(){Id="vision",Protocol=protocol,BaseUrl=new Uri(r.Server.BaseUri,"browser/").AbsoluteUri,AllowLoopbackHttp=true,Authentication=ApiAuthentication.None,Model="PUBLIC_BROWSER_IMAGE",Streaming=false,Capabilities=new(){Vision=Capability.Supported}}]},"synthetic-browser",SettingsLoadState.Current);
            r.Flow=r.Window.ConfigureScreenshotForTest(config,r.Provider,new Immediate());var op=new Operation();var task=r.Window.BrowserForTest(Input(),op);await op.Entered.Task;op.Reply.SetResult(Owner());await task;Assert.Equal(Activation.Off,r.Window.MarkdownGate.Snapshot.Activation);Assert.Equal(0,r.Script.Requests);
            var origin=new EndpointOrigin(new Uri("https://synthetic-text.invalid"));r.Window.MarkdownGate.ConsentTo(origin);Assert.True(r.Window.MarkdownGate.TryEnable(origin,true));return r;
        }
        public Task Reply(string text)=>Script.Write(Protocol==Protocol.OpenAIChat?JsonSerializer.Serialize(new{choices=new[]{new{index=0,message=new{content=text},finish_reason="stop"}}}):JsonSerializer.Serialize(new{type="message",content=new[]{new{type="text",text}},stop_reason="end_turn"}),true);
        public async ValueTask DisposeAsync(){try{await Flow.DisposeAsync();await Window.ClearContentForTest();}finally{Provider.Dispose();await Server.DisposeAsync();}}
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task BrowserSourceFlowsThroughTheSameExplicitVisionConfirmationAndTwoRealHttpStages(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol);Assert.False(r.Window.SendScreenshotForTest(ImageOutputMode.Automatic,_=>false));Assert.Equal(0,r.Script.Requests);
        Assert.True(r.Window.SendScreenshotForTest(ImageOutputMode.Automatic,offer=>{Assert.Contains("浏览器图片",offer.ConfirmationText);Assert.DoesNotContain("本地图片",offer.ConfirmationText);Assert.DoesNotContain("裁剪的截图",offer.ConfirmationText);Assert.Equal(ImageSourceKind.Browser,offer.Snapshot.SourceKind);return true;}));
        await r.Script.Received.Task.WaitAsync(TimeSpan.FromSeconds(5));await r.Reply(ImageClassificationTests.Json());await r.SecondRequest.Task.WaitAsync(TimeSpan.FromSeconds(5));await r.Reply("PUBLIC BROWSER RESULT");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2,r.Script.Requests);var snapshot=r.Flow.SelectedSnapshot!;Assert.Equal(ImageSourceKind.Browser,snapshot.SourceKind);Assert.False(snapshot.Transform!.HasDesktopAnchor);Assert.Null(snapshot.NormalizationProof);Assert.Contains("浏览器图片",r.Window.ResultLabel);Assert.Contains("浏览器图片",r.Window.VersionDescriptionForTest(r.Window.MarkdownResults.SelectedVersionId!.Value));Assert.All(r.Script.Bodies,b=>{Assert.DoesNotContain("chrome-extension",b);Assert.DoesNotContain("Cookie",b);Assert.DoesNotContain(new string('a',64),b);});Assert.False(r.Window.SendScreenshotForTest(ImageOutputMode.Automatic,_=>true));Assert.Equal(2,r.Script.Requests);
    }
    [Fact]public async Task BrowserFailureUsesItsActualSourceLabelAndRecoveryInsteadOfClaimingAScreenshot()
    {
        await using var r=await Rig.Create(Protocol.OpenAIChat);Assert.True(r.Window.SendScreenshotForTest(ImageOutputMode.Automatic,_=>true));await r.Script.Received.Task.WaitAsync(TimeSpan.FromSeconds(5));await r.Reply("invalid classification");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(1,r.Script.Requests);Assert.Contains("浏览器图片",r.Window.FailureNoticeFull);Assert.Contains("从浏览器",r.Window.FailureNoticeFull);Assert.DoesNotContain("截图图译",r.Window.FailureNoticeFull);
    }
}
}
