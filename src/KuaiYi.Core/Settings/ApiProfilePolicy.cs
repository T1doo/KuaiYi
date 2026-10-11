namespace KuaiYi.Core.Settings;
/// <summary>The same deterministic resolver is used by settings preview, validation and transport mapping.</summary>
public static class ApiProfilePolicy
{
    public static Uri Resolve(ApiProfile profile,bool developmentLoopback=false)
    {
        if(!Uri.TryCreate(profile.BaseUrl,UriKind.Absolute,out var basis)||basis.Scheme is not("https" or "http")||basis.UserInfo.Length!=0||basis.Query.Length!=0||basis.Fragment.Length!=0)throw new SettingsValidationException("profile.baseUrl");
        if(basis.Scheme=="http"&&!((basis.IsLoopback&&(profile.AllowLoopbackHttp||developmentLoopback))||!basis.IsLoopback&&profile.AllowInsecureHttp))throw new SettingsValidationException("profile.httpConsent");
        if(!Enum.IsDefined(profile.AddressMode)||!Enum.IsDefined(profile.Protocol))throw new SettingsValidationException("profile.addressMode");
        if(profile.AddressMode==ApiAddressMode.ExactEndpoint)
        {if(profile.EndpointPath.Length!=0)throw new SettingsValidationException("profile.exactEndpointExtraPath");return basis;}
        string normal=basis.AbsolutePath.TrimEnd('/');
        if(normal.EndsWith("/chat/completions",StringComparison.OrdinalIgnoreCase)||normal.EndsWith("/messages",StringComparison.OrdinalIgnoreCase))throw new SettingsValidationException("profile.baseIsCompleteEndpoint");
        string path=profile.EndpointPath.Length==0?(profile.Protocol==ApiProtocol.OpenAIChat?"chat/completions":"messages"):profile.EndpointPath;
        if(path.Length>2048||path.Trim('/').Length==0||path.Any(char.IsControl)||path.Contains('?')||path.Contains('#')||path.Contains('\\')||path.Contains(':')||path.Split('/').Any(s=>s is "." or ".."||s.Contains('%')))throw new SettingsValidationException("profile.endpointPath");
        return new(basis.AbsoluteUri.TrimEnd('/')+"/"+path.TrimStart('/'));
    }
    public static bool HeaderNameAllowed(string name,bool credential)
    {
        if(name.Length is <1 or >64||name.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))return false;
        string value=name.ToLowerInvariant();
        if(value is "host" or "content-length" or "connection" or "transfer-encoding" or "upgrade" or "te" or "trailer" or "expect" or "content-type" or "accept" or "cookie" or "set-cookie"||value.StartsWith("proxy-",StringComparison.Ordinal)||value.StartsWith("sec-",StringComparison.Ordinal))return false;
        return credential||!new[]{"auth","token","secret","password","credential","api-key","api_key","apikey"}.Any(value.Contains);
    }
    public static void ValidateParameters(ApiProfile profile)
    {
        if(!Enum.IsDefined(profile.OutputLimitParameter))throw new SettingsValidationException("profile.outputLimitParameter");
        if(profile.Protocol==ApiProtocol.AnthropicMessages&&(profile.OutputLimitParameter is OutputTokenParameter.MaxCompletionTokens or OutputTokenParameter.Omit))throw new SettingsValidationException("profile.anthropicRequiresMaxTokens");
        if(profile.Temperature is {} temperature&&(!double.IsFinite(temperature)||temperature<0||temperature>(profile.Protocol==ApiProtocol.AnthropicMessages?1:2)))throw new SettingsValidationException("profile.temperature");
        if(profile.Authentication==ApiAuthentication.CustomHeader&&!HeaderNameAllowed(profile.AuthenticationHeader,true))throw new SettingsValidationException("profile.authenticationHeader");
        if(profile.Authentication==ApiAuthentication.CustomHeader&&(profile.Headers.Keys.Any(k=>k.Equals(profile.AuthenticationHeader,StringComparison.OrdinalIgnoreCase))||profile.AuthenticationHeader.Equals("anthropic-version",StringComparison.OrdinalIgnoreCase)))throw new SettingsValidationException("profile.duplicateAuthenticationHeader");
        if(profile.Authentication!=ApiAuthentication.CustomHeader&&profile.AuthenticationHeader.Length!=0)throw new SettingsValidationException("profile.unusedAuthenticationHeader");
        if(profile.SendStoreFalse&&profile.Protocol!=ApiProtocol.OpenAIChat)throw new SettingsValidationException("profile.storeProtocol");
        if(profile.Headers.Count>8||profile.Headers.Any(p=>!HeaderNameAllowed(p.Key,false)||p.Value.Length>2048||p.Value.Any(c=>c<32||c>126)))throw new SettingsValidationException("profile.headers");
        if(profile.Headers.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=profile.Headers.Count)throw new SettingsValidationException("profile.duplicateHeader");
        if(profile.Headers.Any(p=>p.Key.Equals("anthropic-version",StringComparison.OrdinalIgnoreCase)&&!System.Text.RegularExpressions.Regex.IsMatch(p.Value,"^[0-9]{4}-[0-9]{2}-[0-9]{2}$")))throw new SettingsValidationException("profile.anthropicVersion");
        if(profile.MaxContextTokens is {} context&&profile.MaxOutputTokens>context&&profile.OutputLimitParameter!=OutputTokenParameter.Omit)throw new SettingsValidationException("profile.outputExceedsContext");
    }
    public static bool RequestEquivalent(ApiProfile a,ApiProfile b)=>a with{Name=b.Name,Headers=b.Headers}==b&&a.Headers.OrderBy(p=>p.Key,StringComparer.OrdinalIgnoreCase).SequenceEqual(b.Headers.OrderBy(p=>p.Key,StringComparer.OrdinalIgnoreCase));
}
