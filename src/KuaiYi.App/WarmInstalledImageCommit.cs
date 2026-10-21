using KuaiYi.Core;
using KuaiYi.Core.Installation;
using KuaiYi.Imaging;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private void RefreshWarmImageLabel(OwnedFileImage image,Func<bool> current)
    {
        if(!current())return;
        resultLabel.Text=$"文件图片{(image.Notice is {} notice?" · "+notice:"")} · 未翻译 · {image.Width} × {image.Height}";if(!current())return;
        resultLabel.ToolTip="仅保留所选文件的归一化图片；未翻译、未发送，不保留文件路径。";if(!current())return;
        foreach(var tool in tools.ToArray())
        {
            if(Equals(tool.Content,"重新生成")||Equals(tool.Content,"复制")){tool.IsEnabled=false;if(!current())return;}
            if(Equals(tool.Tag,"翻译截图")||Equals(tool.Content,"翻译截图")){tool.Content="翻译图片";if(!current())return;tool.IsEnabled=CanTranslateScreenshot;if(!current())return;}
            if(Equals(tool.Content,"查看原文")){tool.IsEnabled=true;if(!current())return;}
        }
    }
    // WarmSync -> Gate only. Everything in the publish callback is managed memory, with the Worker already finalized.
    private bool AcceptWarmFileImage(InstallationWarmImageAdmission warm,InstallationWarmLease lease,ImageAdmission admission,FileImageIntent intent,OwnedFileImage image)
    {
        var candidate=new LocalImageVersion(Guid.NewGuid(),image);LocalImageVersion? previous=null;
        CancellationTokenSource? detached=null;bool published=false;
        long revision=0;var initial=lifecycle.Gate.Snapshot.Activation;string settingsVersion=lifecycle.Settings?.Version??"";
        bool StillSelected()
        {
            var state=lifecycle.Gate.Snapshot;
            return !exiting&&!Volatile.Read(ref installedImageClosed)&&ReferenceEquals(retainedImage,candidate)&&imageSelected&&!image.IsDisposed&&
                SelectedSource.VersionId==candidate.VersionId&&ReferenceEquals(SelectedSource.Image,image)&&resultRevision==revision&&
                state.Epoch==checked(admission.Epoch+2)&&state.Activation==initial&&state.Suspension==Suspension.None&&state.Presentation!=Presentation.Hidden&&
                (lifecycle.Settings?.Version??"")==settingsVersion;
        }
        try
        {
            if(!warm.TryAccept(lease,()=>
            {
                var accepted=new InstallationWarmAccepted(image.SourceId,fileImageVersion,candidate.VersionId);
                if(!intent.TryCommitLocalImage(()=>!Volatile.Read(ref installedImageClosed)&&ImageAdmissionCurrent(admission,admission.Epoch),()=>
                {
                    previous=retainedImage;retainedImage=candidate;imageSelected=true;revision=++resultRevision;overlayResults.Clear();published=true;
                },out detached))return null;
                return accepted;
            },previewPending:true))return false;
            // No WarmSync/Gate lock is held by any cancellation, disposal, rendering or native operation below.
            List<Exception> cancellationErrors=[];
            if(detached!=null){ScreenshotPreviewCleanup.Attempt(cancellationErrors,detached.Cancel);ScreenshotPreviewCleanup.Attempt(cancellationErrors,detached.Dispose);}
            if(previous!=null)ScreenshotPreviewCleanup.Attempt(cancellationErrors,previous.Image.Dispose);
            if(cancellationErrors.Count!=0)throw new AggregateException("Previous local source cleanup failed.",cancellationErrors);
            if(!StillSelected())return false;translation?.Clear();if(!StillSelected())return false;
            imageTranslation?.Clear();if(!StillSelected())return false;
            OcrSourceChanged(true);if(!StillSelected())return false;
            translationStatus="本地图片 · 未翻译 · 未发送";connectionStatus.Text="本地图片 · 未翻译";if(!StillSelected())return false;
            RefreshWarmImageLabel(image,StillSelected);if(!StillSelected())return false;
            RefreshMarkdown(renderBody:true,StillSelected);if(!StillSelected())return false;
            bodyMarkdown.ExpandImageSource(candidate.VersionId,image);if(!StillSelected())return false;
            ResetBodyReaderFollow();if(!StillSelected())return false;peek=false;OpenBody(StillSelected);if(!StillSelected())return false;
            return warm.ConfirmPreview(lease,()=>StillSelected()&&bodyMarkdown.IsImagePreviewReady(candidate.VersionId,image));
        }
        catch(Exception error)
        {
            List<Exception> errors=[error];
            // Capture exact regions before any Dispose callback can reenter and select another source/view.
            var clearBody=bodyMarkdown.CaptureImageSourceCleanup(candidate.VersionId,image);
            var clearMeasure=measurementMarkdown.CaptureImageSourceCleanup(candidate.VersionId,image);
            ScreenshotPreviewCleanup.Attempt(errors,clearBody);ScreenshotPreviewCleanup.Attempt(errors,clearMeasure);
            if(ReferenceEquals(retainedImage,candidate)&&imageSelected&&SelectedSource.VersionId==candidate.VersionId)
            {retainedImage=null;imageSelected=false;imageInputRevision++;resultRevision++;ScreenshotPreviewCleanup.Attempt(errors,image.Dispose);}
            throw new ScreenshotCleanupException(errors);
        }
        finally{if(!published)image.Dispose();}
    }
}
