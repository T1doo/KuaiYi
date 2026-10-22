using System.Security.Cryptography;
using System.Text.Json;
namespace KuaiYi.Core.Installation;

// Original snapshot owner supplies another read, never caller-provided authorization or a replacement owner.
internal interface IInstallationDeploymentPreparedRead:IInstallationDeploymentSnapshotRead
{Task<InstallationDeploymentRegistrationSnapshot> ReadForPreparationAsync();}
internal sealed record InstallationHostOwnership(int SchemaVersion,string Product,Guid Owner,InstalledVersion Version,string Manifest);
internal sealed record PreparedBootstrapPlan(string InactiveDirectory,string CanonicalDirectory,string Owner,string Receipt);
internal sealed record PreparedHostPlan(InstallationHostOwnership? Before,string Receipt,InstallationDeploymentRegistrationSnapshot Targets);
internal sealed record InstallationDeploymentPlan(int SchemaVersion,string Kind,string Operation,string TicketSha256,string BeforeSha256,string GenerationSourceSha256,InstalledVersion Generation,string BootstrapSourceSha256,string BootstrapRevision,string TargetCurrent,PreparedBootstrapPlan Bootstrap,PreparedHostPlan Host);
internal sealed record PreparedPreparationContext(string Root,string Data,string Operation,string Bundle,string Path,string TicketSha256,string BeforeSha256,InstallationDeploymentBefore Before,InstallationBootstrapCandidate Bootstrap,CancellationToken Cancellation);

