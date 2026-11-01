using System.Windows;
using System.Windows.Controls;
namespace KuaiYi.App;
internal sealed partial class SettingsWindow
{
    private readonly Button addProfileAction=new(){Content="新增配置",Margin=new Thickness(4)};
    // Only called while constructing a NEW window, before any user edits.
    // An already open draft is activated as-is by the Shell.
    private void SetupEnableGuide(ListBox navigation)
    {
        var guide=SettingsEnableGuide.Plan(draft.Basis);
        if(guide.Target==SettingsEnableTarget.Recovery)
        {
            navigation.SelectedIndex=0; // Keep the original recovery/read-only explanation.
            if(!draft.Basis.ReadOnly&&draft.Basis.RequiresRecovery)FocusOnce(recovery,0);
            return;
        }
        if(guide.ProfileId is {} id){selectedId=id;RefreshProfiles();}
        navigation.SelectedIndex=guide.Page;
        status.Text=guide.Notice+" 保存设置不自动启用；关闭后请再次点击红色按钮。";
        FrameworkElement target=guide.Target switch
        {
            SettingsEnableTarget.CreateProfile=>addProfileAction,
            SettingsEnableTarget.ChooseTextProfile=>textProfile,
            SettingsEnableTarget.Model=>model,
            SettingsEnableTarget.Key=>password,
            SettingsEnableTarget.Address=>url,
            _=>profiles
        };
        FocusOnce(target,guide.Page);
        void FocusOnce(FrameworkElement element,int page)
        {
            RoutedEventHandler? ready=null;
            ready=(_,_)=>
            {
                Loaded-=ready;
                if(!IsVisible||saving||draft.IsClosed||draft.Basis.ReadOnly||navigation.SelectedIndex!=page||!element.IsEnabled||!element.IsVisible)return;
                element.BringIntoView();element.Focus();
            };
            Loaded+=ready;
        }
    }
}
