using System.Windows.Controls;
using KuaiYi.Core;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private RequestIdentity? ScreenshotRecoveryIdentity
    {
        get
        {
            if(FailureNoticeVersion is {} failed&&imageTranslation?.SnapshotForVersion(failed.Identity.VersionId)!=null)return failed.Identity;
            var selected=overlayResults.Versions.FirstOrDefault(v=>v.Identity.VersionId==overlayResults.SelectedVersionId);
            return selected is {Complete:false,Stopped:true}&&imageTranslation?.SnapshotForVersion(selected.Identity.VersionId)!=null?selected.Identity:null;
        }
    }
    private bool CanRecaptureScreenshot=>!exiting&&IsVisible&&lifecycle.Gate.Snapshot.Presentation!=Presentation.Hidden&&lifecycle.Gate.Snapshot.Suspension==Suspension.None&&
        screenshotHotkeyTask.IsCompletedSuccessfully&&screenshotHotkeyCleanup.IsCompletedSuccessfully&&screenshotHotkeyOwner==null;
    private void AddScreenshotRecovery(ContextMenu menu)
    {
        if(ScreenshotRecoveryIdentity is not {} recovery)return;
        var item=new MenuItem{Header="重新截图",IsEnabled=CanRecaptureScreenshot,ToolTip="重新框选新区域；取消保留当前结果。新截图先本地预览，发送前另行确认。"};
        item.Click+=(_,_)=>_=StartMenuScreenshot(recovery);menu.Items.Add(item);
    }
}
