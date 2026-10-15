using KuaiYi.Core;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private bool CanReadOriginalSource()=>!exiting&&!lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.SessionLocked);
    private bool CanReadOriginalSourceVersion(Guid version)=>CanReadOriginalSource()&&overlayResults.SelectedVersionId==version&&translation?.SelectedSnapshot!=null;
    private void ValidateOriginalSourceBinding()
    {
        if(!CanReadOriginalSource()||translation?.SelectedSnapshot==null){ClearOriginalSource();return;}
        bodyMarkdown.RetainSourceVersion(overlayResults.SelectedVersionId);measurementMarkdown.RetainSourceVersion(overlayResults.SelectedVersionId);
    }
    private void OriginalSourceChanged(){if(!CanReadOriginalSource())return;dirty=true;if(!flush.IsEnabled)flush.Start();}
    private void ClearOriginalSource(){bodyMarkdown?.ClearSource();measurementMarkdown?.ClearSource();}
    private void ShowSelectionSource()
    {
        if(!CanReadOriginalSource()||translation?.SelectedSnapshot==null)return;
        if(!IsVisible)ShowOverlay();if(!IsVisible)return;
        RefreshMarkdown(renderBody:true);bodyMarkdown.ExpandSource();peek=false;OpenBody();
    }
}
