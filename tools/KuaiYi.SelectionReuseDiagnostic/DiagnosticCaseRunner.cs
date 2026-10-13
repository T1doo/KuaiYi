namespace KuaiYi.SelectionReuseDiagnostic;

public sealed record DiagnosticCaseResult(bool Passed,bool TimedOut,bool CleanupSettled);
// Case timeout does not abandon the original task. An uncooperative case keeps the runner here.
public static class DiagnosticCaseRunner
{
    private sealed class Registry
    {
        public readonly List<ReuseDiagnosticHost> Hosts=[];
        public void Stop(){lock(Hosts)foreach(var host in Hosts)host.ParentDisconnected();}
        public async Task SettleAsync()
        {
            ReuseDiagnosticHost[] hosts;lock(Hosts)hosts=Hosts.ToArray();
            foreach(var host in hosts)
            {
                while(true)
                {
                    try{await host.DisposeAsync().ConfigureAwait(false);break;}
                    catch(IOException){await Task.Delay(20).ConfigureAwait(false);}
                }
            }
        }
    }
    private static readonly AsyncLocal<Registry?> current=new();
    internal static void Register(ReuseDiagnosticHost host)
    {
        if(current.Value is not {} registry)return;
        lock(registry.Hosts){if(registry.Hosts.Count!=0)throw new InvalidOperationException("One owned host per case.");registry.Hosts.Add(host);}
    }
    public static async Task<DiagnosticCaseResult> ExecuteAsync(Func<Task> operation,TimeSpan limit,Action? timeoutObservedForTest=null)
    {
        if(limit<=TimeSpan.Zero||limit>TimeSpan.FromSeconds(12))throw new ArgumentOutOfRangeException(nameof(limit));
        var prior=current.Value;var registry=new Registry();current.Value=registry;
        bool passed=true,timedOut=false;Task? original=null;
        try
        {
            try{original=operation();await original.WaitAsync(limit).ConfigureAwait(false);}
            catch(TimeoutException){passed=false;timedOut=true;registry.Stop();timeoutObservedForTest?.Invoke();}
            catch(Exception){passed=false;registry.Stop();}
            if(original!=null){try{await original.ConfigureAwait(false);}catch(Exception){passed=false;}}
            await registry.SettleAsync().ConfigureAwait(false);
            return new(passed&&!timedOut,timedOut,true);
        }
        finally{current.Value=prior;}
    }
}
