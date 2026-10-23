using KuaiYi.Core;
namespace KuaiYi.App;
// Optional internal probe data only; no text, source content, or layout mutation.
internal sealed record MarkdownMeasureState(bool IsMeasureValid,bool IsArrangeValid,double DesiredWidth,double DesiredHeight,
    double ExtentHeight,double ViewportHeight,int DocumentBlocks,int DocumentSymbols);
internal sealed record MarkdownMeasureObservation(Guid? VersionBefore,Guid? VersionAfter,int TextLengthBefore,int TextLengthAfter,
    MarkdownViewport Constraint,MarkdownMeasureState Before,MarkdownMeasureState After,bool CurrentAccepted);
internal sealed record OverlayMeasureObservation(long Qpc,MarkdownMeasureObservation Markdown,double ChromeHeightDip,
    int ReturnedBodyPixels,int PlanMaximumHeightPixels,int CalculatedClampedHeightPixels,double Dpi);
internal sealed partial class ShellWindow
{
    internal Action<OverlayMeasureObservation>? PresentationMeasureObservedForTest{get;set;}
    private readonly RestrictedMarkdown markdown=new();
    private MarkdownFlowView bodyMarkdown=null!,measurementMarkdown=null!;
    private MarkdownViewport markdownViewport=new(388,560);
    private void SetMarkdownViewport(MarkdownViewport viewport,Func<bool>? current=null)
    {if(current?.Invoke()==false)return;viewport.Validate();markdownViewport=viewport;bodyMarkdown.SetViewport(viewport,current);if(current?.Invoke()==false)return;measurementMarkdown.SetViewport(viewport,current);}
    private double MeasureMarkdown(LayoutPlan plan,bool hasLabel,Func<bool>? current=null,Action<MarkdownMeasureObservation>? observe=null)
    {
        if(current?.Invoke()==false)return 0;SetMarkdownViewport(MarkdownViewport.FromPlan(plan,hasLabel,FailureNoticeHeight),current);if(current?.Invoke()==false)return 0;RefreshMarkdown(renderBody:false,current);if(current?.Invoke()==false)return 0;
        var before=observe==null?null:MeasureState();var version=observe==null?null:SelectedSource.VersionId;
        int length=observe==null?0:overlayResults.Text.Length;var constraint=markdownViewport;
        measurement.Measure(new System.Windows.Size(markdownViewport.WidthDip,markdownViewport.MaximumHeightDip));
        bool accepted=current?.Invoke()!=false;double height=accepted?measurement.DesiredSize.Height:0;
        if(observe!=null)observe(new(version,SelectedSource.VersionId,length,overlayResults.Text.Length,constraint,before!,MeasureState(),accepted));
        return height;
    }
    private MarkdownMeasureState MeasureState()=>new(measurement.IsMeasureValid,measurement.IsArrangeValid,
        measurement.DesiredSize.Width,measurement.DesiredSize.Height,measurement.ExtentHeight,measurement.ViewportHeight,
        measurement.Document.Blocks.Count,measurement.Document.ContentStart.GetOffsetToPosition(measurement.Document.ContentEnd));
    private void RefreshMarkdown(bool renderBody,Func<bool>? current=null)
    {
        if(current?.Invoke()==false)return;var selection=SelectedSource;
        if(current!=null&&selection.Image is {} local&&selection.VersionId is {} version&&LocalImagePreview)
        {
            double localSize=lifecycle.Settings?.Data.Overlay.FontSizeDip??15;
            measurementMarkdown.ShowLocalImage(version,local,localSize,markdownViewport,current);if(!current())return;
            if(renderBody)bodyMarkdown.ShowLocalImage(version,local,localSize,markdownViewport,current);return;
        }
        var selected=overlayResults.Versions.FirstOrDefault(v=>v.Identity.VersionId==selection.VersionId);
        if(selection.Image is {} image&&CanReadOriginalSource())
        {
            bodyMarkdown.SetImageSource(selection.VersionId,image,current:current);if(current?.Invoke()==false)return;
            measurementMarkdown.SetImageSource(selection.VersionId,image,bodyMarkdown.SourceExpanded,materialize:false,current:current);if(current?.Invoke()==false)return;
        }
        else
        {
            var source=CanReadOriginalSource()?selection.Text?.Source.RawText:null;
            bodyMarkdown.SetSource(selection.VersionId,source);
            measurementMarkdown.SetSource(selection.VersionId,source,bodyMarkdown.SourceExpanded);
        }
        if(current?.Invoke()==false)return;markdown.Update(selection.VersionId,LocalImagePreview?"":overlayResults.Text,LocalImagePreview||selected?.Complete==true||selected?.Stopped==true);
        double size=lifecycle.Settings?.Data.Overlay.FontSizeDip??15;
        measurementMarkdown.Apply(markdown.Blocks,size,markdownViewport,selection.VersionId);
        if(current?.Invoke()==false)return;bodyMarkdown.SetViewport(markdownViewport);if(current?.Invoke()==false)return;
        if(renderBody)bodyMarkdown.Apply(markdown.Blocks,size,markdownViewport,selection.VersionId);
    }
    private void ClearMarkdown(){ForgetCodeReaderIntent();markdown.Clear();bodyMarkdown.Clear();measurementMarkdown.Clear();}
}
