using KuaiYi.Core;
namespace KuaiYi.NativeHost;
/// <summary>Continuous revocation spans the last source check, Task installation and actual preparation.
/// Cancelling marks eligibility immediately; the original async cancellation callbacks remain owned.</summary>
public sealed class BrowserImagePreparationPermit:IAsyncDisposable
{
    private readonly object sync=new();private readonly CancellationTokenSource source=new();private readonly Func<bool> current;
    private readonly TaskCompletionSource issued=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task cancellation=Task.CompletedTask;private bool cancelling,closing;
    public long ExpectedEpoch{get;}public Activation InitialActivation{get;}
    public CancellationToken Token{get;}
    public bool IsCurrent{get{lock(sync)return !closing&&!cancelling&&!Token.IsCancellationRequested&&current();}}
    internal BrowserImagePreparationPermit(BrowserImageSourceState state,Func<bool> current)
    {ExpectedEpoch=state.Epoch;InitialActivation=state.Activation;this.current=current;Token=source.Token;}
    internal void Revoke()
    {
        lock(sync){if(closing||cancelling)return;cancelling=true;}
        Task original;try{original=source.CancelAsync();}catch(Exception error){original=Task.FromException(error);}
        lock(sync)cancellation=original;issued.TrySetResult();
    }
    public async ValueTask DisposeAsync()
    {
        bool pending;lock(sync){closing=true;pending=cancelling;}
        try{if(pending){await issued.Task.ConfigureAwait(false);Task original;lock(sync)original=cancellation;await original.ConfigureAwait(false);}}
        finally{source.Dispose();}
    }
}
