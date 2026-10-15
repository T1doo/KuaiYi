using KuaiYi.App;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class OverlayScrollTests
{
    [Fact] public void ReaderUpAfterFlushIsNotReplacedByCapturedOffset()
    {
        var window=new ShellWindow();window.QueueScroll(true,800);window.ReaderScroll(120);window.Deliver();
        Assert.Equal(120,window.ScrollOffset);Assert.Equal(0,window.ScrollCalls);
    }
    [Fact] public void ExplicitFollowEndIsNotUndoneByOldPausedRestore()
    {
        var window=new ShellWindow();window.ReaderScroll(120);window.QueueScroll(false,120);window.ReaderFollowEnd();window.Deliver();
        Assert.Equal(1000,window.ScrollOffset);Assert.Equal(1,window.ScrollCalls);
    }
    [Fact] public void SupersededFlushCannotRestoreAfterLatestFlush()
    {
        var window=new ShellWindow();window.QueueScroll(false,100);window.QueueScroll(false,200);window.Deliver(1);window.Deliver();
        Assert.Equal(200,window.ScrollOffset);Assert.Equal(1,window.ScrollCalls);
    }
    [Fact] public void InputFenceDoesNotDisableNewCurrentRestore()
    {
        var window=new ShellWindow();window.QueueScroll(false,800);window.ReaderScroll(120);window.QueueScroll(false,120);window.Deliver(1);window.Deliver();
        Assert.Equal(120,window.ScrollOffset);Assert.Equal(1,window.ScrollCalls);
    }
    [Fact] public void HiddenPresentationDoesNotRunQueuedRestore()
    {
        var window=new ShellWindow();window.QueueScroll(true,0);window.HideOrInvalidate();window.Deliver();
        Assert.Equal(0,window.ScrollCalls);
    }
    [Fact] public void ResultReplacementFencesOldRestore()
    {
        var window=new ShellWindow();window.QueueScroll(true,0);window.ReplaceResult();window.Deliver();Assert.Equal(0,window.ScrollCalls);
    }
    [Fact] public void SessionLockFencesQueuedRestore()
    {
        var window=new ShellWindow();window.QueueScroll(true,0);window.Lock();window.Deliver();Assert.Equal(0,window.ScrollCalls);
    }
    [Fact] public void ExitFencesQueuedRestore()
    {
        var window=new ShellWindow();window.QueueScroll(true,0);window.Exit();window.Deliver();Assert.Equal(0,window.ScrollCalls);
    }
    [Fact] public void CurrentFollowerStillMovesToEnd()
    {
        var window=new ShellWindow();window.QueueScroll(true,0);window.Deliver();Assert.Equal(1000,window.ScrollOffset);Assert.Equal(1,window.ScrollCalls);
    }
    [Fact] public void CurrentPausedReaderStillRestoresOffset()
    {
        var window=new ShellWindow();window.ReaderScroll(120);window.QueueScroll(false,120);window.Deliver();Assert.Equal(120,window.ScrollOffset);Assert.Equal(1,window.ScrollCalls);
    }
    [Fact] public void SupersededCallbackIsSkippedInFifoOrder()
    {
        var window=new ShellWindow();window.QueueScroll(false,100);window.QueueScroll(false,200);
        window.Deliver();Assert.Equal(0,window.ScrollCalls);
        window.Deliver();Assert.Equal(200,window.ScrollOffset);Assert.Equal(1,window.ScrollCalls);
    }

}
