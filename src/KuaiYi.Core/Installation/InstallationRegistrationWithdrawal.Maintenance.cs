using System.Security.Cryptography;
namespace KuaiYi.Core.Installation;
internal sealed partial class InstallationRegistrationWithdrawal
{
    private sealed record MaintenanceDirectory(string Original,string Staging,string Retired,InstallationFile[] Before,InstallationFile[] Expected);
    private readonly bool maintenanceExecution;
    private readonly string? maintenanceSource;
    private InstallationPackage? maintenanceGeneration,maintenanceBootstrap;
    private string? maintenanceBootstrapRevision;
    private InstallationPreparationIO? maintenanceIO;
    private byte[]? maintenanceFilePlan;
    private MaintenanceDirectory? maintenanceG,maintenanceB;
    private bool maintenanceCommitLocked,maintenanceCancelled,maintenanceReferencesCommitted,maintenanceCandidateAttempted;
    private bool maintenanceMutationBegan,maintenancePayloadReady,maintenanceRetired,maintenanceRepaired;
    private readonly List<InstallationMaintenanceAction> maintenanceCompleted=[];
    private readonly List<byte[]> maintenanceMoveIntents=[];
    private string MaintenanceMovePath(int index)=>Path.Combine(root,$"maintenance-move-{Operation}-{index}.json");
    private string MaintenanceFinishedPath=>Path.Combine(root,"maintenance-finished-"+Operation+".json");
    private string MaintenanceFilePlanPath=>Path.Combine(root,"maintenance-files-"+Operation+".json");
    private bool RepairingDamagedFiles=>maintenanceExecution&&inspection==InstallationMaintenanceIntent.Repair&&!maintenancePayloadReady;
    internal static InstallationRegistrationWithdrawal ForMaintenanceExecution(ReadOnlyMemory<byte> originalEmbeddedPins,string root,string data,
        IInstallationStartupRegistry registry,InstallationMaintenanceIntent intent,string fixedRepairSource,CancellationToken cancellation=default,InstallationWithdrawalFaults? faults=null)
    {
        if(intent is not(InstallationMaintenanceIntent.Repair or InstallationMaintenanceIntent.Uninstall))throw new ArgumentOutOfRangeException(nameof(intent));
        return new(originalEmbeddedPins,root,data,registry,cancellation,faults,intent,fixedRepairSource,true);
    }
    internal Task<InstallationMaintenanceOutcome> StartMaintenance()
    {lock(sync){if(!maintenanceExecution)throw new InstallationRecoveryException("A read-only observation is not executable maintenance.");StartOriginal();return preflightWork!;}}
    private void ReadMaintenanceSources()
    {
        if(inspection!=InstallationMaintenanceIntent.Repair){maintenanceIO=io.CreateMaintenanceChannel(root,root,[]);return;}
        string source=InstallationPaths.Root(maintenanceSource??throw new InstallationRecoveryException("Fixed repair source absent."));
        if(InstallationPaths.Contains(root,source)||InstallationPaths.Contains(source,root)||InstallationPaths.Contains(data,source)||InstallationPaths.Contains(source,data))throw new InstallationRecoveryException("Repair source must be separate from installation and user data.");
        maintenanceIO=io.CreateMaintenanceChannel(root,source,[]);
        string generation=Path.Combine(source,"generation");maintenanceGeneration=InstallationPackage.ReadSource(generation,pins!.Generation,cancellation,maintenanceIO);
        if(maintenanceGeneration.Schema!=2||maintenanceGeneration.Payload==null)throw new InstallationRecoveryException("Complete independently pinned repair payload required.");
        var b=InstallationBootstrapPackage.Read(Path.Combine(source,"bootstrap"),pins.Bootstrap,new(maintenanceGeneration.Manifest.Version,generation,maintenanceGeneration.CatalogSha256),cancellation,maintenanceIO);
        if(InstallationBootstrapPackage.ModeOf(b.Package)!=InstallationBootstrapPackage.SelfContainedMode)throw new InstallationRecoveryException("Self-contained pinned bootstrap required.");
        maintenanceBootstrap=b.Package;maintenanceBootstrapRevision=b.Revision;
    }
    private InstallationBootstrapDeployment.Receipt RequireRepairBootstrap(InstallationBootstrapDeployment.StoreOwner owner,byte[] bytes,InstalledVersion selected)
    {
        InstallationBootstrapDeployment.RequireOwner(owner,root,data);var value=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.Receipt>(bytes);
        if(value.SchemaVersion!=1||value.Product!="KuaiYi"||value.Mode!=InstallationBootstrapPackage.SelfContainedMode||value.Owner!=owner.Owner||value.Bundle==Guid.Empty||
            value.Generation!=selected||!HashEqual(value.CatalogSha256,pins!.Bootstrap)||value.Revision!=maintenanceBootstrapRevision)throw new InstallationRecoveryException("Repair bootstrap binding differs from the fixed source and original owner.");
        return value; // Validates binding only. Corrupt payload bytes are never used as a trusted repair source.
    }
    private static InstallationFile MetadataFile(string path,byte[] bytes)=>new(path,bytes.Length,Convert.ToHexString(SHA256.HashData(bytes)));
    private static InstallationFile[] Closure(InstallationPackage package)=>package.Manifest.Files.Append(MetadataFile(InstallationPackage.CatalogName,package.Catalog)).OrderBy(f=>f.Path,StringComparer.Ordinal).ToArray();
    private void PrepareMaintenanceFilePlan()
    {
        var selected=frozen!.Selected;InstallationPackage generation,bootstrap;
        if(inspection==InstallationMaintenanceIntent.Repair)
        {
            byte[] catalog=InstallationPayload.InstalledCatalog(maintenanceGeneration!,InstallationPayload.Derived(maintenanceGeneration!,selected.DirectoryPath));generation=InstallationPackage.Parse(catalog);bootstrap=maintenanceBootstrap!;
        }
        else
        {
            generation=InstallationPackage.ReadInstalled(selected.DirectoryPath,cancellation,expectedDigest:selected.CatalogSha256,io:io);
            bootstrap=InstallationBootstrapPackage.Read(InstallationBootstrapDeployment.BundlePath(root,frozen.Bundle),pins!.Bootstrap,selected,cancellation,io).Package;
        }
        MaintenanceDirectory Plan(string original,string name,InstallationFile[] expected)=>new(original,Path.Combine(root,"maintenance-new-"+name+"-"+Operation),Path.Combine(root,"maintenance-retired-"+name+"-"+Operation),
            InstallationMaintenanceFiles.Capture(io,original,expected,inspection==InstallationMaintenanceIntent.Repair),expected);
        maintenanceG=Plan(selected.DirectoryPath,"g",Closure(generation));maintenanceB=Plan(InstallationBootstrapDeployment.BundlePath(root,frozen.Bundle),"b",Closure(bootstrap));
        foreach(var p in new[]{maintenanceG,maintenanceB}){io.RequireAbsent(p.Staging);io.RequireAbsent(p.Retired);}
        maintenanceFilePlan=Encode(new{schemaVersion=1,kind="KuaiYiOwnedMaintenanceFiles",operation=Operation,intent=inspection,root,data,generationSourceSha256=pins!.Generation,bootstrapSourceSha256=pins.Bootstrap,g=maintenanceG,b=maintenanceB});
        if(maintenanceFilePlan.Length>4*1024*1024)throw new InstallationRecoveryException("Maintenance file plan exceeds bound.");
        var paths=new[]{maintenanceG,maintenanceB}.SelectMany(p=>new[]{p.Original,p.Staging,p.Retired}.Concat(p.Expected.Select(f=>Path.Combine(p.Staging,f.Path)))).Concat(Enumerable.Range(1,4).Select(MaintenanceMovePath));
        foreach(string path in Enumerable.Range(1,4).Select(MaintenanceMovePath))io.RequireAbsent(path);
        maintenanceIO=io.CreateMaintenanceChannel(root,inspection==InstallationMaintenanceIntent.Repair?InstallationPaths.Root(maintenanceSource!):root,paths);
        observation=new(selected,frozen.Bundle,frozen.BootstrapRevision,pins.Generation,pins.Bootstrap);
    }
    private void RequireMaintenanceFiles()
    {
        if(!maintenanceExecution||maintenanceFilePlan==null||maintenanceIO==null||unknown||io.StorageUnconfirmed||!io.CleanupConfirmed||
            closing&&(inspection!=InstallationMaintenanceIntent.Uninstall||prefix!=7))throw new InstallationRecoveryException("Original maintenance file phase is unavailable.");
        RequireJournal(false);admission.RequireMutation(root);io.RequireExactMetadata(MaintenanceFilePlanPath,maintenanceFilePlan,4*1024*1024);
        for(int i=0;i<maintenanceMoveIntents.Count;i++)io.RequireExactMetadata(MaintenanceMovePath(i+1),maintenanceMoveIntents[i],16384);
    }
    private void PrepareMaintenanceCandidates()
    {
        if(inspection!=InstallationMaintenanceIntent.Repair)return;RequireMaintenanceFiles();maintenanceCandidateAttempted=true;
        var g=maintenanceG!;var b=maintenanceB!;maintenanceIO!.CreateMaintenanceDirectory(g.Staging);maintenanceIO.CreateMaintenanceDirectory(b.Staging);
        foreach(var file in maintenanceGeneration!.Manifest.Files){CheckCancellation();RequireMaintenanceFiles();maintenanceIO.CopyPayload(Path.Combine(maintenanceSource!,"generation",file.Path),Path.Combine(g.Staging,file.Path),file);}
        foreach(var metadata in InstallationPayload.Derived(maintenanceGeneration,g.Original))maintenanceIO.WriteMetadata(Path.Combine(g.Staging,metadata.Key),metadata.Value,1024*1024);
        byte[] catalog=InstallationPayload.InstalledCatalog(maintenanceGeneration,InstallationPayload.Derived(maintenanceGeneration,g.Original));maintenanceIO.WriteMetadata(Path.Combine(g.Staging,InstallationPackage.CatalogName),catalog,1024*1024);
        foreach(var file in maintenanceBootstrap!.Manifest.Files){CheckCancellation();RequireMaintenanceFiles();maintenanceIO.CopyPayload(Path.Combine(maintenanceSource!,"bootstrap",file.Path),Path.Combine(b.Staging,file.Path),file);}
        maintenanceIO.WriteMetadata(Path.Combine(b.Staging,InstallationPackage.CatalogName),maintenanceBootstrap.Catalog,1024*1024);
        InstallationMaintenanceFiles.Verify(maintenanceIO,g.Staging,g.Expected);InstallationMaintenanceFiles.Verify(maintenanceIO,b.Staging,b.Expected);
        _=InstallationPackage.ReadInstalled(g.Staging,cancellation,g.Original,frozen!.Selected.CatalogSha256,maintenanceIO);
        if(InstallationBootstrapPackage.Read(b.Staging,pins!.Bootstrap,new(frozen.Selected.Version,g.Staging,frozen.Selected.CatalogSha256),cancellation,maintenanceIO).Revision!=frozen.BootstrapRevision)throw new InstallationRecoveryException("Repair staging build mismatch.");
        CheckCancellation();maintenanceCompleted.Add(InstallationMaintenanceAction.PrepareRepairPayload);
    }
    private void MoveMaintenance(MaintenanceDirectory plan,bool replacement,InstallationMaintenanceAction action)
    {
        RequireMaintenanceFiles();string from=replacement?plan.Staging:plan.Original,to=replacement?plan.Original:plan.Retired;
        byte[] intent=Encode(new{schemaVersion=1,kind="KuaiYiMaintenancePlannedMove",operation=Operation,index=maintenanceMoveIntents.Count+1,filesPlanSha256=Hash(maintenanceFilePlan!),from,to,replacement});
        if(intent.Length>16384)throw new InstallationRecoveryException("Maintenance move intent exceeds bound.");
        maintenanceIO!.WriteMetadata(MaintenanceMovePath(maintenanceMoveIntents.Count+1),intent,16384);maintenanceMoveIntents.Add(intent);
        var files=new InstallationMaintenanceFiles(maintenanceIO,RequireMaintenanceFiles,CommitMaintenanceFileMutation);
        files.Move(from,to,replacement?plan.Expected:plan.Before,plan.Expected);
        maintenanceCompleted.Add(action);
    }
    private void CommitMaintenanceFileMutation()
    {
        // All expensive byte/absence validation has completed. Linearize cancellation at the original owner lock.
        lock(sync){if(!maintenanceCommitLocked){if(!maintenanceReferencesCommitted)CheckCancellation();maintenanceCommitLocked=true;}maintenanceMutationBegan=true;}
    }
    private void RepairMaintenanceFiles()
    {
        // Originals, including damaged bytes, remain in exclusive recovery directories. Nothing is overwritten/deleted.
        MoveMaintenance(maintenanceG!,false,InstallationMaintenanceAction.RetireCurrentGeneration);
        MoveMaintenance(maintenanceB!,false,InstallationMaintenanceAction.RetireCurrentBootstrap);
        MoveMaintenance(maintenanceG!,true,InstallationMaintenanceAction.RestoreCurrentGeneration);
        MoveMaintenance(maintenanceB!,true,InstallationMaintenanceAction.RestoreCurrentBootstrap);
        maintenancePayloadReady=true;
        if(Qualify(frozen!.Before)!=frozen)throw new InstallationRecoveryException("Repaired canonical payload does not requalify the original bindings.");
        maintenanceCompleted.Add(InstallationMaintenanceAction.RestorePayload);
    }
    private void RetireMaintenanceFiles()
    {
        MoveMaintenance(maintenanceG!,false,InstallationMaintenanceAction.RetireCurrentGeneration);
        MoveMaintenance(maintenanceB!,false,InstallationMaintenanceAction.RetireCurrentBootstrap);maintenanceRetired=true;
    }
    private async Task CheckMaintenanceRetiredAsync()
    {
        RequireJournal(false);io.RequireExactMetadata(MaintenanceFilePlanPath,maintenanceFilePlan!,4*1024*1024);
        foreach(var p in new[]{maintenanceG!,maintenanceB!}){InstallationMaintenanceFiles.Verify(io,p.Retired,p.Before,p.Expected);io.RequireAbsent(p.Original);}
        foreach(int step in FileSteps){io.RequireAbsent(RecordPath(step));var prior=RecordBefore(step);if(prior!=null)io.RequireExactMetadata(Tombstone(Operation,step),prior,Limit(step));}
        if(await ReadSlots().ConfigureAwait(false)!=ExpectedSlots(7))throw new InstallationRecoveryException("Retired installation references changed.");
    }
    private InstallationWithdrawalStatus CompletedWithdrawalStatus(bool withdrawn)=>maintenanceRepaired?InstallationWithdrawalStatus.MaintenancePayloadRepaired:maintenanceRetired?InstallationWithdrawalStatus.MaintenanceFilesRetired:withdrawn?InstallationWithdrawalStatus.ReferencesWithdrawn:InstallationWithdrawalStatus.AbortedRestored;
}
