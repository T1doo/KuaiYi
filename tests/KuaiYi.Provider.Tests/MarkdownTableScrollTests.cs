using KuaiYi.Core;
using KuaiYi.App;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class MarkdownTableScrollTests
{
    private const string Source="**head**|*other*\n:---|---:\nx|`literal *code*`\n";
    private const string Open="a|b\n---|---\nx|abcdef";
    private static ShellWindow Window()
    {
        var w=new ShellWindow();w.InitializeMarkdown();var origin=new EndpointOrigin(new Uri("https://public.invalid"));w.MarkdownGate.ConsentTo(origin);Assert.True(w.MarkdownGate.TryEnable(origin,true));return w;
    }
    private static RequestIdentity Begin(ShellWindow w,string source,bool complete=false,Guid? session=null)
    {
        Assert.True(w.MarkdownGate.TryStartRequest(session??Guid.NewGuid(),Guid.NewGuid(),null,out var id,out _));Assert.True(w.MarkdownResults.Begin(id));w.ReplaceResult();w.ResetMarkdownReadingForTest();Assert.True(w.MarkdownResults.Append(id,source));if(complete)Assert.True(w.MarkdownResults.Complete(id));return id;
    }
    private static RichTextBox Region(RichTextBox host)=>Assert.IsType<RichTextBox>(Assert.Single(host.Document.Blocks.OfType<BlockUIContainer>(),b=>b.Child is RichTextBox).Child);
    private static Table Native(RichTextBox region)=>Assert.IsType<Table>(Assert.Single(region.Document.Blocks));
    private static string Text(Paragraph paragraph)=>string.Concat(paragraph.Inlines.Select(i=>i is Run r?r.Text:string.Concat(Assert.IsType<Span>(i).Inlines.Select(n=>Assert.IsType<Run>(n).Text))));
    private static void Select(RichTextBox view,int start,int end)=>view.Selection.Select(view.Document.ContentStart.GetPositionAtOffset(start)!,view.Document.ContentStart.GetPositionAtOffset(end)!);
    private static void At(RichTextBox view,int start,int end,double horizontal,double vertical)
    {Assert.Equal(start,view.Selection.Start.Offset);Assert.Equal(end,view.Selection.End.Offset);Assert.Equal(horizontal,view.HorizontalOffset);Assert.Equal(vertical,view.VerticalOffset);}
    private static LayoutPlan Plan(double dpi)=>new("public",new(0,0,2000,2000),new(0,0,48,48),new(0,0,(int)(420*dpi),(int)(640*dpi)),GrowthDirection.RightDown,new(dpi),(int)(640*dpi));

    [Fact]public void WideTableKeepsNativeStructureInsideFiniteLocalViewportAndProsePage()
    {
        string source="ordinary prose\n\n"+RestrictedMarkdownTableTests.Grid(16,2,new string('字',1000));var w=Window();Begin(w,source);w.MarkdownViewportForTest(new(300,400));w.RenderMarkdown();
        foreach(var host in new[]{w.MarkdownBody,w.MarkdownMeasurement})
        {
            Assert.IsType<Paragraph>(host.Document.Blocks[0]);var view=Region(host);var table=Native(view);
            Assert.True(view.IsReadOnly);Assert.False(view.IsUndoEnabled);Assert.False(view.AllowDrop);Assert.True(view.IsDocumentEnabled);Assert.True(view.IsReadOnlyCaretVisible);Assert.True(view.ClipToBounds);
            Assert.Equal("表格",view.AutomationName);Assert.Equal(ScrollBarVisibility.Visible,view.HorizontalScrollBarVisibility);Assert.Equal(ScrollBarVisibility.Visible,view.VerticalScrollBarVisibility);
            Assert.Equal(283,view.Width);Assert.Equal(view.Width,view.MaxWidth);Assert.Equal(320,view.MaxHeight);Assert.Equal(300,host.Width);Assert.Equal(283,host.Document.PageWidth);Assert.Equal(283,host.Document.ColumnWidth);Assert.Equal(400,host.MaxHeight);Assert.Equal(ScrollBarVisibility.Disabled,host.HorizontalScrollBarVisibility);
            Assert.Equal(16,table.Columns.Count);Assert.All(table.Columns,c=>Assert.Equal(480,c.Width.Value));Assert.Equal(7748,view.Document.PageWidth);Assert.Equal(view.Document.PageWidth,view.Document.ColumnWidth);
            Assert.Equal(2,table.RowGroups[0].Rows.Count);Assert.Equal(new string('字',1000),Text(table.RowGroups[0].Rows[1].Cells[15].Paragraph));
        }
        Assert.NotSame(Region(w.MarkdownBody),Region(w.MarkdownMeasurement));Assert.Equal(source,w.MarkdownResults.Text);
    }
    [Theory][InlineData(1,false)][InlineData(1.5,true)][InlineData(2,false)]
    public void ActualMeasureBridgeSharesFiniteTableConstraintsForDpiAndLabel(double dpi,bool label)
    {
        var w=Window();Begin(w,Source);w.RenderMarkdown();w.MarkdownMeasurement.DesiredSize=new(222,77);Assert.Equal(77,w.MeasureMarkdownForTest(Plan(dpi),label));w.RenderMarkdown();
        double height=560-(label?20:0);Assert.Equal(new Size(388,height),w.MarkdownMeasurement.LastMeasure);
        foreach(var host in new[]{w.MarkdownBody,w.MarkdownMeasurement}){Assert.Equal(388,host.Width);Assert.Equal(height,host.MaxHeight);Assert.Equal(371,Region(host).Width);Assert.Equal(320,Region(host).MaxHeight);Assert.True(double.IsFinite(Region(host).Document.PageWidth));}
    }
    [Theory][InlineData(1,1)][InlineData(50,80)][InlineData(200,240)]
    public void SmallViewportKeepsTablesBounded(double width,double height)
    {
        var w=Window();Begin(w,Source);w.MarkdownViewportForTest(new(width,height));w.RenderMarkdown();
        foreach(var host in new[]{w.MarkdownBody,w.MarkdownMeasurement}){Assert.Equal(Math.Max(1,width-17),Region(host).Width);Assert.Equal(height,Region(host).MaxHeight);Assert.InRange(Region(host).Document.PageWidth,1,8192);}
    }
    [Fact]public void StableTableKeepsControlNativeCellsOffsetsAndStylesAcrossTailFontThemeAndViewportUpdates()
    {
        var w=Window();var id=Begin(w,Source+"\ntail");w.RenderMarkdown();var view=Region(w.MarkdownBody);var table=Native(view);var header=table.RowGroups[0].Rows[0].Cells[0];var span=header.Paragraph.Inlines[0];view.ScrollToHorizontalOffset(70);Select(view,7,19);
        Assert.True(w.MarkdownResults.Append(id," grows"));w.RenderMarkdown();w.MarkdownSize(20);w.MarkdownBody.Foreground=w.MarkdownMeasurement.Foreground=Brushes.White;w.MarkdownViewportForTest(new(250,180));w.RenderMarkdown();
        Assert.Same(view,Region(w.MarkdownBody));Assert.Same(table,Native(view));Assert.Same(header,table.RowGroups[0].Rows[0].Cells[0]);Assert.Same(span,header.Paragraph.Inlines[0]);At(view,7,19,70,0);
        foreach(var host in new[]{w.MarkdownBody,w.MarkdownMeasurement}){Assert.Equal(233,Region(host).Width);Assert.Equal(180,Region(host).MaxHeight);Assert.Equal(20,Region(host).FontSize);Assert.Equal(20,Native(Region(host)).FontSize);Assert.Equal(31,Native(Region(host)).LineHeight);Assert.Equal(Brushes.White,Region(host).Foreground);}
        Assert.Equal("Bold",Assert.IsType<Span>(span).FontWeight);Assert.Equal(TextAlignment.Right,table.RowGroups[0].Rows[0].Cells[1].Paragraph.TextAlignment);Assert.Equal("Consolas",Assert.IsType<Span>(table.RowGroups[0].Rows[1].Cells[1].Paragraph.Inlines[0]).FontFamily!.Source);
    }
    [Fact]public void OpenTailReusesNativeTableAndUnchangedCellsWhileMeasurementOnlyWaitsForBodyFlush()
    {
        var w=Window();var id=Begin(w,Open);w.RenderMarkdown();var view=Region(w.MarkdownBody);var table=Native(view);var header=table.RowGroups[0].Rows[0];var firstCell=table.RowGroups[0].Rows[1].Cells[0];var changedCell=table.RowGroups[0].Rows[1].Cells[1];var before=changedCell.Paragraph;
        Assert.True(w.MarkdownResults.Append(id," more"));w.RenderMarkdown(false);Assert.Equal("abcdef",Text(changedCell.Paragraph));Assert.Equal("abcdef more",Text(Native(Region(w.MarkdownMeasurement)).RowGroups[0].Rows[1].Cells[1].Paragraph));w.RenderMarkdown();
        Assert.Same(view,Region(w.MarkdownBody));Assert.Same(table,Native(view));Assert.Same(header,table.RowGroups[0].Rows[0]);Assert.Same(firstCell,table.RowGroups[0].Rows[1].Cells[0]);Assert.Same(changedCell,table.RowGroups[0].Rows[1].Cells[1]);Assert.NotSame(before,changedCell.Paragraph);
        Assert.True(w.MarkdownResults.Append(id,"\nnew|**bold**\n"));w.RenderMarkdown();Assert.Same(table,Native(view));Assert.Equal(3,table.RowGroups[0].Rows.Count);Assert.Equal("Bold",Assert.IsType<Span>(table.RowGroups[0].Rows[2].Cells[1].Paragraph.Inlines[0]).FontWeight);Assert.Equal(0,Region(w.MarkdownMeasurement).Dispatcher.Pending);
    }
    [Fact]public void CoalescedSymbolSelectionAndOffsetsRestoreOnlyNewestUpdateInReverseOrder()
    {
        var w=Window();var id=Begin(w,Open);w.RenderMarkdown();var view=Region(w.MarkdownBody);Select(view,17,31);view.ScrollToHorizontalOffset(70);view.ScrollToVerticalOffset(20);
        Assert.True(w.MarkdownResults.Append(id," more"));w.RenderMarkdown();Select(view,0,0);view.ScrollToHorizontalOffset(0);view.ScrollToVerticalOffset(0);
        Assert.True(w.MarkdownResults.Append(id," again"));w.RenderMarkdown();Assert.Equal(2,view.Dispatcher.Pending);view.Dispatcher.Deliver(1);view.Dispatcher.Deliver();At(view,17,31,70,20);Assert.Equal(0,Region(w.MarkdownMeasurement).Dispatcher.Pending);
    }
    [Fact]public void SymbolSelectionRestoreClampsToRetainedCellBoundary()
    {
        var w=Window();var id=Begin(w,Open);w.RenderMarkdown();var view=Region(w.MarkdownBody);Select(view,17,31);Assert.True(w.MarkdownResults.Append(id," more"));w.RenderMarkdown();Native(view).RowGroups[0].Rows[0].Cells[0].SymbolEnd=20;view.Dispatcher.Deliver();At(view,17,20,0,0);
    }
    [Fact]public void WholeTableSelectionAnchorsToOldCellsWhenNewRowsMoveClosingSymbols()
    {
        var w=Window();var id=Begin(w,Open);w.RenderMarkdown();var view=Region(w.MarkdownBody);var rows=Native(view).RowGroups[0].Rows;
        for(int row=0;row<2;row++)for(int column=0;column<2;column++){rows[row].Cells[column].SymbolStart=10+(row*2+column)*20;rows[row].Cells[column].SymbolEnd=20+(row*2+column)*20;}
        view.Document.SymbolLength=100;Select(view,0,100);Assert.True(w.MarkdownResults.Append(id,"\ny|new row"));w.RenderMarkdown();view.Document.SymbolLength=140;
        rows[2].Cells[0].SymbolStart=90;rows[2].Cells[0].SymbolEnd=100;rows[2].Cells[1].SymbolStart=110;rows[2].Cells[1].SymbolEnd=120;
        Select(view,0,0);view.Dispatcher.Deliver();At(view,10,80,0,0);Assert.Equal("abcdef",Text(rows[1].Cells[1].Paragraph));Assert.Equal("new row",Text(rows[2].Cells[1].Paragraph));
    }
    [Fact]public void CellLocalAnchorFollowsRetainedCellWhenItsAbsolutePositionMoves()
    {
        var w=Window();var id=Begin(w,Open);w.RenderMarkdown();var view=Region(w.MarkdownBody);var rows=Native(view).RowGroups[0].Rows;
        rows[0].Cells[0].SymbolStart=10;rows[0].Cells[0].SymbolEnd=30;Select(view,17,21);Assert.True(w.MarkdownResults.Append(id," more"));w.RenderMarkdown();rows[0].Cells[0].SymbolStart=50;rows[0].Cells[0].SymbolEnd=70;Select(view,0,0);view.Dispatcher.Deliver();At(view,57,61,0,0);
    }
    [Fact]public void WholeTableSelectionExcludesMovedRunAndParagraphClosingsWhenLastCellGrows()
    {
        var w=Window();var id=Begin(w,Open);w.RenderMarkdown();var view=Region(w.MarkdownBody);var rows=Native(view).RowGroups[0].Rows;
        for(int row=0;row<2;row++)for(int column=0;column<2;column++){var cell=rows[row].Cells[column];cell.SymbolStart=10+(row*2+column)*20;cell.SymbolEnd=20+(row*2+column)*20;cell.InsertionStart=cell.SymbolStart+2;cell.InsertionEnd=cell.SymbolEnd-4;}
        view.Document.SymbolLength=100;Select(view,0,100);Assert.True(w.MarkdownResults.Append(id," more"));w.RenderMarkdown();var last=rows[1].Cells[1];last.SymbolEnd=85;last.InsertionEnd=81;view.Document.SymbolLength=105;
        Select(view,0,0);view.Dispatcher.Deliver();At(view,12,76,0,0);Assert.Equal("abcdef more",Text(last.Paragraph));Assert.True(view.Selection.End.Offset<last.InsertionEnd);
    }
    [Theory][InlineData("local")][InlineData("reader")][InlineData("version")][InlineData("lock")][InlineData("exit")][InlineData("clear")]
    public void LocalAndParentGuardsRejectStaleDeferredTableRestores(string change)
    {
        var w=Window();var id=Begin(w,Open);w.RenderMarkdown();var view=Region(w.MarkdownBody);Select(view,17,31);view.ScrollToHorizontalOffset(30);Assert.True(w.MarkdownResults.Append(id," more"));w.RenderMarkdown();Assert.Equal(1,view.Dispatcher.Pending);
        switch(change){case "local":view.RaiseWheel();break;case "reader":w.ObserveReaderInput();break;case "version":Begin(w,Source);w.RenderMarkdown();break;case "lock":w.Lock();break;case "exit":w.Exit();break;case "clear":w.MarkdownResults.Clear();w.DropMarkdown();break;}
        Select(view,0,0);view.ScrollToHorizontalOffset(90);view.Dispatcher.Deliver();At(view,0,0,90,0);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    [InlineData(6)][InlineData(7)][InlineData(8)][InlineData(9)][InlineData(10)][InlineData(11)][InlineData(12)][InlineData(13)][InlineData(14)]
    public void TableReadingDelegatesFenceOuterRestoreAndStayPausedAtOuterBottom(int input)
    {
        var w=Window();Begin(w,Source);w.RenderMarkdown();var view=Region(w.MarkdownBody);w.QueueScroll(true,800);
        switch(input)
        {
            case 0:view.RaiseWheel();break;case 1:view.RaiseMouse(MouseButton.Left);break;case 2:view.RaiseMove(MouseButtonState.Pressed);break;
            case 3:view.RaiseTouch();break;case 4:view.RaiseTouchMove();break;case 5:view.RaiseScroll();break;
            case 14:try{Keyboard.Modifiers=ModifierKeys.Control;view.RaiseKey(Key.A);}finally{Keyboard.Modifiers=ModifierKeys.None;}break;
            default:view.RaiseKey(new[]{Key.Up,Key.Down,Key.Left,Key.Right,Key.PageUp,Key.PageDown,Key.Home,Key.End}[input-6]);break;
        }
        Assert.False(w.Following);w.Deliver();Assert.Equal(0,w.ScrollCalls);w.ObserveReaderInput();w.ChangeGeometry(1200,200,1000);w.Deliver();Assert.False(w.Following);w.ReaderFollowEnd();Assert.True(w.Following);
    }
    [Fact]public void MeasurementAndNonReadingKeysDoNotPauseBodyWhileNewResultResetsLocalPause()
    {
        var w=Window();Begin(w,Source);w.RenderMarkdown();Region(w.MarkdownMeasurement).RaiseWheel();var view=Region(w.MarkdownBody);view.RaiseMouse(MouseButton.Right);view.RaiseMove(MouseButtonState.Released);view.RaiseKey(Key.C);view.RaiseKey(Key.A);Assert.True(w.Following);
        view.RaiseScroll();Assert.False(w.Following);Begin(w,Source);w.RenderMarkdown();Assert.True(w.Following);
    }
    [Fact]public void ReplacedTableClearsNativeRowsHandlersOffsetsSelectionAndLateCallbacks()
    {
        var w=Window();var id=Begin(w,Open);w.RenderMarkdown();var view=Region(w.MarkdownBody);var measure=Region(w.MarkdownMeasurement);var table=Native(view);var late=view.CaptureWheel();Select(view,8,20);view.ScrollToHorizontalOffset(30);Assert.True(w.MarkdownResults.Append(id," more"));w.RenderMarkdown();
        Begin(w,"new paragraph");w.RenderMarkdown();foreach(var old in new[]{view,measure}){Assert.Empty(old.Document.Blocks);Assert.Equal(0,old.ReaderHandlerCount);At(old,0,0,0,0);}Assert.Empty(table.Columns);Assert.Empty(table.RowGroups);view.Dispatcher.Deliver();At(view,0,0,0,0);
        w.QueueScroll(false,55);late();w.Deliver();Assert.Equal(55,w.ScrollOffset);Assert.Equal(1,w.ScrollCalls);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void UnsupportedTailOrCapacityFallbackDisposesLocalRegionsWithoutLosingRawText(bool capacity)
    {
        var w=Window();var id=Begin(w,Source);w.RenderMarkdown();var view=Region(w.MarkdownBody);var measure=Region(w.MarkdownMeasurement);string tail=capacity?new string('x',RestrictedMarkdown.MaximumFormattedCharacters):"[link](file:///inert)|x\n";
        Assert.True(w.MarkdownResults.Append(id,tail));w.RenderMarkdown();Assert.Equal(capacity,w.MarkdownModel.CapacityFallback);foreach(var old in new[]{view,measure}){Assert.Empty(old.Document.Blocks);Assert.Equal(0,old.ReaderHandlerCount);}foreach(var host in new[]{w.MarkdownBody,w.MarkdownMeasurement})Assert.Equal(Source+tail,Text(Assert.IsType<Paragraph>(Assert.Single(host.Document.Blocks))));Assert.Equal(Source+tail,w.MarkdownResults.Text);
    }
    [Fact]public void ActualThreeVersionCacheFailureCancelLockClearAndLateChunksIsolateTableViews()
    {
        var w=Window();var session=Guid.NewGuid();var ids=new List<RequestIdentity>();for(int i=0;i<4;i++){ids.Add(Begin(w,Source.Replace("head","version "+i,StringComparison.Ordinal),true,session));w.RenderMarkdown();}
        Assert.Equal(3,w.MarkdownResults.Versions.Count);var old=Region(w.MarkdownBody);Assert.True(w.MarkdownResults.SelectVersion(ids[1].VersionId));w.ReplaceResult();w.ResetMarkdownReadingForTest();w.RenderMarkdown();Assert.Empty(old.Document.Blocks);Assert.Equal("version 1",Text(Native(Region(w.MarkdownBody)).RowGroups[0].Rows[0].Cells[0].Paragraph));
        var failed=Begin(w,Open,false,session);Assert.True(w.MarkdownResults.Fail(failed));w.RenderMarkdown();Assert.Equal(ids[1].VersionId,w.MarkdownResults.SelectedVersionId);Assert.False(w.MarkdownResults.Append(ids[0],"late evicted"));Assert.False(w.MarkdownResults.Append(failed,"late failed"));
        var active=Begin(w,Open,false,session);w.RenderMarkdown();var canceled=Region(w.MarkdownBody);w.MarkdownGate.Disable();w.MarkdownResults.MarkStopped();w.RenderMarkdown();Assert.False(w.MarkdownResults.Append(active,"late canceled"));Assert.Same(canceled,Region(w.MarkdownBody));w.MarkdownGate.SetSessionLocked(true);w.MarkdownResults.Clear();w.DropMarkdown();Assert.Empty(canceled.Document.Blocks);Assert.Equal(0,canceled.ReaderHandlerCount);Assert.False(w.MarkdownResults.Append(active,"late clear"));Assert.Empty(w.MarkdownBody.Document.Blocks);Assert.Empty(w.MarkdownMeasurement.Document.Blocks);
    }
    [Fact]public void MixedProseCodeAndTableKeepSeparateFiniteRegionsAndUnchangedRawCopy()
    {
        string source="# title\n\n```\nliteral | **code**\n```\n\n"+Source+"\nending";var w=Window();Begin(w,source);w.MarkdownViewportForTest(new(300,400));w.RenderMarkdown();
        foreach(var host in new[]{w.MarkdownBody,w.MarkdownMeasurement}){Assert.Equal(2,host.Document.Blocks.OfType<BlockUIContainer>().Count());var code=Assert.IsType<TextBox>(host.Document.Blocks.OfType<BlockUIContainer>().Single(b=>b.Child is TextBox).Child);Assert.Equal("literal | **code**\n",code.Text);Assert.Equal(283,code.Width);Assert.Equal(283,Region(host).Width);Assert.Equal(283,host.Document.PageWidth);Assert.IsType<Paragraph>(host.Document.Blocks[^1]);}Assert.Equal(source,w.MarkdownResults.Text);
    }
}
