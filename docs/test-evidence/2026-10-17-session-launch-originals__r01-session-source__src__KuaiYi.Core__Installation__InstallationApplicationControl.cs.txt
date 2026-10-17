using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
namespace KuaiYi.Core.Installation;

internal static class InstallationControlWire
{
    internal static async Task<string?> Read(Stream input,CancellationToken cancellation)
    {
        using var bytes=new MemoryStream();byte[] one=new byte[1];
        while(await input.ReadAsync(one,cancellation).ConfigureAwait(false)!=0)
        {
            if(one[0]==10)return Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\r');
            if(one[0] is <32 or >126||bytes.Length>=65536)throw new InvalidDataException("Invalid or oversized installation control line.");bytes.WriteByte(one[0]);
        }
        if(bytes.Length!=0)throw new EndOfStreamException("Incomplete installation control line.");return null;
    }
    internal static async Task Send(Stream output,string line,CancellationToken cancellation)
    {byte[] bytes=Encoding.ASCII.GetBytes(line+"\n");await output.WriteAsync(bytes,cancellation).ConfigureAwait(false);await output.FlushAsync(cancellation).ConfigureAwait(false);}
}

/// <summary>Production controller-side protocol for the exact Start-returned child. No discovery, PID or model work.</summary>
public interface IInstallationSessionProtocol:IInstallationControlledStartProtocol
{
    Task SettleSessionAsync(Process owned,CancellationToken cancellation);
}
public sealed class InstallationApplicationClient:IInstallationHandoffProtocol,IInstallationSessionProtocol
{
    private sealed class ChannelState
    {
        internal readonly CancellationTokenSource Readers=new();
        internal readonly Channel<string> Lines=System.Threading.Channels.Channel.CreateBounded<string>(8);
        internal readonly Task Output,Error;
        internal InstallationControlledLaunch? Proof;
        internal bool Closed;
        internal bool ClosingReceived;
        private Task? sessionJoin;
        private readonly object joinSync=new();private Task? join;
        internal ChannelState(Process child){Output=Task.Run(()=>Read(child));Error=Task.Run(()=>Drain(child));}
        private async Task Read(Process child)
        {
            try
            {
                while(await InstallationControlWire.Read(child.StandardOutput.BaseStream,Readers.Token).ConfigureAwait(false) is {} line)
                {
                    if(Proof is {} proof&&line==$"CLOSING|{proof.Launch:N}|{proof.Fingerprint}")
                    {if(ClosingReceived)throw new InvalidDataException("Duplicate application close receipt.");ClosingReceived=true;child.StandardInput.Close();continue;}
                    await Lines.Writer.WriteAsync(line,Readers.Token).ConfigureAwait(false);
                }
            }
            catch(OperationCanceledException)when(Readers.IsCancellationRequested){}
            catch(Exception error){Lines.Writer.TryComplete(error);throw;}
            finally{Lines.Writer.TryComplete();}
        }
        private async Task Drain(Process child)
        {
            byte[] bytes=new byte[1024];int total=0;
            try{int count;while((count=await child.StandardError.BaseStream.ReadAsync(bytes,Readers.Token).ConfigureAwait(false))!=0){total+=count;if(total>65536)throw new IOException("Application diagnostic stream overflow.");}}
            catch(OperationCanceledException)when(Readers.IsCancellationRequested){}
        }
        internal Task Join()
        {
            lock(joinSync)return join??=Task.Run(async()=>{Task cancellation=Readers.CancelAsync();await Task.WhenAll(cancellation,Output,Error).ConfigureAwait(false);Closed=true;});
        }
        internal Task JoinSession()
        {
            lock(joinSync)return sessionJoin??=Task.Run(async()=>
            {
                // Normal user exit must end both original streams naturally, including any late fault.
                await Task.WhenAll(Output,Error).ConfigureAwait(false);
                if(!ClosingReceived||Lines.Reader.TryRead(out _))throw new IOException("Application clean-close receipt is unconfirmed.");
                Closed=true;
            });
        }
    }
    private readonly ConditionalWeakTable<Process,ChannelState> channels=new();
    private readonly object channelsSync=new();
    private ChannelState Channel(Process owned){lock(channelsSync)return channels.GetValue(owned,p=>new(p));}
    private async Task Receipt(Process owned,string expected,CancellationToken cancellation)
    {if(await Channel(owned).Lines.Reader.ReadAsync(cancellation).ConfigureAwait(false)!=expected)throw new InvalidDataException("Application control receipt mismatch.");}
    private static Task Send(Process owned,string line,CancellationToken cancellation)=>InstallationControlWire.Send(owned.StandardInput.BaseStream,line,cancellation);
    public async Task AdmitLaunchAsync(Process owned,InstallationControlledLaunch proof,CancellationToken cancellation)
    {
        var state=Channel(owned);if(state.Proof!=null)throw new InvalidOperationException("Child launch already admitted.");state.Proof=proof;
        await Send(owned,"START|"+proof.Encode(),cancellation).ConfigureAwait(false);await Receipt(owned,$"ADMITTED|{proof.Launch:N}|{proof.Fingerprint}",cancellation).ConfigureAwait(false);
    }
    public Task ConfirmHealthyAsync(Process owned,InstalledVersion version,Guid launch,CancellationToken cancellation)
    {
        var proof=Channel(owned).Proof??throw new InvalidOperationException("No controlled launch proof.");
        if(proof.Version!=version||proof.Launch!=launch)throw new InvalidDataException("Health launch mismatch.");return Receipt(owned,$"READY|{launch:N}|{proof.Fingerprint}",cancellation);
    }
    public Task RequestExitAsync(Process owned,CancellationToken cancellation)=>Send(owned,"STOP",cancellation);
    public async Task SettleAsync(Process owned,CancellationToken cancellation)
    {
        if(!owned.HasExited)throw new IOException("Exited IO settlement requires actual child exit.");
        await Channel(owned).Join().WaitAsync(cancellation).ConfigureAwait(false);
    }
    public async Task SettleSessionAsync(Process owned,CancellationToken cancellation)
    {
        if(!owned.HasExited)throw new IOException("Session settlement requires actual child exit.");
        await Channel(owned).JoinSession().WaitAsync(cancellation).ConfigureAwait(false);
    }
    public async Task PrepareHandoffAsync(Process owned,InstallationHandoffOffer offer,CancellationToken cancellation)
    {await Send(owned,"OFFER|"+offer.Encode(),cancellation).ConfigureAwait(false);await Receipt(owned,Reply("PREPARED",offer),cancellation).ConfigureAwait(false);}
    public async Task AcceptHandoffAsync(Process owned,InstallationHandoffOffer offer,CancellationToken cancellation)
    {await Send(owned,"ACQUIRE",cancellation).ConfigureAwait(false);await Receipt(owned,Reply("ACCEPTED",offer),cancellation).ConfigureAwait(false);}
    public async Task CompleteControlAsync(Process owned,InstallationHandoffOffer offer,CancellationToken cancellation)
    {
        var state=Channel(owned);if(state.Closed)return;
        try{await Send(owned,"COMMIT",cancellation).ConfigureAwait(false);await Receipt(owned,Reply("COMMITTED",offer),cancellation).ConfigureAwait(false);}
        finally{owned.StandardInput.Close();await state.Join().ConfigureAwait(false);}
    }
    internal static string Reply(string phase,InstallationHandoffOffer offer)=>$"{phase}|{offer.Transfer:N}|{offer.Receiver:N}|{offer.Launch:N}";
}

