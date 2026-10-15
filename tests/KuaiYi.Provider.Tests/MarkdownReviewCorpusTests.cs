using KuaiYi.Core;
using KuaiYi.GrowthHarness;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class MarkdownReviewCorpusTests
{
    [Fact]public void FixedReviewInputFitsExistingBoundsAndContainsRealNativeCodeAndTableModels()
    {
        string text=MarkdownReviewText.Text;var model=new RestrictedMarkdown();model.Update(Guid.NewGuid(),text,true);Assert.False(model.CapacityFallback);Assert.InRange(text.Length,20000,RestrictedMarkdown.MaximumFormattedCharacters);
        Assert.Equal("4b2a6cd375c4fa39d56d3a7a685840f81545bd4085d2124c66ee6272679b863e",Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant());
        Assert.Equal(text,string.Concat(model.Blocks.Select(b=>b.Source)));Assert.Equal(MarkdownBlockKind.Table,model.Blocks[^1].Kind);var table=Assert.IsType<MarkdownTable>(model.Blocks[^1].Table);Assert.Equal(12,table.Alignments.Count);Assert.Equal(65,table.Rows.Count);Assert.Equal("row-63-col-11-中文",table.Rows[^1].Cells[^1].Text);
        var code=Assert.Single(model.Blocks,b=>b.Kind==MarkdownBlockKind.Code);Assert.Equal(128,code.Text.Count(c=>c=='\n'));Assert.Contains("**inert** <img> file:///inert",code.Text);Assert.Contains(model.Blocks,b=>b.Text==new string('W',1200));
    }
    [Fact]public void FixedReviewInputUsesActualIncrementalParserAcrossStreamingChunksWithoutLosingSource()
    {
        string text=MarkdownReviewText.Text;var model=new RestrictedMarkdown();var id=Guid.NewGuid();
        for(int end=137;end<text.Length;end+=137){model.Update(id,text[..end],false);Assert.False(model.CapacityFallback);Assert.Equal(text[..end],string.Concat(model.Blocks.Select(b=>b.Source)));}
        model.Update(id,text,true);Assert.Equal(text,string.Concat(model.Blocks.Select(b=>b.Source)));Assert.Equal(65,Assert.IsType<MarkdownTable>(model.Blocks[^1].Table).Rows.Count);
    }
}
