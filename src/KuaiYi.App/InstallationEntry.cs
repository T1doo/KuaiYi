using System.IO;
using System.Windows;
using KuaiYi.Core.Installation;
namespace KuaiYi.App;

internal sealed partial class ShellWindow
{
    private readonly TaskCompletionSource installationReady=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool startupHealthy;
    internal void AttachInstallationControl(InstallationApplicationControl control)
    {
        installationControl=control;AttachWarmInstalledImages(control);
        Loaded+=async(_,_)=>
        {
            try
            {
                await startup;
                if(!startupHealthy||exiting||!IsLoaded||!IsVisible||hwnd==0||!geometryReady||lifecycle.Settings is not {ReadOnly:false,RequiresRecovery:false})throw new IOException("Application startup health is unconfirmed.");
                installationReady.TrySetResult();
            }
            catch(Exception error){installationReady.TrySetException(error);}
        };
        // The original loop remains held by control; observing its fault does not declare shutdown or remove any fence.
        _=control.RunAsync(installationReady.Task,RequestInstallationCloseAsync,(offer,cancellation)=>QueueInstalledImage(control,offer,cancellation)).ContinueWith(t=>{diagnostics.RecordFailure(t.Exception!.GetBaseException());},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
    }
    private void QueueInstalledImage(InstallationApplicationControl control,InstallationImageOffer offer,CancellationToken cancellation)
    {
        RegisterInstalledImageDispatch(async expected=>
            {
                try
                {
                    await Dispatcher.InvokeAsync(()=>
                    {
                        if(cancellation.IsCancellationRequested||!control.IsImageCurrent(offer.Request)||Volatile.Read(ref installedImageClosed)||expected!=CaptureInstalledImageState())return;
                        if(!startupHealthy||exiting||!IsLoaded||!IsVisible||lifecycle.Settings is not {ReadOnly:false,RequiresRecovery:false})return;
                        if(!CanReceiveBrowserImage()){button.ToolTip="原图片任务或清理未确认；本次资源管理器请求未接纳，请稍后重新选择。";return;}
                        _=StartInstalledImage(offer.Path,expected,CreateFileImageOperation,cancellation,()=>control.IsImageCurrent(offer.Request),ct=>control.VerifyImageAsync(offer.Request,ct));
                    },System.Windows.Threading.DispatcherPriority.Normal,cancellation);
                }
                catch(OperationCanceledException)when(cancellation.IsCancellationRequested){}
                // The original task is retained and joined by exit. This observer does not make it successful.
                catch(Exception error){diagnostics.RecordFailure(error);throw;}
            });
    }
}