// Qualified plan only. No method publishes files, moves a bundle, starts a role or writes registry.
internal sealed class InstallationDeploymentPrepared
{
    private readonly InstallationDeploymentScope owner;
    internal InstallationBootstrapCandidate Bootstrap{get;}
    internal InstallationDeploymentPlan Plan{get;}
    internal string Sha256{get;}
    private InstallationDeploymentPrepared(InstallationDeploymentScope owner,InstallationBootstrapCandidate bootstrap,InstallationDeploymentPlan plan,byte[] bytes)
    {this.owner=owner;Bootstrap=bootstrap;Plan=plan;Sha256=Convert.ToHexString(SHA256.HashData(bytes));}
    internal bool BelongsTo(InstallationDeploymentScope scope)=>ReferenceEquals(owner,scope);
    internal bool IsLiveFor(InstallationDeploymentScope scope)=>BelongsTo(scope)&&scope.IsLivePrepared(this);
    internal static async Task<InstallationDeploymentPrepared> SealAsync(InstallationDeploymentScope scope,InstallationPreparationIO io,IInstallationDeploymentPreparedRead registry)
    {
        bool attempted=false;
        try
        {
            var c=scope.RequirePreparedPreparation(io);var stop=c.Cancellation;var b=c.Bootstrap;var g=b.Generation;
            void CheckCandidates()
            {
                var payload=InstallationPackage.ReadInstalled(g.Version.DirectoryPath,stop,expectedDigest:g.Version.CatalogSha256,io:io);
                if(payload.Schema!=3||payload.SourceCatalogSha256!=g.SourceCatalogSha256)throw new InstallationRecoveryException("Original G qualification changed.");
                if(InstallationBootstrapPackage.Read(b.DirectoryPath,b.CatalogSha256,g.Version,stop,io).Revision!=b.Revision)throw new InstallationRecoveryException("Original B qualification changed.");
            }
            CheckCandidates();
            var(current,old)=InstallationStore.PlanCurrent(c.Root,Decode(c.Before.Current),g.Version,stop,io);
            var errors=new List<Exception>();InstallationDeploymentRegistrationSnapshot? slots=null;
            try{slots=await registry.ReadForPreparationAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
            try{await registry.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
            if(!registry.CleanupConfirmed)errors.Add(new IOException("Original Prepared registry resources are unsettled."));
            InstallationErrors.Throw(errors);stop.ThrowIfCancellationRequested();
            if(slots!=c.Before.Registry)throw new InstallationRecoveryException("Original registry snapshot changed.");
            var host=PlanHost(c,old,slots!,g.Version);
            byte[]? ownerBefore=Decode(c.Before.BootstrapOwner),activeBefore=Decode(c.Before.BootstrapActive);InstallationBootstrapDeployment.StoreOwner owner;
            if(ownerBefore==null)
            {
                if(activeBefore!=null)throw new InstallationRecoveryException("Bootstrap active has no original owner.");
                io.RequireAbsent(InstallationBootstrapDeployment.BundlesPath(c.Root));owner=new(1,"KuaiYi",Guid.ParseExact(c.Operation,"N"),c.Root,c.Data);
            }
            else
            {
                owner=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.StoreOwner>(ownerBefore);InstallationBootstrapDeployment.RequireOwner(owner,c.Root,c.Data);io.RequireDirectory(InstallationBootstrapDeployment.BundlesPath(c.Root));
                if(activeBefore!=null)
                {var previous=InstallationBootstrapDeployment.RequireReceipt(c.Root,c.Data,owner,activeBefore,stop,io);if(previous.Generation!=old)throw new InstallationRecoveryException("Bootstrap receipt does not bind original current.");}
                io.RequireAbsent(InstallationBootstrapDeployment.BundlePath(c.Root,Guid.ParseExact(c.Bundle,"N")));
            }
            byte[] ownerBytes=InstallationBootstrapDeployment.EncodeOwner(owner),receipt=InstallationBootstrapDeployment.EncodeReceipt(new(1,"KuaiYi",InstallationBootstrapPackage.Mode,owner.Owner,Guid.ParseExact(c.Bundle,"N"),b.CatalogSha256,b.Revision,g.Version));
            InstallationBootstrapDeployment.RequireTransport(c.Root,c.Data,g.Version,current,ownerBytes,receipt);
            var bootstrap=new PreparedBootstrapPlan(b.DirectoryPath,InstallationBootstrapDeployment.BundlePath(c.Root,Guid.ParseExact(c.Bundle,"N")),Convert.ToBase64String(ownerBytes),Convert.ToBase64String(receipt));
            var plan=new InstallationDeploymentPlan(2,"KuaiYiUnifiedDeploymentPrepared",c.Operation,c.TicketSha256,c.BeforeSha256,g.SourceCatalogSha256,g.Version,b.CatalogSha256,b.Revision,Convert.ToBase64String(current),bootstrap,host);
            byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(plan);if(bytes.Length>131072)throw new InvalidDataException("Prepared exceeds its actual encoded bound.");scope.RequirePreparedTerminalCapacity(Convert.ToHexString(SHA256.HashData(bytes)));
            CheckCandidates();io.Stage(GenerationPreparationStage.SourceVerified);scope.RequirePreparedPreparation(io);
            if(!io.CleanupConfirmed||io.StorageUnconfirmed||!registry.CleanupConfirmed)throw new InstallationRecoveryException("Original preparation reads have not settled.");
            stop.ThrowIfCancellationRequested();attempted=true;io.WriteMetadata(c.Path,bytes,131072);io.RequireExactMetadata(c.Path,bytes,131072);io.Stage(GenerationPreparationStage.MetadataWritten);
            scope.RequirePreparedPreparation(io);if(!io.CleanupConfirmed||!registry.CleanupConfirmed)throw new InstallationRecoveryException("Original plan persistence remains unsettled.");
            var result=new InstallationDeploymentPrepared(scope,b,plan,bytes);io.Stage(GenerationPreparationStage.BeforeQualification);io.RequireExactMetadata(c.Path,bytes,131072);scope.CompletePrepared(io,result);io.Stage(GenerationPreparationStage.AfterQualification);return result;
        }
        catch(Exception error){if(attempted||error is not OperationCanceledException)io.MarkUnconfirmed();throw;}
    }
    private static byte[]? Decode(string? value)=>value==null?null:Convert.FromBase64String(value);
    private static PreparedHostPlan PlanHost(PreparedPreparationContext c,InstalledVersion? old,InstallationDeploymentRegistrationSnapshot slots,InstalledVersion next)
    {
        byte[]? raw=Decode(c.Before.HostReceipt);InstallationHostOwnership? before=null;
        if(raw!=null)
        {
            if(old==null)throw new InstallationRecoveryException("Host receipt has no original current.");
            before=JsonSerializer.Deserialize<InstallationHostOwnership>(raw)??throw new InstallationRecoveryException("Missing original Host ownership.");
            if(!raw.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(before))||before.SchemaVersion!=1||before.Product!="KuaiYi"||before.Owner==Guid.Empty||before.Version!=old||before.Manifest!=Path.Combine(old.DirectoryPath,InstallationPayload.HostManifestName))throw new InstallationRecoveryException("Unknown or stale original Host ownership.");
        }
        foreach(var slot in new[]{slots.Chrome,slots.Edge})
            if(slot.ForeignContent||slot.Value!=before?.Manifest||(!slot.KeyExists&&slot.Value!=null))throw new InstallationRecoveryException("Observed registry defaults have no matching original Host ownership.");
        string manifest=Path.Combine(next.DirectoryPath,InstallationPayload.HostManifestName);byte[] receipt=JsonSerializer.SerializeToUtf8Bytes(new InstallationHostOwnership(1,"KuaiYi",Guid.ParseExact(c.Operation,"N"),next,manifest));
        if(receipt.Length>8192)throw new InvalidDataException("Target Host receipt exceeds consumer bound.");
        return new(before,Convert.ToBase64String(receipt),new(new(true,manifest),new(true,manifest)));
    }
}
