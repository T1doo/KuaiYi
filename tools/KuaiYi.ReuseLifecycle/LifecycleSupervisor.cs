using System.Diagnostics;
using System.Text.Json;
namespace KuaiYi.ReuseLifecycle;
public sealed record LifecyclePublicResult(LifecycleCase Case,string Status,string Stage,bool ParentExitConfirmed,bool WorkerExitConfirmed,bool ReleaseCallsReturned,bool EofObserved,bool RescueUsed,bool IoSettled);
internal static class LifecycleSupervisor
{
    public static async Task<int> Run(string native){int failed=0;foreach(var mode in new[]{LifecycleCase.Normal,LifecycleCase.BoundParentDeath,LifecycleCase.BeforeBindParentDeath,LifecycleCase.Watchdog}){var result=await RunCase(native,mode);Console.WriteLine("KUAIYI_LIFECYCLE_CASE "+JsonSerializer.Serialize(result));if(result.Status!="Pass")failed++;}return failed==0?0:3;}
    private static async Task<LifecyclePublicResult> RunCase(string native,LifecycleCase mode){
        long startupOrigin=Stopwatch.GetTimestamp();long deathOrigin=0;bool parentStopIssued=false;Guid nonce=Guid.NewGuid();var trace=new LifecycleTrace(mode,nonce);var parent=TrackedProcess.Start(TrackedProcess.Info(Environment.ProcessPath!,[typeof(LifecycleSupervisor).Assembly.Location,"--parent",native,((byte)mode).ToString(System.Globalization.CultureInfo.InvariantCulture),nonce.ToString("N")]));TrackedProcess? worker=null;bool eof=false,rescue=false,settled=false;string stage="ProtocolFailure";bool valid=false;
        try {
            long origin=startupOrigin;using var startup=LifecycleIo.Budget(origin,LifecycleBudgets.Hard);var process=await parent.ReadyAsync(startup.Token);LifecycleIo.Check(origin,LifecycleBudgets.Hard);
            var started=await LifecycleIo.ReadAsync(process.StandardOutput.BaseStream,parent.Work,startup.Token)??throw new InvalidDataException();LifecycleIo.Check(origin,LifecycleBudgets.Hard);trace.Accept(started);
            worker=TrackedProcess.AttachVerified(started.Worker); // Holds the exact worker process handle; no Job handle transfer.
            if(mode==LifecycleCase.BeforeBindParentDeath){LifecycleIo.Check(origin,LifecycleBudgets.Hard);if(parent.Exited||worker.Exited)throw new InvalidDataException();deathOrigin=Stopwatch.GetTimestamp();DeathPlan.KillOnlyParent(parent.KillRequiredExact);parentStopIssued=parent.KillIssued;stage="BeforeBindParentTerminationRequested";}
            else {
                var bound=await LifecycleIo.ReadAsync(process.StandardOutput.BaseStream,parent.Work,startup.Token)??throw new InvalidDataException();LifecycleIo.Check(origin,LifecycleBudgets.Hard);trace.Accept(bound);if(!trace.Bound)throw new InvalidDataException();
                await LifecycleIo.WriteAsync(process.StandardInput.BaseStream,started with{Phase=LifecyclePhase.Authorize},parent.Work,startup.Token);LifecycleIo.Check(origin,LifecycleBudgets.Hard);
                if(mode==LifecycleCase.BoundParentDeath){var parked=await LifecycleIo.ReadAsync(process.StandardOutput.BaseStream,parent.Work,startup.Token)??throw new InvalidDataException();trace.Accept(parked);if(!trace.Parked)throw new InvalidDataException();LifecycleIo.Check(origin,LifecycleBudgets.Hard);if(parent.Exited||worker.Exited)throw new InvalidDataException();deathOrigin=Stopwatch.GetTimestamp();DeathPlan.KillOnlyParent(parent.KillRequiredExact);parentStopIssued=parent.KillIssued;stage="BoundParentTerminationRequested";}
                else {
                    // This supervision envelope covers at most two already bounded requests plus final settlement.
                    // It does not extend either parent's 250/600ms request or 120ms idle deadline.
                    using var envelope=new CancellationTokenSource(mode==LifecycleCase.Normal?2*LifecycleBudgets.Hard+LifecycleBudgets.Idle+LifecycleBudgets.Settlement:LifecycleBudgets.Hard+LifecycleBudgets.Settlement);
                    while(true){var frame=await LifecycleIo.ReadAsync(process.StandardOutput.BaseStream,parent.Work,envelope.Token);if(frame==null){eof=true;break;}trace.Accept(frame);if(trace.WatchdogFailure!=null)stage=trace.WatchdogFailure.Value.ToString();}
                    stage=mode==LifecycleCase.Normal?"FinalCallsReturned":trace.WatchdogFailure?.ToString()??(trace.WatchdogExpired&&trace.WatchdogKillIssued?"WatchdogTerminationIssuedAndSettled":"WatchdogProofMissing");
                }
            }
            if(deathOrigin==0)deathOrigin=Stopwatch.GetTimestamp();using var death=LifecycleIo.Budget(deathOrigin,LifecycleBudgets.Hard);await LifecycleIo.ExitWithinAsync(parent.ExitWait(death.Token),deathOrigin,death.Token);await LifecycleIo.ExitWithinAsync(worker.ExitWait(death.Token),deathOrigin,death.Token);valid=trace.ExpectedStages;
        }catch{valid=false;}
        finally {
            // Any unplanned cleanup termination is rescue, makes the case FAIL, and is never destructor evidence.
            if(!parent.Exited){rescue=true;await parent.CleanupAsync(kill:true);}
            if(worker!=null&&!worker.Exited){rescue=true;await worker.CleanupAsync(kill:true);}
            bool parentDone=await parent.CleanupAsync(kill:false);bool workerDone=worker==null||await worker.CleanupAsync(kill:false);settled=parentDone&&workerDone;
            if(!settled){valid=false;stage="Unsettled";Console.WriteLine("KUAIYI_LIFECYCLE_UNSETTLED");await parent.AwaitRetainedSettlementAsync();if(worker!=null)await worker.AwaitRetainedSettlementAsync();settled=worker!=null&&parent.Settled&&worker.Settled;}
        }
        bool passed=worker!=null&&LifecycleBudgets.Pass(mode,valid,parent.Exited,worker.Exited,parent.NaturalZero,worker.NaturalZero,eof,rescue,settled,parentStopIssued,trace.WatchdogExpired,trace.WatchdogKillIssued)&&parent.StderrBounded;
        var result=new LifecyclePublicResult(mode,passed?"Pass":"Fail",stage,parent.Exited,worker?.Exited==true,mode==LifecycleCase.Normal&&trace.FinalReturned,eof,rescue,settled);parent.Release();worker?.Release();return result;
    }
}
