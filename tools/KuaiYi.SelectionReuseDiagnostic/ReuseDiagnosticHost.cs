using System.Diagnostics;
using System.Security.Cryptography;
using KuaiYi.Core;
using KuaiYi.SelectionReusePrototype;
namespace KuaiYi.SelectionReuseDiagnostic;

public enum ReuseRoundReason { Completed, Disabled, Cancelled, HardTimeout, Protocol, ResetFailed, Crash, IdleLease, Disposed, ParentDisconnected, Concurrent }
public sealed record ReuseRoundSummary(ReuseRoundReason Reason,int Requested,int Confirmed,bool FirstMatch,bool SecondMatch,bool ExitConfirmed,bool BuffersCleared);

// Synthetic diagnostic only. Never wired to SelectionBrokerClient or a UIA provider.
public sealed class ReuseDiagnosticHost : IAsyncDisposable
{
    private readonly object sync=new();private readonly SemaphoreSlim settlement=new(1,1);private readonly Guid ownerToken=Guid.NewGuid();
    private readonly OwnedProcessSession owner;private readonly ReuseProtocolPrototype model;
    private readonly CancellationTokenSource lifetime=new();private readonly string executable,assembly,scenario;
    private readonly bool enabled;private readonly Stopwatch clock=Stopwatch.StartNew();
    private Task? running;private readonly List<Task> lateIo=[];private int requests,admitted;
    private bool firstMatch,secondMatch;private ReuseRoundReason reason=ReuseRoundReason.Completed;
    private Process? child;private Task<Process>? starting;private bool active,disposed,lifetimeDisposed,noChildSettled;private ReuseRoundSummary? summary;
    internal Func<Stream,Stream>? OutputForTest{get;set;}
    internal Func<CancellationToken,Task>? BetweenRequestsForTest{get;set;}
    internal OwnedProcessSession SessionForTest=>owner;
    internal Action<byte[]>? ReadBufferAllocatedForTest{get;set;}
    internal TaskCompletionSource StartedForTest{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ReuseDiagnosticHost(string executable,string assembly,string scenario="normal",bool enabled=false,IOwnedProcessStarter? starter=null)
    {this.executable=executable;this.assembly=assembly;this.scenario=scenario;this.enabled=enabled;owner=new(starter);model=new(()=>clock.ElapsedMilliseconds,ownerToken,enabled);DiagnosticCaseRunner.Register(this);}
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
        var bytes=await ReadAsync(output,cancellation,origin).ConfigureAwait(false)??throw new EndOfStreamException();
        ReuseFrame? frame=null;
        try
        {
            frame=ReuseFrameCodec.Decode(bytes);
            bool match=frame.Kind==ReuseKind.Completed&&frame.Outcome==ReuseOutcome.Success&&frame.Payload!.AsSpan().SequenceEqual(new byte[]{1,2,3});
            if(!model.ReceiveOwnedFrame(bytes))throw new InvalidDataException();
            if(frame.Kind==ReuseKind.Completed){if(!match)throw new InvalidDataException();if(frame.Sequence==1)firstMatch=true;else secondMatch=true;}
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
            var request=model.Begin(new(42,123456,99),Guid.NewGuid())??throw new InvalidDataException();
            var info=OwnedProcessSession.Info(executable,assembly);info.ArgumentList.Clear();info.ArgumentList.Add(assembly);info.ArgumentList.Add("--synthetic-child");info.ArgumentList.Add(scenario);
            // Begin and the first 600ms origin precede Start, including blocking/late starters.
            starting=owner.Start(info,firstBudget.Token);
            child=await starting.WaitAsync(firstBudget.Token).ConfigureAwait(false);StartedForTest.TrySetResult();Check(firstBudget.Token,origin);
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
                await WriteAsync(child.StandardInput.BaseStream,request,token,origin).ConfigureAwait(false);
                if(sequence==1)await ReceiveAsync(output,token,origin).ConfigureAwait(false);
                await ReceiveAsync(output,token,origin).ConfigureAwait(false);
                await ReceiveAsync(output,token,origin).ConfigureAwait(false);
                if(model.State!=ReuseState.Idle&&!(sequence==2&&model.State==ReuseState.Retiring))throw new InvalidDataException();
            }
            model.FinishRound();
            // Stop the held child; retain the pipe to reject any queued/truncated/extra trailing frame.
            await owner.StopAsync().ConfigureAwait(false);
            var extra=await ReadAsync(output,token,origin).ConfigureAwait(false);
            if(extra!=null){CryptographicOperations.ZeroMemory(extra);throw new InvalidDataException();}
            Check(token,origin);
        }
        catch(OperationCanceledException){Stop(cancellation.IsCancellationRequested?ReuseRoundReason.Cancelled:lifetime.IsCancellationRequested?reason:ReuseRoundReason.HardTimeout);}
        catch(TimeoutException){Stop(ReuseRoundReason.HardTimeout);}
        catch(Exception error)when(error is IOException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {Stop(model.Failure==ReuseFailure.ResetFailed?ReuseRoundReason.ResetFailed:ReuseRoundReason.Protocol);}
        finally
        {
            secondBudget?.Dispose();model.FinishRound();
            await owner.StopAsync().ConfigureAwait(false);
            lock(sync)active=false;
            await TrySettleAsync().ConfigureAwait(false);
        }
    }
    public async Task<bool> TrySettleAsync()
    {
        await settlement.WaitAsync().ConfigureAwait(false);
        try{
        lock(sync){if(summary!=null||noChildSettled)return true;if(!enabled||admitted==0||active||lateIo.Any(t=>!t.IsCompleted||t.IsFaulted))return false;}
        // TryFinalize verifies startup and stop settlement plus HasExited before releasing the held process.
        if(starting is not {IsCompletedSuccessfully:true})
        {
            if(starting is {IsCompleted:false})return false;
            // Settled failed/cancelled startup and finalized empty owner, not a child-exit attestation.
            if(!await owner.TryFinalizeAsync(retry:true).ConfigureAwait(false)||!owner.ObserveExit())return false;
            lock(sync)noChildSettled=true;
            return true; // No summary and no ConfirmOwnedProcessExit: no child existed.
        }
        // The synthetic crash role exits 23; never expose arbitrary exit codes or stderr.
        if(reason==ReuseRoundReason.Protocol&&starting.Result.HasExited&&starting.Result.ExitCode==23){reason=ReuseRoundReason.Crash;model.Crash();}
        if(!await owner.TryFinalizeAsync(retry:true).ConfigureAwait(false))return false;
        lock(sync)
        {
            if(active||lateIo.Any(t=>!t.IsCompleted)||lateIo.Any(t=>t.IsFaulted))return false;
            if(!owner.ObserveExit()||!model.ConfirmOwnedProcessExit(ownerToken))return false;
            summary=new(reason,requests,model.ConfirmedRequests,firstMatch,secondMatch,true,!model.HasBufferedPayload);
            return true;
        }
        }finally{settlement.Release();}
    }
    public async ValueTask DisposeAsync()
    {
        lock(sync)disposed=true;Stop(ReuseRoundReason.Disposed);Task? task;lock(sync)task=running;
        if(task!=null)await task.ConfigureAwait(false);
        if(enabled&&admitted>0&&!await TrySettleAsync().ConfigureAwait(false))throw new IOException("Owned child or IO settlement unconfirmed; ownership retained.");
        lock(sync){if(!lifetimeDisposed){lifetimeDisposed=true;lifetime.Dispose();}}
    }
}
