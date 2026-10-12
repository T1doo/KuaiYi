using KuaiYi.Core;
namespace KuaiYi.Providers;
/// <summary>Provider facade over the shared, identity-checked destination queue.</summary>
public sealed class ProviderEventReceiver(RuntimeGate gate, SynchronizationContext destination)
{
    private readonly IdentityEventReceiver<ModelEvent> receiver = new(gate,destination);
    public Task<bool> ReceiveAsync(ModelEventEnvelope envelope, Action<ModelEvent> apply, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return receiver.ReceiveAsync(envelope.Identity,envelope.Event,apply,cancellation);
    }
}
