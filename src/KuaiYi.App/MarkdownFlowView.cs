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
    private readonly Dictionary<Block,MarkdownCodeRegion> codeRegions=[];
    private readonly Action? observeReader;
    private readonly Func<Func<bool>>? captureReaderGuard;
    private Guid? appliedVersion;
    private MarkdownViewport viewport=new(388,560);
    private double appliedFontSize=double.NaN;
    private static readonly FontFamily ProseFont=new("Segoe UI"),CodeFont=new("Consolas");
    public MarkdownFlowView(RichTextBox host,Action? observeReader=null,Func<Func<bool>>? captureReaderGuard=null)
    {
        this.host=host;this.observeReader=observeReader;this.captureReaderGuard=captureReaderGuard;host.Document=new FlowDocument{PagePadding=new Thickness(0),ColumnGap=0};
        host.IsReadOnly=true;host.IsUndoEnabled=false;host.IsDocumentEnabled=true;host.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;host.VerticalScrollBarVisibility=ScrollBarVisibility.Visible;host.ClipToBounds=true;
        SetViewport(viewport);
    }
    public void Clear(){foreach(var region in codeRegions.Values)region.Dispose();codeRegions.Clear();host.Document.Blocks.Clear();applied.Clear();elements.Clear();appliedVersion=null;}
    public void SetViewport(MarkdownViewport value)
    {
        value.Validate();viewport=value;host.Width=host.MaxWidth=value.WidthDip;host.MaxHeight=value.MaximumHeightDip;
        double contentWidth=Math.Max(1,value.WidthDip-SystemParameters.VerticalScrollBarWidth);
        host.Document.PageWidth=host.Document.ColumnWidth=contentWidth;
        foreach(var region in codeRegions.Values)region.Constrain(contentWidth,value.CodeMaximumHeightDip);
    }
    public void RefreshCodeAppearance()
    {foreach(var region in codeRegions.Values){region.View.FontSize=host.FontSize;region.View.Foreground=host.Foreground;}}
    public void Apply(IReadOnlyList<MarkdownBlock> blocks,double fontSize,MarkdownViewport viewport,Guid? selectedVersion)
    {
        SetViewport(viewport);
        using var change=host.DeclareChangeBlock();
        int keep=0;while(keep<applied.Count&&keep<blocks.Count&&ReferenceEquals(applied[keep],blocks[keep]))keep++;
        // The open fenced-code tail can grow without replacing its selectable native region.
        if(appliedVersion==selectedVersion&&keep<applied.Count&&keep<blocks.Count&&applied[keep].Kind==MarkdownBlockKind.Code&&blocks[keep].Kind==MarkdownBlockKind.Code&&blocks[keep].Source.StartsWith(applied[keep].Source,StringComparison.Ordinal)&&codeRegions.TryGetValue(elements[keep],out var growing))
        {growing.UpdateText(blocks[keep].Text);applied[keep]=blocks[keep];keep++;}
        appliedVersion=selectedVersion;
        for(int i=elements.Count-1;i>=keep;i--){if(codeRegions.Remove(elements[i],out var region))region.Dispose();host.Document.Blocks.Remove(elements[i]);elements.RemoveAt(i);applied.RemoveAt(i);}
        for(int i=keep;i<blocks.Count;i++)
        {
            var data=blocks[i];Block element;
            if(data.Kind==MarkdownBlockKind.Code)
            {
                var region=new MarkdownCodeRegion(data.Text,observeReader,captureReaderGuard);region.Constrain(host.Document.PageWidth,viewport.CodeMaximumHeightDip);
                element=new BlockUIContainer(region.View){Margin=new Thickness(0)};codeRegions.Add(element,region);
            }
            else if(data.Table is {} table)element=CreateTable(table);
            else
            {
                var paragraph=CreateParagraph(data.Text,data.Inlines);element=paragraph;
                if(data.Kind is MarkdownBlockKind.Bullet or MarkdownBlockKind.Numbered)
                    element=new System.Windows.Documents.List(new ListItem(paragraph)){MarkerStyle=data.Kind==MarkdownBlockKind.Numbered?TextMarkerStyle.Decimal:TextMarkerStyle.Disc,StartIndex=data.Number,Margin=new Thickness(20,0,0,0),Padding=new Thickness(0)};
            }
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
        RefreshCodeAppearance();
    }
    private static Paragraph CreateParagraph(string text,IReadOnlyList<MarkdownInline> inlines)
    {
        var paragraph=new Paragraph{Margin=new Thickness(0)};
        if(inlines.Count==0)paragraph.Inlines.Add(new Run(text));
        else foreach(var part in inlines)
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
        return paragraph;
    }
    private static Table CreateTable(MarkdownTable data)
    {
        var table=new Table{CellSpacing=4,Margin=new Thickness(0)};var group=new TableRowGroup();table.RowGroups.Add(group);
        foreach(var _ in data.Alignments)table.Columns.Add(new TableColumn());
        for(int index=0;index<data.Rows.Count;index++)
        {
            var row=new TableRow();group.Rows.Add(row);
            for(int column=0;column<data.Alignments.Count;column++)
            {
                var cell=data.Rows[index].Cells[column];var paragraph=CreateParagraph(cell.Text,cell.Inlines);
                paragraph.TextAlignment=data.Alignments[column] switch{MarkdownTableAlignment.Center=>TextAlignment.Center,MarkdownTableAlignment.Right=>TextAlignment.Right,_=>TextAlignment.Left};
                if(index==0)paragraph.FontWeight=FontWeights.SemiBold;
                row.Cells.Add(new TableCell(paragraph){Padding=new Thickness(4,2,4,2)});
            }
        }
        return table;
    }
}
