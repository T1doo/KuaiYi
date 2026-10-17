using System.Windows.Controls;
using KuaiYi.Core.Settings;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private TranslationPreset? SelectedScreenshotPreset=>LocalImagePreview&&retainedImage is {} retained&&ReferenceEquals(SelectedSource.Image,retained.Image)
        ?imageTranslation?.PresetForSource(retained.VersionId,retained.Image.SourceId):null;
    private void AddScreenshotStyleMenu(ContextMenu menu)
    {
        var selected=SelectedScreenshotPreset;var styles=new MenuItem{Header=selected is {} value?$"本次截图风格：{StyleLabel(value)}":"本次截图风格",IsEnabled=selected!=null};
        if(retainedImage is {} retained)
        {
            void Select(TranslationPreset? preset)
            {
                if(SelectedScreenshotPreset==null||!ReferenceEquals(retainedImage,retained)||imageTranslation?.SetSourcePreset(retained.VersionId,retained.Image.SourceId,preset)!=true)return;
                RefreshResultLabel();
            }
            foreach(var preset in Enum.GetValues<TranslationPreset>())
            {
                var item=new MenuItem{Header=StyleLabel(preset),IsCheckable=true,IsChecked=selected==preset};item.Click+=(_,_)=>Select(preset);styles.Items.Add(item);
            }
            var defaults=new MenuItem{Header="沿用当前默认风格"};defaults.Click+=(_,_)=>Select(null);styles.Items.Add(defaults);
        }
        styles.ToolTip="仅用于当前未发送截图；不改变默认设置。按图文解释仍使用解释风格。";menu.Items.Add(styles);
    }
}
