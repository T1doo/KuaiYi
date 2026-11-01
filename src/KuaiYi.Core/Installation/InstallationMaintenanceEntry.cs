namespace KuaiYi.Core.Installation;

internal enum InstallationPublicCommand { Install,Repair,Uninstall }
internal static class InstallationCommandLine
{
    internal static InstallationPublicCommand? Parse(IReadOnlyList<string> args)=>args.Count==1?args[0] switch
    {"--install"=>InstallationPublicCommand.Install,"--repair"=>InstallationPublicCommand.Repair,"--uninstall"=>InstallationPublicCommand.Uninstall,_=>null}:null;
}
// Public command adapter, not a new ownership system. No destructive capability is produced by an observation.
internal sealed class InstallationMaintenanceEntry
{
    private static readonly HashSet<InstallationMaintenanceEntry> Retained=[];
    private readonly object sync=new();private readonly TaskCompletionSource begin=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly byte[] embedded;private readonly InstallationMaintenanceIntent intent;private readonly CancellationToken cancellation;
    private readonly Func<InstallationInstallerPins,InstallationMaintenanceIntent,InstallationRegistrationWithdrawal> factory;
    private readonly Func<string> repairSource;
    private InstallationRegistrationWithdrawal? original;private InstallationPreparationIO? sourceIO;private bool started;
    private readonly string operation=Guid.NewGuid().ToString("N");
    internal Task<InstallationMaintenanceOutcome> OriginalWork{get;}
    internal InstallationMaintenanceEntry(ReadOnlyMemory<byte> pins,InstallationMaintenanceIntent intent,
        Func<InstallationInstallerPins,InstallationMaintenanceIntent,InstallationRegistrationWithdrawal> factory,Func<string> repairSource,CancellationToken cancellation=default)
    {
        if(intent is not (InstallationMaintenanceIntent.Repair or InstallationMaintenanceIntent.Uninstall))throw new ArgumentOutOfRangeException(nameof(intent));
        embedded=pins.ToArray();this.intent=intent;this.factory=factory;this.repairSource=repairSource;this.cancellation=cancellation;
        OriginalWork=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);return await RunAsync().ConfigureAwait(false);});
    }
    internal Task<InstallationMaintenanceOutcome> Start()
    {lock(sync){if(!started){started=true;lock(Retained)Retained.Add(this);begin.SetResult();}return OriginalWork;}}
    private async Task<InstallationMaintenanceOutcome> RunAsync()
    {
        List<Exception> errors=[];InstallationMaintenanceOutcome? outcome=null;bool cancelled=false;
        try
        {
            var pins=InstallationInstallerPins.Read(embedded);cancellation.ThrowIfCancellationRequested();
            if(intent==InstallationMaintenanceIntent.Repair)
            {
                // Fixed embedded pins precede even path resolution. Adjacent hashes/receipts never supply trust.
                string source=InstallationPaths.Root(repairSource());sourceIO=InstallationPreparationIO.ReadOnly(source);
                string generation=Path.Combine(source,"generation"),bootstrap=Path.Combine(source,"bootstrap");
                var g=InstallationPackage.ReadSource(generation,pins.Generation,cancellation,sourceIO);
                if(g.Schema!=2||g.Payload==null)throw new InstallationRecoveryException("Repair requires the complete pinned production source, not an installed/development catalog.");
                var b=InstallationBootstrapPackage.Read(bootstrap,pins.Bootstrap,new(g.Manifest.Version,generation,g.CatalogSha256),cancellation,sourceIO);
                if(InstallationBootstrapPackage.ModeOf(b.Package)!=InstallationBootstrapPackage.SelfContainedMode)throw new InstallationRecoveryException("Repair requires the pinned self-contained bootstrap.");
                sourceIO.CloseOriginals();if(!sourceIO.CleanupConfirmed)throw new InstallationRecoveryException("Original repair source resources unsettled.");
            }
            cancellation.ThrowIfCancellationRequested();
            original=factory(pins,intent); // Existing-only admission, owner, pins and original ledger remain authoritative.
            outcome=await original.StartPreflight().ConfigureAwait(false);
        }
        catch(Exception error){cancelled=error is OperationCanceledException&&cancellation.IsCancellationRequested;errors.Add(error);}
        try{sourceIO?.CloseOriginals();}catch(Exception error){errors.Add(error);}
        bool confirmed=(sourceIO?.CleanupConfirmed??true)&&(original?.CleanupConfirmed??true);
        if(outcome!=null)errors.InsertRange(0,outcome.Errors);
        var status=!confirmed?InstallationMaintenanceStatus.Unconfirmed:outcome?.Status??(cancelled?InstallationMaintenanceStatus.Cancelled:InstallationMaintenanceStatus.Refused);
        if(confirmed)lock(Retained)Retained.Remove(this);
        return new(outcome?.Operation??operation,intent,status,status==InstallationMaintenanceStatus.ObservedReadOnly?outcome?.Observation:null,confirmed,outcome?.RemainingSteps??Array.Empty<InstallationMaintenanceStep>(),errors.AsReadOnly());
    }
    internal async Task<IReadOnlyList<Exception>> SettleOriginalsAsync()
    {
        if(!OriginalWork.IsCompleted)throw new InstallationBusyException();List<Exception> errors=[];
        try{sourceIO?.CloseOriginals();}catch(Exception error){errors.Add(error);}
        if(original!=null)try{errors.AddRange(await original.SettleOriginalsAsync().ConfigureAwait(false));}catch(Exception error){errors.Add(error);}
        if((sourceIO?.CleanupConfirmed??true)&&(original?.CleanupConfirmed??true))lock(Retained)Retained.Remove(this);
        return errors.AsReadOnly(); // Settlement cannot replay preflight or turn an observation into deletion/repair authority.
    }
}
