namespace KuaiYi.ReuseLifecycle;
public enum WatchdogStop {NotExpired,KillIssued,NaturalExit,KillNotIssued}
public static class WatchdogGate
{
    // One measured expiry and one attempt on the held identity; cleanup must not turn a no-op into success.
    public static WatchdogStop Stop(long elapsed,Func<bool> exited,Action killRequired,Func<bool> issued)
    {
        if(elapsed<LifecycleBudgets.Hard)return WatchdogStop.NotExpired;
        if(exited())return WatchdogStop.NaturalExit;
        try{killRequired();return issued()?WatchdogStop.KillIssued:WatchdogStop.KillNotIssued;}
        catch{return exited()?WatchdogStop.NaturalExit:WatchdogStop.KillNotIssued;}
    }
    public static bool Passed(bool expired,bool killIssued,bool workerExited,bool ioSettled,bool naturalZero)=>expired&&killIssued&&workerExited&&ioSettled&&!naturalZero;
}
