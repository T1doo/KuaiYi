namespace KuaiYi.Core;

public enum MarkdownTableAlignment { Default, Left, Center, Right }
public sealed record MarkdownTableCell(string Source,string Text,IReadOnlyList<MarkdownInline> Inlines);
public sealed record MarkdownTableRow(string Source,IReadOnlyList<MarkdownTableCell> Cells);
public sealed record MarkdownTable(string SeparatorSource,IReadOnlyList<MarkdownTableAlignment> Alignments,IReadOnlyList<MarkdownTableRow> Rows);

internal static class RestrictedMarkdownTables
{
    internal static bool SeparatorCandidate(string text)
    {
        var value=text.AsSpan().Trim();if(value.Length>0&&value[0]=='|')value=value[1..].TrimStart();
        return value.Length>0&&value[0] is '-' or ':'&&value.Contains('-');
    }
    internal static MarkdownTable? Parse(IReadOnlyList<(string Source,string Content)> rows,string separatorSource,string separator,out bool capacityExceeded)
    {
        capacityExceeded=false;
        if(!Split(separator,out var delimiters,out capacityExceeded))return null;
        var alignments=new List<MarkdownTableAlignment>();
        foreach(string raw in delimiters)
        {
            var cell=raw.AsSpan().Trim(" \t");bool left=cell.Length>0&&cell[0]==':',right=cell.Length>1&&cell[^1]==':';
            if(left)cell=cell[1..];if(right)cell=cell[..^1];
            if(cell.Length<3)return null;foreach(char value in cell)if(value!='-')return null;
            alignments.Add(left&&right?MarkdownTableAlignment.Center:left?MarkdownTableAlignment.Left:right?MarkdownTableAlignment.Right:MarkdownTableAlignment.Default);
        }
        var parsed=new List<MarkdownTableRow>();int pieces=0;
        foreach(var row in rows)
        {
            if(!Split(row.Content,out var cells,out capacityExceeded)||cells.Count!=alignments.Count)return null;
            var result=new List<MarkdownTableCell>();
            foreach(string raw in cells)
            {
                string content=raw.Trim(' ','\t');
                if(!RestrictedMarkdownInlines.TryParse(content,RestrictedMarkdown.MaximumInlines,out var inlines,out capacityExceeded))return null;
                pieces+=Math.Max(1,inlines.Count);if(pieces>RestrictedMarkdown.MaximumInlines){capacityExceeded=true;return null;}
                result.Add(new(raw,string.Concat(inlines.Select(p=>p.Text)),inlines));
            }
            parsed.Add(new(row.Source,result.AsReadOnly()));
        }
        return new(separatorSource,alignments.AsReadOnly(),parsed.AsReadOnly());
    }
    // Pipes in matched code spans are inert; escaped punctuation outside code cannot delimit cells.
    private static bool Split(string text,out IReadOnlyList<string> cells,out bool capacityExceeded)
    {
        cells=Array.Empty<string>();capacityExceeded=false;var cuts=new List<int>();int codeWidth=0;
        for(int i=0;i<text.Length;i++)
        {
            char value=text[i];
            if(codeWidth==0&&value=='\\'&&i+1<text.Length){i++;continue;}
            if(value=='`')
            {
                int end=i+1;while(end<text.Length&&text[end]=='`')end++;int width=end-i;
                if(width>RestrictedMarkdownInlines.MaximumDelimiterLength)return false;
                if(codeWidth==0)codeWidth=width;else if(codeWidth==width)codeWidth=0;
                i=end-1;continue;
            }
            if(codeWidth==0&&value=='|')
            {
                cuts.Add(i);if(cuts.Count>RestrictedMarkdown.MaximumTableColumns+1){capacityExceeded=true;return false;}
            }
        }
        if(codeWidth!=0||cuts.Count==0)return false;
        var result=new List<string>();int start=0;
        foreach(int cut in cuts){result.Add(text[start..cut]);start=cut+1;}result.Add(text[start..]);
        if(string.IsNullOrWhiteSpace(result[0]))result.RemoveAt(0);
        if(result.Count>0&&string.IsNullOrWhiteSpace(result[^1]))result.RemoveAt(result.Count-1);
        if(result.Count>RestrictedMarkdown.MaximumTableColumns){capacityExceeded=true;return false;}
        if(result.Count==0)return false;
        cells=result.AsReadOnly();return true;
    }
}
