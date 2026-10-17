using System.Diagnostics;
using System.Security.Cryptography;
using KuaiYi.Core;
using KuaiYi.SelectionReusePrototype;
namespace KuaiYi.SelectionReuseDiagnostic;

public enum ReuseRoundReason { Completed, Disabled, Cancelled, HardTimeout, Protocol, ResetFailed, Crash, IdleLease, Disposed, ParentDisconnected, Concurrent, JobFailure, SourceStale }
public sealed record ReuseRoundSummary(ReuseRoundReason Reason,int Requested,int Confirmed,ReuseMatch FirstMatch,ReuseMatch SecondMatch,bool ExitConfirmed,bool BuffersCleared,ReuseOutcome? FirstOutcome,ReuseOutcome? SecondOutcome,WorkerInitialization Initialization,WorkerTermination Termination);

// Independent diagnostic only. Never wired to production SelectionBrokerClient.
public sealed class ReuseDiagnosticHost : IAsyncDisposable
{
    private readonly object sync=new();private readonly SemaphoreSlim settlement=new(1,1);private readonly Guid ownerToken=Guid.NewGuid();
    private readonly OwnedProcessSession owner;private readonly ReuseProtocolPrototype model;
    private readonly CancellationTokenSource lifetime=new();private readonly string executable,assembly,scenario;
    private readonly bool enabled;private readonly Stopwatch clock=Stopwatch.StartNew();
    private static readonly TimeSpan DisposeSettlementBudget=TimeSpan.FromSeconds(2);
    private Task? running;private readonly List<Task> lateIo=[];private int requests,admitted;
    private ReuseMatch firstMatch,secondMatch;private ReuseOutcome? firstOutcome,secondOutcome;private WorkerInitialization initialization;private WorkerTermination termination;
    private readonly DiagnosticRequest? requestSpec;private readonly Func<ReadOnlyMemory<byte>,bool>? matcher;private readonly bool nativeWorker;private ExclusiveKillJob? job;private Task? stderrDrain;private bool earlyEof;private ReuseFrame? expected;
    internal Action<byte[]>? StderrBufferForTest{get;set;}
    internal bool JobGateForTest;private ReuseRoundReason reason=ReuseRoundReason.Completed;
    private Process? child;private Task<Process>? starting;private bool active,disposed,lifetimeDisposed,noChildSettled;private ReuseRoundSummary? summary;
    internal Func<Process,CancellationToken,Task>? ExitWaitForTest{get;set;}
    private Task ObserveExitWait(Process process,CancellationToken token)
    {var original=ExitWaitForTest?.Invoke(process,token)??process.WaitForExitAsync(token);var observer=ObserveWriteAsync(original);lock(sync)lateIo.Add(observer);return original;}
    internal Func<Stream,Stream>? OutputForTest{get;set;}
    internal Func<CancellationToken,Task>? BetweenRequestsForTest{get;set;}
    internal OwnedProcessSession SessionForTest=>owner;
    internal Action<byte[]>? ReadBufferAllocatedForTest{get;set;}
    internal TaskCompletionSource StartedForTest{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ReuseDiagnosticHost(string executable,string assembly,string scenario="normal",bool enabled=false,IOwnedProcessStarter? starter=null,DiagnosticRequest? request=null,Func<ReadOnlyMemory<byte>,bool>? matcher=null,bool nativeWorker=false)
    {this.executable=executable;this.assembly=assembly;this.scenario=scenario;this.enabled=enabled;requestSpec=request;this.matcher=matcher;this.nativeWorker=nativeWorker;if(enabled&&request==null)throw new ArgumentException("Explicit source and gesture required.");owner=new(starter);model=new(()=>clock.ElapsedMilliseconds,ownerToken,enabled);DiagnosticCaseRunner.Register(this);}
    public ReuseRoundSummary? Summary{get{lock(sync)return summary;}}
    public bool NoChildStartupSettled{get{lock(sync)return noChildSettled;}}
    public bool CanReplace{get{lock(sync)return noChildSettled||summary!=null&&model.CanReplaceProcess;}}
    public bool HoldsOwnership{get{lock(sync)return enabled&&admitted>0&&summary==null&&!noChildSettled;}}
    private void Stop(ReuseRoundReason why)
    {lock(sync){if(summary!=null||lifetimeDisposed)return;if(reason==ReuseRoundReason.Completed)reason=why;if(why==ReuseRoundReason.Protocol)model.ProtocolFault();else if(why==ReuseRoundReason.Crash)model.Crash();else model.Cancel();lifetime.Cancel();}}
    public void ParentDisconnected()=>Stop(ReuseRoundReason.ParentDisconnected);
    private static void Check(CancellationToken cancellation,long origin)
    {cancellation.ThrowIfCancellationRequested();if(Stopwatch.GetElapsedTime(origin)>=TimeSpan.FromMilliseconds(600))throw new TimeoutException();}
    private CancellationTokenSource Budget(CancellationToken cancellation,long origin)
    {
        var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,lifetime.Token);
        var remaining=TimeSpan.FromMilliseconds(600)-Stopwatch.GetElapsedTime(origin);
        if(remaining<=TimeSpan.Zero)linked.Cancel();else linked.CancelAfter(remaining);
        return linked;
    }
    private async Task<byte[]?> ReadAsync(Stream stream,CancellationToken cancellation,long origin)
    {
        byte[] bytes=new byte[ReuseFrameCodec.Size];ReadBufferAllocatedForTest?.Invoke(bytes);
        var pending=ReuseWireStream.FillAsync(stream,bytes,cancellation);bool delivered=false;
        try{bool filled=await pending.WaitAsync(cancellation).ConfigureAwait(false);Check(cancellation,origin);if(!filled){CryptographicOperations.ZeroMemory(bytes);return null;}delivered=true;return bytes;}
        finally
        {
            if(!delivered){var observer=EraseLateAsync(pending,bytes);lock(sync)lateIo.Add(observer);}
        }
    }
    private static async Task EraseLateAsync(Task<bool> read,byte[] bytes)
    {try{await read.ConfigureAwait(false);}catch(Exception error)when(error is IOException or InvalidDataException or OperationCanceledException or ObjectDisposedException){}finally{CryptographicOperations.ZeroMemory(bytes);}}
    private static async Task ObserveWriteAsync(Task write)
    {try{await write.ConfigureAwait(false);}catch(Exception error)when(error is IOException or OperationCanceledException or ObjectDisposedException){}}
    private async Task WriteAsync(Stream stream,ReuseFrame request,CancellationToken token,long origin)
    {
        var pending=ReuseWireStream.WriteAsync(stream,request,token);
        try{await pending.WaitAsync(token).ConfigureAwait(false);Check(token,origin);}
        finally{if(!pending.IsCompleted){var observer=ObserveWriteAsync(pending);lock(sync)lateIo.Add(observer);}}
    }
    private async Task ReceiveAsync(Stream output,CancellationToken cancellation,long origin)
    {
        var bytes=await ReadAsync(output,cancellation,origin).ConfigureAwait(false);if(bytes==null){earlyEof=true;throw new EndOfStreamException();}
        ReuseFrame? frame=null;
        try
        {
            frame=ReuseFrameCodec.Decode(bytes);
            var match=frame.Kind==ReuseKind.Completed&&frame.Outcome==ReuseOutcome.Success&&frame.Payload!.Length!=0&&matcher!=null?(matcher(frame.Payload)?ReuseMatch.Matched:ReuseMatch.NotMatched):ReuseMatch.NotRun;
            bool same=expected!=null&&frame.Nonce==expected.Nonce&&frame.Sequence==expected.Sequence&&frame.Target==expected.Target&&frame.Reused==expected.Reused;
            if(!model.ReceiveOwnedFrame(bytes)){if(same&&frame.Kind==ReuseKind.Completed&&frame.Outcome==ReuseOutcome.Stale&&frame.Payload!.Length==0&&frame.Persistent==(PersistentResources.WindowlessMta|PersistentResources.Automation)&&frame.Transient==RequestResources.None&&frame.PendingOperations==0&&!frame.ResetOk&&model.Failure==ReuseFailure.IdentityChanged){if(frame.Sequence==1)firstOutcome=frame.Outcome;else secondOutcome=frame.Outcome;Stop(ReuseRoundReason.SourceStale);throw new OperationCanceledException();}throw new InvalidDataException();}
            if(frame.Kind==ReuseKind.Initialized)initialization=WorkerInitialization.Ready;
            if(frame.Kind==ReuseKind.InitializationFailed){initialization=frame.Outcome==ReuseOutcome.InitializationFailed?WorkerInitialization.Failed:WorkerInitialization.NotRun;firstOutcome=frame.Outcome;}
            if(frame.Kind==ReuseKind.Completed){if(frame.Sequence==1){firstMatch=match;firstOutcome=frame.Outcome;}else{secondMatch=match;secondOutcome=frame.Outcome;}if(match==ReuseMatch.NotMatched)throw new InvalidDataException();}
            Check(cancellation,origin);
        }
        finally{if(frame?.Payload!=null)CryptographicOperations.ZeroMemory(frame.Payload);model.DiscardBufferedPayload();CryptographicOperations.ZeroMemory(bytes);}
    }
    public Task RunAsync(int count,CancellationToken cancellation=default)
    {
        lock(sync)
        {
            if(count is <1 or >2)throw new ArgumentOutOfRangeException(nameof(count));
            if(!enabled||disposed)return Task.CompletedTask;
            if(admitted!=0){Stop(ReuseRoundReason.Concurrent);return Task.CompletedTask;}
            admitted=1;requests=count;active=true;return running=RunCoreAsync(cancellation);
        }
    }
    private async Task RunCoreAsync(CancellationToken cancellation)
    {
        long origin=Stopwatch.GetTimestamp();using var firstBudget=Budget(cancellation,origin);
        CancellationTokenSource? secondBudget=null;
        try
        {
            var request=model.Begin(requestSpec!.Source,Guid.NewGuid())??throw new InvalidDataException();
            if(nativeWorker){try{job=ExclusiveKillJob.Create();}catch{Stop(ReuseRoundReason.JobFailure);throw;}}
            var info=OwnedProcessSession.Info(executable,assembly);info.ArgumentList.Clear();if(nativeWorker)info.ArgumentList.Add("--native-reuse-worker");else{info.ArgumentList.Add(assembly);info.ArgumentList.Add("--synthetic-child");info.ArgumentList.Add(scenario);}
            // Begin and the first 600ms origin precede Start, including blocking/late starters.
            starting=owner.Start(info,firstBudget.Token);
            child=await starting.WaitAsync(firstBudget.Token).ConfigureAwait(false);StartedForTest.TrySetResult();Check(firstBudget.Token,origin);
            StartStderrDrain(child);
            if(nativeWorker){try{job!.AssignVerified(child);}catch{Stop(ReuseRoundReason.JobFailure);throw;}}
            var output=OutputForTest?.Invoke(child.StandardOutput.BaseStream)??child.StandardOutput.BaseStream;
            var token=firstBudget.Token;
            for(int sequence=1;sequence<=requests;sequence++)
            {
                if(sequence==2)
                {
                    long idleOrigin=Stopwatch.GetTimestamp();
                    using var idle=CancellationTokenSource.CreateLinkedTokenSource(cancellation,lifetime.Token);idle.CancelAfter(120);
                    try{if(BetweenRequestsForTest!=null)await BetweenRequestsForTest(idle.Token).WaitAsync(idle.Token).ConfigureAwait(false);}
                    catch(OperationCanceledException)when(!cancellation.IsCancellationRequested&&!lifetime.IsCancellationRequested){Stop(ReuseRoundReason.IdleLease);throw;}
                    if(Stopwatch.GetElapsedTime(idleOrigin)>=TimeSpan.FromMilliseconds(120)){Stop(ReuseRoundReason.IdleLease);throw new OperationCanceledException();}
                    origin=Stopwatch.GetTimestamp();secondBudget=Budget(cancellation,origin);token=secondBudget.Token;
                    request=model.Begin(request.Target,Guid.NewGuid())??throw new InvalidDataException();
                }
                Check(token,origin);
                expected=request;await WriteAsync(child.StandardInput.BaseStream,request with{Payload=requestSpec!.Encode(origin,nativeWorker)},token,origin).ConfigureAwait(false);
                if(sequence==1){await ReceiveAsync(output,token,origin).ConfigureAwait(false);if(model.State==ReuseState.Retiring)break;}
                await ReceiveAsync(output,token,origin).ConfigureAwait(false);
                await ReceiveAsync(output,token,origin).ConfigureAwait(false);
                if(model.State!=ReuseState.Idle&&!(sequence==2&&model.State==ReuseState.Retiring))throw new InvalidDataException();
            }
            model.FinishRound();
            // Stop the held child; retain the pipe to reject any queued/truncated/extra trailing frame.
            child.StandardInput.Close();
            var extra=await ReadAsync(output,token,origin).ConfigureAwait(false);
            if(extra!=null){CryptographicOperations.ZeroMemory(extra);throw new InvalidDataException();}
            await ObserveExitWait(child,token).WaitAsync(token).ConfigureAwait(false);Check(token,origin);
        }
        catch(OperationCanceledException){Stop(cancellation.IsCancellationRequested?ReuseRoundReason.Cancelled:lifetime.IsCancellationRequested?reason:ReuseRoundReason.HardTimeout);}
        catch(TimeoutException){Stop(ReuseRoundReason.HardTimeout);}
        catch(Exception error)when(error is IOException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception or PlatformNotSupportedException)
        {Stop(model.Failure==ReuseFailure.ResetFailed?ReuseRoundReason.ResetFailed:ReuseRoundReason.Protocol);}
        finally
        {
            secondBudget?.Dispose();model.FinishRound();
            await RetireOwnedAsync().ConfigureAwait(false);
            lock(sync){active=false;expected=null;}
            await TrySettleAsync().ConfigureAwait(false);
        }
    }
    private void StartStderrDrain(Process process)
    {
        lock(sync){if(stderrDrain!=null)return;stderrDrain=Task.Factory.StartNew(()=>{
            byte[] bytes=new byte[256];StderrBufferForTest?.Invoke(bytes);int total=0;
            try{while(true){int n=process.StandardError.BaseStream.Read(bytes,0,bytes.Length);if(n==0)break;total=Math.Min(4097,total+n);CryptographicOperations.ZeroMemory(bytes);if(total>4096)Stop(ReuseRoundReason.Protocol);}}
            catch(Exception error)when(error is IOException or ObjectDisposedException){Stop(ReuseRoundReason.Protocol);}
            finally{CryptographicOperations.ZeroMemory(bytes);}
        },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);}
    }
    private async Task RetireOwnedAsync()
    {
        if(starting is {IsCompletedSuccessfully:true})
        {
            var process=starting.Result;
            if(earlyEof&&!process.HasExited){try{await ObserveExitWait(process,CancellationToken.None).WaitAsync(TimeSpan.FromMilliseconds(20));}catch(TimeoutException){}}
            if(process.HasExited){termination=process.ExitCode==0?WorkerTermination.NaturalZero:WorkerTermination.NaturalNonZero;if(process.ExitCode!=0&&reason is ReuseRoundReason.Protocol or ReuseRoundReason.Completed){reason=ReuseRoundReason.Crash;model.Crash();}}
            else termination=WorkerTermination.ParentRequested;
        }
        else termination=WorkerTermination.ParentRequested;
        await owner.StopAsync().ConfigureAwait(false);
    }
    public Task<bool> TrySettleAsync()=>TrySettleAsync(CancellationToken.None);
    private async Task<bool> TrySettleAsync(CancellationToken cancellation)
    {
        await settlement.WaitAsync(cancellation).ConfigureAwait(false);
        try{
        cancellation.ThrowIfCancellationRequested();
        lock(sync){if(summary!=null||noChildSettled)return true;if(!enabled||admitted==0||active||lateIo.Any(t=>!t.IsCompleted||t.IsFaulted))return false;}
        // TryFinalize verifies startup and stop settlement plus HasExited before releasing the held process.
        if(starting is not {IsCompletedSuccessfully:true})
        {
            if(starting is {IsCompleted:false})return false;
            // Settled failed/cancelled startup and finalized empty owner, not a child-exit attestation.
            if(!await owner.TryFinalizeAsync(retry:true).ConfigureAwait(false)||!owner.ObserveExit())return false;
            lock(sync){noChildSettled=true;job?.Dispose();job=null;}
            return true; // No summary and no ConfirmOwnedProcessExit: no child existed.
        }
        StartStderrDrain(starting.Result);if(stderrDrain is not {IsCompletedSuccessfully:true})return false;
        if(!await owner.TryFinalizeAsync(retry:true).ConfigureAwait(false))return false;
        lock(sync)
        {
            if(active||lateIo.Any(t=>!t.IsCompleted)||lateIo.Any(t=>t.IsFaulted))return false;
            if(!owner.ObserveExit()||!model.ConfirmOwnedProcessExit(ownerToken))return false;
            job?.Dispose();job=null;summary=new(reason,requests,model.ConfirmedRequests,firstMatch,secondMatch,true,!model.HasBufferedPayload,firstOutcome,secondOutcome,initialization,termination);
            return true;
        }
        }finally{settlement.Release();}
    }
    public async ValueTask DisposeAsync()
    {
        lock(sync)disposed=true;Stop(ReuseRoundReason.Disposed);Task? task;lock(sync)task=running;
        if(task!=null)await task.ConfigureAwait(false);
        if(enabled&&admitted>0)
        {
            // This cleanup budget begins after running settles; it does not extend the request's 250/600ms gates.
            long origin=Stopwatch.GetTimestamp();using var cleanup=new CancellationTokenSource(DisposeSettlementBudget);
            void CheckSettlement(){cleanup.Token.ThrowIfCancellationRequested();if(Stopwatch.GetElapsedTime(origin)>=DisposeSettlementBudget)throw new TimeoutException();}
            try
            {
                while(true)
                {
                    CheckSettlement();Task[] io;
                    lock(sync)
                    {
                        if(summary!=null||noChildSettled)break;
                        // A startup which outlived RunCore still belongs to this owner and needs its drain joined.
                        if(starting is {IsCompletedSuccessfully:true})StartStderrDrain(starting.Result);
                        io=lateIo.Append(stderrDrain??Task.CompletedTask).ToArray();
                    }
                    await Task.WhenAll(io).WaitAsync(cleanup.Token).ConfigureAwait(false);CheckSettlement();
                    // Wait for startup/stop/actual exit before entering finalization, whose existing stop budget is 2s.
                    // Thus polling cannot stack that budget. The settlement lock uses this same cleanup deadline.
                    if(owner.ObserveExit()&&await TrySettleAsync(cleanup.Token).ConfigureAwait(false))break;
                    await Task.Delay(10,cleanup.Token).ConfigureAwait(false);
                }
            }
            catch(OperationCanceledException error)when(cleanup.IsCancellationRequested)
            {throw new IOException("Owned child or IO settlement unconfirmed; ownership retained.",error);}
            catch(TimeoutException error)
            {throw new IOException("Owned child or IO settlement unconfirmed; ownership retained.",error);}
        }
        lock(sync){if(!lifetimeDisposed){lifetimeDisposed=true;lifetime.Dispose();}}
    }
}
