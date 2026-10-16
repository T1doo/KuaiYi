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
        RefreshResultLabel();if(update.Failure!=null&&overlayResults.LastFailure is {} failed)translationStatus=FailureSummary(failed);button.ToolTip=update.Presentation.Kind==OverlayResultEventKind.Begin?connectionStatus.Text:translationStatus;
    }
}
