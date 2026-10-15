using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Imaging;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class OverlayClearContentTests
{
    private static void Drain(ShellWindow w){for(int i=0;i<4&&w.FlushPending;i++)w.TickForTest();Assert.False(w.FlushPending);}
    private static Expander Source(ShellWindow w)=>Assert.IsType<Expander>(Assert.IsType<BlockUIContainer>(w.MarkdownBody.Document.Blocks[^1]).Child);
    private static void Empty(ShellWindow w)
    {
        Assert.Empty(w.MarkdownResults.Versions);Assert.Empty(w.MarkdownResults.Text);Assert.Empty(w.MarkdownModel.Blocks);
        Assert.Empty(w.MarkdownBody.Document.Blocks);Assert.Empty(w.MarkdownMeasurement.Document.Blocks);Assert.Empty(w.ResultLabel);Assert.Null(w.ResultTip);
        Assert.False(w.FlushPending);Assert.False(w.Dirty);Assert.False(w.Waiting);Assert.False(w.Awaiting);Assert.False(w.ProgressVisible);Assert.False(w.PendingReveal);Assert.False(w.NewContentVisible);Assert.False(w.DropIntentVisible);Assert.True(w.Following);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ActualClearImmediatelyCancelsAndClearsPendingOrRenderedResultAndSource(bool rendered)
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;w.WithGeometry();var id=await rig.Start();Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"```\naccepted\n```")));if(rendered)Drain(w);w.ShowSourceForTest();var source=Source(w);var text=Assert.IsType<TextBox>(source.Content);w.HoverForTest();w.ReaderScroll(145);
        Task<bool> clear=w.ClearContentForTest();Empty(w);Assert.True(await clear);await rig.Flow.Running;Assert.Null(rig.Flow.SelectedSnapshot);Assert.Null(rig.Flow.NextGeneration);Assert.Equal(0,rig.Flow.RetainedSnapshotCount);Assert.Empty(text.Text);Assert.Null(source.Content);Assert.False(w.OriginalSourceCommandEnabled);Assert.Equal(1,w.SelectionClears);Assert.Equal(1,w.DropRevocations);Assert.Equal(Activation.On,w.MarkdownGate.Snapshot.Activation);Assert.False(w.MarkdownGate.CanApply(id));Assert.Null(w.TranslationStatus);Assert.Equal("正在连接…",w.ConnectionText);Assert.Equal("当前内容已清除",w.ButtonTip);
        while(text.Dispatcher.Pending>0)text.Dispatcher.Deliver();w.DrainResultDispatchForTest();w.ReopenForTest();w.ShowSourceForTest();w.TickForTest();Empty(w);Assert.False(w.Frames!.IsExpanded);
    }
    [Fact]public async Task ActualCacheAndRegenerationOffersAreGoneAndOffSettingsConsentRemain()
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var ids=new List<RequestIdentity>();
        for(int i=0;i<4;i++){var id=await rig.Start(i>0);ids.Add(id);Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"result "+i)));w.StopForTest();await rig.Flow.Running;Drain(w);}
        Assert.Equal(3,rig.Flow.RetainedSnapshotCount);Assert.True(w.MarkdownResults.SelectVersion(ids[1].VersionId));w.RenderMarkdown();var offer=rig.Flow.PrepareRegeneration();Assert.NotNull(offer);w.MarkdownGate.Disable();w.MarkdownGate.SetSuspension(Suspension.Settings,true);int attempts=rig.ConnectionAttempts;var settings=rig.Settings;
        Assert.True(await w.ClearContentForTest());Empty(w);Assert.Equal(Activation.Off,w.MarkdownGate.Snapshot.Activation);Assert.True(w.MarkdownGate.Snapshot.Suspension.HasFlag(Suspension.Settings));Assert.Same(settings,rig.Settings);Assert.True(w.MarkdownGate.HasConsentTo(new(new Uri("https://public.invalid"))));Assert.Equal(KuaiYi.Providers.TextSubmission.Rejected,rig.Flow.Submit(offer));Assert.Null(rig.Flow.PrepareRegeneration());foreach(var id in ids)Assert.False(w.MarkdownResults.SelectVersion(id.VersionId));Assert.Equal(attempts,rig.ConnectionAttempts);
    }
    private sealed class Deferred:SynchronizationContext
    {private readonly Queue<Action> queue=[];public override void Post(SendOrPostCallback action,object? state)=>queue.Enqueue(()=>action(state));public void Deliver(){while(queue.TryDequeue(out var action))action();}}
    [Theory][InlineData(OverlayResultEventKind.Begin)][InlineData(OverlayResultEventKind.Delta)][InlineData(OverlayResultEventKind.Complete)][InlineData(OverlayResultEventKind.Stopped)]
    public async Task ActualDestinationRejectsQueuedOldEventsAfterClear(OverlayResultEventKind kind)
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var id=await rig.Start();var destination=new Deferred();w.ResultDestination(destination);var late=w.ReceivePresentationAsync(id,new(kind,"late synthetic"));Assert.False(late.IsCompleted);Assert.True(await w.ClearContentForTest());destination.Deliver();Assert.False(await late);Empty(w);
    }
    [Fact]public async Task ClearInvalidatesPreparedConfirmationCaptureTicketAndHotkeyInput()
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;Assert.True(w.MarkdownGate.TryBeginPassiveCapture(TriggerMode.Selection,out var ticket));string text=new('a',12001);var source=new SelectionSourceSnapshot(new(17,92823,132000,@"C:\Synthetic\fixture-editor.exe"),text,text,[new(1,2,20,22)],[7,8],rig.Settings.Version,text.Length,true);var offer=rig.Flow.Prepare(source);Assert.NotNull(offer);Assert.Equal(KuaiYi.Providers.TextSubmission.ConfirmationRequired,rig.Flow.Submit(offer));long epoch=w.MarkdownGate.Snapshot.Epoch;Assert.True(w.MarkdownGate.TryAdmitHotkeyInput(unchecked((uint)Environment.TickCount64+1),out var input));Assert.True(w.MarkdownGate.IsHotkeyInputCurrent(input));
        Assert.True(await w.ClearContentForTest());Assert.True(w.MarkdownGate.Snapshot.Epoch>epoch);Assert.False(w.MarkdownGate.IsCaptureCurrent(ticket));Assert.False(w.MarkdownGate.IsHotkeyInputCurrent(input));Assert.Equal(KuaiYi.Providers.TextSubmission.Rejected,rig.Flow.Submit(offer,true));Assert.Equal(0,rig.ConnectionAttempts);Empty(w);
    }
    [Fact]public async Task RepeatedEmptyClearStillFencesNewWorkAndDoesNotCreateFakeVersion()
    {
        var w=new ShellWindow();w.InitializeResultFlow();long epoch=w.MarkdownGate.Snapshot.Epoch;Assert.True(await w.ClearContentForTest());Assert.True(await w.ClearContentForTest());Assert.Equal(epoch+2,w.MarkdownGate.Snapshot.Epoch);Assert.Equal(2,w.SelectionClears);Assert.Equal(2,w.DropRevocations);Empty(w);Assert.Equal(Activation.Off,w.MarkdownGate.Snapshot.Activation);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ClearAndExitShareOwnedFinalizationAndOldCompletionDoesNotChangeExitUi(bool fail)
    {
        var w=new ShellWindow();w.InitializeResultFlow();var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var owner=new CaptureHostClient{Finalize=()=>new(release.Task)};w.ScreenshotForTest(Task.CompletedTask,owner:owner);var clear=w.ClearContentForTest();Assert.False(clear.IsCompleted);Assert.Equal(1,owner.Calls);w.Exit();var exit=w.FinishScreenshotForTest();Assert.Same(exit,w.FinishScreenshotForTest());object? tip=w.ButtonTip;release.SetResult(!fail);if(fail)await Assert.ThrowsAsync<ScreenshotCleanupException>(()=>exit);else await exit;Assert.Equal(!fail,await clear);Assert.Equal(1,owner.Calls);Assert.Equal(tip,w.ButtonTip);Assert.Equal(0,w.ClearErrors);Assert.Equal(fail,w.HasScreenshotOwner);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task SecondClearCancelsNewTextWhileSharedImageCleanupIsPending(bool fail)
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var owner=new CaptureHostClient{Finalize=()=>new(release.Task)};w.ScreenshotForTest(Task.CompletedTask,owner:owner);var first=w.ClearContentForTest();var id=await rig.Start();Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"new valid source")));Drain(w);var second=w.ClearContentForTest();Empty(w);Assert.False(w.MarkdownGate.CanApply(id));Assert.Equal(1,owner.Calls);release.SetResult(!fail);Assert.Equal(!fail,await first);Assert.Equal(!fail,await second);await rig.Flow.Running;Assert.Equal(fail?1:0,w.ClearErrors);Assert.Equal(2,w.SelectionClears);Empty(w);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task OldClearCompletionOrFailureCannotOverwriteNewGeneration(bool fail)
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var owner=new CaptureHostClient{Finalize=()=>new(release.Task)};w.ScreenshotForTest(Task.CompletedTask,owner:owner);var clear=w.ClearContentForTest();var id=await rig.Start();Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"new text")));Drain(w);object? tip=w.ButtonTip;release.SetResult(!fail);Assert.Equal(!fail,await clear);Assert.Equal("new text",w.MarkdownResults.Text);Assert.Equal(id.VersionId,w.MarkdownResults.SelectedVersionId);Assert.Equal(tip,w.ButtonTip);Assert.Equal(0,w.ClearErrors);
    }
    [Fact]public async Task ScreenshotFailureStillAttemptsKnownOwnerAndNeverClaimsComplete()
    {
        var w=new ShellWindow();w.InitializeResultFlow();var owner=new CaptureHostClient();w.ScreenshotForTest(Task.FromException(new ScreenshotCleanupException([new InvalidOperationException("synthetic mask cleanup") ])),owner:owner);Assert.False(await w.ClearContentForTest());Assert.Equal(1,owner.Calls);Assert.False(w.HasScreenshotOwner);Assert.Equal(1,w.ClearErrors);Assert.Contains("尚未确认",w.ButtonTip!.ToString());Empty(w);
    }
    [Fact]public async Task FailedKnownOwnerCanBeRetriedWithoutOverwritingOriginalOperation()
    {
        var w=new ShellWindow();w.InitializeResultFlow();bool confirmed=false;var owner=new CaptureHostClient{Finalize=()=>ValueTask.FromResult(confirmed)};w.ScreenshotForTest(Task.CompletedTask,owner:owner);Assert.False(await w.ClearContentForTest());Assert.True(w.HasScreenshotOwner);confirmed=true;Assert.True(await w.ClearContentForTest());Assert.False(w.HasScreenshotOwner);Assert.Equal(2,owner.Calls);Assert.Equal("当前内容已清除",w.ButtonTip);
    }
    [Fact]public async Task DropSettlementMustConfirmAndDoesNotSkipImageSettlement()
    {
        var w=new ShellWindow();w.InitializeResultFlow();var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var owner=new CaptureHostClient{Finalize=()=>new(release.Task)};w.ScreenshotForTest(Task.CompletedTask,owner:owner);w.DropPeerForTest();w.DropCleanup=Task.FromException(new IOException("synthetic drop owner pending"));var clear=w.ClearContentForTest();Assert.False(clear.IsCompleted);release.SetResult(true);Assert.False(await clear);Assert.False(w.HasScreenshotOwner);Assert.Equal(1,w.ClearErrors);Empty(w);
    }
    [Fact]public async Task ExitingClearCreatesNoNewUiOrCancellationEra()
    {
        var w=new ShellWindow();w.InitializeResultFlow();long epoch=w.MarkdownGate.Snapshot.Epoch;w.Exit();Assert.False(await w.ClearContentForTest());Assert.Equal(epoch,w.MarkdownGate.Snapshot.Epoch);Assert.Equal(0,w.SelectionClears);Assert.Equal(0,w.DropRevocations);Assert.Null(w.ButtonTip);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public async Task ClearCancelsActualImageLifetimeAndZerosSnapshotCropOrPng(int stage)
    {
        var w=new ShellWindow();w.InitializeResultFlow();var topology=new ScreenshotTopology([new("synthetic",new(0,0,8,8),new(1))]);byte[] raw=Enumerable.Repeat((byte)73,8*8*4).ToArray();var tile=new ScreenshotPixels(new(0,0,8,8),raw);var snapshot=new ScreenshotSnapshot(topology,[tile]);ScreenshotPixels? crop=null;ScreenshotPng? png=null;ReadOnlyMemory<byte> memory=tile.Bytes;
        if(stage>0){crop=snapshot.CropAndRelease(new(0,0,8,8),topology);memory=crop.Bytes;}
        if(stage>1){png=ScreenshotPngEncoder.Encode(crop!);memory=png.Bytes;}
        using var cancellation=new CancellationTokenSource();using var intent=new ScreenshotIntent(w.MarkdownGate);bool released=false;
        async Task Hold(){try{await Task.Delay(Timeout.Infinite,cancellation.Token);}catch(OperationCanceledException)when(cancellation.IsCancellationRequested){}finally{snapshot.Dispose();crop?.Dispose();png?.Dispose();intent.Dispose();released=true;}}
        w.ScreenshotForTest(Hold(),cancellation);Assert.True(await w.ClearContentForTest());Assert.True(released);Assert.True(cancellation.IsCancellationRequested);Assert.False(intent.IsCurrent);Assert.False(w.MarkdownGate.Snapshot.Suspension.HasFlag(Suspension.Capture));Assert.All(raw,b=>Assert.Equal(0,b));Assert.All(memory.ToArray(),b=>Assert.Equal(0,b));Assert.Equal("当前内容已清除",w.ButtonTip);Empty(w);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void ProductionPreviewReleaseDetachesReferencesAndWipesBitmapEvenWhenCloseFails(bool failClose)
    {
        var bitmap=new WriteableBitmap(8,8);var image=new Image{Source=bitmap};var panel=new Panel();panel.Children.Add(image);var window=new System.Windows.Window{Content=panel,ThrowOnClose=failClose};if(failClose)Assert.Throws<ScreenshotCleanupException>(()=>ScreenshotPreviewCleanup.Release(window,panel,image,bitmap));else ScreenshotPreviewCleanup.Release(window,panel,image,bitmap);Assert.Null(image.Source);Assert.Null(window.Content);Assert.Empty(panel.Children);Assert.All(bitmap.Pixels,b=>Assert.Equal(0,b));Assert.Equal(Enumerable.Range(0,8),bitmap.Writes);Assert.Equal(1,window.CloseCalls);
    }
    [Fact]public void PreviewReleaseAttemptsEveryRowAndRetainsFailureInsteadOfPretendingWipe()
    {
        var bitmap=new WriteableBitmap(8,8){FailRow=2};var image=new Image{Source=bitmap};var panel=new Panel();panel.Children.Add(image);var window=new System.Windows.Window{Content=panel};Assert.Throws<ScreenshotCleanupException>(()=>ScreenshotPreviewCleanup.Release(window,panel,image,bitmap));Assert.Equal(Enumerable.Range(0,8),bitmap.Writes);Assert.All(bitmap.Pixels.Take(64),b=>Assert.Equal(0,b));Assert.All(bitmap.Pixels.Skip(64).Take(32),b=>Assert.Equal(51,b));Assert.All(bitmap.Pixels.Skip(96),b=>Assert.Equal(0,b));Assert.Null(image.Source);Assert.Null(window.Content);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void ScreenshotRestoreCannotSurviveEndNewCaptureHiddenOrExit(int action)
    {
        var w=new ShellWindow();w.InitializeResultFlow();using var first=new CancellationTokenSource();using var second=new CancellationTokenSource();var old=w.RestoreGuardForTest(first);Assert.True(old());switch(action){case 0:w.EndRestoreForTest(first);break;case 1:Assert.True(w.RestoreGuardForTest(second)());break;case 2:w.MarkdownGate.Disable(hide:true);break;case 3:w.Exit();break;}Assert.False(old());
    }
    [Fact]public void EndingOldRestoreDoesNotRevokeNewOwner()
    {
        var w=new ShellWindow();w.InitializeResultFlow();using var first=new CancellationTokenSource();using var second=new CancellationTokenSource();var old=w.RestoreGuardForTest(first);var current=w.RestoreGuardForTest(second);w.EndRestoreForTest(first);Assert.False(old());Assert.True(current());w.EndRestoreForTest(second);Assert.False(current());
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task OldScreenshotObserverCannotReportAfterClearOrExit(bool exit)
    {
        var w=new ShellWindow();w.InitializeResultFlow();var finish=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var observer=w.ObserveScreenshotForTest(finish.Task,w.ResultRevision);if(exit)w.Exit();else Assert.True(await w.ClearContentForTest());object? tip=w.ButtonTip;finish.SetException(new ScreenshotCleanupException([new IOException("synthetic old failure")]));await observer;Assert.Equal(0,w.ClearErrors);Assert.Equal(tip,w.ButtonTip);
    }
    [Fact]public async Task HiddenClearKeepsHiddenPresentationAndCannotRestoreOldScreenshotWindows()
    {
        var w=new ShellWindow();w.InitializeResultFlow();using var owner=new CancellationTokenSource();var restore=w.RestoreGuardForTest(owner);w.MarkdownGate.Disable(hide:true);Assert.True(await w.ClearContentForTest());Assert.Equal(Presentation.Hidden,w.MarkdownGate.Snapshot.Presentation);Assert.False(restore());
    }
    [Fact]public async Task RealGateCancellationCallbackFailureStillClearsEveryIndependentContentOwner()
    {
        var w=new ShellWindow();w.InitializeResultFlow();var origin=new EndpointOrigin(new Uri("https://public.invalid"));w.MarkdownGate.ConsentTo(origin);Assert.True(w.MarkdownGate.TryEnable(origin,true));Assert.True(w.MarkdownGate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var token));Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Begin)));Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"partial")));using var registration=token.Register(()=>throw new InvalidOperationException("synthetic cancellation failure"));var owner=new CaptureHostClient();w.ScreenshotForTest(Task.CompletedTask,owner:owner);Assert.False(await w.ClearContentForTest());Assert.False(w.MarkdownGate.CanApply(id));Assert.Equal(1,owner.Calls);Assert.Equal(1,w.ClearErrors);Empty(w);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ClearCompletionDoesNotOverwriteNewCaptureOrConfirmationWithoutBegin(bool capture)
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var owner=new CaptureHostClient{Finalize=()=>new(release.Task)};w.ScreenshotForTest(Task.CompletedTask,owner:owner);var clear=w.ClearContentForTest();ScreenshotIntent? intent=null;
        if(capture)intent=new(w.MarkdownGate);else{string value=new('a',12001);var source=new SelectionSourceSnapshot(new(17,92823,132000,@"C:\Synthetic\fixture-editor.exe"),value,value,[new(1,2,20,22)],[7,8],rig.Settings.Version,value.Length,true);var offer=rig.Flow.Prepare(source);Assert.NotNull(offer);Assert.Equal(KuaiYi.Providers.TextSubmission.ConfirmationRequired,rig.Flow.Submit(offer));}
        long revision=w.ResultRevision;object? tip=w.ButtonTip;release.SetResult(true);Assert.True(await clear);Assert.Equal(revision,w.ResultRevision);Assert.Equal(tip,w.ButtonTip);Assert.Equal(0,w.ClearErrors);intent?.Dispose();
    }
    [Fact]public async Task ScreenshotCancellationFailureStillAwaitsOperationAndAttemptsKnownOwner()
    {
        var w=new ShellWindow();w.InitializeResultFlow();using var cancel=new CancellationTokenSource();using var callback=cancel.Token.Register(()=>throw new IOException("synthetic screenshot cancel failure"));var owner=new CaptureHostClient();w.ScreenshotForTest(Task.CompletedTask,cancel,owner);Assert.False(await w.ClearContentForTest());Assert.Equal(1,owner.Calls);Assert.False(w.HasScreenshotOwner);Assert.Equal(1,w.ClearErrors);Empty(w);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void ProductionBitmapOwnershipStartsBeforeCopyAndPartialUiConstruction(int stage)
    {
        var bitmap=new WriteableBitmap(8,8);System.Windows.Window? window=null;Panel? panel=null;Image? image=null;
        Assert.Throws<IOException>(()=>ScreenshotPreviewCleanup.Own(bitmap,()=>{if(stage==0)throw new IOException("synthetic copy failure");window=new();if(stage==1)throw new IOException("synthetic panel creation failure");panel=new();window.Content=panel;if(stage==2)throw new IOException("synthetic image creation failure");image=new(){Source=bitmap};panel.Children.Add(image);throw new IOException("synthetic show failure");},()=> (window,panel,image)));
        Assert.All(bitmap.Pixels,b=>Assert.Equal(0,b));Assert.Equal(Enumerable.Range(0,8),bitmap.Writes);if(window!=null)Assert.Null(window.Content);if(panel!=null)Assert.Empty(panel.Children);if(image!=null)Assert.Null(image.Source);
    }
}
