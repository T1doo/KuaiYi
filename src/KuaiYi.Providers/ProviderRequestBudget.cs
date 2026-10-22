namespace KuaiYi.Providers;
/// <summary>One immutable monotonic origin shared by credential acquisition, HTTP and streaming.</summary>
public sealed class ProviderRequestBudget
{
    private readonly TimeProvider clock;private readonly long started;private readonly int limitSeconds;private readonly ProviderRequestBudget? parent;
    public int Seconds{get;}
    public ProviderRequestBudget(int seconds,TimeProvider? clock=null)
    {if(seconds is <1 or >600)throw new ArgumentOutOfRangeException(nameof(seconds));Seconds=limitSeconds=seconds;this.clock=clock??TimeProvider.System;started=this.clock.GetTimestamp();}
    private ProviderRequestBudget(ProviderRequestBudget parent,int seconds)
    {if(seconds is <1 or >600)throw new ArgumentOutOfRangeException(nameof(seconds));this.parent=parent;Seconds=parent.Seconds;limitSeconds=seconds;clock=parent.clock;started=clock.GetTimestamp();}
    // Seconds remains the frozen profile total. A phase can only shorten its parent's deadline.
    public ProviderRequestBudget LimitPhase(int seconds)=>new(this,seconds);
    public TimeSpan Remaining
    {
        get
        {
            var remaining=TimeSpan.FromTicks(Math.Max(0,TimeSpan.FromSeconds(limitSeconds).Ticks-clock.GetElapsedTime(started).Ticks));
            if(parent==null)return remaining;
            var original=parent.Remaining;return original<remaining?original:remaining;
        }
    }
    public void Check(){if(Remaining==TimeSpan.Zero)throw new OperationCanceledException("Frozen request budget expired.");}
    public Deadline Link(CancellationToken parent)=>new(this,clock,parent);
    public sealed class Deadline:IDisposable
    {
        private readonly CancellationTokenSource timer,linked;private readonly ProviderRequestBudget budget;
        internal Deadline(ProviderRequestBudget budget,TimeProvider clock,CancellationToken parent)
        {this.budget=budget;timer=new(budget.Remaining,clock);linked=CancellationTokenSource.CreateLinkedTokenSource(parent,timer.Token);if(budget.Remaining==TimeSpan.Zero)timer.Cancel();}
        public CancellationToken Token=>linked.Token;
        public bool IsCancellationRequested=>linked.IsCancellationRequested||budget.Remaining==TimeSpan.Zero;
        public void Check(){linked.Token.ThrowIfCancellationRequested();budget.Check();}
        public void Dispose(){linked.Dispose();timer.Dispose();}
    }
}
