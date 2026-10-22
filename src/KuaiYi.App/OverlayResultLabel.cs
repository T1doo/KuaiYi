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
        if(LocalImagePreview&&selection.Image is {} image){resultLabel.Text=$"{(image.Kind==KuaiYi.Imaging.ImageSourceKind.Browser?"浏览器图片 · 本地接收":image.Kind==KuaiYi.Imaging.ImageSourceKind.Bitmap?"投放位图":image.Kind==KuaiYi.Imaging.ImageSourceKind.LocalFile?"文件图片":"本地图片")}{(image.Notice is {} frame?" · "+frame:"")} · 未翻译 · {image.Width} × {image.Height}"+(SelectedScreenshotPreset is {} preset?$" · 本次风格：{StyleLabel(preset)}":"");resultLabel.ToolTip=image.Kind==KuaiYi.Imaging.ImageSourceKind.Browser?"只保留本次明确接收的归一化PNG；未翻译、未发送，不持有浏览器Cookie或页面。":image.Kind==KuaiYi.Imaging.ImageSourceKind.Bitmap?"仅保留本次投放位图的归一化图片；未发送，无文件路径或桌面坐标证明。":image.Kind==KuaiYi.Imaging.ImageSourceKind.LocalFile?"仅保留所选文件的归一化图片；未翻译、未发送，不保留文件路径。":"仅保留本次裁剪图；本地预览尚未翻译、未发送。";}
        if(selection.Generation is {} generated)
        {
            resultLabel.Text=$"{(generated.SourceKind==KuaiYi.Imaging.ImageSourceKind.Browser?"浏览器图片":generated.SourceKind==KuaiYi.Imaging.ImageSourceKind.Bitmap?"投放位图":generated.SourceKind==KuaiYi.Imaging.ImageSourceKind.LocalFile?"文件图片":"截图")}{(generated.SourceNotice is {} sourceNotice?" · "+sourceNotice:"")} · 版本 {generated.Number} · {StyleLabel(generated.Plan?.Preset??imageTranslation?.PresetForVersion(selection.VersionId!.Value)??generated.Settings.Data.Translation.Preset)} · {generated.Profile.Model}"+(generated.Plan?.Notice is {Length:>0} note?" · "+note:"")+(selected?.Failure!=null?" · 未完成":selected?.Stopped==true?" · 已停止":"");
            resultLabel.Text+=generated.Ocr?.UsedForText==true?" · 已批准 OCR 文字":"";
            resultLabel.ToolTip=$"此版本图片快照 · {new EndpointOrigin(generated.Transport.ResolveEndpoint()).Value}\n{ScreenshotUsageLabel(generated)}\n{generated.Plan?.Notice}\n仅显示服务端报告，不相加或估算费用；当前图片复用未启用。";
        }
        RefreshFailureNotice();
        var next=SelectedNextGeneration;var screenshotPreset=SelectedScreenshotPreset;if(styleTool!=null){styleTool.IsEnabled=next!=null||screenshotPreset!=null;styleTool.Content=(screenshotPreset??next?.Preset) switch{TranslationPreset.Faithful=>"忠实",TranslationPreset.Plain=>"通俗",TranslationPreset.Professional=>"专业",TranslationPreset.Explain=>"解释",_=>"风格"};System.Windows.Automation.AutomationProperties.SetName(styleTool,screenshotPreset is {} style?$"本次图片风格：{StyleLabel(style)}":next==null?"会话风格":$"下次生成风格：{StyleLabel(next.Preset)}");styleTool.ToolTip=screenshotPreset is {} imageStyle?$"本次图片文字风格：{StyleLabel(imageStyle)}；按图文解释使用解释风格":next==null?"无保留会话":$"下次生成：版本 {next.VersionNumber} · {StyleLabel(next.Preset)}；不改变已有版本";}
        foreach(var tool in tools)if(Equals(tool.Content,"重新生成")){tool.IsEnabled=lifecycle.Gate.Snapshot.Activation==Activation.On&&snapshot!=null;tool.ToolTip=next==null?"无保留原文":$"重新生成：版本 {next.VersionNumber} · {StyleLabel(next.Preset)} · {next.Model}";}
        foreach(var tool in tools)if(Equals(tool.Content,"复制"))tool.IsEnabled=!LocalImagePreview&&overlayResults.HasText;
        foreach(var tool in tools)if(Equals(tool.Tag,"翻译截图")||Equals(tool.Content,"翻译截图")){tool.Content=selection.Image is {Kind:not KuaiYi.Imaging.ImageSourceKind.Screenshot}?"翻译图片":"翻译截图";tool.IsEnabled=CanTranslateScreenshot;System.Windows.Automation.AutomationProperties.SetName(tool,tool.Content.ToString()!);}
        foreach(var tool in tools)if(Equals(tool.Content,"查看原文"))tool.IsEnabled=CanReadOriginalSource()&&selection.HasSource;
    }
    private string VersionMenuLabel(CachedOverlayVersion version)
    {
        var image=imageTranslation?.SnapshotForVersion(version.Identity.VersionId);
        var ocr=translation?.SnapshotForVersion(version.Identity.VersionId)?.Source.Ocr;
        string counts=image==null?UsageLabel(version.Usage):ScreenshotUsageLabel(image);
        return $"{OcrSourcePrefix(ocr)}{(image?.SourceKind==KuaiYi.Imaging.ImageSourceKind.Browser?"浏览器图片 · ":"")}{(ocr?.UsedFirstFrame==true?"使用首帧 · ":"")}{(image?.SourceKind==KuaiYi.Imaging.ImageSourceKind.Bitmap?"投放位图 · ":image?.SourceKind==KuaiYi.Imaging.ImageSourceKind.LocalFile?"文件图片 · ":"")}{(image?.SourceNotice is {} frameNote?frameNote+" · ":"")}{(overlayResults.Identity is {} current&&current.SessionId!=version.Identity.SessionId?"另一来源 · ":"")}版本 {version.Number} · {StyleLabel(image?.Plan?.Preset??imageTranslation?.PresetForVersion(version.Identity.VersionId)??translation?.SnapshotForVersion(version.Identity.VersionId)?.Preset)} · {(version.Failure is {} reason?"未完成 · "+FailureLabel(reason):version.Complete?"完成":version.Stopped?"已停止":"生成中")}\n服务端报告 · {counts}"+(image?.Plan?.Notice is {Length:>0} note?"\n"+note:"")+(version.Failure!=null?"\n"+FailureSummary(version):"");
    }
    private static string ScreenshotUsageLabel(ScreenshotGenerationSnapshot value)=>$"分类：输入 {UsageCount(value.ClassificationUsage?.Tokens.InputTokens)} / 输出 {UsageCount(value.ClassificationUsage?.Tokens.OutputTokens)} token\n生成：输入 {UsageCount(value.GenerationUsage?.Tokens.InputTokens)} / 输出 {UsageCount(value.GenerationUsage?.Tokens.OutputTokens)} token";
    private static string UsageLabel(TranslationUsage? value)=>$"输入 token：{UsageCount(value?.InputTokens)} · 输出 token：{UsageCount(value?.OutputTokens)}";
    private static string UsageCount(long? value)=>value?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"Unknown（未知）";
    private static string StyleLabel(TextTranslationSnapshot value)=>StyleLabel(value.Preset);
    private static string StyleLabel(TranslationPreset? preset)=>preset switch{TranslationPreset.Faithful=>"忠实翻译",TranslationPreset.Plain=>"通俗翻译",TranslationPreset.Professional=>"专业翻译",TranslationPreset.Explain=>"解释",_=>"公开探针"};
    private static string OcrSourcePrefix(OcrTextProvenance? source)=>source?.ImageKind switch
    {
        null or OcrImageSourceKind.Screenshot=>"",
        OcrImageSourceKind.LocalFile=>"文件图片 · ",
        OcrImageSourceKind.Browser=>"浏览器图片 · ",
        OcrImageSourceKind.Bitmap=>"投放位图 · ",
        _=>throw new InvalidOperationException("Unknown OCR source label.")
    };
    private static string SourceLabel(TextTranslationSnapshot value)=>value.Source.SourceKind switch{SelectionSourceKind.Clipboard=>"复制兼容",SelectionSourceKind.OleDrop=>"拖放文字",SelectionSourceKind.OcrOnly=>OcrSourcePrefix(value.Source.Ocr)+(value.Source.Ocr?.UsedFirstFrame==true?"使用首帧 · ":"")+"OCR-only · 图形关系未分析",_=>"选区"};
}
