using System.Windows;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.Providers;
using KuaiYi.Core.Settings;
using System.Windows.Controls;
using KuaiYi.Windows;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private ProviderClient? translationProvider;private TextTranslationCoordinator? translation;
    private ApiConnectionTestWorkflow? apiTests;
    private string? translationStatus;
    private void SetupTranslation(DevelopmentTranslationBoundary? developmentBoundary)
    {
        if(!enableSelectionPlatform)return; // Public growth harness never acquires text or calls HTTP.
        translationProvider=new(developmentBoundary);
        apiTests=new(lifecycle.Gate,()=>lifecycle.Settings,translationProvider,new DispatcherSynchronizationContext(Dispatcher),
            async(identity,intent,cancellation)=>
            {using var lease=await coordinator.OpenRequestSecretAsync(lifecycle.Gate,identity,intent.Binding,new(intent.Plan.Transport.ResolveEndpoint()),cancellation).ConfigureAwait(false);return new KuaiYi.Providers.SecretLease(lease.Bytes.Span);},developmentBoundary);
        translation=new(lifecycle.Gate,()=>lifecycle.Settings,
            (source,explicitUse)=>explicitUse?SelectionDesktop.IsRetainedSourceCurrent(source.Window):SelectionDesktop.IsCurrent(source.Window),
            translationProvider,new DispatcherSynchronizationContext(Dispatcher),overlayResults,TranslationChanged,
            async(identity,snapshot,cancellation)=>
            {
                using var encryptedLease=await coordinator.OpenRequestSecretAsync(lifecycle.Gate,identity,snapshot.Binding,new(snapshot.Transport.ResolveEndpoint()),cancellation).ConfigureAwait(false);
                return new KuaiYi.Providers.SecretLease(encryptedLease.Bytes.Span);
            },developmentBoundary:developmentBoundary,ocrSourceCurrent:source=>ocrOnly?.IsApprovedSourceCurrent(source)==true);
        ocrOnly=new(lifecycle.Gate,()=>lifecycle.Settings,
            (version,source)=>!exiting&&imageSelected&&retainedImage is {} image&&image.VersionId==version&&image.Image.SourceId==source&&!image.Image.IsDisposed,
            new DispatcherSynchronizationContext(Dispatcher),()=>new KuaiYi.Imaging.ImageWorkerClient(System.IO.Path.Combine(AppContext.BaseDirectory,"KuaiYi.ImageWorker.exe")));
    }
    private void QueueTranslation(SelectionSourceSnapshot source)
    {
        var offer=translation?.Prepare(source);
        if(offer==null){button.ToolTip="尚未发送 · "+(translation?.LastRejection??"模型调度未启用");return;}
        // Capture's final source commit is synchronous. Leave it before starting network or showing confirmation UI.
        Dispatcher.BeginInvoke(()=>{try{if(translation?.IsOfferCurrent(offer)==true)SubmitTranslation(offer);}catch(Exception error){lifecycle.Gate.Disable();StopPresentation();Refresh();Report("翻译调度失败，已停用",error);}});
    }
    private void SubmitTranslation(TextTranslationOffer offer)
    {
        if(exiting||translation==null)return;
        switch(translation.Submit(offer))
        {
            case TextSubmission.Started:break;
            case TextSubmission.Reopened:ReopenResult();break;
            case TextSubmission.ConfirmationRequired:
                StopPresentation();
                var snapshot=offer.Snapshot;
                string explanation=snapshot.Source.SourceKind==SelectionSourceKind.OcrOnly
                    ?KuaiYi.Imaging.OcrOnlyCoordinator.Warning+"\n是否发送已核对的本地 OCR 文字？"
                    :$"实际原文包含 {snapshot.Source.Scalars:N0} 个 Unicode 字符，超过自动发送上限 {snapshot.Settings.Data.Translation.AutoSoftLimitScalars:N0}。\n是否发送完整选区？";
                bool confirmed=MessageBox.Show(this,$"{explanation}\n目标：{new EndpointOrigin(snapshot.Transport.ResolveEndpoint()).Value}\n模型：{snapshot.Profile.Model}；可能产生服务费用。","快译 · 文字发送确认",MessageBoxButton.OKCancel,MessageBoxImage.Information)==MessageBoxResult.OK;
                if(!confirmed){translation.RejectConfirmation(offer);translationStatus="已取消文字发送，旧结果保留";}
                else if(translation.Submit(offer,true)!=TextSubmission.Started)translationStatus="来源、启用状态或配置已改变，未发送";
                button.ToolTip=translationStatus;break;
            case TextSubmission.Rejected:button.ToolTip="来源、启用状态或配置已改变，未发送";break;
            default:throw new ArgumentOutOfRangeException();
        }
    }
    private void RegenerateResult(bool currentSettings=false)
    {
        if(lifecycle.Gate.Snapshot.Activation!=Activation.On){button.ToolTip="启用后可重新生成";return;}
        if(SelectedSource.Image!=null){button.ToolTip="本地图片尚未接入翻译；没有发送。";return;}
        var offer=translation?.PrepareRegeneration(currentSettings);
        if(offer==null){button.ToolTip="无法重新生成 · "+(translation?.LastRejection??"无保留原文");return;}
        SubmitTranslation(offer);
    }
    private string RegenerationMenuLabel(bool currentSettings=false)
    {
        string action=currentSettings?"按当前设置重新翻译":"重新生成";var next=SelectedNextGeneration;
        return next==null?action:$"{action} · 版本 {next.VersionNumber} · {StyleLabel(currentSettings?lifecycle.Settings?.Data.Translation.Preset:next.Preset)}";
    }
    private void AddSessionStyleMenu(ContextMenu menu)
    {
        var next=SelectedNextGeneration;var styles=new MenuItem{Header=next==null?"本会话下次生成风格":$"下次：版本 {next.VersionNumber} · {StyleLabel(next.Preset)}",IsEnabled=next!=null};
        foreach(var preset in Enum.GetValues<TranslationPreset>())
        {
            var item=new MenuItem{Header=StyleLabel(preset),IsCheckable=true,IsChecked=next?.Preset==preset};
            System.Windows.Automation.AutomationProperties.SetName(item,$"本会话风格：{StyleLabel(preset)}");item.Click+=(_,_)=>{if(translation?.SetSessionStyle(preset)==true){RefreshResultLabel();button.ToolTip=$"下次生成使用{StyleLabel(preset)}；本会话已有版本保留原风格";}};styles.Items.Add(item);
        }
        styles.Items.Add(new Separator());var original=new MenuItem{Header="沿用所示版本风格",IsEnabled=translation?.SessionStyle!=null};original.Click+=(_,_)=>{if(translation?.SetSessionStyle(null)==true)RefreshResultLabel();};styles.Items.Add(original);menu.Items.Add(styles);
    }
    private async Task StopTranslation()
    {
        if(ocrOnly!=null){await ocrOnly.DisposeAsync();ocrOnly=null;}
        var owned=translation;translation=null;if(apiTests!=null){await apiTests.DisposeAsync();apiTests=null;}
        try{if(owned!=null)await owned.DisposeAsync();}
        finally{owned?.Clear();translationProvider?.Dispose();translationProvider=null;}
    }
}
