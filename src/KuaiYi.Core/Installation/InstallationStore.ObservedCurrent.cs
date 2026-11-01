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
    // The fixed source derives the complete expected catalog. Damaged target bytes are preserved, not treated as trust.
    internal static InstalledVersion ReadPinnedRepairCurrent(string root,byte[] current,InstallationPackage source)
    {
        var prior=Parse(current);if(!Encode(prior).AsSpan().SequenceEqual(current))throw new InstallationRecoveryException("Noncanonical repair current.");
        if(source.Schema!=2||source.Payload==null)throw new InstallationRecoveryException("Complete independent repair source required.");
        string directory=Path.Combine(root,"versions",prior.Active.Id);byte[] catalog=InstallationPayload.InstalledCatalog(source,InstallationPayload.Derived(source,directory));
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(catalog));
        if(!string.Equals(hash,prior.Active.CatalogSha256,StringComparison.OrdinalIgnoreCase))throw new InstallationRecoveryException("Current is not derived from the fixed repair source.");
        return new(source.Manifest.Version,directory,hash);
    }

}
