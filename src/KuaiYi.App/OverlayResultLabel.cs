using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private void RefreshResultLabel()
    {
        var selection=SelectedSource;var snapshot=selection.Text;var selected=overlayResults.Versions.FirstOrDefault(v=>v.Identity.VersionId==overlayResults.SelectedVersionId);
        string state=selected?.Failure!=null?"未完成":selected?.Stopped==true?"已停止":selected?.Complete==true?"已完成":"生成中";
        resultLabel.Text=snapshot==null?"":$"{(snapshot.Transport.IsDevelopmentFixture?"本地测试输出 · ":snapshot.Transport.BaseUrl.Scheme=="http"?"非加密 HTTP · ":"")}{state} · {SourceLabel(snapshot)} · {StyleLabel(snapshot)} · 版本 {snapshot.VersionNumber} · {snapshot.Profile.Model}";
        resultLabel.ToolTip=snapshot==null?null:$"此版本快照 · {snapshot.FullTemplateVersion} · {new EndpointOrigin(snapshot.Transport.ResolveEndpoint()).Value}";
        if(snapshot?.Source.SourceKind==SelectionSourceKind.OcrOnly)resultLabel.ToolTip+="\n当前模型不支持图像理解，图形关系未分析";
        if(snapshot!=null)
        {
            string counts=UsageLabel(selected?.Usage);
            resultLabel.Text+=" · "+counts;
            resultLabel.ToolTip+="\n此版本服务端报告 · "+counts+"\nUnknown（未知）表示服务端未报告有效数值；这是请求元数据，不是费用或完整计费凭证。";
        }
        if(LocalImagePreview&&selection.Image is {} image){resultLabel.Text=$"{(image.Kind==KuaiYi.Imaging.ImageSourceKind.Browser?"浏览器图片 · 本地接收":image.Kind==KuaiYi.Imaging.ImageSourceKind.Bitmap?"投放位图":image.Kind==KuaiYi.Imaging.ImageSourceKind.LocalFile?"文件图片":"本地图片")}{(image.Notice is {} frame?" · "+frame:"")} · 未翻译 · {image.Width} × {image.Height}"+(SelectedScreenshotPreset is {} preset?$" · 本次风格：{StyleLabel(preset)}":"");resultLabel.ToolTip=image.Kind==KuaiYi.Imaging.ImageSourceKind.Browser?"只保留本次明确接收的归一化PNG；未翻译、未发送，不持有浏览器Cookie或页面。":image.Kind==KuaiYi.Imaging.ImageSourceKind.Bitmap?"仅保留本次投放位图的归一化图片；未发送，无文件路径或桌面坐标证明。":image.Kind==KuaiYi.Imaging.ImageSourceKind.LocalFile?"仅保留所选文件的归一化图片；未翻译、未发送，不保留文件路径。":"仅保留本次裁剪图；本地预览尚未翻译、未发送。";}
        if(selection.Generation is {} generated)
        {
            resultLabel.Text=$"{state} · {(generated.SourceKind==KuaiYi.Imaging.ImageSourceKind.Browser?"浏览器图片":generated.SourceKind==KuaiYi.Imaging.ImageSourceKind.Bitmap?"投放位图":generated.SourceKind==KuaiYi.Imaging.ImageSourceKind.LocalFile?"文件图片":"截图")}{(generated.SourceNotice is {} sourceNotice?" · "+sourceNotice:"")} · 版本 {generated.Number} · {StyleLabel(generated.Plan?.Preset??imageTranslation?.PresetForVersion(selection.VersionId!.Value)??generated.Settings.Data.Translation.Preset)} · {generated.Profile.Model}"+(generated.Plan?.Notice is {Length:>0} note?" · "+note:"");
            resultLabel.Text+=generated.Ocr?.UsedForText==true?" · 已批准 OCR 文字":generated.VisionContinuation!=null?" · 使用视觉继续 · 未使用 OCR 文字":"";
            resultLabel.ToolTip=$"此版本图片快照 · {new EndpointOrigin(generated.Transport.ResolveEndpoint()).Value}\n{ScreenshotUsageLabel(generated)}\n{generated.Plan?.Notice}\n仅显示服务端报告，不相加或估算费用；当前图片复用未启用。";
        }
        RefreshFailureNotice();
        var next=SelectedNextGeneration;var screenshotPreset=SelectedScreenshotPreset;var imageNext=SelectedImageRegenerationPreview;
        if(styleTool!=null)
        {
            var actualStyle=selection.Generation is {} actualImage?actualImage.Plan?.Preset??imageTranslation?.PresetForVersion(selection.VersionId!.Value)??actualImage.Settings.Data.Translation.Preset:snapshot?.Preset??screenshotPreset;
            styleTool.IsEnabled=next!=null||imageNext!=null||screenshotPreset!=null;
            styleTool.Content=actualStyle switch{TranslationPreset.Faithful=>"忠实",TranslationPreset.Plain=>"通俗",TranslationPreset.Professional=>"专业",TranslationPreset.Explain=>"解释",_=>"风格"};
            System.Windows.Automation.AutomationProperties.SetName(styleTool,actualStyle is {} used?$"本版风格：{StyleLabel(used)}；选择下次生成选项":"会话风格");
            styleTool.ToolTip=screenshotPreset is {} imageStyle?$"本次图片文字风格：{StyleLabel(imageStyle)}；按图文解释使用解释风格":imageNext!=null?$"下次图片：版本 {imageNext.VersionNumber} · {StyleLabel(imageNext.Preset)}；仅调整下次文字风格，图文解释仍用解释风格。"+(ImageRegenerationQualified?"":"重新生成尚未开放。"):next==null?"无保留会话":$"下次生成：版本 {next.VersionNumber} · {StyleLabel(next.Preset)}；不改变已有版本";
        }
        foreach(var tool in tools)if(Equals(tool.Tag,"重新生成")){tool.IsEnabled=lifecycle.Gate.Snapshot.Activation==Activation.On&&(snapshot!=null||CanRegenerateImage);tool.ToolTip=selection.Generation!=null&&!ImageRegenerationQualified?ImageRegenerationQualificationNotice:next==null?"无保留原文":$"重新生成：版本 {next.VersionNumber} · {StyleLabel(snapshot?.Preset)} · {next.Model}；沿本版快照；会话待选项仅影响“按下次选项生成”";}
        foreach(var tool in tools)if(Equals(tool.Tag,"回看")){tool.IsEnabled=CanReadOriginalSource()&&!lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.Capture)&&lifecycle.Gate.Snapshot.Presentation!=Presentation.Hidden&&(LocalImagePreview||overlayResults.HasText||selected?.Failure!=null);tool.ToolTip=tool.IsEnabled?"回看当前缓存；不读取选区或剪贴板，不发送请求":"尚无可回看的当前内容";}
        foreach(var tool in tools)if(Equals(tool.Tag,"复制"))tool.IsEnabled=CopyVersion()!=null;
        foreach(var tool in tools)if(Equals(tool.Tag,"翻译截图")){tool.Content=selection.Image is {Kind:not KuaiYi.Imaging.ImageSourceKind.Screenshot}?"翻译图片":"翻译截图";tool.IsEnabled=CanTranslateScreenshot;System.Windows.Automation.AutomationProperties.SetName(tool,tool.Content.ToString()!);tool.ToolTip=tool.Content.ToString();}
        foreach(var tool in tools)if(Equals(tool.Tag,"查看原文"))tool.IsEnabled=CanReadOriginalSource()&&selection.HasSource;
        // Compact footer retains the complete provenance and reported usage in its disclosure.
        if(resultLabel.Text.Length>0)resultLabel.ToolTip=resultLabel.Text+"\n"+resultLabel.ToolTip;
        if(!LocalImagePreview&&selected!=null)
        {
            string source=snapshot!=null?SourceLabel(snapshot):selection.Generation?.SourceKind switch
            {KuaiYi.Imaging.ImageSourceKind.Screenshot=>"截图",KuaiYi.Imaging.ImageSourceKind.Browser=>"浏览器图片",KuaiYi.Imaging.ImageSourceKind.Bitmap=>"投放位图",KuaiYi.Imaging.ImageSourceKind.LocalFile=>"文件图片",_=>"图片"};
            resultLabel.Text=$"{state} · {source} · 仅内存"+(snapshot?.Source.SourceKind==SelectionSourceKind.OcrOnly?" · 图形关系未分析":"")+
                (selection.Generation?.Plan?.Notice is {Length:>0} notice?" · "+notice:"");
        }
        versionsTool.Content=selected==null?"本地图片":$"版本 {selected.Number} ⌄";
        versionsTool.ToolTip=$"当前来源保留 {overlayResults.Versions.Count} 个内存版本；版本号为创建序号。切换不发送请求。";
        versionsTool.IsEnabled=overlayResults.Versions.Count>0||LocalImagePreview;
        RefreshCopyFeedback();
    }
    private string VersionMenuLabel(CachedOverlayVersion version)
    {
        var image=imageTranslation?.SnapshotForVersion(version.Identity.VersionId);
        string? model=translation?.SnapshotForVersion(version.Identity.VersionId)?.Profile.Model??image?.Profile.Model;
        return $"版本 {version.Number} · {(version.Failure!=null?"失败":version.Complete?"已完成":version.Stopped?"已停止":"生成中")}\n{StyleLabel(image?.Plan?.Preset??imageTranslation?.PresetForVersion(version.Identity.VersionId)??translation?.SnapshotForVersion(version.Identity.VersionId)?.Preset)} · {model??"模型未知"}"+
            (image?.Plan?.Notice is {Length:>0} notice?"\n"+notice:"")+(image==null?"":"\n"+ScreenshotUsageLabel(image));
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
