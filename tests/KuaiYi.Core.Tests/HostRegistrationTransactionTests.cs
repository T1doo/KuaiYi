using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KuaiYi.Core.Installation;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class HostRegistrationTransactionTests
{
    // These are synthetic registry slots, not an emulation claiming atomic Windows CAS.
    private sealed class Registry:IHostRegistrationStore
    {
        internal readonly Dictionary<IntegrationBrowser,HostRegistrationSlot> Slots=new(){[IntegrationBrowser.Chrome]=new(false,null),[IntegrationBrowser.Edge]=new(false,null)};
        internal Action<IntegrationBrowser,string?>? BeforeWrite,AfterWrite;
        internal Action? BeforeRead,BeforeFinalize;internal bool OriginalClosePending;public bool CleanupConfirmed=>!OriginalClosePending;
        public void FinalizeCleanup(){BeforeFinalize?.Invoke();OriginalClosePending=false;}
        internal int Writes,Reads;
        public HostRegistrationSlot Read(IntegrationBrowser browser){Reads++;BeforeRead?.Invoke();return Slots[browser];}
        public void Write(IntegrationBrowser browser,HostRegistrationSlot expected,string? value)
        {
            BeforeWrite?.Invoke(browser,value);if(Slots[browser]!=expected)throw new HostRegistrationConflictException();
            Writes++;Slots[browser]=new(expected.KeyExists||value!=null,value);AfterWrite?.Invoke(browser,value);
        }
        internal HostRegistrationSlot View(IntegrationBrowser browser,int bits)=>bits is 32 or 64?Slots[browser]:throw new ArgumentOutOfRangeException(nameof(bits));
    }
    private sealed class Fixture:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"KuaiYi.HostRegistration."+Guid.NewGuid().ToString("N"));
        internal string App=>Path.Combine(Root,"application");internal string Data=>Path.Combine(Root,"user-data");
        internal string Generation=>Path.Combine(App,"versions",new string('a',32));
        internal string Manifest=>Path.Combine(Generation,"com.kuaiyi.native_host.json");
        internal string Receipt=>Path.Combine(App,"host-registration.json");internal string Pending=>Path.Combine(App,"host-registration-pending.json");
        internal readonly Registry Registry=new();
        internal HostRegistrationTransaction Transaction()=>new(App,Data,Registry);
        internal InstallationStore Store()=>new(App,Data,()=>false);
        internal Fixture()
        {
            Directory.CreateDirectory(Generation);Directory.CreateDirectory(Data);File.WriteAllText(Path.Combine(Data,"synthetic-data.bin"),"PUBLIC_ENCRYPTED_DATA_PLACEHOLDER");
            // Construct a complete, synthetic installed tree with a known final path. This does
            // not claim InstallationStore.Apply already generates path-derived host metadata.
            var hashes=new Dictionary<string,string>();foreach(string name in InstalledRoleFiles.Required)
            {byte[] bytes=Encoding.UTF8.GetBytes("PUBLIC_SYNTHETIC_ROLE "+name);File.WriteAllBytes(Path.Combine(Generation,name),bytes);hashes[name]=Hash(bytes);}
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new{name=InstalledHostPolicy.HostName,description="PUBLIC_SYNTHETIC_HOST",path=Path.Combine(Generation,"KuaiYi.NativeHost.exe"),type="stdio",allowed_origins=new[]{"chrome-extension://"+new string('a',32)+"/"}}));
            File.WriteAllText(Path.Combine(Generation,"native-host-trust.json"),JsonSerializer.Serialize(new{schemaVersion=1,files=hashes}));
            var files=Directory.GetFiles(Generation).Order(StringComparer.Ordinal).Select(path=>new{path=Path.GetFileName(path),bytes=new FileInfo(path).Length,sha256=Hash(File.ReadAllBytes(path))}).ToArray();
            byte[] catalog=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",version="1.0",files});File.WriteAllBytes(Path.Combine(Generation,"package.json"),catalog);
            File.WriteAllText(Path.Combine(App,"owner.txt"),"KuaiYi installation file store v1\n");
            File.WriteAllBytes(InstallationHostActivity.PathFor(App),[]); // This fixture constructs a fresh participating store, not legacy migration.
            File.WriteAllText(Path.Combine(App,"current.json"),JsonSerializer.Serialize(new{schemaVersion=1,active=new{id=new string('a',32),catalogSha256=Hash(catalog)},previous=(object?)null}));
            Assert.Equal(Generation,Store().ReadActive()!.DirectoryPath);
        }
        internal void AdmissionAvailable(){using var held=Store().EnterRoleAdmission();}
        internal void AdmissionBlocked()=>Assert.ThrowsAny<IOException>(()=>Store().EnterRoleAdmission());
        internal static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
        internal void EmptyDefaults()=>Assert.All(Registry.Slots.Values,slot=>Assert.Null(slot.Value));
        internal Guid Operation()=>JsonNode.Parse(File.ReadAllText(Pending))!["Operation"]!.GetValue<Guid>();
        public void Dispose(){Assert.Equal("PUBLIC_ENCRYPTED_DATA_PLACEHOLDER",File.ReadAllText(Path.Combine(Data,"synthetic-data.bin")));Directory.Delete(Root,true);}
    }
    [Fact]public async Task RegistersTwoPhysicalSlotsAndWithdrawsOnlyDurablyOwnedDefaults()
    {
        using var f=new Fixture();f.AdmissionAvailable();var tx=f.Transaction();Assert.Equal(HostRegistrationOutcome.Registered,await tx.RunAsync(true));
        Assert.Equal(2,f.Registry.Writes);Assert.True(File.Exists(f.Receipt));Assert.False(File.Exists(f.Pending));
        foreach(var browser in Enum.GetValues<IntegrationBrowser>()){Assert.Equal(f.Manifest,f.Registry.View(browser,32).Value);Assert.Equal(f.Registry.View(browser,32),f.Registry.View(browser,64));}
        Assert.Equal(HostRegistrationOutcome.AlreadyRegistered,await f.Transaction().RunAsync(true));Assert.Equal(2,f.Registry.Writes);f.AdmissionAvailable();
        Assert.Equal(HostRegistrationOutcome.Withdrawn,await f.Transaction().RunAsync(false));Assert.Equal(4,f.Registry.Writes);f.EmptyDefaults();Assert.All(f.Registry.Slots.Values,slot=>Assert.True(slot.KeyExists));Assert.False(File.Exists(f.Receipt));Assert.False(File.Exists(f.Pending));Assert.Single(Directory.GetFiles(f.App,"host-registration-withdrawn-*.json"));f.AdmissionAvailable();
        Assert.Equal(HostRegistrationOutcome.AlreadyWithdrawn,await f.Transaction().RunAsync(false));Assert.Equal(4,f.Registry.Writes);
    }
    [Theory][InlineData("same-path")][InlineData("foreign")][InlineData("empty-string")][InlineData("other-content")]
    public async Task UnownedOrUnknownDefaultsAreNeverAdoptedOrDeleted(string fault)
    {
        using var f=new Fixture();f.Registry.Slots[IntegrationBrowser.Edge]=fault switch{"same-path"=>new(true,f.Manifest),"foreign"=>new(true,"PUBLIC_FOREIGN"),"empty-string"=>new(true,""),_=>new(true,null,true)};
        var before=f.Registry.Slots.ToArray();await Assert.ThrowsAsync<HostRegistrationConflictException>(()=>f.Transaction().RunAsync(true));await Assert.ThrowsAsync<HostRegistrationConflictException>(()=>f.Transaction().RunAsync(false));Assert.Equal(before,f.Registry.Slots.ToArray());Assert.Equal(0,f.Registry.Writes);Assert.False(File.Exists(f.Pending));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task UnknownPendingFileOrDirectoryBlocksOriginalRoleAdmission(bool directory)
    {
        using var f=new Fixture();if(directory)Directory.CreateDirectory(f.Pending);else File.WriteAllText(f.Pending,"PUBLIC_UNKNOWN_PENDING");f.AdmissionBlocked();await Assert.ThrowsAsync<InstallationRecoveryException>(()=>f.Transaction().RunAsync(true));Assert.Equal(0,f.Registry.Reads);Assert.Equal(0,f.Registry.Writes);
    }
    [Theory][InlineData("owner")][InlineData("upgrade")][InlineData("handoff")][InlineData("lock")]
    public async Task ExistingLifecycleFencesStillRejectRegistrationBeforeAnyRegistryAccess(string fault)
    {
        using var f=new Fixture();File.WriteAllText(Path.Combine(f.App,fault switch{"owner"=>"execution-owner.json","upgrade"=>"upgrade-pending.json","handoff"=>"handoff-pending.json",_=>"upgrade.lock"}),"PUBLIC_UNKNOWN_FENCE");await Assert.ThrowsAnyAsync<IOException>(()=>f.Transaction().RunAsync(true));Assert.Equal(0,f.Registry.Reads);Assert.Equal(0,f.Registry.Writes);
    }
    [Theory][InlineData(false,false)][InlineData(false,true)][InlineData(true,false)][InlineData(true,true)]
    public async Task PartialFailureBeforeCommitRestoresDefaultValuesAndPreservesOriginalError(bool withdraw,bool afterWrite)
    {
        using var f=new Fixture();if(withdraw)await f.Transaction().RunAsync(true);var original=new IOException("PUBLIC_ORIGINAL_WRITE_FAILURE");bool fired=false;
        void Fail(IntegrationBrowser browser,string? value){if(browser==IntegrationBrowser.Edge&&!fired){fired=true;throw original;}}
        if(afterWrite)f.Registry.AfterWrite=Fail;else f.Registry.BeforeWrite=Fail;
        var actual=await Assert.ThrowsAsync<IOException>(()=>f.Transaction().RunAsync(!withdraw));Assert.Same(original,actual);Assert.False(File.Exists(f.Pending));
        Assert.Equal(withdraw,File.Exists(f.Receipt));Assert.All(f.Registry.Slots.Values,s=>Assert.Equal(withdraw?f.Manifest:null,s.Value));f.AdmissionAvailable();
    }
    [Fact]public async Task RollbackConflictPreservesObservedForeignValueAndPendingFence()
    {
        using var f=new Fixture();var original=new IOException("PUBLIC_FAILED_SECOND_SLOT");f.Registry.BeforeWrite=(browser,value)=>{if(browser==IntegrationBrowser.Edge){f.Registry.Slots[IntegrationBrowser.Chrome]=new(true,"PUBLIC_FOREIGN_REPLACEMENT");throw original;}};
        var error=await Assert.ThrowsAsync<HostRegistrationUnconfirmedException>(()=>f.Transaction().RunAsync(true));Assert.Contains(original,Assert.IsType<AggregateException>(error.InnerException).InnerExceptions);Assert.Equal("PUBLIC_FOREIGN_REPLACEMENT",f.Registry.Slots[IntegrationBrowser.Chrome].Value);Assert.True(File.Exists(f.Pending));f.AdmissionBlocked();
    }
    [Fact]public async Task OperationAndRollbackErrorsAreBothPreserved()
    {
        using var f=new Fixture();var original=new IOException("PUBLIC_OPERATION");var cleanup=new IOException("PUBLIC_ROLLBACK");
        f.Registry.BeforeWrite=(browser,value)=>{if(browser==IntegrationBrowser.Edge)throw original;if(browser==IntegrationBrowser.Chrome&&value==null)throw cleanup;};
        var error=await Assert.ThrowsAsync<HostRegistrationUnconfirmedException>(()=>f.Transaction().RunAsync(true));var all=Assert.IsType<AggregateException>(error.InnerException).InnerExceptions;Assert.Contains(original,all);Assert.Contains(cleanup,all);Assert.True(File.Exists(f.Pending));f.AdmissionBlocked();
    }
    [Fact]public async Task CancellationWaitsForOriginalWriteThenRollsBackBeforeReleasingAdmission()
    {
        using var f=new Fixture();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();using var cancel=new CancellationTokenSource();bool held=false;
        f.Registry.AfterWrite=(browser,value)=>{if(!held&&browser==IntegrationBrowser.Chrome){held=true;entered.Set();if(!release.Wait(TimeSpan.FromSeconds(6)))throw new TimeoutException("PUBLIC_TEST_GUARD");}};
        var tx=f.Transaction();var original=tx.RunAsync(true,cancel.Token);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(6)));cancel.Cancel();Assert.False(original.IsCompleted);f.AdmissionBlocked();await Assert.ThrowsAsync<InstallationBusyException>(()=>tx.RunAsync(false));
        }
        finally{release.Set();}
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>original);f.EmptyDefaults();Assert.False(File.Exists(f.Pending));Assert.False(File.Exists(f.Receipt));f.AdmissionAvailable();
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task FailureAfterDurableCommitNeverCompensatesAgainstItsReceipt(bool withdraw)
    {
        using var f=new Fixture();if(withdraw)await f.Transaction().RunAsync(true);f.Registry.AfterWrite=(browser,value)=>{if(browser==IntegrationBrowser.Edge)File.WriteAllText(Path.Combine(f.App,"host-registration-finished-"+f.Operation().ToString("N")+".json"),"PUBLIC_UNKNOWN_ARCHIVE");};
        await Assert.ThrowsAsync<HostRegistrationUnconfirmedException>(()=>f.Transaction().RunAsync(!withdraw));Assert.Equal(!withdraw,File.Exists(f.Receipt));Assert.All(f.Registry.Slots.Values,s=>Assert.Equal(withdraw?null:f.Manifest,s.Value));Assert.True(File.Exists(f.Pending));f.AdmissionBlocked();
    }
    [Theory][InlineData("schema")][InlineData("owner")][InlineData("generation")][InlineData("extra")][InlineData("too-large")][InlineData("malformed")]
    public async Task UnknownOwnershipCannotWithdrawMatchingPath(string fault)
    {
        using var f=new Fixture();await f.Transaction().RunAsync(true);var doc=JsonNode.Parse(File.ReadAllText(f.Receipt))!;
        if(fault=="schema")doc["SchemaVersion"]=2;if(fault=="owner")doc["Owner"]=Guid.Empty;if(fault=="generation")doc["Version"]!["CatalogSha256"]=new string('0',64);if(fault=="extra")doc["Unknown"]=true;
        File.WriteAllText(f.Receipt,fault=="too-large"?new string('x',9000):fault=="malformed"?"{":doc.ToJsonString());int writes=f.Registry.Writes;
        if(fault=="malformed")await Assert.ThrowsAsync<JsonException>(()=>f.Transaction().RunAsync(false));
        else if(fault=="too-large")await Assert.ThrowsAsync<InvalidDataException>(()=>f.Transaction().RunAsync(false));
        else await Assert.ThrowsAsync<InstallationRecoveryException>(()=>f.Transaction().RunAsync(false));
        Assert.Equal(writes,f.Registry.Writes);Assert.All(f.Registry.Slots.Values,s=>Assert.Equal(f.Manifest,s.Value));Assert.False(File.Exists(f.Pending));
    }
    [Fact]public async Task ChangedOriginalJournalCannotBeClearedOrUsedToRollback()
    {
        using var f=new Fixture();f.Registry.AfterWrite=(browser,value)=>{if(browser==IntegrationBrowser.Chrome)File.WriteAllText(f.Pending,"PUBLIC_CHANGED_JOURNAL");};
        await Assert.ThrowsAsync<HostRegistrationUnconfirmedException>(()=>f.Transaction().RunAsync(true));Assert.Equal("PUBLIC_CHANGED_JOURNAL",File.ReadAllText(f.Pending));Assert.Equal(f.Manifest,f.Registry.Slots[IntegrationBrowser.Chrome].Value);Assert.Null(f.Registry.Slots[IntegrationBrowser.Edge].Value);f.AdmissionBlocked();
    }
    [Fact]public async Task CorruptPayloadIsRejectedBeforeAnyRegistrationReadOrWrite()
    {
        using var f=new Fixture();File.WriteAllText(Path.Combine(f.Generation,"KuaiYi.App.exe"),"PUBLIC_CHANGED_ROLE");await Assert.ThrowsAsync<InvalidDataException>(()=>f.Transaction().RunAsync(true));Assert.Equal(0,f.Registry.Reads);Assert.Equal(0,f.Registry.Writes);
    }
    [Theory][InlineData("receipt")][InlineData("generation")]
    public async Task ChangedOwnershipBeforeCompensationMustRejectEveryFurtherWrite(string change)
    {
        using var f=new Fixture();var original=new IOException("PUBLIC_WRITE_THEN_OWNERSHIP_CHANGE");
        f.Registry.AfterWrite=(browser,value)=>
        {
            if(browser!=IntegrationBrowser.Chrome)return;
            if(change=="receipt")File.WriteAllText(f.Receipt,"PUBLIC_FOREIGN_RECEIPT");
            else
            {
                string replacement=Path.Combine(f.App,"versions",new string('b',32));Directory.CreateDirectory(replacement);
                foreach(string path in Directory.GetFiles(f.Generation))File.Copy(path,Path.Combine(replacement,Path.GetFileName(path)));
                var manifest=JsonNode.Parse(File.ReadAllText(Path.Combine(replacement,"com.kuaiyi.native_host.json")))!;manifest["path"]=Path.Combine(replacement,"KuaiYi.NativeHost.exe");File.WriteAllText(Path.Combine(replacement,"com.kuaiyi.native_host.json"),manifest.ToJsonString());
                var files=Directory.GetFiles(replacement).Where(path=>Path.GetFileName(path)!="package.json").Select(path=>new{path=Path.GetFileName(path),bytes=new FileInfo(path).Length,sha256=Fixture.Hash(File.ReadAllBytes(path))}).ToArray();
                byte[] catalog=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",version="2.0",files});File.WriteAllBytes(Path.Combine(replacement,"package.json"),catalog);
                File.WriteAllText(Path.Combine(f.App,"current.json"),JsonSerializer.Serialize(new{schemaVersion=1,active=new{id=new string('b',32),catalogSha256=Fixture.Hash(catalog)},previous=(object?)null}));Assert.Equal(replacement,f.Store().ReadActive()!.DirectoryPath);
            }
            throw original;
        };
        await Assert.ThrowsAsync<HostRegistrationUnconfirmedException>(()=>f.Transaction().RunAsync(true));
        Assert.Equal(1,f.Registry.Writes);Assert.Equal(f.Manifest,f.Registry.Slots[IntegrationBrowser.Chrome].Value);Assert.True(File.Exists(f.Pending));f.AdmissionBlocked();
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task UnsettledOriginalHandleCloseCannotBeReplacedByNewHandleCompensation(bool initialRead)
    {
        using var f=new Fixture();bool fired=false;
        void FailClose(){if(fired)return;fired=true;f.Registry.OriginalClosePending=true;throw new IOException("PUBLIC_ORIGINAL_HANDLE_CLOSE_UNCONFIRMED");}
        if(initialRead)f.Registry.BeforeRead=FailClose;else f.Registry.AfterWrite=(_,_)=>FailClose();
        var tx=f.Transaction();
        try
        {
            await Assert.ThrowsAsync<HostRegistrationUnconfirmedException>(()=>tx.RunAsync(true));
            Assert.True(f.Registry.OriginalClosePending);Assert.True(File.Exists(f.Pending));f.AdmissionBlocked();Assert.Equal(initialRead?0:1,f.Registry.Writes);
            await Assert.ThrowsAsync<InstallationBusyException>(()=>tx.RunAsync(false));
        }
        finally{await tx.FinalizeRetainedCleanupAsync();}
        Assert.False(f.Registry.OriginalClosePending);Assert.True(File.Exists(f.Pending));f.AdmissionBlocked();
    }
    [Fact]public async Task ExplicitCleanupCannotRunBeforeOriginalRunHasSettled()
    {
        using var f=new Fixture();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        f.Registry.AfterWrite=(_,_)=>{f.Registry.OriginalClosePending=true;entered.Set();if(!release.Wait(TimeSpan.FromSeconds(6)))throw new TimeoutException("PUBLIC_GUARD");throw new IOException("PUBLIC_UNSETTLED_CLOSE");};
        var tx=f.Transaction();var original=tx.RunAsync(true);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(6)));Assert.False(original.IsCompleted);
            await Assert.ThrowsAsync<InstallationBusyException>(()=>tx.FinalizeRetainedCleanupAsync());f.AdmissionBlocked();
        }
        finally{release.Set();await Assert.ThrowsAsync<HostRegistrationUnconfirmedException>(()=>original);await tx.FinalizeRetainedCleanupAsync();}
        Assert.True(File.Exists(f.Pending));f.AdmissionBlocked();
    }
    [Fact]public async Task ExplicitCleanupRetriesOnlyOriginalOwnersAfterPriorAttemptSettles()
    {
        using var f=new Fixture();var closeFailure=new IOException("PUBLIC_FIRST_CLOSE_ATTEMPT");
        f.Registry.BeforeRead=()=>{f.Registry.OriginalClosePending=true;throw new IOException("PUBLIC_ORIGINAL_READ_CLOSE");};
        var tx=f.Transaction();await Assert.ThrowsAsync<HostRegistrationUnconfirmedException>(()=>tx.RunAsync(true));int reads=f.Registry.Reads;
        try
        {
            f.Registry.BeforeFinalize=()=>throw closeFailure;var first=tx.FinalizeRetainedCleanupAsync();Assert.Same(closeFailure,await Assert.ThrowsAsync<IOException>(()=>first));
            f.AdmissionBlocked();Assert.True(f.Registry.OriginalClosePending);f.Registry.BeforeFinalize=null;
            var second=tx.FinalizeRetainedCleanupAsync();Assert.NotSame(first,second);await second;
            Assert.True(first.IsFaulted);Assert.False(f.Registry.OriginalClosePending);Assert.Equal(reads,f.Registry.Reads);Assert.Equal(0,f.Registry.Writes);Assert.True(File.Exists(f.Pending));f.AdmissionBlocked();
        }
        finally{f.Registry.BeforeFinalize=null;await tx.FinalizeRetainedCleanupAsync();}
    }
    [Fact]public void WindowsAdapterHasOnlyFixedProductKeysAndFailsClosedOffWindows()
    {
        Assert.Equal(@"Software\Google\Chrome\NativeMessagingHosts\com.kuaiyi.native_host",WindowsHostRegistrationStore.Key(IntegrationBrowser.Chrome));Assert.Equal(@"Software\Microsoft\Edge\NativeMessagingHosts\com.kuaiyi.native_host",WindowsHostRegistrationStore.Key(IntegrationBrowser.Edge));Assert.Throws<ArgumentOutOfRangeException>(()=>WindowsHostRegistrationStore.Key((IntegrationBrowser)22));
        if(!OperatingSystem.IsWindows()){var native=new WindowsHostRegistrationStore();Assert.Throws<PlatformNotSupportedException>(()=>native.Read(IntegrationBrowser.Chrome));Assert.Throws<PlatformNotSupportedException>(()=>native.Write(IntegrationBrowser.Chrome,new(false,null),"/not-accessed"));}
    }
}
