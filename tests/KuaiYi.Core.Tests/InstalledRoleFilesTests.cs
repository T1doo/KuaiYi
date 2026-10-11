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
            if(fault=="valid")Assert.Equal(23,InstalledRoleFiles.Verify(root,doc.RootElement).Count);
            else Assert.Equal(fault.StartsWith("changed-")?BrowserFailure.DesktopUntrusted:BrowserFailure.HostNotConfigured,Assert.Throws<BrowserException>(()=>InstalledRoleFiles.Verify(root,doc.RootElement)).Code);
        }
        finally{Directory.Delete(root,true);}
    }
}
