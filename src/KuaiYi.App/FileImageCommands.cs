using KuaiYi.Core;
using KuaiYi.Imaging;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private Task fileImageTask=Task.CompletedTask,fileImageCleanup=Task.CompletedTask;
    private CancellationTokenSource? fileImageStop;private FileImageIntent? fileImageIntent;private IFileImageOperation? fileImageOwner;
    private Exception? fileImageSettlementFailure;
    private long fileImageVersion;private readonly Guid fileImageSession=Guid.NewGuid();
    private bool FileImageBusy=>!fileImageTask.IsCompletedSuccessfully||!fileImageCleanup.IsCompletedSuccessfully||fileImageOwner!=null;
    // The native dialog/secure loader are adapters; this admission/settlement path is shared with managed regressions.
    private bool EnableAndSendOpenedImage(Func<bool> confirm,Action enable,Action send)
    {
        if(exiting||SelectedSource.Image is not OwnedFileImage file||SelectedSource.VersionId is not {} version)return false;
        if(lifecycle.Gate.Snapshot.Activation==Activation.Off){if(!confirm())return false;enable();}
        if(SelectedSource.VersionId!=version||!ReferenceEquals(SelectedSource.Image,file)||file.IsDisposed||lifecycle.Gate.Snapshot.Activation!=Activation.On)return false;
        send();return true;
    }
    private Task StartOpenImage(string path,Func<IFileImageOperation> create)
    {
        if(exiting||FileImageBusy||!screenshotHotkeyTask.IsCompletedSuccessfully||!screenshotHotkeyCleanup.IsCompletedSuccessfully||screenshotHotkeyOwner!=null)return Task.CompletedTask;
        long revision=resultRevision;fileImageTask=LoadOpenedImage(path,create);_=ObserveOpenedImage(fileImageTask,revision);return fileImageTask;
    }
    private async Task LoadOpenedImage(string path,Func<IFileImageOperation> create)
    {
        OwnedFileImage? image=null;FileImageIntent? intent=null;IFileImageOperation? owner=null;
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);fileImageStop=cancellation;
        try
        {
            intent=new(lifecycle.Gate);fileImageIntent=intent;var admission=CaptureImageAdmission(cancellation,true);long version=++fileImageVersion;
            await Task.Yield(); // Install the original task before loader callbacks can reenter.
            if(!intent.IsCurrent||!ImageAdmissionCurrent(admission,admission.Epoch))return;
            owner=create();fileImageOwner=owner;
            image=await Task.Run(()=>owner.LoadAsync(path,fileImageSession,version,admission.Epoch,cancellation.Token));
            cancellation.Token.ThrowIfCancellationRequested();
            if(!await FinalizeFileOwner(owner))throw new InvalidOperationException("Image worker cleanup unconfirmed.",fileImageSettlementFailure);
            if(ReferenceEquals(fileImageOwner,owner))fileImageOwner=null;
            var transferred=image;image=null;AcceptFileImage(admission,intent,transferred);
        }
        catch(OperationCanceledException)when(cancellation.IsCancellationRequested){ }
        catch(Exception error){if(intent?.IsCurrent==true&&!exiting)Report("打开图片未完成；旧来源保留，没有发送",error);}
        finally
        {
            image?.Dispose();intent?.Dispose();
            if(ReferenceEquals(fileImageStop,cancellation)){fileImageStop=null;fileImageIntent=null;}
            if(owner!=null&&await FinalizeFileOwner(owner))if(ReferenceEquals(fileImageOwner,owner))fileImageOwner=null;
            if(!exiting)Refresh();
        }
    }
    private async Task<bool> FinalizeFileOwner(IFileImageOperation owner)
    {
        try{return await owner.TryFinalizeAsync();}
        catch(Exception error){fileImageSettlementFailure=error;return false;} // Retain the failure and owner; only current admission or an explicit retry may report it.
    }
    private async Task ObserveOpenedImage(Task task,long revision)
    {try{await task;}catch(Exception error){if(!exiting&&revision==resultRevision&&CanReadOriginalSource()&&lifecycle.Gate.Snapshot.Presentation!=Presentation.Hidden)Report("图片原操作清理未确认；保留原所有权",error);}}
    private void ObserveFileImageState()
    {if(fileImageIntent is {} intent&&!intent.IsCurrent)fileImageStop?.Cancel();}
    partial void CancelFileAcquisition(){fileImageStop?.Cancel();}
    partial void CollectFileCleanup(ref Task cleanup)=>cleanup=StopOpenImage();
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
