using KuaiYi.Core;
namespace KuaiYi.Providers;
/// <summary>Provider facade over the shared, identity-checked destination queue.</summary>
public sealed class ProviderEventReceiver(RuntimeGate gate, SynchronizationContext destination)
{
    private readonly IdentityEventReceiver<ModelEvent> receiver = new(gate,destination);
    public Task<bool> ReceiveAsync(ModelEventEnvelope envelope, Action<ModelEvent> apply, CancellationToken cancellation = default,ProviderRequestBudget? requestBudget=null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(apply);
        return receiver.ReceiveAsync(envelope.Identity,envelope.Event,value=>
        {
            // Runs inside the canonical destination gate, after its identity and caller-cancel checks.
            // A delayed event cannot extend the original budget or refresh the frozen phase origin.
            apply(Admit(envelope,value,requestBudget));
        },cancellation);
    }
    internal static ModelEvent Admit(ModelEventEnvelope envelope,ModelEvent value,ProviderRequestBudget? requestBudget)
    {
        string? failure=requestBudget?.Remaining==TimeSpan.Zero?"REQUEST_TIMEOUT":envelope.Admission?.FailureReason();
        return failure==null?value:value with{Kind=ModelEventKind.Failed,Text=null,Usage=null,Reason=failure,RetryAfter=null};
    }
}
