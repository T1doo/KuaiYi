using System.Windows.Controls;
namespace KuaiYi.App;
internal sealed partial class ImageProbeWindow
{
    private OlePresentationWindow? oleWindow;
    private void SetupOlePresentation(StackPanel panel)
    {
        var open=new Button{Content="打开真实浮钮 · P0 OLE 隔离接线（本地）"};panel.Children.Add(open);
        open.Click+=(_,_)=>
        {
            if(oleWindow!=null)return;
            if(gate.Snapshot.Activation!=KuaiYi.Core.Activation.Off||enabled.IsChecked==true||gifPreview!=null||running!=null||ownedWorker!=null||ownedCaptureHost!=null||captureTask is {IsCompleted:false}||browserInbox.CurrentIdentity!=null)
            {status.Text="请先关闭本地采集并确认旧工作流退出；P0浮钮实验单独运行。";return;}
            enabled.IsEnabled=choose.IsEnabled=screenshot.IsEnabled=false;
            oleWindow=new(gate,(identity,text)=>{status.Text="收到明确 Drop 的本地文字；未发送模型。";result.Text=text;},message=>status.Text=message);
            oleWindow.Closed+=(_,_)=>{oleWindow=null;enabled.IsEnabled=true;choose.IsEnabled=screenshot.IsEnabled=running==null&&ownedWorker==null&&ownedCaptureHost==null&&gifPreview==null;};oleWindow.Show();
        };
    }
}
