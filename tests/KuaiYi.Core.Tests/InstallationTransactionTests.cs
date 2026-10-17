using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KuaiYi.Core.Installation;
using KuaiYi.Core.Settings;
using Xunit;
using Xunit.Abstractions;
namespace KuaiYi.Core.Tests;
public sealed class InstallationTransactionTests(ITestOutputHelper output)
{
    private sealed class Protector:ISecretProtector
    {
        private static readonly byte[] Key=Enumerable.Repeat((byte)23,32).ToArray();
        public byte[] Protect(ReadOnlySpan<byte> input){byte[] bytes=new byte[input.Length+28];RandomNumberGenerator.Fill(bytes.AsSpan(0,12));using var aes=new AesGcm(Key,16);aes.Encrypt(bytes.AsSpan(0,12),input,bytes.AsSpan(28),bytes.AsSpan(12,16));return bytes;}
        public byte[] Unprotect(ReadOnlySpan<byte> input){byte[] plain=new byte[input.Length-28];using var aes=new AesGcm(Key,16);aes.Decrypt(input[..12],input[28..],input.Slice(12,16),plain);return plain;}
    }
    private sealed class DirectoryPolicy:ISecretDirectoryPolicy{public void EnsurePrivate(string directory)=>Directory.CreateDirectory(directory);}
    private sealed class Rig:IDisposable
    {
        public readonly string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-install-tests-"+Guid.NewGuid().ToString("N"));
        public string App=>Path.Combine(Root,"application");public string Data=>Path.Combine(Root,"user-data");public SecretFileStore Secrets=null!;public string Reference="";public bool Stopped=true;
        public string Hash(string version)=>CatalogHash(Path.Combine(Root,"package-"+version));
        public static string CatalogHash(string package)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(package,"package.json"))));
        public InstallationStore Store(Action<InstallationStage>? hook=null)=>new(App,Data,()=>Stopped){StageForTest=hook};
        public static async Task<Rig> Create()
        {
            var r=new Rig();Directory.CreateDirectory(r.Root);r.Secrets=new(Path.Combine(r.Data,"secrets"),new Protector(),new DirectoryPolicy());r.Reference=await r.Secrets.SaveNewAsync("PUBLIC_SYNTHETIC_INSTALL_KEY"u8.ToArray());var settings=new SettingsStore(Path.Combine(r.Data,"config"));await settings.SaveAsync(await settings.LoadAsync(),new(){ActiveProfileId="public",Profiles=[new(){Id="public",BaseUrl="https://public.invalid",Model="PUBLIC_MODEL",SecretRef=r.Reference}]});return r;
        }
        public string Package(string version)
        {
            string root=Path.Combine(Root,"package-"+version);Directory.CreateDirectory(Path.Combine(root,"assets"));var files=new List<object>();
            foreach(string path in new[]{"KuaiYi.App.exe","KuaiYi.NativeHost.exe","KuaiYi.Core.dll","assets/icon.bin"}){byte[] bytes=Encoding.UTF8.GetBytes("PUBLIC_SYNTHETIC_PACKAGE_"+version+"_"+path);File.WriteAllBytes(Path.Combine(root,path),bytes);files.Add(new{path,bytes=bytes.Length,sha256=Convert.ToHexString(SHA256.HashData(bytes))});}
            File.WriteAllBytes(Path.Combine(root,"package.json"),JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",version,files}));return root;
        }
        public static Dictionary<string,string> Snapshot(string root)=>Directory.Exists(root)?Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).ToDictionary(p=>Path.GetRelativePath(root,p),p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))):[];
        public async Task DataPreserved(Dictionary<string,string> before)
        {Assert.Equal(before.OrderBy(x=>x.Key),Snapshot(Data).OrderBy(x=>x.Key));using var lease=await Secrets.OpenAsync(Reference);Assert.Equal("PUBLIC_SYNTHETIC_INSTALL_KEY",Encoding.UTF8.GetString(lease.Bytes.Span));Assert.DoesNotContain("PUBLIC_SYNTHETIC_INSTALL_KEY",string.Join("",Directory.GetFiles(Path.Combine(Data,"secrets")).Select(p=>Encoding.UTF8.GetString(File.ReadAllBytes(p)))));}
        public void AssertVersion(InstalledVersion value,string version)
        {Assert.Equal(version,value.Version);foreach(string path in new[]{"KuaiYi.App.exe","KuaiYi.NativeHost.exe","KuaiYi.Core.dll","assets/icon.bin"})Assert.Equal("PUBLIC_SYNTHETIC_PACKAGE_"+version+"_"+path,File.ReadAllText(Path.Combine(value.DirectoryPath,path)));}
        public void Dispose(){if(Directory.Exists(Root))Directory.Delete(Root,true);}
    }
    [Fact]public async Task RealPackageTreesSwitchTogetherAndEncryptedUserDataStaysInItsOriginalDirectory()
    {
        using var r=await Rig.Create();var data=Rig.Snapshot(r.Data);Assert.Null(r.Store().ReadActive());var first=await r.Store().ApplyAsync(r.Package("1.0"),r.Hash("1.0"));r.AssertVersion(first,"1.0");string unknown=Path.Combine(r.App,"user-note.txt");File.WriteAllText(unknown,"PUBLIC_UNKNOWN_DO_NOT_DELETE");
        var second=await r.Store().ApplyAsync(r.Package("2.0"),r.Hash("2.0"));r.AssertVersion(second,"2.0");r.AssertVersion(r.Store().ReadActive()!,"2.0");r.AssertVersion(first,"1.0");Assert.Equal("PUBLIC_UNKNOWN_DO_NOT_DELETE",File.ReadAllText(unknown));Assert.NotEqual(first.DirectoryPath,second.DirectoryPath);await r.DataPreserved(data);
    }
    [Theory][InlineData(InstallationStage.PackageVerified)][InlineData(InstallationStage.VersionPublished)][InlineData(InstallationStage.BeforePointerPublish)][InlineData(InstallationStage.PointerPublished)]
    internal async Task ActualIoFailureBeforeOrAfterPublicationKeepsTheCompletePreviousVersion(InstallationStage stage)
    {
        using var r=await Rig.Create();var first=await r.Store().ApplyAsync(r.Package("1.0"),r.Hash("1.0"));var data=Rig.Snapshot(r.Data);byte[] before=File.ReadAllBytes(Path.Combine(r.App,"current.json"));
        await Assert.ThrowsAsync<IOException>(()=>r.Store(at=>{if(at==stage)throw new IOException("PUBLIC_CONTROLLED_IO_FAILURE");}).ApplyAsync(r.Package("2.0"),r.Hash("2.0")));
        Assert.Equal(before,File.ReadAllBytes(Path.Combine(r.App,"current.json")));r.AssertVersion(r.Store().ReadActive()!,"1.0");r.AssertVersion(first,"1.0");await r.DataPreserved(data);
    }
    [Fact]public async Task InitialPublicationFailureRestoresTheUninstalledStateWithoutDeletingAnyVersion()
    {
        using var r=await Rig.Create();await Assert.ThrowsAsync<IOException>(()=>r.Store(at=>{if(at==InstallationStage.PointerPublished)throw new IOException("PUBLIC_NEW_INSTALL_FAILURE");}).ApplyAsync(r.Package("1.0"),r.Hash("1.0")));Assert.Null(r.Store().ReadActive());Assert.Single(Directory.GetDirectories(Path.Combine(r.App,"versions")));Assert.Single(Directory.GetFiles(r.App,"failed-current-*.json"));
    }
    [Fact]public async Task CorruptActiveVersionRecoversTheVerifiedPreviousTreeAndRetainsUnknownBytes()
    {
        using var r=await Rig.Create();var data=Rig.Snapshot(r.Data);var first=await r.Store().ApplyAsync(r.Package("1.0"),r.Hash("1.0"));var second=await r.Store().ApplyAsync(r.Package("2.0"),r.Hash("2.0"));string changed=Path.Combine(second.DirectoryPath,"KuaiYi.Core.dll");File.WriteAllText(changed,"PUBLIC_CORRUPTION_RETAIN");string unknown=Path.Combine(second.DirectoryPath,"unknown.txt");File.WriteAllText(unknown,"PUBLIC_UNKNOWN_RETAIN");Assert.Throws<InvalidDataException>(()=>r.Store().ReadActive());var recovered=await r.Store().RecoverAsync();Assert.Equal(first.DirectoryPath,recovered!.DirectoryPath);r.AssertVersion(recovered,"1.0");Assert.Equal("PUBLIC_CORRUPTION_RETAIN",File.ReadAllText(changed));Assert.Equal("PUBLIC_UNKNOWN_RETAIN",File.ReadAllText(unknown));await r.DataPreserved(data);
    }
    [Theory][InlineData("missing")][InlineData("digest")][InlineData("extra")][InlineData("traversal")][InlineData("absolute")][InlineData("case")][InlineData("reserved")][InlineData("duplicate-field")][InlineData("unknown-field")][InlineData("oversize")][InlineData("schema")]
    public async Task InvalidPackagesCannotChangeTheInstalledVersion(string fault)
    {
        using var r=await Rig.Create();await r.Store().ApplyAsync(r.Package("1.0"),r.Hash("1.0"));var data=Rig.Snapshot(r.Data);byte[] before=File.ReadAllBytes(Path.Combine(r.App,"current.json"));string package=r.Package("2.0"),manifest=Path.Combine(package,"package.json");var doc=JsonNode.Parse(File.ReadAllText(manifest))!.AsObject();var files=doc["files"]!.AsArray();
        switch(fault)
        {
            case "missing":File.Delete(Path.Combine(package,"KuaiYi.Core.dll"));break;
            case "digest":File.WriteAllText(Path.Combine(package,"KuaiYi.Core.dll"),"PUBLIC_CHANGED");break;
            case "extra":File.WriteAllText(Path.Combine(package,"unknown.dll"),"PUBLIC_UNKNOWN");break;
            case "traversal":files[0]!["path"]="../escape.exe";break;
            case "absolute":files[0]!["path"]="/escape.exe";break;
            case "case":files.Add(files[0]!.DeepClone());files[^1]!["path"]="KUAIYI.APP.EXE";break;
            case "reserved":files[0]!["path"]="CON.exe";break;
            case "unknown-field":doc["unknown"]=1;break;
            case "oversize":files[0]!["bytes"]=long.MaxValue;break;
            case "schema":doc["schemaVersion"]=2;break;
        }
        File.WriteAllText(manifest,doc.ToJsonString());if(fault=="duplicate-field")File.WriteAllText(manifest,File.ReadAllText(manifest).Replace("\"schemaVersion\":1","\"schemaVersion\":1,\"schemaVersion\":1",StringComparison.Ordinal));
        Assert.NotNull(await Record.ExceptionAsync(()=>r.Store().ApplyAsync(package,Rig.CatalogHash(package))));Assert.Equal(before,File.ReadAllBytes(Path.Combine(r.App,"current.json")));r.AssertVersion(r.Store().ReadActive()!,"1.0");await r.DataPreserved(data);
    }
    [Theory][InlineData("schema-string")][InlineData("schema-overflow")][InlineData("version-number")][InlineData("path-number")][InlineData("bytes-string")][InlineData("bytes-overflow")][InlineData("hash-number")]
    public async Task MalformedActiveCatalogStillRecoversTheVerifiedPreviousVersion(string fault)
    {
        using var r=await Rig.Create();var first=await r.Store().ApplyAsync(r.Package("1.0"),r.Hash("1.0"));var second=await r.Store().ApplyAsync(r.Package("2.0"),r.Hash("2.0"));string path=Path.Combine(second.DirectoryPath,"package.json");var doc=JsonNode.Parse(File.ReadAllText(path))!.AsObject();var file=doc["files"]![0]!;
        switch(fault){case "schema-string":doc["schemaVersion"]="wrong";break;case "schema-overflow":doc["schemaVersion"]=1e100;break;case "version-number":doc["version"]=42;break;case "path-number":file["path"]=42;break;case "bytes-string":file["bytes"]="wrong";break;case "bytes-overflow":file["bytes"]=1e100;break;case "hash-number":file["sha256"]=42;break;}
        string corrupt=doc.ToJsonString();File.WriteAllText(path,corrupt);var recovered=await r.Store().RecoverAsync();Assert.Equal(first.DirectoryPath,recovered!.DirectoryPath);r.AssertVersion(recovered,"1.0");Assert.Equal(corrupt,File.ReadAllText(path));
    }
    [Fact]public async Task CallerPinnedCatalogDigestCannotBeReplacedAlongWithPayloadHashes()
    {
        using var r=await Rig.Create();string package=r.Package("1.0"),pinned=Rig.CatalogHash(package),path=Path.Combine(package,"package.json");var doc=JsonNode.Parse(File.ReadAllText(path))!;doc["version"]="replaced";File.WriteAllText(path,doc.ToJsonString());await Assert.ThrowsAsync<InvalidDataException>(()=>r.Store().ApplyAsync(package,pinned));Assert.False(Directory.Exists(r.App));
    }
    [Fact]public async Task ASeparateCaseAliasedDirectoryIsNotSilentlyDroppedFromThePackage()
    {
        using var r=await Rig.Create();await r.Store().ApplyAsync(r.Package("1.0"),r.Hash("1.0"));string package=r.Package("2.0");Directory.CreateDirectory(Path.Combine(package,"ASSETS"));
        if(Directory.GetDirectories(package).Length==1){r.AssertVersion(await r.Store().ApplyAsync(package,Rig.CatalogHash(package)),"2.0");return;}
        await Assert.ThrowsAsync<InvalidDataException>(()=>r.Store().ApplyAsync(package,Rig.CatalogHash(package)));r.AssertVersion(r.Store().ReadActive()!,"1.0");Assert.True(Directory.Exists(Path.Combine(package,"ASSETS")));
    }
    [Theory][InlineData(false)][InlineData(true)]public async Task QuiescenceIsRequiredAndCancellationAfterCommitDoesNotInventRollback(bool afterCommit)
    {
        using var r=await Rig.Create();string package=r.Package("1.0");r.Stopped=false;await Assert.ThrowsAsync<InstallationBusyException>(()=>r.Store().ApplyAsync(package,Rig.CatalogHash(package)));Assert.False(Directory.Exists(r.App));r.Stopped=true;using var cancel=new CancellationTokenSource();var store=r.Store(at=>{if(at==(afterCommit?InstallationStage.PointerPublished:InstallationStage.BeforePointerPublish))cancel.Cancel();});
        if(afterCommit){var installed=await store.ApplyAsync(package,Rig.CatalogHash(package),cancel.Token);r.AssertVersion(installed,"1.0");r.AssertVersion(r.Store().ReadActive()!,"1.0");}
        else{await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>store.ApplyAsync(package,Rig.CatalogHash(package),cancel.Token));Assert.Null(r.Store().ReadActive());}
    }
    [Fact]public async Task UnknownRootLockPointerAndUserDataOverlapAreNotOverwritten()
    {
        using var r=await Rig.Create();string package=r.Package("1.0");Directory.CreateDirectory(r.App);string sentinel=Path.Combine(r.App,"unknown.txt");File.WriteAllText(sentinel,"PUBLIC_UNKNOWN_ROOT");await Assert.ThrowsAsync<InstallationRecoveryException>(()=>r.Store().ApplyAsync(package,Rig.CatalogHash(package)));Assert.Equal("PUBLIC_UNKNOWN_ROOT",File.ReadAllText(sentinel));Assert.False(File.Exists(Path.Combine(r.App,"owner.txt")));Assert.Throws<InvalidDataException>(()=>new InstallationStore(r.Data,r.Data,()=>true));
    }
    [Theory][InlineData("lock")][InlineData("pointer")][InlineData("previous")]
    public async Task UncertainRecoveryRetainsAllFilesAndNeverReportsSuccess(string fault)
    {
        using var r=await Rig.Create();var first=await r.Store().ApplyAsync(r.Package("1.0"),r.Hash("1.0"));var second=await r.Store().ApplyAsync(r.Package("2.0"),r.Hash("2.0"));
        if(fault=="lock"){File.WriteAllText(Path.Combine(r.App,"installation.lock"),"PUBLIC_UNKNOWN_LOCK");await Assert.ThrowsAsync<InstallationRecoveryException>(()=>r.Store().ApplyAsync(r.Package("3.0"),r.Hash("3.0")));Assert.Equal("PUBLIC_UNKNOWN_LOCK",File.ReadAllText(Path.Combine(r.App,"installation.lock")));}
        else if(fault=="pointer"){File.WriteAllText(Path.Combine(r.App,"current.json"),"PUBLIC_UNKNOWN_POINTER");Assert.NotNull(await Record.ExceptionAsync(()=>r.Store().RecoverAsync()));Assert.Equal("PUBLIC_UNKNOWN_POINTER",File.ReadAllText(Path.Combine(r.App,"current.json")));}
        else{File.WriteAllText(Path.Combine(first.DirectoryPath,"KuaiYi.Core.dll"),"PUBLIC_BAD_PREVIOUS");File.WriteAllText(Path.Combine(second.DirectoryPath,"KuaiYi.Core.dll"),"PUBLIC_BAD_CURRENT");var before=File.ReadAllBytes(Path.Combine(r.App,"current.json"));Assert.NotNull(await Record.ExceptionAsync(()=>r.Store().RecoverAsync()));Assert.Equal(before,File.ReadAllBytes(Path.Combine(r.App,"current.json")));}
        Assert.True(Directory.Exists(first.DirectoryPath));Assert.True(Directory.Exists(second.DirectoryPath));
    }
    [Fact]public async Task RollbackFailureIsUnconfirmedAndRetainsBothCompleteVersionTrees()
    {
        using var r=await Rig.Create();var first=await r.Store().ApplyAsync(r.Package("1.0"),r.Hash("1.0"));var failure=await Assert.ThrowsAsync<InstallationRollbackException>(()=>r.Store(at=>{if(at==InstallationStage.PointerPublished){r.Stopped=false;throw new IOException("PUBLIC_PUBLICATION_FAILURE");}}).ApplyAsync(r.Package("2.0"),r.Hash("2.0")));Assert.IsType<AggregateException>(failure.InnerException);r.AssertVersion(first,"1.0");r.AssertVersion(r.Store().ReadActive()!,"2.0");Assert.Equal(2,Directory.GetDirectories(Path.Combine(r.App,"versions")).Length);
    }
    [Fact]public async Task ReadersObserveOnlyCompleteOldOrNewGenerationsAcrossTheAtomicRecordSwitch()
    {
        using var r=await Rig.Create();await r.Store().ApplyAsync(r.Package("1.0"),r.Hash("1.0"));string package=r.Package("2.0");using var oldSeen=new ManualResetEventSlim();using var newSeen=new ManualResetEventSlim();using var stop=new CancellationTokenSource();var observations=new System.Collections.Concurrent.ConcurrentBag<string>();
        Task reader=Task.Run(async()=>{while(!stop.IsCancellationRequested){var version=r.Store().ReadActive()!;r.AssertVersion(version,version.Version);observations.Add(version.Version);if(version.Version=="1.0")oldSeen.Set();if(version.Version=="2.0")newSeen.Set();await Task.Delay(1);}});
        try{Assert.True(oldSeen.Wait(TimeSpan.FromSeconds(6)));await r.Store(at=>{if(at==InstallationStage.PointerPublished)Assert.True(newSeen.Wait(TimeSpan.FromSeconds(6)));}).ApplyAsync(package,Rig.CatalogHash(package));}
        finally{stop.Cancel();await reader.WaitAsync(TimeSpan.FromSeconds(6));}
        Assert.Contains("1.0",observations);Assert.Contains("2.0",observations);Assert.All(observations,v=>Assert.True(v is "1.0" or "2.0"));
    }
    [Fact]public async Task AnActualExclusiveFileLeaseBlocksTheCompetingWriter()
    {
        using var r=await Rig.Create();await r.Store().ApplyAsync(r.Package("1.0"),r.Hash("1.0"));using var lease=new FileStream(Path.Combine(r.App,"installation.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None);await Assert.ThrowsAsync<InstallationBusyException>(()=>r.Store().ApplyAsync(r.Package("2.0"),r.Hash("2.0")));r.AssertVersion(r.Store().ReadActive()!,"1.0");Assert.Equal(0,lease.Length);
    }
    [Fact]public async Task RevokedQuiescenceBeforeCommitCannotChangeTheCurrentVersion()
    {
        using var r=await Rig.Create();await r.Store().ApplyAsync(r.Package("1.0"),r.Hash("1.0"));await Assert.ThrowsAsync<InstallationBusyException>(()=>r.Store(at=>{if(at==InstallationStage.BeforePointerPublish)r.Stopped=false;}).ApplyAsync(r.Package("2.0"),r.Hash("2.0")));r.AssertVersion(r.Store().ReadActive()!,"1.0");
    }
    [Theory][InlineData(InstallationStage.VersionPublished)][InlineData(InstallationStage.BeforePointerPublish)][InlineData(InstallationStage.PointerPublished)]
    internal async Task ActualOwnedProcessExitLeavesExactlyTheOldOrNewCompleteVersion(InstallationStage stage)
    {
        using var r=await Rig.Create();await r.Store().ApplyAsync(r.Package("1.0"),r.Hash("1.0"));var data=Rig.Snapshot(r.Data);string package=r.Package("2.0"),assembly=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tools/KuaiYi.SettingsStoreFixture/bin",new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name,"net10.0/KuaiYi.SettingsStoreFixture.dll"));var start=new ProcessStartInfo(Environment.ProcessPath??"dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};start.FileName=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet";foreach(string arg in new[]{assembly,"--installation-checkpoint",r.Root,package,stage.ToString()})start.ArgumentList.Add(arg);
        using var child=Process.Start(start)!;Task<string> stdout=child.StandardOutput.ReadToEndAsync(),stderr=child.StandardError.ReadToEndAsync();
        try
        {
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));string recorded=await stdout,errors=await stderr;output.WriteLine($"PUBLIC_INSTALL_CHILD stage={stage} exit={child.ExitCode} driverSha256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly)))} stdout={recorded.Trim()} stderrBytes={Encoding.UTF8.GetByteCount(errors)}");
            Assert.Equal(73,child.ExitCode);Assert.Equal("PUBLIC_INSTALL_STAGE="+stage,recorded.Trim());Assert.Equal("",errors);string expected=stage==InstallationStage.PointerPublished?"2.0":"1.0";r.AssertVersion(r.Store().ReadActive()!,expected);r.AssertVersion((await r.Store().RecoverAsync())!,expected);await r.DataPreserved(data);
        }
        finally{if(!child.HasExited){child.Kill();await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));}}

    }
    [Theory][InlineData("package-file")][InlineData("package-directory")][InlineData("installation-ancestor")][InlineData("data-ancestor")]
    public async Task RealSymbolicLinksAreRejectedAndIdenticalDigestTargetsRetainEveryByte(string location)
    {
        using var r=await Rig.Create();await r.Store().ApplyAsync(r.Package("1.0"),r.Hash("1.0"));string package=r.Package("2.0"),target=Path.Combine(r.Root,"link-target"),link=Path.Combine(r.Root,"link");Directory.CreateDirectory(target);File.WriteAllText(Path.Combine(target,"sentinel.txt"),"PUBLIC_LINK_TARGET_RETAIN");
        if(location=="package-file")
        {
            string original=Path.Combine(package,"KuaiYi.Core.dll"),copy=Path.Combine(target,"KuaiYi.Core.dll");File.Move(original,copy);File.CreateSymbolicLink(original,copy);
            Assert.Equal(File.ReadAllBytes(copy),File.ReadAllBytes(original));
        }
        else if(location=="package-directory")
        {string original=Path.Combine(package,"assets"),copy=Path.Combine(target,"assets");Directory.Move(original,copy);Directory.CreateSymbolicLink(original,copy);}
        else Directory.CreateSymbolicLink(link,target);
        var preserved=Rig.Snapshot(target);var data=Rig.Snapshot(r.Data);byte[] pointer=File.ReadAllBytes(Path.Combine(r.App,"current.json"));
        if(location.StartsWith("package",StringComparison.Ordinal))await Assert.ThrowsAsync<IOException>(()=>r.Store().ApplyAsync(package,Rig.CatalogHash(package)));
        else Assert.Throws<IOException>(()=>new InstallationStore(location=="installation-ancestor"?Path.Combine(link,"nested","application"):r.App,location=="data-ancestor"?Path.Combine(link,"nested","user-data"):r.Data,()=>true));
        Assert.Equal(preserved.OrderBy(x=>x.Key),Rig.Snapshot(target).OrderBy(x=>x.Key));Assert.Equal(pointer,File.ReadAllBytes(Path.Combine(r.App,"current.json")));await r.DataPreserved(data);
        Assert.True((File.GetAttributes(location=="package-file"?Path.Combine(package,"KuaiYi.Core.dll"):location=="package-directory"?Path.Combine(package,"assets"):link)&FileAttributes.ReparsePoint)!=0);
    }
    [Fact]public async Task TwoConsecutiveRecoverCallsAreIdempotentForPointerVersionsAndEncryptedData()
    {
        using var r=await Rig.Create();var first=await r.Store().ApplyAsync(r.Package("1.0"),r.Hash("1.0"));var second=await r.Store().ApplyAsync(r.Package("2.0"),r.Hash("2.0"));File.WriteAllText(Path.Combine(second.DirectoryPath,"KuaiYi.Core.dll"),"PUBLIC_CORRUPTION_RETAIN");var data=Rig.Snapshot(r.Data);var versions=Rig.Snapshot(Path.Combine(r.App,"versions"));
        Assert.Equal(first,await r.Store().RecoverAsync());byte[] pointer=File.ReadAllBytes(Path.Combine(r.App,"current.json"));Assert.Equal(first,await r.Store().RecoverAsync());Assert.Equal(pointer,File.ReadAllBytes(Path.Combine(r.App,"current.json")));Assert.Equal(versions.OrderBy(x=>x.Key),Rig.Snapshot(Path.Combine(r.App,"versions")).OrderBy(x=>x.Key));await r.DataPreserved(data);
    }
}
