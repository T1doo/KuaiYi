using System.Windows;
using System.Windows.Controls;
using KuaiYi.Core;
namespace KuaiYi.App;
// Data/action destination ports only. Actual WPF focus, layout and menu routing remain NotRun.
internal sealed partial class ShellWindow
{
    private readonly Button failureNotice=new();private UIElement? recoveryTarget;
    private void OpenOverflowMenu(UIElement target)=>recoveryTarget=target;
    internal string FailureNoticeText=>failureNotice.Content as string??"";
    internal string FailureNoticeFull=>failureNotice.ToolTip as string??"";
    internal string FailureAutomation=>failureNotice.AutomationName??"";
    internal double FailureHeight=>FailureNoticeHeight;
    internal bool RecoveryOpened=>ReferenceEquals(recoveryTarget,failureNotice);
    internal void OpenFailureRecoveryForTest()=>ShowFailureRecovery();
    internal void SelectFailureVersionForTest(Guid id)=>SelectResultVersion(id);
}
