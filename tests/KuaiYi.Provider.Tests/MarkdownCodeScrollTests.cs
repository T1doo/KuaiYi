using KuaiYi.Core;
using KuaiYi.App;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class MarkdownCodeScrollTests
{
    private const string Source="paragraph wraps\n\n```cs\n**literal** <img> [link](file:///inert)\n```\n";
    private static ShellWindow Window()
    {
        var w=new ShellWindow();w.InitializeMarkdown();var origin=new EndpointOrigin(new Uri("https://public.invalid"));w.MarkdownGate.ConsentTo(origin);Assert.True(w.MarkdownGate.TryEnable(origin,true));return w;
    }
    private static RequestIdentity Begin(ShellWindow w,string source,bool complete=false,Guid? session=null)
    {
        Assert.True(w.MarkdownGate.TryStartRequest(session??Guid.NewGuid(),Guid.NewGuid(),null,out var id,out _));Assert.True(w.MarkdownResults.Begin(id));w.ReplaceResult();w.ResetMarkdownReadingForTest();Assert.True(w.MarkdownResults.Append(id,source));if(complete)Assert.True(w.MarkdownResults.Complete(id));return id;
    }
    private static TextBox Code(RichTextBox host)=>Assert.IsType<TextBox>(Assert.Single(host.Document.Blocks.OfType<BlockUIContainer>()).Child);
    private static LayoutPlan Plan(double dpi)=>new("public",new(0,0,2000,2000),new(0,0,48,48),new(0,0,(int)(420*dpi),(int)(640*dpi)),GrowthDirection.RightDown,new(dpi),(int)(640*dpi));
    [Fact]public void LongCodeIsLocalNoWrapReadOnlyAndProseHasFinitePageWidthInBothDocuments()
    {
        string line=new('x',50000);string source="ordinary prose\n\n```\n"+line+"\n```\n";var w=Window();Begin(w,source);w.MarkdownViewportForTest(new(300,400));w.RenderMarkdown();
        foreach(var host in new[]{w.MarkdownBody,w.MarkdownMeasurement})
        {
            Assert.IsType<Paragraph>(host.Document.Blocks[0]);var code=Code(host);Assert.Equal(line+"\n",code.Text);
            Assert.True(code.IsReadOnly);Assert.False(code.IsUndoEnabled);Assert.False(code.AllowDrop);Assert.True(code.IsReadOnlyCaretVisible);Assert.Equal(TextWrapping.NoWrap,code.TextWrapping);
            Assert.Equal(ScrollBarVisibility.Visible,code.HorizontalScrollBarVisibility);Assert.Equal(ScrollBarVisibility.Visible,code.VerticalScrollBarVisibility);
            Assert.Equal(283,code.Width);Assert.Equal(code.Width,code.MaxWidth);Assert.Equal(240,code.MaxHeight);Assert.True(code.ClipToBounds);
            Assert.Equal(300,host.Width);Assert.Equal(300,host.MaxWidth);Assert.Equal(400,host.MaxHeight);Assert.Equal(ScrollBarVisibility.Disabled,host.HorizontalScrollBarVisibility);Assert.Equal(ScrollBarVisibility.Visible,host.VerticalScrollBarVisibility);Assert.True(host.IsDocumentEnabled);Assert.True(host.IsReadOnly);Assert.False(host.IsUndoEnabled);
            Assert.Equal(283,host.Document.PageWidth);Assert.Equal(283,host.Document.ColumnWidth);Assert.Equal(15,code.FontSize);
        }
        Assert.NotSame(Code(w.MarkdownBody),Code(w.MarkdownMeasurement));Assert.Equal(source,w.MarkdownResults.Text);
    }
    [Theory][InlineData(1,false)][InlineData(1.5,true)][InlineData(2,false)]
    public void ActualMeasureBridgeUsesTheSameFinitePlanConstraintsForBothHosts(double dpi,bool label)
    {
        var w=Window();Begin(w,Source);w.RenderMarkdown();w.MarkdownMeasurement.DesiredSize=new(123,42);
        Assert.Equal(42,w.MeasureMarkdownForTest(Plan(dpi),label));w.RenderMarkdown();double height=560-(label?20:0);
        Assert.Equal(new Size(388,height),w.MarkdownMeasurement.LastMeasure);
        foreach(var host in new[]{w.MarkdownBody,w.MarkdownMeasurement}){Assert.Equal(388,host.Width);Assert.Equal(height,host.MaxHeight);Assert.Equal(371,Code(host).Width);Assert.Equal(240,Code(host).MaxHeight);}
    }
    [Theory][InlineData(40,60,true,8,1)][InlineData(180,200,false,148,120)]
    public void SmallCardsKeepCodeAndDocumentConstraintsFinite(double width,double height,bool label,double expectedWidth,double expectedHeight)
    {
        var w=Window();Begin(w,Source);var viewport=MarkdownViewport.FromCard(width,height,label);w.MarkdownViewportForTest(viewport);w.RenderMarkdown();
        Assert.Equal(expectedWidth,viewport.WidthDip);Assert.Equal(expectedHeight,viewport.MaximumHeightDip);
        foreach(var host in new[]{w.MarkdownBody,w.MarkdownMeasurement}){Assert.InRange(Code(host).Width,1,expectedWidth);Assert.Equal(expectedHeight,Code(host).MaxHeight);Assert.Equal(expectedWidth,host.Width);}
    }
    [Theory][InlineData(0,100)][InlineData(-1,100)][InlineData(double.NaN,100)][InlineData(double.PositiveInfinity,100)]
    [InlineData(100,0)][InlineData(100,-1)][InlineData(100,double.NaN)][InlineData(100,double.PositiveInfinity)]
    public void InvalidConstraintsRejectBeforeChangingEitherDocument(double width,double height)
    {
        var w=Window();Begin(w,Source);w.RenderMarkdown();var code=Code(w.MarkdownBody);double old=code.Width;
        Assert.Throws<ArgumentOutOfRangeException>(()=>w.MarkdownViewportForTest(new(width,height)));Assert.Same(code,Code(w.MarkdownBody));Assert.Equal(old,code.Width);Assert.Equal(old,Code(w.MarkdownMeasurement).Width);
    }
    [Fact]public void StableCodeSurvivesTailAndViewportFontThemeUpdatesWithoutExplicitOffsetReset()
    {
        var w=Window();var id=Begin(w,Source+"tail");w.RenderMarkdown();var code=Code(w.MarkdownBody);code.ScrollToHorizontalOffset(70);
        Assert.True(w.MarkdownResults.Append(id," grows"));w.RenderMarkdown();Assert.Same(code,Code(w.MarkdownBody));
        w.MarkdownViewportForTest(new(250,180));w.MarkdownSize(20);w.MarkdownBody.Foreground=w.MarkdownMeasurement.Foreground=Brushes.White;w.RenderMarkdown();
        Assert.Same(code,Code(w.MarkdownBody));Assert.Equal(70,code.HorizontalOffset);Assert.Equal(233,code.Width);Assert.Equal(180,code.MaxHeight);
        foreach(var host in new[]{w.MarkdownBody,w.MarkdownMeasurement}){Assert.Equal(20,Code(host).FontSize);Assert.Equal(Brushes.White,Code(host).Foreground);Assert.Equal("Consolas",Code(host).FontFamily!.Source);}
    }
    [Fact]public void VersionReplacementClearsOldControlsHandlersOffsetsAndLateInput()
    {
        var w=Window();var id=Begin(w,"```\nold");w.RenderMarkdown();var old=Code(w.MarkdownBody);var oldMeasure=Code(w.MarkdownMeasurement);var late=old.CaptureWheel();old.ScrollToHorizontalOffset(50);old.ScrollToVerticalOffset(20);old.Select(1,2);
        Begin(w,"```\nnew");w.RenderMarkdown();Assert.NotSame(old,Code(w.MarkdownBody));Assert.Equal("new",Code(w.MarkdownBody).Text);
        Assert.Equal("",old.Text);Assert.Equal("",oldMeasure.Text);Assert.Equal(0,old.ReaderHandlerCount);Assert.Equal(0,oldMeasure.ReaderHandlerCount);Assert.Equal(0,old.HorizontalOffset);Assert.Equal(0,old.VerticalOffset);Assert.Equal(0,old.SelectionLength);Assert.Equal(0,old.SelectionStart);
        w.QueueScroll(false,55);late();w.Deliver();Assert.Equal(55,w.ScrollOffset);Assert.Equal(1,w.ScrollCalls);
    }
    [Fact]public void CapacityFallbackRemovesCodeRegionsAndRetainsCompleteRawText()
    {
        var w=Window();var id=Begin(w,"```\ncode");w.RenderMarkdown();var old=Code(w.MarkdownBody);
        Assert.True(w.MarkdownResults.Append(id,new string('x',RestrictedMarkdown.MaximumFormattedCharacters)));w.RenderMarkdown();
        Assert.True(w.MarkdownModel.CapacityFallback);Assert.Empty(w.MarkdownBody.Document.Blocks.OfType<BlockUIContainer>());Assert.Equal("",old.Text);Assert.Equal(0,old.ReaderHandlerCount);
        Assert.Equal(w.MarkdownResults.Text,Assert.IsType<Run>(Assert.Single(Assert.IsType<Paragraph>(Assert.Single(w.MarkdownBody.Document.Blocks)).Inlines)).Text);
    }
    [Fact]public void ActualVersionSelectionFailureCancelClearAndLateEventsDoNotRetainCodeControls()
    {
        var w=Window();var session=Guid.NewGuid();var first=Begin(w,"```\nfirst\n```\n",true,session);w.RenderMarkdown();var old=Code(w.MarkdownBody);
        var second=Begin(w,"```\nsecond\n```\n",true,session);w.RenderMarkdown();Assert.Equal("",old.Text);var prior=Code(w.MarkdownBody);
        Assert.True(w.MarkdownResults.SelectVersion(first.VersionId));w.ReplaceResult();w.RenderMarkdown();Assert.Equal("",prior.Text);Assert.Equal("first\n",Code(w.MarkdownBody).Text);
        var failed=Begin(w,"```\nfailed",false,session);Assert.True(w.MarkdownResults.Fail(failed));w.RenderMarkdown();Assert.Equal("first\n",Code(w.MarkdownBody).Text);Assert.False(w.MarkdownResults.Append(failed,"late"));
        var active=Begin(w,"```\npartial",false,session);w.RenderMarkdown();var canceled=Code(w.MarkdownBody);canceled.RaiseWheel();w.ObserveReaderInput();w.MarkdownGate.Disable();w.MarkdownResults.MarkStopped();w.RenderMarkdown();Assert.False(w.MarkdownResults.Append(active,"late canceled"));
        w.MarkdownGate.SetSessionLocked(true);w.MarkdownResults.Clear();w.DropMarkdown();Assert.Equal("",canceled.Text);Assert.Equal(0,canceled.ReaderHandlerCount);Assert.Empty(w.MarkdownBody.Document.Blocks);Assert.Empty(w.MarkdownMeasurement.Document.Blocks);
        w.ChangeGeometry(1200,200,1000);w.Deliver();Assert.False(w.Following);Assert.False(w.MarkdownResults.Append(active,"late clear"));
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    [InlineData(6)][InlineData(7)][InlineData(8)][InlineData(9)][InlineData(10)][InlineData(11)][InlineData(12)][InlineData(13)][InlineData(14)]
    public void CodeInputDelegatesFenceExistingRestoreUsingProductionReaderRevision(int input)
    {
        var w=Window();Begin(w,Source);w.RenderMarkdown();var code=Code(w.MarkdownBody);w.QueueScroll(true,800);
        switch(input)
        {
            case 0:code.RaiseWheel();break;case 1:code.RaiseMouse(MouseButton.Left);break;case 2:code.RaiseMove(MouseButtonState.Pressed);break;
            case 3:code.RaiseTouch();break;case 4:code.RaiseTouchMove();break;case 5:code.RaiseScroll();break;
            case 14:try{Keyboard.Modifiers=ModifierKeys.Control;code.RaiseKey(Key.A);}finally{Keyboard.Modifiers=ModifierKeys.None;}break;
            default:code.RaiseKey(new[]{Key.Up,Key.Down,Key.Left,Key.Right,Key.PageUp,Key.PageDown,Key.Home,Key.End}[input-6]);break;
        }
        Assert.False(w.Following);w.Deliver();Assert.Equal(0,w.ScrollCalls);w.ObserveReaderInput();w.ChangeGeometry(1200,200,1000);w.Deliver();Assert.False(w.Following);
    }
    [Fact]public void MeasurementInputAndNonReadingKeysDoNotAffectReaderPolicy()
    {
        var w=Window();Begin(w,Source);w.RenderMarkdown();Code(w.MarkdownMeasurement).RaiseWheel();var code=Code(w.MarkdownBody);code.RaiseMouse(MouseButton.Right);code.RaiseMove(MouseButtonState.Released);code.RaiseKey(Key.C);code.RaiseKey(Key.A);Assert.True(w.Following);Assert.Equal(0,w.ScrollCalls);
    }
    [Fact]public void DoubleObservationAndClearFencePendingCallbacksInReverseDeliveryOrder()
    {
        var w=Window();Begin(w,Source);w.RenderMarkdown();w.ObserveReaderInput();Code(w.MarkdownBody).RaiseScroll();w.QueueScroll(false,90);
        w.Deliver(1);w.Deliver();Assert.Equal(90,w.ScrollOffset);Assert.Equal(1,w.ScrollCalls);Assert.False(w.Following);
        Code(w.MarkdownBody).RaiseWheel();w.ObserveReaderInput();w.MarkdownResults.Clear();w.DropMarkdown();w.ChangeGeometry(1200,200,1000);w.Deliver();Assert.False(w.Following);
    }
    [Fact]public void OpenCodeTailRetainsControlAndCoalescedSelectionRestoreAcrossAppends()
    {
        var w=Window();var id=Begin(w,"```\nabcdef");w.RenderMarkdown();var code=Code(w.MarkdownBody);code.Select(1,3);code.ScrollToHorizontalOffset(70);code.ScrollToVerticalOffset(20);
        Assert.True(w.MarkdownResults.Append(id," more"));w.RenderMarkdown();Assert.True(w.MarkdownResults.Append(id," again"));w.RenderMarkdown();Assert.Same(code,Code(w.MarkdownBody));Assert.Equal("abcdef more again",code.Text);Assert.Equal(0,Code(w.MarkdownMeasurement).Dispatcher.Pending);
        w.QueueScroll(false,50);code.Dispatcher.Deliver(1);code.Dispatcher.Deliver();Assert.Equal(1,code.SelectionStart);Assert.Equal(3,code.SelectionLength);Assert.Equal(70,code.HorizontalOffset);Assert.Equal(20,code.VerticalOffset);w.Deliver();Assert.Equal(50,w.ScrollOffset);
    }
    [Fact]public void NewReaderInputAndClearFenceDeferredLocalRestores()
    {
        var w=Window();var id=Begin(w,"```\nabcdef");w.RenderMarkdown();var code=Code(w.MarkdownBody);code.Select(1,2);code.ScrollToHorizontalOffset(30);
        Assert.True(w.MarkdownResults.Append(id," more"));w.RenderMarkdown();code.RaiseWheel();code.Select(3,1);code.ScrollToHorizontalOffset(90);code.Dispatcher.Deliver();Assert.Equal(3,code.SelectionStart);Assert.Equal(90,code.HorizontalOffset);
        Assert.True(w.MarkdownResults.Append(id," again"));w.RenderMarkdown();w.MarkdownResults.Clear();w.DropMarkdown();code.Dispatcher.Deliver();Assert.Equal("",code.Text);Assert.Equal(0,code.SelectionLength);Assert.Equal(0,code.HorizontalOffset);
    }
    [Fact]public void LocalReaderPauseOnlyResumesExplicitlyOrOnNewResult()
    {
        var w=Window();Begin(w,Source);w.RenderMarkdown();Code(w.MarkdownBody).RaiseScroll();w.ObserveReaderInput();w.ChangeGeometry(1200,200,1000);w.Deliver();Assert.False(w.Following);
        w.ReaderFollowEnd();Assert.True(w.Following);Code(w.MarkdownBody).RaiseWheel();Assert.False(w.Following);Begin(w,Source);w.RenderMarkdown();w.ObserveReaderInput();w.Deliver();Assert.True(w.Following);
    }
    [Theory][InlineData("reader")][InlineData("version")][InlineData("lock")][InlineData("exit")]
    public void ParentReaderVersionLockAndExitGuardsRejectDeferredLocalRestore(string change)
    {
        var w=Window();var id=Begin(w,"```\nabcdef");w.RenderMarkdown();var code=Code(w.MarkdownBody);code.Select(1,2);code.ScrollToHorizontalOffset(30);
        Assert.True(w.MarkdownResults.Append(id," more"));w.RenderMarkdown();Assert.Equal(1,code.Dispatcher.Pending);
        switch(change){case "reader":w.ObserveReaderInput();break;case "version":Begin(w,"```\nreplacement");w.RenderMarkdown();break;case "lock":w.Lock();break;case "exit":w.Exit();break;}
        code.Dispatcher.Deliver();Assert.Equal(0,code.SelectionLength);Assert.Equal(0,code.HorizontalOffset);
    }
}
