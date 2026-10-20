using System.Windows;
using System.Windows.Controls;
using KuaiYi.Core.Settings;
using Microsoft.Win32;
namespace KuaiYi.App;

internal sealed partial class SettingsWindow
{
    private SettingsBackupWindow? backupWindow;private bool backupClosePending;
    private void SetupBackupEntry(Panel panel)
    {
        var backup=new Button{Content="预览并导出已保存普通配置…",Margin=new Thickness(0,8,0,8)};panel.Children.Add(backup);
        Note(panel,"普通配置备份不包含密钥、自定义请求头、应用规则/授权、端点同意记录或浮窗位置。草稿更改需先保存。本切片提供导出；完整配置导入尚未提供。");
        Closing+=async(_,e)=>
        {
            if(backupWindow is not {} window||!window.ExportPending)return;
            e.Cancel=true;if(backupClosePending)return;backupClosePending=true;
            try{await window.SettleForCloseAsync();if(window.IsVisible)window.Close();}
            finally{backupClosePending=false;}
            if(IsVisible)Close(); // Reenter the original save/API-test Closing guards after actual file settlement.
        };
        backup.Click+=(_,_)=>
        {
            if(backupWindow!=null){backupWindow.Activate();return;}
            if(saving||coordinator.Effective is not {} saved){status.Text="请先保存有效配置，再预览普通配置备份。";return;}
            var session=new SettingsBackupSession();
            try
            {
                session.Prepare(saved);backupWindow=new(session){Owner=this};backupWindow.Closed+=(_,_)=>backupWindow=null;backupWindow.Show();
            }
            catch(Exception error)when(error is InvalidOperationException or SettingsValidationException or UnknownSettingsSchemaException)
            {session.Dispose();status.Text="已保存配置当前不可备份；请先完成恢复并保存。";}
        };
    }
}
internal sealed partial class SettingsBackupWindow:Window
{
    private readonly SettingsBackupSession session;private bool closed,closePending;
    private readonly TextBlock note=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)};
    private readonly Button export=new(){Content="将所示配置导出到新文件",Margin=new Thickness(4)};
    private readonly TextBox text=new(){IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.NoWrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};
    public SettingsBackupWindow(SettingsBackupSession session)
    {
        this.session=session;Title="快译 · 普通配置导出预览";Width=Math.Min(740,SystemParameters.WorkArea.Width);Height=Math.Min(580,SystemParameters.WorkArea.Height);WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel{Margin=new Thickness(12)};Content=root;
        var actions=new StackPanel();DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
        note.Text="此为已保存配置的冻结预览，后续编辑不改变本份备份。包含服务地址、模型、模板与偏好，请选择自己保管的本地文件。所有密钥引用、自定义头（含 anthropic-version 覆盖）、应用规则/授权、端点同意、位置及事务标识均已剔除。HTTP 风险开关仅为传输偏好，备份不构成端点授权。重新使用时须补回凭据、必要请求头与安全规则并重新确认授权。";actions.Children.Add(note);
        var row=new WrapPanel();actions.Children.Add(row);var cancel=new Button{Content="取消未提交导出",Margin=new Thickness(4)};row.Children.Add(export);row.Children.Add(cancel);
        text.Text=session.Preview?.Text??throw new InvalidOperationException("Backup preview required.");root.Children.Add(text);
        System.Windows.Automation.AutomationProperties.SetName(text,"普通配置备份完整 JSON 预览");
        cancel.Click+=(_,_)=>CancelBackup();
        export.Click+=async(_,_)=>
        {
            if(closed||session.Busy||session.Preview==null)return;
            var picker=new SaveFileDialog{Title="将所示普通配置导出到新文件",Filter="配置备份 (*.json)|*.json",DefaultExt=".json",AddExtension=true,FileName="KuaiYi-settings-backup.json",OverwritePrompt=false,CheckPathExists=true};
            if(picker.ShowDialog(this)!=true||closed)return;
            await ExportBackup(picker.FileName);
        };
        Closing+=async(_,e)=>
        {
            if(!ExportPending){CancelBackup();return;}
            e.Cancel=true;if(closePending)return;closePending=true;
            try{await SettleForCloseAsync();}
            finally{closePending=false;}
            if(!closed)Close();
        };
        Closed+=(_,_)=>DisposeBackupView();
    }
}
