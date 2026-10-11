using System.Text;
using KuaiYi.Core;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class OleDroppedTextTests
{
    internal static byte[] Html(string fragment,string prefix="<html><body>public outside<!--StartFragment-->",string suffix="<!--EndFragment-->outside public</body></html>",int? selectedStart=null,int? selectedEnd=null)
    {
        string Header(int htmlStart,int htmlEnd,int start,int end)=>$"Version:1.0\r\nStartHTML:{htmlStart:0000000000}\r\nEndHTML:{htmlEnd:0000000000}\r\nStartFragment:{start:0000000000}\r\nEndFragment:{end:0000000000}\r\n"+(selectedStart.HasValue?$"StartSelection:{start+selectedStart:0000000000}\r\nEndSelection:{start+selectedEnd:0000000000}\r\n":"");
        int header=Encoding.UTF8.GetByteCount(Header(0,0,0,0));int first=header+Encoding.UTF8.GetByteCount(prefix),last=first+Encoding.UTF8.GetByteCount(fragment);return Encoding.UTF8.GetBytes(Header(header,last+Encoding.UTF8.GetByteCount(suffix),first,last)+prefix+fragment+suffix);
    }
    private static string Rtf(string text)=>OleDroppedText.Decode(OleTextFormat.Rtf,Encoding.ASCII.GetBytes(text));
    [Theory][InlineData(false,true,true,true,OleTextFormat.Unicode)][InlineData(false,false,true,true,OleTextFormat.Html)][InlineData(false,false,false,true,OleTextFormat.Rtf)][InlineData(false,false,false,false,OleTextFormat.None)][InlineData(true,true,true,true,OleTextFormat.None)]
    public void FormatPriorityRejectsFilesBeforeText(bool files,bool unicode,bool html,bool rtf,OleTextFormat expected)=>Assert.Equal(expected,OleDroppedText.Choose(files,unicode,html,rtf));
    [Theory][InlineData("a")][InlineData("中文😀")][InlineData("  a\r\n  code\tvalue  ")]
    public void UnicodeCopyPreservesOwnedTextAndStopsAtTerminator(string text)
    {var data=Encoding.Unicode.GetBytes(text+"\0public unused allocation");Assert.Equal(text,OleDroppedText.Decode(OleTextFormat.Unicode,data));}
    [Theory][InlineData("odd")][InlineData("unterminated")][InlineData("high")][InlineData("low")][InlineData("empty")][InlineData("allocation")]
    public void MalformedUnicodeDoesNotFallBackToLowerPriorityFormat(string reason)
    {byte[] bytes=reason switch{"odd"=>[65,0,0],"unterminated"=>[65,0],"high"=>[0,216,0,0],"low"=>[0,220,0,0],"allocation"=>new byte[OleUnicodeText.MaximumGlobalBytes+2],_=>[0,0]};Assert.ThrowsAny<Exception>(()=>OleDroppedText.Decode(OleTextFormat.Unicode,bytes));}
    [Fact]public void HtmlOffsetsAreUtf8BytesAndSurroundingPageIsNeverIncluded()
    {Assert.Equal("中文😀 & public",OleDroppedText.Decode(OleTextFormat.Html,Html("<p>中文😀 &amp; public</p>","<html>中文outside<!--StartFragment-->")));}
    [Fact]public void HtmlSelectionOffsetsOverrideOnlyWithinSelectedFragment()
    {string text="outside中文😀outside";Assert.Equal("中文😀",OleDroppedText.Decode(OleTextFormat.Html,Html(text,selectedStart:Encoding.UTF8.GetByteCount("outside"),selectedEnd:Encoding.UTF8.GetByteCount("outside中文😀"))));}
    [Fact]public void HtmlDropsMarkupImagesCommentsScriptsStylesAndObjectsWithoutExecuting()
    {string html="<p>public<img src='https://public.invalid/image'></p><!--secret--><script>fetch('secret')</script><style>secret</style><template>secret</template><iframe src='secret'>secret</iframe><object>secret</object><svg>secret</svg><head>secret</head><div>visible &lt;code&gt;</div>";Assert.Equal("public\nvisible <code>",OleDroppedText.Decode(OleTextFormat.Html,Html(html)));}
    [Fact]public void HtmlQuotedAttributesPreIndentAndTableParagraphsAreInertText()
    {string value=OleDroppedText.Decode(OleTextFormat.Html,Html("<p title='a > b'>first  value</p><pre>a\n  code\t中</pre><table><tr><td>A</td><td>B</td></tr></table>"));Assert.Equal("first value\na\n  code\t中\nA\tB",value);}
    [Theory][InlineData("<script>secret")][InlineData("<!--secret")][InlineData("<pre>public")][InlineData("<script>secret</scriptx>public")][InlineData("<template><template>secret</template>secret</template>")][InlineData("<p x='")]
    public void MalformedOrAmbiguousHtmlIsRejected(string text)=>Assert.ThrowsAny<Exception>(()=>OleDroppedText.Decode(OleTextFormat.Html,Html(text)));
    [Theory][InlineData("missing")][InlineData("outside")][InlineData("duplicate")][InlineData("utf8")][InlineData("selection")][InlineData("header")][InlineData("overflow")]
    public void HtmlHeaderAndUtf8AreValidatedBeforeExtracting(string kind)
    {
        byte[] bytes=Html("public");string text=Encoding.UTF8.GetString(bytes);
        bytes=kind switch{"missing"=>Encoding.UTF8.GetBytes(text.Replace("EndFragment:","OtherFieldX:")),"outside"=>Encoding.UTF8.GetBytes(text.Replace("StartFragment:0000000150","StartFragment:9999999999")),"duplicate"=>Encoding.UTF8.GetBytes("StartFragment:0000000100\r\n"+text),"utf8"=>Html("中"),"selection"=>Html("public",selectedStart:-1,selectedEnd:2),"header"=>Encoding.UTF8.GetBytes(text.Replace(text.Split('\n').Single(l=>l.StartsWith("StartFragment:")),"StartFragment:0000000001\r")),"overflow"=>Encoding.UTF8.GetBytes(text.Replace("EndHTML:","EndHTML:999999999999999999999999")),_=>bytes};
        if(kind=="outside")bytes=Encoding.UTF8.GetBytes(text.Replace(text.Split('\n').Single(l=>l.StartsWith("StartFragment:")),"StartFragment:9999999999\r"));if(kind=="utf8"){int offset=Encoding.UTF8.GetString(bytes).IndexOf("中",StringComparison.Ordinal);bytes[offset]=0xff;}
        Assert.ThrowsAny<Exception>(()=>OleDroppedText.Decode(OleTextFormat.Html,bytes));
    }
    [Theory][InlineData(@"{\rtf1\ansi public\par  code\tab value}","public\n code\tvalue")][InlineData(@"{\rtf1 a\{b\}\\c}","a{b}\\c")][InlineData(@"{\rtf1\ansi\ansicpg1252 caf\'e9}","café")][InlineData(@"{\rtf1\ansi\ansicpg936 \'d6\'d0\'ce\'c4}","中文")][InlineData(@"{\rtf1\uc1\u20013?\u25991?\u-10179?\u-8704?}","中文😀")][InlineData(@"{\rtf1\uc0\u20013\u25991}","中文")]
    public void RtfEscapesUnicodeAndCodepagesPreserveActualText(string rtf,string expected)=>Assert.Equal(expected,Rtf(rtf));
    [Fact]public void RtfHiddenDestinationsFieldsAndEmbeddedObjectsProduceOnlyVisibleBody()
    {Assert.Equal("public link visible",Rtf(@"{\rtf1{\fonttbl secret}{\colortbl secret}{\info secret}{\pict\bin4 abcd}{\object secret}{\*\unknown secret}public {\field{\*\fldinst HYPERLINK secret}{\fldrslt link}}{\v secret} visible}"));}
    [Theory][InlineData("public")][InlineData(@"{\rtf1 public")][InlineData(@"{\rtf1 public}data")][InlineData(@"{\rtf1{\unknown public}}")][InlineData(@"{\rtf1\ansicpg9999 public}")][InlineData(@"{\rtf1\uc11 public}")][InlineData(@"{\rtf1\u999999 public}")][InlineData(@"{\rtf1\bin100 public}")][InlineData(@"{\rtf1\'zz}")][InlineData(@"{\rtf1\upr public}")][InlineData(@"{\rtf1\u-10179?}")][InlineData(@"{\rtf1\ansicpg936 \'d6}")]
    [InlineData(@"{\rtf1\mac public}")][InlineData(@"{\rtf1{\fonttbl{\f0\fcharset134 font}} public}")][InlineData(@"{\rtf1\cpg932 public}")]
    public void UnsupportedOrMalformedRtfIsBoundedlyRejected(string value)=>Assert.ThrowsAny<Exception>(()=>Rtf(value));
    [Fact]public void RtfNestingAndControlBoundsRejectOversizedGrammar()
    {Assert.ThrowsAny<Exception>(()=>Rtf(@"{\rtf1 "+new string('{',65)+"public"+new string('}',66)));Assert.ThrowsAny<Exception>(()=>Rtf(@"{\rtf1 \"+new string('a',33)+" public}"));}
    [Theory][InlineData(OleTextFormat.Unicode)][InlineData(OleTextFormat.Html)][InlineData(OleTextFormat.Rtf)]
    public void EveryFormatHasAllocationAndScalarLimits(OleTextFormat format)
    {Assert.ThrowsAny<Exception>(()=>OleDroppedText.Decode(format,new byte[OleDroppedText.ByteLimit(format)+1]));string body=new string('a',50001);byte[] bytes=format switch{OleTextFormat.Unicode=>Encoding.Unicode.GetBytes(body+"\0"),OleTextFormat.Html=>Html(body),_=>Encoding.ASCII.GetBytes(@"{\rtf1 "+body+"}")};Assert.ThrowsAny<Exception>(()=>OleDroppedText.Decode(format,bytes));}
    [Theory][InlineData("https://public.invalid/path")][InlineData("mailto:public@example.invalid")][InlineData("javascript:alert(1)")][InlineData("data:text/plain,public")]
    public void UrlOnlyDropsAreRejectedInAllFormats(string value)
    {Assert.ThrowsAny<Exception>(()=>OleDroppedText.Decode(OleTextFormat.Unicode,Encoding.Unicode.GetBytes(value+"\0")));Assert.ThrowsAny<Exception>(()=>OleDroppedText.Decode(OleTextFormat.Html,Html(value)));Assert.ThrowsAny<Exception>(()=>Rtf(@"{\rtf1 "+value+"}"));}
}
