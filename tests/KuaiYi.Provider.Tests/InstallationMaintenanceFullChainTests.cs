using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using KuaiYi.Core.Installation;
using KuaiYi.Core.Tests;
using Xunit;
using Xunit.Abstractions;
namespace KuaiYi.Provider.Tests;
// Full filesystem/contract chain. The original source supplier's neutral native/runtime placeholders are NOT runnable Windows binaries.
public sealed class InstallationMaintenanceFullChainTests(ITestOutputHelper output)
{
    private const long MiB=1024L*1024,OutputLimit=352*MiB,Floor=1024*MiB;
    private static string Evidence=>Path.Combine(MaterializedPayloadSource.Repo,"artifacts/2026-11-01-maintenance-full-chain");
    private sealed record Budget(long GSourceUpper,long BSourceUpper,long SharedSourceInstalledAndRepairStage,long ThreeTransactionRecords,long EvidenceAndTooling,long LogicalPeakUpper,long FreeBytes,long Limit);
    private static string Product(string name)=>(string)typeof(MaterializedPayloadSource).GetMethod("Product",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[name])!;
    private static Budget Bound()
    {
        long g=MiB; // Catalog upper bound, not an estimate of its actual serialization.
        int references=0,assets=0;
        foreach(string name in InstallationPayload.ManagedFiles)
        {string path=Product(name);g+=new FileInfo(path).Length;using var stream=File.OpenRead(path);using var pe=new PEReader(stream);references+=pe.GetMetadataReader().AssemblyReferences.Count;}
        foreach(string role in InstallationPayload.Roles)
        {
            g+=new FileInfo(Product(role+".exe")).Length+new FileInfo(Product(role+".deps.json")).Length+4096; // Generated runtimeconfig is below4KiB.
            using var doc=JsonDocument.Parse(File.ReadAllBytes(Product(role+".deps.json")));var d=doc.RootElement;string target=d.GetProperty("runtimeTarget").GetProperty("name").GetString()!;
            foreach(var lib in d.GetProperty("targets").GetProperty(target).EnumerateObject())foreach(string section in new[]{"runtime","native","resources","runtimeTargets"})
                if(lib.Value.TryGetProperty(section,out var entries))assets+=entries.EnumerateObject().Count();
        }
        // Count every possible neutral fixture, even duplicates/already-provided names: conservative overcount.
        g+=(InstallationPayload.TrustedFiles.Count+references+assets)*512L;
        foreach(string name in InstallationPayload.ResourceFiles)
        {
            string? source=name.StartsWith("identity/",StringComparison.Ordinal)?Path.Combine(MaterializedPayloadSource.Repo,"packaging",name):name.StartsWith("browser-extension/",StringComparison.Ordinal)?Path.Combine(MaterializedPayloadSource.Repo,"extensions/chromium",name[18..]):name.StartsWith("licenses/SkiaSharp",StringComparison.Ordinal)?Path.Combine(MaterializedPayloadSource.Repo,name):null;
            g+=source==null?4096:new FileInfo(source).Length;
        }
        long b=MiB+InstallationBootstrapPackage.Files.Sum(name=>new FileInfo(Path.Combine(MaterializedPayloadSource.Repo,"src/KuaiYi.Launcher/bin/Release/net10.0/win-x64",name)).Length)
            +InstallationBootstrapPackage.RuntimeFiles.Sum(f=>f.Bytes)+4096+new FileInfo(Path.Combine(MaterializedPayloadSource.Repo,"tests/fixtures/payload-runtime/KuaiYi.Launcher.deps.json")).Length;
        // Three coexisting copies: immutable source, original installed/recovery, replacement stage/canonical.
        // Derived G metadata+catalog <=2MiB per installed copy; three operations' bounded records <=6MiB each.
        long shared=3*(g+b)+4*MiB,records=3*6*MiB,tooling=8*MiB,peak=checked(shared+records+tooling);
        return new(g,b,shared,records,tooling,peak,new DriveInfo(Path.GetPathRoot(Evidence)!).AvailableFreeSpace,OutputLimit);
    }
    private void RequireBudget(Budget b)
    {output.WriteLine(JsonSerializer.Serialize(b));Assert.True(b.LogicalPeakUpper<=b.Limit,"Complete source/installed/staging/record coexistence exceeds bound.");Assert.True(b.FreeBytes>=Floor+b.LogicalPeakUpper,"Existing disk floor cannot contain this exact bounded fixture.");}
    [Fact]public void FullChainResourceProofUsesCurrentInputLengthsBeforeAllocation()
    {var b=Bound();RequireBudget(b);Directory.CreateDirectory(Evidence);File.WriteAllBytes(Path.Combine(Evidence,"budget-"+Guid.NewGuid().ToString("N")+".json"),JsonSerializer.SerializeToUtf8Bytes(b));}

