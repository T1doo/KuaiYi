using System.Security.Cryptography;
using System.Text.Json;
namespace KuaiYi.Core.Installation;

internal sealed record InactiveBootstrapRetention(string Bundle,string IntentSha256,string SourceCatalogSha256,string Generation,string GenerationCatalogSha256,string? Revision,string State);
internal sealed record BootstrapPreparationContext(string Root,string Source,string SourceSha256,string Bundle,string Operation,string Staging,string Destination,string Intent,string TicketSha256,string BeforeSha256,InstallationGenerationCandidate Generation,CancellationToken Cancellation);
// Inactive protocol result only. Canonical bootstrap store/active publication belongs to a later phase.
internal sealed class InstallationBootstrapCandidate
{
    private readonly InstallationDeploymentScope owner;
    internal InstallationGenerationCandidate Generation{get;}
    internal string DirectoryPath{get;}
    internal string CatalogSha256{get;}
    internal string Revision{get;}
    internal string Mode{get;}
    private InstallationBootstrapCandidate(InstallationDeploymentScope owner,BootstrapPreparationContext context,string digest,string revision,string mode)
    {this.owner=owner;Generation=context.Generation;DirectoryPath=context.Destination;CatalogSha256=digest;Revision=revision;Mode=mode;}
    internal bool IsLiveFor(InstallationDeploymentScope scope)=>ReferenceEquals(owner,scope)&&scope.IsLiveBootstrap(this);
    internal static InstallationBootstrapCandidate Prepare(InstallationDeploymentScope scope,InstallationPreparationIO io)
    {
        var context=scope.RequireBootstrapPreparation(io);var cancellation=context.Cancellation;
        void CheckGeneration()
        {
            scope.RequireBootstrapPreparation(io);var generation=InstallationPackage.ReadInstalled(context.Generation.Version.DirectoryPath,cancellation,expectedDigest:context.Generation.Version.CatalogSha256,io:io);
            if(generation.Schema!=3||generation.SourceCatalogSha256!=context.Generation.SourceCatalogSha256)throw new InstallationRecoveryException("Original G candidate changed.");
        }
        CheckGeneration();var (package,revision)=InstallationBootstrapPackage.Read(context.Source,context.SourceSha256,context.Generation.Version,cancellation,io);
        io.Stage(GenerationPreparationStage.SourceVerified);scope.RequireBootstrapPreparation(io);
        byte[] intent=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=2,kind="KuaiYiBootstrapPreparation",operation=context.Operation,bundle=context.Bundle,source=context.Source,sourceCatalogSha256=package.CatalogSha256,ticketSha256=context.TicketSha256,beforeSha256=context.BeforeSha256,generation=context.Generation.Version,generationSourceSha256=context.Generation.SourceCatalogSha256,revision,staging=context.Staging,destination=context.Destination});
        if(intent.Length>16384)throw new InvalidDataException("Bootstrap ownership exceeds its bound.");
        var retention=new InactiveBootstrapRetention(context.Bundle,Convert.ToHexString(SHA256.HashData(intent)),package.CatalogSha256,Path.GetFileName(context.Generation.Version.DirectoryPath),context.Generation.Version.CatalogSha256,revision,"VerifiedInactive");
        scope.RequireBootstrapRetentionCapacity(retention);scope.RequireBootstrapRetentionCapacity(retention with{Revision=null,State="IncompleteInactive"});
        io.WriteMetadata(context.Intent,intent,16384);io.RequireExactMetadata(context.Intent,intent,16384);scope.RecordBootstrapIntent(io,retention with{Revision=null,State="IncompleteInactive"});
        io.Stage(GenerationPreparationStage.IntentWritten);scope.RequireBootstrapPreparation(io);io.CreateStaging();
        foreach(var entry in package.Manifest.Files){scope.RequireBootstrapPreparation(io);io.CopyPayload(Path.Combine(context.Source,entry.Path),Path.Combine(context.Staging,entry.Path),entry);}
        io.Stage(GenerationPreparationStage.SourceCopied);scope.RequireBootstrapPreparation(io);io.WriteMetadata(Path.Combine(context.Staging,InstallationPackage.CatalogName),package.Catalog,1024*1024);io.Stage(GenerationPreparationStage.MetadataWritten);
        if(InstallationBootstrapPackage.Read(context.Staging,package.CatalogSha256,context.Generation.Version,cancellation,io).Revision!=revision)throw new InvalidDataException("Staged bootstrap revision changed.");
        io.Stage(GenerationPreparationStage.StagingVerified);scope.RequireBootstrapPreparation(io);io.MoveToFinal();io.Stage(GenerationPreparationStage.DirectoryMoved);
        CheckGeneration();if(InstallationBootstrapPackage.Read(context.Destination,package.CatalogSha256,context.Generation.Version,cancellation,io).Revision!=revision||!io.CleanupConfirmed)throw new InstallationRecoveryException("Bootstrap qualification or original IO settlement unconfirmed.");
        io.Stage(GenerationPreparationStage.FinalVerified);scope.RequireBootstrapPreparation(io);scope.RecordBootstrapIntent(io,retention);io.Stage(GenerationPreparationStage.BeforeCredential);
        return new(scope,context,package.CatalogSha256,revision,InstallationBootstrapPackage.ModeOf(package));
    }
}
