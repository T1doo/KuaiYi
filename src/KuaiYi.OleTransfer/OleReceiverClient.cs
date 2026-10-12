using System.Diagnostics;
using System.Security.Cryptography;
using KuaiYi.Contracts;
using KuaiYi.Core;
namespace KuaiYi.OleTransfer;
public enum OleReceiverFailure { Protocol,Timeout,ExitUnconfirmed,RegistrarUnconfirmed,SettlementUnconfirmed }
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
    private Process? child;private Guid instance;private int started,ownedProcessId;private volatile bool ready,timedOut;
    /// <summary>Cached diagnostic identity of the actual owned child; retained after finalization, never used to kill by PID.</summary>
    public int? OwnedProcessId{get{int id=Volatile.Read(ref ownedProcessId);return id==0?null:id;}}
    private Task? registration;
    private Task? revocation;
    private readonly HashSet<Task> readSettlements=[];
    internal Func<Task<IpcFrame?>,Task>? BeforeReadSettlementForTest{get;set;}
    internal Action<byte[]>? PayloadReleasedForTest{get;set;}
    internal Action? LateReadObserverStartedForTest{get;set;}
    internal Task? LateReadObserverForTest{get;private set;}
    public string? ReadFailureCode{get;private set;}
    private Task? deliverySettlement;private Guid delivering;private long deliveringEpoch,deliveringStarted;private bool hasDeliveryDeadline;
    private TimeProvider Clock=>timeProvider??TimeProvider.System;
    public string? DeliveryFailureCode{get;private set;}
    internal Func<Process,IOwnedHostLifetime>? LifetimeForTest{init{process.LifetimeForTest=value;}}
    internal Action<Process>? OwnershipPublishedForTest{init{process.OwnershipPublishedForTest=value;}}
    internal Task StartupSettlementForTest=>process.StartupSettlement;
    internal bool DeliveryPublishedForTest=>Volatile.Read(ref deliverySettlement)!=null;
    internal Task DeliverySettlementForTest=>Volatile.Read(ref deliverySettlement)??Task.CompletedTask;
    internal Task RevocationSettlementForTest{get{lock(watchSync)return revocation??Task.CompletedTask;}}
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
        Task? remoteRevoke;lock(watchSync)remoteRevoke=revocation;
        bool finalized=exited&&revoked&&Volatile.Read(ref started)!=1&&registration is not {IsCompleted:false}&&registrar.IsSettledAndRevoked&&Volatile.Read(ref deliverySettlement) is not {IsCompleted:false}&&remoteRevoke is not {IsCompleted:false};
        lock(watchSync)finalized=finalized&&readSettlements.All(task=>task.IsCompleted);if(finalized)activation.Dispose();return finalized;
    }
    public async ValueTask DisposeAsync(){if(!await TryFinalizeAsync())throw new OleReceiverException(!process.ObserveExit()?OleReceiverFailure.ExitUnconfirmed:!registrar.IsSettledAndRevoked?OleReceiverFailure.RegistrarUnconfirmed:OleReceiverFailure.SettlementUnconfirmed);}
    /// <summary>Destination-thread admission, serialized with cancellation. Caller performs only bounded local application, no waits or external COM.</summary>
    public bool TryApplyLocal(long epoch,Action apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        lock(activationSync){if(!IsReady||!eligibility.Current(epoch))return false;apply();return true;}
    }
    public bool IsDropCurrent(long epoch,Guid transaction)
    {lock(activationSync){lock(watchSync)return IsReady&&eligibility.Current(epoch)&&hasDeliveryDeadline&&delivering==transaction&&deliveringEpoch==epoch&&Clock.GetElapsedTime(deliveringStarted)<TimeSpan.FromMilliseconds(1500);}}
    public bool TryApplyDrop(long epoch,Guid transaction,Action apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        lock(activationSync)
        {
            lock(watchSync)if(!IsReady||!eligibility.Current(epoch)||!hasDeliveryDeadline||delivering!=transaction||deliveringEpoch!=epoch||Clock.GetElapsedTime(deliveringStarted)>=TimeSpan.FromMilliseconds(1500))return false;
            apply();return true;
        }
    }
    public async Task<bool> SetEnabledAsync(long epoch,bool enabled,DropTargetGeometry? target=null,string[]? blockedApplications=null)
    {
        lock(activationSync){if(!IsReady||!eligibility.Set(epoch,enabled))return false;activation.Set(epoch,enabled);}
        byte[] payload=OleReceiverWire.Serialize(new OleReceiverControl(instance,epoch,enabled,target,blockedApplications));
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
        OleReceiverWire.Validate(hello);if(hello.ActivationName!=activation.Name)throw new InvalidDataException("Wrong owned activation mapping.");if(Interlocked.CompareExchange(ref started,1,0)!=0)throw new InvalidOperationException("One receiver per client.");
        try{await RunOwnedAsync(hello,feedback,deliver,cancellation).ConfigureAwait(false);}
        finally{Volatile.Write(ref started,2);} // Entire original admission/read/registration/cleanup path has returned.
    }
    private async Task RunOwnedAsync(OleReceiverHello hello,Action<OleReceiverEvent> feedback,Func<long,Guid,ReadOnlyMemory<byte>,CancellationToken,Task> deliver,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        lock(activationSync){instance=hello.Instance;eligibility.Set(hello.Epoch,false);activation.Set(hello.Epoch,false);}
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(cancellation,stop.Token);
        using var startup=new CancellationTokenSource(TimeSpan.FromSeconds(10),timeProvider??TimeProvider.System);
        using var beginning=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,startup.Token);
        byte[]? pending=null;Guid transaction=Guid.Empty,queryIdentity=Guid.Empty,dropIdentity=Guid.Empty;long payloadEpoch=0;Task? watchdog=null;SelectionWindow? sourceIdentity=null;PixelPoint? dropPoint=null;OleTextFormat selectedFormat=OleTextFormat.None;
        var starting=process.Start(OwnedProcessSession.Info(executable,managedAssembly),beginning.Token);
        lock(watchSync)watch.Start(instance,hello.Epoch);
        try
        {
            child=await starting.WaitAsync(beginning.Token).ConfigureAwait(false);
            Volatile.Write(ref ownedProcessId,child.Id);
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
                    if(hello.ProductionTextDrop&&item.Phase is "QueryBegin" or "Hover" or "QueryEnd" or "DropBegin" or "CleanupComplete" or "DropEnd")
                    {
                        if(item.Source is not {} origin||origin.Handle==0||origin.ProcessId==0||origin.ProcessStartUtcTicks<=0||origin.Executable is not {Length:>0 and <=260}||item.Point==null||!Enum.IsDefined(item.TextFormat)||item.TextFormat==OleTextFormat.None&&item.Phase!="QueryBegin"&&!(item.Phase=="QueryEnd"&&item.Effect==0))throw new OleReceiverException(OleReceiverFailure.Protocol);
                    }
                    if(hello.ProductionTextDrop)
                    {
                        if(item.Phase=="QueryBegin"){queryIdentity=item.Transaction;sourceIdentity=item.Source;selectedFormat=OleTextFormat.None;dropIdentity=Guid.Empty;dropPoint=null;}
                        else if(item.Phase=="QueryEnd"){if(item.Transaction!=queryIdentity||item.Source!=sourceIdentity)throw new OleReceiverException(OleReceiverFailure.Protocol);selectedFormat=item.TextFormat;}
                        else if(item.Phase=="DropBegin")
                        {
                            if(item.Transaction!=queryIdentity||item.Source!=sourceIdentity||item.TextFormat!=selectedFormat||selectedFormat==OleTextFormat.None||item.Effect!=1||dropIdentity!=Guid.Empty&&item.Point!=dropPoint)throw new OleReceiverException(OleReceiverFailure.Protocol);
                            dropIdentity=item.Transaction;dropPoint=item.Point;
                        }
                        else if(item.Phase is "CleanupComplete" or "DropEnd")
                        {if(item.Transaction!=dropIdentity||item.Source!=sourceIdentity||item.TextFormat!=selectedFormat||item.Point!=dropPoint)throw new OleReceiverException(OleReceiverFailure.Protocol);}
                    }
                    OleWatchDecision decision;lock(watchSync)decision=watch.Accept(frame.RequestId,item.Epoch,item.Phase,item.CleanupCompleted);
                    if(item.Phase=="DropBegin"&&decision==OleWatchDecision.Progress)lock(watchSync){if(!hasDeliveryDeadline||delivering!=item.Transaction||deliveringEpoch!=item.Epoch){delivering=item.Transaction;deliveringEpoch=item.Epoch;deliveringStarted=Clock.GetTimestamp();hasDeliveryDeadline=true;}}
                    if(decision==OleWatchDecision.Rejected)throw new OleReceiverException(OleReceiverFailure.Timeout);
                    if(item.Phase=="DropEnd")
                    {
                        if(decision!=OleWatchDecision.Completed||!item.CleanupCompleted)throw new OleReceiverException(OleReceiverFailure.Protocol);
                        if(pending!=null)
                        {
                            if(transaction!=item.Transaction||payloadEpoch!=item.Epoch||item.Effect!=1||!eligibility.Current(item.Epoch)||OleReceiverWire.DecodeText(pending).Length!=item.CodeUnits)throw new OleReceiverException(OleReceiverFailure.Protocol);
                            lifetime.Token.ThrowIfCancellationRequested();
                            if(hello.ProductionTextDrop&&!TryApplyDrop(item.Epoch,item.Transaction,static()=>{}))throw new OleReceiverException(OleReceiverFailure.Timeout);
                            Task originalDelivery=deliver(item.Epoch,item.Transaction,pending,lifetime.Token);
                            Task settlement=ObserveDelivery(originalDelivery,pending);Volatile.Write(ref deliverySettlement,settlement);pending=null; // Original callback owns the read lease until actual settlement, even when our waiter cancels.
                            await settlement.WaitAsync(lifetime.Token).ConfigureAwait(false); // Finish clearing this lease before accepting another payload.
                            await originalDelivery.ConfigureAwait(false); // Preserve the original error for the active caller.
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
        async Task<IpcFrame> Read(CancellationToken token)=>await ReadOwnedAsync(child!.StandardOutput.BaseStream,token).ConfigureAwait(false)??throw new EndOfStreamException();
        async Task Watchdog(CancellationToken token)
        {
            try{while(true){await Task.Delay(TimeSpan.FromMilliseconds(25),timeProvider??TimeProvider.System,token);lock(watchSync)if(watch.Expired){timedOut=true;lifetime.Cancel();return;}}}
            catch(OperationCanceledException)when(token.IsCancellationRequested){/* Owned loop shutdown. */}
        }
    }
    internal async Task<IpcFrame?> ReadOwnedAsync(Stream stream,CancellationToken cancellation)
    {
        var reading=IpcFrameCodec.ReadAsync(stream,cancellation).AsTask();bool delivered=false;
        try{var result=await reading.WaitAsync(cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();delivered=true;return result;}
        finally
        {
            if(BeforeReadSettlementForTest!=null)await BeforeReadSettlementForTest(reading).ConfigureAwait(false);
            if(!delivered){var observer=ObserveRead(reading);LateReadObserverForTest=observer;lock(watchSync)readSettlements.Add(observer);_ =RemoveReadSettlement(observer);}
        }
    }
    private async Task RemoveReadSettlement(Task original){await original.ConfigureAwait(false);lock(watchSync)readSettlements.Remove(original);}
    private async Task ObserveRead(Task<IpcFrame?> reading)
    {
        LateReadObserverStartedForTest?.Invoke();try{var frame=await reading.ConfigureAwait(false);if(frame!=null){CryptographicOperations.ZeroMemory(frame.Payload);PayloadReleasedForTest?.Invoke(frame.Payload);}}
        catch(Exception error){ReadFailureCode=error.GetType().Name;}
    }
    private async Task ObserveDelivery(Task original,byte[] owned)
    {
        try{await original.ConfigureAwait(false);}
        catch(Exception error){DeliveryFailureCode=error.GetType().Name;} // Original failure is observed; current loop reports it if still waiting.
        finally{CryptographicOperations.ZeroMemory(owned);}
    }
    private static async Task ObserveRegistration(Task task){try{await task.ConfigureAwait(false);}catch(Exception){/* Registrar retains finite failure state and original native work until settlement. */}}
}
