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
    private TextBlock? pageHeading,pageDescription;
    private void BuildSettingsChrome(StackPanel[] pages,out ListBox navigation)
    {
        DesktopTheme.Install(this,()=>theme.SelectedItem is ThemePreference selected?selected:draft.Data.Overlay.Theme,forms:true);
        var root=new Grid();root.ColumnDefinitions.Add(new(){Width=new GridLength(SettingsUiMetrics.RailWidth)});root.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var rail=new Border{BorderThickness=new Thickness(0,0,1,0),Padding=new Thickness(16,24,16,20)};DesktopTheme.Bind(rail,Border.BackgroundProperty,DesktopTheme.Rail);DesktopTheme.Bind(rail,Border.BorderBrushProperty,DesktopTheme.Stroke);root.Children.Add(rail);
        var sidebar=new DockPanel();rail.Child=sidebar;
        var brand=new StackPanel{Margin=new Thickness(4,0,0,28)};DockPanel.SetDock(brand,Dock.Top);sidebar.Children.Add(brand);
        var brandRow=new StackPanel{Orientation=Orientation.Horizontal};var mark=new Border{CornerRadius=new CornerRadius(10),Width=36,Height=36,Margin=new Thickness(0,0,10,0)};DesktopTheme.Bind(mark,Border.BackgroundProperty,DesktopTheme.Accent);var letter=new TextBlock{Text="译",FontSize=20,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};DesktopTheme.Bind(letter,TextBlock.ForegroundProperty,DesktopTheme.AccentText);mark.Child=letter;brandRow.Children.Add(mark);brandRow.Children.Add(new TextBlock{Text="快译",FontSize=21,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});brand.Children.Add(brandRow);
        var brandNote=new TextBlock{Text="让理解自然发生",FontSize=11,Margin=new Thickness(0,12,0,0)};DesktopTheme.Bind(brandNote,TextBlock.ForegroundProperty,DesktopTheme.Muted);brand.Children.Add(brandNote);
        var privacy=new TextBlock{Text="默认 Off（未启用）\n启用后可自动发送选区\n保存设置本身不发送内容",FontSize=11,TextWrapping=TextWrapping.Wrap,LineHeight=18,Margin=new Thickness(4,20,4,0)};DesktopTheme.Bind(privacy,TextBlock.ForegroundProperty,DesktopTheme.Muted);DockPanel.SetDock(privacy,Dock.Bottom);sidebar.Children.Add(privacy);
        navigation=new ListBox{ItemsSource=PageTitles,BorderThickness=new Thickness(0),Background=System.Windows.Media.Brushes.Transparent,Padding=new Thickness(0),ItemContainerStyle=(Style)Resources[DesktopTheme.NavigationItem]};AutomationProperties.SetName(navigation,"设置分类");ScrollViewer.SetHorizontalScrollBarVisibility(navigation,ScrollBarVisibility.Disabled);sidebar.Children.Add(navigation);
        var main=new DockPanel();Grid.SetColumn(main,1);root.Children.Add(main);
        var header=new StackPanel{Margin=new Thickness(SettingsUiMetrics.PageInset,24,SettingsUiMetrics.PageInset,22)};DockPanel.SetDock(header,Dock.Top);main.Children.Add(header);
        var badge=new TextBlock{Text="偏好设置  /  PREFERENCES",FontSize=10,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,10)};DesktopTheme.Bind(badge,TextBlock.ForegroundProperty,DesktopTheme.Muted);header.Children.Add(badge);
        pageHeading=new TextBlock{Text=PageTitles[0],FontSize=25,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};header.Children.Add(pageHeading);pageDescription=new TextBlock{Text=PageDescriptions[0],FontSize=13,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};DesktopTheme.Bind(pageDescription,TextBlock.ForegroundProperty,DesktopTheme.Muted);header.Children.Add(pageDescription);
        body.Margin=new Thickness(SettingsUiMetrics.PageInset,0,SettingsUiMetrics.PageInset,SettingsUiMetrics.PageInset);body.VerticalAlignment=VerticalAlignment.Top;
        foreach(var page in pages)
        {
            page.Visibility=Visibility.Collapsed;var card=new Border{CornerRadius=new CornerRadius(SettingsUiMetrics.CardRadius),BorderThickness=new Thickness(1),Child=page};DesktopTheme.Bind(card,Border.BackgroundProperty,DesktopTheme.Surface);DesktopTheme.Bind(card,Border.BorderBrushProperty,DesktopTheme.Stroke);card.SetBinding(UIElement.VisibilityProperty,new Binding(nameof(Visibility)){Source=page,Mode=BindingMode.OneWay});body.Children.Add(card);
        }
        main.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        var footer=new Border{BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(20,10,20,14)};DesktopTheme.Bind(footer,Border.BackgroundProperty,DesktopTheme.Surface);DesktopTheme.Bind(footer,Border.BorderBrushProperty,DesktopTheme.Stroke);Grid.SetRow(footer,1);Grid.SetColumnSpan(footer,2);root.Children.Add(footer);
        var bottom=new StackPanel();footer.Child=bottom;status.Margin=new Thickness(2,0,2,6);status.FontSize=12;DesktopTheme.Bind(status,TextBlock.ForegroundProperty,DesktopTheme.Muted);AutomationProperties.SetName(status,"设置状态");bottom.Children.Add(status);
        var actions=new DockPanel();bottom.Children.Add(actions);var diagnostics=new StackPanel{Orientation=Orientation.Horizontal};SetupDiagnosticEntry(diagnostics);actions.Children.Add(diagnostics);
        var decisions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(decisions,Dock.Right);actions.Children.Insert(0,decisions);decisions.Children.Add(recovery);decisions.Children.Add(cancel);decisions.Children.Add(save);save.Content="保存更改";save.Style=(Style)Resources[DesktopTheme.PrimaryButton];AutomationProperties.SetName(save,"保存设置更改");AutomationProperties.SetName(cancel,"取消设置更改");Content=root;
        theme.SelectionChanged+=(_,_)=>DesktopTheme.Apply(this,theme.SelectedItem is ThemePreference selected?selected:draft.Data.Overlay.Theme);
    }
    private void ShowSettingsPage(int index)
    {if(index<0||index>=PageTitles.Length)return;pageHeading!.Text=PageTitles[index];pageDescription!.Text=PageDescriptions[index];}
}
