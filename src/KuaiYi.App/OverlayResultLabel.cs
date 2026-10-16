using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private void RefreshResultLabel()
    {
        var selection=SelectedSource;var snapshot=selection.Text;var selected=overlayResults.Versions.FirstOrDefault(v=>v.Identity.VersionId==overlayResults.SelectedVersionId);
        resultLabel.Text=snapshot==null?"":$"{(snapshot.Transport.IsDevelopmentFixture?"本地测试输出 · ":snapshot.Transport.BaseUrl.Scheme=="http"?"非加密 HTTP · ":"")}{(selected?.Complete==true||selected?.Stopped==true?"版本":"生成版本")} {snapshot.VersionNumber} · {StyleLabel(snapshot)} · {SourceLabel(snapshot)} · {snapshot.Profile.Model}";
        resultLabel.ToolTip=snapshot==null?null:$"此版本快照 · {snapshot.FullTemplateVersion} · {new EndpointOrigin(snapshot.Transport.ResolveEndpoint()).Value}";
        if(snapshot?.Source.SourceKind==SelectionSourceKind.OcrOnly)resultLabel.ToolTip+="\n当前模型不支持图像理解，图形关系未分析";
        if(snapshot!=null)
        {
            string counts=UsageLabel(selected?.Usage);
            resultLabel.Text+=" · "+counts+(selected?.Failure!=null?" · 未完成":selected?.Stopped==true?" · 已停止":"");
            resultLabel.ToolTip+="\n此版本服务端报告 · "+counts+"\nUnknown（未知）表示服务端未报告有效数值；这是请求元数据，不是费用或完整计费凭证。";
        }
        if(selection.Image is {} image){resultLabel.Text=$"本地图片 · 未翻译 · {image.Width} × {image.Height}";resultLabel.ToolTip="仅保留本次裁剪图；本地预览尚未翻译、未发送。";}
        RefreshFailureNotice();
        var next=SelectedNextGeneration;if(styleTool!=null){styleTool.IsEnabled=next!=null;styleTool.Content=next?.Preset switch{TranslationPreset.Faithful=>"忠实",TranslationPreset.Plain=>"通俗",TranslationPreset.Professional=>"专业",TranslationPreset.Explain=>"解释",_=>"风格"};System.Windows.Automation.AutomationProperties.SetName(styleTool,next==null?"会话风格":$"下次生成风格：{StyleLabel(next.Preset)}");styleTool.ToolTip=next==null?"无保留会话":$"下次生成：版本 {next.VersionNumber} · {StyleLabel(next.Preset)}；不改变已有版本";}
        foreach(var tool in tools)if(Equals(tool.Content,"重新生成")){tool.IsEnabled=lifecycle.Gate.Snapshot.Activation==Activation.On&&snapshot!=null;tool.ToolTip=next==null?"无保留原文":$"重新生成：版本 {next.VersionNumber} · {StyleLabel(next.Preset)} · {next.Model}";}
        foreach(var tool in tools)if(Equals(tool.Content,"复制"))tool.IsEnabled=selection.Image==null&&overlayResults.HasText;
        foreach(var tool in tools)if(Equals(tool.Content,"查看原文"))tool.IsEnabled=CanReadOriginalSource()&&selection.HasSource;
    }
    private string VersionMenuLabel(CachedOverlayVersion version)=>$"{(overlayResults.Identity is {} current&&current.SessionId!=version.Identity.SessionId?"另一来源 · ":"")}版本 {version.Number} · {StyleLabel(translation?.SnapshotForVersion(version.Identity.VersionId)?.Preset)} · {(version.Failure is {} reason?"未完成 · "+FailureLabel(reason):version.Complete?"完成":version.Stopped?"已停止":"生成中")}\n服务端报告 · {UsageLabel(version.Usage)}"+(version.Failure!=null?"\n"+FailureSummary(version):"");
    private static string UsageLabel(TranslationUsage? value)=>$"输入 token：{UsageCount(value?.InputTokens)} · 输出 token：{UsageCount(value?.OutputTokens)}";
    private static string UsageCount(long? value)=>value?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"Unknown（未知）";
    private static string StyleLabel(TextTranslationSnapshot value)=>StyleLabel(value.Preset);
    private static string StyleLabel(TranslationPreset? preset)=>preset switch{TranslationPreset.Faithful=>"忠实翻译",TranslationPreset.Plain=>"通俗翻译",TranslationPreset.Professional=>"专业翻译",TranslationPreset.Explain=>"解释",_=>"公开探针"};
    private static string SourceLabel(TextTranslationSnapshot value)=>value.Source.SourceKind switch{SelectionSourceKind.Clipboard=>"复制兼容",SelectionSourceKind.OleDrop=>"拖放文字",SelectionSourceKind.OcrOnly=>"OCR-only · 图形关系未分析",_=>"选区"};
}
