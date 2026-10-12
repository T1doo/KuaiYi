using System.Security.Cryptography;
using System.Text.Json.Nodes;
using KuaiYi.Core;

namespace KuaiYi.Providers;

public enum ApiProtocol { OpenAIChat, AnthropicMessages }
public enum Authentication { None, Bearer, ApiKey, CustomHeader }
public enum OutputLimitParameter { MaxCompletionTokens, MaxTokens, Omit, ProtocolDefault }
public enum ModelEventKind { Started, TextDelta, Usage, Completed, Failed }
public sealed record TokenUsage(long? InputTokens, long? OutputTokens);
public sealed record ModelEvent(ModelEventKind Kind, string? Text = null, string? Reason = null, TokenUsage? Usage = null);
public sealed record ModelEventEnvelope(RequestIdentity Identity, ModelEvent Event);

public sealed record ApiProfile(ApiProtocol Protocol, Uri BaseUrl, string Endpoint, string Model,
    Authentication Authentication, OutputLimitParameter OutputLimit = OutputLimitParameter.ProtocolDefault,
    int MaxOutputTokens = 4096, bool Stream = true, bool AllowLoopbackHttp = false, string AnthropicVersion = "2023-06-01",
    DevelopmentTranslationBoundary? DevelopmentBoundary = null,double? Temperature=null,bool IncludeUsage=false,bool SendStoreFalse=false,
    bool AllowInsecureHttp=false,string AuthenticationHeader="",System.Collections.Immutable.ImmutableDictionary<string,string>? Headers=null,
    int ImageByteLimit=4194304,KuaiYi.Core.Settings.Capability? VisionCapability=null,int TotalTimeoutSeconds=120)
{
    public bool IsDevelopmentFixture=>DevelopmentBoundary?.Allows(this)==true;
    public Uri ResolveEndpoint()
    {
        var endpoint=ResolveEndpointCore();
        if(DevelopmentBoundary is {} boundary&&!boundary.AllowsEndpoint(this,endpoint))throw new ArgumentException("Test transport outside authorization.");
        return endpoint;
    }
    private Uri ResolveEndpointCore()
    {
        if (!Enum.IsDefined(Protocol) || !Enum.IsDefined(Authentication) || !Enum.IsDefined(OutputLimit) ||
            string.IsNullOrWhiteSpace(Model) || Model.Length > 512 || MaxOutputTokens is < 1 or > 131072)
            throw new ArgumentException("Invalid API profile.");
        if (!BaseUrl.IsAbsoluteUri || BaseUrl.Scheme is not ("https" or "http") || BaseUrl.UserInfo.Length > 0 ||
            BaseUrl.Query.Length > 0 || BaseUrl.Fragment.Length > 0) throw new ArgumentException("Invalid endpoint base URI.");
        if (Temperature is {} temperature&&(!double.IsFinite(temperature)||temperature<0||temperature>(Protocol==ApiProtocol.AnthropicMessages?1:2))||ImageByteLimit is <65536 or >4194304||TotalTimeoutSeconds is <1 or >600)throw new ArgumentException("Invalid optional API parameter.");
        if(Protocol==ApiProtocol.AnthropicMessages&&OutputLimit is not(OutputLimitParameter.MaxTokens or OutputLimitParameter.ProtocolDefault))throw new ArgumentException("Anthropic requires max_tokens.");
        if(Authentication==Authentication.CustomHeader&&!KuaiYi.Core.Settings.ApiProfilePolicy.HeaderNameAllowed(AuthenticationHeader,true))throw new ArgumentException("Invalid credential header.");
        if(Headers?.Any(p=>!KuaiYi.Core.Settings.ApiProfilePolicy.HeaderNameAllowed(p.Key,false)||p.Value.Length>2048||p.Value.Any(c=>c<32||c>126))==true)throw new ArgumentException("Invalid non-secret header.");
        if (BaseUrl.Scheme == "http" && !(BaseUrl.IsLoopback&&AllowLoopbackHttp||!BaseUrl.IsLoopback&&AllowInsecureHttp))
            throw new ArgumentException("HTTP requires explicit loopback permission in this probe implementation.");
        if (Endpoint.Length > 2048 || Endpoint.Contains('?') || Endpoint.Contains('#') || Endpoint.Contains('\\'))
            throw new ArgumentException("Invalid endpoint path.");
        if (AnthropicVersion.Length > 64 || AnthropicVersion.Any(c => c < 32 || c > 126))
            throw new ArgumentException("Invalid Anthropic version header.");
        if (Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || Endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var complete)) throw new ArgumentException("Invalid complete endpoint.");
            var origin = new EndpointOrigin(complete);
            if (origin != new EndpointOrigin(BaseUrl)) throw new ArgumentException("Full endpoint must use explicitly configured origin.");
            if (complete.Query.Length != 0 || complete.Fragment.Length != 0) throw new ArgumentException("Invalid complete endpoint.");
            return complete;
        }
        if (Endpoint.Contains(':')) throw new ArgumentException("Invalid endpoint scheme.");
        if (Endpoint.Split('/').Any(s => s is "." or ".." || s.Contains('%'))) throw new ArgumentException("Ambiguous endpoint path.");
        return new Uri(BaseUrl.AbsoluteUri.TrimEnd('/') + "/" + Endpoint.TrimStart('/'), UriKind.Absolute);
    }
}

public sealed record TranslationRequest(ApiProfile Profile, string SystemPrompt, string SourceText,
    byte[]? Image = null, string ImageMime = "image/png");

/// <summary>Transient secret access. Headers inevitably contain managed string copies; no absolute erasure claim.</summary>
public sealed class SecretLease : IDisposable
{
    private byte[]? bytes;
    public SecretLease(ReadOnlySpan<byte> utf8) => bytes = utf8.ToArray();
    public string HeaderValue(bool allowSpaces=false)
    {
        ObjectDisposedException.ThrowIf(bytes is null, this);
        string value = System.Text.Encoding.UTF8.GetString(bytes!);
        if (value.Length == 0 || value.Any(c => c < 32 || c > 126||!allowSpaces&&c==' ')) throw new ArgumentException("Invalid credential header.");
        return value;
    }
    public void Dispose()
    {
        byte[]? owned = Interlocked.Exchange(ref bytes, null);
        if (owned is not null) CryptographicOperations.ZeroMemory(owned);
    }
}


public interface IProtocolAdapter
{
    ApiProtocol Protocol { get; }
    JsonObject BuildBody(TranslationRequest request);
    IReadOnlyList<ModelEvent> ParseEvent(string data);
    IReadOnlyList<ModelEvent> ParseNonStreaming(string json);
}
