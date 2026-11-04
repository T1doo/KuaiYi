using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace KuaiYi.App;

internal sealed partial class SettingsWindow
{
    private sealed record SettingsSection(string Label,int Page)
    {public override string ToString()=>Label;}
    private FrameworkElement SimpleNavigation(ListBox route)
    {
        var mainItems=new[]{new SettingsSection("翻译与模型",1),new SettingsSection("外观",3),new SettingsSection("快捷操作",4)};
        var extraItems=new[]{new SettingsSection("翻译偏好",2),new SettingsSection("图片与集成",5),new SettingsSection("隐私与诊断",6),new SettingsSection("备份与恢复",0)};
        ListBox List(SettingsSection[] items)=>new(){ItemsSource=items,DisplayMemberPath=nameof(SettingsSection.Label),
            Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0),
            ItemContainerStyle=(Style)Resources[DesktopTheme.NavigationItem]};
        var main=List(mainItems);var extra=List(extraItems);
        AutomationProperties.SetName(main,"常用设置");AutomationProperties.SetName(extra,"更多设置分类");
        var disclosure=new Expander{Header="更多设置",IsExpanded=false,Content=extra,Margin=new Thickness(0,16,0,0)};
        var panel=new StackPanel();panel.Children.Add(main);panel.Children.Add(disclosure);
        bool syncing=false;
        main.SelectionChanged+=(_,_)=>{if(!syncing&&main.SelectedItem is SettingsSection section)route.SelectedIndex=section.Page;};
        extra.SelectionChanged+=(_,_)=>{if(!syncing&&extra.SelectedItem is SettingsSection section)route.SelectedIndex=section.Page;};
        route.SelectionChanged+=(_,_)=>
        {
            syncing=true;
            try
            {
                main.SelectedItem=mainItems.FirstOrDefault(item=>item.Page==route.SelectedIndex);
                extra.SelectedItem=extraItems.FirstOrDefault(item=>item.Page==route.SelectedIndex);
                if(extra.SelectedItem!=null)disclosure.IsExpanded=true;
            }
            finally{syncing=false;}
        };
        return panel;
    }
}
