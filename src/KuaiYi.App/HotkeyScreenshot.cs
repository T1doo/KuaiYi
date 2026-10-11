using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KuaiYi.Imaging;
using KuaiYi.Windows;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private Task screenshotHotkeyTask=Task.CompletedTask;private CancellationTokenSource? screenshotHotkeyStop;private CaptureHostClient? screenshotHotkeyOwner;
    private Task StartHotkeyScreenshot()
    {
        if(!screenshotHotkeyTask.IsCompleted){screenshotHotkeyStop?.Cancel();return screenshotHotkeyTask;}
        if(screenshotHotkeyOwner!=null){button.ToolTip="旧截图进程退出尚未确认；暂不再启动。";return Task.CompletedTask;}
        if(!IsVisible)ShowOverlay();return screenshotHotkeyTask=ObserveHotkeyScreenshot();
    }
    private async Task ObserveHotkeyScreenshot()
    {try{await PrepareHotkeyScreenshot();}catch(Exception error){Report("截图原操作未正常结束；退出前继续核实所有权",error);}}
    private async Task PrepareHotkeyScreenshot()
    {
        using var intent=new ScreenshotIntent(lifecycle.Gate);using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);screenshotHotkeyStop=cancellation;ScreenshotSnapshot? snapshot=null;ScreenshotSelection? selection=null;
        var visible=Application.Current.Windows.Cast<Window>().Where(w=>w.IsVisible).ToArray();var closed=new HashSet<Window>();void OnClosed(object? sender,EventArgs args){if(sender is Window window)closed.Add(window);}foreach(var window in visible)window.Closed+=OnClosed;var poll=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};poll.Tick+=(_,_)=>{if(!intent.IsCurrent||exiting)cancellation.Cancel();};poll.Start();Refresh();
        void Restore(){if(exiting||lifecycle.Gate.Snapshot.Suspension.HasFlag(KuaiYi.Core.Suspension.SessionLocked))return;foreach(var window in visible)if(!closed.Contains(window)&&!window.IsVisible)window.Show();}
        using var onCancel=cancellation.Token.Register(()=>Dispatcher.BeginInvoke(Restore));
        try
        {
            long[] handles=visible.Select(w=>new WindowInteropHelper(w).Handle.ToInt64()).Order().ToArray();if(!CaptureBoundary.VisibleWindowsForProcess(Environment.ProcessId).SequenceEqual(handles))throw new CaptureException(CaptureFailure.HiddenWindowNotVerified);
            foreach(long handle in handles)if(!ScreenshotDesktop.ExcludeWindow(new(handle)))throw new CaptureException(CaptureFailure.HiddenWindowNotVerified);foreach(var window in visible)window.Hide();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle).Task.WaitAsync(cancellation.Token);
            if(!CaptureBoundary.HiddenForProcess(handles,Environment.ProcessId,true))throw new CaptureException(CaptureFailure.HiddenWindowNotVerified);
            var owner=new CaptureHostClient(Path.Combine(AppContext.BaseDirectory,"KuaiYi.CaptureHost.exe"));screenshotHotkeyOwner=owner;var job=new CaptureJob(Guid.NewGuid(),lifecycle.Gate.Snapshot.Epoch,Environment.ProcessId,handles,CaptureBackend.WindowsGraphicsCapture);
            snapshot=await ScreenshotAcquisition.AcquireAsync(intent,token=>owner.RunAsync(job,token),cancellation.Token);
            if(await owner.TryFinalizeAsync())screenshotHotkeyOwner=null;if(screenshotHotkeyOwner!=null)throw new CaptureException(CaptureFailure.ExitUnconfirmed);
            selection=new(snapshot,()=>intent.IsCurrent);var region=await selection.RunAsync(cancellation.Token);selection=null;if(region==null||!intent.IsCurrent)return;
            using var crop=snapshot.CropAndRelease(region.Value,new(ScreenshotDesktop.Displays().Select(d=>d.Monitor)),cancellation.Token);snapshot=null;Restore();
            var bitmap=new WriteableBitmap(crop.Bounds.Width,crop.Bounds.Height,96,96,PixelFormats.Bgra32,null);using(var memory=crop.Bytes.Pin())unsafe{bitmap.WritePixels(new(0,0,bitmap.PixelWidth,bitmap.PixelHeight),(nint)memory.Pointer,crop.Bytes.Length,bitmap.PixelWidth*4);}
            var preview=new Window{Owner=this,Title="快译 · 本地截图预览（翻译尚未接入）",Width=500,Height=380,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel{Margin=new(16)};panel.Children.Add(new Image{Source=bitmap,Height=270,Stretch=Stretch.Uniform});var close=new Button{Content="关闭本地预览",IsCancel=true,IsDefault=true};close.Click+=(_,_)=>preview.Close();panel.Children.Add(new TextBlock{Text="本次仅准备所选区域；不启用翻译、不上传，模型调度尚未接入。",TextWrapping=TextWrapping.Wrap});panel.Children.Add(close);preview.Content=panel;
            using var cancelled=cancellation.Token.Register(()=>Dispatcher.BeginInvoke(()=>{if(preview.IsLoaded)preview.Close();}));try{if(intent.IsCurrent)preview.ShowDialog();}finally{preview.Close();byte[] zero=new byte[bitmap.PixelWidth*4];for(int y=0;y<bitmap.PixelHeight;y++)bitmap.WritePixels(new(0,y,bitmap.PixelWidth,1),zero,zero.Length,0);}
        }
        catch(OperationCanceledException)when(cancellation.IsCancellationRequested){ }
        catch(Exception error){if(intent.IsCurrent)Report("截图入口尚不可用或未完成；没有发送",error);}
        finally{poll.Stop();selection?.Dispose();snapshot?.Dispose();intent.Dispose();screenshotHotkeyStop=null;Restore();foreach(var window in visible)window.Closed-=OnClosed;if(screenshotHotkeyOwner is {} owner&&await owner.TryFinalizeAsync())screenshotHotkeyOwner=null;Refresh();}
    }
    private async Task StopHotkeyScreenshot(){screenshotHotkeyStop?.Cancel();await screenshotHotkeyTask;if(screenshotHotkeyOwner is {} owner){if(!await owner.TryFinalizeAsync(true))throw new IOException("Screenshot hotkey owner exit unconfirmed.");screenshotHotkeyOwner=null;}}
}
