using KuaiYi.Core;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private RequestIdentity? pendingFailureReveal;
    private void OfferFailureReveal(RequestIdentity identity)
    {if(overlayResults.LastFailure?.Identity==identity&&lifecycle.Gate.TryRevealDelta(identity))pendingFailureReveal=identity;}
    private bool TryRevealFailure()
    {
        var pending=pendingFailureReveal;pendingFailureReveal=null;
        return pending is {} id&&!exiting&&IsVisible&&overlayResults.LastFailure?.Identity==id&&lifecycle.Gate.CanApply(id)&&lifecycle.Gate.TryRevealDelta(id);
    }
    private CachedOverlayVersion? FailureNoticeVersion=>SelectedSource.Image!=null?null:overlayResults.Versions.FirstOrDefault(v=>v.Identity.VersionId==overlayResults.SelectedVersionId&&v.Failure!=null)??overlayResults.LastFailure;
    private double FailureNoticeHeight=>FailureNoticeVersion==null?0:32;
    private void RefreshFailureNotice()
    {
        var failed=FailureNoticeVersion;
        string text=failed==null?"":FailureSummary(failed);
        failureNotice.Content=failed==null?"":overlayResults.SelectedVersionId==failed.Identity.VersionId?$"未完成 · {ShortFailureLabel(failed.Failure!.Value)} · 状态/恢复":"新尝试未完成 · 正文为旧结果 · 查看";
        failureNotice.ToolTip=text;System.Windows.Automation.AutomationProperties.SetName(failureNotice,text);
    }
    private string FailureSummary(CachedOverlayVersion failed)
    {
        var snapshot=translation?.SnapshotForVersion(failed.Identity.VersionId);
        return $"{(overlayResults.Identity is {} current&&current.SessionId!=failed.Identity.SessionId?"另一来源尝试 · ":"")}版本 {failed.Number} · {(snapshot==null?"文字尝试":SourceLabel(snapshot))}：{FailureLabel(failed.Failure!.Value)}。"+
            (failed.Text.Length>0?"此失败版本保留部分结果，未完成。":"此失败版本没有可显示正文，未完成。")+
            (overlayResults.SelectedVersionId!=failed.Identity.VersionId?"当前显示旧结果，此原因属于上述失败尝试。":"")+
            "打开状态/恢复，在本次会话版本中查看失败或旧版本；可明确重新生成，或打开设置检查 API。不会自动重发或增加输出限制。";
    }
    private void ShowFailureRecovery()=>OpenOverflowMenu(failureNotice);
    private void SelectResultVersion(Guid id)
    {if(overlayResults.SelectVersion(id)){RefreshResultLabel();resultRevision++;ResetBodyReaderFollow();dirty=true;pendingReveal=false;peek=false;OpenBody();}}
    private static string FailureLabel(string reason)=>FailureLabel(TranslationFailures.Normalize(reason));
    private static string FailureLabel(TranslationFailureReason reason)=>reason switch
    {
        TranslationFailureReason.OutputTruncated=>"达到模型输出限制，结果可能不完整；可缩小原文或检查 API 输出限制后手动重新生成",
        TranslationFailureReason.ContentRefused=>"模型未提供该内容的翻译",
        TranslationFailureReason.Interrupted=>"生成中断，已收到的正文仅为部分结果",
        TranslationFailureReason.RequestTimeout=>"总时限已到",TranslationFailureReason.ConnectTimeout=>"连接超时",
        TranslationFailureReason.HeadersTimeout=>"响应头超时",TranslationFailureReason.FirstResponseTimeout=>"首有效响应超时",TranslationFailureReason.IdleTimeout=>"有效内容读取停滞",
        TranslationFailureReason.Authentication=>"密钥不可用或没有访问权限，请检查 API 设置",
        TranslationFailureReason.Endpoint=>"服务地址、模型或参数配置不可用，请检查 API 设置",
        TranslationFailureReason.RateLimited=>"服务暂时限流，请稍后手动重试",
        TranslationFailureReason.Network=>"无法连接服务",TranslationFailureReason.Tls=>"证书校验失败，请修正证书配置",
        TranslationFailureReason.InvalidProtocol=>"服务响应格式无效",TranslationFailureReason.ProviderError=>"服务未完成请求",
        TranslationFailureReason.LocalOutputLimit=>"输出达到本地安全上限",TranslationFailureReason.SourceOrSettingsChanged=>"来源或授权已改变",
        TranslationFailureReason.UnsupportedResponse=>"服务未提供可显示的文字响应",TranslationFailureReason.LocalBoundary=>"测试配置越出本地授权边界，已拒绝发送",
        _=>"请求未完成，请检查 API 设置"
    };
    private static string ShortFailureLabel(TranslationFailureReason reason)=>reason switch
    {TranslationFailureReason.OutputTruncated=>"输出达上限",TranslationFailureReason.ContentRefused=>"未提供翻译",TranslationFailureReason.Interrupted=>"生成中断",_=>"结果不完整"};
}
