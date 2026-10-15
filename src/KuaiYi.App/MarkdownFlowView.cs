using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KuaiYi.Core;
using KuaiYi.Imaging;
namespace KuaiYi.App;

/// <summary>Only locally constructed native text elements; never XAML, HTML, resources or hyperlinks.</summary>
internal sealed class MarkdownFlowView
{
    private readonly RichTextBox host;
    private readonly List<MarkdownBlock> applied=[];
    private readonly List<Block> elements=[];
    private readonly Dictionary<Block,MarkdownCodeRegion> codeRegions=[];
    private readonly Dictionary<Block,MarkdownTableRegion> tableRegions=[];
    private readonly Action? observeReader;
    private readonly Func<Func<bool>>? captureReaderGuard;
    private readonly Func<bool>? shouldFollow;
    private readonly Action? sourceChanged;
    private readonly Func<Guid,bool>? canReadSource;
    private readonly Action<Exception>? sourceFailed;
    private MarkdownSourceRegion? sourceRegion;
    private MarkdownImageSourceRegion? imageSourceRegion;
    private BlockUIContainer? sourceBlock;
    private Guid? sourceVersion;
    private Guid? appliedVersion;
    private MarkdownViewport viewport=new(388,560);
    private double appliedFontSize=double.NaN;
    private static readonly FontFamily ProseFont=new("Segoe UI"),CodeFont=new("Consolas");
    public MarkdownFlowView(RichTextBox host,Action? observeReader=null,Func<Func<bool>>? captureReaderGuard=null,Func<bool>? shouldFollow=null,Action? sourceChanged=null,Func<Guid,bool>? canReadSource=null,Action<Exception>? sourceFailed=null)
    {
        this.host=host;this.observeReader=observeReader;this.captureReaderGuard=captureReaderGuard;this.shouldFollow=shouldFollow;this.sourceChanged=sourceChanged;this.canReadSource=canReadSource;this.sourceFailed=sourceFailed;host.Document=new FlowDocument{PagePadding=new Thickness(0),ColumnGap=0};
        host.IsReadOnly=true;host.IsUndoEnabled=false;host.IsDocumentEnabled=true;host.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;host.VerticalScrollBarVisibility=ScrollBarVisibility.Visible;host.ClipToBounds=true;
        SetViewport(viewport);
    }
    public void Clear(){ClearSource();foreach(var region in codeRegions.Values)region.Dispose();foreach(var region in tableRegions.Values)region.Dispose();codeRegions.Clear();tableRegions.Clear();host.Document.Blocks.Clear();applied.Clear();elements.Clear();appliedVersion=null;}
    public bool SourceExpanded=>sourceRegion?.View.IsExpanded==true||imageSourceRegion?.View.IsExpanded==true;
    public void ExpandSource(){sourceRegion?.Expand();imageSourceRegion?.Expand();}
    public void ClearSource()
    {sourceRegion?.Dispose();imageSourceRegion?.Dispose();imageSourceRegion=null;if(sourceBlock!=null)host.Document.Blocks.Remove(sourceBlock);sourceRegion=null;sourceBlock=null;sourceVersion=null;}
    public void RetainSourceVersion(Guid? version){if(sourceVersion!=version)ClearSource();}
    public void SetSource(Guid? version,string? source,bool? expanded=null)
    {
        if(version==null||source==null){ClearSource();return;}
        if(sourceRegion==null||sourceVersion!=version||!sourceRegion.Matches(source))
        {
            ClearSource();sourceVersion=version;sourceRegion=new(source,observeReader,sourceChanged,()=>canReadSource?.Invoke(version.Value)!=false,captureReaderGuard);
            sourceBlock=new(sourceRegion.View){Margin=new Thickness(0,8,0,0)};host.Document.Blocks.Add(sourceBlock);
        }
        if(expanded.HasValue)sourceRegion.SetExpanded(expanded.Value);
        ConstrainSource();
    }
    public void SetImageSource(Guid? version,OwnedSourceImage? source,bool? expanded=null,bool materialize=true)
    {
        if(version==null||source==null||source.IsDisposed){ClearSource();return;}
        if(imageSourceRegion==null||sourceVersion!=version||!imageSourceRegion.Matches(source))
        {
            ClearSource();sourceVersion=version;imageSourceRegion=new(source,observeReader,sourceChanged,()=>canReadSource?.Invoke(version.Value)!=false,captureReaderGuard,sourceFailed);
            sourceBlock=new(imageSourceRegion.View){Margin=new Thickness(0,8,0,0)};host.Document.Blocks.Add(sourceBlock);
        }
        if(expanded.HasValue)imageSourceRegion.SetExpanded(expanded.Value,materialize);
        ConstrainSource();
    }
    private void ConstrainSource()
    {sourceRegion?.Constrain(host.Document.PageWidth,viewport.MaximumHeightDip,host.FontSize,host.Foreground);imageSourceRegion?.Constrain(host.Document.PageWidth,viewport.MaximumHeightDip,host.FontSize,host.Foreground);}
    public void SetViewport(MarkdownViewport value)
    {
        value.Validate();viewport=value;host.Width=host.MaxWidth=value.WidthDip;host.MaxHeight=value.MaximumHeightDip;
        double contentWidth=Math.Max(1,value.WidthDip-SystemParameters.VerticalScrollBarWidth);
        host.Document.PageWidth=host.Document.ColumnWidth=contentWidth;
        foreach(var region in codeRegions.Values)region.Constrain(contentWidth,value.CodeMaximumHeightDip);
        foreach(var region in tableRegions.Values)region.Constrain(contentWidth,value.TableMaximumHeightDip);
        ConstrainSource();
    }
    public void RefreshCodeAppearance()
    {
        foreach(var region in codeRegions.Values){region.View.FontSize=host.FontSize;region.View.Foreground=host.Foreground;}
        foreach(var region in tableRegions.Values)region.RefreshAppearance(host.FontSize,host.Foreground);
        ConstrainSource();
    }
    public void FollowTailEnd()
    {
        if(elements.Count==0)return;var tail=elements[^1];
        if(codeRegions.TryGetValue(tail,out var code))code.FollowEnd();
        if(tableRegions.TryGetValue(tail,out var table))table.FollowEnd();
    }
    public void Apply(IReadOnlyList<MarkdownBlock> blocks,double fontSize,MarkdownViewport viewport,Guid? selectedVersion)
    {
        SetViewport(viewport);
        using var change=host.DeclareChangeBlock();
        int keep=0;while(keep<applied.Count&&keep<blocks.Count&&ReferenceEquals(applied[keep],blocks[keep]))keep++;
        ConfigureTail(blocks.Count-1); // Incoming structure decides whether a retained region is the tail.
        // The open fenced-code tail can grow without replacing its selectable native region.
        if(appliedVersion==selectedVersion&&keep<applied.Count&&keep<blocks.Count&&applied[keep].Kind==MarkdownBlockKind.Code&&blocks[keep].Kind==MarkdownBlockKind.Code&&blocks[keep].Source.StartsWith(applied[keep].Source,StringComparison.Ordinal)&&codeRegions.TryGetValue(elements[keep],out var growing))
        {growing.UpdateText(blocks[keep].Text);applied[keep]=blocks[keep];keep++;}
        if(appliedVersion==selectedVersion&&keep<applied.Count&&keep<blocks.Count&&applied[keep].Table is not null&&blocks[keep].Table is {} tableTail&&blocks[keep].Source.StartsWith(applied[keep].Source,StringComparison.Ordinal)&&tableRegions.TryGetValue(elements[keep],out var growingTable)&&growingTable.CanAppend(tableTail))
        {growingTable.UpdateTable(tableTail);applied[keep]=blocks[keep];keep++;}
        appliedVersion=selectedVersion;
        for(int i=elements.Count-1;i>=keep;i--){if(codeRegions.Remove(elements[i],out var region))region.Dispose();if(tableRegions.Remove(elements[i],out var tableRegion))tableRegion.Dispose();host.Document.Blocks.Remove(elements[i]);elements.RemoveAt(i);applied.RemoveAt(i);}
        for(int i=keep;i<blocks.Count;i++)
        {
            var data=blocks[i];Block element;
            if(data.Kind==MarkdownBlockKind.Code)
            {
                var region=new MarkdownCodeRegion(data.Text,observeReader,captureReaderGuard);region.Constrain(host.Document.PageWidth,viewport.CodeMaximumHeightDip);
                region.ConfigureTail(i==blocks.Count-1,shouldFollow);
                element=new BlockUIContainer(region.View){Margin=new Thickness(0)};codeRegions.Add(element,region);
            }
            else if(data.Table is {} table)
            {
                var region=new MarkdownTableRegion(table,CreateTable,CreateTableParagraph,observeReader,captureReaderGuard);region.Constrain(host.Document.PageWidth,viewport.TableMaximumHeightDip);
                region.ConfigureTail(i==blocks.Count-1,shouldFollow);
                element=new BlockUIContainer(region.View){Margin=new Thickness(0)};tableRegions.Add(element,region);
            }
            else
            {
                var paragraph=CreateParagraph(data.Text,data.Inlines);element=paragraph;
                if(data.Kind is MarkdownBlockKind.Bullet or MarkdownBlockKind.Numbered)
                    element=new System.Windows.Documents.List(new ListItem(paragraph)){MarkerStyle=data.Kind==MarkdownBlockKind.Numbered?TextMarkerStyle.Decimal:TextMarkerStyle.Disc,StartIndex=data.Number,Margin=new Thickness(20,0,0,0),Padding=new Thickness(0)};
            }
            applied.Add(data);elements.Add(element);if(sourceBlock!=null)host.Document.Blocks.InsertBefore(sourceBlock,element);else host.Document.Blocks.Add(element);
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
    private void ConfigureTail(int index)
    {
        var tail=index>=0&&index<elements.Count?elements[index]:null;
        foreach(var pair in codeRegions)pair.Value.ConfigureTail(ReferenceEquals(pair.Key,tail),shouldFollow);
        foreach(var pair in tableRegions)pair.Value.ConfigureTail(ReferenceEquals(pair.Key,tail),shouldFollow);
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
                var paragraph=CreateTableParagraph(data.Rows[index].Cells[column],data.Alignments[column],index==0);
                row.Cells.Add(new TableCell(paragraph){Padding=new Thickness(4,2,4,2)});
            }
        }
        return table;
    }
    private static Paragraph CreateTableParagraph(MarkdownTableCell cell,MarkdownTableAlignment alignment,bool header)
    {
        var paragraph=CreateParagraph(cell.Text,cell.Inlines);
        paragraph.TextAlignment=alignment switch{MarkdownTableAlignment.Center=>TextAlignment.Center,MarkdownTableAlignment.Right=>TextAlignment.Right,_=>TextAlignment.Left};
        if(header)paragraph.FontWeight=FontWeights.SemiBold;
        return paragraph;
    }
}
