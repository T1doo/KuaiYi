using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.Imaging;
using KuaiYi.Windows;
namespace KuaiYi.App;
internal sealed partial class ImageProbeWindow
{
    private readonly Button screenshot=new(){Content="框选截图 · 本地 OCR"};
    private readonly ComboBox captureBackend=new(){ItemsSource=new[]{"Windows Graphics Capture（首选）","GDI（明确兼容后端）"},SelectedIndex=0};
    private readonly CheckBox capturePreview=new(){Content="截图完成后总是本地预览"};
    private readonly TextBox captureKey=new(){Text="S",MaxLength=1,Width=35};
    private readonly CheckBox keyCtrl=new(){Content="Ctrl",IsChecked=true},keyAlt=new(){Content="Alt",IsChecked=true},keyShift=new(){Content="Shift"};
    private ScreenshotHotkey? hotkey;
    private Task? captureTask;
    private readonly object captureSync=new();
    private CancellationTokenSource? captureStop;
    private void CancelScreenshot(){lock(captureSync)captureStop?.Cancel();}
    private void SetupScreenshot(StackPanel panel)
    {
        panel.Children.Add(captureBackend);panel.Children.Add(capturePreview);panel.Children.Add(screenshot);
        var keys=new StackPanel{Orientation=Orientation.Horizontal};keys.Children.Add(keyCtrl);keys.Children.Add(keyAlt);keys.Children.Add(keyShift);keys.Children.Add(captureKey);
        var register=new Button{Content="注册截图快捷键"};var clear=new Button{Content="清除快捷键"};keys.Children.Add(register);keys.Children.Add(clear);panel.Children.Add(keys);
        screenshot.Click+=async(_,_)=>await StartScreenshot();
        SourceInitialized+=(_,_)=>{hotkey=new((HwndSource)PresentationSource.FromVisual(this),()=>_ = StartScreenshot(),CancelScreenshot);ScreenshotDesktop.ExcludeWindow(new WindowInteropHelper(this).Handle);};
        register.Click+=(_,_)=>
        {
            uint modifiers=(keyCtrl.IsChecked==true?2u:0)|(keyAlt.IsChecked==true?1u:0)|(keyShift.IsChecked==true?4u:0);string text=captureKey.Text.ToUpperInvariant();
            status.Text=text.Length==1&&hotkey!.Set(modifiers,text[0])?"截图快捷键已注册（本次运行）":"快捷键无效或已占用；原快捷键保留。";
        };
        clear.Click+=(_,_)=>status.Text=hotkey!.Set(0,0)?"截图快捷键未分配":"快捷键清除失败；原快捷键保留。";
        Height=580;
    }
    private static string CaptureFailureText(ScreenshotFailure code)=>code switch
    {
        ScreenshotFailure.SnapshotTooLarge=>"显示器快照总像素超限，请减少启用的显示器或降低分辨率。",
        ScreenshotFailure.CaptureUnavailable=>"截图后端不可用，请检查本机原生组件与系统支持。",
        ScreenshotFailure.ProtectedOrBlackFrame=>"捕获画面为全黑或受保护，无法确认有效内容。",
        ScreenshotFailure.TopologyChanged=>"屏幕配置已变化，请重新截图。",
        ScreenshotFailure.Timeout=>"截图等待超时，请重新尝试。",
        _=>$"截图未完成 · {code}；请重新选择较小区域。"
    };
    private async Task StartScreenshot()
    {
        if(captureTask is {IsCompleted:false}||running!=null||ownedWorker!=null||gate.Snapshot.Suspension!=Suspension.None||OwnedWindows.Cast<Window>().Any(window=>window.IsVisible))return;
        captureTask=Capture();await captureTask;
    }
    private async Task Capture()
    {
        using var intent=new ScreenshotIntent(gate);using var stop=new CancellationTokenSource();lock(captureSync)captureStop=stop;
        bool visible=IsVisible,stillCurrent=false;string previousStatus=status.Text;ScreenshotSnapshot? snapshot=null;
        var backend=captureBackend.SelectedIndex==0?ScreenshotBackend.WindowsGraphicsCapture:ScreenshotBackend.GdiCompatibility;
        bool alwaysPreview=capturePreview.IsChecked==true;screenshot.IsEnabled=false;choose.IsEnabled=false;
        var poll=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
        ScreenshotSelection? selection=null;
        poll.Tick+=(_,_)=>{if(!intent.IsCurrent)stop.Cancel();};poll.Start();
        try
        {
            var displays=ScreenshotDesktop.Displays();var topology=new ScreenshotTopology(displays.Select(d=>d.Monitor));
            Hide();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);ScreenshotDesktop.FlushHiddenWindows();
            snapshot=await ScreenshotAcquisition.AcquireAsync(intent,token=>ScreenshotDesktop.CaptureAsync(displays,backend,token),stop.Token);
            stop.Token.ThrowIfCancellationRequested();if(!intent.IsCurrent)throw new OperationCanceledException();
            selection=new(snapshot,()=>intent.IsCurrent);var region=await selection.RunAsync(stop.Token);selection=null;
            if(region==null)return;
            if(!intent.IsCurrent)throw new OperationCanceledException();
            using var crop=snapshot.CropAndRelease(region.Value,new(ScreenshotDesktop.Displays().Select(d=>d.Monitor)),stop.Token);snapshot=null;
            // Full desktop/frame staging already released before PNG encoding or preview; only selected pixels remain.
            using var png=await Task.Run(()=>ScreenshotPngEncoder.Encode(crop,stop.Token));
            stop.Token.ThrowIfCancellationRequested();if(!intent.IsCurrent)throw new OperationCanceledException();
            if(visible)Show();
            bool confirmed=!intent.RequiresConfirmation(alwaysPreview)||ScreenshotPreview.Confirm(this,crop,gate.Snapshot.Activation==Activation.Off,stop.Token);
            if(!confirmed)return;
            stop.Token.ThrowIfCancellationRequested();if(!topology.Matches(new(ScreenshotDesktop.Displays().Select(d=>d.Monitor))))throw new ScreenshotException(ScreenshotFailure.TopologyChanged);
            if(!intent.TryCommit(confirmed,session,Guid.NewGuid(),out var current,out _))return;
            poll.Stop();using var revoke=stop.Token.Register(()=>gate.StopRequest(current));enabled.IsChecked=true;stillCurrent=false;
            await Recognize(png,current);
        }
        catch(OperationCanceledException){/* Explicit cancellation leaves old session/result intact. */}
        catch(Exception error)
        {
            if(intent.IsCurrent)status.Text=error is ScreenshotException capture?CaptureFailureText(capture.Code):$"截图未完成 · {error.GetType().Name}";
            previousStatus=status.Text;
        }
        finally
        {
            stillCurrent=intent.IsCurrent;poll.Stop();selection?.Dispose();snapshot?.Dispose();intent.Dispose();
            lock(captureSync)if(ReferenceEquals(captureStop,stop))captureStop=null;
            if(visible&&!gate.Snapshot.Suspension.HasFlag(Suspension.SessionLocked))Show();
            screenshot.IsEnabled=running==null&&ownedWorker==null;choose.IsEnabled=screenshot.IsEnabled;
            if(stillCurrent)status.Text=previousStatus; // Cancel only; committed/stale operations cannot rewrite a newer result.
        }
    }
}
