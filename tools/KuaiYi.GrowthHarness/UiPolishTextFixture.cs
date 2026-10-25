using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;

namespace KuaiYi.GrowthHarness;

// Synthetic source and held transport only. Original coordinator owns snapshots and gates.
// No stream/socket is ever returned, and no provider response is represented as genuine.
internal sealed class UiPolishTextFixture:IAsyncDisposable
{
    internal const string SourceText="公开合成原文：清晰的信息，让理解自然发生。";
    internal const string ResultText="公开合成译文：清晰的信息，让理解自然发生。";
    internal const string Endpoint="https://public.invalid/v1/";
    internal const string Model="PUBLIC_VISUAL_FIXTURE";
    private readonly RuntimeGate gate;
    private readonly List<Task> runs=[];
    private readonly List<Task> connectors=[];
    private readonly object sync=new();
    private RequestIdentity? activeIdentity;
    private TaskCompletionSource entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int attempts;
    private int started;
    private Task? disposal;
    private bool providerDisposed;
    internal ProviderClient Provider{get;}
    internal TextTranslationCoordinator Flow{get;}
    internal SelectionSourceSnapshot Source{get;}
    internal int Attempts=>Volatile.Read(ref attempts);
    internal IReadOnlyList<Task> OriginalRuns=>runs;
    internal IReadOnlyList<Task> OriginalConnectors{get{lock(sync)return connectors.ToArray();}}
    internal static AppSettings Configuration(ThemePreference theme,MotionPreference motion)=>new()
    {
        ActiveProfileId="ui-polish",Profiles=[new(){Id="ui-polish",Name="公开合成视觉夹具",Model=Model,BaseUrl=Endpoint,Authentication=ApiAuthentication.None,Timeouts=new(){ConnectSeconds=60,HeadersSeconds=60}}],
        Overlay=new(){Theme=theme,ReduceMotion=motion,FontSizeDip=16,PreferredWidthDip=480,RememberPosition=false}
    };
    internal UiPolishTextFixture(RuntimeGate gate,OverlayResults results,SettingsSnapshot settings,SynchronizationContext destination,Action<TextTranslationUpdate> changed)
    {
        var profile=settings.Data.Profiles.Single();
        if(profile.Id!="ui-polish"||settings.Data.ActiveProfileId!=profile.Id||profile.BaseUrl!=Endpoint||profile.Model!=Model||
            profile.Protocol!=KuaiYi.Core.Settings.ApiProtocol.OpenAIChat||profile.AddressMode!=ApiAddressMode.BasePrefix||profile.EndpointPath.Length!=0||profile.AuthenticationHeader.Length!=0||
            profile.Authentication!=ApiAuthentication.None||profile.SecretRef!=null||profile.Headers.Count!=0||profile.AllowLoopbackHttp||profile.AllowInsecureHttp)
            throw new InvalidOperationException("Only the fixed AuthNone synthetic profile is accepted.");
        this.gate=gate;
        Source=new(new(17,92823,132000,@"C:\Synthetic\ui-polish.exe"),SourceText,SourceText,[new(1,2,20,22)],[7,8],settings.Version,SourceText.Length,false);
        var signature=new[]{typeof(DevelopmentTranslationBoundary),typeof(TimeProvider),typeof(TimeProvider),typeof(Func<SocketsHttpConnectionContext,CancellationToken,ValueTask<Stream>>)};
        var constructor=typeof(ProviderClient).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,signature,null)
            ??throw new MissingMethodException("Exact original provider test connector required.");
        Provider=(ProviderClient)constructor.Invoke([null,null,null,(Func<SocketsHttpConnectionContext,CancellationToken,ValueTask<Stream>>)Connect]);
        try
        {
            Flow=new(gate,()=>settings,(source,_)=>ReferenceEquals(source,Source),Provider,destination,results,update=>
                {
                    if(update.Presentation.Kind==OverlayResultEventKind.Begin)lock(sync)
                    {if(activeIdentity.HasValue)throw new InvalidOperationException("Only one original identity per provider.");activeIdentity=update.Identity;}
                    changed(update);
                },
                (_,_,_)=>throw new InvalidOperationException("No fixture credentials allowed."),transportFactory:p=>
                {
                    if(!ReferenceEquals(p,profile))throw new InvalidOperationException("Synthetic profile replaced.");
                    var transport=TextTranslationCoordinator.ProfileTransport(p);
                    if(transport.DevelopmentBoundary!=null||transport.ResolveEndpoint().AbsoluteUri!=Endpoint+"chat/completions")
                        throw new InvalidOperationException("A socket-capable development boundary is forbidden.");
                    return transport;
                });
        }
        catch{Provider.Dispose();throw;}
    }
    private ValueTask<Stream> Connect(SocketsHttpConnectionContext context,CancellationToken cancellation)
    {
        RequestIdentity id;lock(sync)id=activeIdentity??throw new InvalidOperationException("No original fixture identity.");
        Task<Stream> original=Hold(context,cancellation,id);lock(sync)connectors.Add(original);
        if(original.IsCompleted)entered.TrySetException(new InvalidOperationException("Synthetic connector failed before entering its hold."));
        else entered.TrySetResult();
        return new(original);
    }
    private async Task<Stream> Hold(SocketsHttpConnectionContext context,CancellationToken cancellation,RequestIdentity id)
    {
        if(context.InitialRequestMessage.Method!=HttpMethod.Post||context.InitialRequestMessage.RequestUri?.AbsoluteUri!=Endpoint+"chat/completions"||
            context.DnsEndPoint.Host!="public.invalid"||context.DnsEndPoint.Port!=443||context.InitialRequestMessage.Headers.Authorization!=null||
            context.InitialRequestMessage.Headers.Contains("x-api-key")||Flow.Results.Identity!=id||!gate.CanApply(id))
            throw new InvalidOperationException("Unexpected synthetic connector context; no socket fallback.");
        if(!gate.TryLinkRequestCancellation(id,cancellation,out var linked))throw new OperationCanceledException(cancellation);
        using(linked)
        {
            Interlocked.Increment(ref attempts);
            await Task.Delay(Timeout.Infinite,linked!.Token).ConfigureAwait(false);
            throw new InvalidOperationException("Synthetic hold may end only through cancellation.");
        }
    }
    internal async Task<RequestIdentity> Start(CancellationToken cancellation)
    {
        if(runs.Any(r=>!r.IsCompleted)||OriginalConnectors.Any(r=>!r.IsCompleted))throw new InvalidOperationException("Original synthetic request still running.");
        if(Interlocked.Exchange(ref started,1)!=0)throw new InvalidOperationException("One request per fixture/provider; create a new original owner after full closure.");
        entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var offer=Flow.Prepare(Source)??throw new InvalidOperationException("Original snapshot admission refused: "+Flow.LastRejection);
        if(Flow.Submit(offer)!=TextSubmission.Started)throw new InvalidOperationException("Original coordinator submission refused.");
        runs.Add(Flow.Running);await entered.Task.WaitAsync(cancellation);
        return Flow.Results.Identity??throw new InvalidOperationException("Original identity missing.");
    }
    internal async Task CancelAndJoin(RequestIdentity id)
    {
        if(Flow.Results.Identity!=id)throw new InvalidOperationException("Current fixture identity changed; no cancellation issued.");
        gate.StopRequest(id); // Exact original request; no cancellation of a later request.
        await runs[^1];
        foreach(var task in OriginalConnectors)
            try{await task;}catch(OperationCanceledException)when(task.IsCanceled){}
        if(OriginalConnectors.Count!=runs.Count||OriginalConnectors.Any(t=>!t.IsCompleted))throw new InvalidOperationException("Original connector settlement differs.");
    }
    internal void DisposeProviderAfterOriginalClose()
    {
        if(providerDisposed)return;
        if(runs.Any(r=>!r.IsCompleted)||OriginalConnectors.Any(r=>!r.IsCompleted))throw new InvalidOperationException("Provider still owned by original activity.");
        Provider.Dispose();providerDisposed=true;
    }
    public ValueTask DisposeAsync()=>new(disposal??=DisposeOriginal());
    private async Task DisposeOriginal()
    {
        List<Exception> errors=[];
        try{await Flow.DisposeAsync();}catch(Exception error){errors.Add(error);}
        foreach(var task in runs)try{await task;}catch(Exception error){errors.Add(error);}
        foreach(var task in OriginalConnectors)
            try{await task;}catch(OperationCanceledException)when(task.IsCanceled){}catch(Exception error){errors.Add(error);}
        try{DisposeProviderAfterOriginalClose();}catch(Exception error){errors.Add(error);}
        if(errors.Count>0)throw new AggregateException("Original fixture settlement failed; evidence retained.",errors);
    }
}
