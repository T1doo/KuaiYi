using System.Windows;
using System.Windows.Controls;
using KuaiYi.Imaging;
using KuaiYi.Providers;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private bool ConfirmImageSend(ProductionScreenshotOffer offer)=>MessageBox.Show(this,offer.ConfirmationText,"快译 · 本次图片发送确认",MessageBoxButton.OKCancel,MessageBoxImage.Information)==MessageBoxResult.OK;
    private void RegenerateImageResult(bool currentSettings=false,bool reclassify=false,ImageOutputMode? mode=null)
    {
        ImageRegenerationCommand? admitted=null;
        Task original=StartQualifiedImageRegeneration(currentSettings,reclassify,mode,command=>
        {
            admitted=command;
            return command.Mode==ImageOutputMode.Automatic?TranslateImageText(true,command):SubmitSmartScreenshot(null,ConfirmImageSend,command);
        });
        _=ObserveImageRegeneration(original,()=>admitted);
    }
    private async Task ObserveImageRegeneration(Task original,Func<ImageRegenerationCommand?> admitted)
    {
        try{await original;}
        catch(Exception error)
        {
            // Observe without replacing the faulted original smartImageTask or silently reopening admission.
            if(!exiting&&admitted() is {} command&&retainedImage is {} retained&&ReferenceEquals(retained.Image,command.Image)&&retained.VersionId==command.SourceVersion)
                Report("本次图片操作未确认结束；旧结果保留，请先清除当前内容",error);
        }
    }
    private void AddImageRegenerationCommands(ContextMenu menu)
    {
        if(SelectedSource.Generation is not {} snapshot)return;
        var parent=new MenuItem{Header=$"此版本图片 · {snapshot.Number}",IsEnabled=CanRegenerateImage,ToolTip=ImageRegenerationQualified?"使用所选版本的原图片；每次独立确认发送，不读取当前剪贴板或选区。":ImageRegenerationQualificationNotice};
        ToolTipService.SetShowOnDisabled(parent,true);
        void Add(string label,bool reclassify=false,ImageOutputMode? mode=null)
        {var item=new MenuItem{Header=label};item.Click+=(_,_)=>RegenerateImageResult(reclassify:reclassify,mode:mode);parent.Items.Add(item);}
        Add("重新生成 · 复用此版本分类");Add("重新判断图片 · 先分类再生成",true);
        parent.Items.Add(new Separator());Add("本次仅翻译图中文字",mode:ImageOutputMode.TranslateText);Add("本次按图文解释",mode:ImageOutputMode.ExplainImage);Add("本次智能判断 · 重新取得本地批准",mode:ImageOutputMode.Automatic);
        menu.Items.Add(parent);
    }
}
