namespace KuaiYi.Core;
public static class ApiConnectionTestContent
{
    public const string Source="KuaiYi connection test. Please reply: 快译连接测试。";
    public const string System="这是快译固定无隐私连接测试。请只回应：快译连接测试。";
}
public sealed partial class RuntimeGate
{
    private bool connectionTest,runningRequest;
    public bool HasRunningGeneration{get{lock(sync)return active!=null&&runningRequest&&!connectionTest;}}
    public bool IsConnectionTest(RequestIdentity identity){lock(sync)return active==identity&&connectionTest;}
    public void MarkRequestTerminal(RequestIdentity identity){lock(sync){if(active==identity)runningRequest=false;}}
    public bool TryStartConnectionTest(RequestProfileBinding binding,EndpointOrigin origin,long expectedEpoch,out RequestIdentity identity,out CancellationToken cancellation)
    {
        CancellationTokenSource? previous;
        lock(sync)
        {
            identity=default;cancellation=default;
            if(epoch!=expectedEpoch||active!=null&&runningRequest||localOnly||!consented.Contains(origin)||
                (suspension&~Suspension.Settings)!=Suspension.None||!profileBindings.TryGetValue(binding.ProfileId,out var current)||current!=binding)return false;
            previous=activeCancellation;activeCancellation=new();activeProfileBinding=binding;connectionTest=runningRequest=true;
            identity=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),++epoch);active=identity;cancellation=activeCancellation.Token;
        }
        previous?.Cancel();previous?.Dispose();return true;
    }
}
