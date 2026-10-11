using System.Windows;
using System.Windows.Controls;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
namespace KuaiYi.App;
/// <summary>Explicit per-application immediate consent. Never simulates input or captures during confirmation.</summary>
internal sealed class ClipboardPermissionsWindow:Window
{
    private readonly ListBox applications=new(){MinHeight=120};private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap};private readonly Button grant=new(){Content="明确授权此应用使用 Ctrl+C",Margin=new Thickness(0,8,0,8)},revoke=new(){Content="撤销选中应用授权",Margin=new Thickness(0,8,0,8)};private bool saving;
    public ClipboardPermissionsWindow(SettingsCoordinator coordinator,string? candidate)
    {
        Title="快译 · 复制兼容授权";ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;Width=Math.Min(620,SystemParameters.WorkArea.Width);Height=Math.Min(510,SystemParameters.WorkArea.Height);
        var panel=new StackPanel{Margin=new Thickness(18)};Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        panel.Children.Add(new TextBlock{Text="默认关闭。仅 UIA 失败且来源、权限、修饰键和选区关联可信时，才会向已授权应用发送一次 Ctrl+C。不会强制切换前台。",TextWrapping=TextWrapping.Wrap});
        panel.Children.Add(new TextBlock{Text="复制可能改变系统剪贴板历史、云同步或第三方管理器内容。条件恢复仅恢复当前剪贴板，不能保证删除历史。遇不可完整保存格式会拒绝；后续复制不会被覆盖。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,10)});
        panel.Children.Add(new TextBlock{Text=candidate==null?"先在目标应用执行主动取词；失败后重新打开此页面选择来源。":"最近取词失败的应用：\n"+candidate,TextWrapping=TextWrapping.Wrap});panel.Children.Add(grant);panel.Children.Add(new TextBlock{Text="已授权应用（按完整可执行路径匹配）"});panel.Children.Add(applications);panel.Children.Add(revoke);panel.Children.Add(status);
        void Reload(){applications.ItemsSource=coordinator.Effective?.Data.Privacy.ClipboardAllowedApplications??[];grant.IsEnabled=candidate!=null&&!saving;revoke.IsEnabled=!saving;applications.IsEnabled=!saving;}
        async Task Save(string path,bool enabled)
        {
            if(saving)return;saving=true;Reload();try{await coordinator.SetClipboardPermissionAsync(path,enabled);status.Text=enabled?"授权已生效。不会自动启用翻译；回到来源应用后主动读取选区。":"授权已撤销，旧捕获资格已失效。";}
            catch(Exception error){status.Text="应用授权保存失败，未宣称成功："+error.GetType().Name;}
            finally{saving=false;Reload();}
        }
        grant.Click+=async(_,_)=>{if(candidate==null)return;try{ClipboardApplicationConsent.Normalize(candidate);}catch(System.IO.InvalidDataException){status.Text="来源不是可验证的本地可执行文件。";return;}if(MessageBox.Show(this,"仅授权下列应用：\n"+candidate+"\n\n允许快译在可信选区/主动指令后模拟一次 Ctrl+C。剪贴板历史或同步内容无法保证删除。是否明确授权？","应用复制兼容授权",MessageBoxButton.YesNo,MessageBoxImage.Information,MessageBoxResult.No)==MessageBoxResult.Yes)await Save(candidate,true);};
        revoke.Click+=async(_,_)=>{if(applications.SelectedItem is string application)await Save(application,false);};Closing+=(_,e)=>{if(saving){e.Cancel=true;status.Text="授权事务尚未终结，请等待实际保存结果。";}};Reload();
    }
}
