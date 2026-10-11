using KuaiYi.Core.Settings;
namespace KuaiYi.Providers;
/// <summary>Only --translation-test-provider uses this factory. No fixture output or mock transport in production.</summary>
public static class DevelopmentTranslationProfile
{
    public static AppSettings Create(Uri endpointPrefix)
    {
        var boundary=DevelopmentTranslationBoundary.FromPrefix(endpointPrefix);
        return new(){ActiveProfileId="local-test",Profiles=[new(){Id="local-test",Name="本地测试服务（不是模型）",BaseUrl=boundary.Prefix.AbsoluteUri,Model=DevelopmentTranslationBoundary.FixtureModel,Authentication=ApiAuthentication.None,AllowLoopbackHttp=true}]};
    }
}
