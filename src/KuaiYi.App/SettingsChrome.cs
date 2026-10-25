using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using KuaiYi.Core.Settings;
namespace KuaiYi.App;
internal sealed partial class SettingsWindow
{
    private static readonly string[] PageTitles=["常规","API 与模型","翻译风格","交互与浮窗","快捷键","图片与集成","隐私与诊断"];
    private static readonly string[] PageDescriptions=["安心开始，把翻译调整为适合你的方式。","连接你选择的服务，始终清楚内容发往哪里。","同一份原文，用适合阅读场景的表达。","让浮窗轻巧、清晰，按你的节奏出现。","为常用操作设置顺手的组合。","从一张图片开始，逐次确认处理与发送。","内容留在当前会话，授权由你决定。"];
    private TextBlock? pageHeading,pageDescription,pageIndex;
    private void BuildSettingsChrome(StackPanel[] pages,out ListBox navigation)
    {
        DesktopTheme.Install(this,()=>theme.SelectedItem is ThemePreference selected?selected:draft.Data.Overlay.Theme,forms:true);
        var rail=new Border{BorderThickness=new Thickness(0,0,1,0),Padding=new Thickness(16,24,16,20)};DesktopTheme.Bind(rail,Border.BackgroundProperty,DesktopTheme.Rail);DesktopTheme.Bind(rail,Border.BorderBrushProperty,DesktopTheme.Stroke);
        var sidebar=new Grid();sidebar.RowDefinitions.Add(new(){Height=GridLength.Auto});sidebar.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});sidebar.RowDefinitions.Add(new(){Height=GridLength.Auto});rail.Child=sidebar;
        var brand=new StackPanel{Margin=new Thickness(4,0,0,24)};Grid.SetRow(brand,0);sidebar.Children.Add(brand);
        var brandRow=new StackPanel{Orientation=Orientation.Horizontal};var mark=new Border{Width=32,Height=32,CornerRadius=new CornerRadius(10),Margin=new Thickness(0,0,10,0)};DesktopTheme.Bind(mark,Border.BackgroundProperty,DesktopTheme.Accent);var letter=new TextBlock{Text="译",FontSize=18,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};DesktopTheme.Bind(letter,TextBlock.ForegroundProperty,DesktopTheme.AccentText);mark.Child=letter;brandRow.Children.Add(mark);brandRow.Children.Add(new TextBlock{Text="快译",FontSize=22,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});brand.Children.Add(brandRow);
        var brandNote=new TextBlock{Text="让理解自然发生",FontSize=11,Margin=new Thickness(0,10,0,0)};DesktopTheme.Bind(brandNote,TextBlock.ForegroundProperty,DesktopTheme.Muted);brand.Children.Add(brandNote);
        var quietCorner=new StackPanel{Margin=new Thickness(4,12,4,0)};Grid.SetRow(quietCorner,2);sidebar.Children.Add(quietCorner);
        var privacy=new TextBlock{Text="默认 Off（未启用）\n启用后可自动发送选区\n保存设置本身不发送内容",FontSize=11,TextWrapping=TextWrapping.Wrap,LineHeight=18,Margin=new Thickness(0,8,0,0)};DesktopTheme.Bind(privacy,TextBlock.ForegroundProperty,DesktopTheme.Muted);quietCorner.Children.Add(privacy);
        navigation=new SettingsNavigationListBox{ItemsSource=PageTitles,BorderThickness=new Thickness(0),Background=System.Windows.Media.Brushes.Transparent,Padding=new Thickness(0),ItemContainerStyle=(Style)Resources[DesktopTheme.NavigationItem],ItemTemplate=DesktopTheme.NavigationTemplate()};AutomationProperties.SetName(navigation,"设置分类");Grid.SetRow(navigation,1);sidebar.Children.Add(navigation);
        var pageContent=new StackPanel();
        var header=new StackPanel();var headingRegion=new Border{Child=header,Padding=new Thickness(0,0,0,22),Margin=new Thickness(0,0,0,22),BorderThickness=new Thickness(0,0,0,1)};DesktopTheme.Bind(headingRegion,Border.BorderBrushProperty,DesktopTheme.Stroke);pageContent.Children.Add(headingRegion);
        pageIndex=new TextBlock{Text="偏好设置  /  01",FontSize=11,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)};DesktopTheme.Bind(pageIndex,TextBlock.ForegroundProperty,DesktopTheme.Accent);header.Children.Add(pageIndex);
        pageHeading=new TextBlock{Text=PageTitles[0],FontSize=28,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};header.Children.Add(pageHeading);pageDescription=new TextBlock{Text=PageDescriptions[0],FontSize=13,TextWrapping=TextWrapping.Wrap,LineHeight=21,Margin=new Thickness(0,8,0,0)};DesktopTheme.Bind(pageDescription,TextBlock.ForegroundProperty,DesktopTheme.Muted);header.Children.Add(pageDescription);
        body.Margin=new Thickness(0);body.VerticalAlignment=VerticalAlignment.Top;
        foreach(var page in pages)
        {
            page.Visibility=Visibility.Collapsed;body.Children.Add(page);
        }
        pageContent.Children.Add(body);
        var main=new ScrollViewer{Content=pageContent,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};DesktopTheme.Bind(main,Control.BackgroundProperty,DesktopTheme.Surface);
        var footer=new Border{BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(16,10,16,14)};DesktopTheme.Bind(footer,Border.BackgroundProperty,DesktopTheme.Surface);DesktopTheme.Bind(footer,Border.BorderBrushProperty,DesktopTheme.Stroke);
        var bottom=new StackPanel();footer.Child=bottom;
        var feedback=new Border{CornerRadius=new CornerRadius(8),Padding=new Thickness(12,9,12,9),Margin=new Thickness(0,0,0,10),Child=status,BorderThickness=new Thickness(1)};DesktopTheme.Bind(feedback,Border.BackgroundProperty,DesktopTheme.Canvas);DesktopTheme.Bind(feedback,Border.BorderBrushProperty,DesktopTheme.Stroke);status.Margin=new Thickness(0);status.FontSize=12;status.LineHeight=19;DesktopTheme.Bind(status,TextBlock.ForegroundProperty,DesktopTheme.Text);AutomationProperties.SetName(status,"设置状态");AutomationProperties.SetLiveSetting(status,AutomationLiveSetting.Polite);bottom.Children.Add(feedback);
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};bottom.Children.Add(actions);SetupDiagnosticEntry(actions);
        actions.Children.Add(recovery);actions.Children.Add(cancel);actions.Children.Add(save);save.Content="保存更改";save.Style=(Style)Resources[DesktopTheme.PrimaryButton];AutomationProperties.SetName(save,"保存设置更改");AutomationProperties.SetName(cancel,"取消设置更改");
        var footerScroll=new ScrollViewer{Content=footer,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        var root=new SettingsChromePanel(rail,main,footerScroll);
        root.SetBinding(SettingsChromePanel.UiFontSizeProperty,new Binding(nameof(FontSize)){Source=this});
        root.SetResourceReference(SettingsChromePanel.ScrollBarWidthProperty,SystemParameters.VerticalScrollBarWidthKey);
        var minimum=new MultiBinding{Converter=new SettingsMinimumWidth(),Mode=BindingMode.OneWay};minimum.Bindings.Add(new Binding(nameof(FontSize)){Source=this});minimum.Bindings.Add(new Binding(nameof(SettingsChromePanel.ScrollBarWidth)){Source=root});root.SetBinding(MinWidthProperty,minimum);
        // The outer viewport scrolls only when even the compact form cannot fit its real minima.
        // Vertical space stays finite; each of the three regions owns its own scrolling.
        var viewport=new ScrollViewer{Content=root,CanContentScroll=false,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};
        root.SetBinding(WidthProperty,new Binding(nameof(ScrollViewer.ViewportWidth)){Source=viewport,Mode=BindingMode.OneWay});
        Content=viewport;
        CompactStyle(brand,root,VisibilityProperty,Visibility.Visible,Visibility.Collapsed);
        CompactStyle(quietCorner,root,VisibilityProperty,Visibility.Visible,Visibility.Collapsed);
        CompactStyle(pageContent,root,MarginProperty,new Thickness(32,30,32,24),new Thickness(16,20,16,20));
        CompactStyle(rail,root,Border.PaddingProperty,new Thickness(16,24,16,20),new Thickness(8,4,8,4));
        // Style setters must own these properties, rather than being shadowed by local values.
        rail.ClearValue(Border.PaddingProperty);
        var items=new FrameworkElementFactory(typeof(StackPanel));
        items.SetBinding(StackPanel.OrientationProperty,new Binding(nameof(SettingsChromePanel.IsCompact)){Source=root,Converter=new SettingsNavigationOrientation()});
        navigation.ItemsPanel=new ItemsPanelTemplate(items);
        ScrollViewer.SetCanContentScroll(navigation,false);ScrollViewer.SetVerticalScrollBarVisibility(navigation,ScrollBarVisibility.Auto);
        CompactStyle(navigation,root,ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Disabled,ScrollBarVisibility.Auto);
        theme.SelectionChanged+=(_,_)=>DesktopTheme.Apply(this,theme.SelectedItem is ThemePreference selected?selected:draft.Data.Overlay.Theme);
    }
    private static void CompactStyle(FrameworkElement element,SettingsChromePanel root,DependencyProperty property,object wide,object compact)
    {
        var style=new Style(element.GetType());style.Setters.Add(new Setter(property,wide));
        var trigger=new DataTrigger{Binding=new Binding(nameof(SettingsChromePanel.IsCompact)){Source=root},Value=true};trigger.Setters.Add(new Setter(property,compact));style.Triggers.Add(trigger);element.Style=style;
    }
    private void ShowSettingsPage(int index)
    {if(index<0||index>=PageTitles.Length)return;pageIndex!.Text=$"偏好设置  /  {index+1:00}";pageHeading!.Text=PageTitles[index];pageDescription!.Text=PageDescriptions[index];}
}
