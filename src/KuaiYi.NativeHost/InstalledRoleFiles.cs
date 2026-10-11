using System.Text.Json;
namespace KuaiYi.NativeHost;
public static class InstalledRoleFiles
{
    public static IReadOnlyList<string> Required {get;}=Array.AsReadOnly(new[]{"KuaiYi.NativeHost.exe","KuaiYi.NativeHost.dll","KuaiYi.NativeHost.deps.json","KuaiYi.NativeHost.runtimeconfig.json","KuaiYi.App.exe","KuaiYi.App.dll","KuaiYi.App.deps.json","KuaiYi.App.runtimeconfig.json","KuaiYi.Core.dll","KuaiYi.Contracts.dll","KuaiYi.Imaging.dll","KuaiYi.Windows.dll","KuaiYi.CaptureNative.dll","KuaiYi.CaptureHost.exe","KuaiYi.CaptureHost.dll","KuaiYi.CaptureHost.deps.json","KuaiYi.CaptureHost.runtimeconfig.json"});
    public static IReadOnlyDictionary<string,string> Verify(string directory,JsonElement files)
    {
        if(files.ValueKind!=JsonValueKind.Object)throw new BrowserException(BrowserFailure.HostNotConfigured);
        var hashes=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var item in files.EnumerateObject())
        {
            if(!Required.Contains(item.Name,StringComparer.Ordinal)||item.Value.ValueKind!=JsonValueKind.String||item.Value.GetString() is not {Length:64} hash||!hash.All(char.IsAsciiHexDigit)||!hashes.TryAdd(item.Name,hash))throw new BrowserException(BrowserFailure.HostNotConfigured);
        }
        if(hashes.Count!=Required.Count)throw new BrowserException(BrowserFailure.HostNotConfigured);
        foreach(var item in hashes)if(!File.Exists(Path.Combine(directory,item.Key))||!string.Equals(InstalledHostPolicy.Digest(Path.Combine(directory,item.Key)),item.Value,StringComparison.OrdinalIgnoreCase))throw new BrowserException(BrowserFailure.DesktopUntrusted);
        return hashes;
    }
}
