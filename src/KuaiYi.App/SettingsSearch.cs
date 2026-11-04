using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
namespace KuaiYi.App;

internal sealed partial class SettingsWindow
{
    private void SetupSettingsSearch(Panel header,ListBox navigation)
    {
        var region=new StackPanel{Margin=new Thickness(0,18,0,0)};
        var searchDisclosure=new Expander{Header="查找设置 · Ctrl+K",Content=region,IsExpanded=false,Margin=new Thickness(0,12,0,0)};
        header.Children.Add(searchDisclosure);
        var label=new TextBlock{Text="查找设置",FontWeight=FontWeights.SemiBold,FontSize=12};
        region.Children.Add(label);
        var row=new DockPanel{Margin=new Thickness(0,6,0,0)};
        region.Children.Add(row);
        var clear=new Button{Content="清空",Margin=new Thickness(8,0,0,0),IsEnabled=false};
        DockPanel.SetDock(clear,Dock.Right);row.Children.Add(clear);
        var search=new TextBox{MaxLength=SettingsSearchCatalog.MaximumQueryLength,MinWidth=0,
            ToolTip="输入设置关键词，例如：字号、API、隐私。Ctrl+K 聚焦。"};
        AutomationProperties.SetName(search,"查找设置分类");
        AutomationProperties.SetHelpText(search,"仅搜索固定分类名称；不搜索配置值或翻译内容。Ctrl+K 聚焦，Escape 清空。");
        AutomationProperties.SetName(clear,"清空设置搜索");row.Children.Add(search);
        var hint=new TextBlock{FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)};
        DesktopTheme.Bind(hint,TextBlock.ForegroundProperty,DesktopTheme.Muted);
        AutomationProperties.SetLiveSetting(hint,AutomationLiveSetting.Polite);
        var matches=new WrapPanel{Margin=new Thickness(-4,4,-4,0)};
        region.Children.Add(hint);region.Children.Add(matches);
        void Refresh()
        {
            matches.Children.Clear();clear.IsEnabled=search.Text.Length>0;
            if(string.IsNullOrWhiteSpace(search.Text))
            {hint.Text="按关键词直达分类 · Ctrl+K · 不改变当前草稿";return;}
            var hits=SettingsSearchCatalog.Search(search.Text);
            hint.Text=hits.Count==0?"没有匹配的分类，试试“字号”“API”或“隐私”。":$"找到 {hits.Count} 个分类 · 选择后跳转，草稿保留";
            foreach(var hit in hits)
            {
                var jump=new Button{Content=hit.Title,Margin=new Thickness(4),ToolTip="前往"+hit.Title};
                AutomationProperties.SetName(jump,"前往设置分类："+hit.Title);
                jump.Click+=(_,_)=>{navigation.SelectedIndex=hit.PageIndex;pageHeading?.Focus();};
                matches.Children.Add(jump);
            }
        }
        search.TextChanged+=(_,_)=>Refresh();
        clear.Click+=(_,_)=>{search.Clear();search.Focus();};
        // Handle Escape for every search-owned focus target, including results.
        // Otherwise the window's original IsCancel button would discard the draft.
        region.PreviewKeyDown+=(_,e)=>
        {if(e.Key==Key.Escape&&Keyboard.Modifiers==ModifierKeys.None&&search.Text.Length>0)
            {search.Clear();search.Focus();e.Handled=true;}};
        search.PreviewKeyDown+=(_,e)=>
        {if(e.Key==Key.Down&&Keyboard.Modifiers==ModifierKeys.None&&matches.Children.Count>0)
            {((Button)matches.Children[0]).Focus();e.Handled=true;}};
        PreviewKeyDown+=(_,e)=>
        {if(!e.Handled&&e.Key==Key.K&&Keyboard.Modifiers==ModifierKeys.Control&&
            !hotkeyFields.Values.Any(field=>field.IsKeyboardFocusWithin))
            {searchDisclosure.IsExpanded=true;search.Focus();search.SelectAll();e.Handled=true;}};
        Closed+=(_,_)=>{search.Clear();matches.Children.Clear();hint.Text="";};
        Refresh();
    }
}
