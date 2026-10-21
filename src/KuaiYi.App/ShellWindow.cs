using System.IO;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Windows;
using DpiScale=KuaiYi.Core.DpiScale;
namespace KuaiYi.App;
/// <summary>P1 production startup; platform experiments remain behind --platform-review.</summary>
internal sealed partial class ShellWindow:OleDropWindow
{
    private readonly Button button=new(){Content="译",FontSize=18,Foreground=Brushes.White,Background=Brushes.Firebrick,BorderThickness=new Thickness(0)};
    private readonly ShellLifecycle lifecycle;private readonly SettingsCoordinator coordinator;
    private readonly OverlayPointerGesture gesture=new();private readonly CancellationTokenSource lifetime=new();
    private readonly uint showMessage;private NativeTray? tray;private nint hwnd;private PixelRect anchor;private DpiScale gestureDpi;
    private SettingsWindow? settings;private Task startup=Task.CompletedTask,positionSave=Task.CompletedTask;private bool opening,exiting,allowClose,placementPending,geometryReady;
    private OverlayPosition? pendingPosition;private bool pointerFinished,exitRequested,positionTouched;private MonitorGeometry? anchorMonitor;
    private readonly bool enableSelectionPlatform;
    public ShellWindow(string instanceScope,SettingsCoordinator? suppliedCoordinator=null,bool enableSelectionPlatform=true,KuaiYi.Providers.DevelopmentTranslationBoundary? developmentBoundary=null)
    {
        this.enableSelectionPlatform=enableSelectionPlatform;
        diagnostics.Record(KuaiYi.Core.Diagnostics.DiagnosticEvent.Started);
        coordinator=suppliedCoordinator??DefaultSettings();
        lifecycle=new(coordinator);showMessage=NativeTray.ShowMessage(instanceScope);
        Title="快译";Width=Height=48;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;ShowActivated=false;Topmost=true;
        SetupPresentation();SetupTranslation(developmentBoundary);SetupBrowserImages(enableSelectionPlatform&&developmentBoundary==null);AutomationProperties.SetName(button,"快译：未启用翻译");button.ToolTip="未启用翻译 · 右键打开设置";
        var menu=new ContextMenu();Add("启用 / 停用翻译",Toggle);Add("回看当前结果",ReopenResult);Add("收起",CollapseResult);Add("停止生成",StopResult);Add("复制结果",CopyResult);Add("清除当前内容",()=>_=ClearCurrentContent());Add("设置",()=>_=OpenSettings());Add("更多操作及版本",()=>OpenOverflowMenu(button));Add("隐藏浮窗（同时停用）",HideOverlay);Add("聚焦浮窗",FocusOverlay);Add("退出",Close);
#if KUAIYI_ISOLATED_SESSION_TEST
        Title="快译 · 独立目录启动验收";menu.Items.Insert(0,new MenuItem{Header="独立目录启动验收 · 非生产安装包",IsEnabled=false});
#endif
        AddBrowserImageStatus(menu);AddImageFileDropMenu(menu);Add("打开图片",()=>_=OpenImageFile());Add("启用并翻译打开的图片",EnableAndTranslateOpenedImage);Add("读取当前选区",()=>_=ObserveSelection(ReadCurrentSelection()));Add("仅提取图片文字并翻译",()=>_=TranslateImageText());Add("重新生成",()=>RegenerateResult());Add("按当前设置重新翻译",()=>RegenerateResult(true));Add("查看原文",ShowSelectionSource);AddScreenshotModes(menu);menu.Opened+=(_,_)=>{foreach(var item in menu.Items.OfType<MenuItem>().Where(i=>Equals(i.Tag,"image-modes"))){item.Header=SelectedSource.Image?.Kind==KuaiYi.Imaging.ImageSourceKind.Browser?"本次浏览器图片翻译":SelectedSource.Image?.Kind==KuaiYi.Imaging.ImageSourceKind.LocalFile?"本次文件图片翻译":"本次截图翻译";item.IsEnabled=CanTranslateScreenshot;}};button.ContextMenu=menu;menu.Closed+=(_,_)=>PresentationMenuClosed();
        menu.Opened+=(_,_)=>{foreach(var item in menu.Items.OfType<MenuItem>()){if(Equals(item.Tag,"启用并翻译打开的图片"))item.IsEnabled=SelectedSource.Image is KuaiYi.Imaging.OwnedFileImage&&lifecycle.Gate.Snapshot.Activation==Activation.Off&&!FileImageBusy;else if(Equals(item.Tag,"仅提取图片文字并翻译"))item.IsEnabled=!ocrFlowBusy&&ocrOnly is {CleanupPending:false}&&lifecycle.Gate.Snapshot.Activation==Activation.On&&SelectedSource.Image is KuaiYi.Imaging.ImageSourceOwner;else if(Equals(item.Tag,"重新生成")||Equals(item.Tag,"按当前设置重新翻译")){item.Header=RegenerationMenuLabel(Equals(item.Tag,"按当前设置重新翻译"));item.IsEnabled=lifecycle.Gate.Snapshot.Activation==Activation.On&&SelectedSource.Text!=null;}else if(Equals(item.Tag,"查看原文"))item.IsEnabled=CanReadOriginalSource()&&SelectedSource.HasSource;}};
        void Add(string text,Action action){var item=new MenuItem{Header=text,Tag=text};item.Click+=(_,_)=>action();menu.Items.Add(item);}
        SourceInitialized+=(_,_)=>
        {
            hwnd=new WindowInteropHelper(this).Handle;((HwndSource)PresentationSource.FromVisual(this)).AddHook(Hook);
            NativeDesktop.SetOverlayStyle(hwnd,false);
            try{tray=new(hwnd);}catch(Exception error){Report("托盘不可用",error);throw;} // Never leave an inaccessible hidden application.
            Reposition(null);
            SetupSelection();SetupTextDrop();
        };
        Loaded+=(_,_)=>{startup=LoadStartup();SetupHotkeys();};
        button.PreviewMouseLeftButtonDown+=(_,e)=>Pointer(()=>
        {
            if(exiting||!geometryReady||!NativeDesktop.GetCursorPos(out var p))return;
            gestureDpi=MonitorTopology.Read().At(anchor).Dpi;
            if(gesture.Down(new(p.X,p.Y),anchor,true)==OverlayPointerAction.Pressed)
            {pointerFinished=false;lifecycle.Gate.SetSuspension(Suspension.Moving,true);RefreshSelection();BeginOverlayMove();if(!button.CaptureMouse())throw new InvalidOperationException("Pointer capture failed.");e.Handled=true;}
        });
        button.PreviewMouseMove+=(_,e)=>Pointer(()=>
        {
            if(!gesture.Active||!NativeDesktop.GetCursorPos(out var p))return;
            var action=gesture.Move(new(p.X,p.Y),out var desired,gestureDpi.CeilingPixels(SystemParameters.MinimumHorizontalDragDistance),gestureDpi.CeilingPixels(SystemParameters.MinimumVerticalDragDistance),gestureDpi);
            if(action!=OverlayPointerAction.Moved)return;
            positionTouched=true;
            try{MoveOverlay(desired,MonitorTopology.Read());}
            catch(Exception error){CancelPointer();Report("移动位置校验失败",error);}e.Handled=true;
        });
        button.PreviewMouseLeftButtonUp+=(_,e)=>Pointer(()=>
        {
            var action=gesture.Up();pointerFinished=true;button.ReleaseMouseCapture();lifecycle.Gate.SetSuspension(Suspension.Moving,false);RefreshSelection();EndOverlayMove();e.Handled=true;
            if(action==OverlayPointerAction.Clicked)Toggle();
            else if(action==OverlayPointerAction.Released){pendingPosition=CurrentPosition();QueuePositionSave();}
        });
        button.LostMouseCapture+=(_,_)=>{if(!pointerFinished)CancelPointer();pointerFinished=false;};
        // Keyboard activation uses Button.Click; handled pointer events suppress WPF's duplicate click.
        button.Click+=(_,_)=>Toggle();
        Deactivated+=(_,_)=>{if(hwnd!=0)NativeDesktop.SetOverlayStyle(hwnd,false);};
        coordinator.Committed+=SettingsCommitted;Microsoft.Win32.SystemEvents.SessionSwitch+=SessionSwitch;
        Closing+=ClosingShell;Closed+=ClosedShell;
    }
    private static SettingsCoordinator DefaultSettings()
    {
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KuaiYi");
        return new(new SettingsStore(Path.Combine(root,"config")),new SecretFileStore(Path.Combine(root,"secrets"),new CurrentUserDpapiProtector(),new CurrentUserSecretDirectory()));
    }
    private async Task LoadStartup()
    {
        try{await lifecycle.InitializeAsync(lifetime.Token);if(!exiting){var data=lifecycle.Settings!.Data;if(!positionTouched)Reposition(data.Overlay.RememberPosition?data.Overlay.LastPosition:null);Refresh();startupHealthy=true;}}
        catch(OperationCanceledException)when(lifetime.IsCancellationRequested){ /* Owned exit waits for startup settlement. */ }
        catch(Exception error){Report("配置无法加载，原文件保留；请打开设置恢复",error);}
    }
    private void SettingsCommitted(SettingsSnapshot saved,SettingsChange __){CancelBrowserAcquisition();translation?.ObserveSettings(saved);imageTranslation?.ObserveSettings(saved);apiTests?.Cancel();if(globalHotkeys!=null)globalHotkeys.ReconcileAsync(saved).WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();selectionCapture?.CancelPending();RevokeTextDrop();Dispatcher.BeginInvoke(()=>{if(!exiting)Refresh();});}
    private void Toggle()
    {
        if(exiting)return;
        CancelBrowserAcquisition();
        if(!IsVisible)ShowOverlay();if(!IsVisible)return;
        if(lifecycle.Gate.Snapshot.Activation==Activation.On){lifecycle.Gate.Disable();StopPresentation();Refresh();return;}
        if(lifecycle.Settings is {} current&&translation?.CanUseSettings(current)==false){button.ToolTip="测试模式只允许启动时授权的无密钥本地服务，请恢复测试配置。";return;}
        if(!lifecycle.TryPrepareEnable(out var intent,out var missing)){button.ToolTip=missing;_=OpenSettings();return;}
        bool consent=lifecycle.Gate.HasConsentTo(intent!.Origin)||MessageBox.Show(this,intent.ConfirmationText,"快译 · 数据发送说明",MessageBoxButton.OKCancel,MessageBoxImage.Information)==MessageBoxResult.OK;
        lifecycle.ConfirmEnable(intent!,consent);Refresh();
    }
    private void Refresh()
    {
        ObserveFileImageState();ObserveOcrState();ObserveScreenshotState();
        RefreshSelection();RefreshTextDrop();
        RefreshPresentationTheme();
        RefreshResultLabel();
        bool on=lifecycle.Gate.Snapshot.Activation==Activation.On;button.Background=on?OverlayTokens.On:OverlayTokens.Off;UpdateButtonGlyph(on);
        AutomationProperties.SetName(button,on?"快译：已启用翻译":"快译：未启用翻译");button.ToolTip=on?translationStatus??"已启用翻译 · 单击停用":dropUnavailable??"未启用翻译 · 单击启用，右键设置";
        try{tray?.Update(on);}catch(Exception error){Report("托盘状态更新失败",error);}
    }
    private async Task OpenSettings()
    {
        if(exiting)return;CancelBrowserAcquisition();if(settings!=null){settings.Activate();return;}if(opening)return;opening=true;
        try
        {
            await startup;await hotkeySetup;await positionSave;if(exiting)return;lifecycle.Gate.SetSuspension(Suspension.Settings,true);RefreshSelection();
            var draft=await coordinator.OpenDraftAsync(lifetime.Token);
            if(exiting){draft.Dispose();return;}
            try{settings=new(coordinator,draft,selectionCapture?.LastFailureWindow?.Executable,globalHotkeys,()=>globalHotkeys?.Status??[],apiTests,ClearCurrentContent,diagnostics,ReadDiagnosticEnvironment){Owner=this};}catch{draft.Dispose();throw;}
            settings.Closed+=(_,_)=>{settings=null;lifecycle.Gate.SetSuspension(Suspension.Settings,false);Refresh();QueuePositionSave();if(exitRequested)Dispatcher.BeginInvoke(Close);};settings.Show();
        }
        catch(OperationCanceledException)when(lifetime.IsCancellationRequested){ /* Exit awaits this owned opening path. */ }
        catch(Exception error){lifecycle.Gate.SetSuspension(Suspension.Settings,false);Report("设置无法打开，原文件保留",error);}
        finally{opening=false;if(exitRequested)_=Dispatcher.BeginInvoke(Close);}
    }
    private void HideOverlay(){CancelPointer();lifecycle.Hide();HidePresentation(false);Hide();Refresh();}
    private void ShowOverlay()
    {
        if(exiting||lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.SessionLocked))return;
        if(!IsVisible){lifecycle.Show();Reposition(CurrentPosition());if(geometryReady)Show();}Refresh();
    }
    private void FocusOverlay(){ShowOverlay();if(!IsVisible)return;NativeDesktop.SetOverlayStyle(hwnd,true);Activate();if(overlayFrames?.IsExpanded==true&&(overlayResults.HasText||SelectedSource.Image!=null))body.Focus();else button.Focus();}
    private OverlayPosition? CurrentPosition()=>geometryReady&&anchorMonitor!=null?OverlayPositionMapper.Capture(anchor,anchorMonitor):lifecycle.Settings?.Data.Overlay.LastPosition;
    private void SessionSwitch(object sender,Microsoft.Win32.SessionSwitchEventArgs e)
    {
        if(e.Reason is not (Microsoft.Win32.SessionSwitchReason.SessionLock or Microsoft.Win32.SessionSwitchReason.SessionUnlock))return;
        bool locked=e.Reason==Microsoft.Win32.SessionSwitchReason.SessionLock;List<Exception> errors=[];
        ScreenshotPreviewCleanup.Attempt(errors,()=>lifecycle.SetSessionLocked(locked));ScreenshotPreviewCleanup.Attempt(errors,()=>selectionCapture?.CancelPending(clearSource:locked));ScreenshotPreviewCleanup.Attempt(errors,()=>RevokeTextDrop(clearSource:locked)); // Gate invalidation precedes queued UI, even if a cancellation callback fails.
        Dispatcher.BeginInvoke(()=>ApplySessionPresentation(locked,()=>settings?.RequestCloseWhenSettled(),errors));
    }
    private nint Hook(nint window,int message,nint w,nint l,ref bool handled)
    {
        try
        {
            if((uint)message==showMessage){ShowOverlay();handled=true;}
            else if(tray!=null&&(uint)message==tray.TaskbarCreated){tray.Restore();RecoverHotkeys(lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.SessionLocked));Refresh();handled=true;}
            else if(message==NativeTray.CallbackMessage)
            {
                int eventCode=(int)(l.ToInt64()&0xffff);handled=true;
                if(eventCode==0x007b){switch(tray!.Menu(lifecycle.Gate.Snapshot.Activation==Activation.On,IsVisible)){case 1:Toggle();break;case 2:if(IsVisible)HideOverlay();else ShowOverlay();break;case 3:_=OpenSettings();break;case 4:FocusOverlay();break;case 5:Close();break;}}
                else if(eventCode is 0x0400 or 0x0401)ShowOverlay();
            }
            else if(message==0x02e0)DpiPlacement();
            else if(message is 0x007e or 0x001a)SchedulePlacement();
        }
        catch(Exception error){lifecycle.Gate.Disable();if(!IsVisible&&geometryReady&&!lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.SessionLocked))Show();Refresh();Report("系统入口操作失败",error);}
        return 0;
    }
    private void SchedulePlacement()
    {
        if(placementPending)return;placementPending=true;Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>{placementPending=false;if(exiting)return;Pointer(DpiPlacement);});
    }
    private void Reposition(OverlayPosition? saved)
    {
        try{var monitors=MonitorTopology.Read();anchor=OverlayPositionMapper.Restore(saved,monitors.Monitors,monitors.PrimaryId);anchorMonitor=monitors.At(anchor);ApplyAnchor();geometryReady=true;}
        catch(Exception error){geometryReady=false;lifecycle.Hide();Dispatcher.BeginInvoke(()=>{Hide();Refresh();Report("当前屏幕无法容纳浮钮，请调整显示设置后从托盘恢复",error);});}
    }
    private void ApplyAnchor()
    {if(anchorMonitor==null)return;if(overlayFrames==null)overlayFrames=new(anchor,anchorMonitor);else{overlayFrames.Reanchor(anchor,anchorMonitor,Preferences());if(overlayFrames.IsExpanded&&!peek)OpenBody();}ApplyOverlayFrame(forceNative:true);StartFrames();}
    private void Pointer(Action action)
    {try{action();}catch(Exception error){CancelPointer();lifecycle.Gate.Disable();Refresh();Report("浮钮操作失败",error);}}
    private void CancelPointer(){bool moving=gesture.Active,changed=gesture.HasMoved;gesture.Cancel();pointerFinished=true;button.ReleaseMouseCapture();lifecycle.Gate.SetSuspension(Suspension.Moving,false);RefreshSelection();if(moving)EndOverlayMove();if(changed){pendingPosition=CurrentPosition();QueuePositionSave();}}
    private void QueuePositionSave()
    {
        if(opening||settings!=null||exiting||pendingPosition==null)return;
        var position=pendingPosition;pendingPosition=null;positionSave=PersistPosition(positionSave,position);
    }
    private async Task PersistPosition(Task previous,OverlayPosition position)
    {
        await previous;await startup;
        try{await lifecycle.SavePositionAsync(position,lifetime.Token);}catch(OperationCanceledException)when(lifetime.IsCancellationRequested){}
        catch(Exception error){Report("位置未保存，原配置保留",error);}
    }
    private Task<bool> DispatchCloseAttempt()=>Dispatcher.InvokeAsync(TryCloseAttemptAsync).Task.Unwrap();
    private Task CommitShellCloseAsync()=>Dispatcher.InvokeAsync(Close).Task;
    // Dispatch only; ShellCloseRequests owns/joins the original asynchronous close, so the control loop can receive STOP2.
    private Task RequestInstallationCloseAsync(CancellationToken _)=>Dispatcher.InvokeAsync(Close).Task;
    private void ClosedShell(object? sender,EventArgs e)
    {
        try
        {
            Microsoft.Win32.SystemEvents.SessionSwitch-=SessionSwitch;coordinator.Committed-=SettingsCommitted;DisposePresentation();lifecycle.Dispose();lifetime.Cancel();lifetime.Dispose();
            try{tray?.Dispose();}catch(Exception error){diagnostics.RecordFailure(error);Console.Error.WriteLine("KuaiYi tray cleanup: "+KuaiYi.Core.Diagnostics.DiagnosticLog.Classify(error));}
        }
        finally{diagnostics.Dispose();}
    }
    private void Report(string text,Exception error){diagnostics.RecordFailure(error);var category=KuaiYi.Core.Diagnostics.DiagnosticLog.Classify(error);button.ToolTip=text+" · "+category;Console.Error.WriteLine("KuaiYi shell: "+category);}
}
