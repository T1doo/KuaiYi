using KuaiYi.Imaging;
using KuaiYi.Providers;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private Task smartImageTask=Task.CompletedTask;
    private Task StartSmartImageWorkflow(Func<Task> action,ImageRegenerationCommand? regeneration=null)
    {
        if(exiting||(regeneration==null?!CanTranslateScreenshot:!ImageRegenerationCurrent(regeneration))||!smartImageTask.IsCompletedSuccessfully||SelectedSource.Image is not {} image||retainedImage is not {} retained||
            !ReferenceEquals(image,retained.Image)||lifecycle.Settings is not {} config||
            !lifecycle.Gate.TryBeginPassiveCapture(lifecycle.Gate.Snapshot.Mode,out var ticket))return Task.CompletedTask;
        long input=imageInputRevision,result=resultRevision;
        bool Current()=>(regeneration==null||ImageRegenerationCurrent(regeneration))&&!exiting&&ReferenceEquals(retainedImage,retained)&&ReferenceEquals(SelectedSource.Image,image)&&!image.IsDisposed&&
            input==imageInputRevision&&result==resultRevision&&lifecycle.Settings?.Version==config.Version&&lifecycle.Gate.IsCaptureCurrent(ticket);
        return smartImageTask=Run();
        async Task Run(){await Task.Yield();if(Current())await action();}
    }
    partial void CollectSmartCleanup(ref Task cleanup){cleanup=smartImageTask;}
    // Local approval is not consent to HTTP. The original per-image confirmation remains separate.
    private async Task<bool> SubmitSmartScreenshot(SmartImageApproval? approval,Func<ProductionScreenshotOffer,bool> confirm,ImageRegenerationCommand? regeneration=null)
    {
        ProductionScreenshotOffer? offer=null;ProductionScreenshotCoordinator? flow=null;bool launched=false;List<Exception> errors=[];
        try
        {
            if(!exiting&&imageTranslation is {} current&&SelectedSource.Image is {} image&&retainedImage is {} retained&&ReferenceEquals(retained.Image,image))
            {
                flow=current;
                if(regeneration==null&&approval!=null)offer=flow.Prepare(retained.VersionId,image,ImageOutputMode.Automatic,approval);
                else if(regeneration!=null&&ImageRegenerationCurrent(regeneration))offer=flow.PrepareRegeneration(regeneration.CurrentSettings,regeneration.Reclassify,approval,regeneration.Mode);
                if(offer!=null&&(regeneration==null||ImageRegenerationCurrent(regeneration))&&confirm(offer)&&(regeneration==null||ImageRegenerationCurrent(regeneration)))launched=flow.Submit(offer,true);
                if(!launched&&regeneration!=null&&ImageRegenerationCurrent(regeneration))button.ToolTip=flow.LastRejection=="LOCAL_OCR_APPROVAL_REQUIRED"?"本次智能图片需要新的本地文字批准或明确视觉选择；没有发送。":flow.LastRejection=="VISION_PROFILE_REQUIRED"?"原视觉配置已改变；请明确按当前设置重新翻译，旧结果保留。":"已取消或来源、版本、设置发生改变；没有发送，旧结果保留。";
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
            if(approval!=null)ScreenshotPreviewCleanup.Attempt(errors,approval.Dispose);
        }
        if(errors.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if(errors.Count>1)throw new AggregateException("Smart image admission or original cleanup failed.",errors);
        return launched;
    }
}
