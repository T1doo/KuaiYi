using System.Windows;
using System.Windows.Controls;
namespace KuaiYi.App;

internal sealed partial class MarkdownSourceRegion
{
    private readonly TextBlock sourceMetadata=new(){TextWrapping=TextWrapping.Wrap,FontSize=12,MaxHeight=48,Margin=new Thickness(0,6,0,6)};
    partial void UpdateNativeDetails(string? details)
    {sourceMetadata.Text=details??"";sourceMetadata.Visibility=details==null?Visibility.Collapsed:Visibility.Visible;sourceMetadata.ToolTip=details;}
    partial void SetupNativeCopy()
    {
        var content=new StackPanel();content.Children.Add(sourceMetadata);content.Children.Add(Text);View.Content=content;
        DesktopTheme.Bind(sourceMetadata,TextBlock.ForegroundProperty,DesktopTheme.Muted);
        var menu=new ContextMenu();var copy=new MenuItem{Header="复制完整原文"};
        var header=new DockPanel();var command=new Button{Content="复制原文",MinHeight=28,Padding=new Thickness(6,2,6,2),Margin=new Thickness(8,0,0,0)};command.SetResourceReference(FrameworkElement.StyleProperty,DesktopTheme.ToolButton);DockPanel.SetDock(command,Dock.Right);header.Children.Add(command);header.Children.Add(new TextBlock{Text="查看原文",VerticalAlignment=VerticalAlignment.Center});View.Header=header;
        System.Windows.Automation.AutomationProperties.SetName(copy,"复制所示版本的完整原文");
        void Copy()
        {
            try{View.ToolTip=CopyOriginal(Clipboard.SetText)?"原文已复制":"原文复制未确认，内容或阅读资格已改变。";}
            catch(Exception error)when(error is System.Runtime.InteropServices.ExternalException or System.Threading.ThreadStateException)
            {View.ToolTip="原文复制未确认；可手动重试。";}
        }
        copy.Click+=(_,_)=>Copy();command.Click+=(_,_)=>Copy();
        menu.Items.Add(copy);Text.ContextMenu=menu;
    }
}
