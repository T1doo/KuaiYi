using System.Diagnostics;
using KuaiYi.SelectionReuseDiagnostic;
namespace KuaiYi.ReuseLifecycle;
internal static class LifecycleParent
{
    public static async Task<int> Run(string executable,LifecycleCase mode,Guid nonce){
        long origin=Stopwatch.GetTimestamp();using var first=LifecycleIo.Budget(origin,LifecycleBudgets.Hard);using var input=Console.OpenStandardInput();using var output=Console.OpenStandardOutput();var ownIo=new PendingWork();TrackedProcess? worker=null;ExclusiveKillJob? job=null;LifecycleFrame? started=null;bool success=false,watchdog=false;WatchdogStop watchdogStop=WatchdogStop.NotExpired;byte sequence=0;long idleOrigin=0;var trace=new LifecycleTrace(mode,nonce);
        try {
            if(mode!=LifecycleCase.BeforeBindParentDeath)job=ExclusiveKillJob.Create();
            worker=TrackedProcess.Start(TrackedProcess.Info(executable,["--lifecycle-worker",((byte)mode).ToString(System.Globalization.CultureInfo.InvariantCulture),Convert.ToHexString(nonce.ToByteArray()).ToLowerInvariant()]));
            var child=await worker.ReadyAsync(first.Token);LifecycleIo.Check(origin,LifecycleBudgets.Hard);
            started=await LifecycleIo.ReadAsync(child.StandardOutput.BaseStream,worker.Work,first.Token)??throw new InvalidDataException();
            if(started.Phase!=LifecyclePhase.Started||started.Sequence!=0||started.Nonce!=nonce||started.Case!=mode||started.Worker.Pid!=child.Id||started.Worker.Created!=child.StartTime.ToUniversalTime().Ticks||started.Origin!=0)throw new InvalidDataException();
            trace.Accept(started);await LifecycleIo.WriteAsync(output,started,ownIo,first.Token);
            if(mode!=LifecycleCase.BeforeBindParentDeath){job!.AssignVerified(child);var bound=started with{Phase=LifecyclePhase.Bound};trace.Accept(bound);await LifecycleIo.WriteAsync(output,bound,ownIo,first.Token);}
            // Before-bind test parent waits here, with no Job assignment and no authorization sent.
            var ack=await LifecycleIo.ReadAsync(input,ownIo,first.Token)??throw new InvalidDataException();if(ack!=(started with{Phase=LifecyclePhase.Authorize}))throw new InvalidDataException();
            if(mode==LifecycleCase.BeforeBindParentDeath)throw new InvalidDataException();
            CancellationTokenSource? second=null;var token=first.Token;
            try {
                for(sequence=1;sequence<=2;sequence++){
                    if(sequence==2){LifecycleIo.Check(idleOrigin,LifecycleBudgets.Idle);origin=Stopwatch.GetTimestamp();second=LifecycleIo.Budget(origin,LifecycleBudgets.Hard);token=second.Token;nonce=Guid.NewGuid();}
                    var request=started with{Phase=LifecyclePhase.Authorize,Sequence=sequence,Origin=origin,Nonce=nonce};if(sequence==2){using var idle=LifecycleIo.Budget(idleOrigin,LifecycleBudgets.Idle);await LifecycleIo.WriteAsync(child.StandardInput.BaseStream,request,worker.Work,idle.Token);LifecycleIo.Check(idleOrigin,LifecycleBudgets.Idle);}else await LifecycleIo.WriteAsync(child.StandardInput.BaseStream,request,worker.Work,token);
                    if(mode is LifecycleCase.BoundParentDeath or LifecycleCase.Watchdog){
                        var parked=await LifecycleIo.ReadAsync(child.StandardOutput.BaseStream,worker.Work,token)??throw new InvalidDataException();if(parked.Phase!=LifecyclePhase.Parked||parked.Sequence!=1||parked.Nonce!=nonce||parked.Worker!=started.Worker||parked.Case!=mode||parked.Origin!=origin)throw new InvalidDataException();trace.Accept(parked);await LifecycleIo.WriteAsync(output,parked,ownIo,token);
                        // Bound-death supervisor kills ONLY this parent while worker Sleep(INFINITE) ignores stdin.
                        try{_ =await LifecycleIo.ReadAsync(child.StandardOutput.BaseStream,worker.Work,token);throw new InvalidDataException();}
                        catch(OperationCanceledException)when(mode==LifecycleCase.Watchdog&&Stopwatch.GetElapsedTime(origin)>=TimeSpan.FromMilliseconds(LifecycleBudgets.Hard)){
                            watchdogStop=WatchdogGate.Stop((long)Stopwatch.GetElapsedTime(origin).TotalMilliseconds,()=>worker.Exited,worker.KillRequiredExact,()=>worker.KillIssued);
                            using var control=new CancellationTokenSource(LifecycleBudgets.Settlement);
                            var expired=started with{Phase=LifecyclePhase.WatchdogExpired,Sequence=1,Origin=origin,Elapsed=LifecycleBudgets.Hard};trace.Accept(expired);await LifecycleIo.WriteAsync(output,expired,ownIo,control.Token);
                            var proof=expired with{Phase=watchdogStop switch{WatchdogStop.KillIssued=>LifecyclePhase.WatchdogKillIssued,WatchdogStop.NaturalExit=>LifecyclePhase.WatchdogNaturalExit,_=>LifecyclePhase.WatchdogKillNotIssued}};trace.Accept(proof);await LifecycleIo.WriteAsync(output,proof,ownIo,control.Token);watchdog=true;break;
                        }
                    }
                    if(sequence==1){var initialized=await LifecycleIo.ReadAsync(child.StandardOutput.BaseStream,worker.Work,token)??throw new InvalidDataException();if(initialized.Phase!=LifecyclePhase.Initialized)throw new InvalidDataException();trace.Accept(initialized);LifecycleIo.Check(origin,LifecycleBudgets.Soft);await LifecycleIo.WriteAsync(output,initialized,ownIo,token);}
                    foreach(var expected in new[]{LifecyclePhase.LocalReleased,LifecyclePhase.Quiescent}){var frame=await LifecycleIo.ReadAsync(child.StandardOutput.BaseStream,worker.Work,token)??throw new InvalidDataException();if(frame.Phase!=expected||frame.Sequence!=sequence||frame.Nonce!=nonce||frame.Worker!=started.Worker||frame.Case!=mode||frame.Origin!=origin||frame.Elapsed>=LifecycleBudgets.Soft)throw new InvalidDataException();LifecycleIo.Check(origin,LifecycleBudgets.Soft);trace.Accept(frame);
                        if(expected==LifecyclePhase.Quiescent&&sequence==1){idleOrigin=Stopwatch.GetTimestamp();using var idle=LifecycleIo.Budget(idleOrigin,LifecycleBudgets.Idle);await LifecycleIo.WriteAsync(output,frame,ownIo,idle.Token);LifecycleIo.Check(idleOrigin,LifecycleBudgets.Idle);}else await LifecycleIo.WriteAsync(output,frame,ownIo,token);}
                }
                if(!watchdog){child.StandardInput.Close();var final=await LifecycleIo.ReadAsync(child.StandardOutput.BaseStream,worker.Work,token)??throw new InvalidDataException();if(final.Phase!=LifecyclePhase.FinalReleased||final.Sequence!=2||final.Worker!=started.Worker||final.Nonce!=nonce||final.Case!=mode)throw new InvalidDataException();trace.Accept(final);await LifecycleIo.WriteAsync(output,final,ownIo,token);if(await LifecycleIo.ReadAsync(child.StandardOutput.BaseStream,worker.Work,token)!=null)throw new InvalidDataException();await worker.ExitWait(token).WaitAsync(token);LifecycleIo.Check(origin,LifecycleBudgets.Hard);if(!worker.NaturalZero)throw new InvalidDataException();}
            }finally{second?.Dispose();}
            success=mode==LifecycleCase.Normal||watchdog&&watchdogStop==WatchdogStop.KillIssued;
        }catch{success=false;}
        finally {
            input.Close();
            if(worker!=null){bool settled=await worker.CleanupAsync(kill:!worker.Exited);if(!settled||!ownIo.Settled||!worker.StderrBounded||mode==LifecycleCase.Watchdog&&!WatchdogGate.Passed(watchdog,watchdogStop==WatchdogStop.KillIssued&&worker.KillIssued,worker.Exited,settled,worker.NaturalZero))success=false;
                // Retain handles/Job and stop further cases until all original operations really settle.
                if(!settled){await worker.AwaitRetainedSettlementAsync();}
                await ownIo.WaitAsync();if(success&&started!=null){var settledFrame=started with{Phase=LifecyclePhase.ParentSettled,Sequence=(byte)(watchdog?1:2),Nonce=nonce};trace.Accept(settledFrame);using var receipt=new CancellationTokenSource(LifecycleBudgets.Settlement);await LifecycleIo.WriteAsync(output,settledFrame,ownIo,receipt.Token);await ownIo.WaitAsync();}
                worker.Release();}
            job?.Dispose();
        }
        return success?0:3;
    }
}
