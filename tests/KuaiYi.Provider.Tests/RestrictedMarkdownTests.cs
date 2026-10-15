using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class RestrictedMarkdownTests
{
    private static readonly Guid Version=Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static MarkdownBlock[] Whole(string source)
    {var model=new RestrictedMarkdown();model.Update(Version,source,true);return model.Blocks.ToArray();}
    private static void EqualWhole(string source,IEnumerable<string> chunks)
    {
        var model=new RestrictedMarkdown();string text="";
        foreach(string part in chunks){text+=part;model.Update(Version,text);Assert.Equal(text,string.Concat(model.Blocks.Select(b=>b.Source)));}
        model.Update(Version,text,true);Assert.Equal(source,text);var expected=Whole(source);Assert.Equal(expected.Length,model.Blocks.Count);
        for(int i=0;i<expected.Length;i++)
        {
            var actual=model.Blocks[i];var block=expected[i];
            Assert.Equal((block.Kind,block.Source,block.Text,block.Level,block.Number,block.Closed),(actual.Kind,actual.Source,actual.Text,actual.Level,actual.Number,actual.Closed));
            Assert.Equal(block.Inlines,actual.Inlines);
            MarkdownTableAssertions.Equal(block.Table,actual.Table);
        }
    }
    public static IEnumerable<object[]> Samples()=>new[]{
        "# 标题\r\n\r\n段落🙂\r\n第二行\r\n\r\n- one\r\n2. two\r\n```csharp\r\nvar x = \"<img>\";\r\n```\r\n尾段",
        "first\n```\ncode\n```\nlast", "para\n```info`invalid\nnext", "text\n"+new string('`',33)+"\nrest",
        "a\r\nb\rc\n\r\n## h\r", "~~~text\n未闭围栏\n🙂", "# **bold**\n\n| a | b |\n|---|---|\n\n[link](file:///inert)\n",
        "plain\n \nnext", "1. numbered\n+ bullet\n\n<script>literal</script>\n", "```\n```\n", "", "```"
    }.Select(s=>new object[]{s});
    [Theory][MemberData(nameof(Samples))]
    public void EveryTwoWaySplitAndSingleUtf16UnitsHaveEquivalentFinalBlocks(string source)
    {
        for(int i=0;i<=source.Length;i++)EqualWhole(source,[source[..i],source[i..]]);
        EqualWhole(source,source.Select(c=>c.ToString()));
    }
    [Fact]public void FixedSeedArbitraryChunksPreserveUnicodeAndCrlf()
    {
        string source=string.Concat(Enumerable.Repeat("# 标题🙂\r\n\r\npara\r\n- x\r\n~~~\r\nlong\r\n~~~\r\n",16));var random=new Random(27011);
        for(int run=0;run<50;run++)
        {var chunks=new List<string>();for(int i=0;i<source.Length;){int n=Math.Min(random.Next(1,24),source.Length-i);chunks.Add(source.Substring(i,n));i+=n;}EqualWhole(source,chunks);}
    }
    [Fact]public void ExpectedNativeKindsAndCodeBodyAreExplicit()
    {
        var blocks=Whole("# title\n- bullet\n12. numbered\n```cs\na<b\n```\n");
        Assert.Equal(new[]{MarkdownBlockKind.Heading,MarkdownBlockKind.Bullet,MarkdownBlockKind.Numbered,MarkdownBlockKind.Code},blocks.Select(b=>b.Kind));
        Assert.Equal("title",blocks[0].Text);Assert.Equal(1,blocks[0].Level);Assert.Equal(12,blocks[2].Number);Assert.Equal("a<b\n",blocks[3].Text);Assert.True(blocks[3].Closed);
    }
    [Fact]public void StableParagraphAndCompletedCodeAreReusedWhileTailChanges()
    {
        var model=new RestrictedMarkdown();string stable="paragraph\n\n```\ncode\n```\n";
        model.Update(Version,stable+"tail");var first=model.Blocks.ToArray();Assert.Equal(3,model.StableCount);
        model.Update(Version,stable+"tail grows");for(int i=0;i<3;i++)Assert.Same(first[i],model.Blocks[i]);Assert.NotSame(first[3],model.Blocks[3]);
        var tail=model.Blocks[3];model.Update(Version,stable+"tail grows");Assert.Same(tail,model.Blocks[3]);
    }
    [Fact]public void UnclosedFenceKeepsAllCodeIncludingPossibleClosingPrefix()
    {
        var block=Assert.Single(Whole("~~~lang\n字🙂\r\n~~"));Assert.Equal(MarkdownBlockKind.Code,block.Kind);Assert.False(block.Closed);Assert.Equal("字🙂\r\n~~",block.Text);
    }
    [Theory][InlineData("# ***nested***\r\n")][InlineData("- [link](https://public.invalid)\n")][InlineData("<img src='https://public.invalid'>\n")][InlineData("|a|b|\n|--|--|\n")][InlineData("![image](file:///inert)\n")]
    public void UnsupportedSyntaxIsOneCompleteLiteralBlock(string source)
    {var block=Assert.Single(Whole(source));Assert.Equal(MarkdownBlockKind.Plain,block.Kind);Assert.Equal(source,block.Text);Assert.Equal(source,block.Source);}
    [Fact]public void LongCodeLineIsNotTruncated()
    {string line=new('x',50000);var block=Assert.Single(Whole("```\n"+line+"\n```"));Assert.Equal(line+"\n",block.Text);}
    [Fact]public void CharacterCapacityFallsBackWithoutLosingSourceAndCanReset()
    {
        var model=new RestrictedMarkdown();string source="# "+new string('中',RestrictedMarkdown.MaximumFormattedCharacters-2);
        model.Update(Version,source);Assert.False(model.CapacityFallback);Assert.Equal(MarkdownBlockKind.Heading,Assert.Single(model.Blocks).Kind);source+="中";
        model.Update(Version,source);Assert.True(model.CapacityFallback);Assert.Equal(source,Assert.Single(model.Blocks).Text);
        model.Update(Guid.NewGuid(),"# small",true);Assert.False(model.CapacityFallback);Assert.Equal(MarkdownBlockKind.Heading,Assert.Single(model.Blocks).Kind);
    }
    [Fact]public void BlockCapacityFallsBackWithoutLosingSource()
    {
        var model=new RestrictedMarkdown();string source=new('\n',RestrictedMarkdown.MaximumBlocks);model.Update(Version,source);
        Assert.False(model.CapacityFallback);Assert.Equal(RestrictedMarkdown.MaximumBlocks,model.Blocks.Count);source+="\n";model.Update(Version,source,true);
        Assert.True(model.CapacityFallback);Assert.Equal(source,Assert.Single(model.Blocks).Text);
    }
    [Fact]public void UpstreamCapacityRejectionDoesNotMutatePriorDocument()
    {
        var model=new RestrictedMarkdown();model.Update(Version,"safe",true);var prior=Assert.Single(model.Blocks);
        Assert.Throws<ArgumentOutOfRangeException>(()=>model.Update(Version,new string('x',OverlayResults.MaximumCharacters+1)));
        Assert.Same(prior,Assert.Single(model.Blocks));
    }
    [Fact]public void VersionReplacementNeverReusesPreviousVersionAndClearDropsAllBlocks()
    {
        var model=new RestrictedMarkdown();model.Update(Version,"# same\n");var first=Assert.Single(model.Blocks);
        model.Update(Guid.NewGuid(),"# same\n");Assert.NotSame(first,Assert.Single(model.Blocks));model.Clear();Assert.Empty(model.Blocks);Assert.Equal(0,model.StableCount);
    }
    [Fact]public void FailedRegenerationRendersSelectedFallbackRatherThanActiveFailedVersion()
    {
        var gate=new RuntimeGate();var origin=new EndpointOrigin(new Uri("https://public.invalid"));gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));
        var results=new OverlayResults(gate);var session=Guid.NewGuid();Assert.True(gate.TryStartRequest(session,Guid.NewGuid(),null,out var first,out _));Assert.True(results.Begin(first));Assert.True(results.Append(first,"# previous"));Assert.True(results.Complete(first));
        Assert.True(gate.TryStartRequest(session,Guid.NewGuid(),null,out var second,out _));Assert.True(results.Begin(second));Assert.True(results.Append(second,"# failed partial"));Assert.True(results.Fail(second));
        var model=new RestrictedMarkdown();model.Update(results.SelectedVersionId,results.Text,true);Assert.Equal(first.VersionId,results.SelectedVersionId);Assert.Equal("previous",Assert.Single(model.Blocks).Text);Assert.Equal("# previous",results.Text);
    }
    [Fact]public void ActualThreeVersionCacheAndLateGateEventsRemainIsolatedFromSelectedDocument()
    {
        var gate=new RuntimeGate(requireProfileBinding:true);gate.ObserveSettingsMetadata(new(new(){ActiveProfileId="public",Profiles=[new(){Id="public",BaseUrl="https://public.invalid",Model="public",Authentication=ApiAuthentication.None}]},"public",SettingsLoadState.Current));
        var origin=new EndpointOrigin(new Uri("https://public.invalid"));gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));Assert.True(gate.TryGetProfileBinding("public",out var binding));
        var results=new OverlayResults(gate);var model=new RestrictedMarkdown();var session=Guid.NewGuid();var ids=new List<RequestIdentity>();
        for(int i=0;i<4;i++)
        {Assert.True(gate.TryStartProfileRequest(binding,session,Guid.NewGuid(),null,out var id,out _));ids.Add(id);Assert.True(results.Begin(id));Assert.True(results.Append(id,"# public "+i));Assert.True(results.Complete(id));}
        Assert.Equal(3,results.Versions.Count);Assert.True(results.SelectVersion(ids[1].VersionId));model.Update(results.SelectedVersionId,results.Text,true);var selected=Assert.Single(model.Blocks);
        Assert.False(results.Append(ids[0],"late"));Assert.Equal("# public 1",results.Text);Assert.Equal("public 1",selected.Text);
        Assert.True(gate.TryStartProfileRequest(binding,session,Guid.NewGuid(),null,out var fifth,out _));Assert.True(results.Begin(fifth));Assert.True(results.Append(fifth,"# partial"));
        Assert.DoesNotContain(results.Versions,v=>v.Identity==ids[2]);Assert.True(results.SelectVersion(ids[1].VersionId));gate.Disable();Assert.False(results.Append(fifth,"late after cancel"));model.Update(results.SelectedVersionId,results.Text,true);Assert.Equal("public 1",Assert.Single(model.Blocks).Text);
        gate.SetSessionLocked(true);results.Clear();model.Clear();Assert.Empty(results.Versions);Assert.Empty(model.Blocks);Assert.False(results.Append(fifth,"late after clear"));
    }
}
