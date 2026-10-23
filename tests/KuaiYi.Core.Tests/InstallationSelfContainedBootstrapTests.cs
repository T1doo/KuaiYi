using System.Text.Json;
using System.Text.Json.Nodes;
using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;

// Component metadata only: no synthetic self-contained directory, publish receipt or runtime execution.
public sealed class InstallationSelfContainedBootstrapTests
{
    private static JsonNode Contract()
    {
        using var stream=typeof(InstallationStore).Assembly.GetManifestResourceStream("KuaiYi.BootstrapRuntime")!;
        return JsonNode.Parse(stream)!;
    }
    private static InstallationPackage Table(string fault)
    {
        var files=InstallationBootstrapPackage.Files.Select(p=>new InstallationFile(p,10,new string('A',64))).Concat(InstallationBootstrapPackage.RuntimeFiles).ToList();
        int at=files.FindIndex(f=>f.Path=="hostfxr.dll");
        if(fault=="hash")files[at]=files[at] with{Sha256=new string('0',64)};
        if(fault=="size")files[at]=files[at] with{Bytes=files[at].Bytes+1};
        if(fault=="missing")files.RemoveAt(at);
        if(fault=="extra")files.Add(new("PUBLIC_EXTRA.dll",10,new string('A',64)));
        if(fault=="alias")files[at]=files[at] with{Path="HOSTFXR.dll"};
        return InstallationPackage.Parse(JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",version="1.0",files=files.Select(f=>new{path=f.Path,bytes=f.Bytes,sha256=f.Sha256})}));
    }
    [Theory][InlineData("valid")][InlineData("hash")][InlineData("size")][InlineData("missing")][InlineData("extra")][InlineData("alias")]
    public void FixedRuntimeEntryTableCannotBeRePinnedIntoADifferentBootstrapMode(string fault)
    {
        var table=Table(fault);
        if(fault!="valid"){Assert.Throws<InvalidDataException>(()=>InstallationBootstrapPackage.RequireRuntimeEntries(table));return;}
        Assert.Equal(194,table.Manifest.Files.Count);Assert.Equal(189,InstallationBootstrapPackage.RuntimeFiles.Count);
        Assert.Equal(InstallationBootstrapPackage.SelfContainedMode,InstallationBootstrapPackage.ModeOf(table));InstallationBootstrapPackage.RequireRuntimeEntries(table);
        Assert.Equal(194,InstallationBootstrapPackage.FilesForMode(InstallationBootstrapPackage.SelfContainedMode).Count);
        Assert.Equal(5,InstallationBootstrapPackage.FilesForMode(InstallationBootstrapPackage.Mode).Count);
        Assert.Equal(80374232,InstallationBootstrapPackage.RuntimeFiles.Where(f=>!f.Path.StartsWith("licenses/",StringComparison.Ordinal)).Sum(f=>f.Bytes));
        Assert.Throws<InvalidDataException>(()=>InstallationBootstrapPackage.FilesForMode("PUBLIC_UNKNOWN_MODE"));
    }
    [Theory][InlineData("valid")][InlineData("framework")][InlineData("version")][InlineData("probe")][InlineData("config")][InlineData("runtime-duplicate")]
    [InlineData("missing-asset")][InlineData("extra-asset")][InlineData("asset-alias")][InlineData("asset-path")][InlineData("rid")][InlineData("asset-type")][InlineData("library")][InlineData("deps-duplicate")][InlineData("graph-missing")][InlineData("graph-extra")][InlineData("graph-order")][InlineData("native-version")]
    public void ExactConfigurationRefusesExternalProbingUnknownFieldsOrChangedAssets(string fault)
    {
        string root=Path.Combine(Path.GetTempPath(),"kuaiyi-bootstrap-config-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var runtime=JsonNode.Parse("""
            {"runtimeOptions":{"tfm":"net10.0","includedFrameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.13"}],"configProperties":{"System.Reflection.Metadata.MetadataUpdater.IsSupported":false,"System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization":false}}}
            """)!;var deps=Contract()["dependencies"]!.DeepClone();var options=runtime["runtimeOptions"]!;
            var target=deps["targets"]![".NETCoreApp,Version=v10.0/win-x64"]!;var pack=target["runtimepack.Microsoft.NETCore.App.Runtime.win-x64/10.0.13"]!;
            if(fault=="framework")options["framework"]=new JsonObject{["name"]="Microsoft.NETCore.App",["version"]="10.0.13"};
            if(fault=="version")options["includedFrameworks"]![0]!["version"]="10.0.12";
            if(fault=="probe")options["additionalProbingPaths"]=new JsonArray("PUBLIC_PROBE_PATH");
            if(fault=="config")options["configProperties"]!["PUBLIC_UNKNOWN"]=true;
            if(fault=="missing-asset")pack["native"]!.AsObject().Remove("hostfxr.dll");
            if(fault=="extra-asset")pack["native"]!["PUBLIC_EXTRA.dll"]=new JsonObject();
            if(fault is "asset-alias" or "asset-path")
            {var value=pack["native"]!["hostfxr.dll"]!.DeepClone();pack["native"]!.AsObject().Remove("hostfxr.dll");pack["native"]![fault=="asset-alias"?"HOSTFXR.dll":"../hostfxr.dll"]=value;}
            if(fault=="rid")deps["runtimeTarget"]!["name"]=".NETCoreApp,Version=v10.0/win-arm64";
            if(fault=="asset-type"){pack["runtimeTargets"]=new JsonObject{["hostfxr.dll"]=new JsonObject{["rid"]="win-x64",["assetType"]="runtime"}};}
            if(fault=="library")deps["libraries"]!["PUBLIC_EXTRA/1.0.0"]=new JsonObject();
            if(fault=="graph-missing")deps.AsObject().Remove("runtimes");
            if(fault=="graph-extra")deps["runtimes"]!["PUBLIC_EXTRA"]=new JsonArray("win");
            if(fault=="graph-order")deps["runtimes"]!["win-x64"]=new JsonArray("any","win","base");
            if(fault=="native-version")pack["native"]!["hostfxr.dll"]!["fileVersion"]="PUBLIC_UNKNOWN_VERSION";
            string runtimeText=runtime.ToJsonString(),depsText=deps.ToJsonString();
            if(fault=="runtime-duplicate")runtimeText=runtimeText.Insert(1,"\"runtimeOptions\":{},");
            if(fault=="deps-duplicate")depsText=depsText.Insert(1,"\"compilationOptions\":{},");
            File.WriteAllText(Path.Combine(root,"KuaiYi.Launcher.runtimeconfig.json"),runtimeText);File.WriteAllText(Path.Combine(root,"KuaiYi.Launcher.deps.json"),depsText);
            if(fault=="valid")InstallationBootstrapPackage.RequireConfiguration(root,InstallationBootstrapPackage.SelfContainedMode);
            else Assert.Throws<InvalidDataException>(()=>InstallationBootstrapPackage.RequireConfiguration(root,InstallationBootstrapPackage.SelfContainedMode));
            Assert.Throws<InvalidDataException>(()=>InstallationBootstrapPackage.RequireConfiguration(root,"PUBLIC_UNKNOWN_MODE"));
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]public void ActualLinuxSdk402PublishedDependenciesMatchTheOnlyAcceptedSelfContainedShape()
    {
        string root=Path.Combine(Path.GetTempPath(),"kuaiyi-actual-bootstrap-config-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root,"KuaiYi.Launcher.runtimeconfig.json"),InstallationBootstrapPackage.SelfContainedRuntime);
            File.Copy(Path.Combine(MaterializedPayloadSource.Repo,"tests/fixtures/payload-runtime/KuaiYi.Launcher.deps.json"),Path.Combine(root,"KuaiYi.Launcher.deps.json"));
            InstallationBootstrapPackage.RequireConfiguration(root,InstallationBootstrapPackage.SelfContainedMode);
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]public async Task ExistingDevelopmentReceiptCannotClaimSelfContainedQualification()
    {
        string root=Path.Combine(Path.GetTempPath(),"kuaiyi-bootstrap-mode-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            string app=Path.Combine(root,"app"),data=Path.Combine(root,"data");var source=new MaterializedPayloadSource(Path.Combine(root,"source"));var store=new InstallationStore(app,data,()=>true);await store.ApplyAsync(source.Root,source.Digest);
            var b=new BootstrapSource(Path.Combine(root,"bootstrap"));await new InstallationBootstrapDeployment(app,data).DeployAsync(b.Root,b.Digest);
            var owner=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.StoreOwner>(File.ReadAllBytes(InstallationBootstrapDeployment.OwnerPath(app)));
            var receipt=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.Receipt>(File.ReadAllBytes(InstallationBootstrapDeployment.ActivePath(app)));
            Assert.Equal(InstallationBootstrapPackage.Mode,receipt.Mode);var changed=receipt with{Mode=InstallationBootstrapPackage.SelfContainedMode};
            Assert.Throws<InstallationRecoveryException>(()=>InstallationBootstrapDeployment.RequireReceipt(app,data,owner,InstallationBootstrapDeployment.EncodeReceipt(changed),CancellationToken.None));
            using var entry=InstallationBootstrapEntry.Open(InstallationBootstrapDeployment.BundlePath(app,receipt.Bundle),data);Assert.Equal(app,entry.Root);
        }
        finally{Directory.Delete(root,true);}
    }
}
