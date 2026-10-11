using System.Diagnostics;
using System.Security.Cryptography;
using KuaiYi.Contracts;
using KuaiYi.Core;
namespace KuaiYi.OleTransfer;
public enum OleReceiverFailure { Protocol,Timeout,ExitUnconfirmed,RegistrarUnconfirmed }
public sealed class OleReceiverException(OleReceiverFailure failure):Exception(failure.ToString()){public OleReceiverFailure Failure{get;}=failure;}
/// <summary>Presentation does not await this loop synchronously. Own child and original registrar work remain retained.</summary>
public sealed class OleReceiverClient(string executable,IOleTargetRegistrar registrar,IOleActivationLease activation,string? managedAssembly=null,IOwnedProcessStarter? starter=null,TimeProvider? timeProvider=null):IAsyncDisposable
{
    private readonly OwnedProcessSession process=new(starter);
    private readonly SemaphoreSlim writer=new(1);
    private readonly CancellationTokenSource stop=new();
    private readonly OleReceiverGate eligibility=new();
    private readonly object watchSync=new();
    private readonly object activationSync=new();
    private readonly OleCallWatch watch=new(timeProvider);
    private Process? child;private Guid instance;private int started;private volatile bool ready,timedOut;
    private Task? registration;
    private Task? revocation;
    internal Func<Process,IOwnedHostLifetime>? LifetimeForTest{init{process.LifetimeForTest=value;}}
    internal Action<Process>? OwnershipPublishedForTest{init{process.OwnershipPublishedForTest=value;}}
    internal Task StartupSettlementForTest=>process.StartupSettlement;
    public bool IsReady=>ready&&!stop.IsCancellationRequested;
    public bool IsBusy{get{lock(watchSync)return watch.IsArmed;}}
    public HostExitState? ExitState=>process.ExitState;
    public bool ObserveExit()=>process.ObserveExit();
    public void Cancel()
    {
        lock(activationSync){ready=false;eligibility.Set(long.MaxValue,false);activation.Set(long.MaxValue,false);stop.Cancel();} // Direct parent store, serialized against enable publication.
        if(child is {} owned)lock(watchSync)revocation??=RevokeRemote(owned,instance);
    }
    private async Task RevokeRemote(Process owned,Guid scope)
    {
        byte[] payload=OleReceiverWire.Serialize(new OleReceiverControl(scope,long.MaxValue,false));using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(1));bool entered=false;
        try
        {
            await writer.WaitAsync(limit.Token).ConfigureAwait(false);entered=true;
            await IpcFrameCodec.WriteAsync(owned.StandardInput.BaseStream,new(IpcMessageType.OleControl,scope,payload),limit.Token);await owned.StandardInput.BaseStream.FlushAsync(limit.Token);
        }
        catch(Exception e)when(e is IOException or OperationCanceledException or ObjectDisposedException or InvalidOperationException){/* Owned remote revoke failed; owner/registration remain retained if termination cannot be confirmed. */}
        finally{CryptographicOperations.ZeroMemory(payload);if(entered)writer.Release();}
    }
    public async ValueTask<bool> TryFinalizeAsync(bool retry=false)
    {
        Cancel();bool exited=await process.TryFinalizeAsync(retry).ConfigureAwait(false);
        bool revoked=await registrar.TryFinalizeAsync().ConfigureAwait(false);
        bool finalized=exited&&revoked&&registration is not {IsCompleted:false}&&registrar.IsSettledAndRevoked;
        if(finalized)activation.Dispose();return finalized;
    }
    public async ValueTask DisposeAsync(){if(!await TryFinalizeAsync())throw new OleReceiverException(OleReceiverFailure.ExitUnconfirmed);}
    public async Task<bool> SetEnabledAsync(long epoch,bool enabled)
    {
        lock(activationSync){if(!IsReady||!eligibility.Set(epoch,enabled))return false;activation.Set(epoch,enabled);}
        byte[] payload=OleReceiverWire.Serialize(new OleReceiverControl(instance,epoch,enabled));
        try
        {
            await writer.WaitAsync(stop.Token).ConfigureAwait(false);
            try{if(!IsReady||child==null)return false;await IpcFrameCodec.WriteAsync(child.StandardInput.BaseStream,new(IpcMessageType.OleControl,instance,payload),stop.Token);await child.StandardInput.BaseStream.FlushAsync(stop.Token);return true;}
            finally{writer.Release();}
        }
        catch(Exception error)when(error is IOException or OperationCanceledException or ObjectDisposedException){Cancel();return false;}
        finally{CryptographicOperations.ZeroMemory(payload);}
    }
    public async Task RunAsync(OleReceiverHello hello,Action<OleReceiverEvent> feedback,Func<long,Guid,ReadOnlyMemory<byte>,CancellationToken,Task> deliver,CancellationToken cancellation=default)
    {
        OleReceiverWire.Validate(hello);if(hello.ActivationName!=activation.Name)throw new InvalidDataException("Wrong owned activation mapping.");if(Interlocked.Exchange(ref started,1)!=0)throw new InvalidOperationException("One receiver per client.");cancellation.ThrowIfCancellationRequested();
        lock(activationSync){instance=hello.Instance;eligibility.Set(hello.Epoch,false);activation.Set(hello.Epoch,false);}
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(cancellation,stop.Token);
        using var startup=new CancellationTokenSource(TimeSpan.FromSeconds(10),timeProvider??TimeProvider.System);
        using var beginning=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,startup.Token);
        byte[]? pending=null;Guid transaction=Guid.Empty;long payloadEpoch=0;Task? watchdog=null;
        var starting=process.Start(OwnedProcessSession.Info(executable,managedAssembly),beginning.Token);
        lock(watchSync)watch.Start(instance,hello.Epoch);
        try
        {
            child=await starting.WaitAsync(beginning.Token).ConfigureAwait(false);
            byte[] command=OleReceiverWire.Serialize(hello);
            try{await IpcFrameCodec.WriteAsync(child.StandardInput.BaseStream,new(IpcMessageType.OleHello,instance,command),beginning.Token);await child.StandardInput.BaseStream.FlushAsync(beginning.Token);}
            finally{CryptographicOperations.ZeroMemory(command);}
            var response=await Read(beginning.Token);
            bool transferred=false;
            try
            {
                if(response.Type!=IpcMessageType.OleHello||response.RequestId!=instance||!CryptographicOperations.FixedTimeEquals(response.Payload,hello.Nonce))throw new OleReceiverException(OleReceiverFailure.Protocol);
            }finally{CryptographicOperations.ZeroMemory(response.Payload);}
            var export=await Read(beginning.Token);
            try
            {
                if(export.Type!=IpcMessageType.OleExport||export.RequestId!=instance)throw new OleReceiverException(OleReceiverFailure.Protocol);
                OleReceiverWire.ValidateExport(export.Payload);transferred=true;
                registration=registrar.RegisterAsync(hello.Hwnd,export.Payload,lifetime.Token);
                await registration.WaitAsync(beginning.Token).ConfigureAwait(false);
                beginning.Token.ThrowIfCancellationRequested();lock(watchSync){if(watch.Expired)throw new OleReceiverException(OleReceiverFailure.Timeout);watch.Reset();}
            }finally{if(!transferred)CryptographicOperations.ZeroMemory(export.Payload);}
            lock(activationSync){beginning.Token.ThrowIfCancellationRequested();ready=true;}feedback(new(instance,hello.Epoch,Guid.Empty,"Ready"));watchdog=Watchdog(lifetime.Token);
            while(true)
            {
                var frame=await Read(lifetime.Token);bool retained=false;
                try
                {
                    if(frame.Type==IpcMessageType.BinaryChunk)
                    {
                        bool valid;lock(watchSync)valid=watch.CanReadPayload(frame.RequestId,eligibility.Snapshot.Epoch);
                        if(!valid||pending!=null||frame.Payload.Length is <1 or >OleReceiverWire.TextLimit)throw new OleReceiverException(OleReceiverFailure.Protocol);
                        pending=frame.Payload;transaction=frame.RequestId;payloadEpoch=eligibility.Snapshot.Epoch;retained=true;continue;
                    }
                    if(frame.Type!=IpcMessageType.OleEvent)throw new OleReceiverException(OleReceiverFailure.Protocol);
                    var item=OleReceiverWire.Read<OleReceiverEvent>(frame.Payload);
                    if(item.Instance!=instance||item.Transaction!=frame.RequestId||item.Effect is not (0 or 1)||item.CodeUnits is <0 or >OleUnicodeText.MaximumReadUnits)throw new OleReceiverException(OleReceiverFailure.Protocol);
                    if(item.Epoch!=eligibility.Snapshot.Epoch)continue;
                    OleWatchDecision decision;lock(watchSync)decision=watch.Accept(frame.RequestId,item.Epoch,item.Phase,item.CleanupCompleted);
                    if(decision==OleWatchDecision.Rejected)throw new OleReceiverException(OleReceiverFailure.Timeout);
                    if(item.Phase=="DropEnd")
                    {
                        if(decision!=OleWatchDecision.Completed||!item.CleanupCompleted)throw new OleReceiverException(OleReceiverFailure.Protocol);
                        if(pending!=null)
                        {
                            if(transaction!=item.Transaction||payloadEpoch!=item.Epoch||item.Effect!=1||!eligibility.Current(item.Epoch)||OleReceiverWire.DecodeText(pending).Length!=item.CodeUnits)throw new OleReceiverException(OleReceiverFailure.Protocol);
                            try{await deliver(item.Epoch,item.Transaction,pending,lifetime.Token).WaitAsync(lifetime.Token).ConfigureAwait(false);}
                            finally{CryptographicOperations.ZeroMemory(pending);pending=null;}
                        }
                        else if(item.Effect!=0||item.CodeUnits!=0)throw new OleReceiverException(OleReceiverFailure.Protocol);
                    }
                    feedback(item);
                }finally{if(!retained)CryptographicOperations.ZeroMemory(frame.Payload);}
            }
        }
        catch(OperationCanceledException)when((timedOut||startup.IsCancellationRequested)&&!cancellation.IsCancellationRequested&&!stop.IsCancellationRequested){throw new OleReceiverException(OleReceiverFailure.Timeout);}
        finally
        {
            lock(activationSync){ready=false;eligibility.Set(long.MaxValue,false);activation.Set(long.MaxValue,false);}lifetime.Cancel();if(pending!=null)CryptographicOperations.ZeroMemory(pending);
            if(registration!=null)_ = ObserveRegistration(registration);
            await process.StopAsync().ConfigureAwait(false);await registrar.TryFinalizeAsync().ConfigureAwait(false);
            if(watchdog!=null)await watchdog.ConfigureAwait(false);
            if(!process.ObserveExit())throw new OleReceiverException(OleReceiverFailure.ExitUnconfirmed);
            if(registration is {IsCompleted:false}||!registrar.IsSettledAndRevoked)throw new OleReceiverException(OleReceiverFailure.RegistrarUnconfirmed);
        }
        async Task<IpcFrame> Read(CancellationToken token)
        {
            var reading=IpcFrameCodec.ReadAsync(child!.StandardOutput.BaseStream,token).AsTask();
            try
            {
                var frame=await reading.WaitAsync(token).ConfigureAwait(false)??throw new EndOfStreamException();
                try{token.ThrowIfCancellationRequested();return frame;}catch{CryptographicOperations.ZeroMemory(frame.Payload);throw;}
            }
            catch{_ = ObserveRead(reading);throw;}
        }
        async Task Watchdog(CancellationToken token)
        {
            try{while(true){await Task.Delay(TimeSpan.FromMilliseconds(25),timeProvider??TimeProvider.System,token);lock(watchSync)if(watch.Expired){timedOut=true;lifetime.Cancel();return;}}}
            catch(OperationCanceledException)when(token.IsCancellationRequested){/* Owned loop shutdown. */}
        }
    }
    private static async Task ObserveRead(Task<IpcFrame?> reading){try{var frame=await reading.ConfigureAwait(false);if(frame!=null)CryptographicOperations.ZeroMemory(frame.Payload);}catch(Exception e)when(e is IOException or OperationCanceledException or ObjectDisposedException){/* Owned read failure observed. */}}
    private static async Task ObserveRegistration(Task task){try{await task.ConfigureAwait(false);}catch(Exception){/* Registrar retains finite failure state and original native work until settlement. */}}
}
