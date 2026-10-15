using KuaiYi.Core;
using KuaiYi.App;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class MarkdownTableProductionTests
{
    private const string Source="| **a** | *b* | `c|d` |\n| :--- | :---: | ---: |\n| x\\|y | z | |\n";
    private static ShellWindow Window()
    {
        var window=new ShellWindow();window.InitializeMarkdown();var origin=new EndpointOrigin(new Uri("https://public.invalid"));
        window.MarkdownGate.ConsentTo(origin);Assert.True(window.MarkdownGate.TryEnable(origin,true));return window;
    }
    private static RequestIdentity Begin(ShellWindow window,Guid session,string source,bool complete=true)
    {
        Assert.True(window.MarkdownGate.TryStartRequest(session,Guid.NewGuid(),null,out var id,out _));Assert.True(window.MarkdownResults.Begin(id));Assert.True(window.MarkdownResults.Append(id,source));if(complete)Assert.True(window.MarkdownResults.Complete(id));return id;
    }
    private static Table Native(RichTextBox host,int index=0)=>Assert.IsType<Table>(Assert.Single(Assert.IsType<RichTextBox>(Assert.IsType<BlockUIContainer>(host.Document.Blocks[index]).Child).Document.Blocks));
    private static string Text(Paragraph paragraph)=>string.Concat(paragraph.Inlines.Select(i=>i is Run r?r.Text:string.Concat(Assert.IsType<Span>(i).Inlines.Select(n=>Assert.IsType<Run>(n).Text))));
    [Fact]public void BodyAndMeasurementBothConstructActualNativeTableRowsCellsAndAlignment()
    {
        var window=Window();Begin(window,Guid.NewGuid(),Source);window.RenderMarkdown();
        foreach(var host in new[]{window.MarkdownBody,window.MarkdownMeasurement})
        {
            var table=Native(host);Assert.Equal(3,table.Columns.Count);var rows=Assert.Single(table.RowGroups).Rows;Assert.Equal(2,rows.Count);
            Assert.Equal(new[]{System.Windows.TextAlignment.Left,System.Windows.TextAlignment.Center,System.Windows.TextAlignment.Right},rows[0].Cells.Select(c=>c.Paragraph.TextAlignment));
            Assert.All(rows[0].Cells,c=>Assert.Equal("SemiBold",c.Paragraph.FontWeight));Assert.Equal("Bold",Assert.IsType<Span>(rows[0].Cells[0].Paragraph.Inlines[0]).FontWeight);
            var code=Assert.IsType<Span>(rows[0].Cells[2].Paragraph.Inlines[0]);Assert.Equal("Consolas",code.FontFamily!.Source);Assert.Equal("Normal",code.FontWeight);Assert.Equal("Normal",code.FontStyle);
            Assert.Equal(new[]{"x|y","z",""},rows[1].Cells.Select(c=>Text(c.Paragraph)));Assert.Equal(4,table.CellSpacing);
        }
        Assert.NotSame(Native(window.MarkdownBody),Native(window.MarkdownMeasurement));Assert.Equal(Source,window.MarkdownResults.Text);
    }
    [Fact]public void StreamingHeaderAndPartialCellUseActualBridgeForMeasureAndBody()
    {
        var window=Window();var id=Begin(window,Guid.NewGuid(),"a|b\n---|",false);window.RenderMarkdown();Assert.IsType<Paragraph>(window.MarkdownBody.Document.Blocks[0]);
        Assert.True(window.MarkdownResults.Append(id,"---\n"));window.RenderMarkdown();var old=Native(window.MarkdownBody);
        Assert.True(window.MarkdownResults.Append(id,"**unfinished"));window.RenderMarkdown(false);Assert.Same(old,Native(window.MarkdownBody));Assert.Equal(2,window.MarkdownMeasurement.Document.Blocks.Count);Assert.IsType<Paragraph>(window.MarkdownMeasurement.Document.Blocks[1]);
        Assert.True(window.MarkdownResults.Append(id,"**|y"));window.RenderMarkdown();Assert.Equal("unfinished",Text(Native(window.MarkdownBody).RowGroups[0].Rows[1].Cells[0].Paragraph));
        Assert.True(window.MarkdownResults.Append(id,"\n"));window.RenderMarkdown();Assert.Equal(2,Native(window.MarkdownMeasurement).RowGroups[0].Rows.Count);Assert.Equal("a|b\n---|---\n**unfinished**|y\n",window.MarkdownResults.Text);
    }
    [Fact]public void NativeStableTableIsRetainedWhileTailChangesAndFontPreferenceUpdates()
    {
        var window=Window();var id=Begin(window,Guid.NewGuid(),Source+"\ntail",false);window.RenderMarkdown();var body=Native(window.MarkdownBody);var measure=Native(window.MarkdownMeasurement);
        Assert.True(window.MarkdownResults.Append(id," grows"));window.RenderMarkdown();Assert.Same(body,Native(window.MarkdownBody));Assert.Same(measure,Native(window.MarkdownMeasurement));
        window.MarkdownSize(20);window.RenderMarkdown();Assert.Same(body,Native(window.MarkdownBody));Assert.Equal(20,body.FontSize);Assert.Equal(31,body.LineHeight);Assert.Equal(20,measure.FontSize);
    }
    [Fact]public void ActualThreeVersionSelectionFailureCancelClearAndLateEventsKeepTablesIsolated()
    {
        var window=Window();var session=Guid.NewGuid();var ids=new List<RequestIdentity>();
        for(int i=0;i<4;i++)ids.Add(Begin(window,session,Source.Replace("x\\|y","version "+i,StringComparison.Ordinal)));
        Assert.Equal(3,window.MarkdownResults.Versions.Count);Assert.True(window.MarkdownResults.SelectVersion(ids[1].VersionId));window.RenderMarkdown();Assert.Equal("version 1",Text(Native(window.MarkdownBody).RowGroups[0].Rows[1].Cells[0].Paragraph));
        var failed=Begin(window,session,"a|b\n---|---\n*unclosed|y",false);Assert.True(window.MarkdownResults.Fail(failed));window.RenderMarkdown();Assert.Equal(ids[1].VersionId,window.MarkdownResults.SelectedVersionId);Assert.Equal(Source.Replace("x\\|y","version 1",StringComparison.Ordinal),window.MarkdownResults.Text);
        Assert.False(window.MarkdownResults.Append(ids[0],"late evicted"));Assert.False(window.MarkdownResults.Append(failed,"late failed"));window.RenderMarkdown();Assert.Equal("version 1",Text(Native(window.MarkdownMeasurement).RowGroups[0].Rows[1].Cells[0].Paragraph));
        var active=Begin(window,session,Source,false);window.RenderMarkdown();window.MarkdownGate.Disable();window.MarkdownResults.MarkStopped();window.RenderMarkdown();Assert.False(window.MarkdownResults.Append(active,"late canceled"));Native(window.MarkdownBody);
        window.MarkdownGate.SetSessionLocked(true);window.MarkdownResults.Clear();window.DropMarkdown();Assert.Empty(window.MarkdownModel.Blocks);Assert.Empty(window.MarkdownBody.Document.Blocks);Assert.Empty(window.MarkdownMeasurement.Document.Blocks);Assert.False(window.MarkdownResults.Append(active,"late cleared"));
    }
    [Fact]public void WholeCapacityFallbackIsRenderedLiterallyInBothDocumentsAndRawCopyData()
    {
        string source=RestrictedMarkdownTableTests.Grid(17,2);var window=Window();Begin(window,Guid.NewGuid(),source);window.RenderMarkdown();Assert.True(window.MarkdownModel.CapacityFallback);
        foreach(var host in new[]{window.MarkdownBody,window.MarkdownMeasurement})Assert.Equal(source,Text(Assert.IsType<Paragraph>(Assert.Single(host.Document.Blocks))));Assert.Equal(source,window.MarkdownResults.Text);
    }
    [Fact]public void UnsupportedCellReplacesTheEntireOpenNativeTableWithOriginalLiteralText()
    {
        var window=Window();var id=Begin(window,Guid.NewGuid(),Source,false);window.RenderMarkdown();Assert.Equal(2,Native(window.MarkdownBody).RowGroups[0].Rows.Count);
        const string invalid="| [link](file:///inert) | x | y |\n";Assert.True(window.MarkdownResults.Append(id,invalid));window.RenderMarkdown();
        foreach(var host in new[]{window.MarkdownBody,window.MarkdownMeasurement})Assert.Equal(Source+invalid,Text(Assert.IsType<Paragraph>(Assert.Single(host.Document.Blocks))));Assert.Equal(Source+invalid,window.MarkdownResults.Text);
    }
}