/// <summary>Application side. UI readiness and close dispatch are supplied by the existing application lifecycle.
/// No success is inferred from a close request, EOF or a cancellation waiter. Keep this object rooted until process exit.</summary>
public sealed class InstallationApplicationControl
{
    private readonly Stream input,output;
    private readonly CancellationTokenSource stop=new();
    private Task? loop;
    private Task? cancellationWork;
    private InstallationRuntimeOwnership? runtime;
    private InstallationHandoffOffer? offer;
    private bool committed;
    private readonly object stateSync=new();
    private readonly SemaphoreSlim outputSerial=new(1,1);
    private InstallationApplicationControl(Stream input,Stream output,InstallationControlledLaunch proof){this.input=input;this.output=output;Proof=proof;}
    public InstallationControlledLaunch Proof{get;}
    public string? FailureCode{get;private set;}
    public static async Task<InstallationApplicationControl> AdmitAsync(Stream input,Stream output,Guid launch,string directory,string userData,CancellationToken cancellation=default)
    {
        string line=await InstallationControlWire.Read(input,cancellation).ConfigureAwait(false)??throw new EndOfStreamException("Controlled startup requires an inherited proof.");
        if(!line.StartsWith("START|",StringComparison.Ordinal))throw new InvalidDataException("Controlled startup proof required.");
        var proof=InstallationControlledLaunch.Decode(line[6..]);proof.Claim(launch,directory,userData);
        await InstallationControlWire.Send(output,$"ADMITTED|{launch:N}|{proof.Fingerprint}",cancellation).ConfigureAwait(false);return new(input,output,proof);
    }
    public Task RunAsync(Task applicationReady,Func<CancellationToken,Task> requestClose)
    {
        lock(stateSync)
        {
        if(loop!=null)throw new InvalidOperationException("Application control loop already started.");
        var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        loop=Task.Run(async()=>
        {
            await begin.Task.ConfigureAwait(false);
            try
            {
                await applicationReady.WaitAsync(stop.Token).ConfigureAwait(false);
                await Send($"READY|{Proof.Launch:N}|{Proof.Fingerprint}",stop.Token).ConfigureAwait(false);
                while(await InstallationControlWire.Read(input,stop.Token).ConfigureAwait(false) is {} command)
                {
                    if(command=="STOP"){await requestClose(stop.Token).ConfigureAwait(false);continue;}
                    if(command.StartsWith("OFFER|",StringComparison.Ordinal))
                    {
                        if(offer!=null)throw new InvalidDataException("Duplicate handoff offer.");var candidate=InstallationHandoffOffer.Decode(command[6..]);
                        if(candidate.Launch!=Proof.Launch||candidate.Root!=Proof.Root||candidate.UserData!=Proof.UserData||candidate.Version!=Proof.Version||candidate.OwnerRecord!=Proof.OwnerRecord||candidate.CurrentRecord!=Proof.CurrentRecord||candidate.UpgradeRecord!=Proof.UpgradeRecord)throw new InvalidDataException("Handoff differs from admitted launch.");
                        runtime=InstallationRuntimeOwnership.Prepare(candidate,Proof.Launch);offer=candidate;await Reply("PREPARED").ConfigureAwait(false);
                    }
                    else if(command=="ACQUIRE"){runtime!.Acquire();await Reply("ACCEPTED").ConfigureAwait(false);}
                    else if(command=="COMMIT"){runtime!.ConfirmCommitted();committed=true;await Reply("COMMITTED").ConfigureAwait(false);}
                    else throw new InvalidDataException("Unknown installation command.");
                }
                if(runtime==null)await requestClose(stop.Token).ConfigureAwait(false);
                else{runtime.ConfirmCommitted();committed=true;} // EOF cannot create a commit; a durable exact Committed decision is required.
            }
            catch(OperationCanceledException)when(stop.IsCancellationRequested){}
            catch(Exception error)
            {
                FailureCode=error.GetType().Name;
                if(!committed&&runtime!=null)try{runtime.ConfirmCommitted();committed=true;}catch(Exception check)when(check is IOException or InvalidDataException){}
                // Even a lost COMMITTED output ACK must not close an already committed application.
                if(!committed)
                    try{await requestClose(CancellationToken.None).ConfigureAwait(false);}
                    catch(Exception closeFailure){throw new AggregateException(error,closeFailure);}
                throw;
            }
        });
        begin.TrySetResult();return loop;
        }
    }
    private Task Reply(string phase)=>Send(InstallationApplicationClient.Reply(phase,offer!),stop.Token);
    private async Task Send(string line,CancellationToken cancellation)
    {
        await outputSerial.WaitAsync(cancellation).ConfigureAwait(false);
        try{await InstallationControlWire.Send(output,line,cancellation).ConfigureAwait(false);}
        finally{outputSerial.Release();}
    }
    public async Task SettleForExitAsync()
    {
        // Retain the original cancellation work even when the calling UI/task stops waiting.
        lock(stateSync)cancellationWork??=Task.Run(async()=>
        {
            Task cancellation=stop.CancelAsync(); // Start cancellation before waiting behind an original protocol write.
            try
            {
                if(loop is {IsCompleted:false})
                    try{await Send($"CLOSING|{Proof.Launch:N}|{Proof.Fingerprint}",CancellationToken.None).ConfigureAwait(false);}
                    catch(Exception error)when(error is IOException or ObjectDisposedException){FailureCode??=error.GetType().Name;}
            }
            finally{await cancellation.ConfigureAwait(false);}
        });
        try{await cancellationWork.ConfigureAwait(false);}
        finally
        {
            if(loop!=null)try{await loop.ConfigureAwait(false);}catch(Exception error)when(loop.IsCompleted){FailureCode??=error.GetType().Name;_ =loop.Exception;}
            GC.KeepAlive(runtime);
        }
    }
}
