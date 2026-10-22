using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed class InstallationBootstrapDeploymentTests
{
    private sealed class Rig:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-bootstrap-tests-"+Guid.NewGuid().ToString("N"));
        internal string App=>Path.Combine(Root,"application");internal string Data=>Path.Combine(Root,"user-data");internal BootstrapSource Source{get;}
        internal InstallationStore Store=>new(App,Data,()=>true);
        internal InstallationBootstrapDeployment Tx=>new(App,Data);
        internal string Active=>InstallationBootstrapDeployment.ActivePath(App);internal string Pending=>InstallationBootstrapDeployment.PendingPath(App);internal string Owner=>InstallationBootstrapDeployment.OwnerPath(App);
        internal Rig(){Directory.CreateDirectory(Data);File.WriteAllText(Path.Combine(Data,"PUBLIC_OFF"),"PUBLIC_ENCRYPTED_SENTINEL");new InstallationStore(App,Data,()=>true).ApplyAsync(new MaterializedPayloadSource(Path.Combine(Root,"generation-source")).Root,MaterializedPayloadSource.Hash(Path.Combine(Root,"generation-source/package.json"))).GetAwaiter().GetResult();Source=new(Path.Combine(Root,"bootstrap-source"));}
        internal InstallationBootstrapDeployment.Receipt Receipt()=>InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.Receipt>(File.ReadAllBytes(Active));
        internal string Bundle=>InstallationBootstrapDeployment.BundlePath(App,Receipt().Bundle);
        internal async Task Deploy()=>Assert.Equal(BootstrapOutcome.Deployed,await Tx.DeployAsync(Source.Root,Source.Digest));
        internal void Available(){using var a=Store.EnterRoleAdmission();}
        internal void Blocked()=>Assert.ThrowsAny<IOException>(()=>Store.EnterRoleAdmission());
        public void Dispose(){Assert.Equal("PUBLIC_ENCRYPTED_SENTINEL",File.ReadAllText(Path.Combine(Data,"PUBLIC_OFF")));Directory.Delete(Root,true);}
    }
    [Fact]public async Task ExactImmutableDevelopmentBundleReplacesAndWithdrawsWithoutOverwritingAnyPublishedFile()
    {
        using var r=new Rig();await r.Deploy();string old=r.Bundle;var before=Directory.GetFiles(old).ToDictionary(p=>Path.GetFileName(p),InstallationBootstrapPackage.Hash);using var entry=InstallationBootstrapEntry.Open(old,r.Data);
        Assert.Equal(r.App,entry.Root);Assert.Equal(6,before.Count);Assert.Equal(InstallationBootstrapPackage.Mode,r.Receipt().Mode);
        r.Source.Version="2.0";r.Source.Catalog();await r.Deploy();Assert.NotEqual(old,r.Bundle);foreach(var (name,hash) in before)Assert.Equal(hash,InstallationBootstrapPackage.Hash(Path.Combine(old,name)));
        Assert.ThrowsAny<IOException>(()=>InstallationBootstrapEntry.Open(old,r.Data));using(var current=InstallationBootstrapEntry.Open(r.Bundle,r.Data))Assert.Equal(r.App,current.Root);
        Assert.Equal(BootstrapOutcome.Withdrawn,await r.Tx.WithdrawAsync());Assert.False(File.Exists(r.Active));Assert.True(Directory.Exists(old));Assert.Equal(2,Directory.GetDirectories(InstallationBootstrapDeployment.BundlesPath(r.App)).Length);r.Available();Assert.Equal(BootstrapOutcome.AlreadyWithdrawn,await r.Tx.WithdrawAsync());
    }
    [Theory][InlineData("extra")][InlineData("missing")][InlineData("core-byte")][InlineData("launcher-revision")][InlineData("deps")][InlineData("runtime")][InlineData("duplicate")]
    public async Task ANewPinnedDigestCannotAuthorizeAnUnknownClosureOrMixedCore(string fault)
    {
        using var r=new Rig();string core=Path.Combine(r.Source.Root,"KuaiYi.Core.dll"),launcher=Path.Combine(r.Source.Root,"KuaiYi.Launcher.dll");
        if(fault=="extra")File.WriteAllText(Path.Combine(r.Source.Root,"PUBLIC_EXTRA.dll"),"PUBLIC_UNKNOWN");
        if(fault=="missing")File.Delete(core);
        if(fault=="core-byte"){byte[] b=File.ReadAllBytes(core);b[^1]^=1;File.WriteAllBytes(core,b);}
        if(fault=="launcher-revision"){byte[] b=File.ReadAllBytes(launcher),revision=Encoding.ASCII.GetBytes(MaterializedPayloadSource.Revision);int at=b.AsSpan().IndexOf(revision);Assert.True(at>=0);b[at]=b[at]=='0'?(byte)'1':(byte)'0';File.WriteAllBytes(launcher,b);}
        if(fault=="deps"){string p=Path.Combine(r.Source.Root,"KuaiYi.Launcher.deps.json");var node=JsonNode.Parse(File.ReadAllText(p))!;node["libraries"]!["PUBLIC_EXTRA/1.0"]=new JsonObject();File.WriteAllText(p,node.ToJsonString());}
        if(fault=="runtime"){string p=Path.Combine(r.Source.Root,"KuaiYi.Launcher.runtimeconfig.json");var node=JsonNode.Parse(File.ReadAllText(p))!;node["runtimeOptions"]!["framework"]!["version"]="10.0.13";File.WriteAllText(p,node.ToJsonString());}
        if(fault=="duplicate"){string p=Path.Combine(r.Source.Root,"KuaiYi.Launcher.deps.json");File.WriteAllText(p,File.ReadAllText(p).Insert(1,"\"compilationOptions\":{},"));}
        r.Source.Catalog();await Assert.ThrowsAnyAsync<Exception>(()=>r.Tx.DeployAsync(r.Source.Root,r.Source.Digest));Assert.False(File.Exists(r.Active));Assert.False(File.Exists(r.Pending));Assert.False(Directory.Exists(InstallationBootstrapDeployment.BundlesPath(r.App)));r.Available();
    }
    [Theory]
    [InlineData(false,(int)BootstrapStage.JournalPublished)][InlineData(false,(int)BootstrapStage.StoreOwned)][InlineData(false,(int)BootstrapStage.BundleCopied)][InlineData(false,(int)BootstrapStage.BundlePublished)][InlineData(false,(int)BootstrapStage.ReceiptPublished)][InlineData(false,(int)BootstrapStage.BeforeSettlement)]
    [InlineData(true,(int)BootstrapStage.JournalPublished)][InlineData(true,(int)BootstrapStage.StoreOwned)][InlineData(true,(int)BootstrapStage.BundleCopied)][InlineData(true,(int)BootstrapStage.BundlePublished)][InlineData(true,(int)BootstrapStage.ReceiptPublished)][InlineData(true,(int)BootstrapStage.BeforeSettlement)]
    public async Task EveryProvisionalPhaseRestoresTheExactPriorReceiptAndRetainsOnlyOwnedCandidates(bool replace,int phase)
    {
        BootstrapStage stage=(BootstrapStage)phase;using var r=new Rig();if(replace)await r.Deploy();byte[]? before=File.Exists(r.Active)?File.ReadAllBytes(r.Active):null;string? old=replace?r.Bundle:null;var failure=new IOException("PUBLIC_PHASE_FAILURE");
        var tx=new InstallationBootstrapDeployment(r.App,r.Data){StageForTest=s=>{if(s==stage)throw failure;}};
        Assert.Same(failure,await Assert.ThrowsAsync<IOException>(()=>tx.DeployAsync(r.Source.Root,r.Source.Digest)));Assert.False(File.Exists(r.Pending));if(before==null)Assert.False(File.Exists(r.Active));else{Assert.Equal(before,File.ReadAllBytes(r.Active));using var entry=InstallationBootstrapEntry.Open(old!,r.Data);}r.Available();
    }
    [Fact]public async Task PartialOriginalCopyIsNotCommittedOrErasedAndDoesNotDamageThePriorBundle()
    {
        using var r=new Rig();await r.Deploy();byte[] before=File.ReadAllBytes(r.Active);string old=r.Bundle;var tx=new InstallationBootstrapDeployment(r.App,r.Data){StageForTest=s=>{if(s==BootstrapStage.JournalPublished)File.WriteAllBytes(Path.Combine(r.Source.Root,"KuaiYi.Core.dll"),"PUBLIC_TRUNCATED_AFTER_VERIFY"u8.ToArray());}};
        await Assert.ThrowsAsync<InvalidDataException>(()=>tx.DeployAsync(r.Source.Root,r.Source.Digest));Assert.Equal(before,File.ReadAllBytes(r.Active));Assert.Single(Directory.GetDirectories(InstallationBootstrapDeployment.BundlesPath(r.App),"staging-*"));using var entry=InstallationBootstrapEntry.Open(old,r.Data);r.Available();
    }
    [Theory][InlineData(false)][InlineData(true)]public async Task WithdrawalFailureRestoresExactActiveWithoutDeletingTheOwnedBundle(bool afterArchive)
    {
        using var r=new Rig();await r.Deploy();byte[] before=File.ReadAllBytes(r.Active);string old=r.Bundle;var failure=new IOException("PUBLIC_WITHDRAW_FAILURE");var tx=new InstallationBootstrapDeployment(r.App,r.Data){StageForTest=s=>{if(s==(afterArchive?BootstrapStage.ReceiptPublished:BootstrapStage.StoreOwned))throw failure;}};
        Assert.Same(failure,await Assert.ThrowsAsync<IOException>(()=>tx.WithdrawAsync()));Assert.Equal(before,File.ReadAllBytes(r.Active));Assert.True(Directory.Exists(old));Assert.False(File.Exists(r.Pending));r.Available();
    }
    [Theory][InlineData("active")][InlineData("owner")]
    public async Task AConflictingCompensationRetainsBothOriginalErrorsAndThePersistentGate(string conflict)
    {
        using var r=new Rig();await r.Deploy();var operation=new IOException("PUBLIC_OPERATION_FAILURE");string path=conflict=="active"?r.Active:r.Owner;var tx=new InstallationBootstrapDeployment(r.App,r.Data){StageForTest=s=>{if(s==BootstrapStage.ReceiptPublished){File.WriteAllText(path,"PUBLIC_FOREIGN_REPLACEMENT");throw operation;}}};
        var error=await Assert.ThrowsAsync<BootstrapUnconfirmedException>(()=>tx.DeployAsync(r.Source.Root,r.Source.Digest));Assert.Contains(operation,Assert.IsType<AggregateException>(error.InnerException).InnerExceptions);Assert.Equal("PUBLIC_FOREIGN_REPLACEMENT",File.ReadAllText(path));Assert.True(File.Exists(r.Pending));r.Blocked();
    }
    [Fact]public async Task CancellationAfterOriginalCopyRollsBackWithoutAbandoningItsOriginalFileIo()
    {
        using var r=new Rig();await r.Deploy();byte[] before=File.ReadAllBytes(r.Active);using var stop=new CancellationTokenSource();var tx=new InstallationBootstrapDeployment(r.App,r.Data){StageForTest=s=>{if(s==BootstrapStage.BundleCopied)stop.Cancel();}};
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>tx.DeployAsync(r.Source.Root,r.Source.Digest,stop.Token));Assert.Equal(before,File.ReadAllBytes(r.Active));Assert.False(File.Exists(r.Pending));r.Available();
    }
    [Theory][InlineData("pending")][InlineData("pending-directory")][InlineData("owner")][InlineData("unowned-folder")][InlineData("unowned-active")]
    public async Task ForeignOrUnknownStateIsNeverAdoptedOrDeleted(string fault)
    {
        using var r=new Rig();string path=fault switch{"owner"=>r.Owner,"unowned-folder"=>InstallationBootstrapDeployment.BundlesPath(r.App),"unowned-active"=>r.Active,_=>r.Pending};bool directory=fault is "unowned-folder" or "pending-directory";
        if(directory)Directory.CreateDirectory(path);else File.WriteAllText(path,"PUBLIC_FOREIGN");await Assert.ThrowsAnyAsync<Exception>(()=>r.Tx.DeployAsync(r.Source.Root,r.Source.Digest));if(directory)Assert.True(Directory.Exists(path));else Assert.Equal("PUBLIC_FOREIGN",File.ReadAllText(path));Assert.False(File.Exists(Path.Combine(r.App,"execution-owner.json")));
    }
    [Fact]public async Task DamagedProvisionalBundleAtFinalSettlementCannotBeReportedAsACompletedDeployment()
    {
        using var r=new Rig();await r.Deploy();string old=r.Bundle;string oldHash=InstallationBootstrapPackage.Hash(Path.Combine(old,"KuaiYi.Core.dll"));
        var tx=new InstallationBootstrapDeployment(r.App,r.Data){StageForTest=s=>{if(s==BootstrapStage.BeforeSettlement)File.WriteAllText(Path.Combine(r.Bundle,"KuaiYi.Core.dll"),"PUBLIC_DAMAGED_BEFORE_FINAL_SETTLEMENT");}};
        await Assert.ThrowsAsync<BootstrapUnconfirmedException>(()=>tx.DeployAsync(r.Source.Root,r.Source.Digest));Assert.True(File.Exists(r.Pending));r.Blocked();Assert.Equal(oldHash,InstallationBootstrapPackage.Hash(Path.Combine(old,"KuaiYi.Core.dll")));Assert.ThrowsAny<IOException>(()=>InstallationBootstrapEntry.Open(r.Bundle,r.Data));
    }
    [Fact]public void OwnerAndReceiptEncodingRejectConsumerOversizeBeforePublication()
    {
        string root=Path.Combine(Path.GetTempPath(),new string('a',2200)),data=Path.Combine(Path.GetTempPath(),new string('b',2200));
        var owner=new InstallationBootstrapDeployment.StoreOwner(1,"KuaiYi",Guid.NewGuid(),root,data);Assert.True(JsonSerializer.SerializeToUtf8Bytes(owner).Length>InstallationBootstrapDeployment.OwnerLimit);Assert.Throws<InvalidDataException>(()=>InstallationBootstrapDeployment.EncodeOwner(owner));
        var receipt=new InstallationBootstrapDeployment.Receipt(1,"KuaiYi",InstallationBootstrapPackage.Mode,Guid.NewGuid(),Guid.NewGuid(),new string('A',64),new string('0',40),new("1.0",root+new string('c',7000),new string('A',64)));
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(receipt).Length>InstallationBootstrapDeployment.ReceiptLimit);Assert.Throws<InvalidDataException>(()=>InstallationBootstrapDeployment.EncodeReceipt(receipt));
    }
    [Fact]public void IndividuallyBoundedMetadataMustAlsoFitTheOriginalSealedStartEnvelope()
    {
        string root=Path.Combine(Path.GetTempPath(),string.Join(Path.DirectorySeparatorChar,Enumerable.Repeat(new string('a',200),8))),data=Path.Combine(Path.GetTempPath(),string.Join(Path.DirectorySeparatorChar,Enumerable.Repeat(new string('b',200),9)));
        var generation=new InstalledVersion("1.0",Path.Combine(root,"versions",Guid.NewGuid().ToString("N")),new string('A',64));Guid id=Guid.NewGuid();
        byte[] owner=InstallationBootstrapDeployment.EncodeOwner(new(1,"KuaiYi",id,root,data)),active=InstallationBootstrapDeployment.EncodeReceipt(new(1,"KuaiYi",InstallationBootstrapPackage.Mode,id,Guid.NewGuid(),new string('A',64),new string('0',40),generation));
        Assert.True(owner.Length<=InstallationBootstrapDeployment.OwnerLimit&&active.Length<=InstallationBootstrapDeployment.ReceiptLimit);
        Assert.Throws<InvalidDataException>(()=>InstallationBootstrapDeployment.RequireTransport(root,data,generation,"PUBLIC_CURRENT"u8.ToArray(),owner,active));
    }
    [Fact]public async Task EntryFailureRetainsTheSameAdmissionAndPreservesBodyAndOriginalCloseErrors()
    {
        using var r=new Rig();await r.Deploy();string old=r.Bundle;r.Source.Version="2.0";r.Source.Catalog();await r.Deploy();InstallationAdmission? original=null;bool release=false;var close=new IOException("PUBLIC_ORIGINAL_CLOSE_FAILURE");
        var failure=Assert.Throws<AggregateException>(()=>InstallationBootstrapEntry.Open(old,r.Data,held=>{original??=held;Assert.Same(original,held);if(!release)throw close;held.Dispose();}));Assert.Contains(close,failure.InnerExceptions);Assert.Contains(failure.InnerExceptions,e=>e is InstallationRecoveryException);var owner=Assert.Single(InstallationBootstrapEntry.RetainedForTest,e=>e.Root==r.App);
        try{Assert.True(File.Exists(r.Pending));r.Blocked();release=true;owner.Dispose();Assert.DoesNotContain(owner,InstallationBootstrapEntry.RetainedForTest);Assert.True(File.Exists(r.Pending));r.Blocked();}
        finally{release=true;owner.Dispose();}
    }
}
