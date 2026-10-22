using System.Security.Cryptography;
using System.Text.Json;
namespace KuaiYi.Core.Installation;

// A private parent/child binding, never an external capability or a replacement for original role admission.
internal sealed record InstallationStableSelection(Guid Nonce,string Root,string UserData,InstalledVersion Version,string Current)
{
    internal byte[] Bytes()=>JsonSerializer.SerializeToUtf8Bytes(this);
    internal string Fingerprint=>Convert.ToHexString(SHA256.HashData(Bytes()));
    internal string Encode()=>Convert.ToBase64String(Bytes());
    internal static InstallationStableSelection Decode(string encoded)
    {
        if(encoded.Length>16384)throw new InvalidDataException("Stable selection exceeds bound.");byte[] bytes=Convert.FromBase64String(encoded);
        var value=JsonSerializer.Deserialize<InstallationStableSelection>(bytes)??throw new InvalidDataException("Missing stable selection.");
        if(!bytes.AsSpan().SequenceEqual(value.Bytes()))throw new InvalidDataException("Noncanonical stable selection.");value.Validate();return value;
    }
    private void Validate()
    {
        if(Nonce==Guid.Empty||Root!=InstallationPaths.Root(Root)||UserData!=InstallationPaths.Root(UserData)||Version==null||Current.Length>5464||Convert.FromBase64String(Current).Length>4096||
            InstallationNormalEntry.FindInstallationRoot(Version.DirectoryPath)!=Root||!InstallationPackage.HashValid(Version.CatalogSha256))throw new InstallationRecoveryException("Invalid stable selection.");
        _=new InstallationStore(Root,UserData,()=>false);
    }
    internal void RequireOwn(Guid nonce,string directory,string userData)
    {
        Validate();if(Nonce!=nonce||Version.DirectoryPath!=InstallationPaths.Root(directory)||UserData!=InstallationPaths.Root(userData))throw new InstallationRecoveryException("Controller layout or data binding differs.");
    }
    internal void RequireSelected(InstalledVersion selected,CancellationToken cancellation)
    {
        Validate();if(selected!=Version)throw new InstallationRecoveryException("Stable selection generation differs.");
        InstallationControlledLaunch.Exact(Path.Combine(Root,"current.json"),Convert.FromBase64String(Current),4096);
        if(InstallationPayload.RequireMaterialized(selected.DirectoryPath,cancellation).CatalogSha256!=selected.CatalogSha256)throw new InstallationRecoveryException("Stable payload qualification differs.");
        InstallationControlledLaunch.Exact(Path.Combine(Root,"current.json"),Convert.FromBase64String(Current),4096);cancellation.ThrowIfCancellationRequested();
    }
    internal static InstallationStableSelection Select(string root,string userData,CancellationToken cancellation)
    {
        root=InstallationPaths.Root(root);userData=InstallationPaths.Root(userData);var store=new InstallationStore(root,userData,()=>false);byte[] current;
        // Existing-only short gates: no store/owner initialization, full scans or child lifetime held under setup.
        using(var lease=store.EnterAdmission(existing:true))current=store.SnapshotRecord(lease)??throw new InstallationRecoveryException("Stable launch has no current generation.");
        cancellation.ThrowIfCancellationRequested();var selected=store.ReadActive(cancellation)??throw new InstallationRecoveryException("No current payload.");
        var value=new InstallationStableSelection(Guid.NewGuid(),root,userData,selected,Convert.ToBase64String(current));value.RequireSelected(selected,cancellation);
        using(var lease=store.EnterAdmission(existing:true))InstallationControlledLaunch.Exact(Path.Combine(root,"current.json"),current,4096);
        return value;
    }
}
