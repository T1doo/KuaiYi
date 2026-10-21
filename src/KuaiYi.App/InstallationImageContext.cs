using System.IO;
using KuaiYi.Core.Installation;
namespace KuaiYi.App;

internal sealed class InstallationImageContext
{
    internal readonly string Root,UserData,Image;
    internal readonly InstalledVersion Version;
    private readonly byte[] current;
    internal InstallationImageContext(string directory,string userData)
    {
        Root=InstallationNormalEntry.FindInstallationRoot(directory)??throw new InstallationRecoveryException("Installed image entry required.");UserData=InstallationPaths.Root(userData);
        Version=new InstallationStore(Root,UserData,()=>false).ReadActive()??throw new InstallationRecoveryException("Selected generation required.");
        if(Version.DirectoryPath!=InstallationPaths.Root(directory))throw new InstallationRecoveryException("Old generation refused.");
        current=InstallationPackage.ReadDocument(Path.Combine(Root,"current.json"),4096);Image=Path.Combine(Version.DirectoryPath,"KuaiYi.App.exe");RequireCurrent();
    }
    internal void RequireCurrent()
    {
        InstallationControlledLaunch.Exact(Path.Combine(Root,"current.json"),current,4096);
        foreach(string name in new[]{"upgrade-pending.json","handoff-pending.json"})
        {string path=Path.Combine(Root,name);InstallationPaths.Check(path);if(File.Exists(path)||Directory.Exists(path))throw new InstallationBusyException();}
    }
    internal Task VerifyAsync(CancellationToken token)=>Task.Run(()=>
    {
        token.ThrowIfCancellationRequested();RequireCurrent();
        if(new InstallationStore(Root,UserData,()=>false).ReadActive(token)!=Version)throw new InstallationRecoveryException("Selected files changed.");RequireCurrent();token.ThrowIfCancellationRequested();
    },CancellationToken.None);
    internal InstallationControlledLaunch RequireGrant(InstallationWarmGrant grant)
    {
        RequireCurrent();var proof=InstallationControlledLaunch.Decode(Convert.ToBase64String(InstallationPackage.ReadDocument(Path.Combine(Root,$"launch-{grant.Identity.Launch:N}-accepted.json"),49152)));
        if(proof.Root!=Root||proof.UserData!=UserData||proof.Version!=Version||proof.Fingerprint!=grant.Identity.Proof||proof.UpgradeRecord!=null)throw new InstallationRecoveryException("Warm launch binding differs.");
        grant.Identity.Require(proof);proof.RequireImageRecords();return proof;
    }
}
