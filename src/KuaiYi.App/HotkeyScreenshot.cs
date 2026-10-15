using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.Imaging;
using KuaiYi.Windows;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private Task StartHotkeyScreenshot(HotkeyInputIdentity input)
    {
        if(!lifecycle.Gate.IsHotkeyInputCurrent(input))return Task.CompletedTask;
        if(!screenshotHotkeyCleanup.IsCompleted)return screenshotHotkeyCleanup;
        if(screenshotHotkeyCleanup.IsFaulted||screenshotHotkeyCleanup.IsCanceled||screenshotHotkeyTask.IsFaulted||screenshotHotkeyTask.IsCanceled){button.ToolTip="旧截图清理尚未确认；暂不开始新截图。";return Task.CompletedTask;}
        if(!screenshotHotkeyTask.IsCompleted){screenshotHotkeyStop?.Cancel();return screenshotHotkeyTask;}
        if(screenshotHotkeyOwner!=null){button.ToolTip="旧截图进程退出尚未确认；暂不再启动。";return Task.CompletedTask;}
        if(!IsVisible){ShowOverlay();button.ToolTip="已显示浮窗，请再次按截图快捷键开始框选。";return Task.CompletedTask;}long revision=resultRevision;screenshotHotkeyTask=PrepareHotkeyScreenshot(input,revision);_=ObserveHotkeyScreenshot(screenshotHotkeyTask,revision);return screenshotHotkeyTask;
    }
    private async Task PrepareHotkeyScreenshot(HotkeyInputIdentity input,long revision)
    {
        using var intent=new ScreenshotIntent(lifecycle.Gate,input);using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);screenshotHotkeyStop=cancellation;ScreenshotSnapshot? snapshot=null;ScreenshotSelection? selection=null;ScreenshotCleanupException? previousCleanupFailure=null;
        var visible=Application.Current.Windows.Cast<Window>().Where(w=>w.IsVisible).ToArray();var closed=new HashSet<Window>();void OnClosed(object? sender,EventArgs args){if(sender is Window window)closed.Add(window);}foreach(var window in visible)window.Closed+=OnClosed;var poll=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};poll.Tick+=(_,_)=>{if(!intent.IsCurrent||exiting)cancellation.Cancel();};poll.Start();Refresh();
        var mayRestore=CaptureScreenshotRestoreGuard(cancellation);
        void Restore(){if(!mayRestore())return;foreach(var window in visible)if(!closed.Contains(window)&&!window.IsVisible)window.Show();}
        using var onCancel=cancellation.Token.Register(()=>Dispatcher.BeginInvoke(Restore));
        try
        {
            var admission=CaptureImageAdmission(cancellation);
            long[] handles=visible.Select(w=>new WindowInteropHelper(w).Handle.ToInt64()).Order().ToArray();if(!CaptureBoundary.VisibleWindowsForProcess(Environment.ProcessId).SequenceEqual(handles))throw new CaptureException(CaptureFailure.HiddenWindowNotVerified);
            foreach(long handle in handles)if(!ScreenshotDesktop.ExcludeWindow(new(handle)))throw new CaptureException(CaptureFailure.HiddenWindowNotVerified);foreach(var window in visible)window.Hide();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle).Task.WaitAsync(cancellation.Token);
            if(!CaptureBoundary.HiddenForProcess(handles,Environment.ProcessId,true))throw new CaptureException(CaptureFailure.HiddenWindowNotVerified);
            var owner=new CaptureHostClient(Path.Combine(AppContext.BaseDirectory,"KuaiYi.CaptureHost.exe"));screenshotHotkeyOwner=owner;var job=new CaptureJob(Guid.NewGuid(),lifecycle.Gate.Snapshot.Epoch,Environment.ProcessId,handles,CaptureBackend.WindowsGraphicsCapture);
            snapshot=await ScreenshotAcquisition.AcquireAsync(intent,token=>owner.RunAsync(job,token),cancellation.Token);
            if(await owner.TryFinalizeAsync())screenshotHotkeyOwner=null;if(screenshotHotkeyOwner!=null)throw new CaptureException(CaptureFailure.ExitUnconfirmed);
            selection=new(snapshot,()=>intent.IsCurrent);var region=await selection.RunAsync(cancellation.Token);selection=null;if(region==null||!intent.IsCurrent)return;
            ScreenshotPixels? crop=snapshot.CropAndRelease(region.Value,new(ScreenshotDesktop.Displays().Select(d=>d.Monitor)),cancellation.Token);snapshot=null;Restore();
            try
            {
                if(!intent.IsCurrent||cancellation.IsCancellationRequested)return;
                var image=new OwnedSourceImage(crop);crop=null;poll.Stop();AcceptLocalImage(admission,intent,image);
            }
            finally{crop?.Dispose();}
        }
        catch(OperationCanceledException)when(cancellation.IsCancellationRequested){ }
        catch(ScreenshotCleanupException error){previousCleanupFailure=error;}
        catch(Exception error){if(intent.IsCurrent)Report("截图入口尚不可用或未完成；没有发送",error);}
        finally
        {
            List<Exception> errors=[];if(previousCleanupFailure!=null)errors.Add(previousCleanupFailure);ScreenshotPreviewCleanup.Attempt(errors,poll.Stop);
            ScreenshotPreviewCleanup.Attempt(errors,()=>selection?.Dispose());ScreenshotPreviewCleanup.Attempt(errors,()=>snapshot?.Dispose());
            ScreenshotPreviewCleanup.Attempt(errors,intent.Dispose);ScreenshotPreviewCleanup.Attempt(errors,Restore);EndScreenshotRestore(cancellation);
            foreach(var window in visible)ScreenshotPreviewCleanup.Attempt(errors,()=>window.Closed-=OnClosed);
            // Pending owner stays held for the shared Stop retry. It is not a lost bitmap/mask failure.
            if(screenshotHotkeyOwner is {} owner)try{if(await owner.TryFinalizeAsync())screenshotHotkeyOwner=null;}catch(Exception error){if(!exiting&&revision==resultRevision&&CanReadOriginalSource())Report("截图进程清理待核实，保留原所有权",error);}
            ScreenshotPreviewCleanup.Attempt(errors,Refresh);if(errors.Count>0)throw new ScreenshotCleanupException(errors);
        }
    }
}
