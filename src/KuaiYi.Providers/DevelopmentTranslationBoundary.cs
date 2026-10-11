using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using KuaiYi.Core.Settings;
using SettingsProfile=KuaiYi.Core.Settings.ApiProfile;
namespace KuaiYi.Providers;
/// <summary>Immutable authorization from the explicit command, never reconstructed from later settings.
/// Test transport pins a loopback IP and bypasses proxies/DNS; profile records cannot widen it.</summary>
public sealed class DevelopmentTranslationBoundary
{
    public const string FixtureModel="TEST_ONLY_FIXTURE";
    public Uri Prefix{get;}
    private readonly IPAddress address;
    private DevelopmentTranslationBoundary(Uri prefix,IPAddress address){Prefix=prefix;this.address=address;}
    public static DevelopmentTranslationBoundary FromPrefix(Uri prefix)
    {
        if(!prefix.IsAbsoluteUri||prefix.Scheme!="http"||!prefix.IsLoopback||prefix.UserInfo.Length!=0||prefix.Query.Length!=0||prefix.Fragment.Length!=0||
            !Regex.IsMatch(prefix.AbsolutePath,"^/runs/[a-f0-9]{32}/(fragmented|non-streaming|slow|held-headers|unauthorized|disconnect|stream-error)/?$",RegexOptions.CultureInvariant))throw new ArgumentException("Authorized local fixture prefix required.");
        IPAddress? address;
        if(string.Equals(prefix.IdnHost,"localhost",StringComparison.OrdinalIgnoreCase))address=IPAddress.Loopback;
        else if(!IPAddress.TryParse(prefix.IdnHost.Trim('[',']'),out address)||!IPAddress.IsLoopback(address))throw new ArgumentException("Pinned loopback address required.");
        return new(new(prefix.AbsoluteUri.TrimEnd('/')+"/"),address);
    }
    internal static DevelopmentTranslationBoundary FromProfile(SettingsProfile profile)
    {
        string value=profile.BaseUrl;
        if(profile.AddressMode==ApiAddressMode.ExactEndpoint)
        {
            string suffix=profile.Protocol==KuaiYi.Core.Settings.ApiProtocol.OpenAIChat?"chat/completions":"messages";
            if(!value.EndsWith("/"+suffix,StringComparison.Ordinal))throw new ArgumentException("Fixture endpoint required.");
            value=value[..^suffix.Length];
        }
        var boundary=FromPrefix(new(value,UriKind.Absolute));
        if(!boundary.Allows(profile))throw new ArgumentException("Credential-free fixture profile required.");
        return boundary;
    }
    private Uri Endpoint(ApiProtocol protocol)=>new(Prefix,protocol==ApiProtocol.OpenAIChat?"chat/completions":"messages");
    public bool Allows(SettingsProfile profile)
    {
        if(!Enum.IsDefined(profile.Protocol)||!Enum.IsDefined(profile.AddressMode)||profile.Authentication!=ApiAuthentication.None||profile.SecretRef!=null||
            profile.Model!=FixtureModel||profile.Headers.Count!=0||!Uri.TryCreate(profile.BaseUrl,UriKind.Absolute,out var uri))return false;
        var protocol=profile.Protocol==KuaiYi.Core.Settings.ApiProtocol.OpenAIChat?ApiProtocol.OpenAIChat:ApiProtocol.AnthropicMessages;
        try{return ApiProfilePolicy.Resolve(profile,developmentLoopback:true).AbsoluteUri==Endpoint(protocol).AbsoluteUri&&
            (profile.AddressMode==ApiAddressMode.BasePrefix?uri.AbsoluteUri.TrimEnd('/')+"/"==Prefix.AbsoluteUri:uri.AbsoluteUri==Endpoint(protocol).AbsoluteUri);}
        catch(SettingsValidationException){return false;}
    }
    internal bool AllowsEndpoint(ApiProfile profile,Uri endpoint)=>profile.Authentication==Authentication.None&&profile.Model==FixtureModel&&
        profile.AllowLoopbackHttp&&endpoint.AbsoluteUri==Endpoint(profile.Protocol).AbsoluteUri&&
        (profile.BaseUrl.AbsoluteUri.TrimEnd('/')+"/"==Prefix.AbsoluteUri||profile.BaseUrl.AbsoluteUri==endpoint.AbsoluteUri);
    public bool Allows(ApiProfile profile,SecretLease? secret=null)
    {
        if(secret!=null)return false;
        try{return AllowsEndpoint(profile,profile.ResolveEndpoint());}catch(ArgumentException){return false;}
    }
    internal async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context,CancellationToken cancellation)
    {
        if(context.DnsEndPoint.Port!=Prefix.Port||!string.Equals(context.DnsEndPoint.Host.Trim('[',']'),Prefix.IdnHost.Trim('[',']'),StringComparison.OrdinalIgnoreCase))throw new HttpRequestException("Test connection outside authorization.");
        var socket=new Socket(address.AddressFamily,SocketType.Stream,ProtocolType.Tcp){NoDelay=true};
        try{await socket.ConnectAsync(new IPEndPoint(address,Prefix.Port),cancellation).ConfigureAwait(false);return new NetworkStream(socket,ownsSocket:true);}
        catch{socket.Dispose();throw;}
    }
}
