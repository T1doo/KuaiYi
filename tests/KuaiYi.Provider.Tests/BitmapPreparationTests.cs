using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Imaging;
using Xunit;
namespace KuaiYi.App
{
    internal sealed partial class ShellWindow
    {
        internal bool BitmapForTest(IBitmapImageOperation op){var s=lifecycle.Gate.Snapshot;return TryStartBitmapPreparation(()=>op,s.Epoch,s.Activation);}
        internal Task BitmapTaskForTest=>droppedBitmapTask;
    }
}
namespace KuaiYi.Provider.Tests
{
public sealed class BitmapPreparationTests
{
    private sealed class Operation:IBitmapImageOperation
    {
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Peer=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<OwnedFileImage> Reply=new(TaskCreationOptions.RunContinuationsAsynchronously);internal CancellationToken Token;internal int Finalized;
        public async Task<OwnedFileImage> LoadAsync(Guid session,long version,long epoch,CancellationToken token){Token=token;Entered.SetResult();await Peer.Task.WaitAsync(token);return await Reply.Task;}
        public Task ObservePeerCleanupAsync()=>Peer.Task;
        public ValueTask<bool> TryFinalizeAsync(){if(!Peer.Task.IsCompleted)return ValueTask.FromResult(false);Finalized++;return ValueTask.FromResult(true);}
    }
    private static OwnedFileImage Owner(ImageSourceKind kind=ImageSourceKind.Bitmap)
    {byte[] raw=Enumerable.Repeat((byte)255,256).ToArray();using var pixels=new ScreenshotPixels(new(0,0,8,8),raw.ToArray());return new(new(EncodedRasterFormat.Bmp,78,new string('a',64),ImageTransform.PlanEncoded(new(8,8),ImageOrientation.ExifTopLeft),2560),raw,ScreenshotPngEncoder.Encode(pixels),kind);}
    private static ShellWindow Window(){var w=new ShellWindow();w.InitializeResultFlow();w.WithGeometry();return w;}
    [Fact]public async Task OffPreviewPublishesBitmapOnlyAfterPeerCleanupAndDoesNotSend()
    {
        var w=Window();var op=new Operation();Assert.True(w.BitmapForTest(op));await op.Entered.Task;Assert.Null(w.SelectedImageForTest);Assert.True(w.FileBusyForTest);var image=Owner();op.Reply.SetResult(image);Assert.Null(w.SelectedImageForTest);op.Peer.SetResult();await w.BitmapTaskForTest;
        Assert.Same(image,w.SelectedImageForTest);Assert.Equal(Activation.Off,w.MarkdownGate.Snapshot.Activation);Assert.Contains("投放位图",w.ResultLabel);Assert.DoesNotContain("截图",w.ResultLabel);Assert.DoesNotContain("文件图片",w.ResultLabel);Assert.False(w.ScreenshotSendEnabledForTest);Assert.False(w.MarkdownGate.HasRunningGeneration);Assert.True(await w.ClearContentForTest());Assert.True(image.IsDisposed);
    }
    [Fact]public async Task CancelledPreparationRetainsSlotUntilOriginalPeerSettlesAndRejectsLateImage()
    {
        var w=Window();var op=new Operation();Assert.True(w.BitmapForTest(op));await op.Entered.Task;var clear=w.ClearContentForTest();Assert.True(op.Token.IsCancellationRequested);Assert.False(w.BitmapTaskForTest.IsCompleted);Assert.True(w.FileBusyForTest);Assert.False(w.BitmapForTest(new Operation()));op.Peer.SetResult();await w.BitmapTaskForTest;await clear;Assert.Null(w.SelectedImageForTest);Assert.False(w.FileBusyForTest);Assert.True(op.Finalized>0);
    }
    [Fact]public async Task LateDecodedImageAfterClearIsDisposedAndCannotReplaceTheOldSource()
    {
        var w=Window();var op=new Operation();Assert.True(w.BitmapForTest(op));await op.Entered.Task;op.Peer.SetResult();var clear=w.ClearContentForTest();Assert.True(op.Token.IsCancellationRequested);var late=Owner();op.Reply.SetResult(late);await w.BitmapTaskForTest;await clear;Assert.True(late.IsDisposed);Assert.Null(w.SelectedImageForTest);Assert.False(w.FileBusyForTest);
    }
    [Fact]public async Task WrongReturnedSourceKindIsDisposedAndCannotMasqueradeAsBitmap()
    {
        var w=Window();var op=new Operation();Assert.True(w.BitmapForTest(op));await op.Entered.Task;var image=Owner(ImageSourceKind.LocalFile);op.Reply.SetResult(image);op.Peer.SetResult();await w.BitmapTaskForTest;Assert.True(image.IsDisposed);Assert.Null(w.SelectedImageForTest);Assert.False(w.FileBusyForTest);
    }
}
}
