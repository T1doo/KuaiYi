using KuaiYi.Core;
using KuaiYi.Providers;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private void TranslationChanged(TextTranslationUpdate update)
    {
        if(exiting)return;
        if(update.Presentation.Kind==OverlayResultEventKind.Usage){RefreshResultLabel();return;}
        var snapshot=update.Snapshot;
        var plan=ApiProfileMapper.Plan(snapshot.Profile,snapshot.Transport.DevelopmentBoundary);
        translationStatus=update.Failure==null?$"{(snapshot.Transport.IsDevelopmentFixture?"本地测试输出 · ":snapshot.Transport.BaseUrl.Scheme=="http"?"非加密 HTTP · ":"")}{StyleLabel(snapshot)} · {SourceLabel(snapshot)} · {snapshot.Profile.Model}{(plan.Notices.Length>0?" · "+string.Join("；",plan.Notices):"")}":$"翻译失败 · {FailureLabel(update.Failure)}；旧结果可回看";
        if(update.Presentation.Kind==OverlayResultEventKind.Begin)connectionStatus.Text=$"生成版本 {snapshot.VersionNumber} · {StyleLabel(snapshot)}";
        ApplyResult(update.Identity,update.Presentation,alreadyApplied:true);
        if(update.Failure!=null)RestoreImageAfterTextFailure(update.Identity);
        RefreshResultLabel();button.ToolTip=update.Presentation.Kind==OverlayResultEventKind.Begin?connectionStatus.Text:translationStatus;
    }
    private static string FailureLabel(string reason)=>reason switch{"AUTH_FAILED" or "SECRET_UNAVAILABLE"=>"密钥不可用，请检查设置","REQUEST_TIMEOUT"=>"总时限已到","CONNECT_TIMEOUT"=>"连接超时","RESPONSE_HEADERS_TIMEOUT"=>"响应头超时","FIRST_RESPONSE_TIMEOUT"=>"首有效响应超时","READ_IDLE_TIMEOUT"=>"有效内容读取停滞","NETWORK_ERROR" or "TLS_ERROR"=>"无法连接服务","RATE_LIMITED"=>"服务限流，请稍后重试","ENDPOINT_NOT_FOUND" or "CONFIG_INVALID"=>"服务配置无效","TEST_BOUNDARY_REJECTED"=>"测试配置越出本地授权边界，已拒绝发送","CANCELLED_OR_UNCONSENTED" or "SOURCE_OR_SETTINGS_CHANGED"=>"来源或授权已改变","STREAM_INTERRUPTED"=>"输出中断","LOCAL_OUTPUT_LIMIT"=>"输出超过本地安全上限",_=>"请求未完成，请检查服务设置"};
}
