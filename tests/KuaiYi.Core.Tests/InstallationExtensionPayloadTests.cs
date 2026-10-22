using System.Text.Json;
using System.Text.Json.Nodes;
using KuaiYi.Core.Installation;
using Xunit;
using Xunit.Abstractions;
namespace KuaiYi.Core.Tests;

public sealed class InstallationExtensionPayloadTests(ITestOutputHelper output)
{
    private sealed class Rig:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-extension-contract-"+Guid.NewGuid().ToString("N"));
        internal string App=>Path.Combine(Root,"application");internal string Data=>Path.Combine(Root,"user-data");
        internal Rig()=>Directory.CreateDirectory(Data);
        internal MaterializedPayloadSource Source()=>new(Path.Combine(Root,"source"));
        internal InstallationStore Store()=>new(App,Data,()=>true);
        public void Dispose()=>Directory.Delete(Root,true);
    }
    [Theory]
    [InlineData("optional-permission")][InlineData("optional-empty")][InlineData("content-script")][InlineData("content-empty")]
    [InlineData("unknown-field")][InlineData("background-path")][InlineData("background-shape")][InlineData("action-popup")]
    [InlineData("options-path")][InlineData("icons-path")][InlineData("csp")][InlineData("duplicate-field")]
    [InlineData("host-permission")][InlineData("optional-host-permission")][InlineData("permission-duplicate")]
    public async Task RepinnedSourceCannotExpandOrReplaceTheDeclaredExtensionContract(string fault)
    {
        using var r=new Rig();var source=r.Source();string original=source.Digest;string path=Path.Combine(source.Root,"browser-extension/manifest.json");var manifest=JsonNode.Parse(File.ReadAllBytes(path))!;
        switch(fault)
        {
            case "optional-permission":manifest["optional_permissions"]=new JsonArray("tabs");break;
            case "optional-empty":manifest["optional_permissions"]=new JsonArray();break;
            case "content-script":manifest["content_scripts"]=new JsonArray(new JsonObject{["matches"]=new JsonArray("<all_urls>"),["js"]=new JsonArray("acquire.js")});break;
            case "content-empty":manifest["content_scripts"]=new JsonArray();break;
            case "unknown-field":manifest["externally_connectable"]=new JsonObject{["matches"]=new JsonArray("https://synthetic.invalid/*")};break;
            case "background-path":manifest["background"]!["service_worker"]="acquire.js";break;
            case "background-shape":manifest["background"]!["scripts"]=new JsonArray("acquire.js");break;
            case "action-popup":manifest["action"]!["default_popup"]="options.html";break;
            case "options-path":manifest["options_page"]="https://synthetic.invalid/options";break;
            case "icons-path":manifest["icons"]!["16"]="https://synthetic.invalid/icon.png";break;
            case "csp":manifest["content_security_policy"]!["extension_pages"]="script-src 'self'; object-src 'self'";break;
            case "host-permission":manifest["host_permissions"]=new JsonArray("<all_urls>");break;
            case "optional-host-permission":manifest["optional_host_permissions"]=new JsonArray("<all_urls>");break;
            case "permission-duplicate":manifest["permissions"]=new JsonArray("contextMenus","nativeMessaging","activeTab","activeTab");break;
        }
        string json=manifest.ToJsonString();if(fault=="duplicate-field")json=json[..^1]+",\"permissions\":[\"contextMenus\",\"nativeMessaging\",\"activeTab\"]}";
        File.WriteAllText(path,json);source.Catalog();Assert.NotEqual(original,source.Digest);output.WriteLine($"PUBLIC_REPINNED_SOURCE fault={fault} expectedSourceSha={source.Digest}");
        await Assert.ThrowsAsync<InvalidDataException>(()=>r.Store().ApplyAsync(source.Root,source.Digest));Assert.False(Directory.Exists(r.App));
    }
    [Fact]
    public async Task ExistingLocalStructureWithPresentationChangesAndReorderedPermissionsStillMaterializes()
    {
        using var r=new Rig();var source=r.Source();string path=Path.Combine(source.Root,"browser-extension/manifest.json");var manifest=JsonNode.Parse(File.ReadAllBytes(path))!;
        manifest["name"]="PUBLIC_KUAIYI";manifest["description"]="PUBLIC_PRESENTATION_ONLY";manifest["action"]!["default_title"]="PUBLIC_LOCAL_STATUS";manifest["permissions"]=new JsonArray("activeTab","contextMenus","nativeMessaging");File.WriteAllText(path,manifest.ToJsonString());source.Catalog();
        var installed=await r.Store().ApplyAsync(source.Root,source.Digest);Assert.Equal(installed,r.Store().ReadActive());Assert.Equal(source.Digest,InstallationPayload.RequireMaterialized(installed.DirectoryPath).SourceCatalogSha256);
        output.WriteLine($"PUBLIC_EXTENSION_CONTRACT_ACCEPTED sourceSha={source.Digest} installedSha={installed.CatalogSha256} runtimeNative=StructuralFixtureOnly WindowsExecution=NotRun");
    }
}
