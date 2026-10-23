using System.Windows;
using KuaiYi.Core;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    internal Task<bool> ReceivePresentationAsync(RequestIdentity identity,OverlayResultEvent update,CancellationToken cancellation=default)
        =>presentationReceiver.ReceiveAsync(identity,update,value=>ApplyResult(identity,value),cancellation);
    private LayoutPreferences Preferences()=>new(lifecycle.Settings?.Data.Overlay.PreferredWidthDip??420,640);
    private void ApplyResult(RequestIdentity identity,OverlayResultEvent value,bool alreadyApplied=false,bool imageGeneration=false)
    {
        if(exiting)return;
        switch(value.Kind)
        {
            case OverlayResultEventKind.Usage:RefreshResultLabel();return;
            case OverlayResultEventKind.Begin:
                pendingFailureReveal=null;
                if(!alreadyApplied&&!overlayResults.Begin(identity))return;if(!imageGeneration)BeginTextSource();ClearOriginalSource();resultRevision++;ResetBodyReaderFollow();dirty=true;peek=false;pendingReveal=false;awaitingText=true;progressRing.Visibility=Visibility.Visible;if(!gesture.Active)overlayFrames?.Collapse();StartFrames();waiting.Stop();waiting.Start();Dispatcher.BeginInvoke(()=>{if(!exiting)Refresh();});break;
            case OverlayResultEventKind.Delta:
                if(!alreadyApplied&&!overlayResults.Append(identity,value.Text))return;dirty=true;awaitingText=false;progressRing.Visibility=Visibility.Collapsed;waiting.Stop();
                if(overlayResults.TryAutoReveal(identity)&&(peek||overlayFrames?.IsExpanded!=true))pendingReveal=true;break;
            case OverlayResultEventKind.Complete:
                if(!alreadyApplied&&!overlayResults.Complete(identity))return;if(!imageGeneration)ReleaseRetainedImage();dirty=true;awaitingText=false;progressRing.Visibility=Visibility.Collapsed;waiting.Stop();break;
            case OverlayResultEventKind.Stopped:if(!alreadyApplied)overlayResults.MarkStopped();OfferFailureReveal(identity);dirty=true;awaitingText=false;progressRing.Visibility=Visibility.Collapsed;waiting.Stop();break;
            default:throw new ArgumentOutOfRangeException(nameof(value));
        }
        ValidateOriginalSourceBinding();if(dirty&&!flush.IsEnabled)flush.Start();
    }
    private int MeasureBody(LayoutPlan plan,Func<bool>? current=null)
    {
        if(overlayFrames==null)return 48;
        bool hasLabel=resultLabel.Text.Length>0;
        double chrome=48+32+(hasLabel?20:0)+FailureNoticeHeight;
        var observer=PresentationMeasureObservedForTest;MarkdownMeasureObservation? observation=null;
        int pixels=plan.Dpi.CeilingPixels(chrome+MeasureMarkdown(plan,hasLabel,current,observer==null?null:value=>observation=value));
        if(observer!=null&&observation!=null)observer(new(System.Diagnostics.Stopwatch.GetTimestamp(),observation,chrome,pixels,
            plan.MaximumHeightPixels,Math.Clamp(pixels,plan.Anchor.Height,plan.MaximumHeightPixels),plan.Dpi.Factor));
        return pixels;
    }
    private void OpenBody(Func<bool>? current=null){if(overlayFrames==null||current?.Invoke()==false)return;overlayFrames.OpenMeasured(Preferences(),plan=>MeasureBody(plan,current),current);if(current?.Invoke()==false)return;dirty=true;if(!flush.IsEnabled)flush.Start();StartFrames();}
    private void FlushResult()
    {
        if(!dirty||exiting)return;dirty=false;
        double offset=body.VerticalOffset;RefreshMarkdown(renderBody:true);bool bottom=follow;
        newContent.Visibility=!bottom&&overlayResults.HasText?Visibility.Visible:Visibility.Collapsed;
        ScheduleBodyScrollRestore(bottom,offset);
        if(overlayFrames!=null&&!gesture.Active&&overlayFrames.Transition!=OverlayTransition.Moving)
        {
            if(TryRevealFailure()){peek=false;OpenBody();}
            else if(pendingReveal&&overlayResults.Identity is {} id&&overlayResults.TryAutoReveal(id)){pendingReveal=false;peek=false;OpenBody();}
            else if(overlayFrames.IsExpanded&&!peek&&overlayFrames.Plan is {} plan){overlayFrames.Grow(MeasureBody(plan));StartFrames();}
        }
        ApplyOverlayFrame();
    }
    private void CollapseResult(){InvalidateBodyScrollRestore();ClearOriginalSource();pendingFailureReveal=null;peek=false;pendingReveal=false;peekIn.Stop();peekOut.Stop();waiting.Stop();overlayResults.Collapse();overlayFrames?.Collapse();StartFrames();}
    private void StopResult()
    {
        if(exiting)return;
        CancelFileAcquisition();if(!lifecycle.Gate.HasRunningGeneration)return;
        // Cancellation rejects every late receiver event, so the local command must commit
        // the terminal presentation through the existing coalesced flush, not await Stopped.
        bool imageGeneration=SelectedSource.Generation!=null;if(imageGeneration)imageTranslation?.Stop();lifecycle.Gate.StopGeneration();overlayResults.MarkStopped();pendingReveal=false;
        if(!imageGeneration){if(overlayResults.HasText)ReleaseRetainedImage();else RestoreImageAfterTextFailure(overlayResults.Identity);}
        awaitingText=false;progressRing.Visibility=Visibility.Collapsed;waiting.Stop();
        RefreshResultLabel();dirty=true;if(!flush.IsEnabled)flush.Start();
        button.ToolTip="已停止生成 · 结果保留在本次会话";
    }
    private void FlushResultTick(object? sender,EventArgs e){flush.Stop();FlushResult();}
    private void ReopenResult(){if(!IsVisible)ShowOverlay();if(LocalImagePreview?lifecycle.Gate.TryReopen(true):overlayResults.Reopen()){peek=false;OpenBody();}}
    private void HidePresentation(bool clear)
    {
        List<Exception> errors=[];ScreenshotPreviewCleanup.Attempt(errors,()=>OcrSourceChanged(true));InvalidateBodyScrollRestore();ScreenshotPreviewCleanup.Attempt(errors,ClearOriginalSource);pendingFailureReveal=null;peek=false;
        ScreenshotPreviewCleanup.Attempt(errors,()=>overflowMenu.IsOpen=false);waiting.Stop();flush.Stop();peekIn.Stop();peekOut.Stop();StopFrames();awaitingText=false;progressRing.Visibility=Visibility.Collapsed;overlayResults.MarkStopped();
        if(clear)
        {
            ScreenshotPreviewCleanup.Attempt(errors,()=>imageTranslation?.Clear());
            ScreenshotPreviewCleanup.Attempt(errors,ClearMarkdown);ScreenshotPreviewCleanup.Attempt(errors,ReleaseRetainedImage);overlayResults.Clear();resultRevision++;
        }
        overlayFrames?.Compact();ScreenshotPreviewCleanup.Attempt(errors,()=>ApplyOverlayFrame());
        if(errors.Count>0)throw new AggregateException("Presentation cleanup unconfirmed.",errors);
    }
}
