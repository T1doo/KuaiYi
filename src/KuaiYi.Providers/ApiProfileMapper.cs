using System.Collections.Immutable;
using KuaiYi.Core.Settings;
using SettingsProfile=KuaiYi.Core.Settings.ApiProfile;
namespace KuaiYi.Providers;
public sealed record ApiRequestPlan(ApiProfile Transport,ImmutableArray<string> Notices);
public static class ApiProfileMapper
{
    public static ApiRequestPlan Plan(SettingsProfile profile,DevelopmentTranslationBoundary? boundary=null)
    {
        if(boundary==null&&profile.Model==DevelopmentTranslationBoundary.FixtureModel)throw new ArgumentException("Fixture profile requires explicit development mode.");
        ApiProfilePolicy.ValidateParameters(profile);
        if(boundary!=null&&!boundary.Allows(profile))throw new ArgumentException("Test profile outside authorization.");
        Uri endpoint=ApiProfilePolicy.Resolve(profile,boundary!=null);
        var protocol=profile.Protocol==KuaiYi.Core.Settings.ApiProtocol.OpenAIChat?ApiProtocol.OpenAIChat:ApiProtocol.AnthropicMessages;
        List<string> notices=[];
        bool streaming=profile.Streaming&&profile.Capabilities.Streaming!=Capability.Unsupported;
        if(profile.Streaming&&!streaming)notices.Add("配置不支持流式，使用非流式请求");
        double? temperature=profile.Capabilities.Temperature==Capability.Supported?profile.Temperature:null;
        if(profile.Temperature!=null&&temperature==null)notices.Add("温度能力尚未确认或不支持，省略温度参数");
        var output=profile.OutputLimitParameter switch
        {OutputTokenParameter.Omit=>OutputLimitParameter.Omit,OutputTokenParameter.MaxTokens=>OutputLimitParameter.MaxTokens,OutputTokenParameter.MaxCompletionTokens=>OutputLimitParameter.MaxCompletionTokens,_=>protocol==ApiProtocol.AnthropicMessages?OutputLimitParameter.MaxTokens:OutputLimitParameter.MaxCompletionTokens};
        string version=profile.Headers.FirstOrDefault(p=>p.Key.Equals("anthropic-version",StringComparison.OrdinalIgnoreCase)).Value??"2023-06-01";
        if(profile.Headers.Any(p=>p.Key.Equals("anthropic-version",StringComparison.OrdinalIgnoreCase))&&profile.Protocol!=KuaiYi.Core.Settings.ApiProtocol.AnthropicMessages)throw new SettingsValidationException("profile.anthropicVersionProtocol");
        if(profile.Headers.Any(p=>p.Key.Equals("OpenAI-Beta",StringComparison.OrdinalIgnoreCase)))throw new SettingsValidationException("profile.betaUnsupported");
        var headers=profile.Headers.Where(p=>!p.Key.Equals("anthropic-version",StringComparison.OrdinalIgnoreCase)).ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
        var transport=new ApiProfile(protocol,new(profile.BaseUrl),endpoint.AbsoluteUri,profile.Model,profile.Authentication switch
        {ApiAuthentication.None=>Authentication.None,ApiAuthentication.ApiKey=>Authentication.ApiKey,ApiAuthentication.CustomHeader=>Authentication.CustomHeader,_=>Authentication.Bearer},
            output,profile.MaxOutputTokens,streaming,profile.AllowLoopbackHttp||boundary!=null,version,boundary,temperature,
            streaming&&profile.Capabilities.UsageInStream==Capability.Supported,profile.SendStoreFalse,profile.AllowInsecureHttp,profile.AuthenticationHeader,headers,profile.ImageByteLimit,profile.Capabilities.Vision,profile.TimeoutSeconds);
        _=transport.ResolveEndpoint();return new(transport,notices.ToImmutableArray());
    }
}