    // A persistent file-backed analogue of the three Windows values. Never accesses Windows registry.
    private sealed class Registry:IInstallationStartupRegistry
    {
        private sealed record Values(InstallationDeploymentRegistrationSlot Chrome,InstallationDeploymentRegistrationSlot Edge,InstallationStartupValue Startup);
        private readonly string path;private readonly object sync=new();
        internal Registry(string path){this.path=path;Write(new(new(false,null),new(false,null),new(false)));}
        private Values Read(){byte[] bytes=File.ReadAllBytes(path);if(bytes.Length>16384)throw new InvalidDataException();return JsonSerializer.Deserialize<Values>(bytes)!;}
        private void Write(Values value){byte[] b=JsonSerializer.SerializeToUtf8Bytes(value);string temp=path+"."+Guid.NewGuid().ToString("N");using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(b);stream.Flush(true);}File.Move(temp,path,true);}
        internal void Initialize(string manifest,string command){lock(sync)Write(new(new(true,manifest),new(true,manifest),new(true,"REG_SZ",command)));}
        internal byte[] Bytes=>File.ReadAllBytes(path);
        public bool CleanupConfirmed=>true;public bool ConflictObserved=>false;
        public Task<InstallationDeploymentRegistrationSnapshot> CaptureAsync(){lock(sync){var v=Read();return Task.FromResult(new InstallationDeploymentRegistrationSnapshot(v.Chrome,v.Edge,v.Startup));}}
        public Task<InstallationDeploymentRegistrationSnapshot> ReadForPreparationAsync()=>CaptureAsync();
        public Task<InstallationDeploymentRegistrationSnapshot> ReadPublishedAsync()=>CaptureAsync();
        public Task<InstallationStartupValue> ReadStartupAsync(){lock(sync)return Task.FromResult(Read().Startup);}
        public Task WriteStartupAsync(InstallationStartupValue expected,InstallationStartupValue next){lock(sync){var v=Read();if(v.Startup!=expected)throw new InstallationRecoveryException("Fixture startup conflict.");Write(v with{Startup=next});return Task.CompletedTask;}}
        public Task WriteDefaultAsync(bool chrome,InstallationDeploymentRegistrationSlot expected,string? value){lock(sync){var v=Read();if((chrome?v.Chrome:v.Edge)!=expected)throw new InstallationRecoveryException("Fixture host conflict.");Write(chrome?v with{Chrome=new(true,value)}:v with{Edge=new(true,value)});return Task.CompletedTask;}}
        public Task VerifyAsync(){_=Read();return Task.CompletedTask;}public Task CloseOriginalsAsync()=>Task.CompletedTask;
    }
    private static object[] Files(string root)=>Directory.GetFiles(root,"*",SearchOption.AllDirectories).Order(StringComparer.Ordinal).Select(path=>(object)new{path=Path.GetRelativePath(root,path),bytes=new FileInfo(path).Length,sha256=MaterializedPayloadSource.Hash(path)}).ToArray();
    [Fact]public async Task FixedSourcesRepairKnownFailureRollbackRetireAndReopenActualFiles()
    {
        var budget=Bound();RequireBudget(budget);Directory.CreateDirectory(Evidence);
        string id=Guid.NewGuid().ToString("N"),root=Path.Combine(Path.GetTempPath(),"ky-maint-full-"+id),app=Path.Combine(root,"app"),data=Path.Combine(root,"data"),source=Path.Combine(root,"source");
        var events=new List<object>();string evidence=Path.Combine(Evidence,"chain-"+id+".json");
        void Save(string phase,object value){events.Add(new{phase,value});File.WriteAllBytes(evidence,JsonSerializer.SerializeToUtf8Bytes(new{kind="SyntheticFilesystemGBChain_NotWindows_NotRunnable",root,budget,events},new JsonSerializerOptions{WriteIndented=true}));}
        InstallationRegistrationWithdrawal? activeScope=null;
        try
        {
            Directory.CreateDirectory(source);Directory.CreateDirectory(data);File.WriteAllText(Path.Combine(data,"PUBLIC_SETTINGS"),"PUBLIC_PRESERVED");
            string neighbor=Path.Combine(root,"neighbor");Directory.CreateDirectory(neighbor);File.WriteAllText(Path.Combine(neighbor,"PUBLIC_OTHER_PRODUCT"),"PUBLIC_NEIGHBOR_UNCHANGED");
            var g=new MaterializedPayloadSource(Path.Combine(source,"generation"));var b=new BootstrapSource(Path.Combine(source,"bootstrap"));
            string approved=Path.Combine(MaterializedPayloadSource.Repo,"artifacts/2026-10-23-product-payload/final/verification-b-source");
            foreach(var f in InstallationBootstrapPackage.RuntimeFiles)
            {
                string from=Path.Combine(approved,f.Path),to=Path.Combine(b.Root,f.Path);Assert.Equal(f.Bytes,new FileInfo(from).Length);Assert.Equal(f.Sha256,MaterializedPayloadSource.Hash(from),ignoreCase:true);Directory.CreateDirectory(Path.GetDirectoryName(to)!);File.Copy(from,to);
            }
            File.WriteAllText(Path.Combine(b.Root,"KuaiYi.Launcher.runtimeconfig.json"),InstallationBootstrapPackage.SelfContainedRuntime);File.Copy(Path.Combine(MaterializedPayloadSource.Repo,"tests/fixtures/payload-runtime/KuaiYi.Launcher.deps.json"),Path.Combine(b.Root,"KuaiYi.Launcher.deps.json"),true);
            var all=Directory.GetFiles(b.Root,"*",SearchOption.AllDirectories).Where(p=>Path.GetFileName(p)!="package.json").Order(StringComparer.Ordinal).Select(p=>new{path=Path.GetRelativePath(b.Root,p).Replace('\\','/'),bytes=new FileInfo(p).Length,sha256=MaterializedPayloadSource.Hash(p)}).ToArray();
            File.WriteAllBytes(Path.Combine(b.Root,"package.json"),JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",version=b.Version,files=all}));
            Assert.True(Directory.GetFiles(g.Root,"*",SearchOption.AllDirectories).Sum(p=>new FileInfo(p).Length)<=budget.GSourceUpper);Assert.True(Directory.GetFiles(b.Root,"*",SearchOption.AllDirectories).Sum(p=>new FileInfo(p).Length)<=budget.BSourceUpper);
            byte[] pins=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,kind="KuaiYiInstallerSourcePins",availability="Pinned",generationSha256=g.Digest,bootstrapSha256=b.Digest});
            Save("sources-verified",new{g=g.Digest,b=b.Digest,officialBootstrapRuntime=InstallationBootstrapPackage.RuntimeFiles.Count,sourceFiles=Files(source),normalProductionPins="UnchangedUnavailable"});
            var store=new InstallationStore(app,data,()=>true);using(var initial=store.EnterRoleAdmission()){}var selected=await store.ApplyAsync(g.Root,g.Digest);await new InstallationBootstrapDeployment(app,data).DeployAsync(b.Root,b.Digest);
            var owner=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.StoreOwner>(File.ReadAllBytes(InstallationBootstrapDeployment.OwnerPath(app)));var receipt=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.Receipt>(File.ReadAllBytes(InstallationBootstrapDeployment.ActivePath(app)));
            string bundle=InstallationBootstrapDeployment.BundlePath(app,receipt.Bundle),manifest=Path.Combine(selected.DirectoryPath,InstallationPayload.HostManifestName),command=InstallationStartupRegistration.Command(bundle);
            File.WriteAllBytes(Path.Combine(app,"host-registration.json"),JsonSerializer.SerializeToUtf8Bytes(new InstallationHostOwnership(1,"KuaiYi",Guid.NewGuid(),selected,manifest)));
            File.WriteAllBytes(InstallationStartupRegistration.OwnerPath(app),JsonSerializer.SerializeToUtf8Bytes(new InstallationStartupOwnership(1,"KuaiYi",owner.Owner,app,data,selected,receipt.Bundle,g.Digest,b.Digest,command)));
            var registry=new Registry(Path.Combine(root,"registry-surrogate.json"));registry.Initialize(manifest,command);byte[] registered=registry.Bytes;string[] records=[Path.Combine(app,"current.json"),InstallationBootstrapDeployment.ActivePath(app),Path.Combine(app,"host-registration.json"),InstallationStartupRegistration.OwnerPath(app)];var before=records.ToDictionary(p=>p,p=>File.ReadAllBytes(p));
            Save("installed",new{selected,bundle,registry=Convert.ToBase64String(registered),g=Files(selected.DirectoryPath),b=Files(bundle)});
            string damaged=Path.Combine(selected.DirectoryPath,"KuaiYi.App.dll"),missing=Path.Combine(selected.DirectoryPath,"browser-extension/options.html");File.WriteAllText(damaged,"PUBLIC_DAMAGED_PRODUCT");File.Delete(missing);Save("damage-in-own-sample",new{damaged=MaterializedPayloadSource.Hash(damaged),missing=!File.Exists(missing)});
            activeScope=InstallationRegistrationWithdrawal.ForMaintenanceExecution(pins,app,data,registry,InstallationMaintenanceIntent.Repair,source);var repaired=await activeScope.StartMaintenance().WaitAsync(TimeSpan.FromSeconds(90));
            Save("repair-result",new{repaired.Status,repaired.CleanupConfirmed,repaired.Progress,repaired.ExecutedSteps,errors=repaired.Errors.Select(e=>e.ToString()).ToArray()});Assert.Equal(InstallationMaintenanceStatus.PayloadRepaired,repaired.Status);Assert.True(repaired.CleanupConfirmed);Assert.Empty(repaired.Errors);
            using(var reader=store.EnterAdmission(existing:true)){Assert.Equal(selected,store.ReadActive());_=InstallationBootstrapDeployment.RequireReceipt(app,data,owner,File.ReadAllBytes(InstallationBootstrapDeployment.ActivePath(app)),default);}
            foreach(var record in before)Assert.Equal(record.Value,File.ReadAllBytes(record.Key));Assert.Equal(registered,registry.Bytes);Assert.Equal(MaterializedPayloadSource.Hash(Path.Combine(g.Root,"KuaiYi.App.dll")),MaterializedPayloadSource.Hash(damaged));Assert.True(File.Exists(missing));
            string oldG=Path.Combine(app,"maintenance-retired-g-"+repaired.Operation);Assert.Equal("PUBLIC_DAMAGED_PRODUCT",File.ReadAllText(Path.Combine(oldG,"KuaiYi.App.dll")));Assert.False(File.Exists(Path.Combine(oldG,"browser-extension/options.html")));Save("repair-reopened",new{g=Files(selected.DirectoryPath),b=Files(bundle),oldG=Files(oldG)});
            // Known after-effect failure, before directory mutation: actual reference rollback must restore the same readable installation.
            activeScope=InstallationRegistrationWithdrawal.ForMaintenanceExecution(pins,app,data,registry,InstallationMaintenanceIntent.Uninstall,source,faults:new(){AfterEffect=(step,reverse)=>{if(step==4&&!reverse)throw new IOException("PUBLIC_KNOWN_REGISTRATION_AFTER_EFFECT");}});
            var rolledBack=await activeScope.StartMaintenance().WaitAsync(TimeSpan.FromSeconds(90));Save("known-failure-rollback",new{rolledBack.Status,rolledBack.CleanupConfirmed,rolledBack.Progress,errors=rolledBack.Errors.Select(e=>e.GetType().Name).ToArray()});
            Assert.Equal(InstallationMaintenanceStatus.Refused,rolledBack.Status);Assert.True(rolledBack.CleanupConfirmed);Assert.Contains(rolledBack.Errors,e=>e.Message=="PUBLIC_KNOWN_REGISTRATION_AFTER_EFFECT");Assert.Equal(0,rolledBack.Progress!.ReferencePrefix);Assert.Equal(0,rolledBack.Progress.ReferenceAttempt);Assert.False(rolledBack.Progress.FileMutationAttempted);
            using(var reader=store.EnterAdmission(existing:true))Assert.Equal(selected,store.ReadActive());foreach(var record in before)Assert.Equal(record.Value,File.ReadAllBytes(record.Key));Assert.Equal(registered,registry.Bytes);Assert.False(File.Exists(store.PendingPath));Save("rollback-reopened",new{g=Files(selected.DirectoryPath),b=Files(bundle)});
            activeScope=InstallationRegistrationWithdrawal.ForMaintenanceExecution(pins,app,data,registry,InstallationMaintenanceIntent.Uninstall,source);var retired=await activeScope.StartMaintenance().WaitAsync(TimeSpan.FromSeconds(90));Save("retirement-result",new{retired.Status,retired.CleanupConfirmed,retired.Progress,retired.ExecutedSteps,errors=retired.Errors.Select(e=>e.ToString()).ToArray()});
            Assert.Equal(InstallationMaintenanceStatus.FilesRetired,retired.Status);Assert.True(retired.CleanupConfirmed);Assert.Empty(retired.Errors);Assert.All(records,p=>Assert.False(File.Exists(p)));Assert.False(Directory.Exists(selected.DirectoryPath));Assert.False(Directory.Exists(bundle));
            using(var reader=store.EnterAdmission(existing:true))Assert.Null(store.ReadActive());var empty=await registry.CaptureAsync();Assert.Null(empty.Chrome.Value);Assert.Null(empty.Edge.Value);Assert.False(empty.Startup!.Exists);
            string retiredG=Path.Combine(app,"maintenance-retired-g-"+retired.Operation),retiredB=Path.Combine(app,"maintenance-retired-b-"+retired.Operation);
            _=InstallationPackage.ReadInstalled(retiredG,default,selected.DirectoryPath,selected.CatalogSha256);_=InstallationBootstrapPackage.Read(retiredB,b.Digest,new(selected.Version,retiredG,selected.CatalogSha256),default);
            Assert.Equal("PUBLIC_PRESERVED",File.ReadAllText(Path.Combine(data,"PUBLIC_SETTINGS")));Assert.Equal("PUBLIC_NEIGHBOR_UNCHANGED",File.ReadAllText(Path.Combine(neighbor,"PUBLIC_OTHER_PRODUCT")));Assert.False(File.Exists(store.PendingPath));Assert.True(File.Exists(Path.Combine(app,"maintenance-finished-"+repaired.Operation+".json")));Assert.True(File.Exists(Path.Combine(app,"maintenance-finished-"+retired.Operation+".json")));
            Save("retired-reopened",new{g=Files(retiredG),b=Files(retiredB),sourceCatalogG=g.Digest,sourceCatalogB=b.Digest,userData=Files(data),neighbor=Files(neighbor),productRolesStarted=0,windowsRegistryOperations=0,fullyUninstalled=false});
            output.WriteLine("PUBLIC_FULL_CHAIN_EVIDENCE "+evidence); // Complete sample remains intact for independent review, including its damaged originals.
        }
        catch(Exception error){Save("failure-retained",new{error=error.ToString(),originalTaskCompleted=activeScope?.OriginalWork.IsCompleted,cleanupConfirmed=activeScope?.CleanupConfirmed});output.WriteLine("PUBLIC_RETAINED_FULL_CHAIN "+evidence);throw;}
    }
}
