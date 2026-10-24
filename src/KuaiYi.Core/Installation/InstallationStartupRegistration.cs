using System.Text.Json;
namespace KuaiYi.Core.Installation;

// Only this one named value is observed. The shared Run key and neighbouring values are never owned.
internal sealed record InstallationStartupValue(bool Exists,string? Kind=null,string? Value=null);
internal interface IInstallationStartupRegistry:IInstallationDeploymentPublicationRegistry
{
    Task<InstallationStartupValue> ReadStartupAsync();
    Task WriteStartupAsync(InstallationStartupValue expected,InstallationStartupValue next);
}
internal sealed record InstallationStartupOwnership(int SchemaVersion,string Product,Guid Owner,string Root,string UserData,
    InstalledVersion Generation,Guid Bundle,string GenerationSourceSha256,string BootstrapSha256,string Command);
internal sealed record InstallationStartupPlan(InstallationStartupValue Before,InstallationStartupValue Target,string? BeforeOwner,string? TargetOwner);
// Read-only uninstall intent. It grants no registry/file mutation; an uninstaller still needs its own live transaction.
internal sealed record InstallationStartupRemovalPlan(InstallationStartupValue Expected,string Owner);

internal sealed partial class InstallationDeploymentScope
{
    internal InstallationStartupRemovalPlan? PlanPreparedStartupRemoval()
    {
        lock(sync)
        {
            startupSources?.Require(this);
            if(startupSources==null||qualifiedPrepared==null||!IsLivePrepared(qualifiedPrepared)||preparedIO==null)
                throw new InstallationRecoveryException("Original qualified live Prepared is required for startup removal planning.");
            RequirePreparedPreparation(preparedIO);
            var plan=qualifiedPrepared.Plan.Startup??throw new InstallationRecoveryException("Startup ownership was not prepared.");
            return plan.TargetOwner==null?null:new(plan.Target,plan.TargetOwner);
        }
    }
}

internal sealed partial class InstallationInstallerEntry
{
    // Reference capability, never encoded, read from disk or supplied as a trusted flag. The original Entry must still be alive.
    internal sealed class StartupSources
    {
        private readonly InstallationInstallerEntry entry;
        private readonly InstallationInstallerPins pins;
        private InstallationOperation? operation;
        private InstallationDeploymentScope? scope;
        private StartupSources(InstallationInstallerEntry entry,InstallationInstallerPins pins){this.entry=entry;this.pins=pins;}
        internal static StartupSources FromOriginal(InstallationInstallerEntry entry)
        {
            if(!entry.started||entry.OriginalWork.IsCompleted||entry.verifiedSources==null)throw new InstallationRecoveryException("Original pinned installer work is required.");
            return new(entry,entry.verifiedSources);
        }
        internal void Bind(InstallationOperation actual)
        {
            if(operation!=null||!ReferenceEquals(entry.Operation,actual)||entry.OriginalWork.IsCompleted)throw new InstallationRecoveryException("Startup sources cannot be rebound.");operation=actual;
        }
        internal void BindScope(InstallationDeploymentScope actual,string generation,string bootstrap)
        {
            if(scope!=null||operation==null||!ReferenceEquals(operation.Scope,actual)||!ReferenceEquals(entry.Operation,operation)||entry.OriginalWork.IsCompleted||generation!=pins.Generation||bootstrap!=pins.Bootstrap)
                throw new InstallationRecoveryException("Different original startup preparation owner.");scope=actual;
        }
        internal void Require(InstallationDeploymentScope actual)
        {
            if(!ReferenceEquals(scope,actual)||operation==null||!ReferenceEquals(operation.Scope,actual)||!ReferenceEquals(entry.Operation,operation)||entry.OriginalWork.IsCompleted)
                throw new InstallationRecoveryException("Original installer source authority has retired.");
        }
    }
}

