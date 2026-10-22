using System.Text.Json;
namespace KuaiYi.NativeHost;
public static class InstalledRoleFiles
{
    public static IReadOnlyList<string> Required=>KuaiYi.Core.Installation.InstallationPayload.TrustedFiles;
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
