using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Imaging;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
using Lease=KuaiYi.Providers.SecretLease;
namespace KuaiYi.Provider.Tests;

public sealed partial class ProductionScreenshotTranslationTests
{
    private sealed class Immediate:SynchronizationContext{public override void Post(SendOrPostCallback action,object? state)=>action(state);}
    private sealed class Deferred:SynchronizationContext
    {private readonly ConcurrentQueue<Action> queue=new();public int Pending=>queue.Count;public override void Post(SendOrPostCallback action,object? state)=>queue.Enqueue(()=>action(state));public void Drain(){while(queue.TryDequeue(out var action))action();}}
    private sealed class Rig:IAsyncDisposable
    {
        public readonly PhaseHttpFixture Server=new();public PhaseHttpFixture.Script Script=null!;
        public readonly RuntimeGate Gate=new(requireProfileBinding:true);public readonly ProviderClient Provider;
        private Rig(TimeProvider? clock=null){Provider=new(clock:clock);}
        public SettingsSnapshot Settings=null!;public ProductionScreenshotCoordinator Flow=null!;public OverlayResults Results=null!;
        public OwnedSourceImage Image=null!;public byte[] Raw=[];public Guid Version=Guid.NewGuid();public bool Current=true;public int SecretReads;public Deferred? DeferredDestination;
        public readonly List<Lease> Leases=[];public readonly List<ProductionScreenshotUpdate> Updates=[];
        public Action<ProductionScreenshotUpdate>? OnUpdate;public Func<int,Task>? BeforeSecret;public Action<Lease,CancellationToken>? SecretCreated;public Protocol Protocol;public bool Stream;
        public static async Task<Rig> Create(Protocol protocol=Protocol.OpenAIChat,bool stream=false,bool auth=false,SynchronizationContext? destination=null,TimeProvider? clock=null)
        {
            var r=new Rig(clock){Protocol=protocol,Stream=stream,DeferredDestination=destination as Deferred};await r.Server.InitializeAsync();r.Script=r.Server.Add("vision",stream?"text/event-stream":"application/json");
            r.Settings=new(new(){ActiveProfileId="text",VisionProfileId="vision",Profiles=[new(){Id="text",BaseUrl="https://text.public.invalid/v1",Model="PUBLIC_TEXT_MODEL",Authentication=ApiAuthentication.None},new(){Id="vision",Protocol=protocol==Protocol.OpenAIChat?KuaiYi.Core.Settings.ApiProtocol.OpenAIChat:KuaiYi.Core.Settings.ApiProtocol.AnthropicMessages,BaseUrl=new Uri(r.Server.BaseUri,"vision/").AbsoluteUri,AllowLoopbackHttp=true,Model="PUBLIC_SCREENSHOT_MODEL",Authentication=auth?(protocol==Protocol.OpenAIChat?ApiAuthentication.Bearer:ApiAuthentication.ApiKey):ApiAuthentication.None,SecretRef=auth?"key/"+new string('a',32):null,Streaming=stream,Capabilities=new(){Vision=Capability.Supported},TimeoutSeconds=20}],Translation=new(){Preset=TranslationPreset.Professional}},"saved-public-config",SettingsLoadState.Current);
            r.Gate.ObserveSettingsMetadata(r.Settings);var textOrigin=new EndpointOrigin(new("https://text.public.invalid"));r.Gate.ConsentTo(textOrigin);Assert.True(r.Gate.TryEnable(textOrigin,true));
            r.Results=new(r.Gate);r.Flow=new(r.Gate,()=>r.Settings,(version,id)=>r.Current&&version==r.Version&&id==r.Image.SourceId&&!r.Image.IsDisposed,r.Provider,destination??new Immediate(),r.Results,u=>{lock(r.Updates)r.Updates.Add(u);r.OnUpdate?.Invoke(u);},async(id,snapshot,ct)=>
            {
                int read=Interlocked.Increment(ref r.SecretReads);Assert.True(r.Gate.IsRequestProfileBindingCurrent(id,snapshot.Binding));Assert.True(r.Gate.CanSend(id,new(snapshot.Transport.ResolveEndpoint())));
                if(read==2){lock(r.Leases)Assert.Throws<ObjectDisposedException>(()=>r.Leases[0].HeaderValue());}
                if(r.BeforeSecret!=null)await r.BeforeSecret(read);var lease=new Lease(Encoding.UTF8.GetBytes("PUBLIC_SYNTHETIC_CREDENTIAL"));lock(r.Leases)r.Leases.Add(lease);r.SecretCreated?.Invoke(lease,ct);return lease;
            });r.ReplaceImage();return r;
        }
        public void ReplaceImage(int width=8,int height=8)
        {Image?.Dispose();Version=Guid.NewGuid();Raw=Enumerable.Range(0,width*height*4).Select(i=>(byte)(i%4==3?255:i%251+1)).ToArray();Image=new(new ScreenshotPixels(new(-17,23,width-17,height+23),Raw));}
        public void Edit(Func<AppSettings,AppSettings> edit,bool observe=true)
        {Settings=Settings with{Data=edit(Settings.Data) with{WriteId=Guid.NewGuid()},Version=Guid.NewGuid().ToString("N")};Gate.ObserveSettingsMetadata(Settings);if(observe)Flow.ObserveSettings(Settings);}
        public ProductionScreenshotOffer Offer(ImageOutputMode mode=ImageOutputMode.Automatic)=>Assert.IsType<ProductionScreenshotOffer>(Flow.Prepare(Version,Image,mode));
        public async Task Requests(int count)
        {using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(Volatile.Read(ref Script.Requests)<count)await Task.Delay(5,limit.Token);}
        public async Task Reply(string text,string finish="stop",int input=11,int output=7)
        {
            if(!Stream){await Script.Write(Protocol==Protocol.OpenAIChat?JsonSerializer.Serialize(new{choices=new[]{new{index=0,message=new{content=text},finish_reason=finish}},usage=new{prompt_tokens=input,completion_tokens=output}}):JsonSerializer.Serialize(new{type="message",content=new[]{new{type="text",text}},stop_reason=finish=="stop"?"end_turn":finish,usage=new{input_tokens=input,output_tokens=output}}),true);return;}
            if(Protocol==Protocol.AnthropicMessages)await Script.Write("data: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":"+input+"}}}\n\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n");
            string quoted=JsonSerializer.Serialize(text);await Script.Write(Protocol==Protocol.OpenAIChat?"data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":"+quoted+"}}]}\n\ndata: {\"choices\":[],\"usage\":{\"prompt_tokens\":"+input+",\"completion_tokens\":"+output+"}}\n\n":"data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":"+quoted+"}}\n\n");
            await Script.Write(Protocol==Protocol.OpenAIChat?"data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\""+finish+"\"}]}\n\ndata: [DONE]\n\n":"data: {\"type\":\"content_block_stop\",\"index\":0}\n\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\""+(finish=="stop"?"end_turn":finish)+"\"},\"usage\":{\"output_tokens\":"+output+"}}\n\ndata: {\"type\":\"message_stop\"}\n\n",true);
        }
        public async Task Generate(ImageOutputMode mode=ImageOutputMode.Automatic,string kind="diagram_or_mixed")
        {Assert.True(Flow.Submit(Offer(mode),true));await Requests(1);await Reply(ImageClassificationTests.Json(kind),input:13,output:3);await Requests(2);await Reply("公开截图输出 **正文**",input:29,output:17);await Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));}
        public async ValueTask DisposeAsync()
        {
            try
            {
                var cleanup=Flow.DisposeAsync().AsTask();using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));
                // Test-only cleanup pumps the original queue AFTER body assertions; never substitutes for the assertion's no-drain contract.
                while(!cleanup.IsCompleted){DeferredDestination?.Drain();await Task.Delay(5,limit.Token);}await cleanup;
            }
            finally{Image.Dispose();Provider.Dispose();await Server.DisposeAsync();}
        }
    }
    private static (string System,byte[] Image) Body(Protocol protocol,string body)
    {
        using var document=JsonDocument.Parse(body);var root=document.RootElement;Assert.Equal("PUBLIC_SCREENSHOT_MODEL",root.GetProperty("model").GetString());var content=root.GetProperty("messages")[protocol==Protocol.OpenAIChat?1:0].GetProperty("content");
        return protocol==Protocol.OpenAIChat?(root.GetProperty("messages")[0].GetProperty("content").GetString()!,Convert.FromBase64String(content[1].GetProperty("image_url").GetProperty("url").GetString()![22..])):(root.GetProperty("system").GetString()!,Convert.FromBase64String(content[0].GetProperty("source").GetProperty("data").GetString()!));
    }
    [Theory][InlineData(Protocol.OpenAIChat,false,false)][InlineData(Protocol.AnthropicMessages,false,false)][InlineData(Protocol.OpenAIChat,true,true)][InlineData(Protocol.AnthropicMessages,true,true)]
    public async Task NormalProductionVisionProfileUsesTheSameCropInBothStagesWithSeparateLeasesAndUsage(Protocol protocol,bool stream,bool auth)
    {
        await using var r=await Rig.Create(protocol,stream,auth);var original=r.Raw.ToArray();var offer=r.Offer();Assert.Empty(r.Script.Bodies);Assert.Equal(0,r.SecretReads);Assert.False(r.Gate.HasConsentTo(new(offer.Snapshot.Transport.ResolveEndpoint())));Assert.Contains("PUBLIC_SCREENSHOT_MODEL",offer.ConfirmationText);Assert.Contains("两次",offer.ConfirmationText);Assert.DoesNotContain("PUBLIC_TEXT_MODEL",offer.ConfirmationText);
        Assert.True(r.Flow.Submit(offer,true));await r.Requests(1);await r.Reply(ImageClassificationTests.Json(),input:13,output:3);await r.Requests(2);await r.Reply("public output",input:29,output:17);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));
        var first=Body(protocol,r.Script.Bodies.First());var final=Body(protocol,r.Script.Bodies.Last());Assert.Equal(first.Image,final.Image);Assert.Equal(new ImageSize(8,8),ImagePolicy.InspectPng(final.Image));Assert.Contains(PromptBuilder.Read("image-classify"),first.System);Assert.Equal("public output",r.Results.Text);Assert.True(Assert.Single(r.Results.Versions).Complete);Assert.False(r.Gate.HasRunningGeneration);Assert.Equal(original,r.Raw);Assert.False(r.Image.IsDisposed);
        var snapshot=r.Flow.SelectedSnapshot!;Assert.False(snapshot.ClassificationUsageReused);Assert.NotEqual(snapshot.ClassificationUsage!.InvocationId,snapshot.GenerationUsage!.InvocationId);Assert.Equal(new TokenUsage(13,3),snapshot.ClassificationUsage.Tokens);Assert.Equal(new TokenUsage(29,17),snapshot.GenerationUsage.Tokens);Assert.Equal(r.Version,snapshot.SourceVersion);Assert.Equal(r.Image.SourceId,snapshot.SourceId);Assert.Equal(auth?2:0,r.SecretReads);Assert.All(r.Leases,l=>Assert.Throws<ObjectDisposedException>(()=>l.HeaderValue()));Assert.DoesNotContain("PUBLIC_SYNTHETIC_CREDENTIAL",string.Join("",r.Script.Bodies));
    }
    [Theory][InlineData("decline")][InlineData("off")][InlineData("lock")][InlineData("settings")][InlineData("source")][InlineData("mode")][InlineData("hidden")][InlineData("superseded")]
    public async Task PendingConfirmationRequiresExactSourceSettingsEpochAndExplicitDecision(string action)
    {
        await using var r=await Rig.Create(auth:true);var offer=r.Offer();switch(action){case "off":r.Gate.Disable();break;case "lock":r.Gate.SetSessionLocked(true);break;case "settings":r.Edit(s=>s with{Overlay=s.Overlay with{FontSizeDip=17}});break;case "source":r.ReplaceImage();break;case "mode":r.Gate.SetMode(TriggerMode.Drop);break;case "hidden":r.Gate.Disable(hide:true);break;case "superseded":r.Offer(ImageOutputMode.ExplainImage);break;}
        Assert.False(r.Flow.Submit(offer,action!="decline"));Assert.Equal(0,r.Script.Requests);Assert.Equal(0,r.SecretReads);Assert.False(r.Gate.HasConsentTo(new(offer.Snapshot.Transport.ResolveEndpoint())));Assert.Empty(r.Results.Versions);
    }
    [Theory][InlineData("no-vision")][InlineData("unknown")][InlineData("unsupported")][InlineData("missing-key")][InlineData("read-only")][InlineData("recovery")]
    public async Task UnavailableVisionConfigurationCannotPrepareOrDecrypt(string action)
    {
        await using var r=await Rig.Create(auth:true);if(action=="read-only")r.Settings=r.Settings with{State=SettingsLoadState.ReadOnlyUnknown};else if(action=="recovery")r.Settings=r.Settings with{State=SettingsLoadState.RecoverableDefaults};else r.Edit(s=>action=="no-vision"?s with{VisionProfileId=null}:s with{Profiles=s.Profiles.SetItem(1,s.Profiles[1] with{SecretRef=action=="missing-key"?null:s.Profiles[1].SecretRef,Capabilities=new(){Vision=action=="unknown"?Capability.Unknown:action=="unsupported"?Capability.Unsupported:Capability.Supported}})});
        Assert.Null(r.Flow.Prepare(r.Version,r.Image));Assert.Empty(r.Results.Versions);Assert.Equal(0,r.SecretReads);Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData(ImageOutputMode.Automatic,"diagram_or_mixed",TranslationPreset.Explain)][InlineData(ImageOutputMode.Automatic,"text_only",TranslationPreset.Professional)][InlineData(ImageOutputMode.Automatic,"uncertain",TranslationPreset.Explain)][InlineData(ImageOutputMode.TranslateText,"diagram_or_mixed",TranslationPreset.Professional)][InlineData(ImageOutputMode.ExplainImage,"text_only",TranslationPreset.Explain)]
    public async Task CurrentImageModeIsFrozenWithoutChangingGlobalStyleOrEnablingReuse(ImageOutputMode mode,string kind,TranslationPreset preset)
    {await using var r=await Rig.Create();await r.Generate(mode,kind);Assert.Equal(mode,r.Flow.SelectedSnapshot!.RequestedMode);Assert.Equal(preset,r.Flow.SelectedSnapshot.Plan!.Preset);Assert.Equal(TranslationPreset.Professional,r.Settings.Data.Translation.Preset);Assert.Null(r.Flow.Prepare(r.Version,r.Image,mode));Assert.Equal("IMAGE_REUSE_DISABLED",r.Flow.LastRejection);Assert.Equal(2,r.Script.Requests);}
    [Theory][InlineData("{}","stop")][InlineData("{\"kind\":","stop")][InlineData("valid","length")][InlineData("unreadable","stop")]
    public async Task InvalidIncompleteOrUnreadableClassificationCannotTriggerGeneration(string value,string finish)
    {await using var r=await Rig.Create(auth:true);Assert.True(r.Flow.Submit(r.Offer(),true));await r.Requests(1);await r.Reply(value=="valid"?ImageClassificationTests.Json():value=="unreadable"?ImageClassificationTests.Json(readability:"unreadable"):value,finish);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(1,r.Script.Requests);Assert.Equal(1,r.SecretReads);Assert.DoesNotContain(r.Updates,u=>u.Kind==ProductionScreenshotEvent.Generating);Assert.False(Assert.Single(r.Results.Versions).Complete);Assert.NotNull(r.Results.LastFailure);}
    [Theory][InlineData("prepared")][InlineData("classifying")][InlineData("generating")][InlineData("lease1")][InlineData("lease2")]
    public async Task ReentrantStopAtEachStageOrLeasePreventsTheNextSend(string action)
    {
        await using var r=await Rig.Create(auth:true);r.OnUpdate=u=>{if(u.Kind.ToString().Equals(action,StringComparison.OrdinalIgnoreCase))r.Flow.Stop();};r.BeforeSecret=n=>{if(action=="lease"+n)r.Flow.Stop();return Task.CompletedTask;};Assert.True(r.Flow.Submit(r.Offer(),true));
        if(action is "generating" or "lease2"){await r.Requests(1);await r.Reply(ImageClassificationTests.Json());}
        await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(action is "generating" or "lease2"?1:0,r.Script.Requests);Assert.False(r.Gate.HasRunningGeneration);Assert.False(Assert.Single(r.Results.Versions).Complete);Assert.All(r.Leases,l=>Assert.Throws<ObjectDisposedException>(()=>l.HeaderValue()));
    }
    [Theory][InlineData("display")][InlineData("open-settings")][InlineData("moving")]
    public async Task DisplaySaveAndNonCapturingSuspensionDoNotReplaceTheFrozenRequest(string action)
    {await using var r=await Rig.Create();Assert.True(r.Flow.Submit(r.Offer(),true));var snapshot=r.Flow.SelectedSnapshot!;await r.Requests(1);if(action=="display")r.Edit(s=>s with{Overlay=s.Overlay with{FontSizeDip=17}});else r.Gate.SetSuspension(action=="moving"?Suspension.Moving:Suspension.Settings,true);await r.Reply(ImageClassificationTests.Json());await r.Requests(2);await r.Reply("still own frozen screenshot");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Same(snapshot.Settings,r.Flow.SelectedSnapshot!.Settings);Assert.True(Assert.Single(r.Results.Versions).Complete);}
    [Theory][InlineData("profile")][InlineData("vision-selection")][InlineData("source")][InlineData("style")][InlineData("off")][InlineData("lock")][InlineData("clear")]
    public async Task RevocationBetweenStagesBlocksSecondSendAndSecretRead(string action)
    {
        await using var r=await Rig.Create(auth:true);Assert.True(r.Flow.Submit(r.Offer(),true));await r.Requests(1);switch(action){case "profile":r.Edit(s=>s with{Profiles=s.Profiles.SetItem(1,s.Profiles[1] with{Model="PUBLIC_CHANGED"})});break;case "vision-selection":r.Edit(s=>s with{VisionProfileId="text"});break;case "source":r.Current=false;break;case "style":r.Edit(s=>s with{Translation=s.Translation with{Preset=TranslationPreset.Plain}});break;case "off":r.Gate.Disable();break;case "lock":r.Gate.SetSessionLocked(true);break;case "clear":r.Flow.Clear();break;}
        if(action is not ("off" or "lock" or "clear"))await r.Reply(ImageClassificationTests.Json());await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(1,r.Script.Requests);Assert.Equal(1,r.SecretReads);Assert.DoesNotContain(r.Updates,u=>u.Kind==ProductionScreenshotEvent.Completed);
    }
    [Fact]public async Task QueuedPreparedNotificationAfterClearCannotRestoreMetadataOrSend()
    {var queue=new Deferred();await using var r=await Rig.Create(destination:queue);Assert.True(r.Flow.Submit(r.Offer(),true));using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(queue.Pending==0)await Task.Delay(5,timeout.Token);var running=r.Flow.Running;r.Flow.Clear();await running.WaitAsync(TimeSpan.FromSeconds(2));Assert.Null(r.Flow.SelectedSnapshot);Assert.DoesNotContain(r.Updates,u=>u.Kind!=ProductionScreenshotEvent.Begin);Assert.Equal(0,r.Script.Requests);Assert.False(r.Image.IsDisposed);}
    [Fact]public async Task EncodedFourMiBCapFailsBeforeSecretOrHttpWithoutDisposingBorrowedCrop()
    {await using var r=await Rig.Create(auth:true);r.ReplaceImage(1280,1280);new Random(412936).NextBytes(r.Raw);for(int i=3;i<r.Raw.Length;i+=4)r.Raw[i]=255;Assert.True(r.Flow.Submit(r.Offer(),true));await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(0,r.Script.Requests);Assert.Equal(0,r.SecretReads);Assert.Contains(r.Updates,u=>u.Reason=="SMALLER_SCREENSHOT_REQUIRED");Assert.False(r.Image.IsDisposed);}
    [Fact]public async Task ClearFencesAReplacementAdmissionInsideTheOriginalCancellationCallback()
    {
        await using var r=await Rig.Create();Assert.True(r.Flow.Submit(r.Offer(),true));await r.Requests(1);
        Assert.True(r.Gate.TryLinkRequestCancellation(r.Results.Identity!.Value,default,out var original));using var lifetime=original!;
        bool callback=false,accepted=false;using var registration=lifetime.Token.Register(()=>
        {callback=true;r.ReplaceImage();var offered=r.Flow.Prepare(r.Version,r.Image);if(offered!=null)accepted=r.Flow.Submit(offered,true);});
        r.Flow.Clear();Assert.True(callback);Assert.False(accepted);Assert.Null(r.Flow.SelectedSnapshot);Assert.False(r.Gate.HasRunningGeneration);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));
    }

    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task ClassificationAndGenerationShareOneBudgetAndDoNotOpenASecondSecretAfterExpiry(Protocol protocol)
    {
        var clock=new PhaseClock();await using var r=await Rig.Create(protocol,auth:true,clock:clock);
        r.OnUpdate=u=>{if(u.Kind==ProductionScreenshotEvent.Generating)clock.Advance(TimeSpan.FromSeconds(20),dispatch:false);};
        Assert.True(r.Flow.Submit(r.Offer(),true));await r.Requests(1);await r.Reply(ImageClassificationTests.Json());await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.Equal(1,r.Script.Requests);Assert.Equal(1,r.SecretReads);Assert.Equal(TranslationFailureReason.RequestTimeout,Assert.Single(r.Results.Versions).Failure);Assert.DoesNotContain(r.Updates,u=>u.Kind==ProductionScreenshotEvent.Completed);
    }
    [Theory][InlineData(Protocol.OpenAIChat,1)][InlineData(Protocol.AnthropicMessages,1)][InlineData(Protocol.OpenAIChat,2)][InlineData(Protocol.AnthropicMessages,2)]
    public async Task ExpiryInsideAwaitedSecretDisposesAndErasesReturnedLeaseWithoutSendingThatStage(Protocol protocol,int phase)
    {
        var clock=new PhaseClock();await using var r=await Rig.Create(protocol,auth:true,clock:clock);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var ownedBuffers=new List<byte[]>();
        r.BeforeSecret=async read=>{if(read!=phase)return;entered.TrySetResult();await release.Task;clock.Advance(TimeSpan.FromSeconds(20),dispatch:false);};
        r.SecretCreated=(lease,cancellation)=>
        {
            // Test-only observation of the actual lease-owned array, not the constructor's copied input.
            var owned=Assert.IsType<byte[]>(typeof(Lease).GetField("bytes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(lease));
            Assert.Equal("PUBLIC_SYNTHETIC_CREDENTIAL",Encoding.UTF8.GetString(owned));ownedBuffers.Add(owned);
            Assert.False(cancellation.IsCancellationRequested); // Monotonic expiry; timer callbacks were deliberately not dispatched.
        };
        try
        {
            Assert.True(r.Flow.Submit(r.Offer(),true));
            if(phase==2){await r.Requests(1);await r.Reply(ImageClassificationTests.Json());}
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(6));Assert.False(r.Flow.Running.IsCompleted);Assert.Equal(phase,r.SecretReads);Assert.Equal(phase-1,r.Script.Requests);Assert.Equal(phase-1,ownedBuffers.Count);
            release.TrySetResult();await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));
            Assert.Equal(phase-1,r.Script.Requests);Assert.Equal(phase-1,r.Script.Bodies.Count);Assert.Equal(phase,r.SecretReads);Assert.Equal(phase,ownedBuffers.Count);
            Assert.All(ownedBuffers,bytes=>Assert.All(bytes,value=>Assert.Equal(0,value)));Assert.All(r.Leases,lease=>Assert.Throws<ObjectDisposedException>(()=>lease.HeaderValue()));
            var failed=Assert.Single(r.Results.Versions);Assert.Equal(TranslationFailureReason.RequestTimeout,failed.Failure);Assert.False(failed.Complete);Assert.True(failed.Stopped);Assert.Empty(failed.Text);
            var terminal=Assert.Single(r.Updates,u=>u.Kind==ProductionScreenshotEvent.Failed);Assert.Equal(failed.Identity,terminal.Identity);Assert.Equal("REQUEST_TIMEOUT",terminal.Reason);Assert.DoesNotContain(r.Updates,u=>u.Kind==ProductionScreenshotEvent.Completed);Assert.False(r.Gate.HasRunningGeneration);Assert.False(r.Image.IsDisposed);
        }
        finally{release.TrySetResult();}
    }
    [Fact]public async Task ClearStillJoinsAnOriginalLeaseWhenCancellationCallbackThrows()
    {
        await using var r=await Rig.Create(auth:true);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        r.BeforeSecret=async _=>{entered.TrySetResult();await release.Task;};
        try
        {
            Assert.True(r.Flow.Submit(r.Offer(),true));await entered.Task.WaitAsync(TimeSpan.FromSeconds(6));Assert.True(r.Gate.TryLinkRequestCancellation(r.Results.Identity!.Value,default,out var original));using var lifetime=original!;
            using var registration=lifetime.Token.Register(()=>throw new InvalidOperationException("PUBLIC_CANCELLATION_CALLBACK_FAULT"));var cleanup=r.Flow.ClearAsync();Assert.False(cleanup.IsCompleted);release.TrySetResult();await Assert.ThrowsAsync<AggregateException>(()=>cleanup.WaitAsync(TimeSpan.FromSeconds(6)));
            Assert.True(r.Flow.Running.IsCompleted);Assert.Equal(0,r.Script.Requests);Assert.Null(r.Flow.SelectedSnapshot);Assert.All(r.Leases,l=>Assert.Throws<ObjectDisposedException>(()=>l.HeaderValue()));
        }
        finally{release.TrySetResult();}
    }
    [Theory][InlineData("Begin")][InlineData("Prepared")]
    public async Task ReplacementFromANotificationKeepsOnlyTheNewSourceOwnerAndItsTrackedResult(string phase)
    {
        await using var r=await Rig.Create();Task? previous=null;RequestIdentity current=default;r.OnUpdate=u=>
        {if(u.Kind.ToString()!=phase)return;r.OnUpdate=null;previous=r.Flow.Running;r.ReplaceImage();Assert.True(r.Flow.Submit(r.Offer(),true));current=r.Results.Identity!.Value;};
        Assert.True(r.Flow.Submit(r.Offer(),true));await r.Requests(1);await r.Reply(ImageClassificationTests.Json());await r.Requests(2);await r.Reply("new owner output");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.NotNull(previous);await previous.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.Equal(current,r.Results.Identity);Assert.Equal(r.Image.SourceId,r.Flow.SelectedSnapshot!.SourceId);Assert.Equal(r.Version,r.Flow.SelectedSnapshot.SourceVersion);Assert.Equal("new owner output",r.Results.Text);Assert.True(r.Results.Versions.Single(v=>v.Identity==current).Complete);Assert.Equal(2,r.Script.Requests);
    }

}
