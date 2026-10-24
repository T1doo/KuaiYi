using System.Text.Json;
namespace KuaiYi.Core.Installation;

// Immutable build input only. Never load an adjacent pin file, package-provided hash, CLI value or environment override.
internal sealed record InstallationInstallerPins(string Generation,string Bootstrap)
{
    internal static InstallationInstallerPins Read(ReadOnlyMemory<byte> embedded)
    {
        if(embedded.Length is <1 or >4096)throw new InvalidDataException("Installer trust input exceeds bound.");
        using var doc=JsonDocument.Parse(embedded,new(){MaxDepth=3});var root=doc.RootElement;
        InstallationPackage.Fields(root,"schemaVersion","kind","availability","generationSha256","bootstrapSha256");
        if(root.GetProperty("schemaVersion").GetInt32()!=1||root.GetProperty("kind").GetString()!="KuaiYiInstallerSourcePins")throw new InvalidDataException("Unsupported installer trust contract.");
        string? available=root.GetProperty("availability").GetString();var g=root.GetProperty("generationSha256");var b=root.GetProperty("bootstrapSha256");
        if(available=="Unavailable"&&g.ValueKind==JsonValueKind.Null&&b.ValueKind==JsonValueKind.Null)throw new InstallationRecoveryException("Complete qualified Windows payload and reviewed embedded source pins are unavailable.");
        if(available!="Pinned"||g.ValueKind!=JsonValueKind.String||b.ValueKind!=JsonValueKind.String||!InstallationPackage.HashValid(g.GetString()!)||!InstallationPackage.HashValid(b.GetString()!))throw new InvalidDataException("Two fixed original source pins are required.");
        return new(g.GetString()!,b.GetString()!);
    }
}
internal sealed record InstallationInstallerSettlement(int ExitCode,InstallationOperationOutcome? Stage,bool CleanupConfirmed,IReadOnlyList<Exception> Errors);

// Bounded command adapter. Completion is a stage result; the caller must keep OriginalWork alive through actual exit.
internal sealed partial class InstallationInstallerEntry
{
    private static readonly HashSet<InstallationInstallerEntry> Retained=[];
    private readonly TaskCompletionSource begin=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<InstallationOperationOutcome> stage=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ReadOnlyMemory<byte> embedded;
    private readonly string root,data,generation,bootstrap;
    private readonly IInstallationDeploymentPublicationRegistry registry;
    private readonly Func<InstallationOperationSession,Task<InstallationUpgradeCoordinator>> factory;
    private readonly Func<InstallationOperationOutcome,Task> report;
    private readonly CancellationToken cancellation;
    private readonly InstallationOperationFaults? faults;
    private bool started;
    private InstallationInstallerPins? verifiedSources;
    internal InstallationOperation? Operation {get;private set;}
    internal Task<InstallationOperationOutcome> Completion=>stage.Task;
    internal Task<InstallationInstallerSettlement> OriginalWork {get;}
    internal InstallationInstallerEntry(ReadOnlyMemory<byte> embedded,string root,string data,string generation,string bootstrap,
        IInstallationDeploymentPublicationRegistry registry,Func<InstallationOperationSession,Task<InstallationUpgradeCoordinator>> factory,
        Func<InstallationOperationOutcome,Task> report,CancellationToken cancellation=default,InstallationOperationFaults? faults=null)
    {
        this.embedded=embedded.ToArray();this.root=root;this.data=data;this.generation=generation;this.bootstrap=bootstrap;
        this.registry=registry;this.factory=factory;this.report=report;this.cancellation=cancellation;this.faults=faults;
        OriginalWork=Task.Run(RunAsync);
    }
    internal Task<InstallationOperationOutcome> Start()
    {
        lock(begin){if(started)throw new InvalidOperationException("One installer invocation per original owner.");started=true;lock(Retained)Retained.Add(this);begin.SetResult();return Completion;}
    }
    private async Task<InstallationInstallerSettlement> RunAsync()
    {
        await begin.Task.ConfigureAwait(false);var errors=new List<Exception>();InstallationOperationOutcome? value=null;bool clean=true;
        try
        {
            var pins=InstallationInstallerPins.Read(embedded);verifiedSources=pins;cancellation.ThrowIfCancellationRequested();
            Operation=new(root,data,generation,pins.Generation,bootstrap,pins.Bootstrap,registry,factory,(_,_)=>Task.CompletedTask,cancellation,faults,initializeMissingRoot:true);
            if(registry is IInstallationStartupRegistry)Operation.BindInstallerStartup(StartupSources.FromOriginal(this));
            value=await Operation.Start().ConfigureAwait(false);stage.TrySetResult(value);
            // A broken status sink must never abandon the original session, roll back a successful READY, or grant replay.
            try{await report(value).ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
            var settled=await Operation.OriginalWork.ConfigureAwait(false);errors.AddRange(settled.Errors);
            errors.AddRange(await Operation.JoinInstallerOriginalsAsync().ConfigureAwait(false));
            clean=settled.CleanupConfirmed&&Operation.CleanupConfirmed;
        }
        catch(Exception error){errors.Add(error);stage.TrySetException(error);_=stage.Task.Exception;clean=Operation==null;}
        int exit=value?.Status switch{InstallationOperationStatus.NewVersionReady when clean&&errors.Count==0=>0,InstallationOperationStatus.PreviousVersionReady=>4,
            InstallationOperationStatus.RestoredNoPrevious or InstallationOperationStatus.RestoredPreviousNotReady=>5,InstallationOperationStatus.NotCommitted=>6,_=>3};
        if(!clean||value?.Status==InstallationOperationStatus.Unconfirmed)lock(Retained)Retained.Add(this);else lock(Retained)Retained.Remove(this);
        return new(exit,value,clean,errors.ToArray());
    }
}
