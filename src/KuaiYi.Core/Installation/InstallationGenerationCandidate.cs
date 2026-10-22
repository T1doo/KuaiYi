using System.Security.Cryptography;
using System.Text.Json;
namespace KuaiYi.Core.Installation;

internal sealed record InactiveGenerationRetention(string Generation,string IntentSha256,string SourceCatalogSha256,string? InstalledCatalogSha256,string State);
internal sealed record GenerationPreparationContext(string Root,string Source,string SourceSha256,string Generation,string Operation,string Staging,string Destination,string Intent,string TicketSha256,string BeforeSha256,CancellationToken Cancellation);
// The constructor is private. Neither InstalledVersion nor a digest can reconstruct this scope-bound result.
internal sealed class InstallationGenerationCandidate
{
    private readonly InstallationDeploymentScope owner;
    internal InstalledVersion Version {get;}
    internal string SourceCatalogSha256 {get;}
    private InstallationGenerationCandidate(InstallationDeploymentScope owner,InstalledVersion version,string source)
    {this.owner=owner;Version=version;SourceCatalogSha256=source;}
    // Identity/lifetime check only; no publication or execution API is exposed by this slice.
    internal bool IsLiveFor(InstallationDeploymentScope scope)=>ReferenceEquals(owner,scope)&&scope.IsLiveCandidate(this);
    internal static InstallationGenerationCandidate Prepare(InstallationDeploymentScope scope,InstallationPreparationIO io)
    {
        var context=scope.RequireGenerationPreparation(io);var cancellation=context.Cancellation;
        var package=InstallationPackage.ReadSource(context.Source,context.SourceSha256,cancellation,io);
        if(package.Schema!=2||package.Payload==null)throw new InvalidDataException("Only a fully qualified materializable source can prepare a generation.");
        io.Stage(GenerationPreparationStage.SourceVerified);scope.RequireGenerationPreparation(io);
        var metadata=InstallationPayload.Derived(package,context.Destination);byte[] catalog=InstallationPayload.InstalledCatalog(package,metadata);
        foreach(var item in metadata)if(item.Value.Length is <1||item.Value.Length>(item.Key==InstallationPayload.SourceCatalogName?1024*1024:65536))throw new InvalidDataException("Derived metadata exceeds consumer bound.");
        byte[] intent=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=2,kind="KuaiYiGenerationPreparation",operation=context.Operation,generation=context.Generation,source=context.Source,sourceCatalogSha256=package.CatalogSha256,ticketSha256=context.TicketSha256,beforeSha256=context.BeforeSha256,staging=context.Staging,destination=context.Destination});
        if(intent.Length>16384)throw new InvalidDataException("Generation ownership exceeds its bound.");
        string intentSha=Convert.ToHexString(SHA256.HashData(intent)),installedSha=Convert.ToHexString(SHA256.HashData(catalog));
        var retention=new InactiveGenerationRetention(context.Generation,intentSha,package.CatalogSha256,installedSha,"VerifiedInactive");
        scope.RequireRetentionCapacity(retention);scope.RequireRetentionCapacity(retention with{InstalledCatalogSha256=null,State="IncompleteInactive"});
        io.WriteMetadata(context.Intent,intent,16384);io.RequireExactMetadata(context.Intent,intent,16384);scope.RecordGenerationIntent(io,retention with{InstalledCatalogSha256=null,State="IncompleteInactive"});
        io.Stage(GenerationPreparationStage.IntentWritten);scope.RequireGenerationPreparation(io);io.CreateStaging();
        io.WriteMetadata(Path.Combine(context.Staging,InstallationPayload.SourceCatalogName),package.Catalog,1024*1024);
        foreach(var entry in package.Manifest.Files)
        {scope.RequireGenerationPreparation(io);io.CopyPayload(Path.Combine(context.Source,entry.Path),Path.Combine(context.Staging,entry.Path),entry);}
        package.Verify(context.Staging,cancellation,InstallationPayload.SourceCatalogName,io);io.Stage(GenerationPreparationStage.SourceCopied);
        scope.RequireGenerationPreparation(io);
        foreach(var item in metadata)if(item.Key!=InstallationPayload.SourceCatalogName)io.WriteMetadata(Path.Combine(context.Staging,item.Key),item.Value,65536);
        io.WriteMetadata(Path.Combine(context.Staging,InstallationPackage.CatalogName),catalog,1024*1024);io.Stage(GenerationPreparationStage.MetadataWritten);
        var staged=InstallationPackage.ReadInstalled(context.Staging,cancellation,context.Destination,installedSha,io);
        if(staged.Schema!=3||staged.SourceCatalogSha256!=package.CatalogSha256)throw new InvalidDataException("Generation qualification differs.");
        io.Stage(GenerationPreparationStage.StagingVerified);scope.RequireGenerationPreparation(io);io.MoveToFinal();io.Stage(GenerationPreparationStage.DirectoryMoved);
        var final=InstallationPackage.ReadInstalled(context.Destination,cancellation,expectedDigest:installedSha,io:io);
        if(final.Schema!=3||final.SourceCatalogSha256!=package.CatalogSha256||!io.CleanupConfirmed)throw new InstallationRecoveryException("Candidate validation or original IO settlement is unconfirmed.");
        io.Stage(GenerationPreparationStage.FinalVerified);scope.RequireGenerationPreparation(io);scope.RecordGenerationIntent(io,retention);
        io.Stage(GenerationPreparationStage.BeforeCredential);
        return new(scope,new(final.Manifest.Version,context.Destination,final.CatalogSha256),package.CatalogSha256);
    }
}
