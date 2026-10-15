using System.Text;

namespace KuaiYi.Core;

public enum MarkdownInlineKind { Text, Code, Bold, Italic }
public sealed record MarkdownInline(MarkdownInlineKind Kind,string Source,string Text);

/// <summary>A flat, bounded subset. Ambiguous or unsupported markup rejects the entire block.</summary>
internal static class RestrictedMarkdownInlines
{
    internal const int MaximumDelimiterLength=32;
    private const string Escapable="!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

    internal static bool TryParse(string text,int budget,out IReadOnlyList<MarkdownInline> inlines,out bool capacityExceeded)
    {
        inlines=Array.Empty<MarkdownInline>();capacityExceeded=false;
        var result=new List<MarkdownInline>();var literal=new StringBuilder();int literalStart=0,i=0;
        while(i<text.Length)
        {
            char marker=text[i];
            if(Escape(text,ref i,literal))continue;
            if(Unsupported(marker))return false;
            if(marker is not ('`' or '*' or '_')){literal.Append(marker);i++;continue;}
            int start=i,width=RunLength(text,i);i+=width;
            if(width>(marker=='`'?MaximumDelimiterLength:2))return false;
            if(!FlushLiteral(start)){capacityExceeded=true;return false;}
            int bodyStart=i;var body=new StringBuilder();bool closed=false;
            if(marker!='`'&&(i==text.Length||char.IsWhiteSpace(text[i])||marker=='_'&&start>0&&!Boundary(text[start-1])))return false;
            while(i<text.Length)
            {
                char value=text[i];
                // Multiline inline spans, nested spans and crossings stay literal as a complete block.
                if(value is '\r' or '\n')return false;
                if(marker!='`'&&Escape(text,ref i,body))continue;
                if(value==marker)
                {
                    int closingWidth=RunLength(text,i);
                    if(closingWidth==width)
                    {
                        if(marker!='`'&&(i==bodyStart||char.IsWhiteSpace(text[i-1])||marker=='_'&&i+width<text.Length&&!Boundary(text[i+width])))return false;
                        i+=width;closed=true;break;
                    }
                    if(marker!='`')return false;
                    body.Append(text,i,closingWidth);i+=closingWidth;continue;
                }
                if(marker!='`'&&(value is '*' or '_' or '`'||Unsupported(value)))return false;
                body.Append(value);i++;
            }
            if(!closed)return false;
            if(result.Count>=budget){capacityExceeded=true;return false;}
            result.Add(new(marker=='`'?MarkdownInlineKind.Code:width==2?MarkdownInlineKind.Bold:MarkdownInlineKind.Italic,text[start..i],body.ToString()));
            literalStart=i;
        }
        if(!FlushLiteral(text.Length)){capacityExceeded=true;return false;}
        inlines=result.AsReadOnly();return true;

        bool FlushLiteral(int end)
        {
            if(end==literalStart)return true;
            if(result.Count>=budget)return false;
            result.Add(new(MarkdownInlineKind.Text,text[literalStart..end],literal.ToString()));literal.Clear();return true;
        }
    }
    private static bool Escape(string text,ref int position,StringBuilder output)
    {
        if(text[position]!='\\'||position+1==text.Length||!Escapable.Contains(text[position+1]))return false;
        output.Append(text[position+1]);position+=2;return true;
    }
    private static int RunLength(string text,int start)
    {int end=start+1;while(end<text.Length&&text[end]==text[start])end++;return end-start;}
    private static bool Boundary(char value)=>char.IsWhiteSpace(value)||char.IsPunctuation(value);
    private static bool Unsupported(char value)=>value is '[' or ']' or '<' or '>' or '|';
}
