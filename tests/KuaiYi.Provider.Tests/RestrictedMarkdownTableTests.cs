using KuaiYi.Core;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class RestrictedMarkdownTableTests
{
    private static readonly Guid Version=Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static RestrictedMarkdown Parse(string source,bool complete=true)
    {var model=new RestrictedMarkdown();model.Update(Version,source,complete);return model;}
    private static void Conserved(RestrictedMarkdown model,string source)
    {
        Assert.Equal(source,string.Concat(model.Blocks.Select(b=>b.Source)));
        foreach(var block in model.Blocks.Where(b=>b.Table!=null))
        {
            var table=block.Table!;Assert.Equal(block.Source,table.Rows[0].Source+table.SeparatorSource+string.Concat(table.Rows.Skip(1).Select(r=>r.Source)));
            foreach(var row in table.Rows)foreach(var cell in row.Cells)
            {Assert.Equal(cell.Source.Trim(' ','\t'),string.Concat(cell.Inlines.Select(p=>p.Source)));Assert.Equal(cell.Text,string.Concat(cell.Inlines.Select(p=>p.Text)));}
        }
    }
    private static void SameDocument(RestrictedMarkdown expected,RestrictedMarkdown actual)
    {
        Assert.Equal(expected.CapacityFallback,actual.CapacityFallback);Assert.Equal(expected.Blocks.Count,actual.Blocks.Count);
        for(int i=0;i<expected.Blocks.Count;i++)
        {
            var a=expected.Blocks[i];var b=actual.Blocks[i];
            Assert.Equal((a.Kind,a.Source,a.Text,a.Level,a.Number,a.Closed),(b.Kind,b.Source,b.Text,b.Level,b.Number,b.Closed));
            Assert.Equal(a.Inlines,b.Inlines);MarkdownTableAssertions.Equal(a.Table,b.Table);
        }
    }
    internal static string Grid(int columns,int rows,string cell="x")
    {
        string row="|"+string.Join('|',Enumerable.Repeat(cell,columns))+"|\n";
        return row+"|"+string.Join('|',Enumerable.Repeat("---",columns))+"|\n"+string.Concat(Enumerable.Repeat(row,rows-1));
    }
    [Theory][InlineData("| a | b |\n| --- | --- |\n| x | y |",2,2)]
    [InlineData("a | b\n:--- | ---:\nx | y",2,2)]
    [InlineData(" | a | \r\n | :---: | \r\n | x | \r\n",1,2)]
    [InlineData("| a | b |\n| --- | --- |",2,1)]
    [InlineData("| a | b |\n| --- | --- |\n| | |",2,2)]
    public void BasicTablesHaveExpectedShapeAndExactRows(string source,int columns,int rows)
    {
        var model=Parse(source);Conserved(model,source);var block=Assert.Single(model.Blocks);Assert.Equal(MarkdownBlockKind.Table,block.Kind);
        Assert.Equal(columns,block.Table!.Alignments.Count);Assert.Equal(rows,block.Table.Rows.Count);Assert.All(block.Table.Rows,row=>Assert.Equal(columns,row.Cells.Count));
    }
    [Fact]public void AlignmentInlineCodeEscapesAndEmptyCellsHaveExplicitDisplayValues()
    {
        const string source="| **left** | *center* | `right` | default |\r\n| :--- | :---: | ---: | --- |\r\n| a\\|b | ``x | ` y`` | \\*literal\\* | |\r\n";
        var model=Parse(source);Conserved(model,source);var data=Assert.Single(model.Blocks).Table!;
        Assert.Equal(new[]{MarkdownTableAlignment.Left,MarkdownTableAlignment.Center,MarkdownTableAlignment.Right,MarkdownTableAlignment.Default},data.Alignments);
        Assert.Equal(new[]{"a|b","x | ` y","*literal*",""},data.Rows[1].Cells.Select(c=>c.Text));
        Assert.Equal(MarkdownInlineKind.Code,Assert.Single(data.Rows[1].Cells[1].Inlines).Kind);Assert.Equal(" **left** ",data.Rows[0].Cells[0].Source);
    }
    [Fact]public void BackslashParityControlsCellSeparatorsAndCodeKeepsLiteralBackslashes()
    {
        foreach(int count in new[]{1,3})
        {
            string source="a|b\n---|---\n| a"+new string('\\',count)+"|b | y |";var model=Parse(source);Conserved(model,source);
            Assert.Equal(count==1?"a|b":"a\\|b",Assert.Single(model.Blocks).Table!.Rows[1].Cells[0].Text);
        }
        const string even="a|b\n---|---\na\\\\|b";var pair=Parse(even);Conserved(pair,even);Assert.Equal(new[]{"a\\","b"},Assert.Single(pair.Blocks).Table!.Rows[1].Cells.Select(c=>c.Text));
        const string code="a|b\n---|---\n| `a\\|b` | c |";var literal=Parse(code);Conserved(literal,code);Assert.Equal("a\\|b",Assert.Single(literal.Blocks).Table!.Rows[1].Cells[0].Text);
    }
    [Theory][InlineData("a|b\n--|---\nx|y\n")][InlineData("a|b\n---x|---\nx|y\n")]
    [InlineData("a|b\n:--:|---\nx|y\n")][InlineData("a|b\n---|---|---\nx|y\n")]
    [InlineData("a|b\n---|---\nx|y|z\n")][InlineData("a|b\n---|---\n| x |\n")]
    [InlineData("a|b\n---|---\n**a *nested* b**|y\n")][InlineData("a|b\n---|---\n`unclosed|y\n")]
    [InlineData("a|b\n---|---\n[link](file:///inert)|y\n")][InlineData("a|b\n---|---\n<img src='https://public.invalid'>|y\n")]
    [InlineData("a|b\n---|---\n***ambiguous***|y\n")][InlineData("a|b\n---|---\n**unclosed|y\n")]
    public void UnsupportedTableContentFallsBackAsOneOriginalBlock(string source)
    {
        var model=Parse(source);Conserved(model,source);Assert.False(model.CapacityFallback);var block=Assert.Single(model.Blocks);
        Assert.Equal(MarkdownBlockKind.Plain,block.Kind);Assert.Equal(source,block.Text);Assert.Null(block.Table);
    }
    public static IEnumerable<object[]> Samples()=>new[]{
        "a|b\n---|---\nx|y", "intro\na|b\n---|---\nx|y\n\nend", "intro\na|b\n---|---\nx|y\n# heading",
        "| a | b |\r\n| :---: | ---: |\r\n| 🙂\\|x | **bold** |\r\n\r\n尾",
        "| `a|b` | c |\n| --- | --- |\n| ``x | ` y`` | *z* |\n", "| single |\n| --- |",
        "a|b\n---|---\n| | |\r", "a|b\n--|---\nx|y\n", "a|b\n---|---\n`unclosed|y\n",
        "a|b\n---|---\nx|y\npartial boundary", "before\npipe | prose\nnext", "a|b\n---|---\n**good**|[bad](https://public.invalid)",
        "a|b\n---|---\nx|y\n# turns|into row\n", "a|b\n---|---\nx|y\n```\ninert|code\n```\n"
    }.Select(s=>new object[]{s});
    [Theory][MemberData(nameof(Samples))]
    public void EverySplitAndUtf16FeedMatchesWholeProductionDocument(string source)
    {
        var expected=Parse(source);
        for(int split=0;split<=source.Length;split++)
        {
            var model=Parse(source[..split],false);Conserved(model,source[..split]);model.Update(Version,source);Conserved(model,source);
            model.Update(Version,source,true);SameDocument(expected,model);
        }
        var units=new RestrictedMarkdown();string text="";
        foreach(char c in source){text+=c;units.Update(Version,text);Conserved(units,text);}units.Update(Version,source,true);SameDocument(expected,units);
    }
    [Fact]public void RandomChunksAcrossMultipleAdjacentTablesPreserveAllSource()
    {
        string source=string.Concat(Enumerable.Repeat("# h\r\n\r\n| **a** | `b|c` |\r\n| :--- | ---: |\r\n| \\| | 🙂 |\r\n\r\n",10));var expected=Parse(source);var random=new Random(11516);
        for(int run=0;run<30;run++)
        {
            var model=new RestrictedMarkdown();int length=0;
            while(length<source.Length){length=Math.Min(source.Length,length+random.Next(1,26));model.Update(Version,source[..length]);Conserved(model,source[..length]);}
            model.Update(Version,source,true);SameDocument(expected,model);
        }
    }
    [Fact]public void HeaderAndSeparatorWaitForLookaheadAndTableWaitsForABoundary()
    {
        const string prefix="# stable\n\n";var model=Parse(prefix+"a|b\n",false);var first=model.Blocks[0];Assert.Equal(2,model.StableCount);
        model.Update(Version,prefix+"a|b\n---|");Assert.Equal(2,model.StableCount);Assert.DoesNotContain(model.Blocks,b=>b.Table!=null);
        model.Update(Version,prefix+"a|b\n---|---\n");Assert.Equal(2,model.StableCount);Assert.NotNull(model.Blocks[^1].Table);
        model.Update(Version,prefix+"a|b\n---|---\nx|y\n");var table=model.Blocks[^1];Assert.Equal(2,model.StableCount);
        model.Update(Version,prefix+"a|b\n---|---\nx|y\n\ntail");var stableTable=model.Blocks[2];Assert.NotSame(table,stableTable);Assert.Equal(4,model.StableCount);
        model.Update(Version,prefix+"a|b\n---|---\nx|y\n\ntail grows");Assert.Same(first,model.Blocks[0]);Assert.Same(stableTable,model.Blocks[2]);
    }
    [Fact]public void PartialNonPipeBoundaryCannotFreezeATableBeforeItBecomesARow()
    {
        const string table="a|b\n---|---\nx|y\n";var model=Parse(table+"# partial",false);Assert.Equal(0,model.StableCount);
        model.Update(Version,table+"# partial|row\n");Assert.Equal(3,Assert.Single(model.Blocks).Table!.Rows.Count);Assert.Equal(0,model.StableCount);
    }
    [Theory][InlineData(16,128,false)][InlineData(17,2,true)][InlineData(2,129,true)]
    public void RowAndColumnCapacityLimitsRetainTheWholeDocument(int columns,int rows,bool fallback)
    {
        string source=Grid(columns,rows);var model=Parse(source);Conserved(model,source);Assert.Equal(fallback,model.CapacityFallback);
        if(fallback)Assert.Equal(source,Assert.Single(model.Blocks).Text);else Assert.Equal(rows,Assert.Single(model.Blocks).Table!.Rows.Count);
    }
    [Fact]public void CumulativeCellBudgetIncludesStableTablesAndResetsOnClearAndVersionChange()
    {
        string full=Grid(16,128);var model=Parse(full+"\n",false);Assert.False(model.CapacityFallback);Assert.Equal(2,model.StableCount);
        model.Update(Version,full+"\n"+Grid(1,1));Assert.True(model.CapacityFallback);Assert.Equal(full+"\n"+Grid(1,1),Assert.Single(model.Blocks).Text);
        model.Clear();Assert.Empty(model.Blocks);model.Update(Guid.NewGuid(),Grid(2,2),true);Assert.False(model.CapacityFallback);Assert.NotNull(Assert.Single(model.Blocks).Table);
        var old=model.Blocks[0];model.Update(Guid.NewGuid(),Grid(2,2),true);Assert.NotSame(old,model.Blocks[0]);
    }
    [Fact]public void TableInlineCapacityAndCharacterOverflowKeepUntruncatedSource()
    {
        string source=Grid(16,128,"*x* y");var model=Parse(source);Assert.False(model.CapacityFallback);Conserved(model,source);
        source=source.Replace("*x* y","*x* y *z*",StringComparison.Ordinal);model.Update(Version,source,true);Assert.True(model.CapacityFallback);Assert.Equal(source,Assert.Single(model.Blocks).Text);
        source=Grid(2,2)+"|"+new string('x',RestrictedMarkdown.MaximumFormattedCharacters)+"|y|";model.Update(Version,source,true);Assert.True(model.CapacityFallback);Assert.Equal(source,Assert.Single(model.Blocks).Text);
    }
}

internal static class MarkdownTableAssertions
{
    internal static void Equal(MarkdownTable? expected,MarkdownTable? actual)
    {
        if(expected is null){Assert.Null(actual);return;}
        Assert.NotNull(actual);Assert.Equal(expected.SeparatorSource,actual.SeparatorSource);Assert.Equal(expected.Alignments,actual.Alignments);Assert.Equal(expected.Rows.Count,actual.Rows.Count);
        for(int row=0;row<expected.Rows.Count;row++)
        {
            Assert.Equal(expected.Rows[row].Source,actual.Rows[row].Source);Assert.Equal(expected.Rows[row].Cells.Count,actual.Rows[row].Cells.Count);
            for(int column=0;column<expected.Rows[row].Cells.Count;column++)
            {
                var a=expected.Rows[row].Cells[column];var b=actual.Rows[row].Cells[column];Assert.Equal((a.Source,a.Text),(b.Source,b.Text));Assert.Equal(a.Inlines,b.Inlines);
            }
        }
    }
}
