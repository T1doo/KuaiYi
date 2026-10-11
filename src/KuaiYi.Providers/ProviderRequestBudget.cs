namespace KuaiYi.Providers;
/// <summary>One immutable monotonic origin shared by credential acquisition, HTTP and streaming.</summary>
public sealed class ProviderRequestBudget
{
    private readonly TimeProvider clock;private readonly long started;
    public int Seconds{get;}
    public ProviderRequestBudget(int seconds,TimeProvider? clock=null)
    {if(seconds is <1 or >600)throw new ArgumentOutOfRangeException(nameof(seconds));Seconds=seconds;this.clock=clock??TimeProvider.System;started=this.clock.GetTimestamp();}
    public TimeSpan Remaining=>TimeSpan.FromTicks(Math.Max(0,TimeSpan.FromSeconds(Seconds).Ticks-clock.GetElapsedTime(started).Ticks));
    public void Check(){if(Remaining==TimeSpan.Zero)throw new OperationCanceledException("Frozen request budget expired.");}
    public Deadline Link(CancellationToken parent)=>new(Remaining,clock,parent);
    public sealed class Deadline:IDisposable
    {
        private readonly CancellationTokenSource timer,linked;
        internal Deadline(TimeSpan remaining,TimeProvider clock,CancellationToken parent)
        {timer=new(remaining,clock);linked=CancellationTokenSource.CreateLinkedTokenSource(parent,timer.Token);}
        public CancellationToken Token=>linked.Token;
        public bool IsCancellationRequested=>linked.IsCancellationRequested;
        public void Dispose(){linked.Dispose();timer.Dispose();}
    }
}
