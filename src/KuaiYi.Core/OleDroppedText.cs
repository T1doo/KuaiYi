using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
namespace KuaiYi.Core;
public enum OleTextFormat{None,Unicode,Html,Rtf,ImageFile,ImageBitmap}
/// <summary>Finite inert text extraction. No HTML document, renderer, OLE object, image decoder or URL loader is instantiated.</summary>
public static class OleDroppedText
{
    public const int HtmlByteLimit=1024*1024,RtfByteLimit=512*1024;
    public static OleTextFormat Choose(bool files,bool unicode,bool html,bool rtf)=>files?OleTextFormat.None:unicode?OleTextFormat.Unicode:html?OleTextFormat.Html:rtf?OleTextFormat.Rtf:OleTextFormat.None;
    public static int ByteLimit(OleTextFormat format)=>format switch{OleTextFormat.Unicode=>OleUnicodeText.MaximumGlobalBytes,OleTextFormat.Html=>HtmlByteLimit,OleTextFormat.Rtf=>RtfByteLimit,_=>throw new InvalidDataException("Unsupported Drop format.")};
    public static string Decode(OleTextFormat format,ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length<1||bytes.Length>ByteLimit(format))throw new InvalidDataException("Drop format byte limit.");
        string text=format switch{OleTextFormat.Unicode=>Unicode(bytes),OleTextFormat.Html=>Html(bytes),OleTextFormat.Rtf=>Rtf(bytes),_=>throw new InvalidDataException("Unsupported Drop format.")};
        if(text.Length>=100002||string.IsNullOrWhiteSpace(text)||text.EnumerateRunes().Count()>50000)throw new InvalidDataException("Drop text length.");
        for(int i=0;i<text.Length;i++){if(char.IsHighSurrogate(text[i])){if(++i>=text.Length||!char.IsLowSurrogate(text[i]))throw new InvalidDataException("Drop surrogate.");}else if(char.IsLowSurrogate(text[i]))throw new InvalidDataException("Drop surrogate.");}
        if(IsUrlOnly(text))throw new InvalidDataException("URL-only Drop is not selected body text.");return text;
    }
    internal static bool IsUrlOnly(string text)=>Uri.TryCreate(text.Trim(),UriKind.Absolute,out var uri)&&uri.IsWellFormedOriginalString();
    private static string Unicode(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length<2||bytes.Length%2!=0)throw new InvalidDataException("Unicode Drop allocation.");int end=-1;for(int i=0;i<bytes.Length;i+=2)if(bytes[i]==0&&bytes[i+1]==0){end=i;break;}
        if(end<0||end>=200004)throw new InvalidDataException("Incomplete Unicode Drop.");return new UnicodeEncoding(false,false,true).GetString(bytes[..end]);
    }
    private static string Html(ReadOnlySpan<byte> bytes)
    {
        // Offsets are UTF8 byte offsets, not UTF16 string indices. Never fall back to surrounding whole-page context.
        int headerSize=Math.Min(bytes.Length,4096);int markup=bytes[..headerSize].IndexOf((byte)'<');if(markup>=0)headerSize=markup;string header=Encoding.Latin1.GetString(bytes[..headerSize]);var fields=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(string line in header.Split('\n'))
        {
            int colon=line.IndexOf(':');if(colon<0)continue;string key=line[..colon].Trim();if(key is not ("StartHTML" or "EndHTML" or "StartFragment" or "EndFragment" or "StartSelection" or "EndSelection"))continue;
            if(!int.TryParse(line[(colon+1)..].Trim(),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int value)||!fields.TryAdd(key,value))throw new InvalidDataException("Ambiguous HTML offsets.");
        }
        int Field(string key)=>fields.TryGetValue(key,out int value)?value:throw new InvalidDataException("Missing HTML offsets.");
        int first=Field("StartFragment"),last=Field("EndFragment"),htmlFirst=Field("StartHTML"),htmlLast=Field("EndHTML");
        if(first<0||first>=last||last>bytes.Length||htmlFirst<0&&htmlFirst!=-1||htmlLast<0&&htmlLast!=-1||htmlFirst>=0&&(htmlFirst>first||htmlLast<last||htmlFirst>=htmlLast)||htmlLast>bytes.Length)throw new InvalidDataException("HTML fragment outside context.");
        int headerEnd=header.LastIndexOf('\n')+1;if(headerEnd==0||first<headerEnd)throw new InvalidDataException("HTML fragment in header.");
        if(fields.ContainsKey("StartSelection")||fields.ContainsKey("EndSelection")){int start=Field("StartSelection"),end=Field("EndSelection");if(start<first||end>last||end<=start)throw new InvalidDataException("HTML selection outside fragment.");first=start;last=end;}
        string fragment=new UTF8Encoding(false,true).GetString(bytes[first..last]);var output=new StringBuilder();int position=0,pre=0;string? suppressed=null;
        void Append(string value)
        {
            value=WebUtility.HtmlDecode(value);foreach(char c in value){if(pre==0&&char.IsWhiteSpace(c)){if(output.Length>0&&output[^1] is not (' ' or '\n' or '\t'))output.Append(' ');}else output.Append(c);if(output.Length>=100002)throw new InvalidDataException("HTML text bound.");}
        }
        void Line(){while(output.Length>0&&output[^1]==' ')output.Length--;if(output.Length>0&&output[^1]!='\n')output.Append('\n');}
        while(position<fragment.Length)
        {
            if(suppressed!=null){int closing=position;while(true){closing=fragment.IndexOf("</"+suppressed,closing,StringComparison.OrdinalIgnoreCase);if(closing<0)break;int after=closing+2+suppressed.Length;if(after<fragment.Length&&(fragment[after]=='>'||char.IsWhiteSpace(fragment[after])))break;closing=after;}if(closing<0)throw new InvalidDataException("Unterminated suppressed HTML.");if(fragment.IndexOf("<"+suppressed,position,closing-position,StringComparison.OrdinalIgnoreCase)>=0)throw new InvalidDataException("Nested suppressed HTML is unsupported.");position=closing;suppressed=null;}
            int tag=fragment.IndexOf('<',position);if(tag<0){Append(fragment[position..]);break;}Append(fragment[position..tag]);
            if(fragment.AsSpan(tag).StartsWith("<!--",StringComparison.Ordinal)){int end=fragment.IndexOf("-->",tag+4,StringComparison.Ordinal);if(end<0)throw new InvalidDataException("HTML comment.");position=end+3;continue;}
            int close=tag+1;char quote='\0';for(;close<fragment.Length&&close-tag<=2048;close++){char c=fragment[close];if(quote!='\0'){if(c==quote)quote='\0';}else if(c is '\'' or '"')quote=c;else if(c=='>')break;}
            if(close>=fragment.Length||close-tag>2048)throw new InvalidDataException("HTML tag bound.");string token=fragment[(tag+1)..close].Trim();position=close+1;if(token.StartsWith('!'))continue;
            bool ending=token.StartsWith('/');if(ending)token=token[1..].TrimStart();int length=0;while(length<token.Length&&char.IsAsciiLetterOrDigit(token[length]))length++;if(length is 0 or >32)throw new InvalidDataException("Malformed HTML tag.");string name=token[..length].ToLowerInvariant();
            if(!ending&&name is "script" or "style" or "template" or "iframe" or "object" or "svg" or "head"){suppressed=name;continue;}
            if(name=="pre"){pre+=ending?-1:1;if(pre is <0 or >64)throw new InvalidDataException("HTML pre nesting.");Line();}
            else if(name is "br" or "p" or "div" or "li" or "tr" or "section" or "article" or "h1" or "h2" or "h3" or "blockquote")Line();
            else if(ending&&name is "td" or "th"){if(output.Length>0&&output[^1]!='\t')output.Append('\t');}
        }
        if(pre!=0)throw new InvalidDataException("Unclosed HTML pre.");return output.ToString().Trim();
    }
    private sealed record RtfState(bool Skip=false,int CodePage=1252,int UnicodeFallback=1,bool Fresh=true,bool Hidden=false);
    private static string Rtf(ReadOnlySpan<byte> bytes)
    {
        if(bytes[^1]==0)bytes=bytes[..^1];string input=Encoding.Latin1.GetString(bytes);if(!input.StartsWith("{\\rtf1",StringComparison.Ordinal))throw new InvalidDataException("RTF header.");
        var stack=new Stack<RtfState>();var state=new RtfState();var output=new StringBuilder();var encoded=new List<byte>();int position=0,fallback=0;bool closed=false;
        void Bound(){if(output.Length>=100002)throw new InvalidDataException("RTF text bound.");}
        void ClearEncoded(){for(int i=0;i<encoded.Count;i++)encoded[i]=0;encoded.Clear();}
        void Flush(){if(encoded.Count==0)return;byte[] memory=encoded.ToArray();try{var encoding=CodePagesEncodingProvider.Instance.GetEncoding(state.CodePage,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback)??throw new InvalidDataException("RTF codepage.");if(!state.Skip&&!state.Hidden)output.Append(encoding.GetString(memory));Bound();}finally{CryptographicOperations.ZeroMemory(memory);ClearEncoded();}}
        void Character(byte value){if(fallback>0){fallback--;return;}if(!state.Skip&&!state.Hidden)encoded.Add(value);}
        try
        {
        while(position<input.Length)
        {
            char c=input[position++];if(closed){if(!char.IsWhiteSpace(c)&&c!='\0')throw new InvalidDataException("Trailing RTF data.");continue;}
            if(c=='{'){Flush();if(stack.Count>=64)throw new InvalidDataException("RTF nesting.");stack.Push(state);state=state with{Fresh=true};continue;}
            if(c=='}'){Flush();if(stack.Count==0)throw new InvalidDataException("RTF group mismatch.");state=stack.Pop();fallback=0;if(stack.Count==0)closed=true;continue;}
            if(c is '\r' or '\n')continue;
            if(c!='\\'){Character((byte)c);state=state with{Fresh=false};continue;}
            if(position>=input.Length)throw new InvalidDataException("RTF escape.");char next=input[position++];
            if(next=='\''){if(position+2>input.Length||!byte.TryParse(input.AsSpan(position,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out byte value))throw new InvalidDataException("RTF hex escape.");position+=2;Character(value);state=state with{Fresh=false};continue;}
            Flush();if(next is '\\' or '{' or '}'){Character((byte)next);continue;}if(next=='*'){state=state with{Skip=true};continue;}if(next is '~' or '_' or '-'){if(fallback>0)fallback--;else if(!state.Skip&&!state.Hidden)output.Append(next=='~'?'\u00a0':next=='_'?'\u2011':'\u00ad');Bound();continue;}
            if(!char.IsAsciiLetter(next))throw new InvalidDataException("RTF control symbol.");int start=position-1;while(position<input.Length&&char.IsAsciiLetter(input[position]))position++;if(position-start>32)throw new InvalidDataException("RTF control bound.");string word=input[start..position];int? argument=null;
            int numberStart=position;if(position<input.Length&&input[position]=='-')position++;while(position<input.Length&&char.IsAsciiDigit(input[position]))position++;if(position>numberStart){if(!int.TryParse(input.AsSpan(numberStart,position-numberStart),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int number))throw new InvalidDataException("RTF argument.");argument=number;}if(position<input.Length&&input[position]==' ')position++;
            if(word=="bin"){int length=argument??-1;if(length<0||length>input.Length-position)throw new InvalidDataException("RTF binary bound.");position+=length;continue;}
            if(state.Fresh&&word is "fonttbl" or "colortbl" or "stylesheet" or "info" or "pict" or "object" or "objdata" or "fldinst" or "header" or "footer" or "annotation" or "listtable" or "listoverridetable" or "generator")state=state with{Skip=true};
            if(word is "upr" or "ud" or "htmlrtf" or "fromhtml" or "fromtext" or "mac" or "pc" or "pca" or "cpg"||word=="fcharset"&&argument is not (0 or 1))throw new InvalidDataException("Unsupported alternate RTF content.");
            if(state.Fresh&&!state.Skip&&word is not ("rtf" or "ansi" or "ansicpg" or "uc" or "u" or "fldrslt" or "field" or "pard" or "plain" or "b" or "i" or "ul" or "ulnone" or "f" or "fs" or "cf" or "highlight" or "lang" or "ltrch" or "rtlch" or "loch" or "hich" or "dbch" or "v" or "super" or "sub" or "nosupersub"))throw new InvalidDataException("Unknown RTF group destination.");
            state=state with{Fresh=false};
            if(word=="v")state=state with{Hidden=argument!=0};
            else if(word=="ansicpg"){int code=argument??0;if(code is not (437 or 850 or 932 or 936 or 949 or 950 or 65001)&&code is not (>=1250 and <=1258))throw new InvalidDataException("Unsupported RTF codepage.");state=state with{CodePage=code};}
            else if(word=="uc"){int count=argument??-1;if(count is <0 or >10)throw new InvalidDataException("RTF Unicode fallback bound.");state=state with{UnicodeFallback=count};}
            else if(word=="u"){int value=argument??int.MaxValue;if(value is <-32768 or >65535)throw new InvalidDataException("RTF Unicode value.");if(!state.Skip&&!state.Hidden){output.Append(unchecked((char)(ushort)value));Bound();}fallback=state.UnicodeFallback;}
            else if(!state.Skip&&!state.Hidden){char symbol=word switch{"par" or "line"=>'\n',"tab"=>'\t',"emdash"=>'\u2014',"endash"=>'\u2013',"bullet"=>'\u2022',"lquote"=>'\u2018',"rquote"=>'\u2019',"ldblquote"=>'\u201c',"rdblquote"=>'\u201d',_=>'\0'};if(symbol!='\0'){if(fallback>0)fallback--;else output.Append(symbol);Bound();}}
        }
        Flush();if(!closed||stack.Count!=0)throw new InvalidDataException("Unclosed RTF group.");return output.ToString();
        }
        finally{ClearEncoded();}
    }
}
