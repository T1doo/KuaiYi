using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
namespace KuaiYi.App;

internal sealed partial class SettingsWindow
{
    private static void Section(Panel panel,string title,string? description=null)
    {
        var section=new StackPanel{Margin=new Thickness(0,panel.Children.Count==0?0:24,0,2)};
        if(panel.Children.Count>0){var rule=new Border{Height=1,Margin=new Thickness(0,0,0,20)};DesktopTheme.Bind(rule,Border.BackgroundProperty,DesktopTheme.Stroke);section.Children.Add(rule);}
        section.Children.Add(new TextBlock{Text=title,FontSize=16,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
        if(description!=null){var caption=new TextBlock{Text=description,FontSize=12,TextWrapping=TextWrapping.Wrap,LineHeight=20,Margin=new Thickness(0,6,0,0)};DesktopTheme.Bind(caption,TextBlock.ForegroundProperty,DesktopTheme.Muted);section.Children.Add(caption);}
        panel.Children.Add(section);
    }
    private static void PreferenceRow(Panel panel,string title,string description,FrameworkElement input)
    {
        var row=new Grid{Margin=new Thickness(0,15,0,0)};row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var labels=new StackPanel{Margin=new Thickness(0,0,20,0),VerticalAlignment=VerticalAlignment.Center};var label=new TextBlock{Text=title,FontSize=13,FontWeight=FontWeights.Medium,TextWrapping=TextWrapping.Wrap};labels.Children.Add(label);
        var detail=new TextBlock{Text=description,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,0)};DesktopTheme.Bind(detail,TextBlock.ForegroundProperty,DesktopTheme.Muted);labels.Children.Add(detail);row.Children.Add(labels);
        input.Width=180;input.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(input,1);AutomationProperties.SetLabeledBy(input,label);AutomationProperties.SetHelpText(input,description);row.Children.Add(input);panel.Children.Add(row);
    }
    private static Border Information(string title,string description)
    {
        var content=new StackPanel();var heading=new TextBlock{Text=title,FontSize=14,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};DesktopTheme.Bind(heading,TextBlock.ForegroundProperty,DesktopTheme.SelectionText);content.Children.Add(heading);
        var detail=new TextBlock{Text=description,FontSize=12,LineHeight=20,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)};DesktopTheme.Bind(detail,TextBlock.ForegroundProperty,DesktopTheme.SelectionText);content.Children.Add(detail);
        var card=new Border{Padding=new Thickness(16),CornerRadius=new CornerRadius(12),Child=content,Margin=new Thickness(0,0,0,8)};DesktopTheme.Bind(card,Border.BackgroundProperty,DesktopTheme.AccentSoft);return card;
    }
    private void AddProfileEmptyState(Panel panel)
    {
        var empty=Information("从你的第一个模型配置开始","点击“新增配置”，填写服务地址与模型。保存不会发出请求；连接测试需要另外确认。");
        var style=new Style(typeof(Border));style.Setters.Add(new Setter(VisibilityProperty,Visibility.Collapsed));
        var trigger=new DataTrigger{Binding=new Binding("Items.Count"){Source=profiles},Value=0};trigger.Setters.Add(new Setter(VisibilityProperty,Visibility.Visible));style.Triggers.Add(trigger);empty.Style=style;
        AutomationProperties.SetLiveSetting(empty,AutomationLiveSetting.Polite);panel.Children.Add(empty);
    }
}
