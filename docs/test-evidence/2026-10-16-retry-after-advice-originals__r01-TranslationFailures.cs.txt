namespace KuaiYi.Core;
// Bounded request metadata. No provider text, exception message, URL, key, or arbitrary code is retained.
public enum TranslationFailureReason
{RequestIncomplete,OutputTruncated,ContentRefused,Interrupted,RequestTimeout,ConnectTimeout,HeadersTimeout,FirstResponseTimeout,IdleTimeout,Authentication,Endpoint,RateLimited,Network,Tls,InvalidProtocol,ProviderError,LocalOutputLimit,SourceOrSettingsChanged,UnsupportedResponse,LocalBoundary}
public static class TranslationFailures
{
    public static TranslationFailureReason Normalize(string? code)=>code switch
    {
        "OUTPUT_TRUNCATED"=>TranslationFailureReason.OutputTruncated,"CONTENT_REFUSED"=>TranslationFailureReason.ContentRefused,
        "STREAM_INTERRUPTED"=>TranslationFailureReason.Interrupted,"REQUEST_TIMEOUT"=>TranslationFailureReason.RequestTimeout,
        "CONNECT_TIMEOUT"=>TranslationFailureReason.ConnectTimeout,"RESPONSE_HEADERS_TIMEOUT"=>TranslationFailureReason.HeadersTimeout,
        "FIRST_RESPONSE_TIMEOUT"=>TranslationFailureReason.FirstResponseTimeout,"READ_IDLE_TIMEOUT"=>TranslationFailureReason.IdleTimeout,
        "AUTH_FAILED" or "SECRET_UNAVAILABLE"=>TranslationFailureReason.Authentication,
        "ENDPOINT_NOT_FOUND" or "CONFIG_INVALID"=>TranslationFailureReason.Endpoint,"RATE_LIMITED"=>TranslationFailureReason.RateLimited,
        "NETWORK_ERROR"=>TranslationFailureReason.Network,"TLS_ERROR"=>TranslationFailureReason.Tls,
        "INVALID_PROTOCOL" or "INVALID_JSON"=>TranslationFailureReason.InvalidProtocol,
        "PROVIDER_ERROR" or "PROVIDER_HTTP_ERROR" or "SERVICE_UNAVAILABLE"=>TranslationFailureReason.ProviderError,
        "LOCAL_OUTPUT_LIMIT"=>TranslationFailureReason.LocalOutputLimit,
        "CANCELLED_OR_UNCONSENTED" or "SOURCE_OR_SETTINGS_CHANGED"=>TranslationFailureReason.SourceOrSettingsChanged,
        "UNSUPPORTED_OR_REFUSED" or "UNSUPPORTED_TOOL_RESPONSE"=>TranslationFailureReason.UnsupportedResponse,
        "TEST_BOUNDARY_REJECTED"=>TranslationFailureReason.LocalBoundary,_=>TranslationFailureReason.RequestIncomplete
    };
}
