using System.IO;
using System.Windows.Controls;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Windows;
namespace KuaiYi.App;
internal sealed partial class ImageProbeWindow
{
    private SettingsWindow? settingsWindow;private bool settingsOpening;
    private SettingsCoordinator? settingsCoordinator;
    private void SetupSettings(StackPanel panel)
    {
        var button=new Button{Content="设置"};panel.Children.Add(button);
        button.Click+=async(_,_)=>
        {
            if(settingsWindow!=null){settingsWindow.Activate();return;}if(settingsOpening)return;
            SettingsDraft? opened=null;settingsOpening=true;gate.SetSuspension(Suspension.Settings,true);
            try
            {
                if(settingsCoordinator==null)
                {
                    string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KuaiYi");
                    settingsCoordinator=new(new SettingsStore(Path.Combine(root,"config")),new SecretFileStore(Path.Combine(root,"secrets"),new CurrentUserDpapiProtector(),new CurrentUserSecretDirectory()));
                    settingsCoordinator.Committed+=(snapshot,retired)=>{if(retired.HasDeletion)gate.Disable();gate.SetMode(snapshot.Data.TriggerMode);}; // Thread-safe gate only; never read a selection or send a request.
                }
                var draft=await settingsCoordinator.OpenDraftAsync();opened=draft;
                settingsWindow=new(settingsCoordinator,draft){Owner=this};
                settingsWindow.Closed+=(_,_)=>{settingsWindow=null;gate.SetSuspension(Suspension.Settings,false);};settingsWindow.Show();opened=null;
            }
            catch(Exception error){opened?.Dispose();if(settingsWindow is {} failed&&!failed.IsVisible){failed.Close();settingsWindow=null;}gate.SetSuspension(Suspension.Settings,false);status.Text="设置无法打开 · "+error.GetType().Name+"；原文件保留。";}
            finally{settingsOpening=false;}
        };
    }
}
