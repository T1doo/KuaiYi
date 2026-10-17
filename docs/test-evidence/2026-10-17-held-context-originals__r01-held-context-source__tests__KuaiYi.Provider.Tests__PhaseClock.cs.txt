namespace KuaiYi.Provider.Tests;
internal sealed class PhaseClock:TimeProvider
{
    private readonly object sync=new();private long timestamp;private readonly List<Timer> timers=[];
    public override long TimestampFrequency=>1000;public override long GetTimestamp(){lock(sync)return timestamp;}
    public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period){var timer=new Timer(this,callback,state);lock(sync)timers.Add(timer);timer.Change(dueTime,period);return timer;}
    public void Advance(TimeSpan value,bool dispatch=true){List<Timer> ready;lock(sync){timestamp+=checked((long)value.TotalMilliseconds);ready=dispatch?timers.Where(t=>t.Due<=timestamp).ToList():[];foreach(var timer in ready)timer.Due=long.MaxValue;}foreach(var timer in ready)timer.Fire();}
    public async Task WaitDeadline(int seconds)
    {using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(4));while(true){lock(sync)if(timers.Any(timer=>timer.Due-timestamp==seconds*1000L))return;await Task.Delay(5,stop.Token);}}
    private sealed class Timer(PhaseClock clock,TimerCallback callback,object? state):ITimer
    {
        public long Due=long.MaxValue;private bool disposed;
        public bool Change(TimeSpan dueTime,TimeSpan period){lock(clock.sync){if(disposed)return false;Due=dueTime==Timeout.InfiniteTimeSpan?long.MaxValue:clock.timestamp+checked((long)dueTime.TotalMilliseconds);return true;}}
        public void Fire(){if(!disposed)callback(state);}public void Dispose(){lock(clock.sync){disposed=true;Due=long.MaxValue;}}public ValueTask DisposeAsync(){Dispose();return ValueTask.CompletedTask;}
    }
}
