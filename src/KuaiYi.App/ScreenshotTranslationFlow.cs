using System.Windows;
using System.Windows.Controls;
using KuaiYi.Providers;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private void TranslateScreenshot(ImageOutputMode? mode=null)
    {
        try
        {
            bool sent=SubmitScreenshot(mode??(lifecycle.Settings?.Data.Images.SmartRouting==true?ImageOutputMode.Automatic:ImageOutputMode.TranslateText),offer=>MessageBox.Show(this,offer.ConfirmationText,"快译 · 本次图片发送确认",MessageBoxButton.OKCancel,MessageBoxImage.Information)==MessageBoxResult.OK);
            if(!sent)button.ToolTip=ScreenshotFailureLabel(imageTranslation?.LastRejection??"SOURCE_OR_SETTINGS_CHANGED");
        }
        catch(Exception error){Report("图片翻译未完成",error);}
    }
    private void AddScreenshotModes(ContextMenu menu)
    {
        var item=new MenuItem{Tag="image-modes",Header=SelectedSource.Image?.Kind==KuaiYi.Imaging.ImageSourceKind.Browser?"本次浏览器图片翻译":SelectedSource.Image?.Kind==KuaiYi.Imaging.ImageSourceKind.LocalFile?"本次文件图片翻译":"本次截图翻译",IsEnabled=CanTranslateScreenshot};
        foreach(var mode in Enum.GetValues<ImageOutputMode>())
        {
            var current=mode;var child=new MenuItem{Header=mode switch{ImageOutputMode.Automatic=>"智能判断后翻译",ImageOutputMode.TranslateText=>"仅翻译图中文字",_=>"按图文解释"}};
            child.Click+=(_,_)=>TranslateScreenshot(current);item.Items.Add(child);
        }
        item.ToolTip="每次发送前确认端点、模型和两阶段调用；当前图片复用与重新生成保持关闭。";menu.Items.Add(item);
    }
}
