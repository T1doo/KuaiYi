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
        =>new(r.App,r.Data,port,TimeSpan.FromSeconds(1),port){BrowserHostPublishedForTest=()=>{port.BrowserPublished.TrySetResult();published.TrySetResult();}};
    private static async Task BrowserBody(InstallationUpgradeCoordinator coordinator,Task<int> run,Peer peer,Func<Task> body,Action? release=null,Func<Task>? close=null)
    {
        List<Exception> errors=[];try{await body();}catch(Exception error){errors.Add(error);}
        try{release?.Invoke();}catch(Exception error){errors.Add(error);}
        if(!run.IsCompleted)try{await (close?.Invoke()??peer.Close());}catch(Exception error){errors.Add(error);}
        try{await run.WaitAsync(Limit);}catch(Exception error){if(!errors.Contains(error))errors.Add(error);}
        try{await coordinator.DisposeAsync();}catch(Exception error){errors.Add(error);}
        try{await peer.DisposeAsync();}catch(Exception error){errors.Add(error);}
        ThrowBrowserErrors(errors);
    }
    private static void ThrowBrowserErrors(List<Exception> errors)
    {
        if(errors.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if(errors.Count>1)throw new AggregateException(errors);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ConnectedBrowserPeerCleanupDoesNotSwallowActualWriteFailureOrReplaceBody(bool withBody)
    {
        string name="ky-connected-cleanup-"+Guid.NewGuid().ToString("N");var peer=new Peer(name);
        using var connect=new CancellationTokenSource(Limit);
        using var client=new System.IO.Pipes.NamedPipeClientStream(".",name,System.IO.Pipes.PipeDirection.InOut,System.IO.Pipes.PipeOptions.Asynchronous|System.IO.Pipes.PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(connect.Token);await peer.Connected.WaitAsync(Limit);client.Dispose(); // Actual connected endpoint closes before the cleanup write.
        var body=new InvalidOperationException("PUBLIC_CONNECTED_BROWSER_BODY");List<Exception> errors=[];
        try{if(withBody)throw body;}catch(Exception error){errors.Add(error);}
        Exception? cleanup=await Record.ExceptionAsync(()=>peer.DisposeAsync().AsTask());if(cleanup!=null)errors.Add(cleanup);
        Exception? observed=Record.Exception(()=>ThrowBrowserErrors(errors));
        output.WriteLine($"PUBLIC_CONNECTED_BROWSER cleanup={cleanup?.GetType().Name??"None"} combined={observed?.GetType().Name??"None"} withBody={withBody}");
        if(withBody){var combined=Assert.IsType<AggregateException>(observed);Assert.Same(body,combined.InnerExceptions[0]);}
        Assert.NotNull(cleanup);var originals=cleanup is AggregateException many?many.Flatten().InnerExceptions.ToArray():new[]{cleanup};Assert.Contains(originals,error=>error is IOException);
    }
    [Fact]public async Task BrowserPublicationFailureRemainsOriginalWhenDiagnosticOutputAlsoFails()
    {
        var expected=new IOException("PUBLIC_BROWSER_ORIGINAL_STARTUP");var port=new Port(new FailingStartupOutput(output));
        var original=Task.FromException<int>(expected);Exception? observed=await Record.ExceptionAsync(()=>port.WaitForBrowserPublishedAsync(original));
        Assert.Same(expected,observed);Assert.IsType<InvalidOperationException>(port.DiagnosticFailure);Assert.False(port.BrowserPublished.Task.IsCompleted);
    }
    [Fact]public async Task UnconnectedBrowserPeerCleanupPreservesOriginalBodyAndJoinsOriginalConnection()
    {
        var body=new InvalidOperationException("PUBLIC_ORIGINAL_BROWSER_BODY");Peer? peer=null;
        Exception? observed=await Record.ExceptionAsync(async()=>{await using var original=peer=new Peer("ky-browser-cleanup-"+Guid.NewGuid().ToString("N"));throw body;});
        Assert.NotNull(peer);Exception? connection=await Record.ExceptionAsync(()=>peer.Connected.WaitAsync(Limit));
        output.WriteLine($"PUBLIC_UNCONNECTED_BROWSER body={observed?.Message} connection={connection?.GetType().Name} originalConnectSettled={peer.Connected.IsCompleted}");
        Assert.Same(body,observed);Assert.True(peer.Connected.IsCompleted);Assert.IsAssignableFrom<OperationCanceledException>(connection);
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
        await RefuseHost(selected);await Command(host,"CHECK");Assert.Equal("REJECTED|InstallationRecoveryException",await host.Row());
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
    [Theory][InlineData("owner")][InlineData("session")][InlineData("generation")][InlineData("pending")][InlineData("activity")][InlineData("closing")][InlineData("closing-directory")]
    public async Task BrowserHostUnknownMetadataIsNotAdopted(string fault)
    {
        using var r=new Rig();var selected=await r.Prepare();await using var peer=new Peer(r.Pipe);var port=new Port(output);var published=Signal();
        var coordinator=BrowserCoordinator(r,port,published);var run=coordinator.RunActiveSessionAsync(selected.DirectoryPath);
        try{await BrowserBody(coordinator,run,peer,async()=>{
            await port.WaitForBrowserPublishedAsync(run);
            string path=fault switch{"owner"=>r.Owner,"session"=>InstallationBrowserHostSession.PathFor(r.App),"generation"=>Path.Combine(r.App,"current.json"),"pending"=>Path.Combine(r.App,"host-registration-pending.json"),"closing" or "closing-directory"=>InstallationBrowserHostSession.ClosingPath(port.Proof!),_=>InstallationHostActivity.PathFor(r.App)};
            byte[]? prior=File.Exists(path)?File.ReadAllBytes(path):null;
            if(fault=="closing-directory")Directory.CreateDirectory(path);else File.WriteAllText(path,"PUBLIC_UNKNOWN");
            try{await RefuseHost(selected);if(fault=="closing-directory")Assert.True(Directory.Exists(path));else Assert.Equal("PUBLIC_UNKNOWN",File.ReadAllText(path));}
            finally{if(fault=="closing-directory")Directory.Delete(path);else if(prior==null)File.Delete(path);else File.WriteAllBytes(path,prior);}
            await peer.Close();Assert.Equal(0,await run.WaitAsync(Limit));using var mutation=r.Store.EnterAdmission(mutation:true);
        });}
        catch{r.Keep=true;throw;}
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
    [Fact]public async Task BrowserHostCancellationWithSetupBusyAndOriginalCleanupHeldStillRevokesNewEntry()
    {
        using var r=new Rig();var selected=await r.Prepare();await using var peer=new Peer(r.Pipe);var port=new Port(output){HoldCleanupIo=true};var published=Signal();using var stop=new CancellationTokenSource();
        var coordinator=BrowserCoordinator(r,port,published);var run=coordinator.RunActiveSessionAsync(selected.DirectoryPath,stop.Token);await published.Task.WaitAsync(Limit);
        FileStream? setup=InstallationStore.OpenExistingSetup(r.App);List<Exception> errors=[];
        try
        {
            stop.Cancel();await port.CleanupIoEntered.Task.WaitAsync(Limit);
            await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));
            Assert.True(File.Exists(port.Proof!.PathFor("accepted")));Assert.False(port.CleanupIoRelease.Task.IsCompleted);
            setup.Dispose();setup=null;
            Assert.True(File.Exists(InstallationBrowserHostSession.ClosingPath(port.Proof)));
            await RefuseHost(selected); // Original accepted/owner leases alone must not authorize a new Host after cancellation.
        }
        catch(Exception error){errors.Add(error);}
        finally
        {
            try{setup?.Dispose();}catch(Exception error){errors.Add(error);}
            port.CleanupIoRelease.TrySetResult();
            try{await coordinator.DisposeAsync();}catch(Exception error){errors.Add(error);}
        }
        ThrowBrowserErrors(errors);Assert.False(File.Exists(InstallationBrowserHostSession.PathFor(r.App)));Assert.True(File.Exists(r.Owner));
    }
    [Fact]public async Task BrowserHostUnknownEvenIdenticalClosingReceiptCannotBeAdoptedByRetirement()
    {
        using var r=new Rig();var selected=await r.Prepare();var peer=new Peer(r.Pipe);var port=new Port(output);var published=Signal();
        var coordinator=BrowserCoordinator(r,port,published);var run=coordinator.RunActiveSessionAsync(selected.DirectoryPath);
        string? path=null;byte[]? foreign=null;List<Exception> errors=[];
        try
        {
            await port.WaitForBrowserPublishedAsync(run);path=InstallationBrowserHostSession.ClosingPath(port.Proof!);
            foreign=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",launch=port.Proof!.Launch.ToString("N"),phase="Closing"});
            File.WriteAllBytes(path,foreign);
            await RefuseHost(selected);await peer.Close();await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));
            Assert.Equal(foreign,File.ReadAllBytes(path));Assert.True(File.Exists(InstallationBrowserHostSession.PathFor(r.App)));
            await Assert.ThrowsAnyAsync<IOException>(async()=>await coordinator.DisposeAsync());Assert.Equal(foreign,File.ReadAllBytes(path));r.Fence();
        }
        catch(Exception error){errors.Add(error);}
        finally
        {
            // Only this test-created synthetic marker; production never deletes or adopts an unknown closing receipt.
            if(foreign!=null)try{Assert.Equal(foreign,File.ReadAllBytes(path!));File.Delete(path!);}catch(Exception error){errors.Add(error);}
            try{await coordinator.DisposeAsync();}catch(Exception error){errors.Add(error);}
            try{await peer.DisposeAsync();}catch(Exception error){errors.Add(error);}
        }
        if(errors.Count!=0)r.Keep=true;ThrowBrowserErrors(errors);Assert.NotNull(path);
        Assert.True(File.Exists(path));Assert.False(File.Exists(InstallationBrowserHostSession.PathFor(r.App)));Assert.True(File.Exists(r.Owner));
    }
}
