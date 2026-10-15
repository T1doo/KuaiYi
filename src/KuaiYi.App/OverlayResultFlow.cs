using System.Windows;
using KuaiYi.Core;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    internal Task<bool> ReceivePresentationAsync(RequestIdentity identity,OverlayResultEvent update,CancellationToken cancellation=default)
        =>presentationReceiver.ReceiveAsync(identity,update,value=>ApplyResult(identity,value),cancellation);
    private LayoutPreferences Preferences()=>new(lifecycle.Settings?.Data.Overlay.PreferredWidthDip??420,640);
    private void ApplyResult(RequestIdentity identity,OverlayResultEvent value,bool alreadyApplied=false)
    {
        if(exiting)return;
        switch(value.Kind)
        {
            case OverlayResultEventKind.Begin:
                if(!alreadyApplied&&!overlayResults.Begin(identity))return;ClearOriginalSource();resultRevision++;ResetBodyReaderFollow();dirty=true;peek=false;pendingReveal=false;awaitingText=true;progressRing.Visibility=Visibility.Visible;if(!gesture.Active)overlayFrames?.Collapse();StartFrames();waiting.Stop();waiting.Start();Dispatcher.BeginInvoke(()=>{if(!exiting)Refresh();});break;
            case OverlayResultEventKind.Delta:
                if(!alreadyApplied&&!overlayResults.Append(identity,value.Text))return;dirty=true;awaitingText=false;progressRing.Visibility=Visibility.Collapsed;waiting.Stop();
                if(overlayResults.TryAutoReveal(identity)&&(peek||overlayFrames?.IsExpanded!=true))pendingReveal=true;break;
            case OverlayResultEventKind.Complete:
                if(!alreadyApplied&&!overlayResults.Complete(identity))return;dirty=true;awaitingText=false;progressRing.Visibility=Visibility.Collapsed;waiting.Stop();break;
            case OverlayResultEventKind.Stopped:if(!alreadyApplied)overlayResults.MarkStopped();dirty=true;awaitingText=false;progressRing.Visibility=Visibility.Collapsed;waiting.Stop();break;
            default:throw new ArgumentOutOfRangeException(nameof(value));
        }
        ValidateOriginalSourceBinding();if(dirty&&!flush.IsEnabled)flush.Start();
    }
    private int MeasureBody(LayoutPlan plan)
    {
        if(overlayFrames==null)return 48;
        bool hasLabel=resultLabel.Text.Length>0;
        return plan.Dpi.CeilingPixels(48+32+(hasLabel?20:0)+MeasureMarkdown(plan,hasLabel));
    }
    private void OpenBody(){if(overlayFrames==null)return;overlayFrames.OpenMeasured(Preferences(),MeasureBody);dirty=true;if(!flush.IsEnabled)flush.Start();StartFrames();}
    private void FlushResult()
    {
        if(!dirty||exiting)return;dirty=false;
        double offset=body.VerticalOffset;RefreshMarkdown(renderBody:true);bool bottom=follow;
        newContent.Visibility=!bottom&&overlayResults.HasText?Visibility.Visible:Visibility.Collapsed;
        ScheduleBodyScrollRestore(bottom,offset);
        if(overlayFrames!=null&&!gesture.Active&&overlayFrames.Transition!=OverlayTransition.Moving)
        {
            if(pendingReveal&&overlayResults.Identity is {} id&&overlayResults.TryAutoReveal(id)){pendingReveal=false;peek=false;OpenBody();}
            else if(overlayFrames.IsExpanded&&!peek&&overlayFrames.Plan is {} plan){overlayFrames.Grow(MeasureBody(plan));StartFrames();}
        }
        ApplyOverlayFrame();
    }
    private void CollapseResult(){InvalidateBodyScrollRestore();ClearOriginalSource();peek=false;pendingReveal=false;peekIn.Stop();peekOut.Stop();waiting.Stop();overlayResults.Collapse();overlayFrames?.Collapse();StartFrames();}
    private void StopResult()
    {
        if(exiting||!lifecycle.Gate.HasRunningGeneration)return;
        // Cancellation rejects every late receiver event, so the local command must commit
        // the terminal presentation through the existing coalesced flush, not await Stopped.
        lifecycle.Gate.StopGeneration();overlayResults.MarkStopped();pendingReveal=false;
        awaitingText=false;progressRing.Visibility=Visibility.Collapsed;waiting.Stop();
        RefreshResultLabel();dirty=true;if(!flush.IsEnabled)flush.Start();
        button.ToolTip="已停止生成 · 结果保留在本次会话";
    }
    private void FlushResultTick(object? sender,EventArgs e){flush.Stop();FlushResult();}
}
