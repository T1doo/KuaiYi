using System.Collections.Concurrent;
using System.Text.Json;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests;
internal sealed class VisionFixtureRig:IAsyncDisposable
{
    internal sealed class Destination:SynchronizationContext
    {
        private readonly ConcurrentQueue<Action> queue=new();public volatile bool Hold;public int Pending=>queue.Count;
        public override void Post(SendOrPostCallback callback,object? state){if(Hold)queue.Enqueue(()=>callback(state));else callback(state);}
        public void Drain(){while(queue.TryDequeue(out var callback))callback();}
    }
    private sealed class NoKeys:ISecretProtector
    {public byte[] Protect(ReadOnlySpan<byte> bytes)=>throw new InvalidOperationException("No credentials allowed.");public byte[] Unprotect(ReadOnlySpan<byte> bytes)=>throw new InvalidOperationException("No credentials allowed.");}
    private sealed class PrivateDirectory:ISecretDirectoryPolicy{public void EnsurePrivate(string path)=>Directory.CreateDirectory(path);}
    public readonly string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-vision-fixture-"+Guid.NewGuid().ToString("N"));
    public readonly PhaseHttpFixture Server=new();public PhaseHttpFixture.Script Script=null!;
    public readonly RuntimeGate Gate=new(requireProfileBinding:true);public readonly Destination Context=new();public readonly ConcurrentQueue<ApiTestUpdate> Updates=new();
    public SettingsCoordinator Coordinator=null!;public SettingsRuntimeLink Link=null!;public SettingsDraft Draft=null!;public SettingsSnapshot Settings=null!;
    public ProviderClient Provider=null!;public ApiConnectionTestWorkflow Flow=null!;public SettingsWindow Window=null!;public DevelopmentTranslationBoundary? Boundary;public PhaseClock? Clock;
    public Uri Prefix=null!;public Protocol Protocol;public bool Stream;public int SecretReads;public Task Job=Task.CompletedTask;public Action? OnRead=null;public Action<ApiTestUpdate>? OnUpdate=null;
    public static async Task<VisionFixtureRig> Create(Protocol protocol=Protocol.OpenAIChat,bool stream=false,Capability capability=Capability.Unknown,bool boundary=true,bool clock=false,bool headers=true)
    {
        var r=new VisionFixtureRig{Protocol=protocol,Stream=stream};await r.Server.InitializeAsync();r.Script=r.Server.Add("runs",stream?"text/event-stream":"application/json",headers);r.Prefix=new(r.Server.BaseUri,$"runs/{Guid.NewGuid():N}/fragmented/");r.Boundary=boundary?DevelopmentTranslationBoundary.FromPrefix(r.Prefix):null;r.Clock=clock?new():null;
        r.Coordinator=new(new(Path.Combine(r.Root,"config")),new(Path.Combine(r.Root,"secrets"),new NoKeys(),new PrivateDirectory()));
        using(var seed=await r.Coordinator.OpenDraftAsync())
        {
            seed.Update(s=>s with{ActiveProfileId="p",Profiles=[new(){Id="p",Protocol=protocol==Protocol.OpenAIChat?KuaiYi.Core.Settings.ApiProtocol.OpenAIChat:KuaiYi.Core.Settings.ApiProtocol.AnthropicMessages,BaseUrl=r.Prefix.AbsoluteUri,Model=boundary?DevelopmentTranslationBoundary.FixtureModel:"PUBLIC_NO_MODEL_CALL",Authentication=ApiAuthentication.None,Streaming=stream,AllowLoopbackHttp=true,Capabilities=new(){Vision=capability,UsageInStream=Capability.Supported},TimeoutSeconds=20,Timeouts=new(){ConnectSeconds=2,HeadersSeconds=3,FirstResponseSeconds=4,IdleSeconds=5}}]});await r.Coordinator.SaveAsync(seed);
        }
        r.Settings=r.Coordinator.Effective!;r.Draft=await r.Coordinator.OpenDraftAsync();r.Link=new(r.Coordinator,r.Gate);r.Link.Initialize(r.Settings);r.Gate.SetSuspension(Suspension.Settings,true);
        r.Provider=new(r.Boundary,r.Clock,r.Clock);r.Flow=new(r.Gate,()=>{r.OnRead?.Invoke();return r.Settings;},r.Provider,r.Context,(_,_,_)=>{r.SecretReads++;throw new InvalidOperationException("Vision fixture must never open a secret.");},r.Boundary);
        r.Window=new(r.Draft,r.Flow);r.Window.ApiCoordinatorForTest(r.Coordinator);return r;
    }
    public ApiTestIntent Prepare(bool vision=true){var intent=vision?Flow.PrepareVision("p",out _):Flow.Prepare("p",out _);return Assert.IsType<ApiTestIntent>(intent);}
    public Task Start(ApiTestIntent? intent=null,bool vision=true){intent??=Prepare(vision);var job=Flow.Start(intent,true,u=>{Updates.Enqueue(u);OnUpdate?.Invoke(u);});Assert.NotNull(job);return Job=job;}
    public async Task Requests(int count)=>await Wait(()=>Volatile.Read(ref Script.Requests)>=count);
    public static async Task Wait(Func<bool> condition){using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(!condition())await Task.Delay(5,stop.Token);}
    public string Response(string text="PUBLIC FIXTURE: KUAIYI TEST")=>Protocol==Protocol.OpenAIChat?JsonSerializer.Serialize(new{choices=new[]{new{index=0,message=new{content=text},finish_reason="stop"}},usage=new{prompt_tokens=14,completion_tokens=3}}):JsonSerializer.Serialize(new{type="message",content=new[]{new{type="text",text}},stop_reason="end_turn",usage=new{input_tokens=14,output_tokens=3}});
    public string StartFrames=>Protocol==Protocol.OpenAIChat?"data: {\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\"}}]}\n\n":"data: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":14,\"output_tokens\":0}}}\n\n";
    public string EndFrames(string text="PUBLIC FIXTURE: KUAIYI TEST")=>Protocol==Protocol.OpenAIChat?"data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":"+JsonSerializer.Serialize(text)+"}}]}\n\ndata: {\"choices\":[],\"usage\":{\"prompt_tokens\":14,\"completion_tokens\":3}}\n\ndata: [DONE]\n\n":
        "data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":"+JsonSerializer.Serialize(text)+"}}\n\ndata: {\"type\":\"content_block_stop\",\"index\":0}\n\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":3}}\n\ndata: {\"type\":\"message_stop\"}\n\n";
    public async Task Reply(){await Script.Write(Stream?StartFrames+EndFrames():Response(),true);await Job.WaitAsync(TimeSpan.FromSeconds(6));}
    public (string System,string Source,byte[]? Image) Wire()
    {
        using var json=JsonDocument.Parse(Script.Bodies.Last());var root=json.RootElement;
        string system=Protocol==Protocol.OpenAIChat?root.GetProperty("messages")[0].GetProperty("content").GetString()!:root.GetProperty("system").GetString()!;
        var content=root.GetProperty("messages")[Protocol==Protocol.OpenAIChat?1:0].GetProperty("content");
        if(content.ValueKind==JsonValueKind.String)return(system,content.GetString()!,null);
        if(Protocol==Protocol.OpenAIChat){string data=content[1].GetProperty("image_url").GetProperty("url").GetString()!;Assert.StartsWith("data:image/png;base64,",data);return(system,content[0].GetProperty("text").GetString()!,Convert.FromBase64String(data[22..]));}
        if(content.GetArrayLength()==1)return(system,content[0].GetProperty("text").GetString()!,null);
        Assert.Equal("image",content[0].GetProperty("type").GetString());Assert.Equal("image/png",content[0].GetProperty("source").GetProperty("media_type").GetString());return(system,content[1].GetProperty("text").GetString()!,Convert.FromBase64String(content[0].GetProperty("source").GetProperty("data").GetString()!));
    }
    public async ValueTask DisposeAsync()
    {
        Gate.Disable();Context.Hold=false;Context.Drain();await Flow.DisposeAsync();Provider.Dispose();Link.Dispose();Draft.Dispose();await Server.DisposeAsync();if(Directory.Exists(Root))Directory.Delete(Root,true);
    }
}
