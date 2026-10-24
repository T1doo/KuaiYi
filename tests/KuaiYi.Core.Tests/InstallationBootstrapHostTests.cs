using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed partial class InstallationSessionTests
{
    [Theory][InlineData(false)][InlineData(true)]public async Task BootstrapPendingRevokesBothExistingAndFreshActualBrowserHostEntry(bool directory)
    {
        using var r=new Rig();var selected=await r.Prepare();await WithPeer(r,async peer=>
        {
            var port=peer.Own(new Port(output));var published=Signal();var coordinator=peer.Own(BrowserCoordinator(r,port,published));Task<int> run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath));
            await BrowserBody(coordinator,run,peer,async()=>{
                await published.Task.WaitAsync(Limit);var host=peer.OwnBrowser(Browser(selected));await EnterHost(host);string pending=InstallationBootstrapDeployment.PendingPath(r.App);Assert.False(InstallationBootstrapDeployment.Exists(pending));
                if(directory)Directory.CreateDirectory(pending);else File.WriteAllText(pending,"PUBLIC_OWNED_SYNTHETIC_PENDING");
                await FixtureBodyAndCleanup(async()=>
                {
                    await Command(host,"CHECK");Assert.Equal("REJECTED|InstallationRecoveryException",await host.Row());await RefuseHost(selected);
                    if(directory)Assert.True(Directory.Exists(pending));else Assert.Equal("PUBLIC_OWNED_SYNTHETIC_PENDING",File.ReadAllText(pending));
                },()=>{if(directory)Directory.Delete(pending);else{Assert.Equal("PUBLIC_OWNED_SYNTHETIC_PENDING",File.ReadAllText(pending));File.Delete(pending);}return Task.CompletedTask;});
                await Command(host,"CHECK");Assert.Equal("REJECTED|InstallationRecoveryException",await host.Row());await Command(host,"EXIT");await host.Finish(0);
            });
        });
    }
}
