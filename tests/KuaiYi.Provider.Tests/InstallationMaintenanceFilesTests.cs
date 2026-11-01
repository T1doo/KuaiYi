using System.Security.Cryptography;
using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Provider.Tests;
/// <summary>Real filesystem lifecycle of the production file component, with a fixture-owned live admission. No Windows/registry operations or production trust override.</summary>
public sealed class InstallationMaintenanceFilesTests
{
    private sealed class Rig:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-owned-maintenance-"+Guid.NewGuid().ToString("N"));
        internal string App=>Path.Combine(Root,"app");internal string Data=>Path.Combine(Root,"data");internal string Source=>Path.Combine(Root,"source");
        internal string Before=>Path.Combine(App,"owned");internal string Stage=>Path.Combine(App,"new");internal string Retired=>Path.Combine(App,"retired");
        internal readonly InstallationAdmission Admission;internal readonly InstallationPreparationIO IO;internal readonly InstallationPreparationIO Channel;internal readonly List<string> Files=[];internal bool Keep=false;
        internal readonly InstallationFile[] Old=[Entry("role.bin","OLD PUBLIC PRODUCT"u8.ToArray()),Entry("nested/role.json","PUBLIC CONFIG"u8.ToArray())];
        internal readonly InstallationFile[] Fresh=[Entry("role.bin","NEW PUBLIC PRODUCT"u8.ToArray()),Entry("nested/role.json","PUBLIC CONFIG"u8.ToArray())];
        internal Rig(InstallationPreparationFaults? faults=null)
        {
            Directory.CreateDirectory(Data);Directory.CreateDirectory(Source);var store=new InstallationStore(App,Data,()=>true);using(var initial=store.EnterAdmission(mutation:true)){}Admission=store.EnterAdmission(mutation:true,existing:true);Directory.CreateDirectory(Before);Directory.CreateDirectory(Path.Combine(Before,"nested"));
            Put(Path.Combine(Before,"role.bin"),"OLD PUBLIC PRODUCT"u8.ToArray());Put(Path.Combine(Before,"nested/role.json"),"PUBLIC CONFIG"u8.ToArray());Put(Path.Combine(Source,"role.bin"),"NEW PUBLIC PRODUCT"u8.ToArray());Put(Path.Combine(Source,"nested/role.json"),"PUBLIC CONFIG"u8.ToArray());Put(Path.Combine(Data,"private-kept.txt"),"PUBLIC USER CONTENT"u8.ToArray());
            IO=InstallationPreparationIO.ReadOnly(App,faults);Channel=IO.CreateMaintenanceChannel(App,Source,new[]{Before,Stage,Retired}.Concat(Fresh.Select(f=>Path.Combine(Stage,f.Path))));
        }
        private static InstallationFile Entry(string p,byte[] b)=>new(p,b.Length,Convert.ToHexString(SHA256.HashData(b)));
        internal void Put(string p,byte[] b){Directory.CreateDirectory(Path.GetDirectoryName(p)!);File.WriteAllBytes(p,b);Files.Add(p);}
        // The callback is an explicit fixture capability. Full production pins/G/B qualification is not simulated or opened.
        internal bool Live=true;internal void Require(){Admission.RequireMutation(App);if(!Live)throw new InstallationRecoveryException("Original fixture capability closed.");}
        internal InstallationMaintenanceFiles Engine=>new(Channel,Require);
        internal void Prepare()
        {Channel.CreateMaintenanceDirectory(Stage);foreach(var f in Fresh)Channel.CopyPayload(Path.Combine(Source,f.Path),Path.Combine(Stage,f.Path),f);InstallationMaintenanceFiles.Verify(Channel,Stage,Fresh);}
        public void Dispose()
        {
            IO.CloseOriginals();Assert.True(IO.CleanupConfirmed);Assert.Equal("PUBLIC USER CONTENT",File.ReadAllText(Path.Combine(Data,"private-kept.txt")));
            Admission.Dispose();if(Keep||IO.StorageUnconfirmed||!IO.CleanupConfirmed)return;
            // Only this exact UUID root and explicitly enumerated fixture files/directories are removed after proven settlement.
            var dirs=Directory.GetDirectories(Root,"*",SearchOption.AllDirectories).OrderByDescending(p=>p.Length).ToArray();foreach(var file in Directory.GetFiles(Root,"*",SearchOption.AllDirectories))File.Delete(file);foreach(var dir in dirs)Directory.Delete(dir);Directory.Delete(Root);
        }
    }
    [Fact]public void UninstallActuallyRetiresOnlyTheWhitelistedProductDirectory()
    {using var r=new Rig();r.Engine.Move(r.Before,r.Retired,r.Old);Assert.False(Directory.Exists(r.Before));InstallationMaintenanceFiles.Verify(r.IO,r.Retired,r.Old);Assert.True(Directory.Exists(r.Source));}
    [Fact]public void RepairCopiesStagesRetainsDamagedOriginalAndInstallsFreshBytes()
    {using var r=new Rig();r.Put(Path.Combine(r.Before,"role.bin"),"DAMAGED PUBLIC PRODUCT"u8.ToArray());var before=InstallationMaintenanceFiles.Capture(r.IO,r.Before,r.Old,true);r.Prepare();r.Engine.Move(r.Before,r.Retired,before);r.Engine.Move(r.Stage,r.Before,r.Fresh);Assert.Equal("NEW PUBLIC PRODUCT",File.ReadAllText(Path.Combine(r.Before,"role.bin")));Assert.Equal("DAMAGED PUBLIC PRODUCT",File.ReadAllText(Path.Combine(r.Retired,"role.bin")));Assert.False(Directory.Exists(r.Stage));}
    [Theory][InlineData("extra.txt")][InlineData("unknown/extra.txt")]
    public void UnknownContentRefusesBeforeAnyRetirement(string name)
    {using var r=new Rig();r.Put(Path.Combine(r.Before,name),"UNKNOWN PUBLIC INPUT"u8.ToArray());Assert.Throws<InstallationRecoveryException>(()=>r.Engine.Move(r.Before,r.Retired,r.Old));Assert.True(Directory.Exists(r.Before));Assert.False(Directory.Exists(r.Retired));Assert.True(File.Exists(Path.Combine(r.Before,name)));}
    [Fact]public void ContentChangedAfterSnapshotIsPreservedAndNotMoved()
    {using var r=new Rig();var before=InstallationMaintenanceFiles.Capture(r.IO,r.Before,r.Old,false);r.Put(Path.Combine(r.Before,"role.bin"),"CHANGED PUBLIC PRODUCT"u8.ToArray());Assert.Throws<InstallationRecoveryException>(()=>r.Engine.Move(r.Before,r.Retired,before));Assert.False(Directory.Exists(r.Retired));}
    [Fact]public void ClosedOriginalCapabilityCannotMoveKnownBytes()
    {using var r=new Rig();r.Live=false;Assert.Throws<InstallationRecoveryException>(()=>r.Engine.Move(r.Before,r.Retired,r.Old));Assert.True(Directory.Exists(r.Before));Assert.False(Directory.Exists(r.Retired));}
    [Fact]public void ExistingRecoveryTargetCannotBeReplaced()
    {using var r=new Rig();Directory.CreateDirectory(r.Retired);r.Put(Path.Combine(r.Retired,"foreign.txt"),"PUBLIC FOREIGN BACKUP"u8.ToArray());Assert.Throws<InstallationRecoveryException>(()=>r.Engine.Move(r.Before,r.Retired,r.Old));Assert.True(Directory.Exists(r.Before));Assert.Equal("PUBLIC FOREIGN BACKUP",File.ReadAllText(Path.Combine(r.Retired,"foreign.txt")));}
    [Theory][InlineData(false)][InlineData(true)]
    public void ActualMoveExceptionBeforeOrAfterEffectIsPermanentlyUnknown(bool moved)
    {
        using var r=new Rig(new(){Move=(from,to)=>{if(moved)Directory.Move(from,to);throw new IOException("PUBLIC_MOVE_UNKNOWN");}});
        Assert.Throws<IOException>(()=>r.Engine.Move(r.Before,r.Retired,r.Old));Assert.True(r.IO.StorageUnconfirmed);Assert.Equal(moved,Directory.Exists(r.Retired));Assert.Equal(!moved,Directory.Exists(r.Before));Assert.True(r.IO.CleanupConfirmed);
    }
    [Fact]public void FailedOriginalCloseKeepsLedgerUnconfirmedUntilThatResourceSettles()
    {
        bool fail=true;using var r=new Rig(new(){BeforeClose=(kind,_)=>{if(fail&&kind=="maintenance-hash")throw new IOException("PUBLIC_CLOSE_UNKNOWN");}});
        Assert.ThrowsAny<Exception>(()=>InstallationMaintenanceFiles.Capture(r.IO,r.Before,r.Old,false));Assert.False(r.IO.CleanupConfirmed);Assert.False(Directory.Exists(r.Retired));fail=false;r.IO.CloseOriginals();Assert.True(r.IO.CleanupConfirmed);
    }
    [Fact]public void CancellationDuringValidationBeforeCommitLeavesOriginalUntouched()
    {
        using var stop=new CancellationTokenSource();using var r=new Rig(new(){BeforeClose=(kind,_)=>{if(kind=="maintenance-hash")stop.Cancel();}});
        var engine=new InstallationMaintenanceFiles(r.Channel,r.Require,()=>stop.Token.ThrowIfCancellationRequested());
        Assert.Throws<OperationCanceledException>(()=>engine.Move(r.Before,r.Retired,r.Old));Assert.True(Directory.Exists(r.Before));Assert.False(Directory.Exists(r.Retired));Assert.False(r.IO.StorageUnconfirmed);
    }
    [Fact]public void CancellationAfterCommitDoesNotAbandonOriginalKnownMove()
    {
        using var stop=new CancellationTokenSource();using var r=new Rig();bool committed=false;
        var engine=new InstallationMaintenanceFiles(r.Channel,r.Require,()=>{if(!committed){stop.Token.ThrowIfCancellationRequested();committed=true;stop.Cancel();}});
        engine.Move(r.Before,r.Retired,r.Old);Assert.True(committed);Assert.True(stop.IsCancellationRequested);Assert.False(Directory.Exists(r.Before));InstallationMaintenanceFiles.Verify(r.IO,r.Retired,r.Old);
    }

    [Fact]public void RepairCanRetireKnownEmptyDirectoryAfterItsFileWentMissing()
    {
        using var r=new Rig();File.Delete(Path.Combine(r.Before,"nested/role.json"));var before=InstallationMaintenanceFiles.Capture(r.IO,r.Before,r.Old,true);r.Prepare();
        r.Engine.Move(r.Before,r.Retired,before,r.Old);r.Engine.Move(r.Stage,r.Before,r.Fresh);Assert.True(File.Exists(Path.Combine(r.Before,"nested/role.json")));Assert.False(File.Exists(Path.Combine(r.Retired,"nested/role.json")));Assert.True(Directory.Exists(Path.Combine(r.Retired,"nested")));
    }
    [Fact]public void PartialReferenceAttemptCannotClaimNothingExecuted()
    {
        var progress=new InstallationMaintenanceProgress(true,true,2,3,true,false,false,true);
        var result=new InstallationMaintenanceOutcome("public-op",InstallationMaintenanceIntent.Uninstall,InstallationMaintenanceStatus.Unconfirmed,null,true,[],[],[],"public-op",progress);
        var lines=InstallationMaintenanceReceiptText.Lines(result);Assert.DoesNotContain(lines,l=>l.StartsWith("MAINTENANCE_NOT_EXECUTED"));Assert.Contains("MAINTENANCE_RECOVERY|public-op",lines);Assert.Contains(lines,l=>l.StartsWith("MAINTENANCE_PROGRESS|True|2|3|"));
    }

    [Fact]public void PartialMaintenanceCopyIsUnknownAndOriginalRemainsRecoverable()
    {
        using var r=new Rig(new(){WritePayload=(_,file,bytes)=>{file.Write(bytes.Span[..Math.Max(1,bytes.Length/2)]);throw new IOException("PUBLIC_PARTIAL_COPY");}}){Keep=true};
        Assert.Throws<IOException>(r.Prepare);Assert.True(r.IO.StorageUnconfirmed);Assert.True(r.IO.CleanupConfirmed);Assert.Equal("OLD PUBLIC PRODUCT",File.ReadAllText(Path.Combine(r.Before,"role.bin")));Assert.True(File.Exists(Path.Combine(r.Stage,"role.bin")));Assert.False(Directory.Exists(r.Retired));
    }
    [Fact]public void UnknownEmptyDirectoryIsNotAdopted()
    {using var r=new Rig();Directory.CreateDirectory(Path.Combine(r.Before,"unknown-empty"));Assert.Throws<InstallationRecoveryException>(()=>r.Engine.Move(r.Before,r.Retired,r.Old));Assert.False(Directory.Exists(r.Retired));}
    [Fact]public void ReparseFileIsRejectedWithoutReadingOrMovingItsTarget()
    {using var r=new Rig();File.Delete(Path.Combine(r.Before,"role.bin"));File.CreateSymbolicLink(Path.Combine(r.Before,"role.bin"),Path.Combine(r.Source,"role.bin"));Assert.ThrowsAny<IOException>(()=>r.Engine.Move(r.Before,r.Retired,r.Old));Assert.Equal("NEW PUBLIC PRODUCT",File.ReadAllText(Path.Combine(r.Source,"role.bin")));Assert.False(Directory.Exists(r.Retired));}
    [Fact]public void LeaseLossDuringByteValidationCannotReachMove()
    {Rig? owner=null;using var r=new Rig(new(){BeforeClose=(kind,_)=>{if(kind=="maintenance-hash"&&owner!=null)owner.Live=false;}});owner=r;Assert.Throws<InstallationRecoveryException>(()=>r.Engine.Move(r.Before,r.Retired,r.Old));Assert.True(Directory.Exists(r.Before));Assert.False(Directory.Exists(r.Retired));}

}
