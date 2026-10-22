using System.Security.Cryptography;
using System.Text.Json;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class InstalledRoleFilesTests
{
    [Theory][InlineData("valid")][InlineData("changed-KuaiYi.OleTransfer.dll")][InlineData("changed-KuaiYi.OleNativeBridge.dll")][InlineData("changed-KuaiYi.OleReceiver.exe")][InlineData("changed-KuaiYi.OleReceiver.dll")][InlineData("changed-KuaiYi.OleReceiver.deps.json")][InlineData("changed-KuaiYi.OleReceiver.runtimeconfig.json")][InlineData("changed-KuaiYi.Windows.dll")][InlineData("changed-KuaiYi.CaptureNative.dll")][InlineData("changed-KuaiYi.CaptureHost.exe")][InlineData("changed-KuaiYi.CaptureHost.dll")][InlineData("missing-dll")][InlineData("changed-host-dll")][InlineData("changed-app-dll")][InlineData("path")][InlineData("duplicate")][InlineData("unknown")]
    public void EntireManagedRoleMustMatchReceipt(string fault)
    {
        string root=Path.Combine(Path.GetTempPath(),"KuaiYi.RoleFixture."+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var files=new Dictionary<string,string>();
            foreach(string name in InstalledRoleFiles.Required){byte[] bytes=System.Text.Encoding.UTF8.GetBytes("public synthetic "+name);File.WriteAllBytes(Path.Combine(root,name),bytes);files.Add(name,Convert.ToHexString(SHA256.HashData(bytes)));}
            if(fault=="missing-dll")files.Remove("KuaiYi.NativeHost.dll");
            if(fault=="changed-host-dll")File.WriteAllText(Path.Combine(root,"KuaiYi.NativeHost.dll"),"modified synthetic");
            if(fault=="changed-app-dll")File.WriteAllText(Path.Combine(root,"KuaiYi.App.dll"),"modified synthetic");
            if(fault is "changed-KuaiYi.OleTransfer.dll" or "changed-KuaiYi.OleNativeBridge.dll" or "changed-KuaiYi.OleReceiver.exe" or "changed-KuaiYi.OleReceiver.dll" or "changed-KuaiYi.OleReceiver.deps.json" or "changed-KuaiYi.OleReceiver.runtimeconfig.json" or "changed-KuaiYi.Windows.dll" or "changed-KuaiYi.CaptureNative.dll" or "changed-KuaiYi.CaptureHost.exe" or "changed-KuaiYi.CaptureHost.dll")File.WriteAllText(Path.Combine(root,fault[8..]),"modified synthetic");
            if(fault=="path")files.Add("../other.dll",new string('0',64));if(fault=="unknown")files.Add("Extra.dll",new string('0',64));
            string json=JsonSerializer.Serialize(files);if(fault=="duplicate")json=json[..^1]+",\"KuaiYi.NativeHost.exe\":\""+files["KuaiYi.NativeHost.exe"]+"\"}";
            using var doc=JsonDocument.Parse(json);
            if(fault=="valid")Assert.Equal(48,InstalledRoleFiles.Verify(root,doc.RootElement).Count);
            else Assert.Equal(fault.StartsWith("changed-")?BrowserFailure.DesktopUntrusted:BrowserFailure.HostNotConfigured,Assert.Throws<BrowserException>(()=>InstalledRoleFiles.Verify(root,doc.RootElement)).Code);
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void SevenRolesAndRuntimeReceiptPinsEveryRequiredFile()
    {
        string[] roles=["KuaiYi.App","KuaiYi.NativeHost","KuaiYi.CaptureBroker","KuaiYi.ClipboardHost","KuaiYi.CaptureHost","KuaiYi.OleReceiver","KuaiYi.ImageWorker"];
        string[] common=["KuaiYi.Core.dll","KuaiYi.Contracts.dll","KuaiYi.Imaging.dll","KuaiYi.Windows.dll","KuaiYi.Providers.dll","KuaiYi.OleTransfer.dll","KuaiYi.CaptureNative.dll","KuaiYi.OcrNative.dll","KuaiYi.OleNativeBridge.dll","KuaiYi.ShellExtension.dll","libSkiaSharp.dll","hostfxr.dll","hostpolicy.dll","coreclr.dll","wpfgfx_cor3.dll","SkiaSharp.dll","System.Private.CoreLib.dll","PresentationFramework.dll","PresentationCore.dll","WindowsBase.dll"];
        string[] expected=roles.SelectMany(r=>new[]{r+".exe",r+".dll",r+".deps.json",r+".runtimeconfig.json"}).Concat(common).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(48,expected.Length);Assert.Equal(expected,InstalledRoleFiles.Required);
        string root=Path.Combine(Path.GetTempPath(),"KuaiYi.CompleteRoleFixture."+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            byte[] original="PUBLIC_COMPLETE_ROLE_FIXTURE"u8.ToArray();string hash=Convert.ToHexString(SHA256.HashData(original));
            var files=expected.ToDictionary(n=>n,_=>hash,StringComparer.Ordinal);foreach(string name in expected)File.WriteAllBytes(Path.Combine(root,name),original);
            using(var valid=JsonDocument.Parse(JsonSerializer.Serialize(files)))Assert.Equal(expected,InstalledRoleFiles.Verify(root,valid.RootElement).Keys.Order(StringComparer.Ordinal));
            foreach(string name in expected)
            {
                files.Remove(name);using(var missingReceipt=JsonDocument.Parse(JsonSerializer.Serialize(files)))Assert.Equal(BrowserFailure.HostNotConfigured,Assert.Throws<BrowserException>(()=>InstalledRoleFiles.Verify(root,missingReceipt.RootElement)).Code);files.Add(name,hash);
                using var complete=JsonDocument.Parse(JsonSerializer.Serialize(files));string path=Path.Combine(root,name);File.Delete(path);
                Assert.Equal(BrowserFailure.DesktopUntrusted,Assert.Throws<BrowserException>(()=>InstalledRoleFiles.Verify(root,complete.RootElement)).Code);
                File.WriteAllText(path,"PUBLIC_CHANGED_ROLE_FIXTURE");Assert.Equal(BrowserFailure.DesktopUntrusted,Assert.Throws<BrowserException>(()=>InstalledRoleFiles.Verify(root,complete.RootElement)).Code);File.WriteAllBytes(path,original);
            }
        }
        finally{Directory.Delete(root,true);}
    }

}
