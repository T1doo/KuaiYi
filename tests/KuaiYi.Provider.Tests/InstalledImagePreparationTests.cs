using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Imaging;
using Xunit;
namespace KuaiYi.App
{
    internal sealed partial class ShellWindow
    {
        internal Task QueueInstalledForTest(IFileImageOperation op,Task dispatch,CancellationToken token=default,Func<bool>? current=null,Func<CancellationToken,Task>? verify=null)
        {
            RegisterInstalledImageDispatch(async expected=>{await dispatch;_ =StartInstalledImage("C:\\public\\image.gif",expected,()=>op,token,current??(()=>true),verify??(_=>Task.CompletedTask));});
            return installationImageDispatch;
        }
        internal Task InstalledFileTaskForTest=>fileImageTask;
        internal Task StopInstalledForTest()=>StopInstalledImageDispatch();
        internal void ChangeInstalledSettingsForTest()=>lifecycle.Settings=new(new(),"changed-installed-config",SettingsLoadState.Current);
    }
}
namespace KuaiYi.Provider.Tests
{
public sealed class InstalledImagePreparationTests
{
    // Original production dispatch registration/admission/file task are linked. WPF and file bytes are synthetic ports.
    private sealed class Operation:IFileImageOperation
    {
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<OwnedFileImage> Reply=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Loads;internal bool Finalized=true;internal CancellationToken Token;
        public async Task<OwnedFileImage> LoadAsync(string path,Guid session,long version,long epoch,CancellationToken cancellation)
        {Assert.Equal("C:\\public\\image.gif",path);Loads++;Token=cancellation;Entered.TrySetResult();return await Reply.Task;}
        public ValueTask<bool> TryFinalizeAsync()=>ValueTask.FromResult(Finalized);
    }
    private static ShellWindow Window(){var w=new ShellWindow();w.InitializeResultFlow();w.WithGeometry();return w;}
    private static TaskCompletionSource Signal()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    [Fact]public async Task InstalledImageOnlyCreatesTheOriginalFilePreviewInOffAndKeepsTheFirstFrameLabel()
    {
        var w=Window();var op=new Operation();await w.QueueInstalledForTest(op,Task.CompletedTask);await op.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var image=FileImageSourceTests.Owner(out _);op.Reply.SetResult(image);await w.InstalledFileTaskForTest;
        Assert.Same(image,w.SelectedImageForTest);Assert.Equal(ImageSourceKind.LocalFile,image.Kind);Assert.Contains("使用首帧",w.ResultLabel);Assert.Equal(Activation.Off,w.MarkdownGate.Snapshot.Activation);Assert.False(w.MarkdownGate.HasRunningGeneration);Assert.False(w.ScreenshotSendEnabledForTest);
        Assert.True(await w.ClearContentForTest());Assert.True(image.IsDisposed);
    }
    [Theory][InlineData("clear")][InlineData("lock")][InlineData("hidden")][InlineData("settings")][InlineData("cancel")][InlineData("revoked")]
    public async Task AQueuedInstalledRequestCannotBorrowNewerStateAtDispatcherDelivery(string change)
    {
        var w=Window();var op=new Operation();var dispatch=Signal();using var token=new CancellationTokenSource();bool current=true;
        var original=w.QueueInstalledForTest(op,dispatch.Task,token.Token,()=>current);
        if(change=="clear")await w.ClearContentForTest();if(change=="lock")w.Lock();if(change=="hidden")w.HideImageWindowForTest();if(change=="settings")w.ChangeInstalledSettingsForTest();if(change=="cancel")token.Cancel();if(change=="revoked")current=false;
        dispatch.TrySetResult();await original;Assert.Equal(0,op.Loads);Assert.Null(w.SelectedImageForTest);Assert.Equal(Activation.Off,w.MarkdownGate.Snapshot.Activation);
        await w.StopInstalledForTest();await w.SettleFileForTest();
    }
    [Fact]public async Task ExitClosesRegistrationAndJoinsTheSameQueuedDispatchBeforeAnyFileCanStart()
    {
        var w=Window();var op=new Operation();var dispatch=Signal();var original=w.QueueInstalledForTest(op,dispatch.Task);
        var stop=w.StopInstalledForTest();Assert.Same(original,stop);Assert.False(stop.IsCompleted);
        var other=new Operation();Assert.Same(original,w.QueueInstalledForTest(other,Task.CompletedTask));dispatch.TrySetResult();await stop;
        Assert.Equal(0,op.Loads);Assert.Equal(0,other.Loads);Assert.Null(w.SelectedImageForTest);
    }
    [Theory][InlineData("cancel")][InlineData("clear")][InlineData("record")][InlineData("validator-fault")]
    public async Task HeldPostLoadVerificationRetainsTheExactRasterAndTaskUntilItSettles(string change)
    {
        var w=Window();var op=new Operation();var entered=Signal();var release=Signal();using var cancellation=new CancellationTokenSource();bool current=true;
        async Task Verify(CancellationToken token){entered.TrySetResult();await release.Task;if(change=="validator-fault")throw new IOException("PUBLIC_BAD_RECORD");token.ThrowIfCancellationRequested();}
        await w.QueueInstalledForTest(op,Task.CompletedTask,cancellation.Token,()=>current,Verify);await op.Entered.Task;var image=FileImageSourceTests.Owner(out var raw);op.Reply.SetResult(image);await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task? clear=null;try
        {
            Assert.False(w.InstalledFileTaskForTest.IsCompleted);Assert.False(image.IsDisposed);Assert.True(w.FileBusyForTest);
            if(change=="cancel")cancellation.Cancel();if(change=="clear")clear=w.ClearContentForTest();if(change=="record")current=false;
            Assert.False(w.InstalledFileTaskForTest.IsCompleted);if(clear!=null)Assert.False(clear.IsCompleted);
        }
        finally{release.TrySetResult();await w.InstalledFileTaskForTest;if(clear!=null)await clear;}
        Assert.True(image.IsDisposed);Assert.All(raw,b=>Assert.Equal(0,b));Assert.Null(w.SelectedImageForTest);Assert.False(w.FileBusyForTest);await w.SettleFileForTest();
    }
    [Fact]public async Task UnsettledWorkerStillBlocksReplacementAndExitUntilTheOriginalOwnerCanFinalize()
    {
        var w=Window();var op=new Operation{Finalized=false};await w.QueueInstalledForTest(op,Task.CompletedTask);await op.Entered.Task;var image=FileImageSourceTests.Owner(out _);op.Reply.SetResult(image);await w.InstalledFileTaskForTest;
        Assert.True(image.IsDisposed);Assert.Null(w.SelectedImageForTest);Assert.True(w.FileBusyForTest);
        var other=new Operation();await w.QueueInstalledForTest(other,Task.CompletedTask);Assert.Equal(0,other.Loads);await Assert.ThrowsAnyAsync<Exception>(()=>w.SettleFileForTest());op.Finalized=true;await w.SettleFileForTest();Assert.False(w.FileBusyForTest);
    }
}
}
