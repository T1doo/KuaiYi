using KuaiYi.Core;
using KuaiYi.Imaging;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private Task StartHotkeyScreenshot(HotkeyInputIdentity input)
    {
        if(!lifecycle.Gate.IsHotkeyInputCurrent(input))return Task.CompletedTask;
        return StartScreenshotCapture(input);
    }
    private Task StartMenuScreenshot(RequestIdentity recovery)
    {
        if(ScreenshotRecoveryIdentity!=recovery||!CanRecaptureScreenshot)return Task.CompletedTask;
        return StartScreenshotCapture(null); // Explicit menu intent; never synthesize a hotkey identity.
    }
    private Task StartScreenshotCapture(HotkeyInputIdentity? input)
    {
        if(exiting)return Task.CompletedTask;
        if(!screenshotHotkeyCleanup.IsCompleted)return screenshotHotkeyCleanup;
        if(screenshotHotkeyCleanup.IsFaulted||screenshotHotkeyCleanup.IsCanceled||screenshotHotkeyTask.IsFaulted||screenshotHotkeyTask.IsCanceled){button.ToolTip="旧截图清理尚未确认；暂不开始新截图。";return Task.CompletedTask;}
        if(!screenshotHotkeyTask.IsCompleted){screenshotHotkeyStop?.Cancel();return screenshotHotkeyTask;}
        if(screenshotHotkeyOwner!=null){button.ToolTip="旧截图进程退出尚未确认；暂不再启动。";return Task.CompletedTask;}
        if(!IsVisible){ShowOverlay();button.ToolTip="已显示浮窗，请再次按截图快捷键开始框选。";return Task.CompletedTask;}
        long revision=resultRevision;screenshotHotkeyTask=PrepareScreenshotCapture(input,revision);_=ObserveHotkeyScreenshot(screenshotHotkeyTask,revision);return screenshotHotkeyTask;
    }
    private async Task PrepareScreenshotCapture(HotkeyInputIdentity? input,long revision)
    {
        using var intent=new ScreenshotIntent(lifecycle.Gate,input);using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);screenshotHotkeyStop=cancellation;
        OwnedSourceImage? image=null;List<Exception> errors=[];
        try
        {
            var admission=CaptureImageAdmission(cancellation);
            await Task.Yield(); // Install the original tracked task before a capture port or notification can reenter.
            if(!ImageAdmissionCurrent(admission,admission.Epoch)||!intent.IsCurrent)return;
            image=await CaptureFinalScreenshot(intent,cancellation,revision);
            if(image==null)return;
            var transferred=image;image=null;AcceptLocalImage(admission,intent,transferred);
        }
        catch(OperationCanceledException)when(cancellation.IsCancellationRequested){ }
        catch(ScreenshotCleanupException error){errors.Add(error);}
        catch(Exception error){if(intent.IsCurrent&&!exiting)Report("截图入口尚不可用或未完成；没有发送",error);}
        finally
        {
            ScreenshotPreviewCleanup.Attempt(errors,()=>image?.Dispose());ScreenshotPreviewCleanup.Attempt(errors,intent.Dispose);EndScreenshotRestore(cancellation);
            ScreenshotPreviewCleanup.Attempt(errors,Refresh);if(errors.Count>0)throw new ScreenshotCleanupException(errors);
        }
    }
}
