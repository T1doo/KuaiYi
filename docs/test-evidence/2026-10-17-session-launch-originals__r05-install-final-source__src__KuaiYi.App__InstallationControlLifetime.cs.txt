using KuaiYi.Core.Installation;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private InstallationApplicationControl? installationControl=null;
    private Task SettleInstallationControl()=>installationControl?.SettleForExitAsync()??Task.CompletedTask;
}
