using System.ComponentModel;
namespace KuaiYi.App;

// Only WPF dispatcher/window/settings surfaces. The actual ClosingShell, RuntimeGate and content cleanup run unchanged.
internal sealed partial class ShellWindow
{
    private bool opening=false,allowClose=false,exitRequested=false;
    private CloseSettingsPort? settings=null;
    private readonly TaskCompletionSource closeDispatchRelease=new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int ClosedForTest{get;private set;}
    internal bool ExitRequestedForTest=>exitRequested;
    internal bool ExitingForTest=>exiting;
    internal Task? OriginalCloseForTest=>closeRequests?.Current;
    internal void RequestCloseForTest()=>Close();
    internal void ReleaseCloseDispatchForTest()=>closeDispatchRelease.TrySetResult();
    private void Close(){var e=new CancelEventArgs();ClosingShell(this,e);if(!e.Cancel)ClosedForTest++;}
    private void QueuePositionSave(){ /* Position persistence is a UI call port; no save/exit receipt is forged. */ }
    private async Task<bool> DispatchCloseAttempt(){await closeDispatchRelease.Task;return await TryCloseAttemptAsync();}
    private Task CommitShellCloseAsync(){Close();return Task.CompletedTask;}
    private sealed class CloseSettingsPort{internal void RequestCloseWhenSettled()=>throw new InvalidOperationException("Settings WPF is outside this port.");}
}
