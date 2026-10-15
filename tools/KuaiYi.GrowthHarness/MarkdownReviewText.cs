using System.Text;
namespace KuaiYi.GrowthHarness;

/// <summary>Fixed synthetic input for the existing production-overlay review tool; no files, capture, HTTP or credentials.</summary>
internal static class MarkdownReviewText
{
    public static string Text {get;}=Build();
    private static string Build()
    {
        var text=new StringBuilder("# 原生 Markdown 合成验收 V1\n\n固定公开语料：**粗体**、*斜体*、`literal **code**`。中文标点：，。！？；公式按字面显示 E = mc^2。\n\n");
        text.Append(new string('W',1200)).Append("\n\n```text\n");
        for(int row=0;row<128;row++)text.Append("code_").Append(row.ToString("D3",System.Globalization.CultureInfo.InvariantCulture)).Append('_').Append(new string('x',100)).Append(" **inert** <img> file:///inert\n");
        text.Append("```\n\n");
        for(int column=0;column<12;column++)text.Append('|').Append("**列 ").Append(column.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("**");
        text.Append("|\n");for(int column=0;column<12;column++)text.Append(column%3==0?"|:---":column%3==1?"|:---:":"|---:");text.Append("|\n");
        for(int row=0;row<64;row++)
        {
            for(int column=0;column<12;column++)text.Append('|').Append("row-").Append(row.ToString("D2",System.Globalization.CultureInfo.InvariantCulture)).Append("-col-").Append(column.ToString("D2",System.Globalization.CultureInfo.InvariantCulture)).Append("-中文");
            text.Append("|\n");
        }
        return text.ToString();
    }
}
