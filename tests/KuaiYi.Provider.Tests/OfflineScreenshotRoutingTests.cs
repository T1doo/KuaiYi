using System.Collections.Concurrent;
using System.Text.Json;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Imaging;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests;

public sealed class OfflineScreenshotRoutingTests
{
    private sealed class Immediate:SynchronizationContext {public override void Post(SendOrPostCallback action,object? state)=>action(state);}
    private sealed class Deferred:SynchronizationContext
    {
        private readonly ConcurrentQueue<Action> callbacks=new();public int Pending=>callbacks.Count;
        public override void Post(SendOrPostCallback action,object? state)=>callbacks.Enqueue(()=>action(state));
        public void Drain(){while(callbacks.TryDequeue(out var action))action();}
    }
    private sealed class Rig:IAsyncDisposable
    {
        public readonly PhaseHttpFixture Server=new();public PhaseHttpFixture.Script Script=null!;
        public readonly RuntimeGate Gate=new(requireProfileBinding:true);
        public SettingsSnapshot Settings=null!;public OfflineScreenshotCoordinator Flow=null!;
        public readonly List<ScreenshotGenerationUpdate> Events=[];public Action<ScreenshotGenerationUpdate>? OnUpdate;
        public OwnedSourceImage Image=null!;public byte[] Raw=[];public Guid SourceVersion;public bool Current=true;
        public Protocol Protocol;public bool Streaming;
        public static async Task<Rig> Create(Protocol protocol,bool on=true,bool stream=false,SynchronizationContext? destination=null)
        {
            var r=new Rig{Protocol=protocol,Streaming=stream};await r.Server.InitializeAsync();r.Script=r.Server.Add("runs",stream?"text/event-stream":"application/json");
            var prefix=new Uri(r.Server.BaseUri,$"runs/{Guid.NewGuid():N}/fragmented/");var boundary=DevelopmentTranslationBoundary.FromPrefix(prefix);
            r.Settings=new(new(){ActiveProfileId="image-fixture",Profiles=[new(){Id="image-fixture",Protocol=protocol==Protocol.OpenAIChat?KuaiYi.Core.Settings.ApiProtocol.OpenAIChat:KuaiYi.Core.Settings.ApiProtocol.AnthropicMessages,BaseUrl=prefix.AbsoluteUri,Model=DevelopmentTranslationBoundary.FixtureModel,Authentication=ApiAuthentication.None,Streaming=stream,Capabilities=new(){Vision=Capability.Supported},TimeoutSeconds=20}],Translation=new(){Preset=TranslationPreset.Professional}},"image-synthetic",SettingsLoadState.Current);
            r.Gate.ObserveSettingsMetadata(r.Settings);r.Gate.ConsentTo(new(prefix));if(on)Assert.True(r.Gate.TryEnable(new(prefix),true));
            r.Flow=new(r.Gate,()=>r.Settings,(version,id)=>r.Current&&version==r.SourceVersion&&id==r.Image.SourceId,boundary,destination??new Immediate(),update=>{lock(r.Events)r.Events.Add(update);r.OnUpdate?.Invoke(update);});r.Adopt();return r;
        }
        public void Adopt(int width=8,int height=8)
        {Raw=Enumerable.Range(0,width*height*4).Select(i=>(byte)(i%4==3?255:i%251+1)).ToArray();Image=new(new ScreenshotPixels(new(0,0,width,height),Raw));SourceVersion=Guid.NewGuid();Flow.AdoptScreenshot(SourceVersion,Image);}
        public async Task Requests(int count)
        {using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(Volatile.Read(ref Script.Requests)<count)await Task.Delay(5,limit.Token);}
        public async Task Reply(string text,string finish="stop")
        {
            if(!Streaming)
            {
                string json=Protocol==Protocol.OpenAIChat?JsonSerializer.Serialize(new{choices=new[]{new{index=0,message=new{content=text},finish_reason=finish}},usage=new{prompt_tokens=11,completion_tokens=7}}):JsonSerializer.Serialize(new{type="message",content=new[]{new{type="text",text}},stop_reason=finish=="stop"?"end_turn":finish,usage=new{input_tokens=11,output_tokens=7}});
                await Script.Write(json,true);return;
            }
            if(Protocol==Protocol.AnthropicMessages)await Script.Write("data: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":11,\"output_tokens\":0}}}\n\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n");
            foreach(string part in new[]{text[..(text.Length/3)],text[(text.Length/3)..(2*text.Length/3)],text[(2*text.Length/3)..]})
            {
                string json=Protocol==Protocol.OpenAIChat?JsonSerializer.Serialize(new{choices=new[]{new{index=0,delta=new{content=part}}}}):JsonSerializer.Serialize(new{type="content_block_delta",index=0,delta=new{type="text_delta",text=part}});await Script.Write("data: "+json+"\n\n");
            }
            if(Protocol==Protocol.OpenAIChat)await Script.Write("data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n",true);
            else await Script.Write("data: {\"type\":\"content_block_stop\",\"index\":0}\n\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":7}}\n\ndata: {\"type\":\"message_stop\"}\n\n",true);
        }
        public async Task Generate(string kind="text_only",string final="公开合成视觉输出",bool regenerate=false,bool reclassify=false)
        {
            int count=Script.Requests;bool classified=regenerate&&!reclassify&&Flow.SelectedSnapshot?.Classification!=null;
            Assert.True(Flow.TryStart(true,regenerate,reclassify));await Requests(count+1);
            if(!classified){await Reply(ImageClassificationTests.Json(kind));await Requests(count+2);}await Reply(final);await Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(final,Flow.Results.Text);
        }
        public async ValueTask DisposeAsync(){try{await Flow.DisposeAsync();}finally{await Server.DisposeAsync();}}
    }
    private static (string System,string User,byte[] Image) Body(Protocol protocol,string body)
    {
        using var document=JsonDocument.Parse(body);var root=document.RootElement;var content=root.GetProperty("messages")[protocol==Protocol.OpenAIChat?1:0].GetProperty("content");
        Assert.Equal(DevelopmentTranslationBoundary.FixtureModel,root.GetProperty("model").GetString());
        string system=protocol==Protocol.OpenAIChat?root.GetProperty("messages")[0].GetProperty("content").GetString()!:root.GetProperty("system").GetString()!;
        if(protocol==Protocol.OpenAIChat){Assert.Equal("text",content[0].GetProperty("type").GetString());string data=content[1].GetProperty("image_url").GetProperty("url").GetString()!;Assert.StartsWith("data:image/png;base64,",data);return(system,content[0].GetProperty("text").GetString()!,Convert.FromBase64String(data[22..]));}
        Assert.Equal("image/png",content[0].GetProperty("source").GetProperty("media_type").GetString());Assert.Equal("base64",content[0].GetProperty("source").GetProperty("type").GetString());return(system,content[1].GetProperty("text").GetString()!,Convert.FromBase64String(content[0].GetProperty("source").GetProperty("data").GetString()!));
    }
    [Theory][InlineData(Protocol.OpenAIChat,false)][InlineData(Protocol.AnthropicMessages,false)][InlineData(Protocol.OpenAIChat,true)][InlineData(Protocol.AnthropicMessages,true)]
    public async Task ActualTwoProtocolImagesClassifyPrivatelyThenGenerateWithTheSameExactCrop(Protocol protocol,bool stream)
    {
        await using var r=await Rig.Create(protocol,stream:stream);await r.Generate("diagram_or_mixed");var bodies=r.Script.Bodies.ToArray();Assert.Equal(2,bodies.Length);var classify=Body(protocol,bodies[0]);var final=Body(protocol,bodies[1]);
        Assert.Contains(PromptBuilder.Read("image-classify"),classify.System);Assert.Contains(PromptBuilder.Read("image-explain"),final.System);Assert.Equal(classify.Image,final.Image);Assert.Equal(new ImageSize(8,8),ImagePolicy.InspectPng(final.Image));
        Assert.Equal(TranslationPreset.Explain,r.Flow.SelectedSnapshot!.Plan!.Preset);Assert.Equal(TranslationPreset.Professional,r.Settings.Data.Translation.Preset);Assert.DoesNotContain(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Delta&&e.Text!.Contains("hasMeaningfulVisualStructure"));Assert.Equal(r.Image.SourceId,r.Flow.SelectedSnapshot.SourceId);Assert.Equal(r.SourceVersion,r.Flow.SelectedSnapshot.SourceVersion);
    }
    [Theory][InlineData("text_only",ImageOutputMode.TranslateText)][InlineData("visual_only",ImageOutputMode.ExplainImage)][InlineData("uncertain",ImageOutputMode.ExplainImage)]
    public async Task TextWithoutOcrStillUsesImageAndUncertainIsAnExplicitValidRoute(string kind,ImageOutputMode expected)
    {await using var r=await Rig.Create(Protocol.OpenAIChat);await r.Generate(kind);var plan=r.Flow.SelectedSnapshot!.Plan!;Assert.Equal(expected,plan.Mode);Assert.NotEmpty(Body(r.Protocol,r.Script.Bodies.Last()).Image);if(kind=="uncertain")Assert.Equal("图像结构判断不确定",plan.Notice);if(kind=="visual_only")Assert.Equal("未发现足够可翻译文字",plan.Notice);}
    [Theory][InlineData("{}","stop","CLASSIFICATION_INVALID")][InlineData("{\"kind\":","stop","CLASSIFICATION_INVALID")][InlineData("valid","length","OUTPUT_TRUNCATED")][InlineData("oversize","stop","CLASSIFICATION_TOO_LARGE")][InlineData("unreadable","stop","CLEARER_IMAGE_REQUIRED")]
    public async Task InvalidIncompleteOrUnreadableClassificationNeverSendsSecondRequest(string source,string finish,string expected)
    {
        await using var r=await Rig.Create(Protocol.OpenAIChat);Assert.True(r.Flow.TryStart(true));await r.Requests(1);string text=source switch{"valid"=>ImageClassificationTests.Json(),"oversize"=>new string('x',8193),"unreadable"=>ImageClassificationTests.Json(readability:"unreadable"),_=>source};await r.Reply(text,finish);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(1,r.Script.Requests);Assert.Empty(r.Flow.Results.Text);Assert.Equal(expected,Assert.Single(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Failed).Reason);Assert.DoesNotContain(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Generating);Assert.False(r.Gate.HasRunningGeneration);
    }
    [Theory][InlineData("off")][InlineData("automatic")][InlineData("unknown-vision")][InlineData("unsupported-vision")][InlineData("remote")][InlineData("credential")][InlineData("source")][InlineData("suspended")][InlineData("hidden")]
    public async Task AdoptionNeverSendsAndStartRequiresExplicitOnSourceVisionAndImmutableFixtureAuthority(string condition)
    {
        await using var r=await Rig.Create(Protocol.OpenAIChat,on:condition!="off");
        if(condition is "unknown-vision" or "unsupported-vision" or "remote" or "credential")
        {var p=r.Settings.Data.Profiles[0];p=condition switch{"unknown-vision"=>p with{Capabilities=new(){Vision=Capability.Unknown}},"unsupported-vision"=>p with{Capabilities=new(){Vision=Capability.Unsupported}},"remote"=>p with{BaseUrl="https://public.invalid/"},_=>p with{Authentication=ApiAuthentication.Bearer,SecretRef="synthetic-reference"}};r.Settings=r.Settings with{Data=r.Settings.Data with{Profiles=[p]}};r.Gate.ObserveSettingsMetadata(r.Settings);}
        if(condition=="source")r.Current=false;if(condition=="suspended")r.Gate.SetSuspension(Suspension.Settings,true);if(condition=="hidden")r.Gate.Disable(hide:true);
        Assert.Empty(r.Script.Bodies);Assert.False(r.Flow.TryStart(condition!="automatic"));Assert.Equal(0,r.Script.Requests);Assert.Empty(r.Events);
    }
    [Fact]public async Task SessionOverrideAndRegenerationReuseClassificationAndRetainOnlyThreeVersions()
    {
        await using var r=await Rig.Create(Protocol.AnthropicMessages);await r.Generate("diagram_or_mixed",final:"version1");var classification=r.Flow.SelectedSnapshot!.Classification;
        Assert.True(r.Flow.SetMode(ImageOutputMode.TranslateText));await r.Generate(final:"version2",regenerate:true);Assert.Same(classification,r.Flow.SelectedSnapshot!.Classification);Assert.Equal(ImageOutputMode.TranslateText,r.Flow.SelectedSnapshot.Plan!.Mode);Assert.Equal(3,r.Script.Requests);
        await r.Generate(final:"version3",regenerate:true);await r.Generate(final:"version4",regenerate:true);Assert.Equal(3,r.Flow.Results.Versions.Count);Assert.Equal(new[]{2,3,4},r.Flow.Results.Versions.Select(v=>v.Number));Assert.Equal(5,r.Script.Requests);
        Assert.True(r.Flow.Results.SelectVersion(r.Flow.Results.Versions[0].Identity.VersionId));Assert.Equal("version2",r.Flow.Results.Text);await r.Generate("text_only","version5",regenerate:true,reclassify:true);Assert.Equal(7,r.Script.Requests);Assert.NotSame(classification,r.Flow.SelectedSnapshot!.Classification);
        var old=r.Raw;r.Adopt();Assert.All(old,b=>Assert.Equal(0,b));Assert.Equal(ImageOutputMode.Automatic,r.Flow.Mode);Assert.Empty(r.Flow.Results.Versions);Assert.Equal(TranslationPreset.Professional,r.Settings.Data.Translation.Preset);
    }
    [Theory][InlineData("stop")][InlineData("clear")][InlineData("lock")][InlineData("hide")][InlineData("replace")][InlineData("source")][InlineData("off")][InlineData("settings")][InlineData("binding")]
    public async Task ClassifierToFinalBoundaryRejectsCancelledChangedOrUnownedSource(string action)
    {
        await using var r=await Rig.Create(Protocol.OpenAIChat);var raw=r.Raw;r.OnUpdate=u=>
        {
            if(u.Kind!=ScreenshotGenerationEvent.Generating)return;
            switch(action){case "stop":r.Flow.Stop();break;case "clear":r.Flow.Clear();break;case "lock":r.Flow.LockSession();break;case "hide":r.Flow.Hide();break;case "replace":r.Adopt();break;case "source":r.Current=false;break;case "off":r.Gate.Disable();break;case "settings":r.Gate.SetSuspension(Suspension.Settings,true);break;case "binding":r.Settings=r.Settings with{Data=r.Settings.Data with{Profiles=[]}};r.Gate.ObserveSettingsMetadata(r.Settings);break;}
        };
        Assert.True(r.Flow.TryStart(true));await r.Requests(1);await r.Reply(ImageClassificationTests.Json());await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(1,r.Script.Requests);Assert.DoesNotContain(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Delta);Assert.False(r.Gate.HasRunningGeneration);if(action is "clear" or "lock" or "hide" or "replace")Assert.All(raw,b=>Assert.Equal(0,b));
    }
    [Fact]public async Task QueuedClassifierFromAAfterReplacementCannotCreateBOrAFinalRequest()
    {
        var destination=new Deferred();await using var r=await Rig.Create(Protocol.OpenAIChat,destination:destination);Assert.True(r.Flow.TryStart(true));
        using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(destination.Pending==0)await Task.Delay(5,limit.Token);destination.Drain();await r.Requests(1);await r.Reply(ImageClassificationTests.Json("diagram_or_mixed"));while(destination.Pending==0)await Task.Delay(5,limit.Token);
        var oldRaw=r.Raw;Task old=r.Flow.Running;r.Adopt();destination.Drain();await old.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(1,r.Script.Requests);Assert.Empty(r.Flow.Results.Versions);Assert.DoesNotContain(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Generating);Assert.All(oldRaw,b=>Assert.Equal(0,b));
    }
    [Fact]public async Task ExitDuringClassificationClearsOwnerAndWaitsOnlyForOwnedWork()
    {
        var r=await Rig.Create(Protocol.AnthropicMessages);Assert.True(r.Flow.TryStart(true));await r.Requests(1);await r.Flow.DisposeAsync();Assert.All(r.Raw,b=>Assert.Equal(0,b));Assert.True(r.Flow.Running.IsCompleted);Assert.Empty(r.Flow.Results.Versions);Assert.False(r.Flow.TryStart(true));await r.Server.DisposeAsync();
    }
    [Fact]public async Task OversizeScreenshotRequiresSmallerCropBeforeAnyHttpInsteadOfSendingPreview()
    {await using var r=await Rig.Create(Protocol.OpenAIChat);r.Adopt(2561,8);Assert.True(r.Flow.TryStart(true));await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(0,r.Script.Requests);Assert.Equal(ScreenshotGenerationEvent.Failed,Assert.Single(r.Events).Kind);Assert.False(r.Image.IsDisposed);}
    [Fact]public async Task ThrowingOldCancellationCannotLeaveAnUnownedNewRequestAcrossClearOrDispose()
    {
        await using var r=await Rig.Create(Protocol.OpenAIChat);Assert.True(r.Flow.TryStart(true));await r.Requests(1);var old=r.Flow.Results.Identity!.Value;
        Assert.True(r.Gate.TryLinkRequestCancellation(old,default,out var linked));using var lifetime=linked!;using var callback=lifetime.Token.Register(()=>throw new IOException("Synthetic old cancellation failure"));
        try
        {
            var failure=Assert.ThrowsAny<Exception>(()=>r.Flow.TryStart(true));Assert.Contains("Synthetic old cancellation failure",failure.ToString());Assert.False(r.Gate.HasRunningGeneration);
            await r.Flow.ClearAsync();Assert.False(r.Gate.HasRunningGeneration);Assert.All(r.Raw,b=>Assert.Equal(0,b));Assert.Equal(1,r.Script.Requests);Assert.Empty(r.Flow.Results.Versions);
        }
        finally{r.Gate.Disable();} // Only this fixture's known gate; never an external process or lock.
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task CompleteClassificationJsonWithoutProtocolTerminalCannotAuthorizeFinalGeneration(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol,stream:true);Assert.True(r.Flow.TryStart(true));await r.Requests(1);string classification=ImageClassificationTests.Json();string text=JsonSerializer.Serialize(classification);
        string events=protocol==Protocol.OpenAIChat?"data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":"+text+"}}]}\n\n":"data: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":11}}}\n\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":"+text+"}}\n\ndata: {\"type\":\"content_block_stop\",\"index\":0}\n\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"}}\n\n";
        await r.Script.Write(events,true);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(1,r.Script.Requests);Assert.DoesNotContain(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Generating);Assert.Empty(r.Flow.Results.Text);Assert.Null(r.Flow.SelectedSnapshot!.Classification);Assert.Equal("STREAM_INTERRUPTED",Assert.Single(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Failed).Reason);
    }
    [Theory][InlineData("clear")][InlineData("lock")][InlineData("hide")][InlineData("exit")]
    public async Task CancellationFailureStillClearsOwnedPixelsAndPreservesFailureForCaller(string action)
    {
        await using var r=await Rig.Create(Protocol.OpenAIChat);Assert.True(r.Flow.TryStart(true));await r.Requests(1);Assert.True(r.Gate.TryLinkRequestCancellation(r.Flow.Results.Identity!.Value,default,out var linked));using var lifetime=linked!;using var callback=lifetime.Token.Register(()=>throw new IOException("Synthetic cleanup cancellation failure"));
        Exception error=action=="exit"?await Assert.ThrowsAnyAsync<Exception>(async()=>await r.Flow.DisposeAsync()):Assert.ThrowsAny<Exception>(()=>{if(action=="clear")r.Flow.Clear();else if(action=="lock")r.Flow.LockSession();else r.Flow.Hide();});
        Assert.Contains("Synthetic cleanup cancellation failure",error.ToString());await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.All(r.Raw,b=>Assert.Equal(0,b));Assert.False(r.Gate.HasRunningGeneration);Assert.Empty(r.Flow.Results.Versions);Assert.Equal(1,r.Script.Requests);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task StopAtFirstFinalDeltaRetainsTextAndReusesClassificationOnlyOnANewExplicitAction(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol);r.OnUpdate=u=>{if(u.Kind==ScreenshotGenerationEvent.Delta)r.Flow.Stop();};Assert.True(r.Flow.TryStart(true));await r.Requests(1);await r.Reply(ImageClassificationTests.Json());await r.Requests(2);await r.Reply("accepted partial");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.Equal("accepted partial",r.Flow.Results.Text);var old=Assert.Single(r.Flow.Results.Versions);Assert.True(old.Stopped);Assert.False(old.Complete);Assert.False(r.Image.IsDisposed);Assert.DoesNotContain(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Completed);Assert.Equal(2,r.Script.Requests);
        r.OnUpdate=null;await r.Generate(final:"regenerated",regenerate:true);Assert.Equal(3,r.Script.Requests);Assert.True(r.Flow.Results.Versions.Single(v=>v.Identity==old.Identity).Stopped);
    }
    [Fact]public async Task QueuedFinalEventsCannotRestoreOutputAfterClear()
    {
        var destination=new Deferred();await using var r=await Rig.Create(Protocol.AnthropicMessages,destination:destination);Assert.True(r.Flow.TryStart(true));
        async Task PumpUntil(Func<bool> ready){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(!ready()){destination.Drain();await Task.Delay(5,timeout.Token);}}
        await PumpUntil(()=>r.Script.Requests==1);await r.Reply(ImageClassificationTests.Json());await PumpUntil(()=>r.Script.Requests==2);await r.Reply("late final");
        using var queued=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(destination.Pending==0)await Task.Delay(5,queued.Token);
        Task old=r.Flow.Running;r.Flow.Clear();destination.Drain();await old.WaitAsync(TimeSpan.FromSeconds(6));Assert.Empty(r.Flow.Results.Versions);Assert.DoesNotContain(r.Events,e=>e.Kind is ScreenshotGenerationEvent.Delta or ScreenshotGenerationEvent.Completed);Assert.All(r.Raw,b=>Assert.Equal(0,b));
    }
    [Fact]public async Task ATransferredScreenshotCannotBeAdoptedTwiceAndDisposedInputCannotReplaceCurrentSource()
    {
        await using var r=await Rig.Create(Protocol.OpenAIChat);Assert.Throws<InvalidOperationException>(()=>r.Flow.AdoptScreenshot(r.SourceVersion,r.Image));Assert.False(r.Image.IsDisposed);var disposed=new OwnedSourceImage(new(new(0,0,8,8),new byte[256]));disposed.Dispose();Assert.Throws<InvalidOperationException>(()=>r.Flow.AdoptScreenshot(Guid.NewGuid(),disposed));Assert.False(r.Image.IsDisposed);Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData("clear")][InlineData("stop")][InlineData("adopt")][InlineData("dispose")]
    public async Task NonThrowingCancellationReentryCannotRepublishTheRejectedAdmission(string action)
    {
        await using var r=await Rig.Create(Protocol.OpenAIChat);Assert.True(r.Flow.TryStart(true));await r.Requests(1);var a=r.Flow.Results.Identity!.Value;var original=r.Image;var originalRaw=r.Raw;Task aTask=r.Flow.Running;Task? closing=null;
        Assert.True(r.Gate.TryLinkRequestCancellation(a,default,out var linked));using var lifetime=linked!;using var callback=lifetime.Token.Register(()=>
        {switch(action){case "clear":r.Flow.Clear();break;case "stop":r.Flow.Stop();break;case "adopt":r.Adopt();break;case "dispose":closing=r.Flow.DisposeAsync().AsTask();break;}});
        try
        {
            Assert.False(r.Flow.TryStart(true));await aTask.WaitAsync(TimeSpan.FromSeconds(6));if(closing!=null)await closing.WaitAsync(TimeSpan.FromSeconds(6));
            Assert.False(r.Gate.HasRunningGeneration);Assert.Equal(1,r.Script.Requests);Assert.DoesNotContain(r.Events,e=>e.Identity!=a);
            if(action=="stop"){Assert.Equal(a,Assert.Single(r.Flow.Results.Versions).Identity);Assert.True(r.Flow.Results.Versions[0].Stopped);Assert.False(original.IsDisposed);}
            else {Assert.Empty(r.Flow.Results.Versions);Assert.Null(r.Flow.SelectedSnapshot);Assert.True(original.IsDisposed);Assert.All(originalRaw,b=>Assert.Equal(0,b));}
            if(action=="adopt"){Assert.False(r.Image.IsDisposed);Assert.NotSame(original,r.Image);Assert.Equal(ImageOutputMode.Automatic,r.Flow.Mode);}
        }
        finally{if(closing!=null)await closing.WaitAsync(TimeSpan.FromSeconds(6));}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CancellationReentryStartingCLeavesCIntactWhenOuterBIsRejected(bool replaceSource)
    {
        var destination=new Deferred();await using var r=await Rig.Create(Protocol.OpenAIChat,destination:destination);
        async Task PumpUntil(Func<bool> ready){using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(!ready()){destination.Drain();await Task.Delay(5,limit.Token);}}
        Assert.True(r.Flow.TryStart(true));await PumpUntil(()=>r.Script.Requests==1);var a=r.Flow.Results.Identity!.Value;RequestIdentity c=default;
        Assert.True(r.Gate.TryLinkRequestCancellation(a,default,out var linked));using var lifetime=linked!;using var callback=lifetime.Token.Register(()=>
        {if(replaceSource)r.Adopt();Assert.True(r.Flow.TryStart(true));c=r.Flow.Results.Identity!.Value;});
        Assert.False(r.Flow.TryStart(true));Assert.NotEqual(default,c);Assert.NotEqual(a,c);Assert.Equal(c,r.Flow.Results.Identity);Assert.False(r.Flow.Results.Versions.Single(v=>v.Identity==c).Stopped);
        await PumpUntil(()=>r.Script.Requests>=2||r.Flow.Running.IsCompleted);Assert.Equal(2,r.Script.Requests);Assert.True(r.Gate.CanApply(c));Assert.False(r.Image.IsDisposed);
        await r.Reply(ImageClassificationTests.Json());await PumpUntil(()=>r.Script.Requests==3);await r.Reply("C public output");await PumpUntil(()=>r.Flow.Running.IsCompleted);await r.Flow.Running;
        Assert.Equal("C public output",r.Flow.Results.Text);Assert.Equal(c.VersionId,r.Flow.Results.SelectedVersionId);Assert.Equal(r.Image.SourceId,r.Flow.SelectedSnapshot!.SourceId);Assert.Equal(3,r.Script.Requests);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task ActualClassificationWireDefinesTheStrictParserContract(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol);Assert.True(r.Flow.TryStart(true));await r.Requests(1);var sent=Body(protocol,Assert.Single(r.Script.Bodies));
        foreach(string required in new[]{"clear / partly_unclear / unreadable","布尔","字符串","8192","512","160","32","12","有限","未知字段","重复字段"})Assert.Contains(required,sent.System);
        const string marker="格式示例（不能照抄判定，必须按本图实际内容选择）：";int index=sent.System.IndexOf(marker,StringComparison.Ordinal);Assert.True(index>=0);string example=sent.System[(index+marker.Length)..].Trim();Assert.True(ImageClassification.TryParse(example,out var sample));Assert.NotNull(sample);
        await r.Reply(example);await r.Requests(2);await r.Reply("contract public output");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal("contract public output",r.Flow.Results.Text);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task CompleteButInvalidReadabilityContractStillHasZeroFinalRequests(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol);Assert.True(r.Flow.TryStart(true));await r.Requests(1);await r.Reply(ImageClassificationTests.Json(readability:"readable"));await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(1,r.Script.Requests);Assert.Equal("CLASSIFICATION_INVALID",Assert.Single(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Failed).Reason);Assert.DoesNotContain(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Generating);Assert.Empty(r.Flow.Results.Text);
    }
    [Theory][InlineData("settings")][InlineData("capture-roundtrip")][InlineData("mode")][InlineData("profile")][InlineData("source")][InlineData("lock")][InlineData("hide")][InlineData("owner-disposed")]
    public async Task AdmissionRechecksEpochSettingsBindingModeAndSourceAfterCancellationCallbacks(string action)
    {
        await using var r=await Rig.Create(Protocol.OpenAIChat);Assert.True(r.Flow.TryStart(true));await r.Requests(1);var a=r.Flow.Results.Identity!.Value;Task old=r.Flow.Running;
        Assert.True(r.Gate.TryLinkRequestCancellation(a,default,out var linked));using var lifetime=linked!;using var callback=lifetime.Token.Register(()=>
        {
            switch(action)
            {
                case "settings":r.Settings=r.Settings with{Version="changed-during-admission"};break;
                case "capture-roundtrip":r.Gate.SetSuspension(Suspension.Capture,true);r.Gate.SetSuspension(Suspension.Capture,false);break;
                case "mode":Assert.True(r.Flow.SetMode(ImageOutputMode.ExplainImage));break;
                case "profile":var p=r.Settings.Data.Profiles[0] with{Capabilities=new(){Vision=Capability.Unknown}};r.Settings=r.Settings with{Data=r.Settings.Data with{Profiles=[p]}};r.Gate.ObserveSettingsMetadata(r.Settings);break;
                case "source":r.Current=false;break;
                case "lock":r.Flow.LockSession();break;
                case "hide":r.Flow.Hide();break;
                case "owner-disposed":r.Image.Dispose();break;
            }
        });
        Assert.False(r.Flow.TryStart(true));await old.WaitAsync(TimeSpan.FromSeconds(6));Assert.False(r.Gate.HasRunningGeneration);Assert.Equal(1,r.Script.Requests);Assert.All(r.Flow.Results.Versions,v=>Assert.Equal(a,v.Identity));Assert.DoesNotContain(r.Events,e=>e.Identity!=a);
        if(action=="mode")Assert.Equal(ImageOutputMode.ExplainImage,r.Flow.Mode);if(action is "lock" or "hide")Assert.Empty(r.Flow.Results.Versions);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task NonMutatingCancellationCallbackStillAllowsTheExplicitNewGeneration(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol);Assert.True(r.Flow.TryStart(true));await r.Requests(1);var a=r.Flow.Results.Identity!.Value;Task old=r.Flow.Running;int callbacks=0;
        Assert.True(r.Gate.TryLinkRequestCancellation(a,default,out var linked));using var lifetime=linked!;using var callback=lifetime.Token.Register(()=>callbacks++);
        Assert.True(r.Flow.TryStart(true));var b=r.Flow.Results.Identity!.Value;Assert.NotEqual(a,b);Assert.Equal(1,callbacks);await old.WaitAsync(TimeSpan.FromSeconds(6));await r.Requests(2);await r.Reply(ImageClassificationTests.Json());await r.Requests(3);await r.Reply("B public output");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal("B public output",r.Flow.Results.Text);Assert.Equal(b.VersionId,r.Flow.Results.SelectedVersionId);Assert.False(r.Image.IsDisposed);
    }
    [Theory][InlineData(Protocol.OpenAIChat,"mode")][InlineData(Protocol.AnthropicMessages,"mode")][InlineData(Protocol.OpenAIChat,"settings")][InlineData(Protocol.AnthropicMessages,"settings")]
    public async Task RejectedAdmissionMarksOnlyItsCancelledPartialPredecessorStopped(Protocol protocol,string action)
    {
        await using var r=await Rig.Create(protocol,stream:true);Assert.True(r.Flow.TryStart(true));await r.Requests(1);await r.Reply(ImageClassificationTests.Json());await r.Requests(2);
        string delta=protocol==Protocol.OpenAIChat?"data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"partial A\"}}]}\n\n":"data: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":11}}}\n\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"partial A\"}}\n\n";
        await r.Script.Write(delta);using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(r.Flow.Results.Text!="partial A")await Task.Delay(5,limit.Token);
        var a=r.Flow.Results.Identity!.Value;var classification=r.Flow.SelectedSnapshot!.Classification;Task old=r.Flow.Running;Assert.False(Assert.Single(r.Flow.Results.Versions).Stopped);
        Assert.True(r.Gate.TryLinkRequestCancellation(a,default,out var linked));using var lifetime=linked!;using var callback=lifetime.Token.Register(()=>{if(action=="mode")Assert.True(r.Flow.SetMode(ImageOutputMode.ExplainImage));else r.Settings=r.Settings with{Version="partial-admission-change"};});
        Assert.False(r.Flow.TryStart(true));await old.WaitAsync(TimeSpan.FromSeconds(6));var retained=Assert.Single(r.Flow.Results.Versions);Assert.Equal(a,retained.Identity);Assert.Equal("partial A",retained.Text);Assert.True(retained.Stopped);Assert.False(retained.Complete);Assert.False(r.Gate.HasRunningGeneration);Assert.Equal(2,r.Script.Requests);Assert.False(r.Image.IsDisposed);Assert.Same(classification,r.Flow.SelectedSnapshot!.Classification);
    }
}
