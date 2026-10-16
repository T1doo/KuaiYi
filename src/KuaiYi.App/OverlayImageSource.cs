using KuaiYi.Core;
using KuaiYi.Imaging;
using KuaiYi.Providers;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private sealed record LocalImageVersion(Guid VersionId,OwnedSourceImage Image);
    private readonly record struct SourceSelection(Guid? VersionId,TextTranslationSnapshot? Text,OwnedSourceImage? Image)
    {public bool HasSource=>VersionId!=null&&(Text!=null||Image is {IsDisposed:false});}
    private readonly record struct ImageAdmission(long Input,long Result,long Epoch,CancellationTokenSource Owner);
    private LocalImageVersion? retainedImage;
    private bool imageSelected;
    private long imageInputRevision;
    partial void OcrSourceChanged(bool clearApproved);
    partial void CollectOcrCleanup(ref Task cleanup);
    // All display/source/menu consumers use this selection. A local preview is never a request or a translation.
    private SourceSelection SelectedSource=>imageSelected
        ?retainedImage is {} image&&!image.Image.IsDisposed?new(image.VersionId,null,image.Image):default
        :new(overlayResults.SelectedVersionId,translation?.SelectedSnapshot,null);
    private TextGenerationPreview? SelectedNextGeneration=>!imageSelected?translation?.NextGeneration:null;
    private ImageAdmission CaptureImageAdmission(CancellationTokenSource owner)
    {imageInputRevision++;ClearOriginalSource();return new(imageInputRevision,resultRevision,lifecycle.Gate.Snapshot.Epoch,owner);}
    private bool ImageAdmissionCurrent(ImageAdmission admission,long epoch)=>!exiting&&!admission.Owner.IsCancellationRequested&&
        ReferenceEquals(screenshotHotkeyStop,admission.Owner)&&admission.Input==imageInputRevision&&admission.Result==resultRevision&&lifecycle.Gate.Snapshot.Epoch==epoch&&
        !lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.SessionLocked)&&lifecycle.Gate.Snapshot.Presentation!=Presentation.Hidden;
    // Consumes the cropped source on both success and rejection. No provider, consent or activation mutation.
    private bool AcceptLocalImage(ImageAdmission admission,ScreenshotIntent intent,OwnedSourceImage image)
    {
        bool accepted=false;
        try
        {
            if(!ImageAdmissionCurrent(admission,admission.Epoch)||!intent.IsCurrent)return false;
            intent.Dispose(); // Its own epoch advance is expected; a changed operation is still rejected.
            if(!ImageAdmissionCurrent(admission,checked(admission.Epoch+1))||lifecycle.Gate.Snapshot.Suspension!=Suspension.None)return false;
            lifecycle.Gate.StopGeneration();translation?.Clear();HidePresentation(clear:true);
            retainedImage=new(Guid.NewGuid(),image);imageSelected=true;accepted=true;resultRevision++;
            translationStatus="本地图片 · 未翻译 · 未发送";connectionStatus.Text="本地图片 · 未翻译";
            RefreshResultLabel();RefreshMarkdown(renderBody:true);ResetBodyReaderFollow();peek=false;OpenBody();return true;
        }
        catch(Exception error)
        {
            List<Exception> errors=[error];
            if(ReferenceEquals(retainedImage?.Image,image))
            {ScreenshotPreviewCleanup.Attempt(errors,ClearMarkdown);ScreenshotPreviewCleanup.Attempt(errors,ReleaseRetainedImage);}
            throw new ScreenshotCleanupException(errors);
        }
        finally{if(!accepted)image.Dispose();}
    }
    private void ImageSourcePreviewFailed(Exception error){if(CanReadOriginalSource())Report("图片预览未完成；可清除当前内容后重试",error);}
    private void BeginTextSource(){try{OcrSourceChanged(translation?.SelectedSnapshot?.Source.SourceKind!=SelectionSourceKind.OcrOnly);}finally{imageInputRevision++;imageSelected=false;}}
    private void ReleaseRetainedImage(){try{OcrSourceChanged(false);}finally{imageInputRevision++;imageSelected=false;retainedImage?.Image.Dispose();retainedImage=null;}}
    private bool RestoreImageAfterTextFailure(RequestIdentity? failed=null)
    {
        if(retainedImage==null||imageSelected)return false;
        if(failed is {} failure&&overlayResults.SelectedVersionId is {} selected&&selected!=failure.VersionId&&overlayResults.HasText){ReleaseRetainedImage();return false;}
        // A failed OCR-confirmed text version owns its reason/source and recovery; keep it readable.
        if(failed is {} terminal&&overlayResults.Versions.Any(v=>v.Identity==terminal&&v.Failure!=null))return false;
        ClearOriginalSource();translation?.Clear();overlayResults.Clear();ClearMarkdown();imageSelected=true;resultRevision++;ResetBodyReaderFollow();dirty=true;if(!flush.IsEnabled)flush.Start();return true;
    }
}
