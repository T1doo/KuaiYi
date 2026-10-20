using KuaiYi.Core;
namespace KuaiYi.Imaging;
public enum ImageIoKind { Read,Write,TailRead }
public enum ImageSettlementStage { IoTracked,OriginalObservedComplete,CleanupObservedComplete,PrimaryFailure,StopReturned,ExitObserved,OperationReturned,FinalizePending,Finalized,CleanupFailure }
public sealed record ImageIoObservation(int Id,ImageIoKind Kind,TaskStatus OriginalStatus,TaskStatus CleanupStatus,long OriginalObservedSequence,long CleanupObservedSequence);
public sealed record ImageSettlementEvent(long Sequence,ImageSettlementStage Stage,int IoId=0);
/// <summary>Safe bounded diagnostics: observation order, not OS completion timestamps. No exception messages or content.</summary>
public sealed record ImageSettlementSnapshot(bool OperationActive,TaskStatus StartupStatus,bool ExitObserved,HostExitState? ExitState,string? PrimaryFailureType,ImageFailure? PrimaryFailureCode,string? CleanupFailureType,ImageFailure? SettlementFailureCode,int TotalIo,int PendingOriginalIo,int PendingCleanup,int FaultedCleanup,bool Truncated,IReadOnlyList<ImageIoObservation> Io,IReadOnlyList<ImageSettlementEvent> Events);
public sealed partial class ImageWorkerClient
{
    private sealed class TrackedIo(int id,ImageIoKind kind,Task original,Task cleanup)
    {internal readonly int Id=id;internal readonly ImageIoKind Kind=kind;internal readonly Task Original=original,Cleanup=cleanup;internal long OriginalObserved,CleanupObserved;}
    private readonly List<TrackedIo> ioSettlements=[];
    private readonly Queue<ImageSettlementEvent> settlementEvents=new();private long settlementSequence;private bool exitObserved;
    private Exception? primaryFailure,cleanupFailure;private ImageFailure? settlementFailureCode;
    // In-memory only. Callers must use SettlementSnapshot for logs, never serialize this exception or its message.
    public Exception? PrimaryFailure=>Volatile.Read(ref primaryFailure);
    public Exception? CleanupFailure=>Volatile.Read(ref cleanupFailure);
    internal Func<ImageIoKind,Task>? BeforeIoClearForTest{get;init;}
    internal Task IoSettlementForTest{get{lock(ioSync)return Task.WhenAll(ioSettlements.Select(x=>x.Cleanup));}}
    private bool IoSettled{get{lock(ioSync)return ioSettlements.All(x=>x.Cleanup.IsCompletedSuccessfully);}}
    private long NoteLocked(ImageSettlementStage stage,int id=0){long n=++settlementSequence;if(settlementEvents.Count==32)settlementEvents.Dequeue();settlementEvents.Enqueue(new(n,stage,id));return n;}
    private void Note(ImageSettlementStage stage){lock(ioSync)NoteLocked(stage);}
    private void ObservePrimary(Exception error){if(Interlocked.CompareExchange(ref primaryFailure,error,null)==null)Note(ImageSettlementStage.PrimaryFailure);}
    private void ObserveCleanup(Exception error){if(Interlocked.CompareExchange(ref cleanupFailure,error,null)==null)Note(ImageSettlementStage.CleanupFailure);}
    private void Track(ImageIoKind kind,Task original,Task cleanup)
    {
        TrackedIo io;lock(ioSync){io=new(ioSettlements.Count+1,kind,original,cleanup);ioSettlements.Add(io);NoteLocked(ImageSettlementStage.IoTracked,io.Id);}
        _=original.ContinueWith(_=>{lock(ioSync)if(io.OriginalObserved==0)io.OriginalObserved=NoteLocked(ImageSettlementStage.OriginalObservedComplete,io.Id);},CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
        _=cleanup.ContinueWith(finished=>{if(finished.IsFaulted)ObserveCleanup(finished.Exception!.InnerException??finished.Exception);lock(ioSync)if(io.CleanupObserved==0)io.CleanupObserved=NoteLocked(ImageSettlementStage.CleanupObservedComplete,io.Id);},CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
    }
    private bool ObserveProcessExit()
    {
        bool exited=process.ObserveExit(); // Never call into process ownership while holding the IO lock.
        bool childStarted=process.ExitState==HostExitState.ExitConfirmed&&process.StartupSettlement.IsCompletedSuccessfully;
        if(exited&&childStarted)lock(ioSync)if(!exitObserved){exitObserved=true;NoteLocked(ImageSettlementStage.ExitObserved);}
        return exited;
    }
    public ImageSettlementSnapshot SettlementSnapshot
    {
        get
        {
            bool exited=ObserveProcessExit();var startup=process.StartupSettlement.Status;var state=process.ExitState;
            // The lifetime gate considers an unused owner settled; that is not evidence that a child exited.
            bool childExited=exited&&state==HostExitState.ExitConfirmed&&startup==TaskStatus.RanToCompletion;
            lock(ioSync)
            {
                foreach(var held in ioSettlements)
                {
                    if(held.Original.IsCompleted&&held.OriginalObserved==0)held.OriginalObserved=NoteLocked(ImageSettlementStage.OriginalObservedComplete,held.Id);
                    if(held.Cleanup.IsCompleted&&held.CleanupObserved==0)held.CleanupObserved=NoteLocked(ImageSettlementStage.CleanupObservedComplete,held.Id);
                }
                // Pending owners come first; this display bound never truncates ownership tracking or finalization gates.
                var io=ioSettlements.OrderBy(x=>x.Cleanup.IsCompletedSuccessfully).ThenBy(x=>x.Id).Take(64).Select(x=>new ImageIoObservation(x.Id,x.Kind,x.Original.Status,x.Cleanup.Status,x.OriginalObserved,x.CleanupObserved)).ToArray();
                return new(Volatile.Read(ref started)==1,startup,childExited,state,PrimaryFailure?.GetType().Name,(PrimaryFailure as ImageJobException)?.Code,CleanupFailure?.GetType().Name,settlementFailureCode,ioSettlements.Count,ioSettlements.Count(x=>!x.Original.IsCompleted),ioSettlements.Count(x=>!x.Cleanup.IsCompleted),ioSettlements.Count(x=>x.Cleanup.IsFaulted||x.Cleanup.IsCanceled),ioSettlements.Count>64,Array.AsReadOnly(io),Array.AsReadOnly(settlementEvents.ToArray()));
            }
        }
    }
    private async Task BeforeIoClear(ImageIoKind kind){if(BeforeIoClearForTest is {} hook)await hook(kind).ConfigureAwait(false);}
    private async Task ClearOwnedIo(ImageIoKind kind,byte[]? bytes)
    {
        try{try{await BeforeIoClear(kind).ConfigureAwait(false);}finally{if(bytes!=null)System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);}}
        catch(Exception error){ObserveCleanup(error);throw;}
    }
    private ImageJobException SettlementFailure(ImageFailure code){lock(ioSync)settlementFailureCode=code;return new(code,PrimaryFailure??CleanupFailure);}
}
