using KuaiYi.Core;
using KuaiYi.Core.Installation;
namespace KuaiYi.App;

// The existing App executable is the parent for exactly one user session. No service, adoption or restart.
internal sealed class InstallationSessionEntry
{
    private readonly InstallationUpgradeCoordinator coordinator;
    private readonly string directory;
    private InstallationSessionEntry(string root,string directory,string userData,IInstallationApplicationProtocol protocol,TimeSpan budget,IOwnedProcessStarter? starter)
    {this.directory=directory;coordinator=new(root,userData,protocol,budget,starter);}
    internal static InstallationSessionEntry? TryCreate(string directory,string userData,IInstallationApplicationProtocol? protocol=null,TimeSpan? budget=null,IOwnedProcessStarter? starter=null)
    {
        string? root=InstallationNormalEntry.FindInstallationRoot(directory);
        return root==null?null:new(root,directory,userData,protocol??new InstallationApplicationClient(),budget??TimeSpan.FromSeconds(10),starter);
    }
    internal Task<int> RunAsync(CancellationToken cancellation=default)=>coordinator.RunActiveSessionAsync(directory,cancellation);
}
