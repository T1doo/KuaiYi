using System.Windows.Controls;
using Microsoft.Win32;
namespace KuaiYi.App;

internal sealed partial class SettingsWindow
{
    private readonly TextBlock displayPreview=new(){TextWrapping=System.Windows.TextWrapping.Wrap};
    private readonly Button displayApply=new(){Content="应用预览到草稿",IsEnabled=false},displayWrite=new(){Content="导出到新文件…",IsEnabled=false};
    private void SetupDisplayPreferences(Panel panel)
    {
        Note(panel,"显示偏好文件仅包含字号、首选宽度、主题、减少动画。导入先预览，再应用到草稿；仍须保存。导出只创建新文件，不覆盖已有文件。");
        var actions=new WrapPanel();var import=new Button{Content="导入显示偏好…"};var export=new Button{Content="预览导出四字段"};var discard=new Button{Content="撤销预览"};
        foreach(var button in new[]{import,export,displayApply,displayWrite,discard}){button.Margin=new System.Windows.Thickness(4);actions.Children.Add(button);}panel.Children.Add(actions);panel.Children.Add(displayPreview);
        import.Click+=async(_,_)=>{if(!CanDisplayTransfer())return;CancelDisplayPreferences();var picker=new OpenFileDialog{Title="导入四字段显示偏好",Filter="显示偏好 JSON (*.json)|*.json",Multiselect=false,CheckFileExists=true};if(picker.ShowDialog(this)==true)await PreviewDisplayImport(picker.FileName);};
        export.Click+=(_,_)=>PreviewDisplayExport();displayApply.Click+=(_,_)=>ApplyDisplayImport();discard.Click+=(_,_)=>CancelDisplayPreferences(announce:true);
        displayWrite.Click+=async(_,_)=>{if(!CanDisplayTransfer())return;var picker=new SaveFileDialog{Title="将四字段显示偏好导出到新文件",Filter="显示偏好 JSON (*.json)|*.json",DefaultExt=".json",AddExtension=true,FileName="KuaiYi-display-preferences.json",OverwritePrompt=false,CheckPathExists=true};if(picker.ShowDialog(this)==true)await WriteDisplayExport(picker.FileName);};
        font.TextChanged+=(_,_)=>DisplayFieldsEdited();width.TextChanged+=(_,_)=>DisplayFieldsEdited();theme.SelectionChanged+=(_,_)=>DisplayFieldsEdited();motion.SelectionChanged+=(_,_)=>DisplayFieldsEdited();
        Closing+=(_,_)=>CancelDisplayPreferences();Closed+=(_,_)=>CloseDisplayPreferencesView();
    }
}
