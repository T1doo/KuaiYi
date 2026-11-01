using KuaiYi.Core;
using KuaiYi.Imaging;
using KuaiYi.Providers;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    // Availability only, never source/HTTP authority. No setting, receipt or test override can open it.
    private static bool ImageRegenerationQualified=>false;
    private const string ImageRegenerationQualificationNotice="图片重新生成界面已接线，真实 Windows 生命周期尚未验收，当前保持关闭；原结果保留。";
    private sealed record OriginalOcrScope(OcrOnlyCoordinator Owner,Task Task,RequestIdentity Identity);
    private sealed class ImageRegenerationCommand(ProductionScreenshotCoordinator flow,Guid selected,Guid version,ImageSourceOwner image,
        string settings,long input,long result,long epoch,bool currentSettings,bool reclassify,ImageOutputMode mode)
    {
        internal readonly ProductionScreenshotCoordinator Flow=flow;
        internal readonly Guid Selected=selected,SourceVersion=version,SourceId=image.SourceId;
        internal readonly ImageSourceOwner Image=image;
        internal readonly string Settings=settings;
        internal readonly long Input=input,Result=result;
        internal long ExpectedEpoch=epoch;
        internal OriginalOcrScope? Ocr;
        internal readonly bool CurrentSettings=currentSettings,Reclassify=reclassify;
        internal readonly ImageOutputMode Mode=mode;
    }
    private bool ImageRegenerationCurrent(ImageRegenerationCommand command,bool requireEpoch=true)
    {
        var selected=SelectedSource;var state=lifecycle.Gate.Snapshot;
        return !exiting&&ReferenceEquals(imageTranslation,command.Flow)&&selected.VersionId==command.Selected&&
            selected.Generation is {} generation&&generation.SourceVersion==command.SourceVersion&&generation.SourceId==command.SourceId&&
            ReferenceEquals(selected.Image,command.Image)&&retainedImage is {} retained&&retained.VersionId==command.SourceVersion&&ReferenceEquals(retained.Image,command.Image)&&
            !command.Image.IsDisposed&&command.Image.SourceId==command.SourceId&&imageInputRevision==command.Input&&resultRevision==command.Result&&
            lifecycle.Settings is {ReadOnly:false,RequiresRecovery:false} settings&&settings.Version==command.Settings&&
            state.Activation==Activation.On&&state.Suspension==Suspension.None&&state.Presentation!=Presentation.Hidden&&(!requireEpoch||state.Epoch==command.ExpectedEpoch);
    }
    // Called only immediately after this original OCR Discover/Start reports actual admission.
    // Both operations advance exactly once; external Off/on or any unrelated epoch is never adopted.
    private bool AdvanceImageRegenerationOcr(ImageRegenerationCommand command,OcrOnlyCoordinator owner,Task original,RequestIdentity admitted)
    {
        if(admitted==default)throw new InvalidOperationException("Original OCR admission identity missing.");
        // Cleanup owns this actual original even when synchronous UI invalidation rejects continuation.
        command.Ocr=new(owner,original,admitted);
        long now=lifecycle.Gate.Snapshot.Epoch;
        if(command.ExpectedEpoch==long.MaxValue||admitted.Epoch!=command.ExpectedEpoch+1||now!=admitted.Epoch||!ImageRegenerationCurrent(command,false))return false;
        command.ExpectedEpoch=admitted.Epoch;return ImageRegenerationCurrent(command);
    }
    private bool ImageRegenerationOcrAdmission(ImageRegenerationCommand? command,OcrOnlyCoordinator owner,bool admitted)
    {
        if(!admitted&&command!=null&&owner.CleanupPending)throw new InvalidOperationException("Previous OCR owner cleanup remains unconfirmed.");
        return admitted;
    }
    private void ReleaseImageRegenerationOcrOutcomes(ImageRegenerationCommand command,OcrOnlyCoordinator owner,List<Exception> errors)
    {
        if(command.Ocr is not {} scope||!ReferenceEquals(scope.Owner,owner))return;
        if(!scope.Task.IsCompleted){errors.Add(new InvalidOperationException("Original OCR task has not settled."));return;}
        bool Owns(Guid version,Guid source,string settings,RequestIdentity identity)=>version==command.SourceVersion&&source==command.SourceId&&settings==command.Settings&&identity==scope.Identity;
        ScreenshotPreviewCleanup.Attempt(errors,()=>{if(owner.Capabilities is {} value&&Owns(value.SourceVersion,value.SourceId,value.SettingsVersion,value.Identity))owner.RejectCapabilities(value);});
        ScreenshotPreviewCleanup.Attempt(errors,()=>{if(owner.Preview is {} value&&Owns(value.SourceVersion,value.SourceId,value.SettingsVersion,value.Identity))owner.RejectPreview(value);});
        ScreenshotPreviewCleanup.Attempt(errors,()=>{if(owner.VisionCandidate is {} value&&Owns(value.SourceVersion,value.SourceId,value.SettingsVersion,value.Identity))owner.RejectVision(value);});
    }
    private async Task<bool> SettleImageRegenerationOcr(ImageRegenerationCommand command,OcrOnlyCoordinator owner,Task original,bool advanced)
    {
        await original;
        if(owner.CleanupPending)throw new InvalidOperationException("Original image regeneration OCR cleanup unconfirmed.");
        return advanced&&command.Ocr is {} scope&&ReferenceEquals(scope.Owner,owner)&&ReferenceEquals(scope.Task,original)&&ImageRegenerationCurrent(command);
    }
    private ImageRegenerationCommand? CaptureImageRegeneration(bool currentSettings,bool reclassify,ImageOutputMode? mode)
    {
        if(!smartImageTask.IsCompletedSuccessfully){button.ToolTip="原图片操作尚未确认结束，请先停止或清除当前内容；没有重发。";return null;}
        var selected=SelectedSource;
        if(imageTranslation is not {} flow||selected.VersionId is not {} id||selected.Generation is not {} basis||selected.Image is not {} image||
            retainedImage is not {} retained||!ReferenceEquals(retained.Image,image)||retained.VersionId!=basis.SourceVersion||image.SourceId!=basis.SourceId||lifecycle.Settings is not {} config)
        {button.ToolTip="原图片或所选版本已不可用，请回看仍保留的版本；没有发送。";return null;}
        if(!reclassify&&(basis.Classification==null||basis.Transform==null||basis.ClassificationUsage==null))
        {button.ToolTip="当前版本没有有效的完整分类；如需再次判断，请明确选择“重新判断图片”。旧请求与结果保留。";return null;}
        var requested=mode??basis.RequestedMode;if(!Enum.IsDefined(requested))return null;
        var command=new ImageRegenerationCommand(flow,id,retained.VersionId,image,config.Version,imageInputRevision,resultRevision,lifecycle.Gate.Snapshot.Epoch,currentSettings,reclassify,requested);
        if(ImageRegenerationCurrent(command))return command;
        button.ToolTip="来源、设置或启用状态已改变；没有发送，旧结果保留。";return null;
    }
    private bool CanRegenerateImage=>ImageRegenerationQualified&&SelectedSource.Generation!=null&&SelectedSource.Image is {IsDisposed:false}&&smartImageTask.IsCompletedSuccessfully&&
        lifecycle.Gate.Snapshot.Activation==Activation.On&&lifecycle.Gate.Snapshot.Suspension==Suspension.None;
    private Task StartImageRegeneration(bool currentSettings,bool reclassify,ImageOutputMode? mode,Func<ImageRegenerationCommand,Task> execute)
    {
        var command=CaptureImageRegeneration(currentSettings,reclassify,mode);
        return command==null?Task.CompletedTask:StartSmartImageWorkflow(()=>execute(command),command);
    }
    private Task StartQualifiedImageRegeneration(bool currentSettings,bool reclassify,ImageOutputMode? mode,Func<ImageRegenerationCommand,Task> execute)
    {
        if(!ImageRegenerationQualified){button.ToolTip=ImageRegenerationQualificationNotice;return Task.CompletedTask;}
        return StartImageRegeneration(currentSettings,reclassify,mode,execute);
    }
}
