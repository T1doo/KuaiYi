using System.Windows.Threading;
using System.Windows;
using KuaiYi.Core;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    // UI destination only: a newer flush or reader intent invalidates deferred layout work.
    private long bodyScrollRevision,bodyReaderIntentRevision;
    private bool codeReaderPaused;
    private void InvalidateBodyScrollRestore(){bodyScrollRevision++;bodyReaderIntentRevision++;}
    private void ForgetCodeReaderIntent(){InvalidateBodyScrollRestore();codeReaderPaused=false;}
    private void ResetBodyReaderFollow(){ForgetCodeReaderIntent();follow=true;}
    private void FollowBodyEnd(){ResetBodyReaderFollow();bodyMarkdown?.FollowTailEnd();body.ScrollToEnd();}
    private void ObserveCodeReaderIntent(){InvalidateBodyScrollRestore();codeReaderPaused=true;follow=false;}
    private Func<bool> CaptureBodyReaderGuard()
    {
        long input=bodyReaderIntentRevision,revision=resultRevision;
        return()=>!exiting&&input==bodyReaderIntentRevision&&revision==resultRevision&&!lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.SessionLocked);
    }
    private void ObserveBodyReaderIntent()
    {
        InvalidateBodyScrollRestore();follow=false;
        long scroll=bodyScrollRevision,revision=resultRevision;
        // Read after input/layout work. A newer flush conservatively keeps reading paused.
        Dispatcher.BeginInvoke(DispatcherPriority.Background,()=>
        {
            if(exiting||scroll!=bodyScrollRevision||revision!=resultRevision||lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.SessionLocked))return;
            if(codeReaderPaused)return;
            follow=body.ExtentHeight-body.ViewportHeight-body.VerticalOffset<=24;
            if(follow)newContent.Visibility=Visibility.Collapsed;
        });
    }
    private void ScheduleBodyScrollRestore(bool bottom,double offset)
    {
        long scroll=++bodyScrollRevision,revision=resultRevision;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>
        {
            if(exiting||scroll!=bodyScrollRevision||revision!=resultRevision||lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.SessionLocked))return;
            if(bottom&&follow)body.ScrollToEnd();else body.ScrollToVerticalOffset(offset);
        });
    }
}
