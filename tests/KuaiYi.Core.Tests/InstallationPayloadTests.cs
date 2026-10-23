using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using KuaiYi.Core.Installation;
using KuaiYi.Core.Integration;
using KuaiYi.NativeHost;
using Xunit;
using Xunit.Abstractions;
namespace KuaiYi.Core.Tests;

public sealed class InstallationPayloadTests(ITestOutputHelper output)
{
    private sealed class Rig:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-materialization-"+Guid.NewGuid().ToString("N"));
        internal string App=>Path.Combine(Root,"application");internal string Data=>Path.Combine(Root,"user-data");
        internal Rig(){Directory.CreateDirectory(Data);File.WriteAllText(Path.Combine(Data,"PUBLIC_ENCRYPTED_SENTINEL"),"PUBLIC_OPAQUE_FIXTURE");}
        internal InstallationStore Store(Action<InstallationStage>? stage=null)=>new(App,Data,()=>true){StageForTest=stage};
        internal MaterializedPayloadSource Source(string version="1.0")=>new(Path.Combine(Root,"source-"+version),version);
        public void Dispose(){Assert.Equal("PUBLIC_OPAQUE_FIXTURE",File.ReadAllText(Path.Combine(Data,"PUBLIC_ENCRYPTED_SENTINEL")));Directory.Delete(Root,true);}
    }
    private sealed class Registry:IHostRegistrationStore
    {
        public bool CleanupConfirmed=>true;public void FinalizeCleanup()=>Assert.True(CleanupConfirmed);
        internal int Reads,Writes;internal readonly Dictionary<IntegrationBrowser,HostRegistrationSlot> Values=new(){[IntegrationBrowser.Chrome]=new(false,null),[IntegrationBrowser.Edge]=new(false,null)};
        public HostRegistrationSlot Read(IntegrationBrowser b){Reads++;return Values[b];}
        public void Write(IntegrationBrowser b,HostRegistrationSlot expected,string? next){Assert.Equal(expected,Values[b]);Writes++;Values[b]=new(true,next);}
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    private static byte[] Pointer(Rig r)=>File.ReadAllBytes(Path.Combine(r.App,"current.json"));
    private static Dictionary<string,string> Snapshot(string directory)=>Directory.GetFiles(directory,"*",SearchOption.AllDirectories).ToDictionary(p=>Path.GetRelativePath(directory,p),MaterializedPayloadSource.Hash);
    [Fact]public async Task RealApplyMaterializesRandomGenerationAndBothProductionConsumersAcceptItsDerivedIntegrity()
    {
        using var r=new Rig();var source=r.Source();var before=Snapshot(source.Root);byte[] original=File.ReadAllBytes(Path.Combine(source.Root,"package.json"));
        var installed=await r.Store().ApplyAsync(source.Root,source.Digest);Assert.Equal(installed,r.Store().ReadActive());Assert.NotEqual(source.Digest,installed.CatalogSha256);
        Assert.Equal(original,File.ReadAllBytes(Path.Combine(installed.DirectoryPath,InstallationPayload.SourceCatalogName)));Assert.Equal(before.OrderBy(p=>p.Key),Snapshot(source.Root).OrderBy(p=>p.Key));
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(installed.DirectoryPath,InstallationPayload.HostManifestName)));
        Assert.Equal(Path.Combine(installed.DirectoryPath,"KuaiYi.NativeHost.exe"),manifest.RootElement.GetProperty("path").GetString());Assert.DoesNotContain("staging-",manifest.RootElement.GetProperty("path").GetString());
        var catalog=InstallationPayload.RequireMaterialized(installed.DirectoryPath);Assert.Equal(3,catalog.Schema);Assert.Equal(source.Digest,catalog.SourceCatalogSha256);Assert.Equal(installed.CatalogSha256,catalog.CatalogSha256);
        Assert.Equal((IntegrationState.Configured,IntegrationReason.LocalConfigurationOnly),await IntegrationInstalledFiles.ReadAsync(installed.DirectoryPath,default));
        var host=InstalledHostPolicy.ReadConfiguration(installed.DirectoryPath,MaterializedPayloadSource.Origin,false,Path.Combine(installed.DirectoryPath,"KuaiYi.NativeHost.exe"));
        var app=InstalledHostPolicy.ReadConfiguration(installed.DirectoryPath,null,true,Path.Combine(installed.DirectoryPath,"KuaiYi.App.exe"));Assert.Equal(host.Files,app.Files);Assert.Equal(InstalledRoleFiles.Required.Count,host.Files.Count);
        var registry=new Registry();Assert.Equal(HostRegistrationOutcome.Registered,await new HostRegistrationTransaction(r.App,r.Data,registry).RunAsync(true));Assert.Equal(2,registry.Writes);
        Assert.All(registry.Values.Values,v=>Assert.Equal(Path.Combine(installed.DirectoryPath,InstallationPayload.HostManifestName),v.Value));Assert.Equal(HostRegistrationOutcome.Withdrawn,await new HostRegistrationTransaction(r.App,r.Data,registry).RunAsync(false));Assert.Equal(4,registry.Writes);
        string evidence=Path.Combine(MaterializedPayloadSource.Repo,"artifacts/2026-10-22-payload-materialization","public-sample-"+Path.GetFileName(installed.DirectoryPath));Directory.CreateDirectory(evidence);
        foreach(string name in new[]{"package.json",InstallationPayload.SourceCatalogName,InstallationPayload.ProvenanceName,InstallationPayload.HostManifestName,InstallationPayload.TrustName})File.Copy(Path.Combine(installed.DirectoryPath,name),Path.Combine(evidence,name));
        File.WriteAllBytes(Path.Combine(evidence,"sample-scope.json"),JsonSerializer.SerializeToUtf8Bytes(new{sourceDigest=source.Digest,installedDigest=installed.CatalogSha256,productSourceRevision=MaterializedPayloadSource.Revision,realApply=true,managedRoles="Actual normal build DLLs/apphosts and deps",otherDependencies="Structural fixtures only",programCheck="Shared production pure configuration validator; synthetic expected process path",windowsExecution="NotRun",privateDataCopied=false}));
        output.WriteLine($"PUBLIC_MATERIALIZED sourceSha={source.Digest} installedSha={installed.CatalogSha256} sourceFiles={sourceFileCount()} trustFiles={host.Files.Count} nativeRuntime=StructuralFixtureOnly realApply=true osIdentity=NotRun");
        int sourceFileCount()=>Directory.GetFiles(source.Root,"*",SearchOption.AllDirectories).Length;
    }
    [Theory][InlineData("ClipboardHost")][InlineData("CaptureHost")]
    public async Task ActualSelfContainedDesktopHostRuntimeConfigurationPassesMaterialization(string role)
    {
        using var r=new Rig();var source=r.Source();
        source.Put("KuaiYi."+role+".runtimeconfig.json",File.ReadAllBytes(Path.Combine(MaterializedPayloadSource.Repo,"tests/fixtures/payload-runtime","KuaiYi."+role+".runtimeconfig.json")));
        source.Catalog();var installed=await r.Store().ApplyAsync(source.Root,source.Digest);
        Assert.Equal(installed,r.Store().ReadActive());
        Assert.Equal(File.ReadAllBytes(Path.Combine(source.Root,"KuaiYi."+role+".runtimeconfig.json")),File.ReadAllBytes(Path.Combine(installed.DirectoryPath,"KuaiYi."+role+".runtimeconfig.json")));
    }
    [Theory]
    [InlineData("ClipboardHost","missing")][InlineData("CaptureHost","missing")]
    [InlineData("ClipboardHost","duplicate")][InlineData("CaptureHost","duplicate")]
    [InlineData("ClipboardHost","extra")][InlineData("CaptureHost","extra")]
    [InlineData("ClipboardHost","version")][InlineData("CaptureHost","version")]
    public async Task RehashedDesktopHostRuntimeConfigurationStillRequiresExactFrameworkClosure(string role,string fault)
    {
        using var r=new Rig();var source=r.Source();
        var actual=JsonNode.Parse(File.ReadAllBytes(Path.Combine(MaterializedPayloadSource.Repo,"tests/fixtures/payload-runtime","KuaiYi."+role+".runtimeconfig.json")))!;
        var frameworks=actual["runtimeOptions"]!["includedFrameworks"]!.AsArray();
        switch(fault)
        {
            case "missing":frameworks.RemoveAt(1);break;
            case "duplicate":frameworks.Add(frameworks[1]!.DeepClone());break;
            case "extra":frameworks.Add(new JsonObject{["name"]="PUBLIC_UNAUTHORIZED_FRAMEWORK",["version"]="10.0.13"});break;
            case "version":frameworks[1]!["version"]="10.0.12";break;
        }
        source.Put("KuaiYi."+role+".runtimeconfig.json",JsonSerializer.SerializeToUtf8Bytes(actual));source.Catalog();
        await Assert.ThrowsAsync<InvalidDataException>(()=>r.Store().ApplyAsync(source.Root,source.Digest));
        Assert.Null(r.Store().ReadActive());Assert.False(File.Exists(Path.Combine(r.App,"current.json")));
    }
    [Theory][InlineData("role")][InlineData("dependency")][InlineData("framework-dependent")][InlineData("revision")][InlineData("origin")][InlineData("duplicate-origin")][InlineData("reserved-case")][InlineData("native-machine")][InlineData("identity")][InlineData("extension-permission")][InlineData("native-truncated")][InlineData("deps-shape")][InlineData("identity-dtd")]
    public async Task InvalidSourceClosureCannotCreateOrChangeAnInstalledGeneration(string fault)
    {
        using var r=new Rig();var source=r.Source();
        switch(fault)
        {
            case "role":File.Delete(Path.Combine(source.Root,"KuaiYi.ImageWorker.exe"));break;
            case "dependency":
                string path=Path.Combine(source.Root,"KuaiYi.NativeHost.deps.json");var doc=JsonNode.Parse(File.ReadAllBytes(path))!;string target=doc["runtimeTarget"]!["name"]!.GetValue<string>();
                doc["targets"]![target]!["PUBLIC_DEP/1.0"]=new JsonObject{["runtime"]=new JsonObject{["PUBLIC_MISSING.dll"]=new JsonObject()}};File.WriteAllText(path,doc.ToJsonString());break;
            case "framework-dependent":source.Put("KuaiYi.App.runtimeconfig.json","{\"runtimeOptions\":{\"framework\":{\"name\":\"Microsoft.NETCore.App\",\"version\":\"10.0.13\"}}}"u8.ToArray());break;
            case "reserved-case":source.Put("NATIVE-HOST-TRUST.JSON","PUBLIC_SOURCE_CANNOT_SUPPLY_TRUST"u8.ToArray());break;
            case "native-truncated":source.Put("KuaiYi.CaptureNative.dll","PUBLIC_NOT_A_PE"u8.ToArray());break;
            case "deps-shape":source.Put("KuaiYi.NativeHost.deps.json","{}"u8.ToArray());break;
            case "identity-dtd":source.Put("identity/AppxManifest.xml","<!DOCTYPE Package [<!ENTITY external SYSTEM 'https://synthetic.invalid/never-read'>]><Package/>"u8.ToArray());break;
            case "native-machine":var native=MaterializedPayloadSource.NativeFixture();native[0x84]=0x4c;native[0x85]=1;source.Put("KuaiYi.CaptureNative.dll",native);break;
            case "identity":source.Put("identity/AppxManifest.xml","<Package xmlns=\"http://schemas.microsoft.com/appx/manifest/foundation/windows10\"><Identity ProcessorArchitecture=\"x64\"/><Applications><Application Executable=\"KuaiYi.App.exe\"/></Applications></Package>"u8.ToArray());break;
            case "extension-permission":var ext=JsonNode.Parse(File.ReadAllBytes(Path.Combine(source.Root,"browser-extension/manifest.json")))!;ext["host_permissions"]=new JsonArray("<all_urls>");source.Put("browser-extension/manifest.json",JsonSerializer.SerializeToUtf8Bytes(ext));break;
        }
        source.Catalog();if(fault=="revision")source.Rewrite(d=>d["payload"]!["productSourceRevision"]=new string('0',40));
        if(fault=="origin")source.Rewrite(d=>d["payload"]!["origins"]=new JsonArray("chrome-extension://*/"));
        if(fault=="duplicate-origin")source.Rewrite(d=>d["payload"]!["origins"]=new JsonArray(MaterializedPayloadSource.Origin,MaterializedPayloadSource.Origin));
        await Assert.ThrowsAsync<InvalidDataException>(()=>r.Store().ApplyAsync(source.Root,source.Digest));Assert.False(Directory.Exists(r.App));
    }
    [Theory][InlineData("worker-id")][InlineData("duplicate-id")][InlineData("name")][InlineData("publisher")]
    public async Task SourceIdentityNamesPublisherAndRoleIdsMustMatchExistingProductContract(string fault)
    {
        using var r=new Rig();var source=r.Source();string path=Path.Combine(source.Root,"identity/AppxManifest.xml");
        var doc=System.Xml.Linq.XDocument.Load(path);System.Xml.Linq.XNamespace ns="http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        if(fault=="name")doc.Root!.Element(ns+"Identity")!.SetAttributeValue("Name","PUBLIC_OTHER_PRODUCT");
        else if(fault=="publisher")doc.Root!.Element(ns+"Identity")!.SetAttributeValue("Publisher","CN=PUBLIC_OTHER_PUBLISHER");
        else doc.Root!.Element(ns+"Applications")!.Elements(ns+"Application").Last().SetAttributeValue("Id",fault=="duplicate-id"?"KuaiYi":"PUBLIC_OTHER_WORKER");
        doc.Save(path);source.Catalog();await Assert.ThrowsAsync<InvalidDataException>(()=>r.Store().ApplyAsync(source.Root,source.Digest));Assert.False(Directory.Exists(r.App));
    }
    [Fact]public async Task OnePinnedSourceCanMaterializeTwiceWithoutReusingEitherDestinationIntegrity()
    {
        using var r=new Rig();var source=r.Source();string pinned=source.Digest;
        var first=await r.Store().ApplyAsync(source.Root,pinned);var second=await r.Store().ApplyAsync(source.Root,pinned);
        Assert.NotEqual(first.DirectoryPath,second.DirectoryPath);Assert.NotEqual(first.CatalogSha256,second.CatalogSha256);Assert.Equal(pinned,source.Digest);
        foreach(var version in new[]{first,second})
        {Assert.Equal(pinned,InstallationPayload.RequireMaterialized(version.DirectoryPath).SourceCatalogSha256);using var manifest=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(version.DirectoryPath,InstallationPayload.HostManifestName)));Assert.Equal(Path.Combine(version.DirectoryPath,"KuaiYi.NativeHost.exe"),manifest.RootElement.GetProperty("path").GetString());}
        Assert.Equal(second,r.Store().ReadActive());
    }
    [Fact]public async Task AnotherWellFormedOriginCannotReplaceTheCallerPinnedSourceDefinition()
    {
        using var r=new Rig();var source=r.Source();string pinned=source.Digest;
        source.Rewrite(d=>d["payload"]!["origins"]=new JsonArray("chrome-extension://"+new string('b',32)+"/"));
        await Assert.ThrowsAsync<InvalidDataException>(()=>r.Store().ApplyAsync(source.Root,pinned));Assert.False(Directory.Exists(r.App));
    }
    [Fact]public async Task DerivedMetadataCountsAgainstTheOriginalFixedFileLimit()
    {
        using var r=new Rig();var source=r.Source();int count=Directory.GetFiles(source.Root,"*",SearchOption.AllDirectories).Length-1;
        for(int i=count;i<4093;i++)source.Put("padding-"+i,[]);source.Catalog();
        var error=await Assert.ThrowsAsync<InvalidDataException>(()=>r.Store().ApplyAsync(source.Root,source.Digest));Assert.Equal("Derived files exceed installation file limit.",error.Message);Assert.False(Directory.Exists(r.App));
    }
    [Theory][InlineData(InstallationStage.PayloadMaterialized)][InlineData(InstallationStage.InstalledCatalogVerified)][InlineData(InstallationStage.VersionPublished)][InlineData(InstallationStage.BeforePointerPublish)][InlineData(InstallationStage.PointerPublished)]
    internal async Task MaterializationOrPublicationFailureRetainsPreviousExactPointerAndFailedBytes(InstallationStage stage)
    {
        using var r=new Rig();var firstSource=r.Source();var first=await r.Store().ApplyAsync(firstSource.Root,firstSource.Digest);byte[] pointer=Pointer(r);var second=r.Source("2.0");var error=new IOException("PUBLIC_MATERIALIZATION_IO_FAILURE");
        Assert.Same(error,await Assert.ThrowsAsync<IOException>(()=>r.Store(at=>{if(at==stage)throw error;}).ApplyAsync(second.Root,second.Digest)));
        Assert.Equal(pointer,Pointer(r));Assert.Equal(first,r.Store().ReadActive());if(stage is InstallationStage.PayloadMaterialized or InstallationStage.InstalledCatalogVerified)Assert.Single(Directory.GetDirectories(r.App,"staging-*"));else Assert.Equal(2,Directory.GetDirectories(Path.Combine(r.App,"versions")).Length);
    }
    [Fact]public async Task HealthCallbackSeesDerivedCatalogAndFailureUsesTheOriginalExactRollback()
    {
        using var r=new Rig();var one=r.Source();var before=await r.Store().ApplyAsync(one.Root,one.Digest);byte[] pointer=Pointer(r);var two=r.Source("2.0");var store=r.Store();using var admission=store.EnterAdmission(mutation:true);var failure=new IOException("PUBLIC_SYNTHETIC_HEALTH_FAILURE");InstalledVersion? received=null;
        Assert.Same(failure,await Assert.ThrowsAsync<IOException>(()=>store.ApplyForUpgradeAsync(two.Root,two.Digest,admission,(version,_)=>{received=version;Assert.NotEqual(two.Digest,version.CatalogSha256);Assert.Equal(version.CatalogSha256,MaterializedPayloadSource.Hash(Path.Combine(version.DirectoryPath,"package.json")));throw failure;},default)));
        Assert.NotNull(received);Assert.Equal(pointer,Pointer(r));Assert.Equal(before,store.ReadActive());Assert.True(Directory.Exists(received.DirectoryPath));
    }
    [Fact]public async Task CancellationAfterDerivationRetainsStagingAndCannotPublishNewCurrent()
    {
        using var r=new Rig();var one=r.Source();var first=await r.Store().ApplyAsync(one.Root,one.Digest);var source=r.Source("2.0");byte[] pointer=Pointer(r);using var stop=new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>r.Store(s=>{if(s==InstallationStage.PayloadMaterialized)stop.Cancel();}).ApplyAsync(source.Root,source.Digest,stop.Token));
        Assert.Equal(pointer,Pointer(r));Assert.Equal(first,r.Store().ReadActive());Assert.Single(Directory.GetDirectories(r.App,"staging-*"));
    }
    [Fact]public async Task SourceChangeAfterPinnedVerificationIsNotRehashedIntoTrustedInstalledMetadata()
    {
        using var r=new Rig();var source=r.Source();string pinned=source.Digest;string file=Path.Combine(source.Root,"browser-extension/options.html");
        await Assert.ThrowsAsync<InvalidDataException>(()=>r.Store(s=>{if(s==InstallationStage.PackageVerified)File.AppendAllText(file,"PUBLIC_CHANGED_AFTER_VERIFY");}).ApplyAsync(source.Root,pinned));
        Assert.Null(r.Store().ReadActive());Assert.Contains("PUBLIC_CHANGED_AFTER_VERIFY",File.ReadAllText(file));
    }
    private static void RecatalogInstalled(string directory)
    {
        string path=Path.Combine(directory,"package.json");var doc=JsonNode.Parse(File.ReadAllBytes(path))!;
        foreach(var entry in doc["files"]!.AsArray())
        {string p=Path.Combine(directory,entry!["path"]!.GetValue<string>());entry["bytes"]=new FileInfo(p).Length;entry["sha256"]=MaterializedPayloadSource.Hash(p);}
        File.WriteAllText(path,doc.ToJsonString());
    }
    [Theory][InlineData("payload")][InlineData("manifest")][InlineData("trust")][InlineData("source")][InlineData("provenance")]
    public async Task RecomputedInstalledCatalogCannotAdoptChangedPayloadOrDerivedMetadata(string fault)
    {
        using var r=new Rig();var source=r.Source();var installed=await r.Store().ApplyAsync(source.Root,source.Digest);byte[] pointer=Pointer(r);
        string name=fault switch{"payload"=>"browser-extension/options.html","manifest"=>InstallationPayload.HostManifestName,"trust"=>InstallationPayload.TrustName,"source"=>InstallationPayload.SourceCatalogName,_=>InstallationPayload.ProvenanceName};
        File.AppendAllText(Path.Combine(installed.DirectoryPath,name),"PUBLIC_FOREIGN_CHANGE");RecatalogInstalled(installed.DirectoryPath);
        var failure=Record.Exception(()=>InstallationPayload.RequireMaterialized(installed.DirectoryPath));Assert.NotNull(failure);Assert.True(failure is InvalidDataException or InstallationRecoveryException or JsonException,failure.ToString());
        Assert.Equal(pointer,Pointer(r));Assert.Contains("PUBLIC_FOREIGN_CHANGE",File.ReadAllText(Path.Combine(installed.DirectoryPath,name)));
    }
    [Fact]public async Task InstalledCatalogCannotBeReusedAsSourceAndGenericCatalogCannotAcquirePayloadQualification()
    {
        using var r=new Rig();var source=r.Source();var installed=await r.Store().ApplyAsync(source.Root,source.Digest);string copy=Path.Combine(r.Root,"copied-source");Copy(installed.DirectoryPath,copy);
        var other=new InstallationStore(Path.Combine(r.Root,"other-app"),Path.Combine(r.Root,"other-data"),()=>true);
        await Assert.ThrowsAsync<InvalidDataException>(()=>other.ApplyAsync(copy,MaterializedPayloadSource.Hash(Path.Combine(copy,"package.json"))));
        var doc=JsonNode.Parse(File.ReadAllBytes(Path.Combine(copy,"package.json")))!.AsObject();doc["schemaVersion"]=1;doc.Remove("sourceCatalogSha256");File.WriteAllText(Path.Combine(copy,"package.json"),doc.ToJsonString());
        var generic=await other.ApplyAsync(copy,MaterializedPayloadSource.Hash(Path.Combine(copy,"package.json")));
        Assert.Throws<InstallationRecoveryException>(()=>InstallationPayload.RequireMaterialized(generic.DirectoryPath));
        Assert.Throws<InstallationRecoveryException>(()=>InstalledHostPolicy.ReadConfiguration(generic.DirectoryPath,MaterializedPayloadSource.Origin,false,Path.Combine(generic.DirectoryPath,"KuaiYi.NativeHost.exe")));
        var registry=new Registry();await Assert.ThrowsAsync<InstallationRecoveryException>(()=>new HostRegistrationTransaction(Path.Combine(r.Root,"other-app"),Path.Combine(r.Root,"other-data"),registry).RunAsync(true));Assert.Equal(0,registry.Reads);Assert.Equal(0,registry.Writes);
    }
    [Fact]public async Task CopyingAMaterializedGenerationToAnotherPathDoesNotRetargetItsDerivedManifest()
    {
        using var r=new Rig();var source=r.Source();var installed=await r.Store().ApplyAsync(source.Root,source.Digest);string moved=Path.Combine(r.App,"versions",Guid.NewGuid().ToString("N"));Copy(installed.DirectoryPath,moved);
        Assert.Throws<InstallationRecoveryException>(()=>InstallationPayload.RequireMaterialized(moved));Assert.Equal(installed,r.Store().ReadActive());
    }
    private static void Copy(string from,string to)
    {Directory.CreateDirectory(to);foreach(string path in Directory.GetFiles(from,"*",SearchOption.AllDirectories)){string destination=Path.Combine(to,Path.GetRelativePath(from,path));Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(path,destination);}}
}
