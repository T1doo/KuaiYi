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
        List<Exception> errors=[];ScreenshotPreviewCleanup.Attempt(errors,()=>ocrOnly?.Clear(clearApproved));ScreenshotPreviewCleanup.Attempt(errors,()=>ocrDialog?.Close());
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
        if(exiting||ocrFlowBusy||ocrOnly is not {} ocr||translation is not {} textFlow||SelectedSource.Image is not {} image||SelectedSource.VersionId is not {} version)return;
        if(lifecycle.Gate.Snapshot.Activation!=Activation.On){button.ToolTip="启用后可提取图片文字；没有发送。";return;}
        bool InputCurrent()=>!exiting&&imageSelected&&retainedImage is {} current&&current.VersionId==version&&ReferenceEquals(current.Image,image)&&!image.IsDisposed&&
            lifecycle.Gate.Snapshot.Activation==Activation.On&&lifecycle.Gate.Snapshot.Suspension==Suspension.None&&lifecycle.Gate.Snapshot.Presentation!=Presentation.Hidden;
        long? operationEpoch=null;
        bool FlowCurrent()=>ReferenceEquals(ocrOnly,ocr)&&ReferenceEquals(translation,textFlow)&&InputCurrent()&&(operationEpoch==null||operationEpoch==lifecycle.Gate.Snapshot.Epoch);
        ocrFlowBusy=true;
        // Language is an explicit local request, never a model download or guessed availability.
        var language=new TextBox{Text="en-US",Margin=new Thickness(0,8,0,8)};
        var choose=new Window{Owner=this,Title="本地 OCR 语言",Width=420,Height=210,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var panel=new StackPanel{Margin=new Thickness(16)};panel.Children.Add(new TextBlock{Text="输入已安装的 OCR 语言标签（如 en-US、zh-Hans）。缺少身份或语言时停止，不会上传图片。",TextWrapping=TextWrapping.Wrap});panel.Children.Add(language);
        var next=new Button{Content="在本地提取文字"};next.Click+=(_,_)=>choose.DialogResult=true;panel.Children.Add(next);choose.Content=panel;ocrDialog=choose;
        try
        {
            if(choose.ShowDialog()!=true)return;ocrDialog=null;if(!InputCurrent())return;
            if(!ocr.Start(version,image,language.Text)){button.ToolTip="图片、启用状态或配置已改变，或原 OCR 尚未清理；没有发送。";return;}
            operationEpoch=lifecycle.Gate.Snapshot.Epoch;
            button.ToolTip="正在本地提取图片文字 · 尚未发送";
            await ocr.Running;
            if(!FlowCurrent())return;
            if(ocr.Preview is not {} preview||!ocr.IsPreviewCurrent(preview)){button.ToolTip="本地 OCR 未完成 · "+OcrFailureLabel(ocr.Failure)+"；没有发送。";return;}
            var dialog=new Window{Owner=this,Title="OCR-only · 本地文字预览",Width=620,Height=540,WindowStartupLocation=WindowStartupLocation.CenterOwner};ocrDialog=dialog;
            var grid=new DockPanel{Margin=new Thickness(16)};
            var config=lifecycle.Settings!;var profile=config.Data.Profiles.First(p=>p.Id==config.Data.ActiveProfileId);
            var header=new TextBlock{Text=OcrOnlyCoordinator.Warning+$"\n仅发送下方提取文字；可能有识别错误，请核对。\n目标：{new EndpointOrigin(new Uri(profile.BaseUrl)).Value} · 模型：{profile.Model}；可能产生服务费用。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};DockPanel.SetDock(header,Dock.Top);grid.Children.Add(header);
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
            if(!FlowCurrent()){ocr.Clear(false);return;}
            if(!confirmed){ocr.Clear(false);button.ToolTip="已取消 OCR 文字发送。";return;}
            var source=ocr.Approve(preview);var offer=source==null?null:textFlow.PrepareOcr(source,selected);
            if(offer==null||textFlow.Submit(offer,true)!=TextSubmission.Started)button.ToolTip="来源、启用状态或配置已改变；没有发送。";
        }
        catch(Exception error){if(FlowCurrent()){ocr.Clear();Report("本地 OCR 未完成；没有发送",error);}}
        finally{ocrDialog=null;ocrFlowBusy=false;}
    }
    private static string OcrFailureLabel(string? failure)=>failure switch
    {
        "UnsupportedPlatform"=>"当前系统不支持本地 OCR","IdentityMissing"=>"缺少应用包身份","IdentityMismatch"=>"Worker 包身份不匹配",
        "LanguageUnavailable"=>"未安装所选 OCR 语言","NativeUnavailable"=>"本地 OCR 组件不可用","Cancelled"=>"已取消","Timeout"=>"本地 OCR 等待超时",
        "CLEANUP_UNCONFIRMED"=>"原 Worker 尚未确认清理，不能启动新任务",null=>"来源或启用状态已改变",_=>"图像或文字无法安全识别"
    };
}
