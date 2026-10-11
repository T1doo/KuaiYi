using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed class SettingsRecoveryRegressionTests
{
    private static readonly string[] InvalidSchemaValues=["\"1\"","null","{}","[]","true","false","1.5","2147483648"];
    public static IEnumerable<object[]> SchemaValues()=>InvalidSchemaValues.Select(v=>new object[]{v});
    public static IEnumerable<object[]> SchemaFileMatrix()=>InvalidSchemaValues.SelectMany(v=>Enumerable.Range(0,3).Select(combination=>new object[]{v,combination}));
    public static IEnumerable<object[]> RecoveryRoundTrips()=>new[]{false,true}.SelectMany(defaults=>new[]{false,true}.SelectMany(save=>new[]{false,true}.Select(none=>new object[]{defaults,save,none})));
    private sealed class DirectoryPolicy:ISecretDirectoryPolicy{public void EnsurePrivate(string directory)=>Directory.CreateDirectory(directory);}
    private sealed class SyntheticProtector:ISecretProtector
    {
        private static readonly byte[] Key=Enumerable.Repeat((byte)19,32).ToArray();
        public int Opens;
        public byte[] Protect(ReadOnlySpan<byte> plain)
        {byte[] result=new byte[plain.Length+28];RandomNumberGenerator.Fill(result.AsSpan(0,12));using var aes=new AesGcm(Key,16);aes.Encrypt(result.AsSpan(0,12),plain,result.AsSpan(28),result.AsSpan(12,16));return result;}
        public byte[] Unprotect(ReadOnlySpan<byte> input)
        {Opens++;byte[] result=new byte[input.Length-28];using var aes=new AesGcm(Key,16);aes.Decrypt(input[..12],input[28..],input.Slice(12,16),result);return result;}
    }
    private sealed class Fixture:IDisposable
    {
        public string Root{get;}=Path.Combine(Path.GetTempPath(),"kuaiyi-settings-tests-"+Guid.NewGuid().ToString("N"));
        public SettingsStore Store{get;}public SecretFileStore Secrets{get;}public SyntheticProtector Protector{get;}=new();public SettingsCoordinator Coordinator{get;}
        public Fixture(){Store=new(Path.Combine(Root,"config"));Secrets=new(Path.Combine(Root,"secrets"),Protector,new DirectoryPolicy());Coordinator=new(Store,Secrets);}
        public async Task<SettingsSnapshot> Seed()
        {
            using var draft=await Coordinator.OpenDraftAsync();draft.Update(s=>s with{Profiles=[Profile("a")],ActiveProfileId="a",Translation=s.Translation with{Domain="public-backup"},Overlay=s.Overlay with{FontSizeDip=17}});draft.SetKey("a","public-SYNTHETIC-shared-KEY"u8);return await Coordinator.SaveAsync(draft);
        }
        public async Task<SettingsSnapshot> Shared()
        {var old=await Seed();using var draft=await Coordinator.OpenDraftAsync();draft.Update(s=>s with{Profiles=s.Profiles.Add(Profile("b") with{SecretRef=old.Data.Profiles[0].SecretRef})});return await Coordinator.SaveAsync(draft);}
        public async Task DamagePrimaryWithBackup()
        {await Seed();using(var draft=await Coordinator.OpenDraftAsync()){draft.Update(s=>s with{Overlay=s.Overlay with{FontSizeDip=18}});await Coordinator.SaveAsync(draft);}await File.WriteAllTextAsync(Store.PrimaryPath,"public-broken-primary");}
        public Dictionary<string,byte[]> Files()=>Directory.GetFiles(Root,"*",SearchOption.AllDirectories).ToDictionary(p=>Path.GetRelativePath(Root,p),File.ReadAllBytes);
        public void AssertFiles(Dictionary<string,byte[]> before){var after=Files();Assert.Equal(before.Keys.Order(),after.Keys.Order());foreach(var pair in before)Assert.Equal(pair.Value,after[pair.Key]);}
        public void Dispose(){if(Directory.Exists(Root))Directory.Delete(Root,true);}
    }
    private static ApiProfile Profile(string id)=>new(){Id=id,Name="公开合成配置 "+id,BaseUrl="https://public.invalid/v1",Model="public-model"};
    private static string InvalidDocument(string value)=>"{\"schemaVersion\":"+value+"}";

    [Theory][MemberData(nameof(SchemaValues))]
    public void NonIntegerSchemaIsClassifiedAsDamage(string value)
    {var error=Assert.Throws<SettingsValidationException>(()=>SettingsJson.Read(Encoding.UTF8.GetBytes(InvalidDocument(value)),out _));Assert.Equal("schemaVersion",error.Field);}

    [Theory][MemberData(nameof(SchemaFileMatrix))]
    public async Task NonIntegerSchemaMainAndBackupCombinationsPreserveBytesUntilExplicitSave(string value,int combination)
    {
        using var f=new Fixture();await f.Seed();byte[] valid=await File.ReadAllBytesAsync(f.Store.PrimaryPath);byte[] invalid=Encoding.UTF8.GetBytes(InvalidDocument(value));
        await File.WriteAllBytesAsync(f.Store.BackupPath,combination==0?valid:invalid);
        await File.WriteAllBytesAsync(f.Store.PrimaryPath,combination==2?valid:invalid);
        var before=f.Files();
        using(var cancelled=new SettingsRecoveryDraft(await f.Coordinator.OpenDraftAsync()))
        {
            Assert.Equal(combination==0?SettingsLoadState.RecoverableBackup:combination==1?SettingsLoadState.RecoverableDefaults:SettingsLoadState.Current,cancelled.Draft.Basis.State);
            f.AssertFiles(before);
        }
        f.AssertFiles(before);Assert.Equal(0,f.Protector.Opens);
        using var recovery=new SettingsRecoveryDraft(await f.Coordinator.OpenDraftAsync());
        if(combination!=2)
        {
            await Assert.ThrowsAsync<SettingsRecoveryRequiredException>(()=>recovery.SaveAsync(f.Coordinator));f.AssertFiles(before);
            Assert.True(recovery.Select(combination==0?SettingsRecovery.UseBackup:SettingsRecovery.UseDefaults));
        }
        await recovery.SaveAsync(f.Coordinator);
        SettingsJson.Read(await File.ReadAllBytesAsync(f.Store.PrimaryPath),out _);
        Assert.Equal(combination==2?valid:before[Path.GetRelativePath(f.Root,f.Store.BackupPath)],await File.ReadAllBytesAsync(f.Store.BackupPath));
        if(combination!=2)Assert.Equal(invalid,await File.ReadAllBytesAsync(Assert.Single(Directory.GetFiles(f.Store.DirectoryPath,"settings.corrupt-*.json"))));
        else Assert.Equal(invalid,await File.ReadAllBytesAsync(Assert.Single(Directory.GetFiles(f.Store.DirectoryPath,"settings.corrupt-backup-*.json"))));
    }

    [Theory][InlineData(2)][InlineData(999)]
    public async Task FutureIntegerBackupStillPreservedWhenPrimarySchemaHasWrongType(int future)
    {
        using var f=new Fixture();await f.Seed();await File.WriteAllTextAsync(f.Store.PrimaryPath,InvalidDocument("null"));await File.WriteAllTextAsync(f.Store.BackupPath,"{\"schemaVersion\":"+future+"}");var before=f.Files();
        using var draft=await f.Coordinator.OpenDraftAsync();Assert.True(draft.Basis.ReadOnly);await Assert.ThrowsAsync<UnknownSettingsSchemaException>(()=>f.Coordinator.SaveAsync(draft));f.AssertFiles(before);
    }

    [Theory][MemberData(nameof(RecoveryRoundTrips))]
    public async Task RecoveryRoundTripRetainsEachDisplayedDraftAndPendingKeysAndSavesOnlyChosenDraft(bool useDefaults,bool save,bool throughNone)
    {
        using var f=new Fixture();await f.DamagePrimaryWithBackup();var before=f.Files();
        using var recovery=new SettingsRecoveryDraft(await f.Coordinator.OpenDraftAsync());
        Assert.True(recovery.Select(SettingsRecovery.UseBackup));Assert.Equal(17,recovery.Draft.Data.Overlay.FontSizeDip);Assert.Equal("public-backup",recovery.Draft.Data.Translation.Domain);
        recovery.Draft.Update(s=>s with{Translation=s.Translation with{Domain="public-backup-edited"}});recovery.Draft.SetKey("a","public-SYNTHETIC-backup-draft-KEY"u8);byte[] backupKey=recovery.Draft.PendingKeyForTest("a")!;
        Assert.True(recovery.Select(SettingsRecovery.UseDefaults));Assert.Empty(recovery.Draft.Data.Profiles);Assert.Equal(15,recovery.Draft.Data.Overlay.FontSizeDip);
        recovery.Draft.Update(s=>s with{Profiles=[Profile("defaults")],ActiveProfileId="defaults",Translation=s.Translation with{Domain="public-defaults-edited"}});recovery.Draft.SetKey("defaults","public-SYNTHETIC-defaults-draft-KEY"u8);byte[] defaultsKey=recovery.Draft.PendingKeyForTest("defaults")!;
        Assert.True(recovery.Select(SettingsRecovery.UseBackup));Assert.Equal("public-backup-edited",recovery.Draft.Data.Translation.Domain);Assert.Equal("a",Assert.Single(recovery.Draft.Data.Profiles).Id);Assert.Same(backupKey,recovery.Draft.PendingKeyForTest("a"));
        if(throughNone){Assert.True(recovery.Select(SettingsRecovery.None));Assert.Equal("public-backup-edited",recovery.Draft.Data.Translation.Domain);await Assert.ThrowsAsync<SettingsRecoveryRequiredException>(()=>recovery.SaveAsync(f.Coordinator));f.AssertFiles(before);}
        Assert.True(recovery.Select(useDefaults?SettingsRecovery.UseDefaults:SettingsRecovery.UseBackup));
        string selectedDomain=useDefaults?"public-defaults-edited":"public-backup-edited";Assert.Equal(selectedDomain,recovery.Draft.Data.Translation.Domain);
        if(save)
        {
            var written=await recovery.SaveAsync(f.Coordinator);Assert.Equal(selectedDomain,written.Data.Translation.Domain);Assert.Equal(useDefaults?"defaults":"a",Assert.Single(written.Data.Profiles).Id);
            Assert.Equal(selectedDomain,SettingsJson.Read(await File.ReadAllBytesAsync(f.Store.PrimaryPath),out _).Translation.Domain);
            Assert.Equal(before[Path.GetRelativePath(f.Root,f.Store.BackupPath)],await File.ReadAllBytesAsync(f.Store.BackupPath));
        }
        else f.AssertFiles(before);
        recovery.Dispose();Assert.All(backupKey,b=>Assert.Equal(0,b));Assert.All(defaultsKey,b=>Assert.Equal(0,b));Assert.Equal(0,f.Protector.Opens);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task BothDamagedFilesNeverAllowBackupChoiceAndDefaultsCancelOrSavePreservesOriginals(bool save)
    {
        using var f=new Fixture();await f.Seed();await File.WriteAllTextAsync(f.Store.PrimaryPath,"public-primary-damage");await File.WriteAllTextAsync(f.Store.BackupPath,"public-backup-damage");var before=f.Files();
        using var recovery=new SettingsRecoveryDraft(await f.Coordinator.OpenDraftAsync());Assert.True(recovery.Select(SettingsRecovery.UseDefaults));recovery.Draft.Update(s=>s with{Translation=s.Translation with{Domain="public-defaults-edited"}});
        Assert.Throws<SettingsRecoveryRequiredException>(()=>recovery.Select(SettingsRecovery.UseBackup));Assert.Equal(SettingsRecovery.UseDefaults,recovery.Choice);Assert.Equal("public-defaults-edited",recovery.Draft.Data.Translation.Domain);
        if(save){await recovery.SaveAsync(f.Coordinator);Assert.Equal("public-backup-damage",await File.ReadAllTextAsync(f.Store.BackupPath));Assert.Equal("public-primary-damage",await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(f.Store.DirectoryPath,"settings.corrupt-*.json"))));}
        else f.AssertFiles(before);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task SavingOriginalTaskRejectsRecoveryChangesAndLateEventsAndFailureRestoresEditing(bool fail)
    {
        using var f=new Fixture();await f.DamagePrimaryWithBackup();var before=f.Files();
        var reached=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store=new SettingsStore(f.Store.DirectoryPath){WriteStageForTest=stage=>{if(stage==SettingsWriteStage.BeforePublish){reached.SetResult();release.Task.GetAwaiter().GetResult();if(fail)throw new IOException("public write failure");}}};var coordinator=new SettingsCoordinator(store,f.Secrets);
        using var recovery=new SettingsRecoveryDraft(await coordinator.OpenDraftAsync());Assert.True(recovery.Select(SettingsRecovery.UseBackup));var shown=recovery.Draft;
        var saving=recovery.SaveAsync(coordinator);try{await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.True(recovery.IsSaving);Assert.False(recovery.Select(SettingsRecovery.UseDefaults));Assert.Same(shown,recovery.Draft);Assert.Equal(SettingsRecovery.UseBackup,recovery.Choice);Assert.Throws<InvalidOperationException>(()=>recovery.Dispose());}finally{release.TrySetResult();}
        if(fail){await Assert.ThrowsAsync<IOException>(()=>saving);Assert.False(recovery.IsSaving);f.AssertFiles(before);Assert.True(recovery.Select(SettingsRecovery.UseDefaults));Assert.Empty(recovery.Draft.Data.Profiles);}
        else{await saving;Assert.False(recovery.IsSaving);Assert.False(recovery.Select(SettingsRecovery.UseDefaults));Assert.Same(shown,recovery.Draft);Assert.Equal(SettingsRecovery.UseBackup,recovery.Choice);}
    }

    [Fact]public async Task RemovingOneOfTwoSharedReferencesKeepsRemainingReadableUntilFinalRemoval()
    {
        using var f=new Fixture();var shared=await f.Shared();string reference=shared.Data.Profiles[0].SecretRef!;SettingsChange? change=null;f.Coordinator.Committed+=(_,c)=>change=c;
        using(var draft=await f.Coordinator.OpenDraftAsync()){draft.Update(s=>s with{Profiles=s.Profiles.Where(p=>p.Id!="a").ToImmutableArray(),ActiveProfileId="b"});await f.Coordinator.SaveAsync(draft);}
        Assert.False(f.Secrets.IsRevoked(reference));Assert.Empty(change!.DeletedSecrets);Assert.Contains("a",change.DeletedProfiles);using(var key=await f.Secrets.OpenAsync(reference))Assert.True(key.Bytes.Span.SequenceEqual("public-SYNTHETIC-shared-KEY"u8));
        using(var draft=await f.Coordinator.OpenDraftAsync()){draft.ClearKey("b");await f.Coordinator.SaveAsync(draft);}
        Assert.True(f.Secrets.IsRevoked(reference));Assert.Equal(reference,Assert.Single(change!.DeletedSecrets));await Assert.ThrowsAsync<CryptographicException>(()=>f.Secrets.OpenAsync(reference));
    }
    [Fact]public async Task SharedReferenceSaveFailureAndStaleConcurrentDraftCannotRevokeRemainingSecret()
    {
        using var f=new Fixture();var shared=await f.Shared();string reference=shared.Data.Profiles[0].SecretRef!;var before=f.Files();
        var store=new SettingsStore(f.Store.DirectoryPath){WriteStageForTest=stage=>{if(stage==SettingsWriteStage.BeforePublish)throw new IOException("public pre-publication failure");}};var coordinator=new SettingsCoordinator(store,f.Secrets);
        using(var draft=await coordinator.OpenDraftAsync()){draft.Update(s=>s with{Profiles=s.Profiles.Where(p=>p.Id!="a").ToImmutableArray(),ActiveProfileId="b"});await Assert.ThrowsAsync<IOException>(()=>coordinator.SaveAsync(draft));}
        f.AssertFiles(before);Assert.False(f.Secrets.IsRevoked(reference));
        using var stale=await coordinator.OpenDraftAsync();using(var winner=await f.Coordinator.OpenDraftAsync()){winner.Update(s=>s with{Profiles=s.Profiles.Where(p=>p.Id!="a").ToImmutableArray(),ActiveProfileId="b"});await f.Coordinator.SaveAsync(winner);}
        stale.ClearKey("a");stale.ClearKey("b");await Assert.ThrowsAsync<SettingsConflictException>(()=>coordinator.SaveAsync(stale));Assert.False(f.Secrets.IsRevoked(reference));using var key=await f.Secrets.OpenAsync(reference);Assert.True(key.Bytes.Span.SequenceEqual("public-SYNTHETIC-shared-KEY"u8));
    }
    [Theory][InlineData(SettingsWriteStage.BeforePublish)][InlineData(SettingsWriteStage.AfterPublish)]
    public async Task SharedReferenceSurvivesRealChildCrashAndRecoveryWhileOtherProfileStillUsesIt(SettingsWriteStage stage)
    {
        using var f=new Fixture();var shared=await f.Shared();string reference=shared.Data.Profiles[0].SecretRef!;
        string assembly=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tools/KuaiYi.SettingsStoreFixture/bin",new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name,"net10.0/KuaiYi.SettingsStoreFixture.dll"));var start=new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardInput=true,RedirectStandardError=true};foreach(string arg in new[]{assembly,f.Root,stage.ToString(),"remove-shared"})start.ArgumentList.Add(arg);using var child=Process.Start(start)!;
        try
        {
            Assert.Equal("PUBLIC_STAGE="+stage,await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)));child.Kill();await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));using var draft=await f.Coordinator.OpenDraftAsync();Assert.Equal(stage==SettingsWriteStage.AfterPublish?1:2,draft.Data.Profiles.Length);Assert.False(f.Secrets.IsRevoked(reference));Assert.Empty(Directory.GetFiles(f.Store.DirectoryPath,"secret-txn-*.json"));using var key=await f.Secrets.OpenAsync(reference);Assert.True(key.Bytes.Span.SequenceEqual("public-SYNTHETIC-shared-KEY"u8));
        }
        finally{if(!child.HasExited){child.Kill();await child.WaitForExitAsync();}}
    }
}
