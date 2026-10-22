using System.Runtime.ExceptionServices;
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
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Peer=new(TaskCreationOptions.RunContinuationsAsynchronously),DecodedEntered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<OwnedFileImage> Reply=new(TaskCreationOptions.RunContinuationsAsynchronously);internal CancellationToken Token;internal int Finalized;
        public async Task<OwnedFileImage> LoadAsync(Guid session,long version,long epoch,CancellationToken token){Token=token;Entered.SetResult();await Peer.Task.WaitAsync(token);DecodedEntered.TrySetResult();return await Reply.Task;}
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
        var w=Window();var op=new Operation();Assert.True(w.BitmapForTest(op));var original=w.BitmapTaskForTest;OwnedFileImage? late=null;Task<bool>? clear=null;List<Exception> errors=[];
        using var guard=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await op.Entered.Task.WaitAsync(guard.Token);op.Peer.SetResult();
            // Signal completion alone does not prove that the cancellable peer wait returned successfully.
            await op.DecodedEntered.Task.WaitAsync(guard.Token);clear=w.ClearContentForTest();Assert.True(op.Token.IsCancellationRequested);
            late=Owner();op.Reply.SetResult(late);await original.WaitAsync(guard.Token);Assert.True(await clear.WaitAsync(guard.Token));
            Assert.True(late.IsDisposed);Assert.Null(w.SelectedImageForTest);Assert.False(w.FileBusyForTest);
        }
        catch(Exception error){errors.Add(error);}
        finally
        {
            op.Peer.TrySetResult();op.Reply.TrySetCanceled();
            try{await original;}catch(Exception error){if(!errors.Any(old=>ReferenceEquals(old,error)))errors.Add(error);}
            try{if(!await (clear??w.ClearContentForTest()))throw new InvalidOperationException("Original bitmap test cleanup unconfirmed.");}catch(Exception error){errors.Add(error);}
            late?.Dispose(); // A fixture-created image that was never returned remains the fixture's responsibility.
        }
        if(errors.Count==1)ExceptionDispatchInfo.Capture(errors[0]).Throw();if(errors.Count>1)throw new AggregateException("Bitmap late-image body and original cleanup failed.",errors);
    }
    [Fact]public async Task WrongReturnedSourceKindIsDisposedAndCannotMasqueradeAsBitmap()
    {
        var w=Window();var op=new Operation();Assert.True(w.BitmapForTest(op));await op.Entered.Task;var image=Owner(ImageSourceKind.LocalFile);op.Reply.SetResult(image);op.Peer.SetResult();await w.BitmapTaskForTest;Assert.True(image.IsDisposed);Assert.Null(w.SelectedImageForTest);Assert.False(w.FileBusyForTest);
    }
}
}
