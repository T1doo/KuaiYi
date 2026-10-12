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
            },developmentBoundary:developmentBoundary);
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
                bool confirmed=MessageBox.Show(this,$"实际原文包含 {snapshot.Source.Scalars:N0} 个 Unicode 字符，超过自动发送上限 {snapshot.Settings.Data.Translation.AutoSoftLimitScalars:N0}。\n是否将完整选区发送到 {new EndpointOrigin(snapshot.Transport.ResolveEndpoint()).Value}？\n模型：{snapshot.Profile.Model}；可能产生服务费用。","快译 · 长文本发送确认",MessageBoxButton.OKCancel,MessageBoxImage.Information)==MessageBoxResult.OK;
                if(!confirmed){translation.RejectConfirmation(offer);translationStatus="已取消长文本发送，旧结果保留";}
                else if(translation.Submit(offer,true)!=TextSubmission.Started)translationStatus="来源、启用状态或配置已改变，未发送";
                button.ToolTip=translationStatus;break;
            case TextSubmission.Rejected:button.ToolTip="来源、启用状态或配置已改变，未发送";break;
            default:throw new ArgumentOutOfRangeException();
        }
    }
    private void RegenerateResult(bool currentSettings=false)
    {
        if(lifecycle.Gate.Snapshot.Activation!=Activation.On){button.ToolTip="启用后可重新生成";return;}
        var offer=translation?.PrepareRegeneration(currentSettings);
        if(offer==null){button.ToolTip="无法重新生成 · "+(translation?.LastRejection??"无保留原文");return;}
        SubmitTranslation(offer);
    }
    private void TranslationChanged(TextTranslationUpdate update)
    {
        if(exiting)return;
        var snapshot=update.Snapshot;
        var plan=ApiProfileMapper.Plan(snapshot.Profile,snapshot.Transport.DevelopmentBoundary);
        translationStatus=update.Failure==null?$"{(snapshot.Transport.IsDevelopmentFixture?"本地测试输出 · ":snapshot.Transport.BaseUrl.Scheme=="http"?"非加密 HTTP · ":"")}{StyleLabel(snapshot)} · {SourceLabel(snapshot)} · {snapshot.Profile.Model}{(plan.Notices.Length>0?" · "+string.Join("；",plan.Notices):"")}":$"翻译失败 · {FailureLabel(update.Failure)}；旧结果可回看";
        if(update.Presentation.Kind==OverlayResultEventKind.Begin)connectionStatus.Text=$"生成版本 {snapshot.VersionNumber} · {StyleLabel(snapshot)}";
        ApplyResult(update.Identity,update.Presentation,alreadyApplied:true);
        RefreshResultLabel();button.ToolTip=update.Presentation.Kind==OverlayResultEventKind.Begin?connectionStatus.Text:translationStatus;
    }
    private void RefreshResultLabel()
    {
        var snapshot=translation?.SelectedSnapshot;var selected=overlayResults.Versions.FirstOrDefault(v=>v.Identity.VersionId==overlayResults.SelectedVersionId);
        resultLabel.Text=snapshot==null?"":$"{(snapshot.Transport.IsDevelopmentFixture?"本地测试输出 · ":snapshot.Transport.BaseUrl.Scheme=="http"?"非加密 HTTP · ":"")}{(selected?.Complete==true||selected?.Stopped==true?"版本":"生成版本")} {snapshot.VersionNumber} · {StyleLabel(snapshot)} · {SourceLabel(snapshot)} · {snapshot.Profile.Model}{(selected?.Stopped==true?" · 已停止":"")}";
        resultLabel.ToolTip=snapshot==null?null:$"此版本快照 · {snapshot.FullTemplateVersion} · {new EndpointOrigin(snapshot.Transport.ResolveEndpoint()).Value}";
        var next=translation?.NextGeneration;if(styleTool!=null){styleTool.IsEnabled=next!=null;styleTool.Content=next?.Preset switch{TranslationPreset.Faithful=>"忠实",TranslationPreset.Plain=>"通俗",TranslationPreset.Professional=>"专业",TranslationPreset.Explain=>"解释",_=>"风格"};System.Windows.Automation.AutomationProperties.SetName(styleTool,next==null?"会话风格":$"下次生成风格：{StyleLabel(next.Preset)}");styleTool.ToolTip=next==null?"无保留会话":$"下次生成：版本 {next.VersionNumber} · {StyleLabel(next.Preset)}；不改变已有版本";}
        foreach(var tool in tools)if(Equals(tool.Content,"重新生成")){tool.IsEnabled=lifecycle.Gate.Snapshot.Activation==Activation.On&&snapshot!=null;tool.ToolTip=next==null?"无保留原文":$"重新生成：版本 {next.VersionNumber} · {StyleLabel(next.Preset)} · {next.Model}";}
    }
    private static string StyleLabel(TextTranslationSnapshot value)=>StyleLabel(value.Preset);
    private static string StyleLabel(TranslationPreset? preset)=>preset switch{TranslationPreset.Faithful=>"忠实翻译",TranslationPreset.Plain=>"通俗翻译",TranslationPreset.Professional=>"专业翻译",TranslationPreset.Explain=>"解释",_=>"公开探针"};
    private string RegenerationMenuLabel(bool currentSettings=false)
    {
        string action=currentSettings?"按当前设置重新翻译":"重新生成";var next=translation?.NextGeneration;
        return next==null?action:$"{action} · 版本 {next.VersionNumber} · {StyleLabel(currentSettings?lifecycle.Settings?.Data.Translation.Preset:next.Preset)}";
    }
    private void AddSessionStyleMenu(ContextMenu menu)
    {
        var next=translation?.NextGeneration;var styles=new MenuItem{Header=next==null?"本会话下次生成风格":$"下次：版本 {next.VersionNumber} · {StyleLabel(next.Preset)}",IsEnabled=next!=null};
        foreach(var preset in Enum.GetValues<TranslationPreset>())
        {
            var item=new MenuItem{Header=StyleLabel(preset),IsCheckable=true,IsChecked=next?.Preset==preset};
            System.Windows.Automation.AutomationProperties.SetName(item,$"本会话风格：{StyleLabel(preset)}");item.Click+=(_,_)=>{if(translation?.SetSessionStyle(preset)==true){RefreshResultLabel();button.ToolTip=$"下次生成使用{StyleLabel(preset)}；本会话已有版本保留原风格";}};styles.Items.Add(item);
        }
        styles.Items.Add(new Separator());var original=new MenuItem{Header="沿用所示版本风格",IsEnabled=translation?.SessionStyle!=null};original.Click+=(_,_)=>{if(translation?.SetSessionStyle(null)==true)RefreshResultLabel();};styles.Items.Add(original);menu.Items.Add(styles);
    }
    private static string SourceLabel(TextTranslationSnapshot value)=>value.Source.SourceKind switch{SelectionSourceKind.Clipboard=>"复制兼容",SelectionSourceKind.OleDrop=>"拖放文字",_=>"选区"};
    private static string FailureLabel(string reason)=>reason switch{"AUTH_FAILED" or "SECRET_UNAVAILABLE"=>"密钥不可用，请检查设置","REQUEST_TIMEOUT"=>"服务响应超时","NETWORK_ERROR" or "TLS_ERROR"=>"无法连接服务","RATE_LIMITED"=>"服务限流，请稍后重试","ENDPOINT_NOT_FOUND" or "CONFIG_INVALID"=>"服务配置无效","TEST_BOUNDARY_REJECTED"=>"测试配置越出本地授权边界，已拒绝发送","CANCELLED_OR_UNCONSENTED" or "SOURCE_OR_SETTINGS_CHANGED"=>"来源或授权已改变","STREAM_INTERRUPTED"=>"输出中断","LOCAL_OUTPUT_LIMIT"=>"输出超过本地安全上限",_=>"请求未完成，请检查服务设置"};
    private async Task StopTranslation()
    {
        var owned=translation;translation=null;if(apiTests!=null){await apiTests.DisposeAsync();apiTests=null;}
        try{if(owned!=null)await owned.DisposeAsync();}
        finally{owned?.Clear();translationProvider?.Dispose();translationProvider=null;}
    }
}
