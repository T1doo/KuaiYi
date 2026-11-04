using System.Windows;
using System.Windows.Controls;
namespace KuaiYi.App;

internal sealed partial class MarkdownSourceRegion
{
    private readonly TextBlock sourceMetadata=new(){TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis,FontSize=12,Margin=new Thickness(0,6,0,2)};
    private readonly TextBlock sourceOrigin=new(){TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new Thickness(0,0,0,6)};
    partial void UpdateNativeDetails(string? details)
    {
        int split=details?.IndexOf('\n')??-1;sourceMetadata.Text=split<0?details??"":details![..split];sourceOrigin.Text=split<0?"":details![(split+1)..];
        sourceMetadata.Visibility=details==null?Visibility.Collapsed:Visibility.Visible;sourceOrigin.Visibility=split<0?Visibility.Collapsed:Visibility.Visible;
        sourceMetadata.ToolTip=details;sourceOrigin.ToolTip=sourceOrigin.Text;
    }
    partial void SetupNativeCopy()
    {
        // The base source view initially owns Text. Detach it before composing
        // the disclosure; WPF rejects a child with two logical parents.
        View.Content=null;
        var content=new StackPanel();content.Children.Add(sourceMetadata);content.Children.Add(sourceOrigin);content.Children.Add(Text);View.Content=content;
        DesktopTextScale.Font(sourceMetadata,12);sourceMetadata.SetBinding(FrameworkElement.MaxHeightProperty,DesktopTextScale.Metric(48));
        DesktopTextScale.Font(sourceOrigin,12);sourceOrigin.SetBinding(FrameworkElement.MaxHeightProperty,DesktopTextScale.Metric(48));DesktopTheme.Bind(sourceOrigin,TextBlock.ForegroundProperty,DesktopTheme.Muted);
        DesktopTheme.Bind(sourceMetadata,TextBlock.ForegroundProperty,DesktopTheme.Muted);
        var menu=new ContextMenu();var copy=new MenuItem{Header="复制完整原文"};
        var header=new DockPanel();var command=new Button{Content="复制原文",MinHeight=28,Padding=new Thickness(6,2,6,2),Margin=new Thickness(8,0,0,0)};command.SetResourceReference(FrameworkElement.StyleProperty,DesktopTheme.ToolButton);DesktopTextScale.Font(command,12);DockPanel.SetDock(command,Dock.Right);header.Children.Add(command);var label=new TextBlock{Text="查看原文",VerticalAlignment=VerticalAlignment.Center};DesktopTextScale.Font(label,12);header.Children.Add(label);View.Header=header;
        System.Windows.Automation.AutomationProperties.SetName(copy,"复制所示版本的完整原文");
        void Copy()
        {
            try{View.ToolTip=CopyOriginal(Clipboard.SetText)?"原文已复制":"原文复制未确认，内容或阅读资格已改变。";}
            catch(Exception error)when(error is System.Runtime.InteropServices.ExternalException or System.Threading.ThreadStateException)
            {View.ToolTip="原文复制未确认；可手动重试。";}
        }
        copy.Click+=(_,_)=>Copy();command.Click+=(_,_)=>Copy();
        menu.Items.Add(copy);menu.Opened+=(_,_)=>{if(Window.GetWindow(View) is {} owner)DesktopTheme.ApplyMenu(menu,owner);};Text.ContextMenu=menu;
    }
}
