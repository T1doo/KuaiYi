using System.IO;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.Imaging;
using KuaiYi.NativeHost;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private BrowserImageReception? browserReception;
    private InstalledBrowserReceiver? productionBrowserReceiver;
    private readonly CancellationTokenSource browserReceiveStop=new();
    private Task browserReceiveTask=Task.CompletedTask,browserReceiveCleanup=Task.CompletedTask;
    private string? browserReceiveFailure;private readonly object browserCleanupSync=new();
    private void SetupBrowserImages(bool production)
    {
        if(!production)return;
        Loaded+=(_,_)=>{browserReceiveTask=RunBrowserImages();};
        Closed+=(_,_)=>{browserReception?.Dispose();browserReceiveStop.Dispose();};
    }
    private void AddBrowserImageStatus(System.Windows.Controls.ContextMenu menu)
    {
        var status=new System.Windows.Controls.MenuItem{IsEnabled=false};menu.Items.Add(status);
        menu.Opened+=(_,_)=>status.Header=browserReception?.Busy==true?"浏览器图片：正在接收本次图片":productionBrowserReceiver!=null&&!browserReceiveTask.IsCompleted?"浏览器图片：已运行，接收前逐次确认":"浏览器图片：本地接收不可用 · "+(browserReceiveFailure??"尚未就绪");
    }
    private BrowserImageSourceState BrowserSourceState()
    {
        var snapshot=lifecycle.Gate.Snapshot;var config=lifecycle.Settings;
        return new(snapshot.Epoch,snapshot.Activation,config?.Version??"",imageInputRevision,resultRevision,
            !exiting&&startupHealthy&&IsVisible&&snapshot.Presentation!=Presentation.Hidden&&snapshot.Suspension==Suspension.None&&config is {ReadOnly:false,RequiresRecovery:false});
    }
    private bool CanReceiveBrowserImage()=>!FileImageBusy&&screenshotHotkeyTask.IsCompletedSuccessfully&&screenshotHotkeyCleanup.IsCompletedSuccessfully&&screenshotHotkeyOwner==null&&
        ocrOnly is not {CleanupPending:true}&&!ocrFlowBusy&&!opening&&settings==null&&!lifecycle.Gate.HasRunningGeneration;
    private async Task RunBrowserImages()
    {
        try
        {
            await Task.Yield(); // All Loaded handlers must first install the original startup task.
            await startup;if(exiting||!startupHealthy||browserReceiveStop.IsCancellationRequested)return;
            browserReception=new(new DispatcherSynchronizationContext(Dispatcher),BrowserSourceState,CanReceiveBrowserImage,
                new DesktopLocalConfirmation(this,()=>browserReception?.Current==true),
                (png,permit)=>StartReceivedBrowserImage(png,()=>new BrowserImageOperation(()=>new ImageWorkerClient(Path.Combine(AppContext.BaseDirectory,"KuaiYi.ImageWorker.exe"))),permit.Token,()=>permit.IsCurrent,permit.ExpectedEpoch,permit.InitialActivation));
            var policy=await Task.Run(InstalledHostPolicy.LoadForApp);
            if(exiting||browserReceiveStop.IsCancellationRequested)return;
            productionBrowserReceiver=new(policy,browserReception.TransferGate,browserReception,browserReception.Inbox,browserReception.CompleteAsync);
            await productionBrowserReceiver.RunAsync(browserReceiveStop.Token);
            browserReceiveFailure=productionBrowserReceiver.FailureCode;
        }
        catch(OperationCanceledException)when(browserReceiveStop.IsCancellationRequested){ }
        catch(Exception error){browserReceiveFailure=error is BrowserException known?known.Code.ToString():error.GetType().Name;}
        // A failed policy/bind is not retried or repaired automatically. Other image entry points remain usable.
    }
    partial void BrowserReceiveBusy(ref bool busy){busy|=browserReception?.Busy==true||browserReception?.CleanupPending==true||!Volatile.Read(ref browserReceiveCleanup).IsCompletedSuccessfully;}
    partial void ObserveBrowserImageState()
    {if(browserReception is {Busy:true,Current:false})CancelBrowserAcquisition();}
    partial void CancelBrowserAcquisition()
    {
        try{browserReception?.Revoke();}
        finally
        {
            var original=productionBrowserReceiver?.RevokeAsync()??Task.CompletedTask;
            lock(browserCleanupSync)
            {
                if(browserReceiveCleanup.IsCompletedSuccessfully)Volatile.Write(ref browserReceiveCleanup,original);
                else if(!ReferenceEquals(browserReceiveCleanup,original))Volatile.Write(ref browserReceiveCleanup,Task.WhenAll(browserReceiveCleanup,original));
            }
        }
    }
    partial void CollectBrowserCleanup(ref Task cleanup)
    {
        CancelBrowserAcquisition();
        if(exiting)
        {
            browserReceiveStop.Cancel();cleanup=Task.WhenAll(Volatile.Read(ref browserReceiveCleanup),browserReceiveTask);
        }
        else cleanup=Volatile.Read(ref browserReceiveCleanup);
    }
}
