namespace KuaiYi.GrowthHarness;

// This is a diagnostic execution gate, never product/source authority.
internal sealed record UiPolishRequest(string Output,string Side,string View,string ProductHead);
internal static class UiPolishContract
{
    internal const string Before="e4d57e77426ab235c7219b375debd76bc072e00b";
    internal const string After="e12c1ed8ff351f8300dbdaca18585d7994493317";
    internal const string Branch="refs/heads/dev/ui-polish-visual-r03";
    internal static bool IsCommand(string[] args)=>args.Length>0&&args[0].StartsWith("--ci-ui-polish",StringComparison.Ordinal);
    internal static UiPolishRequest Authorize(string[] args,bool windows,Func<string,string?> env)
    {
        if(args.Length!=4||args[0]!="--ci-ui-polish"||args[2] is not("before" or "after")||args[3] is not("settings" or "overlay"))
            throw new ArgumentException("Expected --ci-ui-polish <exclusive-output> <before|after> <settings|overlay>.");
        if(!windows||env("KUAIYI_UI_POLISH_ALLOW_WINDOWS")!="1"||env("GITHUB_ACTIONS")!="true"||
            env("GITHUB_REPOSITORY")!="T1doo/KuaiYi"||env("GITHUB_REF")!=Branch||env("GITHUB_RUN_ATTEMPT")!="1")
            throw new InvalidOperationException("New one-attempt Windows visual window is disabled; no application created.");
        return new(args[1],args[2],args[3],args[2]=="before"?Before:After);
    }
    internal static void RequireProductVersion(UiPolishRequest request,string? informationalVersion)
    {
        if(informationalVersion?.Split('+') is not [_,var commit]||commit!=request.ProductHead)
            throw new InvalidOperationException("Loaded App revision differs from the fixed comparison side.");
    }
}
