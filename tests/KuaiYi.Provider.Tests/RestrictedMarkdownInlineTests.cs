using KuaiYi.Core;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class RestrictedMarkdownInlineTests
{
    private static readonly Guid Version=Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static RestrictedMarkdown Parse(string text,bool complete=true)
    {var model=new RestrictedMarkdown();model.Update(Version,text,complete);return model;}
    private static void Conserved(RestrictedMarkdown model,string source)
    {
        Assert.Equal(source,string.Concat(model.Blocks.Select(b=>b.Source)));
        foreach(var block in model.Blocks.Where(b=>b.Inlines.Count>0))
        {
            Assert.Equal(block.Text,string.Concat(block.Inlines.Select(p=>p.Text)));
            Assert.Contains(string.Concat(block.Inlines.Select(p=>p.Source)),block.Source,StringComparison.Ordinal);
            if(block.Kind==MarkdownBlockKind.Paragraph)Assert.Equal(block.Source.TrimEnd('\r','\n'),string.Concat(block.Inlines.Select(p=>p.Source)));
        }
    }
    private static void SameDocument(RestrictedMarkdown expected,RestrictedMarkdown actual)
    {
        Assert.Equal(expected.CapacityFallback,actual.CapacityFallback);Assert.Equal(expected.Blocks.Count,actual.Blocks.Count);
        for(int i=0;i<expected.Blocks.Count;i++)
        {
            var a=expected.Blocks[i];var b=actual.Blocks[i];
            Assert.Equal((a.Kind,a.Source,a.Text,a.Level,a.Number,a.Closed),(b.Kind,b.Source,b.Text,b.Level,b.Number,b.Closed));Assert.Equal(a.Inlines,b.Inlines);
        }
    }
    [Theory][InlineData("**bold**",MarkdownInlineKind.Bold,"bold")][InlineData("__bold__",MarkdownInlineKind.Bold,"bold")]
    [InlineData("*斜体🙂*",MarkdownInlineKind.Italic,"斜体🙂")][InlineData("_italic_",MarkdownInlineKind.Italic,"italic")]
    [InlineData("`x **y** _z_ \\ [link](file:///inert) <img>|`",MarkdownInlineKind.Code,"x **y** _z_ \\ [link](file:///inert) <img>|")]
    [InlineData("``x ` y``",MarkdownInlineKind.Code,"x ` y")]
    public void SupportedPiecesHaveExplicitKindsSourceAndDisplay(string source,MarkdownInlineKind kind,string display)
    {
        var block=Assert.Single(Parse(source).Blocks);Assert.Equal(MarkdownBlockKind.Paragraph,block.Kind);
        Assert.Equal(source,block.Source);Assert.Equal(display,block.Text);Assert.Equal(new(kind,source,display),Assert.Single(block.Inlines));
    }
    [Fact]public void ProseHeadingsAndListsShareTheSameFlatInlineModel()
    {
        var model=Parse("# **标题** and `x`\r\n- *item*\r\n7. __next__\r\n\r\n中文**重点**中文 *斜体*。");
        Conserved(model,"# **标题** and `x`\r\n- *item*\r\n7. __next__\r\n\r\n中文**重点**中文 *斜体*。");
        Assert.Equal(new[]{MarkdownBlockKind.Heading,MarkdownBlockKind.Bullet,MarkdownBlockKind.Numbered,MarkdownBlockKind.Paragraph,MarkdownBlockKind.Paragraph},model.Blocks.Select(b=>b.Kind));
        Assert.Equal("标题 and x",model.Blocks[0].Text);Assert.Equal("item",model.Blocks[1].Text);Assert.Equal(7,model.Blocks[2].Number);Assert.Equal("中文重点中文 斜体。",model.Blocks[4].Text);
        Assert.Equal("**标题** and `x`",string.Concat(model.Blocks[0].Inlines.Select(p=>p.Source)));
    }
    [Fact]public void EscapedPunctuationRendersLiterallyAndUnknownEscapesKeepTheirSlash()
    {
        const string source="\\*literal\\* \\_under\\_ \\`tick\\` \\[x\\] \\<tag\\> \\| \\q \\\\ **b\\*old** *i\\_talic*";
        var model=Parse(source);var block=Assert.Single(model.Blocks);Assert.Equal(MarkdownBlockKind.Paragraph,block.Kind);
        Assert.Equal("*literal* _under_ `tick` [x] <tag> | \\q \\ b*old i_talic",block.Text);Conserved(model,source);Assert.Equal(source,string.Concat(block.Inlines.Select(p=>p.Source)));
    }
    [Theory][InlineData("# **unfinished")][InlineData("- *unfinished")][InlineData("1. `unfinished")]
    [InlineData("# **good** and ***ambiguous***")][InlineData("- **bold *nested* text**")][InlineData("**a _b** c_")]
    [InlineData("# **bold `code`**")][InlineData("foo_bar_baz")][InlineData("* white*")][InlineData("**trailing **")]
    [InlineData("**line\nbreak**")][InlineData("`line\nbreak`")][InlineData("# **bold** [link](https://public.invalid)")]
    [InlineData("# **bold** <img src='https://public.invalid'>")][InlineData("- `ok` |table|")]
    public void UnsupportedAmbiguousAndUnclosedBlocksFallBackInFull(string source)
    {var model=Parse(source);var block=Assert.Single(model.Blocks);Assert.Equal(MarkdownBlockKind.Plain,block.Kind);Assert.Equal(source,block.Source);Assert.Equal(source,block.Text);Assert.Empty(block.Inlines);Conserved(model,source);}

    public static IEnumerable<object[]> StreamingSamples()=>new[]{
        "# **标题🙂** *斜体* `x`\r\n\r\n- __one__\r\n2. _two_\r\n\r\n尾**段**",
        "escape \\*x\\* \\\\*yes* and **a\\*b** `\\**inert**`", "``x ` y **z**`` *ok*", "***ambiguous***",
        "**good** [bad](file:///inert)", "unfinished ` **x**", "a\r\n\r\n__b__\r", "**a *b** c*", "*🙂* \\_x\\_"
    }.Select(s=>new object[]{s});
    [Theory][MemberData(nameof(StreamingSamples))]
    public void EverySplitAndUtf16UnitFeedHasTheSameFinalProductionDocument(string source)
    {
        var expected=Parse(source);
        for(int split=0;split<=source.Length;split++)
        {
            var model=Parse(source[..split],false);Conserved(model,source[..split]);model.Update(Version,source);Conserved(model,source);model.Update(Version,source,true);SameDocument(expected,model);
        }
        var units=new RestrictedMarkdown();string accumulated="";
        foreach(char c in source){accumulated+=c;units.Update(Version,accumulated);Conserved(units,accumulated);}
        units.Update(Version,source,true);SameDocument(expected,units);
    }
    [Fact]public void FixedSeedChunksExerciseEscapesCodeAndAmbiguityAcrossBoundaries()
    {
        string source=string.Concat(Enumerable.Repeat("# **标题🙂** *斜* `**inert**`\r\n\r\n- \\*literal\\*\r\n\r\n__tail__\r\n\r\n",12));var expected=Parse(source);var random=new Random(11515);
        for(int run=0;run<30;run++)
        {
            var model=new RestrictedMarkdown();int position=0;
            while(position<source.Length){position=Math.Min(source.Length,position+random.Next(1,27));model.Update(Version,source[..position]);Conserved(model,source[..position]);}
            model.Update(Version,source,true);SameDocument(expected,model);
        }
    }
    [Fact]public void ClosingAnUnfinishedTailReplacesOnlyThatTailAndKeepsStableInlineReferences()
    {
        const string prefix="# **stable**\r\n\r\n";var model=Parse(prefix+"*tail",false);var heading=model.Blocks[0];var inline=heading.Inlines[0];var tail=model.Blocks[^1];
        Assert.Equal(MarkdownBlockKind.Plain,tail.Kind);model.Update(Version,prefix+"*tail*");
        Assert.Same(heading,model.Blocks[0]);Assert.Same(inline,model.Blocks[0].Inlines[0]);Assert.NotSame(tail,model.Blocks[^1]);Assert.Equal(MarkdownInlineKind.Italic,Assert.Single(model.Blocks[^1].Inlines).Kind);
        var completedTail=model.Blocks[^1];model.Update(Version,prefix+"*tail*");Assert.Same(completedTail,model.Blocks[^1]);
    }
    [Fact]public void FencedCodeNeverParsesOrDecodesInlineMarkers()
    {
        const string content="**bold** *italics* `x` \\* [link](file:///inert) <img>\n";
        var block=Assert.Single(Parse("```\n"+content+"```").Blocks);Assert.Equal(MarkdownBlockKind.Code,block.Kind);Assert.Equal(content,block.Text);Assert.Empty(block.Inlines);
    }
    [Fact]public void InlineCapacityBoundaryAndOneMorePieceProduceCompleteDocumentFallback()
    {
        string text=string.Concat(Enumerable.Repeat("*x* ",RestrictedMarkdown.MaximumInlines/2));var model=Parse(text,false);
        Assert.False(model.CapacityFallback);Assert.Equal(RestrictedMarkdown.MaximumInlines,Assert.Single(model.Blocks).Inlines.Count);
        text+="*x*";model.Update(Version,text);Assert.True(model.CapacityFallback);var block=Assert.Single(model.Blocks);Assert.Equal(MarkdownBlockKind.Plain,block.Kind);Assert.Equal(text,block.Text);Conserved(model,text);
        model.Update(Guid.NewGuid(),"**reset**",true);Assert.False(model.CapacityFallback);Assert.Equal(MarkdownInlineKind.Bold,Assert.Single(Assert.Single(model.Blocks).Inlines).Kind);
    }
    [Fact]public void InlineCapacityIncludesStableBlocksAndTheChangedTail()
    {
        string prefix="# "+string.Concat(Enumerable.Repeat("*x* ",(RestrictedMarkdown.MaximumInlines-2)/2))+"\n";var model=Parse(prefix+"*y* ",false);
        Assert.False(model.CapacityFallback);Assert.Equal(1,model.StableCount);var first=model.Blocks[0];
        model.Update(Version,prefix+"*y* grows");Assert.Same(first,model.Blocks[0]);Assert.False(model.CapacityFallback);
        model.Update(Version,prefix+"*y* grows *q*");Assert.True(model.CapacityFallback);Assert.Equal(prefix+"*y* grows *q*",Assert.Single(model.Blocks).Text);
    }
    [Fact]public void CodeDelimiterBoundAndLongMalformedInputStayLiteralWithoutTruncation()
    {
        string delimiter=new('`',32);string text=delimiter+"x"+delimiter;var block=Assert.Single(Parse(text).Blocks);Assert.Equal(MarkdownInlineKind.Code,Assert.Single(block.Inlines).Kind);
        text=new string('`',33)+"x"+new string('`',33);block=Assert.Single(Parse(text).Blocks);Assert.Equal(MarkdownBlockKind.Plain,block.Kind);Assert.Equal(text,block.Text);
        text="**"+new string('x',RestrictedMarkdown.MaximumFormattedCharacters-2);var model=Parse(text);Assert.False(model.CapacityFallback);Assert.Equal(text,Assert.Single(model.Blocks).Text);
    }
}
