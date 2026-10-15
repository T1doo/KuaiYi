namespace KuaiYi.Core;

public enum MarkdownBlockKind { Plain, Paragraph, Heading, Bullet, Numbered, Code }
public sealed record MarkdownBlock(MarkdownBlockKind Kind,string Source,string Text,int Level=0,int Number=1,bool Closed=true);

/// <summary>Bounded presentation data only. Original source is never interpreted as markup by WPF.</summary>
public sealed class RestrictedMarkdown
{
    public const int MaximumFormattedCharacters=120000,MaximumBlocks=2048;
    private readonly List<MarkdownBlock> blocks=[];
    private readonly IReadOnlyList<MarkdownBlock> view;
    private string source="";private Guid? version;private int stableCount,stableEnd;private bool final;
    public RestrictedMarkdown()=>view=blocks.AsReadOnly();
    public IReadOnlyList<MarkdownBlock> Blocks=>view;
    public bool CapacityFallback {get;private set;}
    public int StableCount=>stableCount;
    public void Clear(){blocks.Clear();source="";version=null;stableCount=stableEnd=0;final=CapacityFallback=false;}
    public void Update(Guid? selectedVersion,string text,bool complete=false)
    {
        ArgumentNullException.ThrowIfNull(text);
        if(text.Length>OverlayResults.MaximumCharacters)throw new ArgumentOutOfRangeException(nameof(text));
        if(version==selectedVersion&&source==text&&final==complete)return;
        bool append=version==selectedVersion&&!CapacityFallback&&!final&&text.StartsWith(source,StringComparison.Ordinal);
        if(!append){blocks.Clear();stableCount=stableEnd=0;}
        else blocks.RemoveRange(stableCount,blocks.Count-stableCount);
        source=text;version=selectedVersion;final=complete;CapacityFallback=false;
        if(text.Length>MaximumFormattedCharacters){Fallback();return;}
        int position=stableEnd;
        while(position<source.Length)
        {
            int start=position;var line=Line(position);string content=source[start..line.ContentEnd];
            MarkdownBlock block;bool stable;
            if(Fence(content,out char marker,out int width))
            {
                int bodyStart=line.End,end=line.End,bodyEnd=source.Length;bool closed=false;
                while(end<source.Length)
                {
                    var next=Line(end);string value=source[end..next.ContentEnd];
                    if((next.Terminated||complete)&&ClosingFence(value,marker,width)){bodyEnd=end;end=next.End;closed=true;break;}
                    end=next.End;
                }
                position=end;block=new(MarkdownBlockKind.Code,source[start..end],source[bodyStart..bodyEnd],Closed:closed);stable=closed||complete;
            }
            else if(Heading(content,out int level,out int headingStart))
            {
                position=line.End;block=InlineUnsupported(content[headingStart..])?Plain(start,position):new(MarkdownBlockKind.Heading,source[start..position],content[headingStart..],Level:level);stable=line.Terminated||complete;
            }
            else if(ListItem(content,out bool numbered,out int number,out int itemStart))
            {
                position=line.End;block=InlineUnsupported(content[itemStart..])?Plain(start,position):new(numbered?MarkdownBlockKind.Numbered:MarkdownBlockKind.Bullet,source[start..position],content[itemStart..],Number:number);stable=line.Terminated||complete;
            }
            else if(string.IsNullOrWhiteSpace(content))
            {
                position=line.End;block=new(MarkdownBlockKind.Paragraph,source[start..position],"");stable=line.Terminated||complete;
            }
            else
            {
                position=line.End;
                while(position<source.Length)
                {
                    var next=Line(position);string value=source[position..next.ContentEnd];
                    if((next.Terminated||complete)&&(string.IsNullOrWhiteSpace(value)||Fence(value,out _,out _)||Heading(value,out _,out _)||ListItem(value,out _,out _,out _)))break;
                    position=next.End;
                }
                string raw=source[start..position];block=InlineUnsupported(raw)?Plain(start,position):new(MarkdownBlockKind.Paragraph,raw,raw.TrimEnd('\r','\n'));stable=position<source.Length||complete;
            }
            blocks.Add(block);
            if(blocks.Count>MaximumBlocks){Fallback();return;}
            if(stable){stableCount=blocks.Count;stableEnd=position;}
        }
    }
    private MarkdownBlock Plain(int start,int end)=>new(MarkdownBlockKind.Plain,source[start..end],source[start..end]);
    private void Fallback(){blocks.Clear();if(source.Length>0)blocks.Add(Plain(0,source.Length));stableCount=stableEnd=0;CapacityFallback=true;}
    private (int ContentEnd,int End,bool Terminated) Line(int start)
    {
        int i=start;while(i<source.Length&&source[i] is not ('\r' or '\n'))i++;
        if(i==source.Length)return(i,i,false);
        if(source[i]=='\r'&&i+1==source.Length&&!final)return(i,source.Length,false);
        return(i,i+(source[i]=='\r'&&i+1<source.Length&&source[i+1]=='\n'?2:1),true);
    }
    private static bool Heading(string text,out int level,out int start)
    {level=0;while(level<text.Length&&text[level]=='#')level++;start=level+1;return level is >=1 and <=6&&text.Length>level&&text[level]==' ';}
    private static bool ListItem(string text,out bool numbered,out int number,out int start)
    {
        numbered=false;number=1;start=2;
        if(text.Length>=2&&text[0] is '-' or '+' or '*'&&text[1]==' ')return true;
        int i=0;while(i<text.Length&&i<9&&text[i] is >= '0' and <= '9')i++;
        if(i==0||i+1>=text.Length||text[i]!='.'||text[i+1]!=' '||!int.TryParse(text.AsSpan(0,i),out number)||number<1)return false;
        numbered=true;start=i+2;return true;
    }
    private static bool Fence(string text,out char marker,out int width)
    {
        marker=text.Length>0?text[0]:'\0';width=0;
        if(marker is not ('`' or '~'))return false;
        while(width<text.Length&&text[width]==marker)width++;
        return width is >=3 and <=32&&(marker!='`'||!text.AsSpan(width).Contains('`'));
    }
    private static bool ClosingFence(string text,char marker,int width)
    {int i=0;while(i<text.Length&&text[i]==marker)i++;return i>=width&&text.AsSpan(i).Trim().Length==0;}
    // Unsupported inline syntax stays literal as a whole block, including its original block markers.
    private static bool InlineUnsupported(string text)=>text.IndexOfAny(['*','_','`','[',']','<','>','|','\\'])>=0;
}
