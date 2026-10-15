using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KuaiYi.Core;
namespace KuaiYi.App;

/// <summary>Only locally constructed native text elements; never XAML, HTML, resources or hyperlinks.</summary>
internal sealed class MarkdownFlowView
{
    private readonly RichTextBox host;
    private readonly List<MarkdownBlock> applied=[];
    private readonly List<Block> elements=[];
    private double appliedFontSize=double.NaN;
    private static readonly FontFamily ProseFont=new("Segoe UI"),CodeFont=new("Consolas");
    public MarkdownFlowView(RichTextBox host)
    {this.host=host;host.Document=new FlowDocument{PagePadding=new Thickness(0)};}
    public void Clear(){host.Document.Blocks.Clear();applied.Clear();elements.Clear();}
    public void Apply(IReadOnlyList<MarkdownBlock> blocks,double fontSize)
    {
        using var change=host.DeclareChangeBlock();
        int keep=0;while(keep<applied.Count&&keep<blocks.Count&&ReferenceEquals(applied[keep],blocks[keep]))keep++;
        for(int i=elements.Count-1;i>=keep;i--){host.Document.Blocks.Remove(elements[i]);elements.RemoveAt(i);applied.RemoveAt(i);}
        for(int i=keep;i<blocks.Count;i++)
        {
            var data=blocks[i];var paragraph=new Paragraph{Margin=new Thickness(0)};
            if(data.Inlines.Count==0)paragraph.Inlines.Add(new Run(data.Text));
            else foreach(var part in data.Inlines)
            {
                var run=new Run(part.Text);
                if(part.Kind==MarkdownInlineKind.Text)paragraph.Inlines.Add(run);
                else
                {
                    var span=new Span(run);
                    if(part.Kind==MarkdownInlineKind.Code){span.FontFamily=CodeFont;span.FontWeight=FontWeights.Normal;span.FontStyle=FontStyles.Normal;}
                    else if(part.Kind==MarkdownInlineKind.Bold)span.FontWeight=FontWeights.Bold;
                    else if(part.Kind==MarkdownInlineKind.Italic)span.FontStyle=FontStyles.Italic;
                    paragraph.Inlines.Add(span);
                }
            }
            Block element=paragraph;
            if(data.Kind is MarkdownBlockKind.Bullet or MarkdownBlockKind.Numbered)
                element=new System.Windows.Documents.List(new ListItem(paragraph)){MarkerStyle=data.Kind==MarkdownBlockKind.Numbered?TextMarkerStyle.Decimal:TextMarkerStyle.Disc,StartIndex=data.Number,Margin=new Thickness(20,0,0,0),Padding=new Thickness(0)};
            applied.Add(data);elements.Add(element);host.Document.Blocks.Add(element);
        }
        int styleStart=fontSize==appliedFontSize?keep:0;
        if(fontSize!=appliedFontSize)host.FontSize=host.Document.FontSize=fontSize;
        appliedFontSize=fontSize;
        for(int i=styleStart;i<elements.Count;i++)
        {
            var data=applied[i];Block element=elements[i];
            double size=data.Kind==MarkdownBlockKind.Heading?fontSize*(1.6-.1*data.Level):fontSize;
            element.FontSize=size;element.LineHeight=size*1.55;
            element.FontWeight=data.Kind==MarkdownBlockKind.Heading?FontWeights.SemiBold:FontWeights.Normal;
            element.FontFamily=data.Kind==MarkdownBlockKind.Code?CodeFont:ProseFont;
        }
    }
}
