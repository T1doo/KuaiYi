using System.IO;
using KuaiYi.Imaging;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private Task screenshotHotkeyTask=Task.CompletedTask,screenshotHotkeyCleanup=Task.CompletedTask;
    private CancellationTokenSource? screenshotHotkeyStop;
    private CaptureHostClient? screenshotHotkeyOwner;
    private long screenshotRestoreRevision;
    private Func<bool> CaptureScreenshotRestoreGuard(CancellationTokenSource owner)
    {
        long revision=++screenshotRestoreRevision;
        return()=>revision==screenshotRestoreRevision&&ReferenceEquals(screenshotHotkeyStop,owner)&&!exiting&&
            !lifecycle.Gate.Snapshot.Suspension.HasFlag(KuaiYi.Core.Suspension.SessionLocked)&&lifecycle.Gate.Snapshot.Presentation!=KuaiYi.Core.Presentation.Hidden;
    }
    private void EndScreenshotRestore(CancellationTokenSource owner)
    {if(!ReferenceEquals(screenshotHotkeyStop,owner))return;screenshotRestoreRevision++;screenshotHotkeyStop=null;}
    private async Task ObserveHotkeyScreenshot(Task operation,long revision)
    {try{await operation;}catch(Exception error){if(!exiting&&revision==resultRevision&&CanReadOriginalSource())Report("截图原操作未正常结束；退出前继续核实所有权",error);}}
    private Task StopHotkeyScreenshot()
    {
        Exception? cancellationFailure=null;try{screenshotHotkeyStop?.Cancel();}catch(Exception error){cancellationFailure=error;}
        // Clear, repeated Clear and exit share the same pending ownership settlement.
        if(!screenshotHotkeyCleanup.IsCompleted)return cancellationFailure==null?screenshotHotkeyCleanup:PreserveScreenshotCancellationFailure(screenshotHotkeyCleanup,cancellationFailure);
        return screenshotHotkeyCleanup=FinishHotkeyScreenshot(cancellationFailure);
    }
    private static async Task PreserveScreenshotCancellationFailure(Task pending,Exception failure)
    {List<Exception> errors=[failure];try{await pending;}catch(Exception error){errors.Add(error);}throw new ScreenshotCleanupException(errors);}
    private async Task FinishHotkeyScreenshot(Exception? cancellationFailure)
    {
        List<Exception> errors=[];if(cancellationFailure!=null)errors.Add(cancellationFailure);
        try{await screenshotHotkeyTask;}catch(Exception error){errors.Add(error);}
        if(screenshotHotkeyOwner is {} owner)
        {
            try{if(!await owner.TryFinalizeAsync(true))throw new IOException("Screenshot hotkey owner exit unconfirmed.");screenshotHotkeyOwner=null;}
            catch(Exception error){errors.Add(error);}
        }
        if(errors.Count>0)throw new ScreenshotCleanupException(errors);
    }
}
