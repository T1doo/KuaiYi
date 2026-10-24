using System.Diagnostics;
using KuaiYi.Core.Settings;
namespace KuaiYi.Core.Installation;

internal interface IInstallationStartupRemovalProtocol
{
    void AttachStartupRemoval(Process child,InstallationStartupRemovalServer server);
    Task SendStartupRemovalAsync(Process child,string line,CancellationToken cancellation);
}

internal static class InstallationStartupRemovalWire
{
    internal const string Prefix="STARTUP_REMOVE|";
    internal static string Encode(InstallationControlledLaunch proof,Guid request,string verb,string? basis=null)
        =>$"{Prefix}{proof.Launch:N}|{proof.Fingerprint}|{request:N}|{verb}"+(basis==null?"":"|"+basis);
    internal static (Guid Request,string Verb,string? Basis) Decode(string line,InstallationControlledLaunch proof)
    {
        var fields=line.Split('|');
        if(line.Length>512||fields.Length is not(5 or 6)||fields[0]!="STARTUP_REMOVE"||fields[1]!=proof.Launch.ToString("N")||fields[2]!=proof.Fingerprint||
            !Guid.TryParseExact(fields[3],"N",out var request)||request==Guid.Empty||fields[4].Length is <1 or >16)
            throw new InvalidDataException("Startup control differs from the original launch.");
        string? basis=fields.Length==6?fields[5]:null;
        if(basis!=null&&(basis.Length is <1 or >160||basis.Any(c=>!char.IsAsciiHexDigit(c)&&c!=':'&&c is not('a' or 'b' or 's' or 'e' or 'n' or 't'))))
            throw new InvalidDataException("Startup settings version is invalid.");
        return(request,fields[4],basis);
    }
}

// Controller side. One slot, zero second-request queue, no replay. The original reader only registers work.
internal sealed class InstallationStartupRemovalServer
{
    private readonly object sync=new();
    private readonly InstallationStartupRemovalOwner owner;
    internal InstallationStartupRemovalOwner OriginalOwner=>owner;
    private readonly InstallationControlledLaunch proof;
    private readonly Func<string,CancellationToken,Task> send;
    private readonly Action<Task> track;
    private Guid request;
    private string next="RESERVE";
    private bool sealedProducer,unknown,queued;
    private Task tail=Task.CompletedTask;
    private Task? settlement;
    internal readonly List<Exception> Errors=[];
    internal InstallationStartupRemovalServer(InstallationStartupRemovalOwner owner,InstallationControlledLaunch proof,
        Func<string,CancellationToken,Task> send,Action<Task> track)
    {this.owner=owner;this.proof=proof;this.send=send;this.track=track;}
    internal bool CleanupConfirmed {get{lock(sync)return tail.IsCompleted&&!unknown&&owner.CleanupConfirmed&&next is "RESERVE" or "DONE";}}
    internal bool Handle(string line)
    {
        var row=InstallationStartupRemovalWire.Decode(line,proof);
        lock(sync)
        {
            if(sealedProducer||unknown||queued||next=="DONE"||row.Verb!=next&&!(row.Verb=="ABORT"&&next is "APPLY" or "COMMIT")||
                request!=Guid.Empty&&request!=row.Request||row.Verb=="RESERVE"&&(row.Basis==null||request!=Guid.Empty)||row.Verb!="RESERVE"&&row.Basis!=null)
                throw new InvalidDataException("Startup request is duplicate, unordered or no longer current.");
            request=row.Request;next=row.Verb switch{"RESERVE"=>"APPLY","APPLY"=>"COMMIT",_=>"DONE"};queued=true;
            var previous=tail;var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var original=Task.Run(async()=>
            {
                await begin.Task.ConfigureAwait(false);bool replyStarted=false;
                try
                {
                    await previous.ConfigureAwait(false);
                    lock(sync){queued=false;if(sealedProducer)throw new EndOfStreamException("Original startup connection closed.");}
                    switch(row.Verb)
                    {
                        case "RESERVE":await owner.ReserveAsync(row.Request,row.Basis!).ConfigureAwait(false);break;
                        case "APPLY":await owner.ApplyAsync().ConfigureAwait(false);break;
                        case "COMMIT":await owner.CommitAsync().ConfigureAwait(false);break;
                        case "ABORT":await owner.AbortAsync().ConfigureAwait(false);break;
                    }
                    replyStarted=true;await send(InstallationStartupRemovalWire.Encode(proof,request,row.Verb+"_OK"),CancellationToken.None).ConfigureAwait(false);
                }
                catch(Exception error)
                {
                    bool compensable=!replyStarted&&row.Verb=="APPLY"&&owner.CleanupConfirmed;
                    lock(sync){Errors.Add(error);if(compensable)next="ABORT";else unknown=true;}
                    if(!compensable)owner.Disconnected();
                    if(replyStarted)throw; // A failed/lost response is never resent or treated as an apply refusal.
                    try{await send(InstallationStartupRemovalWire.Encode(proof,request,compensable?"REFUSED":"FAILED"),CancellationToken.None).ConfigureAwait(false);}
                    catch(Exception output){throw new AggregateException("Original startup work and response failed.",error,output);}
                    if(!compensable)throw;
                }
            });
            tail=original;track(original);begin.SetResult();return true;
        }
    }
    internal void Disconnect(){lock(sync)sealedProducer=true;}
    internal Task SettleAsync(){lock(sync)return settlement??=SettleCoreAsync();}
    private async Task SettleCoreAsync()
    {
        Task original;lock(sync){sealedProducer=true;original=tail;}
        try{await original.ConfigureAwait(false);}finally{owner.Disconnected();}
        if(!CleanupConfirmed)throw new InstallationRecoveryException("Original startup slot or response settlement is unconfirmed.");
    }
}

