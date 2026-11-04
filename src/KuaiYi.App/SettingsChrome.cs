using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using KuaiYi.Core.Settings;
namespace KuaiYi.App;
internal sealed partial class SettingsWindow
{
    private static readonly string[] PageTitles=SettingsSearchCatalog.Titles();
    private static readonly string[] PageDescriptions=["安心开始，把翻译调整为适合你的方式。","连接你选择的服务，始终清楚内容发往哪里。","同一份原文，用适合阅读场景的表达。","让浮窗轻巧、清晰，按你的节奏出现。","为常用操作设置顺手的组合。","从一张图片开始，逐次确认处理与发送。","内容留在当前会话，授权由你决定。"];
    private TextBlock? pageHeading,pageDescription,pageIndex;
    private void BuildSettingsChrome(StackPanel[] pages,out ListBox navigation)
    {
        DesktopTheme.Install(this,()=>theme.SelectedItem is ThemePreference selected?selected:draft.Data.Overlay.Theme,forms:true,motionPreference:()=>motion.SelectedItem is MotionPreference selected?selected:draft.Data.Overlay.ReduceMotion);
        var rail=new Border{BorderThickness=new Thickness(0,0,1,0),Padding=new Thickness(16,24,16,20)};DesktopTheme.Bind(rail,Border.BackgroundProperty,DesktopTheme.Rail);DesktopTheme.Bind(rail,Border.BorderBrushProperty,DesktopTheme.Stroke);
        var sidebar=new Grid();sidebar.RowDefinitions.Add(new(){Height=GridLength.Auto});sidebar.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});sidebar.RowDefinitions.Add(new(){Height=GridLength.Auto});rail.Child=sidebar;
        var brand=new StackPanel{Margin=new Thickness(4,0,0,16)};Grid.SetRow(brand,0);sidebar.Children.Add(brand);
        brand.Children.Add(new TextBlock{Text="快译设置",FontSize=16,FontWeight=FontWeights.SemiBold});
        var quietCorner=new StackPanel{Margin=new Thickness(4,12,4,0)};Grid.SetRow(quietCorner,2);sidebar.Children.Add(quietCorner);
        var privacy=new TextBlock{Text="默认 Off（未启用）\n启用后可自动发送选区\n保存设置本身不发送内容",FontSize=11,TextWrapping=TextWrapping.Wrap,LineHeight=18,Margin=new Thickness(0,8,0,0)};DesktopTheme.Bind(privacy,TextBlock.ForegroundProperty,DesktopTheme.Muted);quietCorner.Children.Add(privacy);
        navigation=new SettingsNavigationListBox{ItemsSource=PageTitles};
        var simpleNavigation=new ScrollViewer{Content=SimpleNavigation(navigation),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        Grid.SetRow(simpleNavigation,1);sidebar.Children.Add(simpleNavigation);
        var pageContent=new StackPanel();
        var header=new StackPanel();var headingRegion=new Border{Child=header,Padding=new Thickness(0,0,0,16),Margin=new Thickness(0,0,0,16),BorderThickness=new Thickness(0,0,0,1)};DesktopTheme.Bind(headingRegion,Border.BorderBrushProperty,DesktopTheme.Stroke);pageContent.Children.Add(headingRegion);
        pageIndex=new TextBlock{Visibility=Visibility.Collapsed,Text="偏好设置  /  01",FontSize=11,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)};DesktopTheme.Bind(pageIndex,TextBlock.ForegroundProperty,DesktopTheme.Accent);header.Children.Add(pageIndex);
        pageHeading=new TextBlock{Text=PageTitles[0],FontSize=22,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};header.Children.Add(pageHeading);pageDescription=new TextBlock{Text=PageDescriptions[0],FontSize=13,TextWrapping=TextWrapping.Wrap,LineHeight=21,Margin=new Thickness(0,8,0,0)};DesktopTheme.Bind(pageDescription,TextBlock.ForegroundProperty,DesktopTheme.Muted);header.Children.Add(pageDescription);
        SetupSettingsSearch(header,navigation);
        body.Margin=new Thickness(0);body.VerticalAlignment=VerticalAlignment.Top;
        foreach(var page in pages)
        {
            page.Visibility=Visibility.Collapsed;body.Children.Add(page);
        }
        pageContent.Children.Add(body);
        var main=new ScrollViewer{Content=pageContent,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};DesktopTheme.Bind(main,Control.BackgroundProperty,DesktopTheme.Surface);
        var footer=new Border{BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(24,10,24,12)};DesktopTheme.Bind(footer,Border.BackgroundProperty,DesktopTheme.Surface);DesktopTheme.Bind(footer,Border.BorderBrushProperty,DesktopTheme.Stroke);
        var bottom=new DockPanel{LastChildFill=true};footer.Child=bottom;
        var feedback=new Border{Padding=new Thickness(0,0,0,8),Margin=new Thickness(0,0,0,10),Child=status,BorderThickness=new Thickness(0)};DesktopTheme.Bind(feedback,Border.BackgroundProperty,DesktopTheme.Canvas);DesktopTheme.Bind(feedback,Border.BorderBrushProperty,DesktopTheme.Stroke);status.Margin=new Thickness(0);status.FontSize=12;status.LineHeight=19;DesktopTheme.Bind(status,TextBlock.ForegroundProperty,DesktopTheme.Text);AutomationProperties.SetName(status,"设置状态");AutomationProperties.SetLiveSetting(status,AutomationLiveSetting.Polite);
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(actions,Dock.Bottom);bottom.Children.Add(actions);SetupDiagnosticEntry(pages[6]);pages[0].Children.Insert(0,recovery);
        actions.Children.Add(cancel);actions.Children.Add(save);save.Content="保存更改";save.Style=(Style)Resources[DesktopTheme.PrimaryButton];AutomationProperties.SetName(save,"保存设置更改");AutomationProperties.SetName(cancel,"取消设置更改");
        var feedbackScroll=new ScrollViewer{Content=feedback,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};bottom.Children.Add(feedbackScroll);
        var root=new SettingsChromePanel(rail,main,footer,width=>{actions.Measure(new Size(Math.Max(0,width-48),double.PositiveInfinity));return actions.DesiredSize.Height+22;});
        root.SetBinding(SettingsChromePanel.UiFontSizeProperty,new Binding(nameof(FontSize)){Source=this});
        root.SetResourceReference(SettingsChromePanel.ScrollBarWidthProperty,SystemParameters.VerticalScrollBarWidthKey);
        // The outer viewport scrolls only when even the compact form cannot fit its real minima.
        // Vertical space stays finite; each of the three regions owns its own scrolling.
        var viewport=new ScrollViewer{Content=root,CanContentScroll=false,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};
        root.SetBinding(WidthProperty,new Binding(nameof(ScrollViewer.ViewportWidth)){Source=viewport,Mode=BindingMode.OneWay});
        Content=viewport;
        CompactStyle(brand,root,VisibilityProperty,Visibility.Visible,Visibility.Collapsed);
        CompactStyle(quietCorner,root,VisibilityProperty,Visibility.Visible,Visibility.Collapsed);
        CompactStyle(pageContent,root,MarginProperty,new Thickness(24),new Thickness(16,20,16,20));
        CompactStyle(rail,root,Border.PaddingProperty,new Thickness(16,24,16,20),new Thickness(8,4,8,4));
        // Style setters must own these properties, rather than being shadowed by local values.
        rail.ClearValue(Border.PaddingProperty);
        var items=new FrameworkElementFactory(typeof(StackPanel));
        items.SetBinding(StackPanel.OrientationProperty,new Binding(nameof(SettingsChromePanel.IsCompact)){Source=root,Converter=new SettingsNavigationOrientation()});
        navigation.ItemsPanel=new ItemsPanelTemplate(items);
        ScrollViewer.SetCanContentScroll(navigation,false);ScrollViewer.SetVerticalScrollBarVisibility(navigation,ScrollBarVisibility.Auto);
        CompactStyle(navigation,root,ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Disabled,ScrollBarVisibility.Auto);
        theme.SelectionChanged+=(_,_)=>DesktopTheme.Apply(this,theme.SelectedItem is ThemePreference selected?selected:draft.Data.Overlay.Theme);
        motion.SelectionChanged+=(_,_)=>DesktopControlFeedback.Apply(this,motion.SelectedItem is MotionPreference selected?selected:draft.Data.Overlay.ReduceMotion);
    }
    private static void CompactStyle(FrameworkElement element,SettingsChromePanel root,DependencyProperty property,object wide,object compact)
    {
        var style=new Style(element.GetType());style.Setters.Add(new Setter(property,wide));
        var trigger=new DataTrigger{Binding=new Binding(nameof(SettingsChromePanel.IsCompact)){Source=root},Value=true};trigger.Setters.Add(new Setter(property,compact));style.Triggers.Add(trigger);element.Style=style;
    }
    private void ShowSettingsPage(int index)
    {if(index<0||index>=PageTitles.Length)return;pageIndex!.Text=$"偏好设置  /  {index+1:00}";pageHeading!.Focusable=true;pageHeading.Text=index switch{0=>"备份与恢复",1=>"翻译与模型",3=>"外观",4=>"快捷操作",_=>PageTitles[index]};pageDescription!.Text=PageDescriptions[index];}
}
