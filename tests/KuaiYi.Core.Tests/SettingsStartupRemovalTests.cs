using KuaiYi.Core.Settings;
using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed class SettingsStartupRemovalTests
{
    private sealed class DirectoryPolicy:ISecretDirectoryPolicy{public void EnsurePrivate(string path)=>Directory.CreateDirectory(path);}
    private sealed class NoSecret:ISecretProtector
    {public byte[] Protect(ReadOnlySpan<byte> value)=>throw new InvalidOperationException();public byte[] Unprotect(ReadOnlySpan<byte> value)=>throw new InvalidOperationException();}
    private sealed class Fixture:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"ky-settings-startup-"+Guid.NewGuid().ToString("N"));
        internal SettingsStore Store;
        internal readonly SecretFileStore Secrets;
        internal SettingsCoordinator Coordinator;
        internal Fixture(Action<SettingsWriteStage>? fault=null)
        {Store=new(Path.Combine(Root,"config")){WriteStageForTest=fault};Secrets=new(Path.Combine(Root,"secrets"),new NoSecret(),new DirectoryPolicy());Coordinator=new(Store,Secrets);}
        public void Dispose(){if(Directory.Exists(Root))Directory.Delete(Root,true);}
    }
    // Synthetic participant only. The separate original-stdio cases exercise the sealed Installer capability.
    private sealed class Participant:ISettingsStartupRemovalParticipant,ISettingsStartupRemoval
    {
        public Guid Request{get;}=Guid.NewGuid();
        public bool SaveStarted{get;private set;}
        internal Func<Task>? Reserve,Apply,Commit,Abort;
        internal int Applies,Commits,Aborts;
        public void SettingsSaveStarted()=>SaveStarted=true;
        public void BindIntent(string directory,SettingsStartupIntent intent){Assert.Equal(Request,intent.Request);}
        public Task LocalSettledAsync(string outcome,string configVersion,bool cleanupConfirmed)=>Task.CompletedTask;
        public async Task<ISettingsStartupRemoval> ReserveAsync(string basis,CancellationToken cancellation){cancellation.ThrowIfCancellationRequested();if(Reserve!=null)await Reserve();return this;}
        public async Task ApplyAsync(CancellationToken cancellation){Applies++;cancellation.ThrowIfCancellationRequested();if(Apply!=null)await Apply();}
        public async Task CommitAsync(SettingsSnapshot saved){Commits++;if(Commit!=null)await Commit();}
        public async Task AbortAsync(){Aborts++;if(Abort!=null)await Abort();}
    }
    [Fact]public async Task ReservationPrecedesSettingsSerialAndTransactionFileLock()
    {
        using var f=new Fixture();using var draft=await f.Coordinator.OpenDraftAsync();draft.Update(s=>s with{Overlay=s.Overlay with{FontSizeDip=19}});
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var p=new Participant{Reserve=async()=>{entered.SetResult();await release.Task;}};
        var original=f.Coordinator.SaveWithStartupRemovalAsync(draft,p);Task<SettingsDraft>? concurrent=null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            using(var lease=new FileStream(Path.Combine(f.Store.DirectoryPath,"secret-transaction.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))Assert.Equal(0,lease.Length);
            concurrent=f.Coordinator.OpenDraftAsync();using var second=await concurrent.WaitAsync(TimeSpan.FromSeconds(3));Assert.False(original.IsCompleted);
        }
        finally{release.TrySetResult();await original;if(concurrent!=null)(await concurrent).Dispose();}
        Assert.Equal(1,p.Applies);Assert.Equal(1,p.Commits);Assert.Equal(0,p.Aborts);
    }
    [Fact]public async Task ApplyAndAbortOriginalErrorsPreservedAndV3NeverDeletedByOldRecovery()
    {
        using var f=new Fixture();using var draft=await f.Coordinator.OpenDraftAsync();var apply=new IOException("PUBLIC_APPLY");var abort=new IOException("PUBLIC_ABORT");
        var p=new Participant{Apply=()=>Task.FromException(apply),Abort=()=>Task.FromException(abort)};
        var error=await Assert.ThrowsAsync<AggregateException>(()=>f.Coordinator.SaveWithStartupRemovalAsync(draft,p));Assert.Contains(apply,error.InnerExceptions);Assert.Contains(abort,error.InnerExceptions);
        string path=Assert.Single(Directory.GetFiles(f.Store.DirectoryPath,"secret-txn-*.json"));byte[] bytes=File.ReadAllBytes(path);
        await Assert.ThrowsAsync<SettingsTransactionUnconfirmedException>(()=>f.Coordinator.OpenDraftAsync());Assert.Equal(bytes,File.ReadAllBytes(path));Assert.False(draft.IsClosed);Assert.Equal(1,p.Aborts);
    }
    [Fact]public async Task PublishedParticipantFailureCannotSkipRevocationOrPretendRollback()
    {
        using var f=new Fixture();using var draft=await f.Coordinator.OpenDraftAsync();draft.Update(s=>s with{Overlay=s.Overlay with{FontSizeDip=19}});
        var first=new IOException("PUBLIC_COMMIT");var second=new IOException("PUBLIC_NOTIFY");int notified=0;
        f.Coordinator.Committed+=(_,_)=>throw second;f.Coordinator.Committed+=(_,_)=>notified++;
        var p=new Participant{Commit=()=>Task.FromException(first)};
        var error=await Assert.ThrowsAsync<AggregateException>(()=>f.Coordinator.SaveWithStartupRemovalAsync(draft,p));Assert.Contains(first,error.InnerExceptions);Assert.Contains(second,error.InnerExceptions);
        Assert.True(draft.IsClosed);Assert.Equal(19,(await f.Store.LoadAsync()).Data.Overlay.FontSizeDip);Assert.Equal(19,f.Coordinator.Effective!.Data.Overlay.FontSizeDip);Assert.Equal(1,notified);Assert.Equal(0,p.Aborts);Assert.Single(Directory.GetFiles(f.Store.DirectoryPath,"secret-txn-*.json"));
    }
    [Fact]public async Task UnknownConfigurationPublicationCannotAuthorizeRemoteAbort()
    {
        using var f=new Fixture();using var draft=await f.Coordinator.OpenDraftAsync();
        f.Store=new(f.Store.DirectoryPath){WriteStageForTest=stage=>{if(stage==SettingsWriteStage.AfterPublish){var data=SettingsJson.Read(File.ReadAllBytes(f.Store.PrimaryPath),out _) with{WriteId=Guid.NewGuid()};File.WriteAllBytes(f.Store.PrimaryPath,SettingsJson.Write(data));throw new IOException("PUBLIC_UNKNOWN_CONFIG");}}};
        f.Coordinator=new(f.Store,f.Secrets);var p=new Participant();
        await Assert.ThrowsAsync<SettingsTransactionUnconfirmedException>(()=>f.Coordinator.SaveWithStartupRemovalAsync(draft,p));Assert.Equal(1,p.Applies);Assert.Equal(0,p.Aborts);Assert.Equal(0,p.Commits);Assert.Single(Directory.GetFiles(f.Store.DirectoryPath,"secret-txn-*.json"));
    }
    [Fact]public async Task TrueRemainsRejectedBeforeAnyReservation()
    {
        using var f=new Fixture();using var draft=await f.Coordinator.OpenDraftAsync();draft.Update(s=>s with{LaunchAtSignIn=true});bool reserved=false;var p=new Participant{Reserve=()=>{reserved=true;return Task.CompletedTask;}};
        await Assert.ThrowsAsync<SettingsValidationException>(()=>f.Coordinator.SaveWithStartupRemovalAsync(draft,p));Assert.False(reserved);Assert.Equal(0,p.Applies);Assert.False(File.Exists(f.Store.PrimaryPath));
    }
    [Fact]public void PartialV3IntentAndOriginalCloseErrorAreRetainedTogether()
    {
        string root=Path.Combine(Path.GetTempPath(),"ky-startup-journal-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string path=Path.Combine(root,"secret-txn-"+Guid.NewGuid().ToString("N")+".json");
        var write=new IOException("PUBLIC_STARTUP_JOURNAL_PARTIAL");var close=new IOException("PUBLIC_STARTUP_JOURNAL_CLOSE");
        var journal=new SettingsStartupJournal(root,path,"{}"u8.ToArray(),new(){WriteMetadata=(_,file,bytes)=>{file.Write(bytes.Span[..1]);file.Flush(true);throw write;},BeforeClose=(_,_)=>throw close});
        var error=Assert.Throws<AggregateException>(journal.Write);Assert.Contains(write,error.InnerExceptions);Assert.Contains(close,error.InnerExceptions);
        Assert.Equal(1,new FileInfo(path).Length);Assert.Throws<SettingsTransactionUnconfirmedException>(journal.RequireSettledIntent);Assert.True(File.Exists(path));
        // Original handle/partial intent deliberately retained, not deleted or declared closed by a test cleanup.
    }
    [Fact]public async Task StartupSavePreservesOriginalDiskFailureWhenUnpublishedSecretCleanupAlsoFails()
    {
        using var f=new Fixture();string secretRoot=Path.Combine(f.Root,"secrets");var primary=new IOException("PUBLIC_SETTINGS_PRIMARY");
        string? staged=null;Directory.CreateDirectory(f.Root);string foreign=Path.Combine(f.Root,"PUBLIC_FOREIGN_SECRET_TARGET");File.WriteAllText(foreign,"PUBLIC_SYNTHETIC_FOREIGN");
        var store=new SettingsStore(f.Store.DirectoryPath){WriteStageForTest=stage=>
        {
            if(stage!=SettingsWriteStage.BeforePublish)return;
            staged=Assert.Single(Directory.GetFiles(secretRoot,"*.bin"));File.Delete(staged);File.CreateSymbolicLink(staged,foreign);throw primary;
        }};
        var coordinator=new SettingsCoordinator(store,new(secretRoot,new HashOnlyProtector(),new DirectoryPolicy()));using var draft=await coordinator.OpenDraftAsync();
        draft.Update(s=>s with{Profiles=[new(){Id="public",Name="公开合成配置",BaseUrl="https://public.invalid/v1",Model="public-test-model"}],ActiveProfileId="public"});draft.SetKey("public","PUBLIC_SYNTHETIC_UNPUBLISHED_KEY"u8);
        var p=new Participant();var error=await Assert.ThrowsAsync<AggregateException>(()=>coordinator.SaveWithStartupRemovalAsync(draft,p));
        Assert.Contains(primary,error.InnerExceptions);Assert.Equal(2,error.InnerExceptions.Count);Assert.Equal(1,p.Aborts);Assert.Equal(0,p.Commits);
        Assert.Single(Directory.GetFiles(store.DirectoryPath,"secret-txn-*.json"));Assert.NotNull(new FileInfo(staged!).LinkTarget);Assert.Equal("PUBLIC_SYNTHETIC_FOREIGN",File.ReadAllText(foreign));
    }
    private sealed class HashOnlyProtector:ISecretProtector
    {public byte[] Protect(ReadOnlySpan<byte> data)=>System.Security.Cryptography.SHA256.HashData(data);public byte[] Unprotect(ReadOnlySpan<byte> data)=>throw new InvalidOperationException("This synthetic cleanup test never opens a key.");}

    [Theory][InlineData("COMMIT")][InlineData("ABORT")]
    public async Task MissingFinalReceiptReturnsUnconfirmedWithoutAbandoningOriginalLocalIo(string missing)
    {
        using var f=new Fixture();var primary=new IOException("PUBLIC_BEFORE_PUBLISH");
        var store=new SettingsStore(f.Store.DirectoryPath){WriteStageForTest=stage=>{if(missing=="ABORT"&&stage==SettingsWriteStage.BeforePublish)throw primary;}};
        var coordinator=new SettingsCoordinator(store,f.Secrets);
        // Synthetic peer only: no package qualification is claimed. Real original-process paths are tested separately.
        var proof=new InstallationControlledLaunch(Guid.NewGuid(),"KuaiYi.App.exe",Path.Combine(f.Root,"application"),f.Root,
            new("public",Path.Combine(f.Root,"application","versions",Guid.NewGuid().ToString("N")),new string('0',64)),Convert.ToBase64String("PUBLIC"u8),Convert.ToBase64String("PUBLIC"u8),null);
        InstallationStartupRemovalClient? client=null;
        client=new(proof,(line,_)=>
        {
            var row=InstallationStartupRemovalWire.Decode(line,proof);
            if(row.Verb!=missing)client!.Receive(InstallationStartupRemovalWire.Encode(proof,row.Request,row.Verb+"_OK"));
            return Task.CompletedTask; // Final response is lost; original receipt task remains pending after observation expires.
        });
        using var draft=await coordinator.OpenDraftAsync();draft.Update(s=>s with{Overlay=s.Overlay with{FontSizeDip=19}});
        var error=await Assert.ThrowsAsync<AggregateException>(()=>coordinator.SaveWithStartupRemovalAsync(draft,client));
        Assert.Contains(error.Flatten().InnerExceptions,e=>e is TimeoutException);if(missing=="ABORT")Assert.Contains(primary,error.Flatten().InnerExceptions);
        Assert.Equal(missing=="COMMIT"?19:15,(await store.LoadAsync()).Data.Overlay.FontSizeDip);Assert.Single(Directory.GetFiles(store.DirectoryPath,"secret-txn-*.json"));Assert.False(client.OriginalsSettled);
        var eof=new IOException("PUBLIC_ORIGINAL_CHANNEL_ENDED");client.Disconnect(eof);Assert.Same(eof,await Assert.ThrowsAsync<IOException>(()=>client.JoinAsync()));Assert.True(client.OriginalsSettled);
    }
}
