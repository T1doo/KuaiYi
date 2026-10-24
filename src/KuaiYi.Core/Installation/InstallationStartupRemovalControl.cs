using System.Diagnostics;
using System.Text.Json;
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
        if(line.Length>4096||fields.Length is not(5 or 6)||fields[0]!="STARTUP_REMOVE"||fields[1]!=proof.Launch.ToString("N")||fields[2]!=proof.Fingerprint||
            !Guid.TryParseExact(fields[3],"N",out var request)||request==Guid.Empty||fields[4].Length is <1 or >16)
            throw new InvalidDataException("Startup control differs from the original launch.");
        string? basis=fields.Length==6?fields[5]:null;
        if(basis!=null&&(basis.Length is <1 or >3072||fields[4]=="RESERVE"&&!ValidVersion(basis)))
            throw new InvalidDataException("Startup settings version is invalid.");
        return(request,fields[4],basis);
    }
    internal static bool ValidVersion(string value)=>value.Length is >0 and <=160&&!value.Any(c=>!char.IsAsciiHexDigit(c)&&c!=':'&&c is not('a' or 'b' or 's' or 'e' or 'n' or 't'));
    internal static string Payload<T>(T value)=>Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(value,SettingsJson.Options));
    private static T Payload<T>(string value,string[] fields)
    {
        if(value.Length>3072)throw new InvalidDataException("Startup payload exceeds bound.");var bytes=Convert.FromBase64String(value);
        using var doc=JsonDocument.Parse(bytes,new(){MaxDepth=4});SettingsJson.RejectDuplicates(doc.RootElement);InstallationPackage.Fields(doc.RootElement,fields);
        return JsonSerializer.Deserialize<T>(bytes,SettingsJson.Options)??throw new InvalidDataException("Startup payload missing.");
    }
    internal static SettingsStartupIntent Intent(string value,Guid request)
    {
        var result=Payload<SettingsStartupIntent>(value,["request","writeId","basisVersion","sha256"]);
        if(result.Request!=request||result.WriteId==Guid.Empty||!InstallationPackage.HashValid(result.Sha256)||!ValidVersion(result.BasisVersion))throw new InvalidDataException("Startup intent binding invalid.");return result;
    }
    internal static SettingsStartupLocalSettlement Notice(string value,Guid request)
    {
        var result=Payload<SettingsStartupLocalSettlement>(value,["request","writeId","sha256","outcome","configVersion","cleanupConfirmed"]);
        if(result.Request!=request||result.WriteId==Guid.Empty||!InstallationPackage.HashValid(result.Sha256)||result.Outcome is not("COMMITTED" or "ABORTED")||!ValidVersion(result.ConfigVersion))throw new InvalidDataException("Startup local settlement invalid.");return result;
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
    private readonly TaskCompletionSource retire=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TimeSpan budget;
    private (Process Child,Task Exit,Task Io)? originalExit;
    internal Task RetirementWork{get;}
    private readonly TaskCompletionSource localObserved=new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task LocalNoticeObserved=>localObserved.Task;
    internal readonly List<Exception> Errors=[];
    internal InstallationStartupRemovalServer(InstallationStartupRemovalOwner owner,InstallationControlledLaunch proof,
        Func<string,CancellationToken,Task> send,Action<Task> track,TimeSpan? budget=null)
    {
        this.owner=owner;this.proof=proof;this.send=send;this.track=track;this.budget=budget??TimeSpan.FromSeconds(2);
        RetirementWork=Task.Run(RetireCoreAsync);track(RetirementWork); // Registered before exit, IO or retirement can begin.
    }
    internal bool CleanupConfirmed {get{lock(sync)return RetirementWork.IsCompletedSuccessfully&&!unknown&&owner.CleanupConfirmed&&next is "RESERVE" or "DONE";}}
    internal bool Handle(string line)
    {
        var row=InstallationStartupRemovalWire.Decode(line,proof);
        lock(sync)
        {
            if(sealedProducer||unknown||queued||next=="DONE"||row.Verb!=next&&!(row.Verb=="ABORT"&&next is "APPLY" or "COMMIT")||
                request!=Guid.Empty&&request!=row.Request||row.Verb=="RESERVE"&&(row.Basis==null||request!=Guid.Empty)||
                row.Verb is "APPLY" or "LOCAL_SETTLED"&&row.Basis==null||row.Verb=="COMMIT"&&row.Basis!=null)
                throw new InvalidDataException("Startup request is duplicate, unordered or no longer current.");
            request=row.Request;next=row.Verb switch{"RESERVE"=>"APPLY","APPLY"=>"COMMIT","LOCAL_SETTLED"=>"DONE",_=>"LOCAL_SETTLED"};queued=true;
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
                        case "APPLY":await owner.BindSettingsAsync(InstallationStartupRemovalWire.Intent(row.Basis!,row.Request)).ConfigureAwait(false);await owner.ApplyAsync().ConfigureAwait(false);break;
                        case "COMMIT":await owner.CommitAsync().ConfigureAwait(false);break;
                        case "ABORT":if(row.Basis!=null)await owner.BindSettingsAsync(InstallationStartupRemovalWire.Intent(row.Basis,row.Request)).ConfigureAwait(false);await owner.AbortAsync().ConfigureAwait(false);break;
                        case "LOCAL_SETTLED":owner.ObserveLocalSettlement(InstallationStartupRemovalWire.Notice(row.Basis!,row.Request));localObserved.TrySetResult();return; // Deliberately no reply.
                    }
                    replyStarted=true;await send(InstallationStartupRemovalWire.Encode(proof,request,row.Verb+"_OK"),CancellationToken.None).ConfigureAwait(false);
                }
                catch(Exception error)
                {
                    bool compensable=!replyStarted&&row.Verb=="APPLY"&&owner.OriginalResourcesConfirmed;
                    lock(sync){Errors.Add(error);if(compensable)next="ABORT";else unknown=true;}
                    if(!compensable)owner.Disconnected();
                    if(replyStarted||row.Verb=="LOCAL_SETTLED")throw; // No response replay and no acknowledgement loop.
                    try{await send(InstallationStartupRemovalWire.Encode(proof,request,compensable?"REFUSED":"FAILED"),CancellationToken.None).ConfigureAwait(false);}
                    catch(Exception output){throw new AggregateException("Original startup work and response failed.",error,output);}
                    if(!compensable)throw;
                }
            });
            tail=original;track(original);begin.SetResult();return true;
        }
    }
    internal void Disconnect(){lock(sync)sealedProducer=true;}
    internal Task SettleAsync()
    {
        lock(sync)
        {
            sealedProducer=true;
            if(retire.Task.IsCompleted&&!RetirementWork.IsCompleted){unknown=true;owner.Disconnected();}
            retire.TrySetResult();return RetirementWork;
        }
    }
    internal Task RetireAfterOriginalExitAsync(Process child,Task exit,Task io)
    {
        lock(sync)
        {
            if(retire.Task.IsCompleted)throw new InstallationRecoveryException("Original retirement cannot be replayed.");
            sealedProducer=true;originalExit=(child,exit,io);retire.SetResult();return RetirementWork;
        }
    }
    private async Task RetireCoreAsync()
    {
        await retire.Task.ConfigureAwait(false);Task original;string phase;Guid id;(Process Child,Task Exit,Task Io)? evidence;
        lock(sync){original=tail;phase=next;id=request;evidence=originalExit;}
        try
        {
            await original.WaitAsync(budget).ConfigureAwait(false); // Timeout retains the original task through Role/this owner.
            if(id==Guid.Empty)return;
            bool empty=phase=="LOCAL_SETTLED"&&owner.CanRetireEmptyReservation;
            if(unknown||!empty&&phase!="DONE"||evidence==null)throw new InstallationRecoveryException("Original exit/IO or one-way local settlement is missing.");
            if(empty)
            {
                await owner.RetireEmptyReservationAsync(evidence.Value.Child,evidence.Value.Exit,evidence.Value.Io).ConfigureAwait(false);
                lock(sync)next="DONE"; // No local notification is fabricated for a never-bound reservation.
            }
            else await owner.RetireSettingsAsync(evidence.Value.Child,evidence.Value.Exit,evidence.Value.Io).ConfigureAwait(false);
        }
        catch(Exception error)
        {
            lock(sync){unknown=true;Errors.Add(error);}owner.Disconnected();
            if(original.Exception is {} all)InstallationErrors.Throw(all.InnerExceptions);throw;
        }
    }
}

