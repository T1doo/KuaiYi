using KuaiYi.Core.Installation;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed partial class InstallationDeploymentSessionTests
{
    private static SettingsStartupIntent WriteSyntheticStartupIntent(string data,Guid request)
    {
        string directory=Path.Combine(data,"config");Directory.CreateDirectory(directory);Guid id=Guid.NewGuid();
        byte[] bytes=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=3,startupRequest=request,id,basisVersion="absent:absent",newReferences=Array.Empty<string>(),retiredReferences=Array.Empty<string>(),deletedProfiles=Array.Empty<string>(),revokedBindings=Array.Empty<RevokedProfileBinding>(),clipboardPermissionsChanged=false},SettingsJson.Options);
        SettingsStore.WriteFlushed(Path.Combine(directory,"secret-txn-"+id.ToString("N")+".json"),bytes);
        return new(request,id,"absent:absent",Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
    }
    private static async Task BeginStartupSettings(Rig rig,string mode)
    {
        await rig.Peer!.Send("STARTUP_SAVE|"+mode);Assert.Equal("PUBLIC_STARTUP_STARTED",await rig.Peer.ReadPublic());
    }
    private static async Task<string?> StartupSettingsResult(Rig rig)
    {await rig.Peer!.Send("WAIT_STARTUP_SAVE");return await rig.Peer.ReadPublic();}

    [Theory][InlineData("normal")][InlineData("after-fail")]
    public Task StartupSettingsOriginalStdioPublishesWholeConfigAndRemovesOnlyItsOwnedNamedValue(string mode)=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();Assert.Equal(InstallationOperationStatus.NewVersionReady,(await entry.Start().WaitAsync(Limit)).Status);
        Assert.NotNull(r.Port.StartupServer);await BeginStartupSettings(r,mode);
        Assert.Equal("PUBLIC_STARTUP_SAVE|PublishedAwaitingOriginalControllerSettlement|19|True|1|1",await StartupSettingsResult(r));
        Assert.Equal(new InstallationStartupValue(false),r.Startup.Value);Assert.False(File.Exists(InstallationStartupRegistration.OwnerPath(r.App)));
        Assert.Equal("PUBLIC_UNCHANGED",r.Startup.Neighbours["PUBLIC_OTHER_PRODUCT"]);Assert.True(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));
        Assert.Empty(Directory.GetFiles(r.App,"startup-settings-finished-*.json"));Assert.Single(Directory.GetFiles(r.App,"startup-settings-before-*.json"));
        await r.Peer!.Close();Assert.True((await entry.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);Assert.True(r.Port.StartupServer!.CleanupConfirmed);Assert.Empty(Directory.GetFiles(Path.Combine(r.Data,"config"),"secret-txn-*.json"));Assert.Single(Directory.GetFiles(Path.Combine(r.Data,"config"),"startup-settled-*.json"));
    });

    [Fact]public Task StartupSettingsPrePublicationFailureRestoresBothEffectsBeforeRemovingV3Intent()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();await entry.Start().WaitAsync(Limit);var value=r.Startup.Value;byte[] owner=File.ReadAllBytes(InstallationStartupRegistration.OwnerPath(r.App));
        await BeginStartupSettings(r,"before-fail");Assert.Equal("PUBLIC_STARTUP_SAVE|IOException|15|False|0|1",await StartupSettingsResult(r));
        Assert.Equal(value,r.Startup.Value);Assert.Equal(owner,File.ReadAllBytes(InstallationStartupRegistration.OwnerPath(r.App)));
        Assert.True(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));Assert.Empty(Directory.GetFiles(r.App,"startup-settings-before-*.json"));
        await r.Peer!.Close();Assert.True((await entry.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);
    });

    [Fact]public Task StartupSettingsPublishedObserverFailureStillNotifiesAllAndPreservesV3WithoutRollback()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();await entry.Start().WaitAsync(Limit);await BeginStartupSettings(r,"observer-fail");
        Assert.Equal("PUBLIC_STARTUP_SAVE|IOException|19|True|1|1",await StartupSettingsResult(r));
        Assert.Equal(new InstallationStartupValue(false),r.Startup.Value);Assert.False(File.Exists(InstallationStartupRegistration.OwnerPath(r.App)));
        string journal=Assert.Single(Directory.GetFiles(Path.Combine(r.Data,"config"),"secret-txn-*.json"));byte[] original=File.ReadAllBytes(journal);
        using(var json=System.Text.Json.JsonDocument.Parse(original)){Assert.Equal(3,json.RootElement.GetProperty("schemaVersion").GetInt32());Assert.NotEqual(Guid.Empty,json.RootElement.GetProperty("startupRequest").GetGuid());}
        var fresh=new SettingsCoordinator(new(Path.Combine(r.Data,"config")),new(Path.Combine(r.Data,"secrets"),new UnusedStartupProtector(),new StartupTestDirectory()));
        await Assert.ThrowsAsync<SettingsTransactionUnconfirmedException>(()=>fresh.OpenDraftAsync());Assert.Equal(original,File.ReadAllBytes(journal));
        await r.Peer!.Close();Assert.False((await entry.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);Assert.True(File.Exists(journal));
    });

    [Fact]public Task StartupSettingsKnownNamedWriteAfterEffectCanRestoreOnSameOwner()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();await entry.Start().WaitAsync(Limit);var before=r.Startup.Value;var error=new IOException("PUBLIC_B_KNOWN_AFTER_EFFECT");int once=0;
        r.Startup.AfterWrite=()=>{if(Interlocked.Exchange(ref once,1)==0)throw error;};
        await BeginStartupSettings(r,"normal");Assert.Equal("PUBLIC_STARTUP_SAVE|StartupRemovalRefusedException|15|False|0|1",await StartupSettingsResult(r));
        Assert.Equal(before,r.Startup.Value);Assert.Contains(error,r.Port.StartupServer!.Errors);Assert.True(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));
        await r.Peer!.Close();Assert.True((await entry.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);
    });

    [Fact]public Task StartupSettingsHeldOriginalWriteDoesNotBlockReaderOrDisappearOnCancellation()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();await entry.Start().WaitAsync(Limit);var before=r.Startup.Value;var entered=Signal();var release=Signal();int once=0;
        r.Startup.BeforeWrite=()=>{if(Interlocked.Exchange(ref once,1)==0){entered.SetResult();release.Task.GetAwaiter().GetResult();}};
        await BeginStartupSettings(r,"normal");
        await BodyAndCleanup(async()=>
        {
            await entered.Task.WaitAsync(Limit);await r.Peer!.Send("PING");Assert.Equal("PUBLIC_SHELL|Created",await r.Peer.ReadPublic());
            await r.Peer.Send("CANCEL_STARTUP_SAVE");Assert.Equal("PUBLIC_STARTUP_CANCELLED",await r.Peer.ReadPublic());
            Assert.False(r.Port.StartupServer!.CleanupConfirmed);Assert.False(entry.OriginalWork.IsCompleted);Assert.True(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));
        },()=>{release.TrySetResult();return Task.CompletedTask;});
        string result=(await StartupSettingsResult(r))!;Assert.Contains("CanceledException|15|False|0|1",result);Assert.Equal(before,r.Startup.Value);
        await r.Peer!.Close();Assert.True((await entry.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);
    });

    [Fact]public Task StartupSettingsOrdinaryUnqualifiedOperationNeverAttachesMutationParticipant()=>ExecuteOperation(async r=>
    {
        var operation=await r.PrepareOperation();await operation.Start().WaitAsync(Limit);Assert.Null(r.Port.StartupServer);
        await r.Peer!.Close();Assert.True((await operation.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);
    });

    [Fact]public Task StartupSettingsObservedForeignOwnerCannotWashOutAfterExternalRestoration()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();await entry.Start().WaitAsync(Limit);
        var owner=r.Port.StartupServer!.OriginalOwner;var request=Guid.NewGuid();await owner.ReserveAsync(request,"absent:absent");await owner.BindSettingsAsync(WriteSyntheticStartupIntent(r.Data,request));
        string path=InstallationStartupRegistration.OwnerPath(r.App);byte[] original=File.ReadAllBytes(path);
        File.WriteAllText(path,"PUBLIC_FOREIGN_STARTUP_OWNER");
        try{await Assert.ThrowsAnyAsync<IOException>(()=>owner.ApplyAsync());}
        finally{File.WriteAllBytes(path,original);} // Explicit synthetic external restoration; not a recovery authorization.
        await Assert.ThrowsAsync<InstallationRecoveryException>(()=>owner.AbortAsync());
        Assert.True(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));Assert.Empty(Directory.GetFiles(r.App,"startup-settings-finished-*.json"));
        Assert.False(owner.CleanupConfirmed);Assert.NotEqual(new InstallationStartupValue(false),r.Startup.Value);
    });

    [Fact]public Task StartupSettingsOriginalReplyBackpressureAndFailureNeverReleaseSlotOrReplay()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();await entry.Start().WaitAsync(Limit);var owner=r.Port.StartupServer!.OriginalOwner;
        var entered=Signal();var release=Signal();var outputError=new IOException("PUBLIC_STARTUP_ORIGINAL_REPLY");var tasks=new List<Task>();var replies=new List<string>();
        var server=new InstallationStartupRemovalServer(owner,r.Port.Proof!,async(line,_)=>{replies.Add(line);entered.SetResult();await release.Task;throw outputError;},tasks.Add);
        var request=Guid.NewGuid();server.Handle(InstallationStartupRemovalWire.Encode(r.Port.Proof!,request,"RESERVE","absent:absent"));Task? settlement=null;
        await BodyAndCleanup(async()=>
        {
            await entered.Task.WaitAsync(Limit);Assert.Equal(2,tasks.Count);Assert.All(tasks,t=>Assert.False(t.IsCompleted));Assert.False(server.CleanupConfirmed);
            Assert.Throws<InvalidDataException>(()=>server.Handle(InstallationStartupRemovalWire.Encode(r.Port.Proof!,Guid.NewGuid(),"RESERVE","absent:absent")));
            settlement=server.SettleAsync();Assert.False(settlement.IsCompleted);Assert.True(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));
        },()=>{release.TrySetResult();return Task.CompletedTask;});
        Assert.Same(outputError,await Assert.ThrowsAsync<IOException>(()=>settlement!));Assert.Single(replies);Assert.All(tasks,t=>Assert.True(t.IsCompleted));Assert.False(server.CleanupConfirmed);
        Assert.True(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));Assert.NotEqual(new InstallationStartupValue(false),r.Startup.Value);
    });

    [Fact]public Task StartupSettingsVisibleCommitReceiptCannotDeleteIntentBeforeOriginalRemoteSendSettles()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();await entry.Start().WaitAsync(Limit);
        var entered=Signal();var release=Signal();var outputError=new IOException("PUBLIC_VISIBLE_COMMIT_THEN_FLUSH_FAILURE");
        var tasks=new List<Task>();InstallationStartupRemovalClient? client=null;InstallationStartupRemovalServer? server=null;
        server=new(r.Port.StartupServer!.OriginalOwner,r.Port.Proof!,async(line,_)=>
        {
            client!.Receive(line); // The full line is already visible to the receiving application.
            if(line.EndsWith("|COMMIT_OK",StringComparison.Ordinal))
            {entered.SetResult();await release.Task;throw outputError;} // Original remote send/flush is still pending, then fails.
        },tasks.Add);
        client=new(r.Port.Proof!,(line,_)=>{server.Handle(line);return Task.CompletedTask;});
        string config=Path.Combine(r.Data,"config");
        var coordinator=new SettingsCoordinator(new(config),new(Path.Combine(r.Data,"visible-commit-secrets"),new UnusedStartupProtector(),new StartupTestDirectory()));
        using var draft=await coordinator.OpenDraftAsync();draft.Update(s=>s with{Overlay=s.Overlay with{FontSizeDip=19}});
        var originalSave=coordinator.SaveWithStartupRemovalAsync(draft,client);
        await BodyAndCleanup(async()=>
        {
            await entered.Task.WaitAsync(Limit);var saved=await originalSave.WaitAsync(Limit);
            int intents=Directory.GetFiles(config,"secret-txn-*.json").Length;
            output.WriteLine($"PUBLIC_VISIBLE_COMMIT saveReturned=True configFont={saved.Snapshot.Data.Overlay.FontSizeDip} draftClosed={draft.IsClosed} localV3Count={intents} state={saved.State} remoteSendPending={!tasks[3].IsCompleted} remoteCleanup={server.CleanupConfirmed}");
            Assert.False(tasks[3].IsCompleted);Assert.False(server.CleanupConfirmed);Assert.Equal("PublishedAwaitingOriginalControllerSettlement",saved.State);
            Assert.Equal(1,intents); // Receiving COMMIT_OK is not observation of the sender's original Task settlement.
        },async()=>
        {
            release.TrySetResult();try{await originalSave;}catch(Exception error){output.WriteLine("PUBLIC_VISIBLE_COMMIT saveAfterRelease="+error.GetType().Name);}
            Assert.Same(outputError,await Assert.ThrowsAsync<IOException>(()=>server.SettleAsync()));
            await client.JoinAsync();Assert.All(tasks,t=>Assert.True(t.IsCompleted));Assert.False(server.CleanupConfirmed);
        });
    });

    [Fact]public Task StartupSettingsVisibleAbortReceiptKeepsV3WhenOriginalRemoteSendLaterFails()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();await entry.Start().WaitAsync(Limit);var entered=Signal();var release=Signal();
        var outputError=new IOException("PUBLIC_VISIBLE_ABORT_THEN_FLUSH_FAILURE");var originalError=new IOException("PUBLIC_ABORT_CONFIG_FAILURE");
        var tasks=new List<Task>();InstallationStartupRemovalClient? client=null;InstallationStartupRemovalServer? server=null;
        server=new(r.Port.StartupServer!.OriginalOwner,r.Port.Proof!,async(line,_)=>
        {
            client!.Receive(line);
            if(line.EndsWith("|ABORT_OK",StringComparison.Ordinal)){entered.SetResult();await release.Task;throw outputError;}
        },tasks.Add);
        client=new(r.Port.Proof!,(line,_)=>{server.Handle(line);return Task.CompletedTask;});string config=Path.Combine(r.Data,"config");
        var store=new SettingsStore(config){WriteStageForTest=stage=>{if(stage==SettingsWriteStage.BeforePublish)throw originalError;}};
        var coordinator=new SettingsCoordinator(store,new(Path.Combine(r.Data,"secrets"),new UnusedStartupProtector(),new StartupTestDirectory()));
        using var draft=await coordinator.OpenDraftAsync();draft.Update(s=>s with{Overlay=s.Overlay with{FontSizeDip=19}});
        var originalSave=coordinator.SaveWithStartupRemovalAsync(draft,client);
        await BodyAndCleanup(async()=>
        {
            await entered.Task.WaitAsync(Limit);Assert.Same(originalError,await Assert.ThrowsAsync<IOException>(()=>originalSave));
            Assert.Equal(15,(await store.LoadAsync()).Data.Overlay.FontSizeDip);Assert.False(draft.IsClosed);
            Assert.Single(Directory.GetFiles(config,"secret-txn-*.json"));Assert.False(tasks[3].IsCompleted);Assert.False(server.CleanupConfirmed);
        },async()=>
        {
            release.TrySetResult();await Assert.ThrowsAsync<IOException>(()=>originalSave);
            Assert.Same(outputError,await Assert.ThrowsAsync<IOException>(()=>server.SettleAsync()));await client.JoinAsync();Assert.All(tasks,t=>Assert.True(t.IsCompleted));
            Assert.Single(Directory.GetFiles(config,"secret-txn-*.json"));Assert.False(server.CleanupConfirmed);
        });
    });

    [Theory][InlineData("COMMIT",false)][InlineData("ABORT",false)][InlineData("COMMIT",true)][InlineData("ABORT",true)]
    public Task StartupSettingsRealStdioFinalVisibleResponseRequiresOriginalExitAndIoBeforeRetirement(string verb,bool fail)=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();await entry.Start().WaitAsync(Limit);var entered=Signal();var release=Signal();var outputError=new IOException("PUBLIC_ACTUAL_FINAL_RESPONSE_FAILURE");
        FinalStartupResponseStream? wrapped=null;r.Port.StartupWriteForTest=stream=>wrapped??=new(stream,verb,entered,release,fail?outputError:null);
        await BeginStartupSettings(r,verb=="COMMIT"?"normal":"before-fail");
        await BodyAndCleanup(async()=>
        {
            await entered.Task.WaitAsync(Limit);
            string expected=verb=="COMMIT"?"PUBLIC_STARTUP_SAVE|PublishedAwaitingOriginalControllerSettlement|19|True|1|1":"PUBLIC_STARTUP_SAVE|IOException|15|False|0|1";
            Assert.Equal(expected,await StartupSettingsResult(r));Assert.NotNull(wrapped);Assert.False(wrapped!.OriginalWriteReturned);
            Assert.Single(Directory.GetFiles(Path.Combine(r.Data,"config"),"secret-txn-*.json"));Assert.Empty(Directory.GetFiles(r.App,"startup-settings-finished-*.json"));Assert.False(r.Port.StartupServer!.CleanupConfirmed);
        },()=>{release.TrySetResult();return Task.CompletedTask;});
        if(!fail)
        {
            await r.Port.StartupServer!.LocalNoticeObserved.WaitAsync(Limit);Assert.True(wrapped!.OriginalWriteReturned);
            Assert.False(entry.OriginalWork.IsCompleted);Assert.False(r.Port.StartupServer.CleanupConfirmed); // App is still alive.
        }
        await r.Peer!.Close();var settled=await entry.OriginalWork.WaitAsync(Limit);
        if(fail)
        {
            Assert.False(settled.CleanupConfirmed);Assert.Contains(outputError,Flatten(settled.Errors));
            Assert.Single(Directory.GetFiles(Path.Combine(r.Data,"config"),"secret-txn-*.json"));Assert.True(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));
        }
        else
        {
            Assert.True(settled.CleanupConfirmed);Assert.True(r.Port.StartupServer!.CleanupConfirmed);
            Assert.Empty(Directory.GetFiles(Path.Combine(r.Data,"config"),"secret-txn-*.json"));Assert.Single(Directory.GetFiles(Path.Combine(r.Data,"config"),"startup-settled-*.json"));
            Assert.Single(Directory.GetFiles(r.App,"startup-settings-finished-*.json"));Assert.False(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));
        }
    });

    [Theory][InlineData("missing-notice")][InlineData("wrong-notice")]
    public Task StartupSettingsOriginalExitWithoutExactLocalNoticeCannotRetireV3(string mode)=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();await entry.Start().WaitAsync(Limit);await BeginStartupSettings(r,mode);
        Assert.Equal("PUBLIC_STARTUP_SAVE|PublishedAwaitingOriginalControllerSettlement|19|True|1|1",await StartupSettingsResult(r));
        await r.Peer!.Close();Assert.False((await entry.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);
        Assert.Single(Directory.GetFiles(Path.Combine(r.Data,"config"),"secret-txn-*.json"));Assert.Empty(Directory.GetFiles(Path.Combine(r.Data,"config"),"startup-settled-*.json"));Assert.True(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));
    });

    [Theory][InlineData("cancel-before-bind","TaskCanceledException",15)][InlineData("stale-draft","SettingsConflictException",16)]
    public Task StartupSettingsEmptyReservationAbortConvergesOnlyAfterOriginalStdioExit(string mode,string error,int font)=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();await entry.Start().WaitAsync(Limit);var before=r.Startup.Value;byte[] owner=File.ReadAllBytes(InstallationStartupRegistration.OwnerPath(r.App));
        await BeginStartupSettings(r,mode);Assert.Equal($"PUBLIC_STARTUP_SAVE|{error}|{font}|False|0|0",await StartupSettingsResult(r));
        Assert.Equal(before,r.Startup.Value);Assert.Equal(owner,File.ReadAllBytes(InstallationStartupRegistration.OwnerPath(r.App)));
        Assert.True(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));Assert.Empty(Directory.GetFiles(r.App,"startup-settings-binding-*.json"));Assert.Empty(Directory.GetFiles(r.App,"startup-settings-before-*.json"));
        using(var decision=System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(Assert.Single(Directory.GetFiles(r.App,"startup-settings-decision-*.json")))))
        {Assert.Equal("ABORTED",decision.RootElement.GetProperty("outcome").GetString());Assert.Equal(System.Text.Json.JsonValueKind.Null,decision.RootElement.GetProperty("settings").ValueKind);}
        Assert.False(r.Port.StartupServer!.CleanupConfirmed);Assert.False(entry.OriginalWork.IsCompleted);Assert.Empty(Directory.GetFiles(Path.Combine(r.Data,"config"),"secret-txn-*.json"));
        await r.Peer!.Close();var result=await entry.OriginalWork.WaitAsync(Limit);
        output.WriteLine($"PUBLIC_EMPTY_RESERVATION mode={mode} cleanup={result.CleanupConfirmed} server={r.Port.StartupServer.CleanupConfirmed} pending={File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App))}");
        Assert.True(result.CleanupConfirmed);Assert.True(r.Port.StartupServer.CleanupConfirmed);Assert.False(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));Assert.Single(Directory.GetFiles(r.App,"startup-settings-finished-*.json"));
        Assert.Empty(Directory.GetFiles(Path.Combine(r.Data,"config"),"secret-txn-*.json"));Assert.Empty(Directory.GetFiles(Path.Combine(r.Data,"config"),"startup-settled-*.json"));
        Assert.Equal(before,r.Startup.Value);Assert.Equal(owner,File.ReadAllBytes(InstallationStartupRegistration.OwnerPath(r.App)));
    });

    [Theory][InlineData(false)][InlineData(true)]
    public Task StartupSettingsEmptyAbortVisibleResponseStillRequiresOriginalSendSettlement(bool fail)=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();await entry.Start().WaitAsync(Limit);var before=r.Startup.Value;byte[] owner=File.ReadAllBytes(InstallationStartupRegistration.OwnerPath(r.App));
        var entered=Signal();var release=Signal();var error=new IOException("PUBLIC_EMPTY_ABORT_RESPONSE_FAILURE");
        FinalStartupResponseStream? wrapped=null;r.Port.StartupWriteForTest=stream=>wrapped??=new(stream,"ABORT",entered,release,fail?error:null);
        await BeginStartupSettings(r,"cancel-before-bind");
        await BodyAndCleanup(async()=>
        {
            await entered.Task.WaitAsync(Limit);Assert.Equal("PUBLIC_STARTUP_SAVE|TaskCanceledException|15|False|0|0",await StartupSettingsResult(r));
            Assert.False(wrapped!.OriginalWriteReturned);Assert.False(r.Port.StartupServer!.CleanupConfirmed);Assert.False(entry.OriginalWork.IsCompleted);
            Assert.True(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));Assert.Empty(Directory.GetFiles(r.App,"startup-settings-finished-*.json"));Assert.Empty(Directory.GetFiles(r.App,"startup-settings-binding-*.json"));
        },()=>{release.TrySetResult();return Task.CompletedTask;});
        await r.Peer!.Close();var result=await entry.OriginalWork.WaitAsync(Limit);
        if(fail)
        {Assert.False(result.CleanupConfirmed);Assert.Contains(error,Flatten(result.Errors));Assert.True(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));Assert.Empty(Directory.GetFiles(r.App,"startup-settings-finished-*.json"));}
        else
        {Assert.True(wrapped!.OriginalWriteReturned);Assert.True(result.CleanupConfirmed);Assert.True(r.Port.StartupServer!.CleanupConfirmed);Assert.False(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));Assert.Single(Directory.GetFiles(r.App,"startup-settings-finished-*.json"));}
        Assert.Equal(before,r.Startup.Value);Assert.Equal(owner,File.ReadAllBytes(InstallationStartupRegistration.OwnerPath(r.App)));Assert.Empty(Directory.GetFiles(Path.Combine(r.Data,"config"),"secret-txn-*.json"));Assert.Empty(Directory.GetFiles(Path.Combine(r.Data,"config"),"startup-settled-*.json"));
    });

    [Theory][InlineData("bind")][InlineData("apply")]
    public Task StartupSettingsFailedBindOrApplyAttemptCannotUseEmptyReservationRetirement(string attempt)=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();await entry.Start().WaitAsync(Limit);var owner=r.Port.StartupServer!.OriginalOwner;Guid request=Guid.NewGuid();await owner.ReserveAsync(request,"absent:absent");
        if(attempt=="bind")
        {await Assert.ThrowsAnyAsync<IOException>(()=>owner.BindSettingsAsync(new(request,Guid.NewGuid(),"absent:absent",new string('0',64))));await Assert.ThrowsAsync<InstallationRecoveryException>(()=>owner.AbortAsync());}
        else
        {await Assert.ThrowsAsync<InstallationRecoveryException>(()=>owner.ApplyAsync());await owner.AbortAsync();}
        Assert.False(owner.CanRetireEmptyReservation);
        var refused=await Assert.ThrowsAsync<AggregateException>(()=>owner.RetireEmptyReservationAsync(r.Port.Child!,Task.CompletedTask,Task.CompletedTask));
        Assert.Collection(refused.InnerExceptions,
            error=>Assert.Equal("Only an original never-bound, never-applied aborted reservation can retire without local intent.",Assert.IsType<InstallationRecoveryException>(error).Message),
            error=>Assert.Equal("Original startup cleanup is unconfirmed.",Assert.IsType<InstallationRecoveryException>(error).Message));
        Assert.False(owner.CleanupConfirmed);Assert.True(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));Assert.Empty(Directory.GetFiles(r.App,"startup-settings-finished-*.json"));
        await r.Peer!.Close();Assert.False((await entry.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);
    });

    private sealed class FinalStartupResponseStream(Stream inner,string verb,TaskCompletionSource entered,TaskCompletionSource release,Exception? failure):Stream
    {
        internal bool OriginalWriteReturned;
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush()=>inner.Flush();public override Task FlushAsync(CancellationToken cancellation)=>inner.FlushAsync(cancellation);
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellation=default)
        {
            await inner.WriteAsync(buffer,cancellation);await inner.FlushAsync(cancellation); // Real child can read the full line before this task returns.
            if(System.Text.Encoding.ASCII.GetString(buffer.Span).EndsWith("|"+verb+"_OK\n",StringComparison.Ordinal))
            {entered.TrySetResult();await release.Task;if(failure!=null)throw failure;OriginalWriteReturned=true;}
        }
        public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
        public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();
    }

    private sealed class UnusedStartupProtector:ISecretProtector
    {public byte[] Protect(ReadOnlySpan<byte> data)=>throw new InvalidOperationException();public byte[] Unprotect(ReadOnlySpan<byte> data)=>throw new InvalidOperationException();}
    private sealed class StartupTestDirectory:ISecretDirectoryPolicy
    {public void EnsurePrivate(string path)=>Directory.CreateDirectory(path);}
}
