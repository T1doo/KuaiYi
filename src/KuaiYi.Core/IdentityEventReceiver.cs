namespace KuaiYi.Core;

/// <summary>One identity gate for all event kinds, rechecked atomically on the destination thread.</summary>
public sealed class IdentityEventReceiver<T>(RuntimeGate gate, SynchronizationContext destination) where T : class
{
    public async Task<bool> ReceiveAsync(RequestIdentity identity, T value, Action<T> apply, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(value);
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
                            gate.TryApply(identity, () =>
                            {
                                if (cancellation.IsCancellationRequested) return;
                                apply(value); applied = true;
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
