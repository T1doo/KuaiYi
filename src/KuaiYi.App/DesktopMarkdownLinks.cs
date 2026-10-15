using System.Diagnostics;
using System.Windows;
using KuaiYi.Core;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private MarkdownLinkActions CreateMarkdownLinkActions()=>new(
        target=>MessageBox.Show(this,"将使用系统浏览器打开外部链接。\n目标域名："+target.Domain+"\n地址："+target.Url,
            "快译 · 打开外部链接",MessageBoxButton.OKCancel,MessageBoxImage.Information,MessageBoxResult.Cancel)==MessageBoxResult.OK,
        target=>
        {
            if(!MarkdownHttpTarget.TryParse(target.Url,out var verified)||verified!=target)throw new InvalidOperationException("Link target rejected.");
            using var started=Process.Start(new ProcessStartInfo(target.Url){UseShellExecute=true});
        },error=>Report("外部链接未能打开",error));
}
