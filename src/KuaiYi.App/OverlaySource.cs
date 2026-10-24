using KuaiYi.Core;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private bool CanReadOriginalSource()=>!exiting&&!lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.SessionLocked);
    private bool CanReadOriginalSourceVersion(Guid version)=>CanReadOriginalSource()&&SelectedSource.VersionId==version&&SelectedSource.HasSource&&lifecycle.Gate.Snapshot.Presentation!=Presentation.Hidden&&!lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.Capture);
    private void ValidateOriginalSourceBinding()
    {
        bodyMarkdown.RetainTextSourceChoices(overlayResults.RetainedVersionIds,SelectedSource.VersionId);
        if(!CanReadOriginalSource()||!SelectedSource.HasSource){ClearOriginalSource();return;}
        bodyMarkdown.RetainSourceVersion(SelectedSource.VersionId);measurementMarkdown.RetainSourceVersion(SelectedSource.VersionId);
    }
    private void OriginalSourceChanged(){if(!CanReadOriginalSource())return;dirty=true;if(!flush.IsEnabled)flush.Start();}
    private void ClearOriginalSource(){bodyMarkdown?.ClearSource();measurementMarkdown?.ClearSource();}
    private void ShowSelectionSource()
    {
        if(!CanReadOriginalSource()||!SelectedSource.HasSource)return;
        if(!IsVisible)ShowOverlay();if(!IsVisible)return;
        RefreshMarkdown(renderBody:true);bodyMarkdown.ExpandSource();peek=false;OpenBody();
    }
}
