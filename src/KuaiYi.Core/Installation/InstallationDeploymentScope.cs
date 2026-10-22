using System.Security.Cryptography;
using System.Text.Json;
namespace KuaiYi.Core.Installation;

// This first slice deliberately cannot publish a generation, registry value or Committed result.
internal enum InstallationDeploymentStatus { Refused,AbortedBeforeSnapshot,Unconfirmed }
internal sealed record InstallationDeploymentOutcome(InstallationDeploymentStatus Status,IReadOnlyList<Exception> Errors);
internal interface IInstallationDeploymentFinalRead
{
    // The object is registered before acquisition; it owns handles before its first possibly failing read.
    Task VerifyAsync();
    Task CloseOriginalsAsync();
    bool CleanupConfirmed {get;}
}
internal sealed class InstallationDeploymentFaults
{
    internal Action<FileStream,byte[]>? WriteTicket {get;init;}
    internal Action<string>? BeforeClose {get;set;}
    internal Func<Task>? BeforeClosing {get;init;}
    internal Func<Task>? AfterClosing {get;init;}
    internal Action<string,string>? Archive {get;init;}
}
internal sealed class InstallationDeploymentScope
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
    private readonly string pending,temporary,backup,archive;
    private readonly CancellationToken cancellation;
    private bool started,closing,ticketCreated,ticketComplete;
    internal string Operation {get;}
    internal string Generation {get;}=Guid.NewGuid().ToString("N");
    internal string Bundle {get;}=Guid.NewGuid().ToString("N");
    internal Task<InstallationDeploymentOutcome> Finalization=>finalization;
    internal bool CleanupConfirmed {get{lock(sync)return finalization.IsCompleted&&(settlement==null||settlement.IsCompleted)&&admission.CleanupConfirmed&&files.Count==0&&reads.All(x=>x.CleanupConfirmed);}}
    internal InstallationDeploymentScope(string root,string data,string generationSourceSha,string bootstrapSourceSha,
        CancellationToken cancellation=default,InstallationAdmissionFaults? admissionFaults=null,InstallationDeploymentFaults? faults=null)
    {
        if(!InstallationPackage.HashValid(generationSourceSha)||!InstallationPackage.HashValid(bootstrapSourceSha))throw new InvalidDataException("Both source pins are required.");
        root=InstallationPaths.Root(root);data=InstallationPaths.Root(data);store=new(root,data,()=>true);
        admission=InstallationAdmission.CreateOwner(root,admissionFaults);this.faults=faults;this.cancellation=cancellation;
        Operation=Guid.NewGuid().ToString("N");pending=store.PendingPath;
        temporary=Path.Combine(root,"deployment-terminal-"+Operation+".tmp");backup=Path.Combine(root,"deployment-ticket-"+Operation+".json");
        archive=Path.Combine(root,"deployment-failed-"+Operation+".json");
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
        catch{finish.TrySetResult();var result=await finalization.ConfigureAwait(false);InstallationErrors.Throw(result.Errors);throw;}
    }
    private void Acquire()
    {
        cancellation.ThrowIfCancellationRequested();
        // Existing-only foundation. Initialization and all participant publication remain separate later work.
        store.AcquireAdmission(admission,requireStopped:true,keepSetup:true);cancellation.ThrowIfCancellationRequested();
        foreach(string path in new[]{temporary,backup,archive})
        {InstallationPaths.Check(path);if(File.Exists(path)||Directory.Exists(path))throw new InstallationRecoveryException("Deployment operation already exists.");}
        Write(pending,ticket,true);ticketComplete=true;admission.ReleaseSetup();
    }
    internal Task<InstallationDeploymentOutcome> AbortBeforeSnapshotAsync()
    {lock(sync){if(!started)throw new InvalidOperationException("Scope was not started.");finish.TrySetResult();return finalization;}}
    private FileStream Open(string path,FileMode mode,FileAccess access)
    {
        InstallationPaths.Check(path);var original=new FileStream(path,mode,access,FileShare.None);
        lock(sync)files.Add((path,original));return original;
    }
    private void Close(string path,FileStream original)
    {
        faults?.BeforeClose?.Invoke(path);original.Dispose();lock(sync)files.RemoveAll(x=>ReferenceEquals(x.Original,original));
    }
    private void Write(string path,byte[] bytes,bool isTicket)
    {
        var original=Open(path,FileMode.CreateNew,FileAccess.Write);if(isTicket)ticketCreated=true;
        var errors=new List<Exception>();
        try
        {
            if(isTicket&&faults?.WriteTicket!=null)faults.WriteTicket(original,bytes);
            else for(int offset=0;offset<bytes.Length;offset+=4096)
            {if(isTicket)cancellation.ThrowIfCancellationRequested();original.Write(bytes.AsSpan(offset,Math.Min(4096,bytes.Length-offset)));}
            original.Flush(true);
        }
        catch(Exception error){errors.Add(error);}
        try{Close(path,original);}catch(Exception error){errors.Add(error);}
        InstallationErrors.Throw(errors);
    }
    private void RequireExact(string path,byte[] expected)
    {
        var original=Open(path,FileMode.Open,FileAccess.Read);var errors=new List<Exception>();
        try
        {
            if(original.Length!=expected.Length||expected.Length>16384)throw new InstallationRecoveryException("Deployment journal changed; retained.");
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
        try{if(faults?.BeforeClosing!=null)await faults.BeforeClosing().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        if(cancellation.IsCancellationRequested)errors.Add(new OperationCanceledException(cancellation));
        lock(sync)closing=true;
        bool canArchive=ticketComplete;
        try{if(faults?.AfterClosing!=null)await faults.AfterClosing().ConfigureAwait(false);}catch(Exception error){errors.Add(error);canArchive=false;}
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
                byte[] terminal=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=2,kind="KuaiYiUnifiedDeploymentTerminal",operation=Operation,variant="AbortBeforeSnapshot",ticketSha256=Convert.ToHexString(SHA256.HashData(ticket)),beforeSha256=(string?)null,preparedSha256=(string?)null});
                if(terminal.Length>16384)throw new InvalidDataException("Terminal exceeds its bound.");
                Write(temporary,terminal,false);RequireExact(pending,ticket);RequireExact(temporary,terminal);
                InstallationPaths.Check(backup);if(File.Exists(backup)||Directory.Exists(backup))throw new InstallationRecoveryException("Foreign ticket backup retained.");
                File.Replace(temporary,pending,backup,false);
                RequireExact(pending,terminal);RequireExact(backup,ticket);
                // All fallible resource cleanup is above. No filesystem access or Dispose follows this Move.
                if(faults?.Archive!=null)faults.Archive(pending,archive);else File.Move(pending,archive);
                lock(Retained)Retained.Remove(this);
                return new(InstallationDeploymentStatus.AbortedBeforeSnapshot,errors.ToArray());
            }
            catch(Exception error){errors.Add(error);}
        }
        bool clean=admission.CleanupConfirmed&&reads.All(x=>x.CleanupConfirmed);lock(sync)clean&=files.Count==0;
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
            if(admission.CleanupConfirmed&&files.Count==0&&reads.All(x=>x.CleanupConfirmed))return Task.FromResult<IReadOnlyList<Exception>>([]);
            var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            settlement=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);return await SettleCoreAsync().ConfigureAwait(false);});
            begin.SetResult();return settlement;
        }
    }
    private async Task<IReadOnlyList<Exception>> SettleCoreAsync()
    {
        var errors=new List<Exception>();
        try{admission.Dispose();}catch(Exception error){errors.Add(error);}
        foreach(var owner in reads)try{await owner.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        (string Path,FileStream Original)[] originals;lock(sync)originals=files.ToArray();
        foreach(var file in originals)try{Close(file.Path,file.Original);}catch(Exception error){errors.Add(error);}
        return errors;
    }
}
