using System.Windows.Controls;
using KuaiYi.Core;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private void AddImageFileDropMenu(ContextMenu menu)
    {
        AddBitmapDropMenu(menu);
        var item=new MenuItem{Header="接收单张图片文件投放",IsCheckable=true,IsChecked=imageFileDropEnabled,ToolTip="仅本次运行；Drop 模式开启时接收单个本地图片文件。仅本地准备，发送仍须确认；多文件、虚拟文件和链接暂不支持；位图使用单独入口。"};
        item.Click+=(_,_)=>SetImageFileDropEnabled(item.IsChecked);menu.Items.Add(item);menu.Opened+=(_,_)=>{item.IsChecked=imageFileDropEnabled;item.IsEnabled=!exiting&&(imageFileDropEnabled||!FileImageBusy);};
    }
}
