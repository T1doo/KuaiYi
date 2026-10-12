namespace KuaiYi.Providers;

/// <summary>Immutable eligibility captured before enqueue: original request origin and
/// this event's existing activity origin. Contains no source, output or credential.</summary>
internal sealed class ProviderEventAdmission
{
    private readonly ProviderRequestBudget budget;
    private readonly CancellationToken requestCancellation;
    private readonly TimeProvider? phaseClock;
    private readonly long phaseOrigin;
    private readonly TimeSpan phaseLimit;
    private readonly string? phaseReason;
    public ProviderEventAdmission(ProviderRequestBudget budget,CancellationToken requestCancellation,
        TimeProvider? phaseClock=null,long phaseOrigin=0,TimeSpan phaseLimit=default,string? phaseReason=null)
    {this.budget=budget;this.requestCancellation=requestCancellation;this.phaseClock=phaseClock;this.phaseOrigin=phaseOrigin;this.phaseLimit=phaseLimit;this.phaseReason=phaseReason;}
    public string? FailureReason()
    {
        if(budget.Remaining==TimeSpan.Zero)return "REQUEST_TIMEOUT";
        if(requestCancellation.IsCancellationRequested)return "CANCELLED";
        if(phaseClock!=null&&phaseClock.GetElapsedTime(phaseOrigin)>=phaseLimit)return phaseReason;
        return null;
    }
}
