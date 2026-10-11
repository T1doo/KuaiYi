using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace KuaiYi.NativeHost;

public sealed record InstalledHostPolicy(string Origin,string AppPath,string AppSha256,string PipeName,IReadOnlyDictionary<string,string> Files)
{
    public void VerifyFiles()
    {
        foreach(var item in Files)if(!string.Equals(Digest(Path.Combine(Path.GetDirectoryName(AppPath)!,item.Key)),item.Value,StringComparison.OrdinalIgnoreCase))throw new BrowserException(BrowserFailure.DesktopUntrusted);
    }
    public const string HostName="com.kuaiyi.native_host";
    public static bool IsOrigin(string? origin)=>origin!=null&&Regex.IsMatch(origin,@"\Achrome-extension://[a-p]{32}/\z",RegexOptions.CultureInvariant);
    public static InstalledHostPolicy Load(string[] args)
    {
        if(args.Length is <1 or >2||!IsOrigin(args[0])||(args.Length==2&&!Regex.IsMatch(args[1],@"\A--parent-window=[0-9]{1,20}\z")))throw new BrowserException(BrowserFailure.OriginRejected);
        if(!OperatingSystem.IsWindows())throw new BrowserException(BrowserFailure.UnsupportedPlatform);
        return LoadWindows(args[0]);
    }
    [SupportedOSPlatform("windows")]
    private static InstalledHostPolicy LoadWindows(string origin)
    {
        string manifestPath=Path.Combine(AppContext.BaseDirectory,"com.kuaiyi.native_host.json");
        if(!File.Exists(manifestPath)||!Registered(manifestPath))throw new BrowserException(BrowserFailure.HostNotConfigured);
        using var manifest=JsonDocument.Parse(ReadBounded(manifestPath));var m=manifest.RootElement;
        BrowserEnvelope.Fields(m,"name","description","path","type","allowed_origins");
        if(m.GetProperty("name").GetString()!=HostName||m.GetProperty("type").GetString()!="stdio"||m.GetProperty("path").GetString() is not{} exe||
            !Path.IsPathFullyQualified(exe)||!string.Equals(Path.GetFullPath(exe),Environment.ProcessPath,StringComparison.OrdinalIgnoreCase))throw new BrowserException(BrowserFailure.HostNotConfigured);
        var origins=m.GetProperty("allowed_origins").EnumerateArray().Select(x=>x.GetString()).ToArray();
        if(origins.Length is <1 or >2||origins.Any(x=>!IsOrigin(x))||!origins.Contains(origin,StringComparer.Ordinal))throw new BrowserException(BrowserFailure.OriginRejected);
        // Installed role/integrity receipt is a P7 installation output, not extension-supplied configuration.
        string receiptPath=Path.Combine(AppContext.BaseDirectory,"native-host-trust.json");
        if(!File.Exists(receiptPath))throw new BrowserException(BrowserFailure.HostNotConfigured);
        using var receipt=JsonDocument.Parse(ReadBounded(receiptPath));var r=receipt.RootElement;BrowserEnvelope.Fields(r,"schemaVersion","files");
        if(r.GetProperty("schemaVersion").GetInt32()!=1)throw new BrowserException(BrowserFailure.HostNotConfigured);
        var files=InstalledRoleFiles.Verify(AppContext.BaseDirectory,r.GetProperty("files"));
        string app=Path.Combine(AppContext.BaseDirectory,"KuaiYi.App.exe");string hash=files["KuaiYi.App.exe"];
        using var user=System.Security.Principal.WindowsIdentity.GetCurrent();
        using var process=Process.GetCurrentProcess();
        string sid=user.User?.Value??throw new BrowserException(BrowserFailure.DesktopUntrusted);
        return new(origin,app,hash,$"KuaiYi.{sid}.{process.SessionId}.NativeHost.v1",files);
    }
    [SupportedOSPlatform("windows")]
    private static bool Registered(string path)
    {
        foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64})
        foreach(string browser in new[]{"Google\\Chrome","Microsoft\\Edge"})
        {
            using var user=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,view);using var key=user.OpenSubKey($"Software\\{browser}\\NativeMessagingHosts\\{HostName}",false);
            if(key?.GetValue(null) is string installed&&Path.IsPathFullyQualified(installed)&&string.Equals(Path.GetFullPath(installed),path,StringComparison.OrdinalIgnoreCase))return true;
        }
        return false;
    }
    private static byte[] ReadBounded(string path)
    {
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(file.Length is <=0 or >65536)throw new BrowserException(BrowserFailure.HostNotConfigured);
        byte[] result=new byte[(int)file.Length];file.ReadExactly(result);return result;
    }
    internal static string Digest(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
}
