using KuaiYi.Core.Installation;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed partial class InstallationDeploymentSessionTests
{
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
        Assert.Equal("PUBLIC_STARTUP_SAVE|PASS|19|True|1|0",await StartupSettingsResult(r));
        Assert.Equal(new InstallationStartupValue(false),r.Startup.Value);Assert.False(File.Exists(InstallationStartupRegistration.OwnerPath(r.App)));
        Assert.Equal("PUBLIC_UNCHANGED",r.Startup.Neighbours["PUBLIC_OTHER_PRODUCT"]);Assert.False(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));
        Assert.Single(Directory.GetFiles(r.App,"startup-settings-finished-*.json"));Assert.Single(Directory.GetFiles(r.App,"startup-settings-before-*.json"));
        await r.Peer!.Close();Assert.True((await entry.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);Assert.True(r.Port.StartupServer!.CleanupConfirmed);
    });

    [Fact]public Task StartupSettingsPrePublicationFailureRestoresBothEffectsBeforeRemovingV3Intent()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();await entry.Start().WaitAsync(Limit);var value=r.Startup.Value;byte[] owner=File.ReadAllBytes(InstallationStartupRegistration.OwnerPath(r.App));
        await BeginStartupSettings(r,"before-fail");Assert.Equal("PUBLIC_STARTUP_SAVE|IOException|15|False|0|0",await StartupSettingsResult(r));
        Assert.Equal(value,r.Startup.Value);Assert.Equal(owner,File.ReadAllBytes(InstallationStartupRegistration.OwnerPath(r.App)));
        Assert.False(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));Assert.Empty(Directory.GetFiles(r.App,"startup-settings-before-*.json"));
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
        await r.Peer!.Close();Assert.True((await entry.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);
    });

    [Fact]public Task StartupSettingsKnownNamedWriteAfterEffectCanRestoreOnSameOwner()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();await entry.Start().WaitAsync(Limit);var before=r.Startup.Value;var error=new IOException("PUBLIC_B_KNOWN_AFTER_EFFECT");int once=0;
        r.Startup.AfterWrite=()=>{if(Interlocked.Exchange(ref once,1)==0)throw error;};
        await BeginStartupSettings(r,"normal");Assert.Equal("PUBLIC_STARTUP_SAVE|StartupRemovalRefusedException|15|False|0|0",await StartupSettingsResult(r));
        Assert.Equal(before,r.Startup.Value);Assert.Contains(error,r.Port.StartupServer!.Errors);Assert.False(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));
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
        string result=(await StartupSettingsResult(r))!;Assert.Contains("CanceledException|15|False|0|0",result);Assert.Equal(before,r.Startup.Value);
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
        var owner=r.Port.StartupServer!.OriginalOwner;await owner.ReserveAsync(Guid.NewGuid(),"absent:absent");
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
            await entered.Task.WaitAsync(Limit);_ =Assert.Single(tasks);Assert.False(tasks[0].IsCompleted);Assert.False(server.CleanupConfirmed);
            Assert.Throws<InvalidDataException>(()=>server.Handle(InstallationStartupRemovalWire.Encode(r.Port.Proof!,Guid.NewGuid(),"RESERVE","absent:absent")));
            settlement=server.SettleAsync();Assert.False(settlement.IsCompleted);Assert.True(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));
        },()=>{release.TrySetResult();return Task.CompletedTask;});
        Assert.Same(outputError,await Assert.ThrowsAsync<IOException>(()=>settlement!));Assert.Single(replies);Assert.True(tasks[0].IsCompleted);Assert.False(server.CleanupConfirmed);
        Assert.True(File.Exists(InstallationStartupRemovalOwner.PendingPath(r.App)));Assert.NotEqual(new InstallationStartupValue(false),r.Startup.Value);
    });

    private sealed class UnusedStartupProtector:ISecretProtector
    {public byte[] Protect(ReadOnlySpan<byte> data)=>throw new InvalidOperationException();public byte[] Unprotect(ReadOnlySpan<byte> data)=>throw new InvalidOperationException();}
    private sealed class StartupTestDirectory:ISecretDirectoryPolicy
    {public void EnsurePrivate(string path)=>Directory.CreateDirectory(path);}
}
