using KuaiYi.Core;
using KuaiYi.Core.Installation;
using KuaiYi.Imaging;
using KuaiYi.Providers;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private sealed record LocalImageVersion(Guid VersionId,ImageSourceOwner Image);
    private readonly record struct SourceSelection(Guid? VersionId,TextTranslationSnapshot? Text,ImageSourceOwner? Image,ScreenshotGenerationSnapshot? Generation=null)
    {public bool HasSource=>VersionId!=null&&(Text!=null||Image is {IsDisposed:false});}
    private readonly record struct ImageAdmission(long Input,long Result,long Epoch,CancellationTokenSource Owner,bool File,CancellationToken External=default,Func<bool>? Current=null);
    private LocalImageVersion? retainedImage;
    private bool imageSelected;
    private long imageInputRevision;
    private CancellationTokenSource? imageAdmissionOwner;
    partial void CancelFileAcquisition();
    partial void CollectFileCleanup(ref Task cleanup);
    partial void OcrSourceChanged(bool clearApproved);
    partial void CollectOcrCleanup(ref Task cleanup);
    // All display/source/menu consumers use this selection. A local preview is never a request or a translation.
    private SourceSelection SelectedSource
    {
        get
        {
            var id=overlayResults.SelectedVersionId;
            if(id is {} result&&imageTranslation?.SnapshotForVersion(result) is {} generation)
            {
                var owner=retainedImage is {} retained&&retained.VersionId==generation.SourceVersion&&retained.Image.SourceId==generation.SourceId&&!retained.Image.IsDisposed?retained.Image:null;
                return new(result,null,owner,generation);
            }
            if(id is {} textId&&translation?.SnapshotForVersion(textId) is {} text)return new(textId,text,null);
            return imageSelected&&retainedImage is {} image&&!image.Image.IsDisposed?new(image.VersionId,null,image.Image):new(id,translation?.SelectedSnapshot,null);
        }
    }
    private TextGenerationPreview? SelectedNextGeneration=>!imageSelected?translation?.NextGeneration:null;
    private ImageAdmission CaptureImageAdmission(CancellationTokenSource owner,bool file=false,CancellationToken external=default,Func<bool>? current=null)
    {imageAdmissionOwner=owner;imageInputRevision++;ClearOriginalSource();return new(imageInputRevision,resultRevision,lifecycle.Gate.Snapshot.Epoch,owner,file,external,current);}
    private bool ImageAdmissionCurrent(ImageAdmission admission,long epoch)=>!exiting&&!admission.External.IsCancellationRequested&&admission.Current?.Invoke()!=false&&!admission.Owner.IsCancellationRequested&&
        ReferenceEquals(imageAdmissionOwner,admission.Owner)&&ReferenceEquals(admission.File?fileImageStop:screenshotHotkeyStop,admission.Owner)&&admission.Input==imageInputRevision&&admission.Result==resultRevision&&lifecycle.Gate.Snapshot.Epoch==epoch&&
        !lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.SessionLocked)&&lifecycle.Gate.Snapshot.Presentation!=Presentation.Hidden;
    // Consumes the cropped source on both success and rejection. No provider, consent or activation mutation.
    private bool AcceptLocalImage(ImageAdmission admission,ScreenshotIntent intent,ImageSourceOwner image)=>AcceptImage(admission,()=>intent.IsCurrent,intent.Dispose,image);
    private bool AcceptFileImage(ImageAdmission admission,FileImageIntent intent,OwnedFileImage image,InstallationWarmImageAdmission? warm=null,InstallationWarmLease? lease=null)
    {
        if(warm!=null){if(lease==null){image.Dispose();throw new InvalidOperationException("Original warm lease required.");}return AcceptWarmFileImage(warm,lease,admission,intent,image);}
        bool accepted=AcceptImage(admission,()=>intent.IsCurrent,intent.Dispose,image);if(accepted)ShowSelectionSource();return accepted;}
    private bool AcceptImage(ImageAdmission admission,Func<bool> current,Action release,ImageSourceOwner image)
    {
        bool accepted=false;
        try
        {
            if(!ImageAdmissionCurrent(admission,admission.Epoch)||!current())return false;
            release(); // Its own epoch advance is expected; a changed operation is still rejected.
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
    private void BeginTextSource(){try{CancelFileAcquisition();imageTranslation?.Stop();OcrSourceChanged(translation?.SelectedSnapshot?.Source.SourceKind!=SelectionSourceKind.OcrOnly);}finally{imageInputRevision++;imageSelected=false;}}
    private void ReleaseRetainedImage(){try{if(retainedImage is {} retained)imageTranslation?.RevokeSource(retained.VersionId,retained.Image.SourceId);OcrSourceChanged(false);}finally{imageInputRevision++;imageSelected=false;retainedImage?.Image.Dispose();retainedImage=null;}}
    private bool RestoreImageAfterTextFailure(RequestIdentity? failed=null)
    {
        if(retainedImage==null||imageSelected)return false;
        if(failed is {} failure&&overlayResults.SelectedVersionId is {} selected&&selected!=failure.VersionId&&overlayResults.HasText)
        {
            // Cross-source failure can restore a completed image version. Keep only its exact still-owned crop.
            if(imageTranslation?.SnapshotForVersion(selected) is {} generation&&retainedImage.VersionId==generation.SourceVersion&&
                retainedImage.Image.SourceId==generation.SourceId&&!retainedImage.Image.IsDisposed)return false;
            ReleaseRetainedImage();return false;
        }
        // A failed OCR-confirmed text version owns its reason/source and recovery; keep it readable.
        if(failed is {} terminal&&overlayResults.Versions.Any(v=>v.Identity==terminal&&v.Failure!=null))return false;
        ClearOriginalSource();translation?.Clear();overlayResults.Clear();ClearMarkdown();imageSelected=true;resultRevision++;ResetBodyReaderFollow();dirty=true;if(!flush.IsEnabled)flush.Start();return true;
    }
}
