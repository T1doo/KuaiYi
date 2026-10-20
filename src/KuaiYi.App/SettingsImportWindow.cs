using System.Windows;
using System.Windows.Controls;
using KuaiYi.Core.Settings;
using Microsoft.Win32;
namespace KuaiYi.App;

internal sealed partial class SettingsWindow
{
    private SettingsImportWindow? importWindow;private bool importClosePending;
    private void SetupImportEntry(Panel panel)
    {
        var import=new Button{Content="校验并导入普通配置…",Margin=new Thickness(0,8,0,8)};panel.Children.Add(import);
        Note(panel,"导入前请先保存或取消现有修改。导入保留本机配置档和默认选择，新增无凭据的配置档；普通偏好和热键组合经预览确认后进入草稿，保存才生效。含密钥引用、请求头或授权记录的文件会整份拒绝。");
        import.Click+=async(_,_)=>await OpenConfigurationImport();
        Closing+=async(_,e)=>
        {
            if(importWindow is not {} window||!window.ReadPending)return;
            e.Cancel=true;if(importClosePending)return;importClosePending=true;
            try{await window.SettleForCloseAsync();if(window.IsVisible)window.Close();}
            finally{importClosePending=false;}
            if(IsVisible)Close();
        };
    }
    private async Task OpenConfigurationImport()
    {
        if(importWindow!=null||saving||!apiTestTask.IsCompleted||draft.IsClosed)return;
        // Do not move an uncaptured secret into a different form merely to discover that the draft is dirty.
        if(password.Password.Length!=0||reveal.Text.Length!=0){status.Text="请先保存或取消尚未保存的新密钥，再导入配置。";return;}
        SettingsImportSession? session=null;SettingsImportWindow? window=null;
        try
        {
            CaptureProfile();CapturePreferences();var basis=draft.CaptureImportBasis();
            if(coordinator.Effective?.Version!=basis.SavedVersion)throw new SettingsConflictException();
            var picker=new OpenFileDialog{Title="选择普通配置备份",Filter="配置备份 (*.json)|*.json",Multiselect=false,CheckFileExists=true};
            if(picker.ShowDialog(this)!=true)return;
            CancelDisplayPreferences();apiTests?.DraftEdited();session=new();
            window=importWindow=new(session,basis,picker.FileName,()=>coordinator.Effective?.Version){Owner=this};
            bool? result=window.ShowDialog();
            if(result==true&&window.Applied)
            {
                selectedId=draft.Data.ActiveProfileId??draft.Data.Profiles.FirstOrDefault()?.Id;
                LoadGeneral();RefreshProfiles();RefreshHotkeyFields();
                status.Text="普通配置已合并到草稿；原本机凭据和授权保留。请检查新增配置、能力和 HTTP 风险设置，再保存；取消设置窗口会放弃本次导入。";
            }
        }
        catch(Exception error)when(error is SettingsValidationException or SettingsConflictException or InvalidOperationException or UnknownSettingsSchemaException)
        {status.Text="导入未开始或目标已失效；请先保存或取消当前修改，并完成配置恢复后重试。原配置和草稿未被导入覆盖。";}
        finally
        {
            if(window!=null)await window.SettleForCloseAsync();session?.Dispose();importWindow=null;
        }
    }
}
internal sealed partial class SettingsImportWindow:Window
{
    private readonly SettingsImportSession session;private readonly SettingsImportBasis basis;private readonly string path;private readonly Func<string?> currentSavedVersion;
    private readonly TextBox preview=new(){IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.NoWrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};
    private readonly TextBlock note=new(){Text="正在校验所选文件；不会修改草稿或配置。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)};
    private readonly Button apply=new(){Content="确认所示合并，仅应用到草稿",IsEnabled=false,Margin=new Thickness(4)};private bool closed,closePending;
    public bool Applied{get;private set;}
    public SettingsImportWindow(SettingsImportSession session,SettingsImportBasis basis,string path,Func<string?> currentSavedVersion)
    {
        this.session=session;this.basis=basis;this.path=path;this.currentSavedVersion=currentSavedVersion;
        Title="快译 · 普通配置导入预览";Width=Math.Min(760,SystemParameters.WorkArea.Width);Height=Math.Min(620,SystemParameters.WorkArea.Height);WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel{Margin=new Thickness(12)};Content=root;var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);footer.Children.Add(note);
        var buttons=new WrapPanel();footer.Children.Add(buttons);buttons.Children.Add(apply);var cancel=new Button{Content="取消导入",Margin=new Thickness(4)};buttons.Children.Add(cancel);root.Children.Add(preview);
        System.Windows.Automation.AutomationProperties.SetName(preview,"校验后的普通配置合并预览");
        Loaded+=async(_,_)=>await StartImportRead();apply.Click+=(_,_)=>{if(ApplyImport())DialogResult=true;};cancel.Click+=(_,_)=>Close();
        Closing+=async(_,e)=>
        {
            if(!ReadPending){session.Invalidate();return;}
            e.Cancel=true;if(closePending)return;closePending=true;
            try{await SettleForCloseAsync();}
            finally{closePending=false;}
            if(!closed)Close();
        };
        Closed+=(_,_)=>DisposeImportView();
    }
}
