namespace KuaiYi.Core;
public enum ApiTestPurpose{TextConnection,VisionFixture}
public static class ApiConnectionTestContent
{
    public const string Source="KuaiYi connection test. Please reply: 快译连接测试。";
    public const string System="这是快译固定无隐私连接测试。请只回应：快译连接测试。";
}
public sealed partial class RuntimeGate
{
    private bool connectionTest,runningRequest;
    private EndpointOrigin? connectionTestOrigin;
    private ApiTestPurpose connectionTestPurpose;
    private sealed record VisionTestDestination(Uri Endpoint,Settings.ApiProtocol Protocol);
    private VisionTestDestination? visionTestEndpoint;
    public bool HasRunningGeneration{get{lock(sync)return active!=null&&runningRequest&&!connectionTest;}}
    public bool IsConnectionTest(RequestIdentity identity){lock(sync)return active==identity&&connectionTest&&connectionTestPurpose==ApiTestPurpose.TextConnection;}
    public bool IsVisionTest(RequestIdentity identity){lock(sync)return active==identity&&connectionTest&&connectionTestPurpose==ApiTestPurpose.VisionFixture;}
    public bool IsVisionTestDestination(RequestIdentity identity,Uri endpoint,Settings.ApiProtocol protocol){lock(sync)return IsVisionTest(identity)&&visionTestEndpoint?.Endpoint.AbsoluteUri==endpoint.AbsoluteUri&&visionTestEndpoint.Protocol==protocol;}
    public void MarkRequestTerminal(RequestIdentity identity){lock(sync){if(active==identity)runningRequest=false;}}
    public bool TryStartConnectionTest(RequestProfileBinding binding,EndpointOrigin origin,long expectedEpoch,out RequestIdentity identity,out CancellationToken cancellation)
        =>TryStartApiTest(binding,origin,null,null,expectedEpoch,ApiTestPurpose.TextConnection,out identity,out cancellation);
    public bool TryStartVisionTest(RequestProfileBinding binding,Uri endpoint,Settings.ApiProtocol protocol,long expectedEpoch,out RequestIdentity identity,out CancellationToken cancellation)
        {if(!Enum.IsDefined(protocol)){identity=default;cancellation=default;return false;}return TryStartApiTest(binding,new(endpoint),endpoint,protocol,expectedEpoch,ApiTestPurpose.VisionFixture,out identity,out cancellation);}
    private bool TryStartApiTest(RequestProfileBinding binding,EndpointOrigin origin,Uri? endpoint,Settings.ApiProtocol? protocol,long expectedEpoch,ApiTestPurpose purpose,out RequestIdentity identity,out CancellationToken cancellation)
    {
        CancellationTokenSource? previous;
        lock(sync)
        {
            identity=default;cancellation=default;
            if(epoch!=expectedEpoch||active!=null&&runningRequest||localOnly||string.IsNullOrEmpty(origin.Value)||
                (suspension&~Suspension.Settings)!=Suspension.None||!profileBindings.TryGetValue(binding.ProfileId,out var current)||current!=binding)return false;
            previous=activeCancellation;activeCancellation=new();activeProfileBinding=binding;connectionTest=runningRequest=true;connectionTestPurpose=purpose;visionTestEndpoint=endpoint==null?null:new(endpoint,protocol!.Value);
            identity=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),++epoch);active=identity;connectionTestOrigin=origin;cancellation=activeCancellation.Token;
        }
        previous?.Cancel();previous?.Dispose();return true;
    }
}
