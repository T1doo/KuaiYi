namespace KuaiYi.Core.Installation;
public sealed partial class InstallationStore
{
    // A source-authorized caller still needs its own live mutation lease. This method only observes bytes.
    internal static InstalledVersion ReadObservedCurrent(string root,byte[] current,CancellationToken cancellation,InstallationPreparationIO io)
    {
        var prior=Parse(current);if(!Encode(prior).AsSpan().SequenceEqual(current))throw new InstallationRecoveryException("Noncanonical current observation retained.");
        string directory=Path.Combine(root,"versions",prior.Active.Id);var package=InstallationPackage.ReadInstalled(directory,cancellation,expectedDigest:prior.Active.CatalogSha256,io:io);
        if(package.Schema!=3)throw new InstallationRecoveryException("Current observation is not a qualified v3 payload.");return new(package.Manifest.Version,directory,package.CatalogSha256);
    }
}
