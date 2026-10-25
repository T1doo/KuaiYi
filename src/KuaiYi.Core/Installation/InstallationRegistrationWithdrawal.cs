using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace KuaiYi.Core.Installation;

internal enum InstallationWithdrawalStatus { Refused,AbortedRestored,ReferencesWithdrawn,ReferencesWithdrawnReceiptUncertain,AlreadyWithdrawnReadOnly,Unconfirmed,PreflightObservedReadOnly,PreflightCancelled }
internal sealed record InstallationWithdrawalOutcome(InstallationWithdrawalStatus Status,bool CleanupConfirmed,IReadOnlyList<Exception> Errors);
internal sealed class InstallationWithdrawalFaults
{
    internal InstallationAdmissionFaults? Admission{get;init;}
    internal InstallationPreparationFaults? IO{get;init;}
    internal Action<int,bool>? AfterEffect{get;init;}
    internal Action<string,string>? Move{get;init;}
    internal Action? BeforeClosing{get;init;}
    internal Action? AfterClosing{get;init;}
    internal Action<string,string>? Archive{get;init;}
}
// One original invocation, source qualification and exclusive admission. No CLI, process adoption or payload deletion.
internal sealed partial class InstallationRegistrationWithdrawal
{
    private sealed record Bindings(string? Current,string? BootstrapOwner,string? BootstrapActive,string? HostReceipt,string? StartupOwner,InstallationDeploymentRegistrationSnapshot Registry);
    private sealed record Frozen(InstalledVersion Selected,Guid Bundle,string BootstrapRevision,Bindings Before);
    private sealed record Plan(int SchemaVersion,string Kind,string Operation,string TicketSha256,string BeforeSha256,string GenerationSourceSha256,string BootstrapSourceSha256,Frozen Installed,string[] WritePaths);
    private sealed record Terminal(int SchemaVersion,string Kind,string Operation,string Root,string Data,string TicketSha256,string BeforeSha256,string PlanSha256,string GenerationSourceSha256,string BootstrapSourceSha256,int Prefix,int Attempt,InstallationWithdrawalStatus Status);
    private sealed class Qualified(InstallationRegistrationWithdrawal owner,Plan plan,byte[] bytes)
    {internal readonly InstallationRegistrationWithdrawal Owner=owner;internal readonly Plan Plan=plan;internal readonly byte[] Bytes=bytes;}
    private static readonly HashSet<InstallationRegistrationWithdrawal> Retained=[];
    private readonly object sync=new();
    private readonly TaskCompletionSource begin=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ReadOnlyMemory<byte> embedded;
    private readonly string root,data,pending,beforePath,planPath,terminalPath,backup,finished,aborted;
    private readonly InstallationStore store;
    private readonly InstallationAdmission admission;
    private readonly InstallationPreparationIO io;
    private readonly IInstallationStartupRegistry registry;
    private readonly CancellationToken cancellation;
    private readonly InstallationWithdrawalFaults? faults;
    private readonly List<Task> actions=[];
    private Task<IReadOnlyList<Exception>>? settlement;
    private InstallationInstallerPins? pins;
    private Qualified? qualified;
    private InstallationPreparationIO? intents,effects;
    private byte[]? ticket,beforeBytes;
    private Frozen? frozen;
    private bool registryStarted,started,closing,abort,unknown,ticketAttempted,ticketComplete,beforeComplete,planComplete;
    private int prefix,attempt;
    internal string Operation{get;}=Guid.NewGuid().ToString("N");
    internal Task<InstallationWithdrawalOutcome> OriginalWork{get;}
    private bool RegistryCleanupConfirmed=>!registryStarted||registry.CleanupConfirmed;
    private bool ResourcesConfirmed=>admission.CleanupConfirmed&&io.CleanupConfirmed&&RegistryCleanupConfirmed&&actions.All(x=>x.IsCompleted);
    internal bool CleanupConfirmed=>OriginalWork.IsCompleted&&ResourcesConfirmed&&(settlement==null||settlement.IsCompleted);
    internal InstallationRegistrationWithdrawal(ReadOnlyMemory<byte> originalEmbeddedPins,string root,string data,IInstallationStartupRegistry registry,CancellationToken cancellation=default,InstallationWithdrawalFaults? faults=null)
        :this(originalEmbeddedPins,root,data,registry,cancellation,faults,null){}
    private InstallationRegistrationWithdrawal(ReadOnlyMemory<byte> originalEmbeddedPins,string root,string data,IInstallationStartupRegistry registry,CancellationToken cancellation,InstallationWithdrawalFaults? faults,InstallationMaintenanceIntent? inspection)
    {
        this.embedded=originalEmbeddedPins.ToArray();this.root=InstallationPaths.Root(root);this.data=InstallationPaths.Root(data);this.registry=registry;this.cancellation=cancellation;this.faults=faults;this.inspection=inspection;
        store=new(this.root,this.data,()=>true);admission=InstallationAdmission.CreateOwner(this.root,faults?.Admission);io=InstallationPreparationIO.ReadOnly(this.root,faults?.IO);
        pending=store.PendingPath;beforePath=Path.Combine(this.root,"withdrawal-before-"+Operation+".json");planPath=Path.Combine(this.root,"withdrawal-plan-"+Operation+".json");
        terminalPath=Path.Combine(this.root,"withdrawal-terminal-"+Operation+".tmp");backup=Path.Combine(this.root,"withdrawal-ticket-"+Operation+".json");finished=Path.Combine(this.root,"withdrawal-finished-"+Operation+".json");aborted=Path.Combine(this.root,"withdrawal-aborted-"+Operation+".json");
        // The exact owner and task exist before any source read, handle acquisition or platform operation.
        OriginalWork=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);return inspection==null?await RunAsync().ConfigureAwait(false):await RunPreflightAsync().ConfigureAwait(false);});
        if(inspection!=null)preflightWork=ReportPreflightAsync();
    }
    internal Task<InstallationWithdrawalOutcome> Start()
    {
        lock(sync)
        {
            if(inspection!=null)throw new InvalidOperationException("A read-only maintenance observation cannot start withdrawal.");
            StartOriginal();return OriginalWork;
        }
    }
    private void StartOriginal(){if(!started){started=true;lock(Retained)Retained.Add(this);begin.SetResult();}}
    internal void Abort(){lock(sync)if(!closing)abort=true;}
    private void CheckCancellation(){lock(sync)if(abort||cancellation.IsCancellationRequested)throw new OperationCanceledException("Withdrawal canceled before Closing.",cancellation);}
    private static byte[] Encode<T>(T value)=>JsonSerializer.SerializeToUtf8Bytes(value);
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    private static byte[]? Decode(string? value)=>value==null?null:Convert.FromBase64String(value);
    private static bool HashEqual(string? a,string? b)=>string.Equals(a,b,StringComparison.OrdinalIgnoreCase);
    private byte[]? Optional(string path,int maximum)
    {try{return io.ReadDocument(path,maximum);}catch(FileNotFoundException){io.RequireAbsent(path);return null;}}
    private static string? B64(byte[]? value)=>value==null?null:Convert.ToBase64String(value);
    private async Task<InstallationDeploymentRegistrationSnapshot> ReadSlots(bool initial=false,bool preparation=false)
    {
        registryStarted=true;var errors=new List<Exception>();InstallationDeploymentRegistrationSnapshot? slots=null;
        try
        {
            slots=initial?await registry.CaptureAsync().ConfigureAwait(false):preparation?await registry.ReadForPreparationAsync().ConfigureAwait(false):await registry.ReadPublishedAsync().ConfigureAwait(false);
            slots=await InstallationStartupRegistration.CombineReadAsync(registry,slots,true).ConfigureAwait(false);
        }
        catch(Exception error){errors.Add(error);}
        try{await registry.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){unknown=true;errors.Add(error);}
        if(!registry.CleanupConfirmed){unknown=true;errors.Add(new IOException("Original withdrawal registry handles remain unsettled."));}
        if(registry.ConflictObserved)unknown=true;
        InstallationErrors.Throw(errors);return slots!;
    }
    private Bindings ReadBindings(InstallationDeploymentRegistrationSnapshot slots)=>new(B64(Optional(Path.Combine(root,"current.json"),4096)),B64(Optional(InstallationBootstrapDeployment.OwnerPath(root),4096)),B64(Optional(InstallationBootstrapDeployment.ActivePath(root),8192)),B64(Optional(Path.Combine(root,"host-registration.json"),8192)),B64(Optional(InstallationStartupRegistration.OwnerPath(root),8192)),slots);
    private Frozen Qualify(Bindings b)
    {
        var current=Decode(b.Current)??throw new InstallationRecoveryException("No current generation to qualify.");
        var selected=InstallationStore.ReadObservedCurrent(root,current,CancellationToken.None,io);
        var g=InstallationPackage.ReadInstalled(selected.DirectoryPath,CancellationToken.None,expectedDigest:selected.CatalogSha256,io:io);
        if(g.Schema!=3||!HashEqual(g.SourceCatalogSha256,pins!.Generation))throw new InstallationRecoveryException("Current generation is not this invocation's pinned source.");
        var owner=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.StoreOwner>(Decode(b.BootstrapOwner)??throw new InstallationRecoveryException("Original bootstrap owner absent."));
        var active=InstallationBootstrapDeployment.RequireReceipt(root,data,owner,Decode(b.BootstrapActive)??throw new InstallationRecoveryException("Original bootstrap active absent."),CancellationToken.None,io);
        if(active.Generation!=selected||active.Mode!=InstallationBootstrapPackage.SelfContainedMode||!HashEqual(active.CatalogSha256,pins.Bootstrap))throw new InstallationRecoveryException("Current self-contained bootstrap is not pinned to this generation.");
        InstallationHostOwnership? h=null;
        if(b.HostReceipt!=null)
        {
            h=InstallationBootstrapDeployment.Decode<InstallationHostOwnership>(Decode(b.HostReceipt)!);
            if(h.SchemaVersion!=1||h.Product!="KuaiYi"||h.Owner==Guid.Empty||h.Version!=selected||h.Manifest!=Path.Combine(selected.DirectoryPath,InstallationPayload.HostManifestName))throw new InstallationRecoveryException("Original Host ownership differs.");
        }
        foreach(var slot in new[]{b.Registry.Chrome,b.Registry.Edge})
            if(slot.ForeignContent||slot.Value!=h?.Manifest||!slot.KeyExists&&slot.Value!=null)throw new InstallationRecoveryException("Host observations do not establish original ownership.");
        var value=b.Registry.Startup??throw new InstallationRecoveryException("Original startup observation absent.");InstallationStartupRegistration.Validate(value);
        if(b.StartupOwner==null){if(value.Exists)throw new InstallationRecoveryException("Startup value has no original owner.");}
        else
        {
            var s=InstallationStartupRegistration.ReadOwner(Decode(b.StartupOwner)!);string command=InstallationStartupRegistration.Command(InstallationBootstrapDeployment.BundlePath(root,active.Bundle));
            if(command.Length>260)throw new InstallationRecoveryException("Original Run command cannot be restored within the Windows bound.");
            _=new UnicodeEncoding(false,false,true).GetByteCount(command);
            if(s.Owner!=owner.Owner||s.Root!=root||s.UserData!=data||s.Generation!=selected||s.Bundle!=active.Bundle||!HashEqual(s.GenerationSourceSha256,pins.Generation)||!HashEqual(s.BootstrapSha256,pins.Bootstrap)||s.Command!=command||value!=new InstallationStartupValue(true,"REG_SZ",command))throw new InstallationRecoveryException("Startup owner is not this pinned installed closure.");
        }
        return new(selected,active.Bundle,active.Revision,b);
    }
    private async Task<InstallationWithdrawalOutcome> RunAsync()
    {
        List<Exception> errors=[];bool withdrawn=false,already=false;
        try
        {
            pins=InstallationInstallerPins.Read(embedded);CheckCancellation();
            store.AcquireAdmission(admission,mutation:true,existing:true,requireStopped:true,keepSetup:true);
            io.RequireExactMetadata(Path.Combine(root,"owner.txt"),InstallationStore.InitialOwnerBytes,4096);InstallationPaths.Check(data);if(!Directory.Exists(data))throw new InstallationRecoveryException("Original user data directory absent.");
            var probe=ReadBindings(await ReadSlots(initial:true).ConfigureAwait(false));
            if(probe.Current==null)already=await VerifyAlreadyAsync(probe).ConfigureAwait(false);
            else
            {
                frozen=Qualify(probe);CheckCancellation();
                ticket=Encode(new{schemaVersion=1,kind="KuaiYiRegistrationWithdrawalTicket",operation=Operation,root,data,generationSha256=pins.Generation,bootstrapSha256=pins.Bootstrap});
                if(ticket.Length>16384)throw new InvalidDataException("Withdrawal ticket exceeds bound.");
                var intentPaths=new[]{pending,beforePath,planPath,terminalPath,backup,finished,aborted};foreach(var path in intentPaths)io.RequireAbsent(path);
                intents=io.CreatePublicationChannel(root,intentPaths,faults?.IO);ticketAttempted=true;intents.WritePublicationMetadata(pending,ticket,16384);ticketComplete=true;admission.ReleaseSetup();
                var before=ReadBindings(await ReadSlots(preparation:true).ConfigureAwait(false));if(before!=probe)throw new InstallationRecoveryException("Original qualification observations changed after ticket.");
                frozen=Qualify(before);beforeBytes=Encode(before);intents.WritePublicationMetadata(beforePath,beforeBytes,131072);beforeComplete=true;
                var plan=new Plan(1,"KuaiYiRegistrationWithdrawalPlan",Operation,Hash(ticket),Hash(beforeBytes),pins.Generation,pins.Bootstrap,frozen,WritePaths(Operation));
                byte[] planBytes=Encode(plan);if(planBytes.Length>131072)throw new InvalidDataException("Withdrawal plan exceeds bound.");
                for(int n=0;n<=7;n++)for(int a=0;a<=7;a++)foreach(var status in new[]{InstallationWithdrawalStatus.ReferencesWithdrawn,InstallationWithdrawalStatus.AbortedRestored})
                    if(Encode(TerminalFor(plan,planBytes,n,a,status)).Length>16384)throw new InvalidDataException("Withdrawal terminal variant exceeds bound.");
                intents.WritePublicationMetadata(planPath,planBytes,131072);qualified=new(this,plan,planBytes);planComplete=true;
                effects=io.CreatePublicationChannel(root,plan.WritePaths,faults?.IO);foreach(string path in Tombstones(Operation))io.RequireAbsent(path);
                for(int step=1;step<=7;step++)
                {
                    CheckCancellation();await CheckPrefixAsync(true).ConfigureAwait(false);attempt=step;
                    await OwnStep(step,false).ConfigureAwait(false);await ResolveAttemptAsync(false).ConfigureAwait(false);
                }
                await CheckPrefixAsync(true).ConfigureAwait(false);faults?.BeforeClosing?.Invoke();
                lock(sync){CheckCancellation();closing=true;withdrawn=true;}
            }
        }
        catch(Exception error){if(!io.CleanupConfirmed||!RegistryCleanupConfirmed||io.StorageUnconfirmed||(registryStarted&&registry.ConflictObserved))unknown=true;errors.Add(error);}
        if(!closing&&!already&&qualified!=null&&!unknown&&!io.StorageUnconfirmed&&!registry.ConflictObserved)
        {
            try
            {
                await ResolveAttemptAsync(false).ConfigureAwait(false);
                while(prefix>0){await CheckPrefixAsync(true).ConfigureAwait(false);attempt=prefix;await OwnStep(prefix,true).ConfigureAwait(false);await ResolveAttemptAsync(true).ConfigureAwait(false);}
                await CheckPrefixAsync(true).ConfigureAwait(false);
            }
            catch(Exception error){unknown=true;errors.Add(error);}
        }
        lock(sync)closing=true;
        try{faults?.AfterClosing?.Invoke();}catch(Exception error){unknown=true;errors.Add(error);}
        foreach(var action in actions)try{await action.ConfigureAwait(false);}catch(Exception error){if(!errors.Any(e=>ReferenceEquals(e,error)))errors.Add(error);}
        await CloseOriginals(errors).ConfigureAwait(false);
        bool canArchive=ticketComplete&&beforeComplete&&planComplete&&qualified!=null&&!unknown&&!io.StorageUnconfirmed&&!registry.ConflictObserved&&ResourcesConfirmed&&attempt==0&&prefix==(withdrawn?7:0);
        if(canArchive)
        {
            try{await CheckPrefixAsync(false).ConfigureAwait(false);}catch(Exception error){unknown=true;errors.Add(error);canArchive=false;}
            await CloseOriginals(errors).ConfigureAwait(false);canArchive&=!unknown&&ResourcesConfirmed;
        }
        if(canArchive)
        {
            bool moveAttempted=false;string destination=withdrawn?finished:aborted;byte[]? terminal=null;
            try
            {
                terminal=Encode(TerminalFor(qualified!.Plan,qualified.Bytes,prefix,0,withdrawn?InstallationWithdrawalStatus.ReferencesWithdrawn:InstallationWithdrawalStatus.AbortedRestored));
                intents!.WritePublicationMetadata(terminalPath,terminal,16384);io.RequireExactMetadata(pending,ticket!,16384);io.RequireExactMetadata(terminalPath,terminal,16384);io.RequireAbsent(backup);io.RequireAbsent(destination);
                File.Replace(terminalPath,pending,backup,false);io.RequireExactMetadata(pending,terminal,16384);io.RequireExactMetadata(backup,ticket!,16384);
                if(!ResourcesConfirmed||io.StorageUnconfirmed)throw new InstallationRecoveryException("Terminal resources are unsettled.");
                moveAttempted=true;if(faults?.Archive!=null)faults.Archive(pending,destination);else File.Move(pending,destination);
                // No fallible IO, callback or cleanup follows a successful archive Move.
                lock(Retained)Retained.Remove(this);return new(withdrawn?InstallationWithdrawalStatus.ReferencesWithdrawn:InstallationWithdrawalStatus.AbortedRestored,true,errors.ToArray());
            }
            catch(Exception error)
            {
                errors.Add(error);
                if(withdrawn&&moveAttempted&&terminal!=null)
                {
                    try
                    {
                        io.RequireAbsent(pending);io.RequireExactMetadata(destination,terminal,16384);io.RequireExactMetadata(backup,ticket!,16384);
                        if(ResourcesConfirmed&&!io.StorageUnconfirmed){lock(Retained)Retained.Remove(this);return new(InstallationWithdrawalStatus.ReferencesWithdrawnReceiptUncertain,true,errors.ToArray());}
                    }
                    catch(Exception observation){errors.Add(observation);}
                }
            }
        }
        if(!ticketAttempted&&ResourcesConfirmed&&!unknown)
        {lock(Retained)Retained.Remove(this);return new(already&&errors.Count==0?InstallationWithdrawalStatus.AlreadyWithdrawnReadOnly:InstallationWithdrawalStatus.Refused,true,errors.ToArray());}
        return new(InstallationWithdrawalStatus.Unconfirmed,ResourcesConfirmed,errors.ToArray());
    }
    private async Task CloseOriginals(List<Exception> errors)
    {
        try{io.CloseOriginals();}catch(Exception error){unknown=true;errors.Add(error);}
        if(registryStarted)try{await registry.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){unknown=true;errors.Add(error);}
        // Even before a ticket exists, the exact admission fences every unsettled original owner.
        if(io.CleanupConfirmed&&RegistryCleanupConfirmed&&actions.All(x=>x.IsCompleted))
            try{admission.Dispose();}catch(Exception error){unknown=true;errors.Add(error);}
        if(!ResourcesConfirmed)unknown=true;
    }
    internal Task<IReadOnlyList<Exception>> SettleOriginalsAsync()
    {
        lock(sync)
        {
            if(!OriginalWork.IsCompleted)throw new InstallationBusyException();
            return settlement??=Task.Run(async()=>{List<Exception> errors=[];await CloseOriginals(errors).ConfigureAwait(false);return (IReadOnlyList<Exception>)errors;});
        }
    }
    private Terminal TerminalFor(Plan plan,byte[] bytes,int count,int trying,InstallationWithdrawalStatus status)=>new(1,"KuaiYiRegistrationWithdrawalTerminal",Operation,root,data,plan.TicketSha256,plan.BeforeSha256,Hash(bytes),pins!.Generation,pins.Bootstrap,count,trying,status);
    private async Task<bool> VerifyAlreadyAsync(Bindings actual)
    {
        if(actual.Current!=null||actual.BootstrapActive!=null||actual.HostReceipt!=null||actual.StartupOwner!=null||actual.Registry.Startup!=new InstallationStartupValue(false)||actual.Registry.Chrome.ForeignContent||actual.Registry.Edge.ForeignContent||actual.Registry.Chrome.Value!=null||actual.Registry.Edge.Value!=null)
            throw new InstallationRecoveryException("Missing current with nonempty registration references is not an owned withdrawal.");
        var entries=io.Entries(root).Where(p=>Path.GetFileName(p).StartsWith("withdrawal-finished-",StringComparison.Ordinal)&&Path.GetFileName(p).EndsWith(".json",StringComparison.Ordinal)).ToArray();
        if(entries.Length!=1)throw new InstallationRecoveryException("One independently verifiable withdrawal history is required.");
        var terminal=InstallationBootstrapDeployment.Decode<Terminal>(io.ReadDocument(entries[0],16384));
        if(terminal.SchemaVersion!=1||terminal.Kind!="KuaiYiRegistrationWithdrawalTerminal"||!Guid.TryParseExact(terminal.Operation,"N",out _)||entries[0]!=Path.Combine(root,"withdrawal-finished-"+terminal.Operation+".json")||terminal.Root!=root||terminal.Data!=data||terminal.Prefix!=7||terminal.Attempt!=0||terminal.Status!=InstallationWithdrawalStatus.ReferencesWithdrawn||!HashEqual(terminal.GenerationSourceSha256,pins!.Generation)||!HashEqual(terminal.BootstrapSourceSha256,pins.Bootstrap))throw new InstallationRecoveryException("Withdrawal history cannot establish even read-only status.");
        byte[] priorBefore=io.ReadDocument(Path.Combine(root,"withdrawal-before-"+terminal.Operation+".json"),131072),priorPlan=io.ReadDocument(Path.Combine(root,"withdrawal-plan-"+terminal.Operation+".json"),131072),priorTicket=io.ReadDocument(Path.Combine(root,"withdrawal-ticket-"+terminal.Operation+".json"),16384);
        if(Hash(priorBefore)!=terminal.BeforeSha256||Hash(priorPlan)!=terminal.PlanSha256||Hash(priorTicket)!=terminal.TicketSha256)throw new InstallationRecoveryException("Historical data hashes differ.");
        var plan=InstallationBootstrapDeployment.Decode<Plan>(priorPlan);var prior=InstallationBootstrapDeployment.Decode<Bindings>(priorBefore);
        if(plan.SchemaVersion!=1||plan.Kind!="KuaiYiRegistrationWithdrawalPlan"||plan.Operation!=terminal.Operation||plan.TicketSha256!=terminal.TicketSha256||plan.BeforeSha256!=terminal.BeforeSha256||!HashEqual(plan.GenerationSourceSha256,pins.Generation)||!HashEqual(plan.BootstrapSourceSha256,pins.Bootstrap)||plan.Installed.Before!=prior||!plan.WritePaths.SequenceEqual(WritePaths(terminal.Operation)))throw new InstallationRecoveryException("Historical plan is not the exact closed data set.");
        var checkedInstalled=Qualify(prior);if(checkedInstalled!=plan.Installed||actual.BootstrapOwner!=prior.BootstrapOwner)throw new InstallationRecoveryException("Retained installed bytes are not independently pinned.");
        var confirm=ReadBindings(await ReadSlots(preparation:true).ConfigureAwait(false));if(confirm!=actual)throw new InstallationRecoveryException("Empty references changed during read-only confirmation.");
        // No Qualified object, write channel or ticket is created on this branch. Disk history grants no mutation.
        return true;
    }
}
