using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
namespace KuaiYi.App;
internal sealed partial class SettingsWindow
{
    private readonly Dictionary<string,TextBox> hotkeyFields=[];private readonly Dictionary<string,TextBlock> hotkeyStatuses=[];private readonly ISettingsHotkeyParticipant? hotkeyParticipant;private readonly Func<IReadOnlyList<HotkeyStatus>>? hotkeyStatus;
    private static readonly Dictionary<string,string> HotkeyLabels=new(){["toggle"]="切换启用",["translateSelection"]="翻译当前选择",["captureRegion"]="区域截图翻译",["toggleResult"]="展开 / 收起结果",["regenerate"]="重新生成",["hideOverlay"]="显示 / 隐藏浮窗",["focusResult"]="聚焦浮窗",["openSettings"]="打开设置",["stopGeneration"]="停止生成"};
    private StackPanel Hotkeys()
    {
        var panel=Page();Note(panel,"录制仅在当前控件聚焦时进行；保存后才生效，取消保留原组合。全局注册成功只表示当前可注册，不能检测应用内部快捷键。Ctrl+Alt 在 AltGr 布局可能冲突，建议按需换用其他组合。Windows键、F12、系统保留组合不可配置。");
        foreach(var pair in AppSettings.DefaultHotkeys)
        {
            string action=pair.Key;var input=new TextBox{IsReadOnly=true,MinWidth=170,Text=draft.Data.Hotkeys.GetValueOrDefault(action)??"未分配"};hotkeyFields.Add(action,input);var recorder=new HotkeyRecorder();Field(panel,HotkeyLabels[action],input);
            input.GotKeyboardFocus+=(_,_)=>recorder.Begin();input.LostKeyboardFocus+=(_,_)=>recorder.End();input.PreviewKeyDown+=(_,e)=>
            {
                e.Handled=true;var key=e.Key==Key.System?e.SystemKey:e.Key;if(key==Key.Tab&&(Keyboard.Modifiers==ModifierKeys.None||Keyboard.Modifiers==ModifierKeys.Shift)){recorder.End();input.MoveFocus(new TraversalRequest(Keyboard.Modifiers==ModifierKeys.Shift?FocusNavigationDirection.Previous:FocusNavigationDirection.Next));return;}if(key==Key.Escape&&Keyboard.Modifiers==ModifierKeys.None){recorder.End();save.Focus();return;}
                try{var modifiers=(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)?HotkeyModifiers.Control:0)|(Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)?HotkeyModifiers.Alt:0)|(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)?HotkeyModifiers.Shift:0)|(Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)?HotkeyModifiers.Windows:0);var chord=recorder.Record(modifiers,checked((uint)KeyInterop.VirtualKeyFromKey(key)),e.IsRepeat);if(chord!=null){draft.Update(s=>s with{Hotkeys=s.Hotkeys.SetItem(action,chord.Value.Text)});input.Text=chord.Value.Text;status.Text=chord.Value.AltGrRisk?"已录入草稿；Ctrl+Alt 可能与 AltGr 冲突。保存时检查实际注册。":"已录入草稿；保存时检查实际注册。";}}
                catch(HotkeyValidationException error){status.Text=HotkeyLabels[action]+"：不可配置 · "+error.Problem;}
            };
            var buttons=new WrapPanel();var record=new Button{Content="录制"};var clear=new Button{Content="清除"};var reset=new Button{Content="恢复建议值"};foreach(var button in new[]{record,clear,reset}){button.Margin=new Thickness(4);buttons.Children.Add(button);}panel.Children.Add(buttons);record.Click+=(_,_)=>input.Focus();clear.Click+=(_,_)=>Set(null);reset.Click+=(_,_)=>Set(AppSettings.DefaultHotkeys[action]);void Set(string? value){draft.Update(s=>s with{Hotkeys=s.Hotkeys.SetItem(action,value)});input.Text=value??"未分配";}
            var actual=new TextBlock{TextWrapping=System.Windows.TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)};hotkeyStatuses.Add(action,actual);panel.Children.Add(actual);
        }
        RefreshHotkeyStatuses();if(hotkeyParticipant==null){panel.IsEnabled=false;Note(panel,"当前公开探针禁用真实全局注册。");}return panel;
    }
    private void RefreshHotkeyStatuses(){var statuses=hotkeyStatus?.Invoke()??[];foreach(var field in hotkeyStatuses){var registered=statuses.FirstOrDefault(s=>s.Action==field.Key);field.Value.Text=registered?.RecoveryRequired==true?"注册事务尚未确认；恢复前暂停快捷键动作":registered?.Registered==true?"当前已注册；可能与应用内快捷键冲突":registered?.Error>0?"注册失败 · Win32 "+registered.Error:"当前未分配 / 尚未注册";}}
    private void RefreshHotkeyFields(){foreach(var item in hotkeyFields)item.Value.Text=draft.Data.Hotkeys.GetValueOrDefault(item.Key)??"未分配";}
}
