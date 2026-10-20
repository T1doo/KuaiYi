using System.IO;
using System.Windows;
using System.Windows.Controls;
using KuaiYi.Core;
using KuaiYi.Imaging;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private async Task OpenImageFile()
    {
        if(exiting||lifecycle.Gate.Snapshot.Suspension!=Suspension.None)return;
        if(FileImageBusy){button.ToolTip="原图片任务或 Worker 清理未确认；请清除当前内容后重试。";return;}
        if(!IsVisible)ShowOverlay();if(!IsVisible)return;
        var dialog=new Microsoft.Win32.OpenFileDialog{Filter="图片|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif",Multiselect=false};if(dialog.ShowDialog(this)!=true)return;
        await StartOpenImage(dialog.FileName,()=>new FileImageOperation(()=>new ImageWorkerClient(Path.Combine(AppContext.BaseDirectory,"KuaiYi.ImageWorker.exe"))));
    }
    private void EnableAndTranslateOpenedImage()=>EnableAndSendOpenedImage(
        ()=>MessageBox.Show(this,"启用后翻译当前打开的图片；随后仍会显示本次图片的目标、模型和发送确认。","快译 · 启用并翻译图片",MessageBoxButton.OKCancel,MessageBoxImage.Information)==MessageBoxResult.OK,
        Toggle,()=>TranslateScreenshot()); // Original lifecycle/profile/origin enable; original vision offer confirmation.
    private void AddOpenImageCommands(ContextMenu menu)
    {
        var open=new MenuItem{Header="打开图片",IsEnabled=!FileImageBusy};open.Click+=(_,_)=>_=OpenImageFile();menu.Items.Add(open);
        if(SelectedSource.Image is OwnedFileImage&&lifecycle.Gate.Snapshot.Activation==Activation.Off)
        {var send=new MenuItem{Header="启用并翻译图片"};send.Click+=(_,_)=>EnableAndTranslateOpenedImage();menu.Items.Add(send);}
    }
}
