using System.Security.Cryptography;
using System.Text.Json;
namespace KuaiYi.Core.Installation;

// Original scope owns preparation, provisional publication and final settlement.
internal enum InstallationDeploymentStatus { Refused,AbortedBeforeSnapshot,AbortedBeforePrepared,AbortedAfterPrepared,Committed,CommittedReceiptUncertain,Unconfirmed }
internal sealed record InstallationDeploymentOutcome(InstallationDeploymentStatus Status,IReadOnlyList<Exception> Errors);
internal interface IInstallationDeploymentFinalRead
{
    // The object is registered before acquisition; it owns handles before its first possibly failing read.
    Task VerifyAsync();
    Task CloseOriginalsAsync();
    bool CleanupConfirmed {get;}
}
internal sealed record InstallationDeploymentRegistrationSlot(bool KeyExists,string? Value,bool ForeignContent=false);
internal sealed record InstallationDeploymentRegistrationSnapshot(InstallationDeploymentRegistrationSlot Chrome,InstallationDeploymentRegistrationSlot Edge);
internal interface IInstallationDeploymentSnapshotRead:IInstallationDeploymentFinalRead
{
    Task<InstallationDeploymentRegistrationSnapshot> CaptureAsync();
}
internal sealed record InstallationDeploymentBefore(int SchemaVersion,string Kind,string Operation,string TicketSha256,
    string? Current,string? BootstrapOwner,string? BootstrapActive,string? HostReceipt,InstallationDeploymentRegistrationSnapshot Registry);