// App side. This participant is internal and never grants registration or changes the false-only settings policy.
internal sealed class InstallationStartupRemovalClient:ISettingsStartupRemovalParticipant
{
    private readonly InstallationControlledLaunch proof;
    private readonly Func<string,CancellationToken,Task> send;
    internal InstallationStartupRemovalClient(InstallationControlledLaunch proof,Func<string,CancellationToken,Task> send){this.proof=proof;this.send=send;}
    private readonly object sync=new();
    private readonly List<Task> originals=[];
    private TaskCompletionSource? response;
    private string? expected;
    private Guid request;
    private bool disconnected,used;
    private Task? final;
    private bool aborted;
    private string? basisVersion,durable;
    private SettingsStartupIntent? intent;
    private bool notified;
    internal bool OriginalsSettled{get{lock(sync)return originals.All(t=>t.IsCompleted);}}
    private Task Command(string verb,string? basis=null,CancellationToken cancellation=default)
    {
        lock(sync)
        {
            if(disconnected||response is{Task.IsCompleted:false})throw new InstallationRecoveryException("Original startup response is unavailable.");
            var receipt=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);response=receipt;expected=verb+"_OK";
            var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var original=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);await send(InstallationStartupRemovalWire.Encode(proof,request,verb,basis),cancellation).ConfigureAwait(false);await receipt.Task.ConfigureAwait(false);});
            originals.Add(original);begin.SetResult();
            // Bound only observation. The exact write/receipt task stays owned and is joined on the original lifecycle.
            return original.WaitAsync(TimeSpan.FromSeconds(2));
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
        Task[] tasks;lock(sync)tasks=originals.ToArray();var errors=new List<Exception>();var all=Task.WhenAll(tasks);
        try{await all.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);}
        catch(Exception observed)
        {
            foreach(var task in tasks.Where(t=>t.IsFaulted))foreach(var error in task.Exception!.InnerExceptions)
                if(error is not StartupRemovalRefusedException||!aborted||final is not{IsCompletedSuccessfully:true})errors.Add(error);
            foreach(var task in tasks.Where(t=>t.IsCanceled))errors.Add(new TaskCanceledException(task));
            if(!all.IsCompleted)errors.Add(observed); // Still pending: no cancellation/disposal or fake original completion.
        }
        InstallationErrors.Throw(errors);
    }
    public async Task<ISettingsStartupRemoval> ReserveAsync(string basisVersion,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();lock(sync){if(used||disconnected)throw new InstallationRecoveryException("Startup slot is one-shot.");used=true;request=Guid.NewGuid();this.basisVersion=basisVersion;}
        // No cancellation waiter drops this original write/receipt. After GRANTED, cancellation rolls back on the same owner.
        await Command("RESERVE",basisVersion).ConfigureAwait(false);return new Reservation(this,request);
    }
    private sealed class Reservation(InstallationStartupRemovalClient client,Guid request):ISettingsStartupRemoval
    {
        public Guid Request=>request;
        public bool SaveStarted{get;private set;}
        public void SettingsSaveStarted()=>SaveStarted=true;
        public void BindIntent(string directory,SettingsStartupIntent value)
        {
            lock(client.sync)
            {
                if(client.intent!=null||value.Request!=request||value.WriteId==Guid.Empty||value.BasisVersion!=client.basisVersion||
                    !InstallationPackage.HashValid(value.Sha256)||Path.GetFullPath(directory)!=Path.Combine(client.proof.UserData,"config"))throw new InstallationRecoveryException("Startup settings must use the original fixed data/config path.");
                client.intent=value;
            }
        }
        public Task ApplyAsync(CancellationToken cancellation){cancellation.ThrowIfCancellationRequested();return client.Command("APPLY",InstallationStartupRemovalWire.Payload(client.intent??throw new InstallationRecoveryException("No original settings intent.")));}
        public Task CommitAsync(SettingsSnapshot saved)
        {
            lock(client.sync)
            {
                if(saved.Data.LaunchAtSignIn)throw new InstallationRecoveryException("This participant only removes startup.");
                return client.final??=Finish("COMMIT","COMMITTED");
            }
        }
        private async Task Finish(string verb,string result)
        {await client.Command(verb,verb=="ABORT"&&client.intent!=null?InstallationStartupRemovalWire.Payload(client.intent):null).ConfigureAwait(false);lock(client.sync)client.durable=result;}
        public Task AbortAsync(){lock(client.sync){if(client.final!=null)return client.final;client.aborted=true;return client.final=Finish("ABORT","ABORTED");}}
        public async Task LocalSettledAsync(string outcome,string configVersion,bool cleanupConfirmed)
        {
            Task original;
            lock(client.sync)
            {
                var binding=client.intent??throw new InstallationRecoveryException("No original local intent.");
                if(client.notified||client.disconnected||client.durable!=outcome)throw new InstallationRecoveryException("Local settlement is one-shot and requires the original durable result receipt.");client.notified=true;
                var notice=new SettingsStartupLocalSettlement(request,binding.WriteId,binding.Sha256,outcome,configVersion,cleanupConfirmed);
                var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                original=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);await client.send(InstallationStartupRemovalWire.Encode(client.proof,request,"LOCAL_SETTLED",InstallationStartupRemovalWire.Payload(notice)),CancellationToken.None).ConfigureAwait(false);});
                client.originals.Add(original);begin.SetResult();
            }
            await original.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); // No reply; the original write remains tracked if observation expires.
        }
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
