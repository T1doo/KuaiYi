using KuaiYi.Core;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class RestrictedMarkdownLinkTests
{
    private static readonly Guid Version=Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static RestrictedMarkdown Parse(string source,bool complete=true)
    {var model=new RestrictedMarkdown();model.Update(Version,source,complete);return model;}
    [Theory]
    [InlineData("HTTPS://Public.Invalid:443/a?q=one#part","https://public.invalid/a?q=one#part","public.invalid")]
    [InlineData("http://public.invalid:8080","http://public.invalid:8080/","public.invalid:8080")]
    [InlineData("https://例子.invalid/中文","https://xn--fsqu00a.invalid/%E4%B8%AD%E6%96%87","xn--fsqu00a.invalid")]
    [InlineData("https://[2001:db8::1]:8443/a","https://[2001:db8::1]:8443/a","[2001:db8::1]:8443")]
    [InlineData("http://192.0.2.1/a%20b","http://192.0.2.1/a%20b","192.0.2.1")]
    [InlineData("http://0xc0000201/a","http://192.0.2.1/a","192.0.2.1")]
    [InlineData("http://3221225985/a","http://192.0.2.1/a","192.0.2.1")]
    public void TargetsHaveExplicitCanonicalUrlAndAsciiDomain(string source,string url,string domain)
    {Assert.True(MarkdownHttpTarget.TryParse(source,out var target));Assert.Equal(url,target!.Url);Assert.Equal(domain,target.Domain);Assert.True(MarkdownHttpTarget.TryParse(url,out var again));Assert.Equal(target,again);}
    [Theory]
    [InlineData(null)][InlineData("")][InlineData(" https://public.invalid")][InlineData("//public.invalid")]
    [InlineData("file:///tmp/fixture")][InlineData("javascript:alert(1)")][InlineData("data:text/html,x")]
    [InlineData("shell:AppsFolder")][InlineData("mailto:test@public.invalid")][InlineData("https:public.invalid")]
    [InlineData("https://user:password@public.invalid/")][InlineData("https://user@public.invalid/")]
    [InlineData("https://@public.invalid/")][InlineData("https://:@public.invalid/")]
    [InlineData("https://public.invalid\\@other.invalid/")][InlineData("https://public.invalid/a b")]
    [InlineData("https://public.invalid/a\nb")][InlineData("https://public.invalid/%0d%0a")]
    [InlineData("https://public.invalid/%5cfile")][InlineData("https://public.invalid/%00")]
    [InlineData("https://public.invalid/%")][InlineData("https://public.invalid/%xy")]
    [InlineData("https://public.invalid/\u202eexe")][InlineData("https://public.invalid/%E2%80%AEexe")]
    [InlineData("https://-bad.invalid/")][InlineData("https://bad_.invalid/")]
    [InlineData("https://public.invalid/a(b)")][InlineData("https://public.invalid/a|b")]
    public void UnsafeOrAmbiguousTargetsAreRejectedWithoutIo(string? value)
    {Assert.False(MarkdownHttpTarget.TryParse(value,out var target));Assert.Null(target);}
    [Fact]public void TargetLabelAndDocumentBudgetsFallBackWithoutTruncation()
    {
        string longest="https://public.invalid/";longest+=new string('x',MarkdownHttpTarget.MaximumLength-longest.Length);
        Assert.True(MarkdownHttpTarget.TryParse(longest,out _));Assert.False(MarkdownHttpTarget.TryParse(longest+"x",out _));
        foreach(var source in new[]{"[x]("+longest+"x)","["+new string('a',513)+"](https://public.invalid)",new string('[',50000),string.Concat(Enumerable.Repeat("[x](https://public.invalid)",RestrictedMarkdown.MaximumInlines+1))})
        {var model=Parse(source);Assert.Equal(source,string.Concat(model.Blocks.Select(b=>b.Source)));var block=Assert.Single(model.Blocks);Assert.Equal(MarkdownBlockKind.Plain,block.Kind);Assert.Equal(source,block.Text);Assert.Empty(block.Inlines);}
    }
    [Theory]
    [InlineData("# [域名](https://public.invalid/a)",MarkdownBlockKind.Heading)]
    [InlineData("- [域名](https://public.invalid/a)",MarkdownBlockKind.Bullet)]
    [InlineData("3. [域名](https://public.invalid/a)",MarkdownBlockKind.Numbered)]
    [InlineData("[域名](https://public.invalid/a)",MarkdownBlockKind.Paragraph)]
    public void NativeBlockKindsRetainExactSourceWithTypedLink(string source,MarkdownBlockKind kind)
    {var block=Assert.Single(Parse(source).Blocks);Assert.Equal(kind,block.Kind);Assert.Equal(source,block.Source);Assert.Equal("域名",block.Text);var link=Assert.Single(block.Inlines);Assert.Equal(MarkdownInlineKind.Link,link.Kind);Assert.Equal("[域名](https://public.invalid/a)",link.Source);Assert.Equal("public.invalid",link.Link!.Domain);}
    [Theory]
    [InlineData("![image](https://public.invalid/a)")][InlineData("[outer [inner]](https://public.invalid)")]
    [InlineData("[**bold**](https://public.invalid)")][InlineData("[x](https://public.invalid \"title\")")]
    [InlineData("[x][reference]")][InlineData("<https://public.invalid>")][InlineData("[x](file:///inert)")]
    [InlineData("[x](https://public.invalid")][InlineData("[x](https://public.invalid/a(b))")]
    [InlineData("[x](https://public.invalid) [bad](javascript:alert)")]
    public void UnsupportedLinksRejectWholeParagraphAndWholeTable(string unsupported)
    {
        foreach(var source in new[]{"**before** "+unsupported,"a|b\n---|---\n**before**|"+unsupported})
        {var block=Assert.Single(Parse(source).Blocks);Assert.Equal(source,block.Source);Assert.Equal(source,block.Text);Assert.Equal(MarkdownBlockKind.Plain,block.Kind);Assert.Empty(block.Inlines);Assert.Null(block.Table);}
    }
    [Fact]public void EscapedLabelPunctuationAndInertCodeHaveExactIndependentSources()
    {
        const string source="[a\\]b\\*](https://public.invalid/a%28b%29) `![x](https://public.invalid) **b**`";
        var block=Assert.Single(Parse(source).Blocks);Assert.Equal(source,string.Concat(block.Inlines.Select(i=>i.Source)));Assert.Equal("a]b* ![x](https://public.invalid) **b**",block.Text);Assert.Equal(MarkdownInlineKind.Link,block.Inlines[0].Kind);Assert.Equal(MarkdownInlineKind.Code,block.Inlines[2].Kind);
        var literal=Assert.Single(Parse("\\[x\\]\\(https://public.invalid\\)").Blocks);Assert.All(literal.Inlines,i=>Assert.Equal(MarkdownInlineKind.Text,i.Kind));
    }
    [Theory]
    [InlineData("# [标题🙂](HTTPS://public.invalid/a?q=b#c) **b**\r\n\r\n- [尾](http://public.invalid:8080/a)")]
    [InlineData("a|b\r\n---|---\r\n[x](https://public.invalid)|[a\\]b](https://public.invalid/a%28b%29)\r\n")]
    [InlineData("**before** [x](https://public.invalid) [unclosed](https://public.invalid")]
    public void EveryDelimiterSplitAndUtf16FeedPreservesSourceAndWholeResult(string source)
    {
        var expected=Parse(source);
        void Same(RestrictedMarkdown model)
        {Assert.Equal(source,string.Concat(model.Blocks.Select(b=>b.Source)));Assert.Equal(expected.Blocks.Count,model.Blocks.Count);for(int i=0;i<model.Blocks.Count;i++){Assert.Equal(expected.Blocks[i].Kind,model.Blocks[i].Kind);Assert.Equal(expected.Blocks[i].Text,model.Blocks[i].Text);Assert.Equal(expected.Blocks[i].Inlines,model.Blocks[i].Inlines);MarkdownTableAssertions.Equal(expected.Blocks[i].Table,model.Blocks[i].Table);}}
        for(int split=0;split<=source.Length;split++){var model=Parse(source[..split],false);Assert.Equal(source[..split],string.Concat(model.Blocks.Select(b=>b.Source)));model.Update(Version,source,true);Same(model);}
        var units=new RestrictedMarkdown();string text="";foreach(char c in source){text+=c;units.Update(Version,text);Assert.Equal(text,string.Concat(units.Blocks.Select(b=>b.Source)));}units.Update(Version,source,true);Same(units);
    }
    [Fact]public void StableLinkBlockSurvivesOnlyIncrementalTailAndVersionResetReplacesIt()
    {var model=Parse("[stable](https://public.invalid)\n\n[tail",false);var stable=model.Blocks[0];model.Update(Version,"[stable](https://public.invalid)\n\n[tail](https://public.invalid)");Assert.Same(stable,model.Blocks[0]);Assert.Equal(MarkdownInlineKind.Link,Assert.Single(model.Blocks[^1].Inlines).Kind);model.Update(Guid.NewGuid(),"[stable](https://public.invalid)\n\n[tail](https://public.invalid)",true);Assert.NotSame(stable,model.Blocks[0]);model.Clear();Assert.Empty(model.Blocks);}
}
