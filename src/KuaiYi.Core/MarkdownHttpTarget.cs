using System.Globalization;
namespace KuaiYi.Core;

/// <summary>A bounded HTTP(S) address only. Parsing performs no resolution, I/O or resource loading.</summary>
public sealed record MarkdownHttpTarget
{
    public const int MaximumLength=2048;
    public string Url {get;}
    public string Domain {get;}
    private MarkdownHttpTarget(string url,string domain){Url=url;Domain=domain;}
    public static bool TryParse(string? value,out MarkdownHttpTarget? target)
    {
        target=null;
        if(value is not {Length:>0 and <=MaximumLength}||!(value.StartsWith("https://",StringComparison.OrdinalIgnoreCase)||value.StartsWith("http://",StringComparison.OrdinalIgnoreCase)))return false;
        int authorityStart=value.IndexOf("://",StringComparison.Ordinal)+3,authorityEnd=value.IndexOfAny(['/','?','#'],authorityStart);
        if(value.AsSpan(authorityStart,(authorityEnd<0?value.Length:authorityEnd)-authorityStart).Contains('@'))return false;
        for(int i=0;i<value.Length;i++)
        {
            char c=value[i];if(char.IsWhiteSpace(c)||Forbidden(c)||c is '<' or '>' or '"' or '\'' or '(' or ')' or '|')return false;
            if(c=='%'&&(i+2>=value.Length||!Uri.IsHexDigit(value[i+1])||!Uri.IsHexDigit(value[i+2])))return false;
        }
        try
        {
            if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme is not("http" or "https")||uri.UserInfo.Length!=0||uri.IsFile||uri.Host.Length==0)return false;
            string decoded=Uri.UnescapeDataString(value);if(decoded.Any(Forbidden))return false;
            string host=uri.IdnHost.Trim('[',']').ToLowerInvariant();
            if(host.Any(c=>c>127||c is '%' or '/' or '@' or '\\'))return false;
            if(uri.HostNameType==UriHostNameType.Dns)
            {
                var labels=host.TrimEnd('.').Split('.');if(host.Length>253||labels.Any(label=>label.Length is <1 or >63||label[0]=='-'||label[^1]=='-'||label.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-')))return false;
            }
            else if(uri.HostNameType is not(UriHostNameType.IPv4 or UriHostNameType.IPv6))return false;
            string canonical=new UriBuilder(uri){Host=host}.Uri.AbsoluteUri;if(canonical.Length>MaximumLength)return false;
            string domain=uri.HostNameType==UriHostNameType.IPv6?"["+host+"]":host;
            if(!uri.IsDefaultPort)domain+=":"+uri.Port.ToString(CultureInfo.InvariantCulture);
            target=new(canonical,domain);return true;
        }
        catch(Exception error)when(error is UriFormatException or ArgumentException){return false;}
    }
    private static bool Forbidden(char value)=>char.IsControl(value)||char.IsSurrogate(value)||value=='\\'||char.GetUnicodeCategory(value)==UnicodeCategory.Format;
}
