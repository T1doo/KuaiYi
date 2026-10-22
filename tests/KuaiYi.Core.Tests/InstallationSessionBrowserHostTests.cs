using System.Diagnostics;
using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed partial class InstallationSessionTests
{
    private static string BrowserDriver=>Path.Combine(Repo,"tools/KuaiYi.BrowserHostFixture/bin/Release/net10.0/KuaiYi.BrowserHostFixture.dll");
    private static Parent Browser(InstalledVersion selected)
    {
        var info=new ProcessStartInfo(Host){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in new[]{BrowserDriver,"host-lifetime",selected.DirectoryPath})info.ArgumentList.Add(arg);
        Process p=Process.Start(info)!;return new(p,p.StandardError.ReadToEndAsync());
    }
    private static async Task Command(Parent child,string command){await child.Process.StandardInput.WriteLineAsync(command);await child.Process.StandardInput.FlushAsync();}
    private static async Task EnterHost(Parent child){Assert.Equal("STARTED",await child.Row());await Command(child,"ENTER");Assert.Equal("ENTERED",await child.Row());}
    private static async Task RefuseHost(InstalledVersion selected,string? expected=null)
    {await using var child=Browser(selected);Assert.Equal("STARTED",await child.Row());await Command(child,"ENTER");string? row=await child.Row();Assert.StartsWith("REFUSED|",row);if(expected!=null)Assert.Equal("REFUSED|"+expected,row);await child.Finish(3);}
    private static InstallationUpgradeCoordinator BrowserCoordinator(Rig r,Port port,TaskCompletionSource published)
        =>new(r.App,r.Data,port,TimeSpan.FromSeconds(1),port){BrowserHostPublishedForTest=()=>published.TrySetResult()};
    private static async Task BrowserBody(InstallationUpgradeCoordinator coordinator,Task<int> run,Peer peer,Func<Task> body,Action? release=null,Func<Task>? close=null)
    {
        List<Exception> errors=[];try{await body();}catch(Exception error){errors.Add(error);}
        try{release?.Invoke();}catch(Exception error){errors.Add(error);}
        if(!run.IsCompleted)try{await (close?.Invoke()??peer.Close());}catch(Exception error){errors.Add(error);}
        try{await run.WaitAsync(Limit);}catch(Exception error){if(!errors.Contains(error))errors.Add(error);}
        try{await coordinator.DisposeAsync();}catch(Exception error){errors.Add(error);}
        ThrowBrowserErrors(errors);
    }
    private static void ThrowBrowserErrors(List<Exception> errors)
    {
        if(errors.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if(errors.Count>1)throw new AggregateException(errors);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task BrowserHostOriginalProcessExitAloneReleasesMutationAfterSessionReturn(bool crash)
    {
        using var r=new Rig();var selected=await r.Prepare();await using var peer=new Peer(r.Pipe);var port=new Port(output);var published=Signal();
        var coordinator=BrowserCoordinator(r,port,published);Task<int> run=coordinator.RunActiveSessionAsync(selected.DirectoryPath);
        await BrowserBody(coordinator,run,peer,async()=>{
        await published.Task.WaitAsync(Limit);await using var host=Browser(selected);await EnterHost(host);
        await Command(host,"SESSION");Assert.Equal("SESSION_RETURNED|3",await host.Row());Assert.False(host.Process.HasExited);
        await peer.Close();Assert.Equal(0,await run.WaitAsync(Limit));Assert.False(File.Exists(r.Owner));
        // Independent browser child is NOT in coordinator roles; its live original handle still fences mutation.
        Assert.Throws<InstallationBusyException>(()=>r.Store.EnterAdmission(mutation:true));
        await Assert.ThrowsAsync<InstallationBusyException>(()=>r.Store.RecoverAsync());
        await RefuseHost(selected);await Command(host,"CHECK");Assert.Equal("REJECTED|FileNotFoundException",await host.Row());
        await Command(host,crash?"CRASH":"EXIT");await host.Finish(crash?23:0);
        using var mutation=r.Store.EnterAdmission(mutation:true);mutation.RequireMutation(r.App);
        output.WriteLine($"PUBLIC_BROWSER_HOST actualExit={(crash?23:0)} sessionReturnedBeforeExit=true independentOriginalChild=true mutationAfterActualExit=true");
        });
    }
    [Fact]public async Task BrowserHostOldConnectionPermanentlyRejectsNewNormalSessionWithoutRetargeting()
    {
        using var r=new Rig();var selected=await r.Prepare();await using var old=Browser(selected);
        await using(var peer=new Peer(r.Pipe))
        {
            var published=Signal();var port=new Port(output);await using var coordinator=BrowserCoordinator(r,port,published);
            var run=coordinator.RunActiveSessionAsync(selected.DirectoryPath);await published.Task.WaitAsync(Limit);await EnterHost(old);await peer.Close();Assert.Equal(0,await run.WaitAsync(Limit));
        }
        await using(var peer=new Peer(r.Pipe))
        {
            var published=Signal();var port=new Port(output);await using var coordinator=BrowserCoordinator(r,port,published);
            var run=coordinator.RunActiveSessionAsync(selected.DirectoryPath);await published.Task.WaitAsync(Limit);
            await Command(old,"CHECK");Assert.Equal("REJECTED|InstallationRecoveryException",await old.Row());
            await Command(old,"OFFER");Assert.Equal("REJECTED|InstallationRecoveryException",await old.Row());
            await Command(old,"SUBMIT");Assert.Equal("REJECTED|InstallationRecoveryException",await old.Row());
            await using var fresh=Browser(selected);await EnterHost(fresh);await Command(fresh,"CHECK");Assert.Equal("CURRENT",await fresh.Row());
            await Command(fresh,"EXIT");await fresh.Finish(0);await peer.Close();Assert.Equal(0,await run.WaitAsync(Limit));
        }
        await Command(old,"EXIT");await old.Finish(0);using var mutation=r.Store.EnterAdmission(mutation:true);
    }
    [Theory][InlineData("owner")][InlineData("session")][InlineData("generation")][InlineData("pending")][InlineData("activity")]
    public async Task BrowserHostUnknownMetadataIsNotAdopted(string fault)
    {
        using var r=new Rig();var selected=await r.Prepare();await using var peer=new Peer(r.Pipe);var port=new Port(output);var published=Signal();
        await using var coordinator=BrowserCoordinator(r,port,published);var run=coordinator.RunActiveSessionAsync(selected.DirectoryPath);await published.Task.WaitAsync(Limit);
        string path=fault switch{"owner"=>r.Owner,"session"=>InstallationBrowserHostSession.PathFor(r.App),"generation"=>Path.Combine(r.App,"current.json"),"pending"=>Path.Combine(r.App,"host-registration-pending.json"),_=>InstallationHostActivity.PathFor(r.App)};
        byte[]? prior=File.Exists(path)?File.ReadAllBytes(path):null;File.WriteAllText(path,"PUBLIC_UNKNOWN");
        try{await RefuseHost(selected);Assert.Equal("PUBLIC_UNKNOWN",File.ReadAllText(path));}
        finally{if(prior==null)File.Delete(path);else File.WriteAllBytes(path,prior);}
        await peer.Close();Assert.Equal(0,await run.WaitAsync(Limit));using var mutation=r.Store.EnterAdmission(mutation:true);
    }
    [Fact]public async Task BrowserHostDoesNotEnterBeforeReadyOrDuringMutationAndRoleLeaseCannotMutate()
    {
        using var r=new Rig();var selected=await r.Prepare();await RefuseHost(selected);
        using(var mutation=r.Store.EnterAdmission(mutation:true))
        {await RefuseHost(selected,nameof(InstallationBusyException));mutation.RequireMutation(r.App);}
        using(var role=r.Store.EnterAdmission())
        {
            Assert.Throws<InstallationRecoveryException>(()=>role.RequireMutation(r.App));
            Assert.Throws<InstallationRecoveryException>(()=>r.Store.RestoreForUpgrade(role,null,[]));
            Assert.Throws<InstallationRecoveryException>(()=>r.Store.FinishUpgrade(role,[],Guid.NewGuid(),"PUBLIC"));
        }
        await using var peer=new Peer(r.Pipe);var port=new Port(output){HoldHealth=true};var published=Signal();var coordinator=BrowserCoordinator(r,port,published);
        var run=coordinator.RunActiveSessionAsync(selected.DirectoryPath);Task? close=null;
        await BrowserBody(coordinator,run,peer,async()=>{
            await port.Healthy.Task.WaitAsync(Limit);await RefuseHost(selected);Assert.False(published.Task.IsCompleted);
            port.HealthRelease.TrySetResult();await published.Task.WaitAsync(Limit);await using var host=Browser(selected);await EnterHost(host);await Command(host,"EXIT");await host.Finish(0);
            await (close??=peer.Close());Assert.Equal(0,await run.WaitAsync(Limit));
        },()=>port.HealthRelease.TrySetResult(),()=>close??=peer.Close());
    }

    [Fact]public async Task BrowserHostCancellationRetiresEntryButRetainsOriginalFailedSessionOwner()
    {
        using var r=new Rig();var selected=await r.Prepare();await using var peer=new Peer(r.Pipe);var port=new Port(output);var published=Signal();using var stop=new CancellationTokenSource();
        await using var coordinator=BrowserCoordinator(r,port,published);var run=coordinator.RunActiveSessionAsync(selected.DirectoryPath,stop.Token);await published.Task.WaitAsync(Limit);
        await using var host=Browser(selected);await EnterHost(host);byte[] owner=File.ReadAllBytes(r.Owner);stop.Cancel();
        await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(async()=>await run.WaitAsync(Limit));
        Assert.False(File.Exists(InstallationBrowserHostSession.PathFor(r.App)));Assert.Equal(owner,File.ReadAllBytes(r.Owner));await RefuseHost(selected);
        await Command(host,"OFFER");Assert.StartsWith("REJECTED|",await host.Row());await Command(host,"EXIT");await host.Finish(0);
        Assert.Throws<InstallationRecoveryException>(()=>r.Store.EnterAdmission(mutation:true));
    }
    [Fact]public async Task BrowserHostRetirementMismatchRetainsOriginalAdmissionAndBothErrorsUntilExactRetry()
    {
        using var r=new Rig();var selected=await r.Prepare();await using var peer=new Peer(r.Pipe);var port=new Port(output);var published=Signal();using var stop=new CancellationTokenSource();
        var coordinator=BrowserCoordinator(r,port,published);var run=coordinator.RunActiveSessionAsync(selected.DirectoryPath,stop.Token);await published.Task.WaitAsync(Limit);
        string path=InstallationBrowserHostSession.PathFor(r.App);byte[] exact=File.ReadAllBytes(path);File.WriteAllText(path,"PUBLIC_REPLACEMENT");stop.Cancel();
        List<Exception> errors=[];
        try
        {
            var failure=await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(async()=>await run.WaitAsync(Limit));
            var both=Assert.IsType<AggregateException>(failure.InnerException);Assert.Equal(2,both.InnerExceptions.Count);Assert.IsAssignableFrom<OperationCanceledException>(both.InnerExceptions[0]);Assert.IsType<InstallationRecoveryException>(both.InnerExceptions[1]);
            Assert.Equal("PUBLIC_REPLACEMENT",File.ReadAllText(path));Assert.True(port.WarmActualExitObserved);Assert.Single(port.CleanupExits);
            Assert.Throws<InstallationRecoveryException>(()=>InstallationControlledLaunch.RequireHeldLease(Path.Combine(r.App,"installation.lock"))); // setup itself is never retained
            Assert.ThrowsAny<IOException>(()=>r.Store.EnterRoleAdmission());
            await Assert.ThrowsAsync<InstallationRecoveryException>(async()=>await coordinator.DisposeAsync());
            Assert.Equal("PUBLIC_REPLACEMENT",File.ReadAllText(path));
        }
        catch(Exception error){errors.Add(error);}
        finally
        {
            try{File.WriteAllBytes(path,exact);}catch(Exception error){errors.Add(error);}
            try{await coordinator.DisposeAsync();}catch(Exception error){errors.Add(error);}
        }
        ThrowBrowserErrors(errors);Assert.False(File.Exists(path));Assert.True(File.Exists(r.Owner));
        using var setup=InstallationStore.OpenExistingSetup(r.App);
    }

    [Fact]public async Task BrowserHostExitCannotSubstituteForOriginalApplicationIoSettlement()
    {
        using var r=new Rig();var selected=await r.Prepare();await using var peer=new Peer(r.Pipe);var port=new Port(output){HoldIo=true};var published=Signal();
        var coordinator=BrowserCoordinator(r,port,published);var run=coordinator.RunActiveSessionAsync(selected.DirectoryPath);Task? close=null;
        await BrowserBody(coordinator,run,peer,async()=>{
            await published.Task.WaitAsync(Limit);await using var host=Browser(selected);await EnterHost(host);
            await (close??=peer.Close());await port.IoEntered.Task.WaitAsync(Limit);
            Assert.False(run.IsCompleted);Assert.False(File.Exists(InstallationBrowserHostSession.PathFor(r.App)));await RefuseHost(selected);
            await Command(host,"EXIT");await host.Finish(0);Assert.False(run.IsCompleted);r.Fence();
            port.IoRelease.TrySetResult();Assert.Equal(0,await run.WaitAsync(Limit));using var mutation=r.Store.EnterAdmission(mutation:true);
        },()=>port.IoRelease.TrySetResult(),()=>close??=peer.Close());
    }

    [Fact]public async Task BrowserHostMissingActivityLeaseIsNeverInitializedByEntry()
    {
        using var r=new Rig();var selected=await r.Prepare();await using var peer=new Peer(r.Pipe);var port=new Port(output);var published=Signal();
        await using var coordinator=BrowserCoordinator(r,port,published);var run=coordinator.RunActiveSessionAsync(selected.DirectoryPath);await published.Task.WaitAsync(Limit);
        string path=InstallationHostActivity.PathFor(r.App);File.Delete(path);await RefuseHost(selected);Assert.False(File.Exists(path));
        File.WriteAllBytes(path,[]);await peer.Close();Assert.Equal(0,await run.WaitAsync(Limit));
    }
    [Fact]public async Task BrowserHostLegacyStoreWithoutActivityCannotBeAutomaticallyMigratedOrMutated()
    {
        using var r=new Rig();var selected=await r.Prepare();string path=InstallationHostActivity.PathFor(r.App);File.Delete(path);
        byte[] current=File.ReadAllBytes(Path.Combine(r.App,"current.json"));
        await RefuseHost(selected);Assert.Throws<InstallationRecoveryException>(()=>r.Store.EnterRoleAdmission());
        await Assert.ThrowsAsync<InstallationRecoveryException>(()=>r.Store.RecoverAsync());
        await Assert.ThrowsAsync<InstallationRecoveryException>(()=>r.Store.ApplyAsync(Path.Combine(r.Root,"package"),Hash(Path.Combine(r.Root,"package/package.json"))));
        Assert.False(File.Exists(path));Assert.Equal(current,File.ReadAllBytes(Path.Combine(r.App,"current.json")));
    }
}
