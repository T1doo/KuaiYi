using System.Security.Cryptography;
using System.Text.Json.Nodes;
using KuaiYi.Core;

namespace KuaiYi.Providers;

public enum ApiProtocol { OpenAIChat, AnthropicMessages }
public enum Authentication { None, Bearer, ApiKey }
public enum OutputLimitParameter { MaxCompletionTokens, MaxTokens, Omit }
public enum ModelEventKind { Started, TextDelta, Usage, Completed, Failed }
public sealed record TokenUsage(long? InputTokens, long? OutputTokens);
public sealed record ModelEvent(ModelEventKind Kind, string? Text = null, string? Reason = null, TokenUsage? Usage = null);
public sealed record ModelEventEnvelope(RequestIdentity Identity, ModelEvent Event);

public sealed record ApiProfile(ApiProtocol Protocol, Uri BaseUrl, string Endpoint, string Model,
    Authentication Authentication, OutputLimitParameter OutputLimit = OutputLimitParameter.MaxCompletionTokens,
    int MaxOutputTokens = 4096, bool Stream = true, bool AllowLoopbackHttp = false, string AnthropicVersion = "2023-06-01")
{
    public Uri ResolveEndpoint()
    {
        if (!Enum.IsDefined(Protocol) || !Enum.IsDefined(Authentication) || !Enum.IsDefined(OutputLimit) ||
            string.IsNullOrWhiteSpace(Model) || Model.Length > 512 || MaxOutputTokens is < 1 or > 131072)
            throw new ArgumentException("Invalid API profile.");
        if (!BaseUrl.IsAbsoluteUri || BaseUrl.Scheme is not ("https" or "http") || BaseUrl.UserInfo.Length > 0 ||
            BaseUrl.Query.Length > 0 || BaseUrl.Fragment.Length > 0) throw new ArgumentException("Invalid endpoint base URI.");
        if (BaseUrl.Scheme == "http" && !(BaseUrl.IsLoopback && AllowLoopbackHttp))
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
    public string HeaderValue()
    {
        ObjectDisposedException.ThrowIf(bytes is null, this);
        string value = System.Text.Encoding.UTF8.GetString(bytes!);
        if (value.Length == 0 || value.Any(c => c < 33 || c > 126)) throw new ArgumentException("Invalid credential header.");
        return value;
    }
    public void Dispose()
    {
        byte[]? owned = Interlocked.Exchange(ref bytes, null);
        if (owned is not null) CryptographicOperations.ZeroMemory(owned);
    }
}

public static class PromptBuilder
{
    public static string Build(string style, string domain = "", string readerBackground = "")
    {
        if (style is not ("faithful" or "plain" or "professional" or "explain")) throw new ArgumentException("Unknown style.");
        if (domain.Length > 2000 || readerBackground.Length > 2048) throw new ArgumentException("Prompt context too large.");
        return Read("base") + "\n" + Read(style).Replace("{{domain}}", domain, StringComparison.Ordinal)
            .Replace("{{reader_background}}", readerBackground, StringComparison.Ordinal);
    }
    public static string Read(string name)
    {
        if (name is not ("base" or "faithful" or "plain" or "professional" or "explain" or "image-classify" or "image-explain"))
            throw new ArgumentException("Unknown prompt resource.");
        var assembly = typeof(PromptBuilder).Assembly;
        string resource = "KuaiYi.Prompts." + name + "@1.txt";
        using var stream = assembly.GetManifestResourceStream(resource) ?? throw new InvalidOperationException("Missing prompt resource.");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

public interface IProtocolAdapter
{
    ApiProtocol Protocol { get; }
    JsonObject BuildBody(TranslationRequest request);
    IReadOnlyList<ModelEvent> ParseEvent(string data);
    IReadOnlyList<ModelEvent> ParseNonStreaming(string json);
}
