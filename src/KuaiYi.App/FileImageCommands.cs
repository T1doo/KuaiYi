using KuaiYi.Core;
using KuaiYi.Imaging;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private Task fileImageTask=Task.CompletedTask,fileImageCleanup=Task.CompletedTask;
    private CancellationTokenSource? fileImageStop;private FileImageIntent? fileImageIntent;private IImagePreparationOperation? fileImageOwner;
    private Exception? fileImageSettlementFailure;
    private ImageFailure? installedImageRejection; // Bounded category only; no path, pixels, or exception history.
    private long fileImageVersion;private readonly Guid fileImageSession=Guid.NewGuid();
    private bool FileImageBusy{get{bool busy=!fileImageTask.IsCompletedSuccessfully||!fileImageCleanup.IsCompletedSuccessfully||fileImageOwner!=null;BrowserReceiveBusy(ref busy);return busy;}}
    partial void BrowserReceiveBusy(ref bool busy);
    partial void ObserveBrowserImageState();
    partial void CancelBrowserAcquisition();
    partial void CollectBrowserCleanup(ref Task cleanup);
    // The native dialog/secure loader are adapters; this admission/settlement path is shared with managed regressions.
    private bool EnableAndSendOpenedImage(Func<bool> confirm,Action enable,Action send)
    {
        if(exiting||SelectedSource.Image is not OwnedFileImage file||SelectedSource.VersionId is not {} version)return false;
        if(lifecycle.Gate.Snapshot.Activation==Activation.Off){if(!confirm())return false;enable();}
        if(SelectedSource.VersionId!=version||!ReferenceEquals(SelectedSource.Image,file)||file.IsDisposed||lifecycle.Gate.Snapshot.Activation!=Activation.On)return false;
        send();return true;
    }
    private Task StartOpenImage(string path,Func<IFileImageOperation> create)
        =>StartImagePreparation(create,async(owner,session,version,epoch,cancellation)=>await ((IFileImageOperation)owner).LoadAsync(path,session,version,epoch,cancellation));
    private Task StartImagePreparation(Func<IImagePreparationOperation> create,Func<IImagePreparationOperation,Guid,long,long,CancellationToken,Task<OwnedFileImage>> load,CancellationToken external=default,Func<bool>? current=null,long? expectedEpoch=null,Activation? initial=null,Func<ImageAdmission,FileImageIntent,OwnedFileImage,bool>? accept=null)
    {
        if(exiting||external.IsCancellationRequested||current?.Invoke()==false||FileImageBusy||!screenshotHotkeyTask.IsCompletedSuccessfully||!screenshotHotkeyCleanup.IsCompletedSuccessfully||screenshotHotkeyOwner!=null)return Task.CompletedTask;
        long revision=resultRevision;fileImageTask=LoadPreparedImage(create,load,external,current,expectedEpoch,initial,accept);_=ObserveOpenedImage(fileImageTask,revision);return fileImageTask;
    }
    private async Task LoadPreparedImage(Func<IImagePreparationOperation> create,Func<IImagePreparationOperation,Guid,long,long,CancellationToken,Task<OwnedFileImage>> load,CancellationToken external=default,Func<bool>? current=null,long? expectedEpoch=null,Activation? initial=null,Func<ImageAdmission,FileImageIntent,OwnedFileImage,bool>? accept=null)
    {
        OwnedFileImage? image=null;FileImageIntent? intent=null;IImagePreparationOperation? owner=null;List<Exception> failures=[];ImageJobException? inputRejected=null;ImageAdmission? acquired=null;bool acceptStarted=false;
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,external);fileImageStop=cancellation;
        try
        {
            external.ThrowIfCancellationRequested();if(current?.Invoke()==false)return;
            intent=new(lifecycle.Gate,expectedEpoch,initial);fileImageIntent=intent;var admission=CaptureImageAdmission(cancellation,true,external,current);acquired=admission;if(accept!=null)installedImageRejection=null;long version=++fileImageVersion;
            await Task.Yield(); // Install the original task before loader callbacks can reenter.
            if(!intent.IsCurrent||!ImageAdmissionCurrent(admission,admission.Epoch))return;
            owner=create();fileImageOwner=owner;
            image=await Task.Run(()=>load(owner,fileImageSession,version,admission.Epoch,cancellation.Token));
            external.ThrowIfCancellationRequested();cancellation.Token.ThrowIfCancellationRequested();
            if(!await FinalizeFileOwner(owner))throw new InvalidOperationException("Image worker cleanup unconfirmed.",fileImageSettlementFailure);
            if(ReferenceEquals(fileImageOwner,owner))fileImageOwner=null;
            var transferred=image;image=null;if(accept==null)AcceptFileImage(admission,intent,transferred);else{acceptStarted=true;accept(admission,intent,transferred);}
        }
        catch(OperationCanceledException)when(cancellation.IsCancellationRequested||external.IsCancellationRequested){ }
        catch(ImageJobException error)when(accept!=null&&!acceptStarted&&error.Code is ImageFailure.InvalidImage or ImageFailure.ImageTooLarge or ImageFailure.UnsupportedImage){inputRejected=error;}
        catch(Exception error){if(accept!=null)failures.Add(error);else if(intent?.IsCurrent==true&&!exiting)Report("打开图片未完成；旧来源保留，没有发送",error);}
        finally
        {
            if(accept==null)
            {
                image?.Dispose();intent?.Dispose();
                if(ReferenceEquals(fileImageStop,cancellation)){fileImageStop=null;fileImageIntent=null;}
                if(owner!=null&&await FinalizeFileOwner(owner))if(ReferenceEquals(fileImageOwner,owner))fileImageOwner=null;
                if(!exiting)Refresh();
            }
            else
            {
                // The installed warm actor must observe the actual body AND cleanup; a reporting catch is not acceptance.
                ScreenshotPreviewCleanup.Attempt(failures,()=>image?.Dispose());ScreenshotPreviewCleanup.Attempt(failures,()=>intent?.Dispose());
                if(ReferenceEquals(fileImageStop,cancellation)){fileImageStop=null;fileImageIntent=null;}
                if(owner!=null)
                {
                    if(await FinalizeFileOwner(owner)){if(ReferenceEquals(fileImageOwner,owner))fileImageOwner=null;}
                    else failures.Add(new InvalidOperationException("Original image worker exit unconfirmed.",fileImageSettlementFailure));
                }
                if(inputRejected!=null)
                {
                    if(failures.Count!=0)failures.Insert(0,inputRejected);
                    else
                    {
                        installedImageRejection=inputRejected.Code;
                        if(acquired is {} stamp&&!exiting&&!external.IsCancellationRequested&&current?.Invoke()!=false&&ReferenceEquals(imageAdmissionOwner,cancellation)&&stamp.Input==imageInputRevision&&stamp.Result==resultRevision)
                            try{Report("文件图片未接纳 · "+installedImageRejection+"；没有发送",inputRejected);}
                            catch(Exception report){failures.Add(inputRejected);failures.Add(report);}
                    }
                }
                if(!exiting)ScreenshotPreviewCleanup.Attempt(failures,Refresh);
                if(inputRejected!=null&&failures.Count!=0&&!failures.Contains(inputRejected))failures.Insert(0,inputRejected);
                if(failures.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
                if(failures.Count>1)throw new AggregateException("Installed image body or cleanup failed.",failures);
            }
        }
    }
    private async Task<bool> FinalizeFileOwner(IImagePreparationOperation owner)
    {
        try{return await owner.TryFinalizeAsync();}
        catch(Exception error){fileImageSettlementFailure=error;return false;} // Retain the failure and owner; only current admission or an explicit retry may report it.
    }
    private async Task ObserveOpenedImage(Task task,long revision)
    {try{await task;}catch(Exception error){if(!exiting&&revision==resultRevision&&CanReadOriginalSource()&&lifecycle.Gate.Snapshot.Presentation!=Presentation.Hidden)Report("图片原操作清理未确认；保留原所有权",error);}}
    private void ObserveFileImageState()
    {ObserveBrowserImageState();if(fileImageIntent is {} intent&&!intent.IsCurrent)fileImageStop?.Cancel();}
    partial void CancelFileAcquisition(){try{fileImageStop?.Cancel();}finally{CancelBrowserAcquisition();}}
    partial void CollectFileCleanup(ref Task cleanup){Task browser=Task.CompletedTask;CollectBrowserCleanup(ref browser);cleanup=Task.WhenAll(StopOpenImage(),browser); }
    private Task StopOpenImage()
    {
        Exception? failure=null;try{fileImageStop?.Cancel();}catch(Exception error){failure=error;}
        if(!fileImageCleanup.IsCompleted)return failure==null?fileImageCleanup:WithCancellationFailure(fileImageCleanup,failure);
        return fileImageCleanup=SettleOpenImage(failure);
    }
    private static async Task WithCancellationFailure(Task pending,Exception failure)
    {List<Exception> errors=[failure];try{await pending;}catch(Exception error){errors.Add(error);}throw new AggregateException(errors);}
    private async Task SettleOpenImage(Exception? failure)
    {
        List<Exception> errors=[];if(failure!=null)errors.Add(failure);
        try{await fileImageTask;}catch(Exception error){errors.Add(error);}
        if(fileImageOwner is {} owner)
        {try{if(!await owner.TryFinalizeAsync())throw new InvalidOperationException("Original image worker exit unconfirmed.",fileImageSettlementFailure);if(ReferenceEquals(fileImageOwner,owner)){fileImageOwner=null;fileImageSettlementFailure=null;}}catch(Exception error){errors.Add(error);}}
        if(errors.Count>0)throw new AggregateException("File image cleanup unconfirmed.",errors);
    }
}
