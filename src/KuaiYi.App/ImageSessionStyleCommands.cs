using System.Windows.Controls;
using KuaiYi.Core.Settings;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private ImageGenerationPreview? SelectedImageRegenerationPreview=>SelectedSource.Generation!=null?imageTranslation?.NextRegeneration:null;
    private void AddImageSessionStyleMenu(ContextMenu menu)
    {
        var next=SelectedImageRegenerationPreview;var flow=imageTranslation;var source=SelectedSource;
        var styles=new MenuItem{Header=next==null?"本会话下次图片文字风格":$"下次图片：版本 {next.VersionNumber} · {StyleLabel(next.Preset)}",IsEnabled=next!=null};
        if(next!=null&&flow!=null&&source.VersionId is {} id&&source.Image is {} image)
        {
            long input=imageInputRevision,result=resultRevision;
            void Select(TranslationPreset? preset)
            {
                if(exiting||!ReferenceEquals(imageTranslation,flow)||imageInputRevision!=input||resultRevision!=result||SelectedSource.VersionId!=id||!ReferenceEquals(SelectedSource.Image,image))return;
                if(!flow.SetRegenerationStyle(id,preset))return;
                RefreshResultLabel();button.ToolTip="仅调整本会话下次图片文字风格；已有结果与正在进行的请求不变。"+(ImageRegenerationQualified?"":"图片重新生成仍待验收开启。");
            }
            foreach(var preset in Enum.GetValues<TranslationPreset>())
            {
                var item=new MenuItem{Header=StyleLabel(preset),IsCheckable=true,IsChecked=next.Preset==preset};
                System.Windows.Automation.AutomationProperties.SetName(item,$"下次图片文字风格：{StyleLabel(preset)}");item.Click+=(_,_)=>Select(preset);styles.Items.Add(item);
            }
            styles.Items.Add(new Separator());var shown=new MenuItem{Header="沿用所示版本风格",IsEnabled=flow.RegenerationStyle!=null};shown.Click+=(_,_)=>Select(null);styles.Items.Add(shown);
        }
        styles.ToolTip="只改下次文字风格，不修改全局设置或旧版本；图文解释仍使用解释风格。不采集、不读取密钥、不发送。"+(ImageRegenerationQualified?"":"重新生成尚未开放。");menu.Items.Add(styles);
    }
}
