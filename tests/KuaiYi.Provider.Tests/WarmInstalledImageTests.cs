using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Installation;
using KuaiYi.Imaging;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;
namespace KuaiYi.App
{
    internal sealed partial class ShellWindow
    {
        internal sealed record WarmFileRun(InstallationWarmImageAdmission Admission,InstallationWarmLease Lease,Task Original);
        internal WarmFileRun StartWarmFileForTest(IFileImageOperation operation)
        {
            var proof=new InstallationControlledLaunch(Guid.NewGuid(),"KuaiYi.App.exe",Path.GetFullPath("PUBLIC_INSTALL"),Path.GetFullPath("PUBLIC_DATA"),new("1",Path.GetFullPath("PUBLIC_GENERATION"),new string('A',64)),"e30=","e30=",null);
            var expected=CaptureInstalledImageState();var admission=new InstallationWarmImageAdmission(proof,TimeProvider.System);var lease=admission.Reserve(new(),expected.Epoch);Assert.True(admission.Commit(lease,new(lease.Grant,"C:\\public\\neutral.gif")));
            Task original=StartInstalledImage("C:\\public\\neutral.gif",expected,()=>operation,lease.Token,()=>!lease.Token.IsCancellationRequested&&!Volatile.Read(ref installedImageClosed),_=>Task.CompletedTask,(stamp,intent,image)=>AcceptFileImage(stamp,intent,image,admission,lease));
            return new(admission,lease,original);
        }
    }
}
namespace KuaiYi.Provider.Tests
{
public sealed class WarmInstalledImageTests
{
    private sealed class Operation:IFileImageOperation
    {
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<OwnedFileImage> Reply=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Finalized=true;internal int Finalizes;internal bool FailSecondFinalize;
        public async Task<OwnedFileImage> LoadAsync(string path,Guid session,long version,long epoch,CancellationToken cancellation){Entered.SetResult();return await Reply.Task;}
        public ValueTask<bool> TryFinalizeAsync(){Finalizes++;if(FailSecondFinalize&&Finalizes>=2)throw new IOException("PUBLIC_FINALIZE_FAILURE");return ValueTask.FromResult(Finalized);}
    }
    private static ShellWindow Window(){var w=new ShellWindow();w.InitializeResultFlow();w.WithGeometry();return w;}
    private static async Task<InstallationWarmResult> Retire(ShellWindow.WarmFileRun run)
    {
        if(run.Admission.Phase(run.Lease)==InstallationWarmPhase.Preparing)await run.Admission.Revoke(run.Lease);
        InstallationWarmResult? result=null;Assert.True(await run.Admission.RetireAsync(run.Lease,()=>ValueTask.FromResult(true),value=>{result=value;return Task.CompletedTask;}));return result!;
    }
    [Fact]public async Task ProductionWarmFileChainRequiresFinalizedWorkerAndConfirmsExactVisibleSourceGuidInOff()
    {
        var w=Window();var op=new Operation();var run=w.StartWarmFileForTest(op);await op.Entered.Task;var image=FileImageSourceTests.Owner(out var raw);op.Reply.SetResult(image);await run.Original;
        Assert.True(op.Finalizes>=1);Assert.Same(image,w.SelectedImageForTest);Assert.Contains("使用首帧",w.ResultLabel);Assert.Equal(Activation.Off,w.MarkdownGate.Snapshot.Activation);Assert.False(w.MarkdownGate.HasRunningGeneration);Assert.False(w.ScreenshotSendEnabledForTest);
        var result=await Retire(run);Assert.Equal(InstallationWarmOutcome.PreviewReady,result.Outcome);Assert.Equal(image.SourceId,result.Source);Assert.Equal(w.SelectedSourceVersionForTest,result.SourceVersion);Assert.NotEqual(Guid.Empty,result.SourceVersion);
        Assert.True(await w.ClearContentForTest());Assert.True(image.IsDisposed);Assert.All(raw,b=>Assert.Equal(0,b));
    }
    [Fact]public async Task CancellationBeforeExactWorkerReturnDiscardsPixelsAndCannotBecomePreviewReady()
    {
        var w=Window();var op=new Operation();var run=w.StartWarmFileForTest(op);await op.Entered.Task;await run.Admission.Revoke(run.Lease);Assert.False(run.Original.IsCompleted);Assert.True(w.FileBusyForTest);
        var image=FileImageSourceTests.Owner(out var raw);op.Reply.SetResult(image);await run.Original;Assert.True(image.IsDisposed);Assert.All(raw,b=>Assert.Equal(0,b));Assert.Null(w.SelectedImageForTest);Assert.Equal(InstallationWarmOutcome.Rejected,(await Retire(run)).Outcome);
    }
    [Fact]public async Task PostCommitCancellationRunsOutsideWarmLockAndCannotRevokeAnAppOwnedSource()
    {
        var w=Window();var op=new Operation();var run=w.StartWarmFileForTest(op);await op.Entered.Task;int callbacks=0;
        w.OcrCleanupForTest=()=>{callbacks++;Assert.Equal(InstallationWarmPhase.AppOwned,run.Admission.Phase(run.Lease));Task.Run(()=>run.Admission.Revoke(run.Lease)).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();Assert.False(run.Lease.Token.IsCancellationRequested);};
        var image=FileImageSourceTests.Owner(out _);op.Reply.SetResult(image);await run.Original;Assert.True(callbacks>0);Assert.Equal(InstallationWarmOutcome.PreviewReady,(await Retire(run)).Outcome);w.OcrCleanupForTest=null;await w.ClearContentForTest();
    }
    [Fact]public async Task ReentrantTextSelectionRetainsItsSourceAndSuppressesTheOldPreviewReceipt()
    {
        var w=Window();var op=new Operation();var run=w.StartWarmFileForTest(op);await op.Entered.Task;bool reentered=false;
        w.OcrCleanupForTest=()=>{if(reentered)return;reentered=true;w.BeginTextSourceForTest();};var image=FileImageSourceTests.Owner(out _);op.Reply.SetResult(image);await run.Original;
        Assert.True(reentered);Assert.Null(w.SelectedImageForTest);Assert.False(image.IsDisposed);Assert.Equal(InstallationWarmOutcome.Rejected,(await Retire(run)).Outcome);w.OcrCleanupForTest=null;await w.ClearContentForTest();Assert.True(image.IsDisposed);
    }
    [Fact]public async Task UnconfirmedWorkerCannotReachPureCommitOrReportPreviewReady()
    {
        var w=Window();var op=new Operation{Finalized=false};var run=w.StartWarmFileForTest(op);await op.Entered.Task;var image=FileImageSourceTests.Owner(out _);op.Reply.SetResult(image);await Assert.ThrowsAnyAsync<Exception>(()=>run.Original);
        Assert.Null(w.SelectedImageForTest);Assert.True(image.IsDisposed);Assert.True(w.FileBusyForTest);Assert.Equal(InstallationWarmPhase.Preparing,run.Admission.Phase(run.Lease));op.Finalized=true;await Assert.ThrowsAnyAsync<Exception>(()=>w.SettleFileForTest());await run.Admission.Revoke(run.Lease);await Retire(run);
    }
    [Fact]public async Task RenderBodyAndCleanupFailuresAreBothPreservedAndOnlyExactCandidateIsErased()
    {
        var w=Window();var op=new Operation{FailSecondFinalize=true};var run=w.StartWarmFileForTest(op);await op.Entered.Task;
        w.OcrCleanupForTest=()=>throw new IOException("PUBLIC_RENDER_FAILURE");var image=FileImageSourceTests.Owner(out var raw);op.Reply.SetResult(image);var failure=await Assert.ThrowsAsync<AggregateException>(()=>run.Original);
        string errors=failure.ToString();Assert.Contains("PUBLIC_RENDER_FAILURE",errors);Assert.Contains("PUBLIC_FINALIZE_FAILURE",errors);Assert.True(image.IsDisposed);Assert.All(raw,b=>Assert.Equal(0,b));Assert.Null(w.SelectedImageForTest);Assert.False(run.Lease.PreviewConfirmed);Assert.True(w.FileBusyForTest);w.OcrCleanupForTest=null;
    }
    [Theory][InlineData(ImageFailure.InvalidImage)][InlineData(ImageFailure.ImageTooLarge)][InlineData(ImageFailure.UnsupportedImage)]
    public async Task AConfirmedInputRejectionCanRetireAndTheNextExplicitRequestCanStillPreview(ImageFailure code)
    {
        var w=Window();var op=new Operation();var run=w.StartWarmFileForTest(op);await op.Entered.Task;op.Reply.SetException(new ImageJobException(code));await run.Original;
        Assert.Equal(InstallationWarmOutcome.Rejected,(await Retire(run)).Outcome);Assert.False(w.FileBusyForTest);Assert.Null(w.SelectedImageForTest);
        var next=new Operation();var retry=w.StartWarmFileForTest(next);await next.Entered.Task;var image=FileImageSourceTests.Owner(out _);next.Reply.SetResult(image);await retry.Original;Assert.Equal(InstallationWarmOutcome.PreviewReady,(await Retire(retry)).Outcome);await w.ClearContentForTest();
    }
    [Theory][InlineData(ImageFailure.ProtocolFailure)][InlineData(ImageFailure.ExitUnconfirmed)]
    public async Task ProtocolOrExitFailuresNeverBecomeRecoverableInputRejections(ImageFailure code)
    {
        var w=Window();var op=new Operation();var run=w.StartWarmFileForTest(op);await op.Entered.Task;var original=new ImageJobException(code);op.Reply.SetException(original);
        Assert.Same(original,await Assert.ThrowsAsync<ImageJobException>(()=>run.Original));Assert.True(w.FileBusyForTest);Assert.Null(w.SelectedImageForTest);
    }
    [Fact]public void ImageOnlyViewCleanupCannotDetachANewTextViewInstalledByAnOldRegionCallback()
    {
        using var old=FileImageSourceTests.Owner(out _);using var candidate=FileImageSourceTests.Owner(out _);var host=new RichTextBox();var view=new MarkdownFlowView(host);Guid first=Guid.NewGuid(),next=Guid.NewGuid();bool current=true;
        view.SetImageSource(first,old);view.ExpandSource();var oldExpander=Assert.IsType<Expander>(Assert.IsType<BlockUIContainer>(host.Document.Blocks[^1]).Child);
        oldExpander.Collapsed+=(_,_)=>{current=false;view.SetSource(next,"PUBLIC_NEW_SOURCE");};
        view.ShowLocalImage(Guid.NewGuid(),candidate,15,new(388,560),()=>current);Assert.False(current);view.ExpandSource();
        var shown=Assert.IsType<Expander>(Assert.IsType<BlockUIContainer>(host.Document.Blocks[^1]).Child);Assert.NotSame(oldExpander,shown);Assert.IsType<TextBox>(shown.Content);Assert.True(shown.IsExpanded);view.Clear();
    }
    [Fact]public void FailedExactOldBitmapRemainsRetryableAcrossWarmStagingAndAReentrantNewSource()
    {
        using var old=FileImageSourceTests.Owner(out _);using var candidate=FileImageSourceTests.Owner(out _);var host=new RichTextBox();var view=new MarkdownFlowView(host);bool current=true;
        view.SetImageSource(Guid.NewGuid(),old);view.ExpandSource();var expander=Assert.IsType<Expander>(Assert.IsType<BlockUIContainer>(host.Document.Blocks[^1]).Child);
        var bitmap=Assert.IsType<System.Windows.Media.Imaging.WriteableBitmap>(Assert.IsType<Image>(expander.Content).Source);bitmap.FailRow=2;
        expander.Collapsed+=(_,_)=>{current=false;view.SetSource(Guid.NewGuid(),"PUBLIC_NEW_SOURCE");};
        Assert.Throws<ScreenshotCleanupException>(()=>view.ShowLocalImage(Guid.NewGuid(),candidate,15,new(388,560),()=>current));Assert.False(current);Assert.Contains(bitmap.Pixels,b=>b!=0);
        // A second attempted batch is refused while the exact original bitmap is still unconfirmed.
        Assert.Throws<ScreenshotCleanupException>(()=>view.ShowLocalImage(Guid.NewGuid(),candidate,15,new(388,560),()=>true));
        bitmap.FailRow=null;view.Clear();Assert.All(bitmap.Pixels,b=>Assert.Equal(0,b));Assert.Empty(host.Document.Blocks);
    }
    [Fact]public void MeasuredOldPreviewCannotOpenAfterTheMeasureCallbackChangesItsCurrentStamp()
    {
        var frames=new OverlayFrames(new(100,100,148,148),new("PUBLIC",new(0,0,1920,1080),new(1)));bool current=true;
        frames.OpenMeasured(new(420,640),_=>{current=false;frames.Compact();return 400;},()=>current);Assert.False(frames.IsExpanded);Assert.Equal(OverlayTransition.Compact,frames.Transition);
    }
}
}
