using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class SettingsBindingTests
{
    private sealed class SyntheticDirectory:ISecretDirectoryPolicy{public void EnsurePrivate(string p)=>Directory.CreateDirectory(p);}
    private sealed class SyntheticProtector:ISecretProtector
    {
        private static readonly byte[] Key=Enumerable.Repeat((byte)19,32).ToArray();public int Opens;
        public byte[] Protect(ReadOnlySpan<byte> p){byte[] b=new byte[p.Length+28];RandomNumberGenerator.Fill(b.AsSpan(0,12));using var aes=new AesGcm(Key,16);aes.Encrypt(b.AsSpan(0,12),p,b.AsSpan(28),b.AsSpan(12,16));return b;}
        public byte[] Unprotect(ReadOnlySpan<byte> b){Opens++;byte[] p=new byte[b.Length-28];using var aes=new AesGcm(Key,16);aes.Decrypt(b[..12],b[28..],b.Slice(12,16),p);return p;}
    }
    private sealed class Fixture:IDisposable
    {
        public string Root{get;}=Path.Combine(Path.GetTempPath(),"kuaiyi-settings-tests-"+Guid.NewGuid().ToString("N"));
        public SettingsStore Store{get;}public SecretFileStore Secrets{get;}public SettingsCoordinator Coordinator{get;}public SyntheticProtector Protector{get;}=new();
        public Fixture(){Store=new(Path.Combine(Root,"config"));Secrets=new(Path.Combine(Root,"secrets"),Protector,new SyntheticDirectory());Coordinator=new(Store,Secrets);}
        public async Task<SettingsSnapshot> Seed()
        {
            using(var d=await Coordinator.OpenDraftAsync()){d.Update(s=>s with{Profiles=[new(){Id="a",Name="公开合成A",Model="public-model",BaseUrl="https://public.invalid/v1"}],ActiveProfileId="a"});d.SetKey("a","public-SYNTHETIC-shared-KEY"u8);await Coordinator.SaveAsync(d);}
            using var shared=await Coordinator.OpenDraftAsync();shared.Update(s=>s with{Profiles=s.Profiles.Add(s.Profiles[0] with{Id="b",Name="公开合成B"})});return await Coordinator.SaveAsync(shared);
        }
        public void Dispose(){if(Directory.Exists(Root))Directory.Delete(Root,true);}
    }
    private static readonly EndpointOrigin Origin=new(new Uri("https://public.invalid"));
    private static RuntimeGate Enabled(){var g=new RuntimeGate();g.ConsentTo(Origin);Assert.True(g.TryEnable(Origin,true));return g;}
    private static (RequestProfileBinding Binding,RequestIdentity Identity,CancellationToken Token) Start(RuntimeGate g,string profile)
    {Assert.True(g.TryGetProfileBinding(profile,out var b));Assert.True(g.TryStartProfileRequest(b,Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var t));return(b,id,t);}
    private static void Edit(SettingsDraft d,string operation)
    {
        if(operation=="clear")d.ClearKey("a");
        else if(operation=="delete")d.Update(s=>s with{Profiles=s.Profiles.Where(p=>p.Id!="a").ToImmutableArray(),ActiveProfileId="b"});
        else if(operation=="replace")d.SetKey("a","public-SYNTHETIC-replacement-KEY"u8);
        else throw new ArgumentException("Unknown synthetic operation.");
    }
    [Theory][InlineData("clear")][InlineData("delete")][InlineData("replace")]
    public async Task ActualSettingsLinkCancelsAOldBindingButKeepsBTaskSharedCipherAndLateGatesCorrect(string operation)
    {
        using var f=new Fixture();var snapshot=await f.Seed();string reference=snapshot.Data.Profiles[0].SecretRef!;
        var a=Enabled();var b=Enabled();using var linkA=new SettingsRuntimeLink(f.Coordinator,a);using var linkB=new SettingsRuntimeLink(f.Coordinator,b);linkA.Initialize(snapshot);linkB.Initialize(snapshot);
        var requestA=Start(a,"a");var requestB=Start(b,"b");SettingsChange? observed=null;f.Coordinator.Committed+=(_,change)=>observed=change;
        using(var d=await f.Coordinator.OpenDraftAsync()){Edit(d,operation);await f.Coordinator.SaveAsync(d);}
        var revoked=Assert.Single(observed!.RevokedProfileBindings);Assert.Equal("a",revoked.ProfileId);Assert.Equal(reference,revoked.PreviousSecretReference);Assert.True(observed.HasDeletion);Assert.Empty(observed.DeletedSecrets);
        Assert.True(requestA.Token.IsCancellationRequested);Assert.False(a.CanApply(requestA.Identity));Assert.False(a.CanSend(requestA.Identity,Origin));int callbacks=0;Assert.False(a.TryApply(requestA.Identity,()=>callbacks++));Assert.False(a.TryBeginSend(requestA.Identity,Origin,()=>{callbacks++;return Task.FromResult(true);},out _));Assert.False(a.TryRevealDelta(requestA.Identity));Assert.Equal(0,callbacks);
        Assert.Equal(Activation.On,a.Snapshot.Activation);Assert.False(a.TryStartProfileRequest(requestA.Binding,Guid.NewGuid(),Guid.NewGuid(),null,out _,out _));
        Assert.False(requestB.Token.IsCancellationRequested);Assert.True(b.CanApply(requestB.Identity));Assert.True(b.CanSend(requestB.Identity,Origin));Assert.True(b.TryGetProfileBinding("b",out var sameB));Assert.Equal(requestB.Binding,sameB);
        Assert.False(f.Secrets.IsRevoked(reference));using(var key=await f.Secrets.OpenAsync(reference))Assert.True(key.Bytes.Span.SequenceEqual("public-SYNTHETIC-shared-KEY"u8));
        var newB=Start(a,"b");Assert.True(a.CanSend(newB.Identity,Origin));Assert.True(a.TryApply(newB.Identity,()=>callbacks++));Assert.Equal(1,callbacks);
        a.Disable();b.Disable();
    }
    [Theory][InlineData("clear")][InlineData("delete")][InlineData("replace")]
    public async Task PrePublicationFailureDoesNotNotifyRevokeBindingsOrCancelEitherTask(string operation)
    {
        using var f=new Fixture();var snapshot=await f.Seed();byte[] primary=await File.ReadAllBytesAsync(f.Store.PrimaryPath);byte[] backup=await File.ReadAllBytesAsync(f.Store.BackupPath);
        var store=new SettingsStore(f.Store.DirectoryPath){WriteStageForTest=s=>{if(s==SettingsWriteStage.BeforePublish)throw new IOException("public precommit failure");}};var coordinator=new SettingsCoordinator(store,f.Secrets);
        var a=Enabled();var b=Enabled();using var linkA=new SettingsRuntimeLink(coordinator,a);using var linkB=new SettingsRuntimeLink(coordinator,b);linkA.Initialize(snapshot);linkB.Initialize(snapshot);var ra=Start(a,"a");var rb=Start(b,"b");int notifications=0;coordinator.Committed+=(_,_)=>notifications++;
        using var d=await coordinator.OpenDraftAsync();Edit(d,operation);await Assert.ThrowsAsync<IOException>(()=>coordinator.SaveAsync(d));
        Assert.Equal(0,notifications);Assert.False(ra.Token.IsCancellationRequested);Assert.False(rb.Token.IsCancellationRequested);Assert.True(a.CanApply(ra.Identity));Assert.True(b.CanApply(rb.Identity));Assert.Equal(primary,await File.ReadAllBytesAsync(f.Store.PrimaryPath));Assert.Equal(backup,await File.ReadAllBytesAsync(f.Store.BackupPath));Assert.False(f.Secrets.IsRevoked(ra.Binding.SecretReference!));a.Disable();b.Disable();
    }
    [Theory][InlineData("clear")][InlineData("delete")][InlineData("replace")]
    public async Task OneObserverFaultCannotSkipRuntimeInvalidationAndRecoveryRepeatsSameCommittedBindingNotice(string operation)
    {
        using var f=new Fixture();var snapshot=await f.Seed();Action<SettingsSnapshot,SettingsChange> fault=(_,_)=>throw new IOException("public observer interruption");f.Coordinator.Committed+=fault;
        var a=Enabled();var b=Enabled();using var linkA=new SettingsRuntimeLink(f.Coordinator,a);using var linkB=new SettingsRuntimeLink(f.Coordinator,b);linkA.Initialize(snapshot);linkB.Initialize(snapshot);var ra=Start(a,"a");var rb=Start(b,"b");var notices=new List<SettingsChange>();f.Coordinator.Committed+=(_,change)=>notices.Add(change);
        using(var d=await f.Coordinator.OpenDraftAsync()){Edit(d,operation);await Assert.ThrowsAsync<IOException>(()=>f.Coordinator.SaveAsync(d));Assert.True(d.IsClosed);}
        Assert.True(ra.Token.IsCancellationRequested);Assert.False(rb.Token.IsCancellationRequested);Assert.Single(notices);Assert.Equal("a",Assert.Single(notices[0].RevokedProfileBindings).ProfileId);Assert.Single(Directory.GetFiles(f.Store.DirectoryPath,"secret-txn-*.json"));
        f.Coordinator.Committed-=fault;using var recovered=await f.Coordinator.OpenDraftAsync();Assert.Equal(2,notices.Count);Assert.Equal(notices[0].RevokedProfileBindings.ToArray(),notices[1].RevokedProfileBindings.ToArray());Assert.Empty(Directory.GetFiles(f.Store.DirectoryPath,"secret-txn-*.json"));Assert.True(b.CanApply(rb.Identity));Assert.False(f.Secrets.IsRevoked(rb.Binding.SecretReference!));a.Disable();b.Disable();
    }
    [Fact]public async Task ModelStyleDefaultChangesPreserveExistingProfileBindingAndRequestSnapshot()
    {
        using var f=new Fixture();var snapshot=await f.Seed();var gate=Enabled();using var link=new SettingsRuntimeLink(f.Coordinator,gate);link.Initialize(snapshot);var active=Start(gate,"a");SettingsChange? change=null;f.Coordinator.Committed+=(_,c)=>change=c;
        using(var d=await f.Coordinator.OpenDraftAsync()){d.Update(s=>s with{ActiveProfileId="b",Profiles=s.Profiles.Select(p=>p with{Model="public-next-model"}).ToImmutableArray(),Translation=s.Translation with{Preset=TranslationPreset.Plain}});await f.Coordinator.SaveAsync(d);}
        Assert.False(active.Token.IsCancellationRequested);Assert.True(gate.CanApply(active.Identity));Assert.True(gate.TryGetProfileBinding("a",out var same));Assert.Equal(active.Binding,same);Assert.Empty(change!.RevokedProfileBindings);Assert.Equal("public-model",snapshot.Data.Profiles[0].Model);gate.Disable();
    }
    [Fact]public async Task OffPublicationNeverReadsOldSecretStartsCaptureOrEnablesRuntime()
    {
        using var f=new Fixture();var snapshot=await f.Seed();var gate=new RuntimeGate();using var link=new SettingsRuntimeLink(f.Coordinator,gate);link.Initialize(snapshot);
        using(var d=await f.Coordinator.OpenDraftAsync()){d.ClearKey("a");await f.Coordinator.SaveAsync(d);}Assert.Equal(Activation.Off,gate.Snapshot.Activation);Assert.Equal(0,f.Protector.Opens);Assert.False(gate.TryBeginPassiveCapture(TriggerMode.Selection,out _));Assert.True(gate.TryGetProfileBinding("b",out var binding));Assert.False(gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out _,out _));
    }
    [Fact]public async Task DeletedAndRecreatedSameProfileReferenceCannotReuseOldBindingIdentity()
    {
        using var f=new Fixture();var snapshot=await f.Seed();var gate=Enabled();using var link=new SettingsRuntimeLink(f.Coordinator,gate);link.Initialize(snapshot);var old=Start(gate,"a");
        using(var d=await f.Coordinator.OpenDraftAsync()){Edit(d,"delete");await f.Coordinator.SaveAsync(d);}using(var d=await f.Coordinator.OpenDraftAsync()){d.Update(s=>s with{Profiles=s.Profiles.Add(snapshot.Data.Profiles[0])});await f.Coordinator.SaveAsync(d);}
        Assert.True(old.Token.IsCancellationRequested);Assert.False(gate.TryStartProfileRequest(old.Binding,Guid.NewGuid(),Guid.NewGuid(),null,out _,out _));var next=Start(gate,"a");Assert.NotEqual(old.Binding.BindingId,next.Binding.BindingId);Assert.True(gate.CanApply(next.Identity));gate.Disable();
    }
    [Theory][InlineData(SettingsWriteStage.BeforePublish)][InlineData(SettingsWriteStage.AfterPublish)]
    public async Task RealChildCommitRecoveryPublishesBindingRevocationsOnlyForProvenCommit(SettingsWriteStage stage)
    {
        using var f=new Fixture();var snapshot=await f.Seed();var a=Enabled();var b=Enabled();using var linkA=new SettingsRuntimeLink(f.Coordinator,a);using var linkB=new SettingsRuntimeLink(f.Coordinator,b);linkA.Initialize(snapshot);linkB.Initialize(snapshot);var ra=Start(a,"a");var rb=Start(b,"b");var notices=new List<SettingsChange>();f.Coordinator.Committed+=(_,c)=>notices.Add(c);
        string assembly=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tools/KuaiYi.SettingsStoreFixture/bin",new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name,"net10.0/KuaiYi.SettingsStoreFixture.dll"));var start=new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardInput=true,RedirectStandardError=true};foreach(string arg in new[]{assembly,f.Root,stage.ToString(),"remove-shared"})start.ArgumentList.Add(arg);using var child=Process.Start(start)!;
        try
        {
            Assert.Equal("PUBLIC_STAGE="+stage,await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)));child.Kill();await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));using var recovered=await f.Coordinator.OpenDraftAsync();Assert.Equal(stage==SettingsWriteStage.AfterPublish,ra.Token.IsCancellationRequested);Assert.False(rb.Token.IsCancellationRequested);Assert.True(b.CanApply(rb.Identity));Assert.False(f.Secrets.IsRevoked(rb.Binding.SecretReference!));Assert.Equal(stage==SettingsWriteStage.AfterPublish?1:0,notices.Count);if(notices.Count>0)Assert.Equal("a",Assert.Single(notices[0].RevokedProfileBindings).ProfileId);Assert.Empty(Directory.GetFiles(f.Store.DirectoryPath,"secret-txn-*.json"));
        }
        finally{if(!child.HasExited){child.Kill();await child.WaitForExitAsync();}a.Disable();b.Disable();}
    }
    [Fact]public async Task InvalidForeignAndLocalOnlyBindingAdmissionCannotStartProviderTask()
    {
        using var f=new Fixture();var snapshot=await f.Seed();var gate=Enabled();using var link=new SettingsRuntimeLink(f.Coordinator,gate);link.Initialize(snapshot);
        Assert.False(gate.TryStartProfileRequest(default,Guid.NewGuid(),Guid.NewGuid(),null,out _,out _));Assert.False(gate.TryGetProfileBinding(null!,out _));Assert.True(gate.TryGetProfileBinding("a",out var binding));Assert.False(gate.TryStartProfileRequest(binding with{BindingId=Guid.NewGuid()},Guid.NewGuid(),Guid.NewGuid(),null,out _,out _));
        var other=Enabled();using var foreign=new SettingsRuntimeLink(f.Coordinator,other);foreign.Initialize(snapshot);Assert.False(other.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out _,out _));Assert.True(gate.TryEnableLocalOperation(true));Assert.False(gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out _,out _));gate.Disable();other.Disable();
    }
    [Fact]public async Task UnboundLocalOcrTaskDoesNotUseDeletedApiProfileAndRemainsActive()
    {
        using var f=new Fixture();var snapshot=await f.Seed();var gate=new RuntimeGate();using var link=new SettingsRuntimeLink(f.Coordinator,gate);link.Initialize(snapshot);Assert.True(gate.TryEnableLocalOperation(true));Assert.True(gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var identity,out var token));
        using(var d=await f.Coordinator.OpenDraftAsync()){Edit(d,"delete");await f.Coordinator.SaveAsync(d);}Assert.False(token.IsCancellationRequested);Assert.True(gate.CanApply(identity));Assert.False(gate.CanSend(identity,Origin));gate.Disable();
    }
    [Fact]public async Task DeletingUnauthenticatedProfileStillRevokesItsNullSecretBinding()
    {
        using var f=new Fixture();await f.Seed();SettingsSnapshot snapshot;using(var d=await f.Coordinator.OpenDraftAsync()){d.ClearKey("a");d.Update(s=>s with{Profiles=s.Profiles.Select(p=>p.Id=="a"?p with{Authentication=ApiAuthentication.None}:p).ToImmutableArray()});snapshot=await f.Coordinator.SaveAsync(d);}
        var gate=Enabled();using var link=new SettingsRuntimeLink(f.Coordinator,gate);link.Initialize(snapshot);var request=Start(gate,"a");SettingsChange? change=null;f.Coordinator.Committed+=(_,c)=>change=c;
        using(var d=await f.Coordinator.OpenDraftAsync()){Edit(d,"delete");await f.Coordinator.SaveAsync(d);}Assert.True(request.Token.IsCancellationRequested);Assert.Null(Assert.Single(change!.RevokedProfileBindings).PreviousSecretReference);Assert.False(gate.CanApply(request.Identity));gate.Disable();
    }
    [Fact]public async Task LegacyCommittedJournalInfersBindingsFromExistingEffectiveSnapshotAndKeepsSharedCipher()
    {
        using var f=new Fixture();var snapshot=await f.Seed();var a=Enabled();var b=Enabled();using var linkA=new SettingsRuntimeLink(f.Coordinator,a);using var linkB=new SettingsRuntimeLink(f.Coordinator,b);linkA.Initialize(snapshot);linkB.Initialize(snapshot);var ra=Start(a,"a");var rb=Start(b,"b");SettingsChange? notice=null;f.Coordinator.Committed+=(_,c)=>notice=c;
        Guid id=Guid.NewGuid();await f.Store.SaveAsync(snapshot,snapshot.Data with{WriteId=id,Profiles=snapshot.Data.Profiles.Where(p=>p.Id!="a").ToImmutableArray(),ActiveProfileId="b"});string journal=Path.Combine(f.Store.DirectoryPath,"secret-txn-"+id.ToString("N")+".json");await File.WriteAllTextAsync(journal,"{\"schemaVersion\":1,\"id\":\""+id+"\",\"basisVersion\":\"public-legacy-basis\",\"newReferences\":[],\"retiredReferences\":[\""+ra.Binding.SecretReference+"\"]}");
        using var recovered=await f.Coordinator.OpenDraftAsync();Assert.True(ra.Token.IsCancellationRequested);Assert.False(rb.Token.IsCancellationRequested);Assert.Empty(notice!.DeletedSecrets);Assert.Equal("a",Assert.Single(notice.RevokedProfileBindings).ProfileId);Assert.False(f.Secrets.IsRevoked(rb.Binding.SecretReference!));a.Disable();b.Disable();
    }
    [Fact]public async Task FutureJournalSchemaIsPreservedWithoutNotificationOrSecretRetirement()
    {
        using var f=new Fixture();var snapshot=await f.Seed();var gate=Enabled();using var link=new SettingsRuntimeLink(f.Coordinator,gate);link.Initialize(snapshot);var active=Start(gate,"a");int notices=0;f.Coordinator.Committed+=(_,_)=>notices++;
        string path=Path.Combine(f.Store.DirectoryPath,"secret-txn-"+snapshot.Data.WriteId.ToString("N")+".json");string text="{\"schemaVersion\":3,\"id\":\""+snapshot.Data.WriteId+"\",\"basisVersion\":\"public-preserve-future\",\"newReferences\":[],\"retiredReferences\":[\""+active.Binding.SecretReference+"\"]}";await File.WriteAllTextAsync(path,text);await Assert.ThrowsAsync<SettingsTransactionUnconfirmedException>(()=>f.Coordinator.OpenDraftAsync());Assert.Equal(text,await File.ReadAllTextAsync(path));Assert.Equal(0,notices);Assert.True(gate.CanApply(active.Identity));Assert.False(active.Token.IsCancellationRequested);Assert.False(f.Secrets.IsRevoked(active.Binding.SecretReference!));gate.Disable();
    }
    [Fact]public async Task LinkedOriginalAsyncOperationAActuallyCancelsWhileBRemainsPendingUntilOwnStop()
    {
        using var f=new Fixture();var snapshot=await f.Seed();var a=Enabled();var b=Enabled();using var linkA=new SettingsRuntimeLink(f.Coordinator,a);using var linkB=new SettingsRuntimeLink(f.Coordinator,b);linkA.Initialize(snapshot);linkB.Initialize(snapshot);var ra=Start(a,"a");var rb=Start(b,"b");Assert.True(a.TryLinkRequestCancellation(ra.Identity,CancellationToken.None,out var cancellationA));Assert.True(b.TryLinkRequestCancellation(rb.Identity,CancellationToken.None,out var cancellationB));using var ownedA=cancellationA!;using var ownedB=cancellationB!;
        var originalA=Task.Delay(Timeout.InfiniteTimeSpan,ownedA.Token);var originalB=Task.Delay(Timeout.InfiniteTimeSpan,ownedB.Token);
        try{using var draft=await f.Coordinator.OpenDraftAsync();draft.ClearKey("a");await f.Coordinator.SaveAsync(draft);await Assert.ThrowsAsync<TaskCanceledException>(()=>originalA.WaitAsync(TimeSpan.FromSeconds(3)));Assert.False(originalB.IsCompleted);Assert.True(b.CanApply(rb.Identity));}
        finally{a.Disable();b.Disable();await Assert.ThrowsAsync<TaskCanceledException>(()=>originalB);}
    }
    [Fact]public async Task LateInitializationCannotRestoreOldReferenceOrCancelNewerBindingTask()
    {
        using var f=new Fixture();var oldSnapshot=await f.Seed();var gate=Enabled();using var link=new SettingsRuntimeLink(f.Coordinator,gate);link.Initialize(oldSnapshot);
        using(var draft=await f.Coordinator.OpenDraftAsync()){Edit(draft,"replace");await f.Coordinator.SaveAsync(draft);}var newer=Start(gate,"a");
        try{link.Initialize(oldSnapshot);Assert.False(newer.Token.IsCancellationRequested);Assert.True(gate.CanApply(newer.Identity));Assert.True(gate.TryGetProfileBinding("a",out var current));Assert.Equal(newer.Binding,current);Assert.NotEqual(oldSnapshot.Data.Profiles[0].SecretRef,current.SecretReference);}
        finally{gate.Disable();}
    }
}
