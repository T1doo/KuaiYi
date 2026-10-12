using KuaiYi.Core.Settings;
namespace KuaiYi.Providers;
internal sealed class PhaseTimeoutException(string reason):OperationCanceledException("Request phase expired.")
{public string Reason{get;}=reason;}
internal sealed class ProviderPhaseDeadline:IDisposable
{
    private readonly CancellationTokenSource timer,linked;private readonly Func<bool>? expired;public string Reason{get;}public CancellationToken Token=>linked.Token;public bool Expired=>timer.IsCancellationRequested||expired?.Invoke()==true;
    public ProviderPhaseDeadline(TimeSpan remaining,TimeProvider clock,CancellationToken parent,string reason,Func<bool>? expired=null)
    {Reason=reason;this.expired=expired;timer=new(remaining,clock);linked=CancellationTokenSource.CreateLinkedTokenSource(parent,timer.Token);if(expired?.Invoke()==true)timer.Cancel();}
    public void Check(){if(Expired)throw new PhaseTimeoutException(Reason);}
    public void Dispose(){linked.Dispose();timer.Dispose();}
}
internal sealed class ProviderReadActivity(RequestTimeouts timeouts,TimeProvider clock)
{
    private long last=clock.GetTimestamp();private bool first=true;
    public string Reason=>first?"FIRST_RESPONSE_TIMEOUT":"READ_IDLE_TIMEOUT";
    public ProviderPhaseDeadline Link(CancellationToken parent)
    {
        var remaining=TimeSpan.FromSeconds(first?timeouts.FirstResponseSeconds:timeouts.IdleSeconds)-clock.GetElapsedTime(last);
        if(remaining<=TimeSpan.Zero)throw new PhaseTimeoutException(Reason);
        long origin=last;int seconds=first?timeouts.FirstResponseSeconds:timeouts.IdleSeconds;return new(remaining,clock,parent,Reason,()=>clock.GetElapsedTime(origin)>=TimeSpan.FromSeconds(seconds));
    }
    public void Effective(){first=false;last=clock.GetTimestamp();}
}
