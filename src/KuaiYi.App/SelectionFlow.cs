using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.NativeHost;
using KuaiYi.Windows;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private SelectionBrokerClient? selectionBroker;private ClipboardCaptureClient? clipboardCapture;private SelectionCaptureCoordinator? selectionCapture;private SelectionMouseHook? selectionHook;private SelectionInputBridge? selectionInput;
    private Task selectionReconcile=Task.CompletedTask,selectionEvents=Task.CompletedTask;private ScreenshotHotkey? selectionHotkey;
    private SelectionSourceSnapshot? selectionSource;private Window? sourceViewer;private TextBox? sourceText;
    private (long Epoch,string? Version)? selectionState;private string? registeredSelectionChord;
    private readonly Dictionary<(string Application,SelectionCaptureStatus Status),long> selectionFailureNotices=[];private long lastCopyRecoveryNotice;
    private sealed class SelectionPlatform(SelectionBrokerClient broker,ClipboardCaptureClient clipboard):ISelectionCapturePlatform,ISelectionCopyPlatform
    {public bool IsCurrent(SelectionWindow window)=>SelectionDesktop.IsCurrent(window);public bool IsCopyCurrent(SelectionWindow source,SelectionCopyEvidence evidence)=>CopyInputGuard.IsCurrent(source,evidence);public Task<SelectionCaptureResult> CaptureAsync(SelectionGesture gesture,CancellationToken cancellation)=>broker.CaptureAsync(gesture,cancellation);public Task<SelectionCaptureResult> CaptureCopiedAsync(SelectionGesture gesture,SelectionCopyEvidence evidence,Func<uint,ClipboardCopyStart?> issueCopy,Func<bool> current,CancellationToken cancellation)=>clipboard.CaptureAsync(gesture,evidence,issueCopy,current,cancellation);public ClipboardCopyStart? TryIssueCopy(SelectionGesture gesture,SelectionCopyEvidence evidence,uint sequence)=>CopyInputGuard.Send(gesture,evidence,sequence);}
    private void SetupSelection()
    {
        if(!enableSelectionPlatform)return;
        selectionBroker=new(Path.Combine(AppContext.BaseDirectory,"KuaiYi.CaptureBroker.exe"));
        clipboardCapture=new(Path.Combine(AppContext.BaseDirectory,"KuaiYi.ClipboardHost.exe"));
        var tracker=new SelectionGestureTracker(Math.Max(1,SelectionDesktop.GetSystemMetrics(68)),Math.Max(1,SelectionDesktop.GetSystemMetrics(69)),checked((int)SelectionDesktop.GetDoubleClickTime()),Math.Max(1,SelectionDesktop.GetSystemMetrics(36)),Math.Max(1,SelectionDesktop.GetSystemMetrics(37)));
        selectionCapture=new(lifecycle.Gate,()=>lifecycle.Settings,new SelectionPlatform(selectionBroker,clipboardCapture),tracker,(uint)Environment.ProcessId,new DispatcherSynchronizationContext(Dispatcher),AcceptSelection,SelectionFailed,clipboardRecoveryWarning:CopyRecoveryWarning);
        selectionInput=new(lifecycle.Gate,()=>lifecycle.Settings,()=>selectionHook?.Identity.HookId,input=>SelectionDesktop.Resolve(input.Foreground,input.Point),SelectionDesktop.IsCurrent,selectionCapture,new DispatcherSynchronizationContext(Dispatcher));
        selectionHotkey=new((HwndSource)PresentationSource.FromVisual(this),()=>_=ObserveSelection(ReadCurrentSelection()),SchedulePlacement);
    }
    private void RefreshSelection()
    {
        if(selectionCapture==null)return;var state=(lifecycle.Gate.Snapshot.Epoch,lifecycle.Settings?.Version);
        if(selectionState!=state){selectionState=state;selectionCapture.CancelPending();selectionReconcile=ReconcileSelection(selectionReconcile);}
        string? chord=lifecycle.Settings?.Data.Hotkeys.GetValueOrDefault("translateSelection");
        if(chord!=registeredSelectionChord&&chord!=null)
        {
            var parts=chord.Split('+');uint modifiers=0;bool valid=parts.Length>=2;
            foreach(string part in parts[..^1]){modifiers|=part switch{"Ctrl"=>2u,"Alt"=>1u,"Shift"=>4u,"Win"=>8u,_=>0};if(part is not ("Ctrl" or "Alt" or "Shift" or "Win"))valid=false;}
            uint key=parts[^1].Length==1?(uint)parts[^1][0]:0;
            if(valid&&selectionHotkey!.Set(modifiers,key))registeredSelectionChord=chord;else button.ToolTip="主动取词快捷键不可用；可从右键菜单读取选区。";
        }
        else if(chord==null&&registeredSelectionChord!=null&&selectionHotkey!.Set(0,0))registeredSelectionChord=null;
    }
    private async Task ReconcileSelection(Task previous)
    {
        try
        {
            await previous;var state=lifecycle.Gate.Snapshot;
            bool wanted=!exiting&&state.Activation==Activation.On&&state.Mode==TriggerMode.Selection&&state.Suspension==Suspension.None;
            var identity=wanted?selectionCapture!.FreezeInputIdentity(Guid.NewGuid()):null;wanted=wanted&&identity!=null;
            if(wanted&&selectionHook?.IsFaulted==true)throw new IOException("Previous selection hook cleanup is not confirmed.");
            bool replace=selectionHook!=null&&(!wanted||selectionHook.Identity.Ticket!=identity!.Ticket||selectionHook.Identity.SettingsVersion!=identity.SettingsVersion);
            if(replace){var old=selectionHook!;old.Stop();await old.DisposeAsync();await selectionEvents;selectionHook=null;}
            if(wanted&&selectionHook==null){var created=selectionHook=new(identity!);await created.Ready;selectionEvents=ConsumeSelection(created);}
        }
        catch(Exception error){if(selectionHook?.IsReleased==true)selectionHook=null;SelectionInfrastructureFailed("自动选区监听不可用；请停用后重试",error);}
    }
    private async Task ConsumeSelection(SelectionMouseHook hook)
    {
        long revision=hook.LossRevision;
        try
        {
            await foreach(var item in hook.Events.ReadAllAsync().ConfigureAwait(false))
            {
                if(item.LossRevision!=hook.LossRevision||item.LossRevision!=revision){selectionCapture?.CancelPending();revision=hook.LossRevision;if(item.LossRevision!=revision)continue;}
                var delivery=await selectionInput!.ReceiveAsync(item).ConfigureAwait(false);
                if(delivery.Admitted)await Dispatcher.InvokeAsync(()=>_=ObserveSelection(delivery.Capture));
            }
        }
        catch(Exception error){await Dispatcher.InvokeAsync(()=>{if(selectionHook==hook)SelectionInfrastructureFailed("选区监听已停止",error);});}
    }
    private void SelectionInfrastructureFailed(string message,Exception error)
    {
        lifecycle.Gate.Disable();selectionCapture?.CancelPending();try{selectionHook?.Stop();}catch(Exception stopError){Report("监听停止未确认",stopError);}button.Background=OverlayTokens.Off;UpdateButtonGlyph(false);System.Windows.Automation.AutomationProperties.SetName(button,"快译：未启用翻译");
        try{tray?.Update(false);}catch(Exception trayError){Report("托盘状态更新失败",trayError);}Report(message,error);
    }
    private async Task ObserveSelection(Task operation)
    {try{await operation;}catch(Exception error){if(!exiting)Report("读取选区失败",error);}}
    private Task ReadCurrentSelection()
    {
        if(selectionCapture==null||!lifecycle.Gate.TryBeginPassiveCapture(lifecycle.Gate.Snapshot.Mode,out _))return Task.CompletedTask;
        if(!NativeDesktop.GetCursorPos(out var point))return Task.CompletedTask;
        var source=SelectionDesktop.Resolve(NativeDesktop.GetForegroundWindow().ToInt64());
        return source==null?Task.CompletedTask:selectionCapture.ExplicitAsync(source,new(point.X,point.Y));
    }
    private void AcceptSelection(SelectionSourceSnapshot source,bool duplicate)
    {
        selectionSource=source;
        button.ToolTip=$"{(duplicate?"已保留此选区":"已读取实际选区")} · {source.SourceKind} · {source.Scalars} 字符{(source.RequiresConfirmation?" · 超过自动发送上限，需确认":"")}{(source.Recovery is ClipboardRecovery.Unconfirmed or ClipboardRecovery.Changed?" · 未覆盖后续剪贴板内容；恢复未确认":"")} · 当前尚未接入模型翻译，可右键查看原文";
        if(sourceViewer!=null&&sourceText!=null)sourceText.Text=source.RawText;
    }
    private void SelectionFailed(SelectionCaptureStatus status)
    {
        if(selectionCapture?.LastFailureExplicit!=true&&selectionCapture?.LastFailureWindow is {} source){long now=Environment.TickCount64;var key=(source.Executable,status);if(selectionFailureNotices.TryGetValue(key,out long last)&&now-last<30000)return;if(selectionFailureNotices.Count>=128)selectionFailureNotices.Clear();selectionFailureNotices[key]=now;}
        button.ToolTip=status==SelectionCaptureStatus.Paused?"此应用的自动取词暂缓30秒，请稍后重试。":"无法读取可信选区，可使用截图或从设置中按应用授权复制兼容。 · "+status;
    }
    private void CopyRecoveryWarning(ClipboardRecovery recovery){if(exiting||lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.SessionLocked))return;long now=Environment.TickCount64;if(now-lastCopyRecoveryNotice<30000&&lastCopyRecoveryNotice!=0)return;lastCopyRecoveryNotice=now;button.ToolTip="复制兼容的剪贴板恢复未确认；未尝试覆盖后续复制，系统历史无法保证删除。";}
    private void ShowSelectionSource()
    {
        if(selectionSource==null||lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.SessionLocked))return;
        if(sourceViewer!=null){sourceViewer.Activate();return;}
        sourceText=new(){Text=selectionSource.RawText,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(16)};
        sourceViewer=new(){Title="快译 · 实际选区原文（不是译文）",Owner=this,Width=520,Height=360,Content=sourceText};sourceViewer.Closed+=(_,_)=>{sourceText?.Clear();sourceText=null;sourceViewer=null;};sourceViewer.Show();
    }
    private void ClearSelectionSource(){selectionCapture?.CancelPending(clearSource:true);selectionSource=null;sourceText?.Clear();sourceViewer?.Close();}
    private async Task StopSelection()
    {
        ClearSelectionSource();selectionHotkey?.Dispose();selectionHotkey=null;
        await selectionReconcile;if(selectionHook!=null){selectionHook.Stop();await selectionHook.DisposeAsync();await selectionEvents;selectionHook=null;}
        if(selectionCapture!=null)await selectionCapture.DisposeAsync();if(selectionBroker!=null)await selectionBroker.DisposeAsync();if(clipboardCapture!=null)await clipboardCapture.DisposeAsync();
    }
}