internal sealed class InstallationDeploymentFaults
{
    internal Action<FileStream,byte[]>? WriteTicket {get;init;}
    internal Action<FileStream,byte[]>? WriteBefore {get;init;}
    internal Action<string,FileStream>? BeforeRead {get;init;}
    internal Action<string>? BeforeSnapshotQuery {get;init;}
    internal Action<string>? BeforeClose {get;set;}
    internal Func<Task>? BeforeClosing {get;init;}
    internal Func<Task>? AfterClosing {get;init;}
    internal Action<string,string>? Archive {get;init;}
}
internal sealed partial class InstallationDeploymentScope
{
    private static readonly HashSet<InstallationDeploymentScope> Retained=[];
    private readonly object sync=new();
    private readonly InstallationStore store;
    private readonly InstallationAdmission admission;
    private readonly InstallationDeploymentFaults? faults;
    private readonly List<IInstallationDeploymentFinalRead> reads=[];
    private readonly List<(string Path,FileStream Original)> files=[];
    private readonly TaskCompletionSource start=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource finish=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task acquisition;
    private readonly Task<InstallationDeploymentOutcome> finalization;
    private Task<IReadOnlyList<Exception>>? settlement;
    private readonly byte[] ticket;
    private readonly string root,userData,generationSourceSha,bootstrapSourceSha,pending,temporary,backup,archive,beforePath;
    private Task<InstallationGenerationCandidate>? generationPreparation;
    private InstallationPreparationIO? generationIO;
    private GenerationPreparationContext? generationContext;
    private InactiveGenerationRetention? generationRetention;
    private Task<InstallationBootstrapCandidate>? bootstrapPreparation;
    private InstallationPreparationIO? bootstrapIO;
    private BootstrapPreparationContext? bootstrapContext;
    private InactiveBootstrapRetention? bootstrapRetention;
    private Task<InstallationDeploymentPrepared>? prepared;
    private InstallationPreparationIO? preparedIO;
    private PreparedPreparationContext? preparedContext;
    private InstallationDeploymentPrepared? qualifiedPrepared;
    private Task<InstallationDeploymentBefore>? snapshot;
    private IInstallationDeploymentSnapshotRead? snapshotOwner;
    private byte[]? before;
    private bool finishing,beforeAttempted,beforeComplete;
    private readonly CancellationToken cancellation;
    private bool started,closing,ticketCreated,ticketComplete;
    internal string Operation {get;}
    internal string Generation {get;}=Guid.NewGuid().ToString("N");
    internal string Bundle {get;}=Guid.NewGuid().ToString("N");
    internal Task<InstallationDeploymentOutcome> Finalization=>finalization;
    internal bool BeforeUnconfirmed=>beforeAttempted&&!beforeComplete;
    internal bool CleanupConfirmed {get{lock(sync)return finalization.IsCompleted&&(settlement==null||settlement.IsCompleted)&&admission.CleanupConfirmed&&files.Count==0&&reads.All(x=>x.CleanupConfirmed)&&(snapshot==null||snapshot.IsCompleted)&&(snapshotOwner==null||snapshotOwner.CleanupConfirmed)&&(generationPreparation==null||generationPreparation.IsCompleted)&&(bootstrapPreparation==null||bootstrapPreparation.IsCompleted)&&(prepared==null||prepared.IsCompleted)&&(publicationTask==null||publicationTask.IsCompleted)&&(generationIO==null||generationIO.CleanupConfirmed);}}
    internal InstallationDeploymentScope(string root,string data,string generationSourceSha,string bootstrapSourceSha,
        CancellationToken cancellation=default,InstallationAdmissionFaults? admissionFaults=null,InstallationDeploymentFaults? faults=null)
    {
        if(!InstallationPackage.HashValid(generationSourceSha)||!InstallationPackage.HashValid(bootstrapSourceSha))throw new InvalidDataException("Both source pins are required.");
        root=InstallationPaths.Root(root);data=InstallationPaths.Root(data);this.root=root;userData=data;this.generationSourceSha=generationSourceSha;this.bootstrapSourceSha=bootstrapSourceSha;store=new(root,data,()=>true);
        admission=InstallationAdmission.CreateOwner(root,admissionFaults);this.faults=faults;this.cancellation=cancellation;
        Operation=Guid.NewGuid().ToString("N");pending=store.PendingPath;
        temporary=Path.Combine(root,"deployment-terminal-"+Operation+".tmp");backup=Path.Combine(root,"deployment-ticket-"+Operation+".json");
        archive=Path.Combine(root,"deployment-failed-"+Operation+".json");beforePath=Path.Combine(root,"deployment-before-"+Operation+".json");
        ticket=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=2,kind="KuaiYiUnifiedDeploymentTicket",operation=Operation,root,data,generationSourceSha,bootstrapSourceSha,generation=Generation,bundle=Bundle});
        if(ticket.Length>16384)throw new InvalidDataException("Deployment ticket exceeds its bound.");
        // Neither worker can acquire a resource until both original tasks are visible to this owner.
        acquisition=Task.Run(async()=>{await start.Task.ConfigureAwait(false);Acquire();});
        finalization=Task.Run(FinalizeAsync);
    }
    internal void RegisterFinalRead(IInstallationDeploymentFinalRead owner)
    {ArgumentNullException.ThrowIfNull(owner);lock(sync){if(started||closing||reads.Any(x=>ReferenceEquals(x,owner)))throw new InvalidOperationException("Final read producer is sealed or already owns this reader.");reads.Add(owner);}}
    internal async Task AcquireAsync()
    {
        lock(sync){if(started)throw new InvalidOperationException("Scope acquisition is one-shot.");started=true;lock(Retained)Retained.Add(this);start.SetResult();}
        try{await acquisition.ConfigureAwait(false);}
        catch{RequestFinish();var result=await finalization.ConfigureAwait(false);InstallationErrors.Throw(result.Errors);throw;}
    }
    private void Acquire()
    {
        cancellation.ThrowIfCancellationRequested();
        // Existing-only foundation. Initialization and all participant publication remain separate later work.
        store.AcquireAdmission(admission,requireStopped:true,keepSetup:true);cancellation.ThrowIfCancellationRequested();
        foreach(string path in new[]{temporary,backup,archive,beforePath})
        {InstallationPaths.Check(path);if(File.Exists(path)||Directory.Exists(path))throw new InstallationRecoveryException("Deployment operation already exists.");}
        Write(pending,ticket,true);ticketComplete=true;admission.ReleaseSetup();
    }
    private void RequestFinish(bool abort=true){lock(sync){if(abort)publicationAbort=true;finishing=true;finish.TrySetResult();}}
    internal Task<InstallationDeploymentOutcome> AbortBeforeSnapshotAsync()
    {lock(sync){if(!started||snapshot!=null)throw new InvalidOperationException("Scope is not in the before-snapshot phase.");RequestFinish();return finalization;}}
    internal Task<InstallationDeploymentOutcome> AbortBeforePreparedAsync()
    {lock(sync){if(snapshot==null)throw new InvalidOperationException("Before snapshot was not started.");RequestFinish();return finalization;}}
    internal Task<InstallationDeploymentOutcome> AbortAfterPreparedAsync()
    {lock(sync){if(qualifiedPrepared==null)throw new InvalidOperationException("Original Prepared has not qualified.");RequestFinish();return finalization;}}
    internal Task<InstallationDeploymentBefore> CaptureBeforeAsync(IInstallationDeploymentSnapshotRead owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        lock(sync)
        {
            if(!acquisition.IsCompletedSuccessfully||finishing||closing||snapshot!=null||reads.Any(x=>ReferenceEquals(x,owner)))throw new InvalidOperationException("Snapshot producer is sealed or admission is not ready.");
            snapshotOwner=owner;var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            snapshot=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);return await CaptureCoreAsync().ConfigureAwait(false);});
            begin.SetResult();return ObserveSnapshotAsync(snapshot);
        }
    }
    private async Task<InstallationDeploymentBefore> ObserveSnapshotAsync(Task<InstallationDeploymentBefore> original)
    {
        try{return await original.ConfigureAwait(false);}
        catch{RequestFinish();var result=await finalization.ConfigureAwait(false);InstallationErrors.Throw(result.Errors);throw;}
    }
    internal Task<InstallationGenerationCandidate> PrepareGenerationAsync(string source,InstallationPreparationFaults? preparationFaults=null)
    {
        source=InstallationPaths.Root(source);
        if(InstallationPaths.Contains(root,source)||InstallationPaths.Contains(source,root)||InstallationPaths.Contains(userData,source)||InstallationPaths.Contains(source,userData))throw new InvalidDataException("Generation source must be separate from installation and user data.");
        lock(sync)
        {
            if(snapshot is not{IsCompletedSuccessfully:true}||!beforeComplete||finishing||closing||generationPreparation!=null)throw new InvalidOperationException("Generation preparation requires the original complete Before phase.");
            string staging=Path.Combine(root,"staging-"+Generation),destination=Path.Combine(root,"versions",Generation),intent=Path.Combine(root,"deployment-generation-"+Operation+".json");
            generationContext=new(root,source,generationSourceSha,Generation,Operation,staging,destination,intent,Convert.ToHexString(SHA256.HashData(ticket)),Convert.ToHexString(SHA256.HashData(before!)),cancellation);
            generationIO=new(source,staging,destination,intent,cancellation,preparationFaults);
            var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var owned=generationIO;
            generationPreparation=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);return InstallationGenerationCandidate.Prepare(this,owned);});
            begin.SetResult();return ObserveGenerationAsync(generationPreparation);
        }
    }
    private async Task<InstallationGenerationCandidate> ObserveGenerationAsync(Task<InstallationGenerationCandidate> original)
    {
        try
        {
            var candidate=await original.ConfigureAwait(false);
            lock(sync){if(finishing||closing||cancellation.IsCancellationRequested)throw new OperationCanceledException("Candidate delivery was retired.",cancellation);return candidate;}
        }
        catch{RequestFinish();var result=await finalization.ConfigureAwait(false);InstallationErrors.Throw(result.Errors);throw;}
    }
    internal GenerationPreparationContext RequireGenerationPreparation(InstallationPreparationIO owner)
    {
        lock(sync){if(!ReferenceEquals(owner,generationIO)||generationContext==null||finishing||closing)throw new OperationCanceledException("Original preparation phase is no longer open.");}
        cancellation.ThrowIfCancellationRequested();admission.RequireMutation(root);RequireExact(pending,ticket);RequireExact(beforePath,before!,131072);RequireBeforeFiles(JsonSerializer.Deserialize<InstallationDeploymentBefore>(before!)!);
        return generationContext;
    }
    internal bool IsLiveCandidate(InstallationGenerationCandidate candidate)
    {lock(sync)return publication==null&&!finishing&&!closing&&!cancellation.IsCancellationRequested&&generationPreparation is{IsCompletedSuccessfully:true}&&ReferenceEquals(generationPreparation.Result,candidate);}
    internal void RecordGenerationIntent(InstallationPreparationIO owner,InactiveGenerationRetention value)
    {
        if(!ReferenceEquals(owner,generationIO))throw new InvalidOperationException("Wrong original preparation owner.");generationRetention=value;
    }
    internal void RequireRetentionCapacity(InactiveGenerationRetention value)
    {if(TerminalBytes(value).Length>16384)throw new InvalidDataException("Generation retention exceeds terminal bound.");}
    internal Task<InstallationBootstrapCandidate> PrepareBootstrapAsync(string source,InstallationGenerationCandidate generation,InstallationPreparationFaults? preparationFaults=null)
    {
        ArgumentNullException.ThrowIfNull(generation);source=InstallationPaths.Root(source);
        if(InstallationPaths.Contains(root,source)||InstallationPaths.Contains(source,root)||InstallationPaths.Contains(userData,source)||InstallationPaths.Contains(source,userData))throw new InvalidDataException("Bootstrap source must be separate from installation and user data.");
        lock(sync)
        {
            if(!generation.IsLiveFor(this)||generationIO==null||bootstrapPreparation!=null)throw new InvalidOperationException("Bootstrap preparation requires the original live G and is one-shot.");
            string staging=Path.Combine(root,"bootstrap-staging-"+Bundle),destination=Path.Combine(root,"bootstrap-candidates",Bundle),intent=Path.Combine(root,"deployment-bootstrap-"+Operation+".json");
            bootstrapContext=new(root,source,bootstrapSourceSha,Bundle,Operation,staging,destination,intent,Convert.ToHexString(SHA256.HashData(ticket)),Convert.ToHexString(SHA256.HashData(before!)),generation,cancellation);
            bootstrapIO=generationIO.CreateBootstrapChannel(source,staging,destination,intent,preparationFaults);
            var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var owned=bootstrapIO;
            bootstrapPreparation=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);return InstallationBootstrapCandidate.Prepare(this,owned);});
            begin.SetResult();return ObserveBootstrapAsync(bootstrapPreparation);
        }
    }
    private async Task<InstallationBootstrapCandidate> ObserveBootstrapAsync(Task<InstallationBootstrapCandidate> original)
    {
        try{var candidate=await original.ConfigureAwait(false);lock(sync){if(finishing||closing||cancellation.IsCancellationRequested||!candidate.Generation.IsLiveFor(this))throw new OperationCanceledException("Bootstrap delivery was retired.",cancellation);return candidate;}}
        catch{RequestFinish();var result=await finalization.ConfigureAwait(false);InstallationErrors.Throw(result.Errors);throw;}
    }
    internal BootstrapPreparationContext RequireBootstrapPreparation(InstallationPreparationIO owner)
    {
        lock(sync){if(!ReferenceEquals(owner,bootstrapIO)||bootstrapContext==null||!bootstrapContext.Generation.IsLiveFor(this))throw new OperationCanceledException("Original bootstrap phase is no longer open.",cancellation);}
        cancellation.ThrowIfCancellationRequested();admission.RequireMutation(root);RequireExact(pending,ticket);RequireExact(beforePath,before!,131072);RequireBeforeFiles(JsonSerializer.Deserialize<InstallationDeploymentBefore>(before!)!);return bootstrapContext;
    }
    internal bool IsLiveBootstrap(InstallationBootstrapCandidate candidate)
    {lock(sync)return publication==null&&!finishing&&!closing&&!cancellation.IsCancellationRequested&&candidate.Generation.IsLiveFor(this)&&bootstrapPreparation is{IsCompletedSuccessfully:true}&&ReferenceEquals(bootstrapPreparation.Result,candidate);}
    internal void RecordBootstrapIntent(InstallationPreparationIO owner,InactiveBootstrapRetention value)
    {if(!ReferenceEquals(owner,bootstrapIO))throw new InvalidOperationException("Wrong original bootstrap owner.");bootstrapRetention=value;}
    internal void RequireBootstrapRetentionCapacity(InactiveBootstrapRetention value)
    {if(TerminalBytes(generationRetention,value).Length>16384)throw new InvalidDataException("Combined G/B retention exceeds terminal bound.");}
    internal Task<InstallationDeploymentPrepared> SealPreparedAsync(InstallationGenerationCandidate generation,InstallationBootstrapCandidate bootstrap,InstallationPreparationFaults? preparationFaults=null)
    {
        ArgumentNullException.ThrowIfNull(generation);ArgumentNullException.ThrowIfNull(bootstrap);
        lock(sync)
        {
            if(!generation.IsLiveFor(this)||!bootstrap.IsLiveFor(this)||!ReferenceEquals(bootstrap.Generation,generation)||generationIO==null||prepared!=null||snapshotOwner is not IInstallationDeploymentPreparedRead registry)throw new InvalidOperationException("Prepared requires original live G/B and original registry read owner.");
            string path=Path.Combine(root,"deployment-prepared-"+Operation+".json");preparedContext=new(root,userData,Operation,Bundle,path,Convert.ToHexString(SHA256.HashData(ticket)),Convert.ToHexString(SHA256.HashData(before!)),JsonSerializer.Deserialize<InstallationDeploymentBefore>(before!)!,bootstrap,cancellation);
            preparedIO=generationIO.CreatePreparedChannel(root,path,preparationFaults);var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var owned=preparedIO;
            prepared=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);return await InstallationDeploymentPrepared.SealAsync(this,owned,registry).ConfigureAwait(false);});begin.SetResult();return ObservePreparedAsync(prepared);
        }
    }
    private async Task<InstallationDeploymentPrepared> ObservePreparedAsync(Task<InstallationDeploymentPrepared> original)
    {
        try{var value=await original.ConfigureAwait(false);lock(sync){if(!IsLivePrepared(value))throw new OperationCanceledException("Prepared delivery was retired.",cancellation);return value;}}
        catch{RequestFinish();var result=await finalization.ConfigureAwait(false);InstallationErrors.Throw(result.Errors);throw;}
    }
    internal PreparedPreparationContext RequirePreparedPreparation(InstallationPreparationIO owner)
    {
        lock(sync){if(!ReferenceEquals(owner,preparedIO)||preparedContext==null||!preparedContext.Bootstrap.IsLiveFor(this))throw new OperationCanceledException("Original Prepared phase is retired.",cancellation);}
        cancellation.ThrowIfCancellationRequested();admission.RequireMutation(root);RequireExact(pending,ticket);RequireExact(beforePath,before!,131072);RequireBeforeFiles(preparedContext.Before);return preparedContext;
    }
    internal void CompletePrepared(InstallationPreparationIO owner,InstallationDeploymentPrepared value)
    {
        lock(sync)
        {
            if(!ReferenceEquals(owner,preparedIO)||!value.BelongsTo(this)||!value.Bootstrap.IsLiveFor(this)||finishing||closing||cancellation.IsCancellationRequested)throw new OperationCanceledException("Prepared qualification was retired.",cancellation);
            if(!owner.CleanupConfirmed||owner.StorageUnconfirmed||snapshotOwner?.CleanupConfirmed!=true)throw new InstallationRecoveryException("Original Prepared resources remain unsettled.");qualifiedPrepared=value;
        }
    }
    internal bool IsLivePrepared(InstallationDeploymentPrepared value)
    {lock(sync)return publication==null&&!finishing&&!closing&&!cancellation.IsCancellationRequested&&ReferenceEquals(qualifiedPrepared,value)&&value.Bootstrap.IsLiveFor(this)&&prepared is{IsCompletedSuccessfully:true}&&ReferenceEquals(prepared.Result,value);}
    private byte[] TerminalBytes(InactiveGenerationRetention? retention)=>TerminalBytes(retention,bootstrapRetention);
    private byte[] TerminalBytes(InactiveGenerationRetention? retention,InactiveBootstrapRetention? bootstrap)=>TerminalBytes(retention,bootstrap,qualifiedPrepared?.Sha256);
    private byte[] TerminalBytes(InactiveGenerationRetention? retention,InactiveBootstrapRetention? bootstrap,string? preparedSha,InstallationPublicationRetention? retainedPublication=null,string? variant=null)=>JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=2,kind="KuaiYiUnifiedDeploymentTerminal",operation=Operation,variant=variant??(commitTerminal?"CommitCandidate":preparedSha!=null?"AbortAfterPrepared":beforeComplete?"AbortBeforePrepared":"AbortBeforeSnapshot"),ticketSha256=Convert.ToHexString(SHA256.HashData(ticket)),beforeSha256=beforeComplete?Convert.ToHexString(SHA256.HashData(before!)):null,preparedSha256=preparedSha,inactiveGeneration=retention,inactiveBootstrap=bootstrap,publicationRetention=retainedPublication??publication?.Retention});
    internal void RequirePreparedTerminalCapacity(string sha)
    {if(TerminalBytes(generationRetention,bootstrapRetention,sha).Length>16384)throw new InvalidDataException("Prepared terminal exceeds its actual encoded bound.");}
    private static string? Encode(byte[]? bytes)=>bytes==null?null:Convert.ToBase64String(bytes);
    private async Task<InstallationDeploymentBefore> CaptureCoreAsync()
    {
        cancellation.ThrowIfCancellationRequested();admission.RequireMutation(root);RequireExact(pending,ticket);
        string? current=Encode(ReadOptional(Path.Combine(root,"current.json"),4096));
        string? owner=Encode(ReadOptional(InstallationBootstrapDeployment.OwnerPath(root),4096));
        string? active=Encode(ReadOptional(InstallationBootstrapDeployment.ActivePath(root),8192));
        string? receipt=Encode(ReadOptional(Path.Combine(root,"host-registration.json"),8192));
        var errors=new List<Exception>();InstallationDeploymentRegistrationSnapshot? registry=null;
        try{registry=await snapshotOwner!.CaptureAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        try{await snapshotOwner!.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        if(!snapshotOwner!.CleanupConfirmed)errors.Add(new IOException("Original snapshot registry resources are unsettled."));
        InstallationErrors.Throw(errors);cancellation.ThrowIfCancellationRequested();
        RequireSlot(registry!.Chrome);RequireSlot(registry.Edge);
        var value=new InstallationDeploymentBefore(2,"KuaiYiUnifiedDeploymentBefore",Operation,Convert.ToHexString(SHA256.HashData(ticket)),current,owner,active,receipt,registry);
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value);if(bytes.Length>131072)throw new InvalidDataException("Before snapshot exceeds its actual encoded bound.");
        admission.RequireMutation(root);RequireExact(pending,ticket);cancellation.ThrowIfCancellationRequested();
        Write(beforePath,bytes,false,true);before=bytes;beforeComplete=true;return value;
    }
    private static void RequireSlot(InstallationDeploymentRegistrationSlot slot)
    {
        if(slot==null||slot.Value?.Length>32768||slot.Value?.Contains('\0')==true||!slot.KeyExists&&(slot.Value!=null||slot.ForeignContent))throw new InvalidDataException("Invalid registry snapshot shape.");
    }
    private byte[]? ReadOptional(string path,int limit)
    {
        if(!SnapshotFilePresent(path))return null;
        var original=Open(path,FileMode.Open,FileAccess.Read);var errors=new List<Exception>();byte[]? result=null;
        try
        {
            faults?.BeforeRead?.Invoke(path,original);cancellation.ThrowIfCancellationRequested();
            if(original.Length is <1||original.Length>limit)throw new InvalidDataException("Snapshot metadata exceeds its consumer bound.");
            result=new byte[(int)original.Length];original.ReadExactly(result);
        }
        catch(Exception error){errors.Add(error);}
        try{Close(path,original);}catch(Exception error){errors.Add(error);}
        InstallationErrors.Throw(errors);return result;
    }
    private bool SnapshotFilePresent(string path)
    {
        InstallationPaths.Check(path);faults?.BeforeSnapshotQuery?.Invoke(path);FileAttributes attributes;
        try{attributes=File.GetAttributes(path);}
        catch(FileNotFoundException)
        {
            // A failed/missing parent is not proof that this fixed leaf is absent.
            var parent=File.GetAttributes(Path.GetDirectoryName(path)!);
            if((parent&FileAttributes.Directory)==0||(parent&FileAttributes.ReparsePoint)!=0)throw new DirectoryNotFoundException("Snapshot parent is not a confirmed directory.");
            return false;
        }
        if((attributes&(FileAttributes.Directory|FileAttributes.Device|FileAttributes.ReparsePoint))!=0)throw new InstallationRecoveryException("Snapshot metadata is not a regular file.");
        return true;
    }
    private void RequireBeforeFiles(InstallationDeploymentBefore value)
    {
        Check(Path.Combine(root,"current.json"),value.Current,4096);Check(InstallationBootstrapDeployment.OwnerPath(root),value.BootstrapOwner,4096);
        Check(InstallationBootstrapDeployment.ActivePath(root),value.BootstrapActive,8192);Check(Path.Combine(root,"host-registration.json"),value.HostReceipt,8192);
        void Check(string path,string? expected,int limit)
        {
            if(expected==null){if(SnapshotFilePresent(path))throw new InstallationRecoveryException("Snapshot absence changed; retained.");}
            else RequireExact(path,Convert.FromBase64String(expected),limit);
        }
    }
    private FileStream Open(string path,FileMode mode,FileAccess access)
    {
        InstallationPaths.Check(path);var original=new FileStream(path,mode,access,FileShare.None);
        lock(sync)files.Add((path,original));return original;
    }
    private void Close(string path,FileStream original)
    {
        faults?.BeforeClose?.Invoke(path);original.Dispose();lock(sync)files.RemoveAll(x=>ReferenceEquals(x.Original,original));
    }
    private void Write(string path,byte[] bytes,bool isTicket,bool isBefore=false)
    {
        // An attempted CreateNew may fail with foreign/unknown metadata; never downgrade it to an early abort.
        if(isBefore)beforeAttempted=true;
        var original=Open(path,FileMode.CreateNew,FileAccess.Write);if(isTicket)ticketCreated=true;
        var errors=new List<Exception>();
        try
        {
            if(isTicket&&faults?.WriteTicket!=null)faults.WriteTicket(original,bytes);
            else if(isBefore&&faults?.WriteBefore!=null)faults.WriteBefore(original,bytes);
            else for(int offset=0;offset<bytes.Length;offset+=4096)
            {if(isTicket||isBefore)cancellation.ThrowIfCancellationRequested();original.Write(bytes.AsSpan(offset,Math.Min(4096,bytes.Length-offset)));}
            original.Flush(true);
        }
        catch(Exception error){errors.Add(error);}
        try{Close(path,original);}catch(Exception error){errors.Add(error);}
        InstallationErrors.Throw(errors);
    }
    private void RequireExact(string path,byte[] expected,int maximum=16384)
    {
        var original=Open(path,FileMode.Open,FileAccess.Read);var errors=new List<Exception>();
        try
        {
            if(original.Length!=expected.Length||expected.Length>maximum)throw new InstallationRecoveryException("Deployment journal changed; retained.");
            byte[] actual=new byte[expected.Length];original.ReadExactly(actual);
            if(!actual.AsSpan().SequenceEqual(expected))throw new InstallationRecoveryException("Deployment journal changed; retained.");
        }
        catch(Exception error){errors.Add(error);}
        try{Close(path,original);}catch(Exception error){errors.Add(error);}
        InstallationErrors.Throw(errors);
    }
    private async Task<InstallationDeploymentOutcome> FinalizeAsync()
    {
        await finish.Task.ConfigureAwait(false);var errors=new List<Exception>();
        try{await acquisition.ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        if(snapshot!=null)try{await snapshot.ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        if(generationPreparation!=null)try{await generationPreparation.ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        if(bootstrapPreparation!=null)try{await bootstrapPreparation.ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        if(prepared!=null)try{await prepared.ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        if(publicationTask!=null)return await FinalizePublicationAsync(errors).ConfigureAwait(false);
        try{if(faults?.BeforeClosing!=null)await faults.BeforeClosing().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        if(cancellation.IsCancellationRequested)errors.Add(new OperationCanceledException(cancellation));
        lock(sync)closing=true;
        bool canArchive=ticketComplete&&!BeforeUnconfirmed&&generationIO?.StorageUnconfirmed!=true;
        try{if(faults?.AfterClosing!=null)await faults.AfterClosing().ConfigureAwait(false);}catch(Exception error){errors.Add(error);canArchive=false;}
        if(generationIO!=null)try{generationIO.CloseOriginals();if(!generationIO.CleanupConfirmed)canArchive=false;}catch(Exception error){errors.Add(error);canArchive=false;}
        try{admission.Dispose();}catch(Exception error){errors.Add(error);canArchive=false;}
        if(!admission.CleanupConfirmed)canArchive=false;
        // New post-Closing handles/tasks are owned by this already-published finalization task.
        if(canArchive)
        {
            foreach(var owner in reads)
                try{await owner.VerifyAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);canArchive=false;}
        }
        foreach(var owner in reads)
            try{await owner.CloseOriginalsAsync().ConfigureAwait(false);if(!owner.CleanupConfirmed)canArchive=false;}catch(Exception error){errors.Add(error);canArchive=false;}
        if(snapshotOwner!=null)
        {
            if(canArchive&&beforeComplete)
                try{await snapshotOwner.VerifyAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);canArchive=false;}
            try{await snapshotOwner.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);canArchive=false;}
            if(!snapshotOwner.CleanupConfirmed)canArchive=false;
        }
        if(reads.Any(x=>!x.CleanupConfirmed))canArchive=false;
        // Retry only the actual still-owned streams for settlement; a partial/failed ticket remains unknown forever.
        (string Path,FileStream Original)[] originalFiles;lock(sync)originalFiles=files.ToArray();
        foreach(var file in originalFiles)try{Close(file.Path,file.Original);}catch(Exception error){errors.Add(error);canArchive=false;}
        lock(sync)if(files.Count!=0)canArchive=false;
        if(canArchive)
        {
            try
            {
                RequireExact(pending,ticket);
                if(beforeComplete){RequireExact(beforePath,before!,131072);RequireBeforeFiles(JsonSerializer.Deserialize<InstallationDeploymentBefore>(before!)!);}
                generationIO?.VerifyRecordedMetadata();
                if(generationIO is{CleanupConfirmed:false}or{StorageUnconfirmed:true})throw new InstallationRecoveryException("Inactive candidate metadata or original IO is unconfirmed.");
                byte[] terminal=TerminalBytes(generationRetention);
                if(terminal.Length>16384)throw new InvalidDataException("Terminal exceeds its bound.");
                Write(temporary,terminal,false);RequireExact(pending,ticket);RequireExact(temporary,terminal);
                InstallationPaths.Check(backup);if(File.Exists(backup)||Directory.Exists(backup))throw new InstallationRecoveryException("Foreign ticket backup retained.");
                File.Replace(temporary,pending,backup,false);
                RequireExact(pending,terminal);RequireExact(backup,ticket);
                // All fallible resource cleanup is above. No filesystem access or Dispose follows this Move.
                if(faults?.Archive!=null)faults.Archive(pending,archive);else File.Move(pending,archive);
                lock(Retained)Retained.Remove(this);
                return new(qualifiedPrepared!=null?InstallationDeploymentStatus.AbortedAfterPrepared:beforeComplete?InstallationDeploymentStatus.AbortedBeforePrepared:InstallationDeploymentStatus.AbortedBeforeSnapshot,errors.ToArray());
            }
            catch(Exception error){errors.Add(error);}
        }
        bool clean=admission.CleanupConfirmed&&reads.All(x=>x.CleanupConfirmed)&&(snapshotOwner==null||snapshotOwner.CleanupConfirmed)&&(generationIO==null||generationIO.CleanupConfirmed);lock(sync)clean&=files.Count==0;
        if(!ticketCreated&&clean){lock(Retained)Retained.Remove(this);return new(InstallationDeploymentStatus.Refused,errors.ToArray());}
        return new(InstallationDeploymentStatus.Unconfirmed,errors.ToArray());
    }
    // Explicit same-owner settlement only. Never reopens a stream, deletes a ticket or replays finalization.
    internal Task<IReadOnlyList<Exception>> SettleOriginalsAsync()
    {
        lock(sync)
        {
            if(!finalization.IsCompleted)throw new InvalidOperationException("Original finalization is still pending.");
            if(settlement!=null&&!settlement.IsCompleted)return settlement;
            // After a successful final Move there is no fallible cleanup left, including on this API.
            if(admission.CleanupConfirmed&&files.Count==0&&reads.All(x=>x.CleanupConfirmed)&&(snapshotOwner==null||snapshotOwner.CleanupConfirmed)&&(generationIO==null||generationIO.CleanupConfirmed))return Task.FromResult<IReadOnlyList<Exception>>([]);
            var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            settlement=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);return await SettleCoreAsync().ConfigureAwait(false);});
            begin.SetResult();return settlement;
        }
    }
    private async Task<IReadOnlyList<Exception>> SettleCoreAsync()
    {
        var errors=new List<Exception>();
        if(generationIO!=null)try{generationIO.CloseOriginals();}catch(Exception error){errors.Add(error);}
        try{admission.Dispose();}catch(Exception error){errors.Add(error);}
        foreach(var owner in reads)try{await owner.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        if(snapshotOwner!=null)try{await snapshotOwner.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        (string Path,FileStream Original)[] originals;lock(sync)originals=files.ToArray();
        foreach(var file in originals)try{Close(file.Path,file.Original);}catch(Exception error){errors.Add(error);}
        return errors;
    }
}
