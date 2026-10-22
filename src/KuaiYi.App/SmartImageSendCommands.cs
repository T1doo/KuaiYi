using KuaiYi.Imaging;
using KuaiYi.Providers;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private Task smartImageTask=Task.CompletedTask;
    private Task StartSmartImageWorkflow(Func<Task> action)
    {
        if(exiting||!CanTranslateScreenshot||!smartImageTask.IsCompletedSuccessfully||SelectedSource.Image is not {} image||retainedImage is not {} retained||
            !ReferenceEquals(image,retained.Image)||lifecycle.Settings is not {} config||
            !lifecycle.Gate.TryBeginPassiveCapture(lifecycle.Gate.Snapshot.Mode,out var ticket))return Task.CompletedTask;
        long input=imageInputRevision,result=resultRevision;
        bool Current()=>!exiting&&ReferenceEquals(retainedImage,retained)&&ReferenceEquals(SelectedSource.Image,image)&&!image.IsDisposed&&
            input==imageInputRevision&&result==resultRevision&&lifecycle.Settings?.Version==config.Version&&lifecycle.Gate.IsCaptureCurrent(ticket);
        return smartImageTask=Run();
        async Task Run(){await Task.Yield();if(Current())await action();}
    }
    partial void CollectSmartCleanup(ref Task cleanup){cleanup=smartImageTask;}
    // Local approval is not consent to HTTP. The original per-image confirmation remains separate.
    private async Task<bool> SubmitSmartScreenshot(OcrImageApproval approval,Func<ProductionScreenshotOffer,bool> confirm)
    {
        ProductionScreenshotOffer? offer=null;ProductionScreenshotCoordinator? flow=null;bool launched=false;List<Exception> errors=[];
        try
        {
            if(!exiting&&imageTranslation is {} current&&SelectedSource.Image is {} image&&retainedImage is {} retained&&ReferenceEquals(retained.Image,image))
            {
                flow=current;offer=flow.Prepare(retained.VersionId,image,ImageOutputMode.Automatic,approval);
                if(offer!=null&&confirm(offer))launched=flow.Submit(offer,true);
            }
        }
        catch(Exception error){errors.Add(error);}
        finally
        {
            if(offer!=null)
            {
                if(!launched)ScreenshotPreviewCleanup.Attempt(errors,()=>flow!.Reject(offer));
                // Begin may synchronously Clear/replace flow.Running. Join this offer's task on success AND failure.
                try{await offer.OriginalTask;}catch(Exception error){errors.Add(error);}
            }
            ScreenshotPreviewCleanup.Attempt(errors,approval.Dispose);
        }
        if(errors.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if(errors.Count>1)throw new AggregateException("Smart image admission or original cleanup failed.",errors);
        return launched;
    }
}
