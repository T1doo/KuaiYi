using System.Text.RegularExpressions;
using KuaiYi.Core.Settings;
namespace KuaiYi.Providers;
/// <summary>Only --translation-test-provider uses this factory. No fixture output or mock transport in production.</summary>
public static class DevelopmentTranslationProfile
{
    public static AppSettings Create(Uri endpointPrefix)
    {
        if(!endpointPrefix.IsAbsoluteUri||endpointPrefix.Scheme!="http"||!endpointPrefix.IsLoopback||endpointPrefix.UserInfo.Length>0||endpointPrefix.Query.Length>0||endpointPrefix.Fragment.Length>0||
            !Regex.IsMatch(endpointPrefix.AbsolutePath,"^/runs/[a-f0-9]{32}/(fragmented|non-streaming|slow|held-headers|unauthorized|disconnect|stream-error)/?$",RegexOptions.CultureInvariant))throw new ArgumentException("Explicit local test fixture prefix required.");
        return new(){ActiveProfileId="local-test",Profiles=[new(){Id="local-test",Name="本地测试服务（不是模型）",BaseUrl=endpointPrefix.AbsoluteUri,Model="TEST_ONLY_FIXTURE",Authentication=ApiAuthentication.None}]};
    }
}
