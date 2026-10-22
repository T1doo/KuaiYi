using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed partial class InstallationStableLauncherTests
{
    [Fact]public async Task BootstrapBoundRealControllerAdmissionPublishesOriginalReadyAndRejectsMutationUntilSettlement()
    {
        using var r=new Rig();await r.Prepare();var source=new BootstrapSource(Path.Combine(r.Root,"bootstrap-source"));var deploy=new InstallationBootstrapDeployment(r.App,r.Data);await deploy.DeployAsync(source.Root,source.Digest);
        var record=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.Receipt>(File.ReadAllBytes(InstallationBootstrapDeployment.ActivePath(r.App)));using var entry=InstallationBootstrapEntry.Open(InstallationBootstrapDeployment.BundlePath(r.App,record.Bundle),r.Data);
        await using var peer=new Peer(r.Pipe);var launcher=new InstallationStableLauncher(r.App,r.Data,TimeSpan.FromSeconds(4),new Starter()){Bootstrap=entry.Binding};Task<int> run=launcher.RunAsync();
        await Finish(r,launcher,run,async()=>{
            await launcher.Ready.WaitAsync(Limit);byte[] active=File.ReadAllBytes(InstallationBootstrapDeployment.ActivePath(r.App));await Assert.ThrowsAnyAsync<IOException>(()=>deploy.WithdrawAsync());Assert.Equal(active,File.ReadAllBytes(InstallationBootstrapDeployment.ActivePath(r.App)));Assert.False(File.Exists(InstallationBootstrapDeployment.PendingPath(r.App)));
            await peer.Close();Assert.Equal(0,await run.WaitAsync(Limit));Assert.True(launcher.CleanupConfirmed);
        });
    }
    [Theory][InlineData(false)][InlineData(true)]public async Task BootstrapEntryApprovedBeforeAWinningWithdrawOrReplaceCannotStartUiWhenItsControllerArrivesLate(bool replace)
    {
        using var r=new Rig();await r.Prepare();var source=new BootstrapSource(Path.Combine(r.Root,"bootstrap-source"));var deploy=new InstallationBootstrapDeployment(r.App,r.Data);await deploy.DeployAsync(source.Root,source.Digest);
        var record=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.Receipt>(File.ReadAllBytes(InstallationBootstrapDeployment.ActivePath(r.App)));using var entry=InstallationBootstrapEntry.Open(InstallationBootstrapDeployment.BundlePath(r.App,record.Bundle),r.Data);
        var starter=new Starter{HoldBeforeStart=true};var launcher=new InstallationStableLauncher(r.App,r.Data,TimeSpan.FromSeconds(4),starter){Bootstrap=entry.Binding};Task<int> run=launcher.RunAsync();
        await Finish(r,launcher,run,async()=>{
            await starter.Held.Task.WaitAsync(Limit);if(replace){source.Version="2.0";source.Catalog();await deploy.DeployAsync(source.Root,source.Digest);}else await deploy.WithdrawAsync();starter.Release.TrySetResult();
            Assert.NotNull(await Record.ExceptionAsync(()=>run.WaitAsync(Limit)));Assert.False(launcher.Ready.IsCompletedSuccessfully);Assert.True(launcher.CleanupConfirmed);Assert.Equal(3,launcher.ObservedControllerExitCode);Assert.False(File.Exists(r.Owner));Assert.False(File.Exists(Path.Combine(r.Root,"PUBLIC_SHELL_CREATED")));
        },()=>starter.Release.TrySetResult());
    }
}
