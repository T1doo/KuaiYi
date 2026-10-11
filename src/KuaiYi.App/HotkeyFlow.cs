using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.Windows;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private HotkeyHost? globalHotkeys;private Task hotkeySetup=Task.CompletedTask,hotkeyRecovery=Task.CompletedTask;
    private void SetupHotkeys()
    {
        if(!enableSelectionPlatform)return;globalHotkeys=new(lifecycle.Gate);globalHotkeys.Failed+=code=>Dispatcher.BeginInvoke(()=>{if(!exiting)button.ToolTip="快捷键状态未确认 · "+code;});
        globalHotkeys.Pressed+=signal=>
        {
            var binding=signal.Binding;
            Dispatcher.BeginInvoke(()=>
            {
                if(exiting||globalHotkeys?.IsCurrent(signal)!=true||binding.SettingsVersion!=lifecycle.Settings?.Version)return;
                var state=lifecycle.Gate.Snapshot;switch(HotkeyActionPolicy.Evaluate(binding.Action,state.Activation,state.Suspension))
                {case HotkeyActionDecision.Blocked:return;case HotkeyActionDecision.HintEnable:button.ToolTip="请先启用翻译。";return;}
                switch(binding.Action)
                {
                    case "toggle":Toggle();break;
                    case "translateSelection":_=ObserveSelection(ReadCurrentSelection(signal.Input));break;
                    case "captureRegion":_=StartHotkeyScreenshot(signal.Input);break;
                    case "toggleResult":if(overlayFrames?.IsExpanded==true)CollapseResult();else ReopenResult();break;
                    case "regenerate":button.ToolTip="重新生成尚不可用：正式翻译调度未接入。";break;
                    case "hideOverlay":if(IsVisible)HideOverlay();else ShowOverlay();break;
                    case "focusResult":FocusOverlay();break;
                    case "openSettings":_=OpenSettings();break;
                    case "stopGeneration":if(overlayResults.Identity!=null)StopResult();break;
                }
            });
        };
        hotkeySetup=InitializeHotkeys();
    }
    private async Task InitializeHotkeys()
    {try{await startup;if(globalHotkeys!=null&&lifecycle.Settings is {} saved){await globalHotkeys.RestoreAsync(saved);ReportHotkeyStatuses();}}catch(Exception error){Report("快捷键启动恢复未确认；托盘仍可用",error);}}
    private void ReportHotkeyStatuses(){var failures=globalHotkeys?.Status.Where(s=>s.Error!=0||s.RecoveryRequired).ToArray();if(failures?.Length>0)button.ToolTip="快捷键注册失败 · "+string.Join("；",failures.Select(s=>s.Action+" / Win32 "+s.Error))+"；托盘可恢复。";}
    private void RecoverHotkeys(bool locked)
    {hotkeyRecovery=RecoverHotkeysOwned(hotkeyRecovery,locked);}
    private async Task RecoverHotkeysOwned(Task previous,bool locked)
    {try{await previous;await hotkeySetup;if(globalHotkeys==null)return;await globalHotkeys.SuspendAsync(locked);if(!locked&&lifecycle.Settings is {} saved)await globalHotkeys.RecoverAsync(saved.Version);if(!exiting)ReportHotkeyStatuses();}catch(Exception error){Report("快捷键恢复未确认；保留托盘入口",error);}}
    private async Task StopHotkeys(){await hotkeySetup;await hotkeyRecovery;if(globalHotkeys!=null){await globalHotkeys.DisposeAsync();globalHotkeys=null;}}
}
