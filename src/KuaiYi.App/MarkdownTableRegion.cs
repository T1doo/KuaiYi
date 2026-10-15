using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KuaiYi.Core;
namespace KuaiYi.App;

/// <summary>A bounded local viewport around a native Table; no external document, resource or markup loading.</summary>
internal sealed class MarkdownTableRegion:IDisposable
{
    private Func<MarkdownTableCell,MarkdownTableAlignment,bool,Paragraph>? createParagraph;
    private Action<Paragraph>? releaseParagraph;
    private readonly Action? observeReader;
    private readonly Func<Func<bool>>? captureReaderGuard;
    private readonly ScrollEventHandler scrollHandler;
    private MarkdownTable? data;
    private readonly Table table;
    private long revision;
    private readonly record struct CellAnchor(int Row,int Column,int Offset);
    private (CellAnchor Start,CellAnchor End,double Horizontal,double Vertical)? pendingSelection;
    private Func<bool>? pendingGuard;
    private Func<bool>? shouldFollow;
    private bool isTail,pendingFollow,loadedOnce;
    private bool disposed;
    public RichTextBox View {get;}=new()
    {
        IsReadOnly=true,IsUndoEnabled=false,IsReadOnlyCaretVisible=true,IsDocumentEnabled=true,AllowDrop=false,
        HorizontalScrollBarVisibility=ScrollBarVisibility.Visible,VerticalScrollBarVisibility=ScrollBarVisibility.Visible,
        BorderThickness=new Thickness(0),Padding=new Thickness(0),Background=Brushes.Transparent,ClipToBounds=true,
        Document=new FlowDocument{PagePadding=new Thickness(0),ColumnGap=0}
    };
    public MarkdownTableRegion(MarkdownTable data,Func<MarkdownTable,Table> createTable,Func<MarkdownTableCell,MarkdownTableAlignment,bool,Paragraph> createParagraph,Action? observeReader,Func<Func<bool>>? captureReaderGuard,Action<Paragraph>? releaseParagraph=null)
    {
        this.data=data;this.createParagraph=createParagraph;this.releaseParagraph=releaseParagraph;this.observeReader=observeReader;this.captureReaderGuard=captureReaderGuard;
        table=createTable(data);View.Document.Blocks.Add(table);
        System.Windows.Automation.AutomationProperties.SetName(View,"表格");
        View.PreviewMouseWheel+=Wheel;View.PreviewMouseDown+=MouseDown;View.PreviewMouseMove+=MouseMove;
        View.PreviewKeyDown+=KeyDown;View.PreviewTouchDown+=Touch;View.PreviewTouchMove+=Touch;
        scrollHandler=Scroll;View.AddHandler(ScrollBar.ScrollEvent,scrollHandler,true);
        View.Loaded+=Loaded;
    }
    public void Constrain(double width,double maximumHeight)
    {View.Width=View.MaxWidth=width;View.MaxHeight=maximumHeight;SizeColumns();}
    public void ConfigureTail(bool value,Func<bool>? follow){isTail=value;shouldFollow=follow;}
    private bool CanFollow()=>!disposed&&isTail&&shouldFollow?.Invoke()==true;
    private void Loaded(object? sender,RoutedEventArgs e){if(loadedOnce)return;loadedOnce=true;if(CanFollow())FollowEnd();}
    public void FollowEnd()
    {
        if(!CanFollow()||captureReaderGuard?.Invoke() is not {} guard)return;
        var state=pendingSelection is {} pending&&pendingGuard?.Invoke()==true?pending:(CaptureAnchor(View.Selection.Start),CaptureAnchor(View.Selection.End),View.HorizontalOffset,View.VerticalOffset);
        Restore(state,true,++revision,guard);
    }
    public void RefreshAppearance(double fontSize,Brush foreground)
    {
        View.FontSize=View.Document.FontSize=table.FontSize=fontSize;View.Foreground=foreground;
        table.FontFamily=new FontFamily("Segoe UI");table.LineHeight=fontSize*1.55;SizeColumns();
    }
    private void SizeColumns()
    {
        if(data is null)return;
        // A deterministic width policy, not font measurement. Long cells wrap within a finite column;
        // many/wide columns scroll inside this viewport, never enlarge the outer FlowDocument.
        double total=table.CellSpacing*(data.Alignments.Count+1);
        for(int column=0;column<data.Alignments.Count;column++)
        {
            int longest=0;foreach(var row in data.Rows)longest=Math.Max(longest,Math.Min(512,row.Cells[column].Text.Length));
            double width=Math.Clamp(8+longest*View.FontSize*.65,96,480);
            table.Columns[column].Width=new GridLength(width);total+=width;
        }
        View.Document.PageWidth=View.Document.ColumnWidth=Math.Min(8192,Math.Max(Math.Max(1,View.Width-SystemParameters.VerticalScrollBarWidth),total));
    }
    public bool CanAppend(MarkdownTable next)=>!disposed&&data is {} previous&&next.Rows.Count>=previous.Rows.Count&&next.Alignments.SequenceEqual(previous.Alignments);
    public void UpdateTable(MarkdownTable next)
    {
        if(!CanAppend(next))throw new InvalidOperationException("Only the same table tail may grow.");
        var previous=data!;
        bool coalesced=pendingSelection is not null&&pendingGuard?.Invoke()==true;
        var state=coalesced?pendingSelection!.Value:(CaptureAnchor(View.Selection.Start),CaptureAnchor(View.Selection.End),View.HorizontalOffset,View.VerticalOffset);
        bool end=CanFollow()&&(coalesced?pendingFollow:View.ExtentHeight-View.ViewportHeight-View.VerticalOffset<=24);
        long current=++revision;var canRestore=captureReaderGuard?.Invoke();
        var rows=table.RowGroups[0].Rows;
        for(int index=0;index<next.Rows.Count;index++)
        {
            bool added=index>=previous.Rows.Count;if(added)rows.Add(new TableRow());
            for(int column=0;column<next.Alignments.Count;column++)
            {
                var cell=next.Rows[index].Cells[column];
                if(!added&&cell==previous.Rows[index].Cells[column])continue;
                if(!added&&cell.Source==previous.Rows[index].Cells[column].Source&&cell.Text==previous.Rows[index].Cells[column].Text&&cell.Inlines.SequenceEqual(previous.Rows[index].Cells[column].Inlines))continue;
                var paragraph=createParagraph!(cell,next.Alignments[column],index==0);
                if(added)rows[index].Cells.Add(new TableCell(paragraph){Padding=new Thickness(4,2,4,2)});
                else {foreach(var old in rows[index].Cells[column].Blocks.OfType<Paragraph>())releaseParagraph?.Invoke(old);rows[index].Cells[column].Blocks.Clear();rows[index].Cells[column].Blocks.Add(paragraph);}
            }
        }
        data=next;SizeColumns();
        if(canRestore is null)return; // Measurement has no reader state or deferred work.
        Restore(state,end,current,canRestore);
    }
    private void Restore((CellAnchor Start,CellAnchor End,double Horizontal,double Vertical) state,bool end,long current,Func<bool> canRestore)
    {
        pendingSelection=state;pendingGuard=canRestore;pendingFollow=end;
        View.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>
        {
            if(disposed||current!=revision||!canRestore())return;
            View.Selection.Select(ResolveAnchor(state.Item1),ResolveAnchor(state.Item2));
            if(end&&CanFollow())View.ScrollToEnd();else View.ScrollToVerticalOffset(state.Item4);
            View.ScrollToHorizontalOffset(state.Item3);pendingSelection=null;pendingGuard=null;pendingFollow=false;
        });
    }
    private CellAnchor CaptureAnchor(TextPointer point)
    {
        CellAnchor previous=AnchorInCell(0,0,table.RowGroups[0].Rows[0].Cells[0].ContentStart);var rows=table.RowGroups[0].Rows;
        for(int row=0;row<rows.Count;row++)for(int column=0;column<rows[row].Cells.Count;column++)
        {
            var cell=rows[row].Cells[column];
            if(point.CompareTo(cell.ContentStart)<0)return previous;
            if(point.CompareTo(cell.ContentEnd)<=0)return AnchorInCell(row,column,point);
            previous=AnchorInCell(row,column,cell.ContentEnd);
        }
        // Table/row closing symbols (including Ctrl+A's ContentEnd) bind to the old last
        // cell, so appending rows never makes an old selection extend into new rows.
        return previous;
    }
    private CellAnchor AnchorInCell(int row,int column,TextPointer point)
    {
        var cell=table.RowGroups[0].Rows[row].Cells[column];
        var first=cell.ContentStart.GetInsertionPosition(LogicalDirection.Forward)??cell.ContentStart;
        var last=cell.ContentEnd.GetInsertionPosition(LogicalDirection.Backward)??cell.ContentEnd;
        if(point.CompareTo(first)<0)point=first;if(point.CompareTo(last)>0)point=last;
        // Exclude the old Run/Paragraph closing symbols as well as cell/table closing
        // symbols: those move when the old final cell itself grows, not just on new rows.
        return new(row,column,cell.ContentStart.GetOffsetToPosition(point));
    }
    private TextPointer ResolveAnchor(CellAnchor anchor)
    {
        var cell=table.RowGroups[0].Rows[anchor.Row].Cells[anchor.Column];var start=cell.ContentStart;
        // Offsets count native document symbols, not displayed characters, and stay local
        // to the retained cell even when native row/table closing symbols move.
        var point=start.GetPositionAtOffset(Math.Clamp(anchor.Offset,0,start.GetOffsetToPosition(cell.ContentEnd)))??cell.ContentEnd;
        var first=start.GetInsertionPosition(LogicalDirection.Forward)??start;var last=cell.ContentEnd.GetInsertionPosition(LogicalDirection.Backward)??cell.ContentEnd;
        if(point.CompareTo(first)<0)return first;if(point.CompareTo(last)>0)return last;
        return point;
    }
    private void Observe(){if(!disposed){revision++;pendingSelection=null;pendingGuard=null;observeReader?.Invoke();}}
    private void Wheel(object? sender,MouseWheelEventArgs e)=>Observe();
    private void MouseDown(object? sender,MouseButtonEventArgs e){if(e.ChangedButton==MouseButton.Left)Observe();}
    private void MouseMove(object? sender,MouseEventArgs e){if(e.LeftButton==MouseButtonState.Pressed)Observe();}
    private void Touch(object? sender,TouchEventArgs e)=>Observe();
    private void KeyDown(object? sender,KeyEventArgs e)
    {if(e.Key is Key.Up or Key.Down or Key.Left or Key.Right or Key.PageUp or Key.PageDown or Key.Home or Key.End||e.Key==Key.A&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control))Observe();}
    private void Scroll(object? sender,ScrollEventArgs e)=>Observe();
    public void Dispose()
    {
        if(disposed)return;disposed=true;revision++;pendingSelection=null;pendingGuard=null;data=null;createParagraph=null;releaseParagraph=null;
        View.PreviewMouseWheel-=Wheel;View.PreviewMouseDown-=MouseDown;View.PreviewMouseMove-=MouseMove;
        View.PreviewKeyDown-=KeyDown;View.PreviewTouchDown-=Touch;View.PreviewTouchMove-=Touch;View.RemoveHandler(ScrollBar.ScrollEvent,scrollHandler);
        View.Loaded-=Loaded;shouldFollow=null;
        View.Document.Blocks.Clear();table.RowGroups.Clear();table.Columns.Clear();View.Selection.Select(View.Document.ContentStart,View.Document.ContentStart);View.ScrollToHorizontalOffset(0);View.ScrollToVerticalOffset(0);
    }
}
