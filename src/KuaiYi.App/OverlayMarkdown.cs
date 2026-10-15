using KuaiYi.Core;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private readonly RestrictedMarkdown markdown=new();
    private MarkdownFlowView bodyMarkdown=null!,measurementMarkdown=null!;
    private void RefreshMarkdown(bool renderBody)
    {
        var selected=overlayResults.Versions.FirstOrDefault(v=>v.Identity.VersionId==overlayResults.SelectedVersionId);
        markdown.Update(overlayResults.SelectedVersionId,overlayResults.Text,selected?.Complete==true||selected?.Stopped==true);
        double size=lifecycle.Settings?.Data.Overlay.FontSizeDip??15;
        measurementMarkdown.Apply(markdown.Blocks,size);
        if(renderBody)bodyMarkdown.Apply(markdown.Blocks,size);
    }
    private void ClearMarkdown(){markdown.Clear();bodyMarkdown.Clear();measurementMarkdown.Clear();}
}
