using System.IO;
using System.Windows.Controls;
using KuaiYi.Core;
using KuaiYi.Imaging;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private bool imageBitmapDropEnabled;
    private void AddBitmapDropMenu(ContextMenu menu)
    {
        var item=new MenuItem{Header="接收单张位图投放（仅预览）",IsCheckable=true,IsChecked=imageBitmapDropEnabled,ToolTip="仅本次运行、Drop 模式；Off 也只本地预览。仅 CF_DIB 40 字节头、24bit BI_RGB，转换后 BMP 最多 1 MiB；更大图片请使用打开图片。发送仍逐次确认。"};
        item.Click+=(_,_)=>SetBitmapDropEnabled(item.IsChecked);menu.Items.Add(item);menu.Opened+=(_,_)=>{item.IsChecked=imageBitmapDropEnabled;item.IsEnabled=!exiting&&(imageBitmapDropEnabled||!FileImageBusy);};
    }
    private void SetBitmapDropEnabled(bool enabled)
    {
        if(imageBitmapDropEnabled==enabled)return;imageBitmapDropEnabled=enabled;
        if(!enabled&&ReferenceEquals(fileImageTask,droppedBitmapTask))CancelFileAcquisition();
        RevokeTextDrop();RefreshTextDrop();
    }
    // Unconditional ownership. The callback never waits for the receiver loop which is invoking it.
    private void StartDroppedBitmap(TextDropPeer peer,OwnedOleBitmap bitmap)
    {
        BitmapImageOperation? operation=null;
        try
        {
            var state=lifecycle.Gate.Snapshot;
            if(!imageBitmapDropEnabled||exiting||state.Mode!=TriggerMode.Drop||state.Suspension!=Suspension.None||FileImageBusy||dropCapture?.IsLeaseCurrent(peer.Lease)!=true)return;
            if(TryStartBitmapPreparation(()=>{operation=new(bitmap,peer.Settled.Task,CreateBitmapWorker);bitmap=null!;return operation;},state.Epoch,state.Activation))RevokeTextDrop();
        }
        finally{bitmap?.Dispose();}
    }
    private ImageWorkerClient CreateBitmapWorker()=>new(Path.Combine(AppContext.BaseDirectory,"KuaiYi.ImageWorker.exe"));
}
