using System.Net;
using System.Net.Http.Headers;
using KuaiYi.Core;
namespace KuaiYi.Providers;

internal static class RetryAfterParser
{
    internal const int MaximumHeaderCharacters=128;
    internal static RetryAfterAdvice? Read(HttpResponseMessage response,DateTimeOffset receivedAt)
    {
        if(response.StatusCode is not(HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)||
            !response.Headers.NonValidated.TryGetValues("Retry-After",out var values)||values.Count!=1)return null;
        string raw=values.Single();
        if(raw.Length>MaximumHeaderCharacters||!RetryConditionHeaderValue.TryParse(raw,out var parsed))return null;
        TimeSpan? delay=parsed.Delta??(parsed.Date is {} date?date-receivedAt:null);
        return delay is {} wait?RetryAfterAdvice.FromDelay(receivedAt,wait):null;
    }
}
