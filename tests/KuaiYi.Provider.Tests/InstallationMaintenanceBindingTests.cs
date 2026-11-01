using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class InstallationMaintenanceBindingTests
{
    // Descriptor-only catalog derivation. This is NOT a valid runtime source and cannot pass ReadSource/normal production pins.
    private static InstallationPackage Descriptor()
    {
        var files=InstallationPayload.TrustedFiles.Select(p=>new{path=p,bytes=1L,sha256=Convert.ToHexString(SHA256.HashData("P"u8))}).ToArray();
        return InstallationPackage.Parse(JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=2,product="KuaiYi",version="1.0",files,payload=new{kind=InstallationPayloadDefinition.Kind,runtimeVersion="10.0.13",productSourceRevision=new string('a',40),origins=new[]{"chrome-extension://"+new string('b',32)+"/"}}}));
    }
    private static (string Root,string Id,InstallationPackage Source,string Digest) Identity()
    {string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"ky-maintenance-binding-"+Guid.NewGuid().ToString("N"))),id=Guid.NewGuid().ToString("N");var s=Descriptor();byte[] bytes=InstallationPayload.InstalledCatalog(s,InstallationPayload.Derived(s,Path.Combine(root,"versions",id)));return(root,id,s,Convert.ToHexString(SHA256.HashData(bytes)));}
    private static byte[] Current(string id,string hash)=>JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,active=new{id,catalogSha256=hash},previous=(object?)null});
    [Fact]public void RepairIdentityIsDerivedFromFixedSourceNotFromDamagedTargetBytes()
    {var x=Identity();var v=InstallationStore.ReadPinnedRepairCurrent(x.Root,Current(x.Id,x.Digest),x.Source);Assert.Equal(x.Digest,v.CatalogSha256);Assert.Equal(Path.Combine(x.Root,"versions",x.Id),v.DirectoryPath);Assert.False(Directory.Exists(x.Root));}
    [Fact]public void WrongCurrentDigestCannotAuthorizeRepair()
    {var x=Identity();Assert.Throws<InstallationRecoveryException>(()=>InstallationStore.ReadPinnedRepairCurrent(x.Root,Current(x.Id,new string('0',64)),x.Source));Assert.False(Directory.Exists(x.Root));}
    [Fact]public void AnotherGenerationCannotReuseTheFirstCatalogDigest()
    {var x=Identity();Assert.Throws<InstallationRecoveryException>(()=>InstallationStore.ReadPinnedRepairCurrent(x.Root,Current(Guid.NewGuid().ToString("N"),x.Digest),x.Source));}
    private sealed class UnusedRegistry:IInstallationStartupRegistry
    {
        public bool CleanupConfirmed=>true;public bool ConflictObserved=>false;
        private static Task<T> Unexpected<T>()=>Task.FromException<T>(new InvalidOperationException("UNEXPECTED_PLATFORM_ACCESS"));
        public Task<InstallationDeploymentRegistrationSnapshot> CaptureAsync()=>Unexpected<InstallationDeploymentRegistrationSnapshot>();
        public Task<InstallationDeploymentRegistrationSnapshot> ReadForPreparationAsync()=>Unexpected<InstallationDeploymentRegistrationSnapshot>();
        public Task<InstallationDeploymentRegistrationSnapshot> ReadPublishedAsync()=>Unexpected<InstallationDeploymentRegistrationSnapshot>();
        public Task<InstallationStartupValue> ReadStartupAsync()=>Unexpected<InstallationStartupValue>();
        public Task WriteStartupAsync(InstallationStartupValue expected,InstallationStartupValue next)=>Unexpected<bool>();
        public Task WriteDefaultAsync(bool chrome,InstallationDeploymentRegistrationSlot expected,string? value)=>Unexpected<bool>();
        public Task VerifyAsync()=>Unexpected<bool>();public Task CloseOriginalsAsync()=>Unexpected<bool>();
    }
    private static readonly byte[] Unavailable=Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"kind\":\"KuaiYiInstallerSourcePins\",\"availability\":\"Unavailable\",\"generationSha256\":null,\"bootstrapSha256\":null}");
    [Fact]public async Task ReadonlyObservationCannotBecomeAnExecutableMaintenanceOwner()
    {
        string root=Path.Combine(Path.GetTempPath(),"ky-mode-boundary-"+Guid.NewGuid().ToString("N"));var scope=InstallationRegistrationWithdrawal.ForMaintenancePreflight(Unavailable,Path.Combine(root,"app"),Path.Combine(root,"data"),new UnusedRegistry(),InstallationMaintenanceIntent.Uninstall);
        Assert.Throws<InstallationRecoveryException>(()=>{_=scope.StartMaintenance();});var r=await scope.StartPreflight();Assert.Equal(InstallationMaintenanceStatus.Refused,r.Status);Assert.True(r.CleanupConfirmed);Assert.False(Directory.Exists(root));
    }
    [Fact]public async Task ExecutableOwnerCannotStartThroughWithdrawalOrReadonlyEntryAndUnavailablePinsHaveNoEffects()
    {
        string root=Path.Combine(Path.GetTempPath(),"ky-mode-boundary-"+Guid.NewGuid().ToString("N"));var scope=InstallationRegistrationWithdrawal.ForMaintenanceExecution(Unavailable,Path.Combine(root,"app"),Path.Combine(root,"data"),new UnusedRegistry(),InstallationMaintenanceIntent.Repair,Path.Combine(root,"source"));
        Assert.Throws<InvalidOperationException>(()=>{_=scope.Start();});Assert.Throws<InvalidOperationException>(()=>{_=scope.StartPreflight();});var task=scope.StartMaintenance();Assert.Same(task,scope.StartMaintenance());var r=await task;
        Assert.Equal(InstallationMaintenanceStatus.Refused,r.Status);Assert.True(r.CleanupConfirmed);Assert.False(r.Progress!.TicketAttempted);Assert.Empty(r.ExecutedSteps!);Assert.False(Directory.Exists(root));
    }
    [Theory][InlineData(3,true,false,3)][InlineData(4,true,false,4)][InlineData(5,true,false,4)][InlineData(6,true,false,4)][InlineData(2,true,false,7)][InlineData(1,true,false,6)][InlineData(5,false,false,3)][InlineData(5,true,true,3)]
    public void UnknownCannotBecomeOrdinaryRefusalAndPartialEffectsCannotBecomeWholeSuccess(int status,bool cleanup,bool cancellationFailed,int expected)
    {var r=new InstallationMaintenanceOutcome("public",InstallationMaintenanceIntent.Repair,(InstallationMaintenanceStatus)status,null,cleanup,[],[]);Assert.Equal(expected,InstallationMaintenanceReceiptText.ExitCode(r,cancellationFailed));Assert.NotEqual(0,InstallationMaintenanceReceiptText.ExitCode(r,cancellationFailed));}

}
