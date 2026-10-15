using KuaiYi.App;
using KuaiYi.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class MarkdownTailFollowTests
{
    private static ShellWindow Window()
    {
        var w=new ShellWindow();w.InitializeMarkdown();var origin=new EndpointOrigin(new Uri("https://public.invalid"));w.MarkdownGate.ConsentTo(origin);Assert.True(w.MarkdownGate.TryEnable(origin,true));return w;
    }
    private static string Source(bool table)=>table?"a|b\n---|---\nx|abcdef":"```cs\nabcdef";
    private static RequestIdentity Begin(ShellWindow w,string source)
    {
        Assert.True(w.MarkdownGate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var id,out _));Assert.True(w.MarkdownResults.Begin(id));w.ReplaceResult();w.ResetMarkdownReadingForTest();Assert.True(w.MarkdownResults.Append(id,source));w.RenderMarkdown();return id;
    }
    // Explicit geometry/selection inputs and method-call records, not a WPF model.
    private sealed class Local(UIElement element)
    {
        private TextBox? Code=>element as TextBox;
        private RichTextBox Table=>(RichTextBox)element;
        public double Horizontal=>Code?.HorizontalOffset??Table.HorizontalOffset;
        public double Vertical=>Code?.VerticalOffset??Table.VerticalOffset;
        public int EndCalls=>Code?.EndCalls??Table.EndCalls;
        public int Pending=>Code?.Dispatcher.Pending??Table.Dispatcher.Pending;
        public void Geometry(double extent,double viewport){if(Code is {} code){code.ExtentHeight=extent;code.ViewportHeight=viewport;}else{Table.ExtentHeight=extent;Table.ViewportHeight=viewport;}}
        public void At(double horizontal,double vertical){if(Code is {} code){code.ScrollToHorizontalOffset(horizontal);code.ScrollToVerticalOffset(vertical);}else{Table.ScrollToHorizontalOffset(horizontal);Table.ScrollToVerticalOffset(vertical);}}
        public void Select(int start,int length){if(Code is {} code)code.Select(start,length);else Table.Selection.Select(Table.Document.ContentStart.GetPositionAtOffset(start)!,Table.Document.ContentStart.GetPositionAtOffset(start+length)!);}
        public (int,int) Selection=>Code is {} code?(code.SelectionStart,code.SelectionLength):(Table.Selection.Start.Offset,Table.Selection.End.Offset-Table.Selection.Start.Offset);
        public void Loaded()=>((FrameworkElement)element).RaiseLoaded();
        public void Wheel(){if(Code is {} code)code.RaiseWheel();else Table.RaiseWheel();}
        public void Deliver(int index=0){if(Code is {} code)code.Dispatcher.Deliver(index);else Table.Dispatcher.Deliver(index);}
    }
    private static Local Region(RichTextBox host)=>new(Assert.Single(host.Document.Blocks.OfType<BlockUIContainer>()).Child);

    [Theory][InlineData(false)][InlineData(true)]
    public void ExplicitNewContentFollowsLocalTailWithoutChangingHorizontalOrSelection(bool table)
    {
        var w=Window();Begin(w,Source(table));var local=Region(w.MarkdownBody);local.Geometry(1200,200);local.Select(1,3);local.At(70,100);local.Wheel();Assert.False(w.Following);
        w.ReaderFollowEnd();Assert.True(w.Following);Assert.Equal(1,local.Pending);local.Deliver();Assert.Equal(1000,local.Vertical);Assert.Equal(70,local.Horizontal);Assert.Equal((1,3),local.Selection);Assert.Equal(1,local.EndCalls);Assert.Equal(0,Region(w.MarkdownMeasurement).Pending);
    }
    [Theory][InlineData(false,24,true)][InlineData(false,25,false)][InlineData(true,24,true)][InlineData(true,25,false)]
    public void LocalBottomThresholdIs24DipAndUsesPostLayoutExtent(bool table,double remaining,bool expected)
    {
        var w=Window();var id=Begin(w,Source(table));var local=Region(w.MarkdownBody);local.Geometry(1200,200);local.Select(1,2);local.At(77,1000-remaining);
        Assert.True(w.MarkdownResults.Append(id," more"));w.RenderMarkdown();local.Geometry(1500,200);local.At(0,0);local.Select(0,0);local.Deliver();
        Assert.Equal(expected?1300:1000-remaining,local.Vertical);Assert.Equal(expected?1:0,local.EndCalls);Assert.Equal(77,local.Horizontal);Assert.Equal((1,2),local.Selection);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void PausedReaderAtLocalBottomKeepsAnchorUntilExplicitFollow(bool table)
    {
        var w=Window();var id=Begin(w,Source(table));var local=Region(w.MarkdownBody);local.Geometry(1200,200);local.At(80,1000);local.Wheel();Assert.True(w.MarkdownResults.Append(id," more"));w.RenderMarkdown();local.Geometry(1500,200);local.Deliver();
        Assert.Equal(1000,local.Vertical);Assert.Equal(0,local.EndCalls);Assert.False(w.Following);w.ReaderFollowEnd();local.Deliver();Assert.Equal(1300,local.Vertical);Assert.Equal(80,local.Horizontal);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void ExplicitFollowSupersedesOldRestoreAndCoalescesFurtherAppendInReverseOrder(bool table)
    {
        var w=Window();var id=Begin(w,Source(table));var local=Region(w.MarkdownBody);local.Geometry(1200,200);local.Select(1,2);local.At(70,100);local.Wheel();Assert.True(w.MarkdownResults.Append(id," more"));w.RenderMarkdown();
        local.At(70,100);local.Select(1,2);w.ReaderFollowEnd();Assert.True(w.MarkdownResults.Append(id," again"));w.RenderMarkdown();local.Geometry(1800,200);Assert.Equal(3,local.Pending);
        local.Deliver(2);local.Deliver(1);local.Deliver();Assert.Equal(1600,local.Vertical);Assert.Equal(1,local.EndCalls);Assert.Equal(70,local.Horizontal);Assert.Equal((1,2),local.Selection);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void BottomDecisionAndSelectionCoalesceAcrossTwoAppendsBeforeLayout(bool table)
    {
        var w=Window();var id=Begin(w,Source(table));var local=Region(w.MarkdownBody);local.Geometry(1200,200);local.Select(1,2);local.At(88,990);
        Assert.True(w.MarkdownResults.Append(id," more"));w.RenderMarkdown();local.At(0,0);local.Select(0,0);local.Geometry(1500,200);Assert.True(w.MarkdownResults.Append(id," again"));w.RenderMarkdown();local.Geometry(1800,200);
        local.Deliver(1);local.Deliver();Assert.Equal(1600,local.Vertical);Assert.Equal(1,local.EndCalls);Assert.Equal(88,local.Horizontal);Assert.Equal((1,2),local.Selection);
    }
    [Theory][InlineData(false,"local")][InlineData(true,"local")][InlineData(false,"parent")][InlineData(true,"parent")]
    [InlineData(false,"version")][InlineData(true,"version")][InlineData(false,"lock")][InlineData(true,"lock")]
    [InlineData(false,"exit")][InlineData(true,"exit")][InlineData(false,"clear")][InlineData(true,"clear")]
    public void InputVersionLockExitAndClearFenceDeferredExplicitFollow(bool table,string change)
    {
        var w=Window();Begin(w,Source(table));var local=Region(w.MarkdownBody);local.Geometry(1200,200);w.ReaderFollowEnd();Assert.Equal(1,local.Pending);
        switch(change){case "local":local.Wheel();break;case "parent":w.ObserveReaderInput();break;case "version":Begin(w,Source(table)+" replacement");break;case "lock":w.Lock();break;case "exit":w.Exit();break;case "clear":w.MarkdownResults.Clear();w.DropMarkdown();break;}
        local.At(90,100);local.Deliver();Assert.Equal(0,local.EndCalls);Assert.Equal(90,local.Horizontal);Assert.Equal(100,local.Vertical);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void InitialLoadedTailFollowsButMeasurementAndPausedReloadNeverDo(bool table)
    {
        var w=Window();Begin(w,Source(table));var local=Region(w.MarkdownBody);var measure=Region(w.MarkdownMeasurement);local.At(70,0);local.Geometry(1200,200);measure.Loaded();Assert.Equal(0,measure.Pending);
        local.Loaded();Assert.Equal(1,local.Pending);local.Deliver();Assert.Equal(1000,local.Vertical);Assert.Equal(70,local.Horizontal);local.Wheel();local.At(70,100);local.Loaded();Assert.Equal(0,local.Pending);Assert.Equal(100,local.Vertical);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void NewProseTailRejectsOldLocalFollowAndDoesNotScrollStableEmbeddedBlocks(bool table)
    {
        var w=Window();var id=Begin(w,Source(table));var local=Region(w.MarkdownBody);local.Select(1,2);local.At(80,100);w.ReaderFollowEnd();Assert.Equal(1,local.Pending);
        string ending=table?"\n\nprose":"\n```\n\nprose";Assert.True(w.MarkdownResults.Append(id,ending));w.RenderMarkdown();while(local.Pending>0)local.Deliver();Assert.Equal(0,local.EndCalls);
        w.ReaderFollowEnd();local.Loaded();Assert.Equal(0,local.Pending);Assert.Equal(0,local.EndCalls);Assert.Equal(80,local.Horizontal);Assert.Equal(100,local.Vertical);Assert.Equal((1,2),local.Selection);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void ThemeReloadDoesNotJumpWhenGlobalFollowRemainsTrueButLocalLayoutMoved(bool table)
    {
        var w=Window();Begin(w,Source(table));var local=Region(w.MarkdownBody);local.Loaded();local.Deliver();Assert.Equal(1,local.EndCalls);local.At(70,975);local.Loaded();Assert.True(w.Following);Assert.Equal(0,local.Pending);Assert.Equal(975,local.Vertical);
    }
    [Fact]public void PartialPipeRowPromotesRetainedTableToTailBeforeBottomDecision()
    {
        var w=Window();var id=Begin(w,"a|b\n---|---\nx|y\npartial");Assert.Equal(2,w.MarkdownBody.Document.Blocks.Count);var local=Region(w.MarkdownBody);local.Geometry(1200,200);local.At(70,990);
        Assert.True(w.MarkdownResults.Append(id,"|row\n"));w.RenderMarkdown();Assert.Single(w.MarkdownBody.Document.Blocks);local.Geometry(1500,200);local.Deliver();Assert.Equal(1300,local.Vertical);Assert.Equal(1,local.EndCalls);Assert.Equal(70,local.Horizontal);
    }
    [Fact]public void ExplicitFollowOnlyTouchesLastRegionInMixedNativeDocument()
    {
        var w=Window();Begin(w,"```\nold code\n```\n\n"+Source(true));var regions=w.MarkdownBody.Document.Blocks.OfType<BlockUIContainer>().Select(b=>new Local(b.Child)).ToArray();Assert.Equal(2,regions.Length);regions[0].At(60,50);regions[1].At(80,100);
        w.ReaderFollowEnd();Assert.Equal(0,regions[0].Pending);Assert.Equal(1,regions[1].Pending);regions[1].Deliver();Assert.Equal(50,regions[0].Vertical);Assert.Equal(1000,regions[1].Vertical);Assert.Equal(80,regions[1].Horizontal);
    }
}
