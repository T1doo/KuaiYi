using KuaiYi.App;
using KuaiYi.Core;
using System.Windows.Controls;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class ReadingParagraphRhythmTests
{
    [Fact]public void ParagraphGapIsShorterThanTextLineWithoutRewritingContent()
    {
        const string text="第一行\n第二行\n\nAnother paragraph.";
        var model=new RestrictedMarkdown();var id=Guid.NewGuid();model.Update(id,text,true);
        var host=new RichTextBox();var view=new MarkdownFlowView(host);
        view.Apply(model.Blocks,16,new(428,560),id);
        Assert.Equal(text,string.Concat(model.Blocks.Select(b=>b.Source)));
        int gap=model.Blocks.ToList().FindIndex(b=>b.Kind==MarkdownBlockKind.Paragraph&&b.Text.Length==0);
        Assert.True(gap>0);Assert.InRange(host.Document.Blocks[gap].LineHeight,10,16);
        Assert.True(host.Document.Blocks[gap].LineHeight<host.Document.Blocks[0].LineHeight);
        var retained=host.Document.Blocks[0];view.Apply(model.Blocks,20,new(428,560),id);
        Assert.Same(retained,host.Document.Blocks[0]);Assert.True(host.Document.Blocks[gap].LineHeight<host.Document.Blocks[0].LineHeight);
        view.Clear();
    }
}
