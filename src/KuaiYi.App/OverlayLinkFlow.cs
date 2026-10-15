using KuaiYi.Core;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private bool CanOpenMarkdownVersion(Guid version)=>!exiting&&IsVisible&&overlayFrames?.IsExpanded==true&&!peek&&
        lifecycle.Gate.Snapshot.Suspension==Suspension.None&&lifecycle.Gate.Snapshot.Presentation!=Presentation.Hidden&&
        SelectedSource.Image==null&&overlayResults.SelectedVersionId==version&&overlayResults.Versions.Any(v=>v.Identity.VersionId==version);
    private void OpenMarkdownLink(Guid version,MarkdownHttpTarget target,Func<bool> linkCurrent)
    {
        if(!CanOpenMarkdownVersion(version)||!linkCurrent())return;
        long epoch=lifecycle.Gate.Snapshot.Epoch,revision=resultRevision;var reading=CaptureBodyReaderGuard();
        bool Current()=>CanOpenMarkdownVersion(version)&&linkCurrent()&&revision==resultRevision&&epoch==lifecycle.Gate.Snapshot.Epoch&&reading();
        CreateMarkdownLinkActions().Activate(target,Current);
    }
}
