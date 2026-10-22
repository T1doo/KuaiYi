using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed partial class InstallationSessionTests
{
    [Theory][InlineData(false)][InlineData(true)]public async Task BootstrapPendingRevokesBothExistingAndFreshActualBrowserHostEntry(bool directory)
    {
        using var r=new Rig();var selected=await r.Prepare();await using var peer=new Peer(r.Pipe);var port=new Port(output);var published=Signal();var coordinator=BrowserCoordinator(r,port,published);Task<int> run=coordinator.RunActiveSessionAsync(selected.DirectoryPath);
        await BrowserBody(coordinator,run,peer,async()=>{
            await published.Task.WaitAsync(Limit);await using var host=Browser(selected);await EnterHost(host);string pending=InstallationBootstrapDeployment.PendingPath(r.App);Assert.False(InstallationBootstrapDeployment.Exists(pending));
            if(directory)Directory.CreateDirectory(pending);else File.WriteAllText(pending,"PUBLIC_OWNED_SYNTHETIC_PENDING");
            try
            {
                await Command(host,"CHECK");Assert.Equal("REJECTED|InstallationRecoveryException",await host.Row());await RefuseHost(selected);
                if(directory)Assert.True(Directory.Exists(pending));else Assert.Equal("PUBLIC_OWNED_SYNTHETIC_PENDING",File.ReadAllText(pending));
            }
            finally{if(directory)Directory.Delete(pending);else{Assert.Equal("PUBLIC_OWNED_SYNTHETIC_PENDING",File.ReadAllText(pending));File.Delete(pending);}}
            await Command(host,"CHECK");Assert.Equal("REJECTED|InstallationRecoveryException",await host.Row());await Command(host,"EXIT");await host.Finish(0);
        });
    }
}
