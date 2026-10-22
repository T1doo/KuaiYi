using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Imaging;
using KuaiYi.Providers;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private OcrOnlyCoordinator? ocrOnly;
    private Window? ocrDialog;
    private bool ocrFlowBusy;
    partial void OcrSourceChanged(bool clearApproved)
    {
        var previous=ocrOnly;var previousDialog=ocrDialog;List<Exception> errors=[];ScreenshotPreviewCleanup.Attempt(errors,()=>previous?.Clear(clearApproved));ScreenshotPreviewCleanup.Attempt(errors,()=>previousDialog?.Close());
        if(errors.Count>0)throw new AggregateException("OCR cleanup unconfirmed.",errors);
    }
    partial void CollectOcrCleanup(ref Task cleanup){cleanup=ocrOnly?.ClearAsync()??Task.CompletedTask;}
    private void ObserveOcrState()
    {
        var state=lifecycle.Gate.Snapshot;
        if(state.Activation!=Activation.On||state.Suspension!=Suspension.None||state.Presentation==Presentation.Hidden||
            ocrOnly?.Preview is {} preview&&!ocrOnly.IsPreviewCurrent(preview))OcrSourceChanged(true);
    }
    private async Task TranslateImageText()
    {
        if(exiting||ocrFlowBusy||ocrOnly is not {} ocr||translation is not {} textFlow||SelectedSource.Image is not ImageSourceOwner image||retainedImage?.VersionId is not {} version)return;
        if(image.Kind==ImageSourceKind.Bitmap){button.ToolTip="此位图切片仅支持本地预览与逐次确认的图像理解；本地 OCR 尚未接入，没有发送。";return;}
        if(lifecycle.Gate.Snapshot.Activation!=Activation.On){button.ToolTip="启用后可提取图片文字；没有发送。";return;}
        bool InputCurrent()=>!exiting&&ReferenceEquals(SelectedSource.Image,image)&&retainedImage is {} current&&current.VersionId==version&&ReferenceEquals(current.Image,image)&&!image.IsDisposed&&
            lifecycle.Gate.Snapshot.Activation==Activation.On&&lifecycle.Gate.Snapshot.Suspension==Suspension.None&&lifecycle.Gate.Snapshot.Presentation!=Presentation.Hidden;
        long? operationEpoch=null;
        bool FlowCurrent()=>ReferenceEquals(ocrOnly,ocr)&&ReferenceEquals(translation,textFlow)&&InputCurrent()&&(operationEpoch==null||operationEpoch==lifecycle.Gate.Snapshot.Epoch);
        ocrFlowBusy=true;
        OcrCapabilitiesSnapshot? discovered=null;OcrTextPreview? heldPreview=null;Window? heldDialog=null;
        try
        {
            if(!ocr.Discover(version,image)){button.ToolTip="图片、启用状态或配置已改变，或原 OCR 尚未清理；没有发送。";return;}
            operationEpoch=lifecycle.Gate.Snapshot.Epoch;button.ToolTip="正在检查本机 OCR 能力 · 尚未发送";
            await ocr.Running;if(!FlowCurrent())return;
            discovered=ocr.Capabilities;
            if(discovered==null){button.ToolTip="本地 OCR 不可用 · "+OcrFailureLabel(ocr.Failure)+"；没有发送。";return;}
            var capability=discovered;
            var language=new ComboBox{ItemsSource=capability.Languages,SelectedIndex=0,IsEditable=false,Margin=new Thickness(0,8,0,8)};
            bool fits=image.Width<=Math.Min(capability.MaxImageDimension,(uint)ImageTransform.LongEdgeLimit)&&image.Height<=Math.Min(capability.MaxImageDimension,(uint)ImageTransform.LongEdgeLimit);
            var choose=new Window{Owner=this,Title="选择本机已安装的 OCR 语言",Width=480,Height=260,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner};
            var panel=new StackPanel{Margin=new Thickness(16)};
            panel.Children.Add(new TextBlock{Text=$"本机已安装 {capability.Languages.Length} 种识别语言；OCR 最大边长 {capability.MaxImageDimension} 像素。\n当前{(image.Kind==ImageSourceKind.Browser?"浏览器图片":image.Kind==ImageSourceKind.Bitmap?"投放位图":image.Kind==ImageSourceKind.LocalFile?"文件图片":"截图")} {image.Width} × {image.Height}；本地处理上限 {ImageTransform.LongEdgeLimit} 像素。"+(image.Notice is {} frameNotice?" · "+frameNotice:"")+(fits?"":"\n图片超出上限，请使用更小的图片。"),TextWrapping=TextWrapping.Wrap});panel.Children.Add(language);
            var next=new Button{Content="使用所选语言在本地提取文字",IsEnabled=fits};next.Click+=(_,_)=>choose.DialogResult=true;panel.Children.Add(next);choose.Content=panel;ocrDialog=heldDialog=choose;
            var capabilityWatch=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};capabilityWatch.Tick+=(_,_)=>{if(!ocr.IsCapabilitiesCurrent(capability))choose.Close();};capabilityWatch.Start();
            bool languageConfirmed;try{languageConfirmed=choose.ShowDialog()==true;}finally{capabilityWatch.Stop();}if(ReferenceEquals(ocrDialog,choose))ocrDialog=null;
            if(!languageConfirmed||!FlowCurrent()||!ocr.IsCapabilitiesCurrent(capability)){ocr.RejectCapabilities(capability);return;}
            if(language.SelectedItem is not string tag||!ocr.Start(capability,tag)){button.ToolTip="本地 OCR 未启动 · "+OcrFailureLabel(ocr.Failure)+"；没有发送。";return;}
            operationEpoch=lifecycle.Gate.Snapshot.Epoch;
            button.ToolTip="正在本地提取图片文字 · 尚未发送";
            await ocr.Running;
            if(!FlowCurrent())return;
            if(ocr.Preview is not {} preview||!ocr.IsPreviewCurrent(preview)){button.ToolTip="本地 OCR 未完成 · "+OcrFailureLabel(ocr.Failure)+"；没有发送。";return;}
            heldPreview=preview;
            var dialog=new Window{Owner=this,Title="OCR-only · 本地文字预览",Width=620,Height=540,WindowStartupLocation=WindowStartupLocation.CenterOwner};ocrDialog=heldDialog=dialog;
            var grid=new DockPanel{Margin=new Thickness(16)};
            var config=lifecycle.Settings!;var profile=config.Data.Profiles.First(p=>p.Id==config.Data.ActiveProfileId);
            var header=new TextBlock{Text=(preview.SourceKind==ImageSourceKind.Browser?"浏览器图片 · ":preview.SourceKind==ImageSourceKind.Bitmap?"投放位图":preview.SourceKind==ImageSourceKind.LocalFile?"文件图片 · ":"")+(preview.SourceNotice is {} notice?notice+"\n":"")+OcrOnlyCoordinator.Warning+$"\n仅发送下方提取文字；可能有识别错误，请核对。\n目标：{new EndpointOrigin(new Uri(profile.BaseUrl)).Value} · 模型：{profile.Model}；可能产生服务费用。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};DockPanel.SetDock(header,Dock.Top);grid.Children.Add(header);
            var controls=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,0,0)};DockPanel.SetDock(controls,Dock.Bottom);
            var style=new ComboBox{MinWidth=120,ItemsSource=Enum.GetValues<TranslationPreset>().Select(p=>new ComboBoxItem{Content=StyleLabel(p),Tag=p}).ToArray(),SelectedIndex=(int)(lifecycle.Settings?.Data.Translation.Preset??TranslationPreset.Faithful)};
            TranslationPreset selected=TranslationPreset.Faithful;
            var send=new Button{Content="确认发送这些文字",Margin=new Thickness(12,0,0,0)};var cancel=new Button{Content="取消",Margin=new Thickness(12,0,0,0)};
            send.Click+=(_,_)=>{if(style.SelectedItem is ComboBoxItem{Tag:TranslationPreset preset}){selected=preset;dialog.DialogResult=true;}};cancel.Click+=(_,_)=>dialog.DialogResult=false;
            controls.Children.Add(style);controls.Children.Add(send);controls.Children.Add(cancel);grid.Children.Add(controls);
            var text=new TextBox{Text=preview.Text,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,AcceptsReturn=true};grid.Children.Add(text);dialog.Content=grid;
            // Recheck while the modal dispatcher pumps Off/lock/settings/source changes.
            var watch=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};watch.Tick+=(_,_)=>{if(!ocr.IsPreviewCurrent(preview))dialog.Close();};watch.Start();
            bool confirmed;try{confirmed=dialog.ShowDialog()==true;}finally{watch.Stop();text.Clear();}
            if(!FlowCurrent()){ocr.RejectPreview(preview);return;}
            if(!confirmed){ocr.RejectPreview(preview);button.ToolTip="已取消 OCR 文字发送。";return;}
            var source=ocr.Approve(preview);var offer=source==null?null:textFlow.PrepareOcr(source,selected);
            if(offer==null||textFlow.Submit(offer,true)!=TextSubmission.Started)button.ToolTip="来源、启用状态或配置已改变；没有发送。";
        }
        catch(Exception error){if(FlowCurrent()){if(heldPreview!=null)ocr.RejectPreview(heldPreview);Report("本地 OCR 未完成；没有发送",error);}}
        finally
        {
            List<Exception> errors=[];if(discovered!=null)ScreenshotPreviewCleanup.Attempt(errors,()=>ocr.RejectCapabilities(discovered));if(heldPreview!=null)ScreenshotPreviewCleanup.Attempt(errors,()=>ocr.RejectPreview(heldPreview));if(ReferenceEquals(ocrDialog,heldDialog))ocrDialog=null;ocrFlowBusy=false;
            if(errors.Count>0&&ReferenceEquals(ocrOnly,ocr)&&InputCurrent())Report("OCR 能力任务清理尚未确认",new AggregateException(errors));
        }
    }
    private static string OcrFailureLabel(string? failure)=>failure switch
    {
        "UnsupportedPlatform"=>"当前系统不支持本地 OCR","IdentityMissing"=>"缺少应用包身份","IdentityMismatch"=>"Worker 包身份不匹配",
        "LanguageUnavailable"=>"没有可用的已安装 OCR 语言","NativeUnavailable"=>"本地 OCR 组件不可用","Cancelled"=>"已取消","Timeout"=>"本地 OCR 等待超时",
        "OcrDimensionExceeded"=>"图片超过本机 OCR 尺寸上限，请使用更小的图片","SMALLER_SCREENSHOT_REQUIRED"=>"图片超过本地处理上限，请使用更小的图片",
        "CLEANUP_UNCONFIRMED"=>"原 Worker 尚未确认清理，不能启动新任务",null=>"来源或启用状态已改变",_=>"图像或文字无法安全识别"
    };
}
