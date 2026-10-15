using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.App;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;

namespace KuaiYi.App
{
    internal sealed partial class ShellWindow
    {
        private OverlayResults overlayResults=null!;
        internal RichTextBox MarkdownBody {get;}=new();
        internal RichTextBox MarkdownMeasurement {get;}=new();
        private RichTextBox measurement=>MarkdownMeasurement;
        internal RuntimeGate MarkdownGate=>lifecycle.Gate;
        internal OverlayResults MarkdownResults=>overlayResults;
        internal RestrictedMarkdown MarkdownModel=>markdown;
        internal void InitializeMarkdown()
        {overlayResults=new(lifecycle.Gate);bodyMarkdown=new(MarkdownBody,ObserveCodeReaderIntent,CaptureBodyReaderGuard,()=>follow,OriginalSourceChanged,CanReadOriginalSourceVersion);measurementMarkdown=new(MarkdownMeasurement,canReadSource:CanReadOriginalSourceVersion);}
        internal void MarkdownViewportForTest(MarkdownViewport viewport)=>SetMarkdownViewport(viewport);
        internal double MeasureMarkdownForTest(LayoutPlan plan,bool label)=>MeasureMarkdown(plan,label);
        internal void ResetMarkdownReadingForTest()=>ResetBodyReaderFollow();
        internal void RenderMarkdown(bool renderBody=true)=>RefreshMarkdown(renderBody);
        internal void DropMarkdown()=>ClearMarkdown();
        internal void MarkdownSize(double size)=>lifecycle.Settings=new(new(){Overlay=new(){FontSizeDip=size}},"public",SettingsLoadState.Current);
    }
}
namespace KuaiYi.Provider.Tests
{
    public sealed class MarkdownProductionBridgeTests
    {
        private static ShellWindow Window()
        {
            var window=new ShellWindow();window.InitializeMarkdown();var origin=new EndpointOrigin(new Uri("https://public.invalid"));
            window.MarkdownGate.ConsentTo(origin);Assert.True(window.MarkdownGate.TryEnable(origin,true));return window;
        }
        private static RequestIdentity Begin(ShellWindow window,Guid session,string text,bool complete=true)
        {
            Assert.True(window.MarkdownGate.TryStartRequest(session,Guid.NewGuid(),null,out var identity,out _));
            Assert.True(window.MarkdownResults.Begin(identity));Assert.True(window.MarkdownResults.Append(identity,text));
            if(complete)Assert.True(window.MarkdownResults.Complete(identity));return identity;
        }
        private static Paragraph First(RichTextBox host)=>Assert.IsType<Paragraph>(host.Document.Blocks[0]);
        private static string Display(RichTextBox host)=>string.Concat(host.Document.Blocks.Select(Text));
        private static string Text(Block block)=>block switch
        {
            Paragraph p=>string.Concat(p.Inlines.Select(i=>i is Run r?r.Text:string.Concat(Assert.IsType<Span>(i).Inlines.Select(n=>Assert.IsType<Run>(n).Text)))),
            System.Windows.Documents.List list=>Text(list.Item.Paragraph),
            _=>throw new InvalidOperationException("Unexpected native port block")
        };
        [Fact]public void ActualViewConstructsStyledNativeRunsAndSpansForBodyAndMeasurement()
        {
            var window=Window();Begin(window,Guid.NewGuid(),"# **bold** *italic* `**literal**` and text");window.RenderMarkdown();
            foreach(var host in new[]{window.MarkdownBody,window.MarkdownMeasurement})
            {
                var paragraph=First(host);Assert.Equal("SemiBold",paragraph.FontWeight);
                Assert.Equal("Bold",Assert.IsType<Span>(paragraph.Inlines[0]).FontWeight);
                Assert.IsType<Run>(paragraph.Inlines[1]);Assert.Equal("Italic",Assert.IsType<Span>(paragraph.Inlines[2]).FontStyle);
                var code=Assert.IsType<Span>(paragraph.Inlines[4]);Assert.Equal("Consolas",code.FontFamily!.Source);Assert.Equal("Normal",code.FontWeight);Assert.Equal("Normal",code.FontStyle);
                Assert.Equal("**literal**",Assert.IsType<Run>(Assert.Single(code.Inlines)).Text);Assert.Equal("bold italic **literal** and text",Display(host));
            }
            Assert.NotSame(window.MarkdownBody.Document,window.MarkdownMeasurement.Document);Assert.NotSame(First(window.MarkdownBody),First(window.MarkdownMeasurement));
            Assert.Equal("# **bold** *italic* `**literal**` and text",window.MarkdownResults.Text);
        }
        [Fact]public void ActualViewPreservesStableNativeBlocksAndUpdatesBothFontsWhenPreferenceChanges()
        {
            var window=Window();var identity=Begin(window,Guid.NewGuid(),"# **stable**\n\n*tail",false);window.RenderMarkdown();
            var body=First(window.MarkdownBody);var measurement=First(window.MarkdownMeasurement);var span=body.Inlines[0];var tail=window.MarkdownBody.Document.Blocks[^1];
            Assert.True(window.MarkdownResults.Append(identity,"*"));window.RenderMarkdown();
            Assert.Same(body,First(window.MarkdownBody));Assert.Same(measurement,First(window.MarkdownMeasurement));Assert.Same(span,First(window.MarkdownBody).Inlines[0]);Assert.NotSame(tail,window.MarkdownBody.Document.Blocks[^1]);
            window.MarkdownSize(20);window.RenderMarkdown();Assert.Same(body,First(window.MarkdownBody));Assert.Equal(30,body.FontSize);Assert.Equal(30,measurement.FontSize);Assert.Equal(20,window.MarkdownBody.Document.FontSize);
        }
        [Fact]public void ActualMeasureOnlyBridgeDoesNotReplaceVisibleBodyUntilFlush()
        {
            var window=Window();var identity=Begin(window,Guid.NewGuid(),"**one**",false);window.RenderMarkdown();var old=First(window.MarkdownBody);
            Assert.True(window.MarkdownResults.Append(identity," *two*"));window.RenderMarkdown(false);Assert.Same(old,First(window.MarkdownBody));Assert.Equal("one",Display(window.MarkdownBody));Assert.Equal("one two",Display(window.MarkdownMeasurement));
            window.RenderMarkdown();Assert.Equal("one two",Display(window.MarkdownBody));
        }
        [Fact]public void ActualSelectedThreeVersionCacheKeepsRawCopyFailedFallbackAndLateEventsIsolated()
        {
            var window=Window();var session=Guid.NewGuid();var ids=new List<RequestIdentity>();
            for(int i=0;i<4;i++)ids.Add(Begin(window,session,$"**version {i}** `literal *x*`"));
            Assert.Equal(3,window.MarkdownResults.Versions.Count);Assert.DoesNotContain(window.MarkdownResults.Versions,v=>v.Identity==ids[0]);
            Assert.True(window.MarkdownResults.SelectVersion(ids[1].VersionId));window.RenderMarkdown();Assert.Equal("version 1 literal *x*",Display(window.MarkdownBody));
            var failed=Begin(window,session,"*failed",false);Assert.True(window.MarkdownResults.Fail(failed));window.RenderMarkdown();
            Assert.Equal(ids[1].VersionId,window.MarkdownResults.SelectedVersionId);Assert.Equal("**version 1** `literal *x*`",window.MarkdownResults.Text);Assert.Equal("version 1 literal *x*",Display(window.MarkdownMeasurement));
            Assert.False(window.MarkdownResults.Append(ids[0],"late evicted"));Assert.False(window.MarkdownResults.Append(failed,"* late failed"));window.RenderMarkdown();Assert.Equal("version 1 literal *x*",Display(window.MarkdownBody));
        }
        [Fact]public void ActualCancelStopsUnclosedTailAndClearDropsModelAndBothDocuments()
        {
            var window=Window();var identity=Begin(window,Guid.NewGuid(),"# **closed**\n\n*unclosed",false);window.RenderMarkdown();
            window.MarkdownGate.Disable();window.MarkdownResults.MarkStopped();window.RenderMarkdown();Assert.False(window.MarkdownResults.Append(identity,"* late"));
            Assert.Equal("closed*unclosed",Display(window.MarkdownBody));Assert.Equal(MarkdownBlockKind.Plain,window.MarkdownModel.Blocks[^1].Kind);
            window.MarkdownGate.SetSessionLocked(true);window.MarkdownResults.Clear();window.DropMarkdown();
            Assert.Empty(window.MarkdownResults.Versions);Assert.Empty(window.MarkdownModel.Blocks);Assert.Empty(window.MarkdownBody.Document.Blocks);Assert.Empty(window.MarkdownMeasurement.Document.Blocks);
            Assert.False(window.MarkdownResults.Append(identity,"late after clear"));window.RenderMarkdown();Assert.Empty(window.MarkdownBody.Document.Blocks);Assert.Empty(window.MarkdownMeasurement.Document.Blocks);
        }
        [Fact]public void ActualRendererLeavesUnsupportedBlocksAndFencedCodeAsLiteralText()
        {
            var window=Window();Begin(window,Guid.NewGuid(),"# **good** [link](file:///inert)\n\n```\n**literal** <img>\n```");window.RenderMarkdown();
            var literal=First(window.MarkdownBody);Assert.Equal("# **good** [link](file:///inert)\n",Assert.IsType<Run>(Assert.Single(literal.Inlines)).Text);
            var code=Assert.IsType<TextBox>(Assert.IsType<BlockUIContainer>(window.MarkdownBody.Document.Blocks[^1]).Child);Assert.Equal("Consolas",code.FontFamily!.Source);Assert.Equal("**literal** <img>\n",code.Text);Assert.True(code.IsReadOnly);Assert.False(code.IsUndoEnabled);
        }
    }
}
