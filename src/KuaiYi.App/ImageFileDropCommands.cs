using KuaiYi.Core;
using KuaiYi.Imaging;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private bool imageFileDropEnabled;private Task droppedImageFileTask=Task.CompletedTask;
    private void SetImageFileDropEnabled(bool value)
    {
        if(imageFileDropEnabled==value)return;imageFileDropEnabled=value;
        if(!value&&ReferenceEquals(fileImageTask,droppedImageFileTask))CancelFileAcquisition();
        RevokeTextDrop();RefreshTextDrop(); // Original peer must settle before its frozen feature flag can change.
    }
    private Task StartDroppedImageFile(string path,Func<IFileImageOperation> create)
    {
        var state=lifecycle.Gate.Snapshot;
        if(!imageFileDropEnabled||exiting||state.Activation!=Activation.On||state.Mode!=TriggerMode.Drop||state.Suspension!=Suspension.None)return Task.CompletedTask;
        if(FileImageBusy){button.ToolTip="原图片任务或清理尚未确认；未接收新投放，旧来源保留。";return Task.CompletedTask;}
        return droppedImageFileTask=StartOpenImage(path,create); // Same owned task, secure file read, preparation, preview and explicit send path.
    }
}
