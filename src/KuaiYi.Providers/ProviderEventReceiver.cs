using KuaiYi.Core;

namespace KuaiYi.Providers;

/// <summary>All event kinds cross one identity-checked destination queue. UI integration must use this receiving boundary.</summary>
public sealed class ProviderEventReceiver(RuntimeGate gate, SynchronizationContext destination)
{
    public async Task<bool> ReceiveAsync(ModelEventEnvelope envelope, Action<ModelEvent> apply, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(apply);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        object deliverySync = new();
        using var registration = cancellation.Register(() => { lock (deliverySync) completion.TrySetResult(false); });
        if (!completion.Task.IsCompleted)
        {
            try
            {
                destination.Post(_ =>
                {
                    lock (deliverySync)
                    {
                        if (completion.Task.IsCompleted) return;
                        try
                        {
                            bool applied = false;
                            gate.TryApply(envelope.Identity, () =>
                            {
                                if (cancellation.IsCancellationRequested) return;
                                apply(envelope.Event); applied = true;
                            });
                            completion.TrySetResult(applied);
                        }
                        catch (Exception error) { completion.TrySetException(error); }
                    }
                }, null);
            }
            catch (Exception error) { completion.TrySetException(error); }
        }
        return await completion.Task.ConfigureAwait(false);
    }
}