// App side. This participant is internal and never grants registration or changes the false-only settings policy.
internal sealed class InstallationStartupRemovalClient(InstallationControlledLaunch proof,Func<string,CancellationToken,Task> send):ISettingsStartupRemovalParticipant
{
    private readonly object sync=new();
    private readonly List<Task> originals=[];
    private TaskCompletionSource? response;
    private string? expected;
    private Guid request;
    private bool disconnected,used;
    private Task? final;
    private bool aborted;
    private Task Command(string verb,string? basis=null,CancellationToken cancellation=default)
    {
        lock(sync)
        {
            if(disconnected||response is{Task.IsCompleted:false})throw new InstallationRecoveryException("Original startup response is unavailable.");
            var receipt=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);response=receipt;expected=verb+"_OK";
            var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var original=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);await send(InstallationStartupRemovalWire.Encode(proof,request,verb,basis),cancellation).ConfigureAwait(false);await receipt.Task.ConfigureAwait(false);});
            originals.Add(original);begin.SetResult();return original;
        }
    }
    internal void Receive(string line)
    {
        var row=InstallationStartupRemovalWire.Decode(line,proof);
        lock(sync)
        {
            if(disconnected||row.Request!=request||row.Basis!=null||response==null||response.Task.IsCompleted||row.Verb!=expected&&row.Verb is not("FAILED" or "REFUSED"))
                throw new InvalidDataException("Unknown or replayed startup response.");
            if(row.Verb=="REFUSED")response.SetException(new StartupRemovalRefusedException());
            else if(row.Verb=="FAILED")response.SetException(new InstallationRecoveryException("Original startup transaction failed; intent retained."));else response.SetResult();
        }
    }
    internal void Disconnect(Exception error)
    {lock(sync){disconnected=true;response?.TrySetException(error);if(response!=null)_=response.Task.Exception;}}
    internal async Task JoinAsync()
    {
        Task[] tasks;lock(sync)tasks=originals.ToArray();var errors=new List<Exception>();
        foreach(var task in tasks)try{await task.ConfigureAwait(false);}catch(StartupRemovalRefusedException)when(aborted&&final is{IsCompletedSuccessfully:true}){}catch(Exception error){errors.Add(error);}
        InstallationErrors.Throw(errors);
    }
    public async Task<ISettingsStartupRemoval> ReserveAsync(string basisVersion,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();lock(sync){if(used||disconnected)throw new InstallationRecoveryException("Startup slot is one-shot.");used=true;request=Guid.NewGuid();}
        // No cancellation waiter drops this original write/receipt. After GRANTED, cancellation rolls back on the same owner.
        await Command("RESERVE",basisVersion).ConfigureAwait(false);return new Reservation(this,request);
    }
    private sealed class Reservation(InstallationStartupRemovalClient client,Guid request):ISettingsStartupRemoval
    {
        public Guid Request=>request;
        public bool SaveStarted{get;private set;}
        public void SettingsSaveStarted()=>SaveStarted=true;
        public Task ApplyAsync(CancellationToken cancellation){cancellation.ThrowIfCancellationRequested();return client.Command("APPLY");}
        public Task CommitAsync(SettingsSnapshot saved)
        {
            lock(client.sync)
            {
                if(saved.Data.LaunchAtSignIn)throw new InstallationRecoveryException("This participant only removes startup.");
                return client.final??=client.Command("COMMIT");
            }
        }
        public Task AbortAsync(){lock(client.sync){if(client.final!=null)return client.final;client.aborted=true;return client.final=client.Command("ABORT");}}
    }
}

internal sealed class StartupRemovalRefusedException():IOException("Original startup apply failed; only the same owner may attempt exact restoration.");

public sealed partial class InstallationApplicationClient
{
    void IInstallationStartupRemovalProtocol.AttachStartupRemoval(Process child,InstallationStartupRemovalServer server)
    {
        var state=Channel(child);lock(state)
        {
            if(!state.Healthy||state.Closed||state.ClosingReceived||state.StartupRemoval!=null||state.Proof?.UpgradeRecord!=null)
                throw new InstallationRecoveryException("Original healthy startup control is unavailable.");
            state.StartupRemoval=server;
        }
    }
    Task IInstallationStartupRemovalProtocol.SendStartupRemovalAsync(Process child,string line,CancellationToken cancellation)=>Send(child,line,cancellation);
}
public sealed partial class InstallationApplicationControl
{
    private InstallationStartupRemovalClient? startupRemoval;
    internal ISettingsStartupRemovalParticipant AttachStartupRemovalSettings()
    {
        lock(stateSync)
        {
            if(loop!=null||startupRemoval!=null||Proof.UpgradeRecord!=null)throw new InstallationRecoveryException("Startup settings must attach before original Run.");
            return startupRemoval=new(Proof,Send);
        }
    }
}
