using KuaiYi.Core;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private readonly RestrictedMarkdown markdown=new();
    private MarkdownFlowView bodyMarkdown=null!,measurementMarkdown=null!;
    private MarkdownViewport markdownViewport=new(388,560);
    private void SetMarkdownViewport(MarkdownViewport viewport)
    {viewport.Validate();markdownViewport=viewport;bodyMarkdown.SetViewport(viewport);measurementMarkdown.SetViewport(viewport);}
    private double MeasureMarkdown(LayoutPlan plan,bool hasLabel)
    {
        SetMarkdownViewport(MarkdownViewport.FromPlan(plan,hasLabel));RefreshMarkdown(renderBody:false);
        measurement.Measure(new System.Windows.Size(markdownViewport.WidthDip,markdownViewport.MaximumHeightDip));
        return measurement.DesiredSize.Height;
    }
    private void RefreshMarkdown(bool renderBody)
    {
        var selected=overlayResults.Versions.FirstOrDefault(v=>v.Identity.VersionId==overlayResults.SelectedVersionId);
        markdown.Update(overlayResults.SelectedVersionId,overlayResults.Text,selected?.Complete==true||selected?.Stopped==true);
        double size=lifecycle.Settings?.Data.Overlay.FontSizeDip??15;
        measurementMarkdown.Apply(markdown.Blocks,size,markdownViewport,overlayResults.SelectedVersionId);
        bodyMarkdown.SetViewport(markdownViewport);
        if(renderBody)bodyMarkdown.Apply(markdown.Blocks,size,markdownViewport,overlayResults.SelectedVersionId);
    }
    private void ClearMarkdown(){ForgetCodeReaderIntent();markdown.Clear();bodyMarkdown.Clear();measurementMarkdown.Clear();}
}
