using System.ComponentModel;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private ShellCloseRequests? closeRequests;
    private List<Exception>? closeAdmissionErrors;
    private void ClosingShell(object? sender,CancelEventArgs e)
    {
        if(allowClose)return;e.Cancel=true;
        if(opening||settings!=null){exitRequested=true;PrepareSettingsExit(()=>settings?.RequestCloseWhenSettled());return;}
        try
        {
            (closeRequests??=new(DispatchCloseAttempt)).Request(()=>
            {
                if(exiting)return;
                var errors=closeAdmissionErrors??=[];
                try{QueuePositionSave();}catch(Exception error){errors.Add(error);}
                exiting=true;button.IsEnabled=false;
                // Preserve the original Closing event's synchronous send revocation before Dispatcher work can wait.
                try{lifecycle.Hide();}catch(Exception error){errors.Add(error);}
            });
        }
        catch(Exception error){exiting=false;button.IsEnabled=true;Report("退出清理尚未完成",error);}
    }
    private async Task<bool> TryCloseAttemptAsync()
    {
        var admissionErrors=closeAdmissionErrors;closeAdmissionErrors=null;
        try
        {
            QueuePositionSave();exiting=true;button.IsEnabled=false;
            await SettleExitContent(admissionErrors);allowClose=true;await CommitShellCloseAsync();return true;
        }
        catch(Exception error){allowClose=false;exiting=false;button.IsEnabled=true;Report("退出清理尚未完成",error);return false;}
    }
}