internal static class InstallationStartupRegistration
{
    internal const int OwnerLimit=8192;
    internal static string OwnerPath(string root)=>Path.Combine(root,"startup-owner.json");
    private static string Command(string directory)
    {
        string path=Path.Combine(directory,"KuaiYi.Launcher.exe");InstallationPaths.Check(path);
        if(path.IndexOfAny(['"','\r','\n','\0'])>=0)throw new InstallationRecoveryException("Startup executable cannot be encoded exactly.");return "\""+path+"\"";
    }
    internal static void Validate(InstallationStartupValue value)
    {
        if(value==null||!value.Exists&&(value.Kind!=null||value.Value!=null)||value.Exists&&(value.Kind!="REG_SZ"||value.Value==null||value.Value.Length>4096||value.Value.IndexOfAny(['\r','\n','\0'])>=0))
            throw new InstallationRecoveryException("Unknown named startup value retained.");
    }
    internal static InstallationStartupPlan Prepare(InstallationDeploymentScope scope,PreparedPreparationContext context,InstallationPreparationIO io,InstallationGenerationCandidate generation,InstallationBootstrapCandidate bootstrap,PreparedBootstrapPlan target)
    {
        scope.RequireStartupPreparation(context,io);
        if(bootstrap.Mode!=InstallationBootstrapPackage.SelfContainedMode)throw new InstallationRecoveryException("Development bootstrap cannot qualify for startup.");
        var before=context.Before.Registry.Startup??throw new InstallationRecoveryException("Original named startup snapshot missing.");Validate(before);
        string? prior=context.Before.StartupOwner;
        if(prior==null)
        {
            if(before.Exists)throw new InstallationRecoveryException("Existing startup value has no original ownership.");
            return new(before,new(false),null,null); // No implicit opt-in on first installation.
        }
        var owner=ReadOwner(Convert.FromBase64String(prior));
        var oldActive=context.Before.BootstrapActive??throw new InstallationRecoveryException("Startup owner has no original bootstrap.");
        var oldStore=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.StoreOwner>(Convert.FromBase64String(context.Before.BootstrapOwner??throw new InstallationRecoveryException("Startup store owner missing.")));
        var old=InstallationBootstrapDeployment.RequireReceipt(context.Root,context.Data,oldStore,Convert.FromBase64String(oldActive),CancellationToken.None,io);
        if(old.Mode!=InstallationBootstrapPackage.SelfContainedMode)throw new InstallationRecoveryException("Development startup ownership cannot be adopted.");
        var oldG=InstallationPackage.ReadInstalled(old.Generation.DirectoryPath,CancellationToken.None,expectedDigest:old.Generation.CatalogSha256,io:io);
        if(owner.Owner!=oldStore.Owner||owner.Root!=context.Root||owner.UserData!=context.Data||owner.Generation!=old.Generation||owner.Bundle!=old.Bundle||owner.BootstrapSha256!=old.CatalogSha256||owner.GenerationSourceSha256!=oldG.SourceCatalogSha256||owner.Command!=Command(InstallationBootstrapDeployment.BundlePath(context.Root,old.Bundle))||before!=new InstallationStartupValue(true,"REG_SZ",owner.Command))
            throw new InstallationRecoveryException("Startup value or owner is foreign.");
        var next=owner with{Generation=generation.Version,Bundle=Guid.ParseExact(scope.Bundle,"N"),GenerationSourceSha256=generation.SourceCatalogSha256,BootstrapSha256=bootstrap.CatalogSha256,Command=Command(target.CanonicalDirectory)};
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(next);if(bytes.Length>OwnerLimit)throw new InvalidDataException("Startup ownership exceeds bound.");
        return new(before,new(true,"REG_SZ",next.Command),prior,Convert.ToBase64String(bytes));
    }
    internal static InstallationStartupOwnership ReadOwner(byte[] bytes)
    {
        if(bytes.Length is <1 or >OwnerLimit)throw new InvalidDataException("Startup ownership exceeds bound.");
        var owner=InstallationBootstrapDeployment.Decode<InstallationStartupOwnership>(bytes);
        if(owner.SchemaVersion!=1||owner.Product!="KuaiYi"||owner.Owner==Guid.Empty||owner.Bundle==Guid.Empty||owner.Generation==null||string.IsNullOrEmpty(owner.Root)||string.IsNullOrEmpty(owner.UserData)||string.IsNullOrEmpty(owner.Command)||!InstallationPackage.HashValid(owner.GenerationSourceSha256)||!InstallationPackage.HashValid(owner.BootstrapSha256))throw new InstallationRecoveryException("Unknown startup ownership.");
        InstallationPaths.Check(owner.Root);InstallationPaths.Check(owner.UserData);InstallationPaths.Check(owner.Generation.DirectoryPath);
        Validate(new(true,"REG_SZ",owner.Command));return owner;
    }
    internal static async Task<InstallationDeploymentRegistrationSnapshot> CombineReadAsync(IInstallationDeploymentSnapshotRead original,InstallationDeploymentRegistrationSnapshot slots,bool startup)
    {
        if(!startup)return slots;
        if(original is not IInstallationStartupRegistry registry)throw new InstallationRecoveryException("Original startup registry owner is absent.");
        var value=await registry.ReadStartupAsync().ConfigureAwait(false);Validate(value);return slots with{Startup=value};
    }
}
