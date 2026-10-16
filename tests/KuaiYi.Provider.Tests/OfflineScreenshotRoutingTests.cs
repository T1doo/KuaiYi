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
        {AdoptRaw(new(0,0,width,height),Enumerable.Range(0,width*height*4).Select(i=>(byte)(i%4==3?255:i%251+1)).ToArray());}
        public void AdoptRaw(PixelRect bounds,byte[] raw)
        {Raw=raw;Image=new(new ScreenshotPixels(bounds,raw));SourceVersion=Guid.NewGuid();Flow.AdoptScreenshot(SourceVersion,Image);}
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
    private static async Task UsageReply(Rig r,string text,string? usage,bool stream=false,bool terminal=true,string finish="stop")
    {
        string field=usage==null?"":",\"usage\":"+usage;string quoted=JsonSerializer.Serialize(text);
        if(!stream)
        {
            string body=r.Protocol==Protocol.OpenAIChat?"{\"choices\":[{\"index\":0,\"message\":{\"content\":"+quoted+"},\"finish_reason\":\""+finish+"\"}]"+field+"}":"{\"type\":\"message\",\"content\":[{\"type\":\"text\",\"text\":"+quoted+"}],\"stop_reason\":\""+(finish=="stop"?"end_turn":finish)+"\""+field+"}";
            await r.Script.Write(body,true);return;
        }
        string events=r.Protocol==Protocol.OpenAIChat?"data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":"+quoted+"}}]}\n\ndata: {\"choices\":[]"+field+"}\n\n":
            "data: {\"type\":\"message_start\",\"message\":{}}\n\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":"+quoted+"}}\n\ndata: {\"type\":\"content_block_stop\",\"index\":0}\n\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"}"+field+"}\n\n";
        if(terminal)events+=r.Protocol==Protocol.OpenAIChat?"data: [DONE]\n\n":"data: {\"type\":\"message_stop\"}\n\n";
        await r.Script.Write(events,true);
    }
    private static string Counts(Protocol protocol,long input,long output)=>protocol==Protocol.OpenAIChat?JsonSerializer.Serialize(new{prompt_tokens=input,completion_tokens=output}):JsonSerializer.Serialize(new{input_tokens=input,output_tokens=output});
    [Theory][InlineData(Protocol.OpenAIChat,false)][InlineData(Protocol.AnthropicMessages,false)][InlineData(Protocol.OpenAIChat,true)][InlineData(Protocol.AnthropicMessages,true)]
    public async Task ActualStageUsageReceiptsBindDistinctInvocationsToTheExactImageAndVersion(Protocol protocol,bool stream)
    {
        await using var r=await Rig.Create(protocol,stream:stream);Assert.True(r.Flow.TryStart(true));var identity=r.Flow.Results.Identity!.Value;await r.Requests(1);await UsageReply(r,ImageClassificationTests.Json(),Counts(protocol,13,3),stream);await r.Requests(2);await UsageReply(r,"usage public output",Counts(protocol,29,17),stream);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));
        var snapshot=r.Flow.SelectedSnapshot!;var classify=Assert.IsType<ScreenshotUsageReceipt>(snapshot.ClassificationUsage);var final=Assert.IsType<ScreenshotUsageReceipt>(snapshot.GenerationUsage);
        Assert.NotEqual(classify.InvocationId,final.InvocationId);Assert.NotEqual(Guid.Empty,classify.InvocationId);Assert.False(snapshot.ClassificationUsageReused);Assert.Equal(ScreenshotUsageStage.Classification,classify.Stage);Assert.Equal(ScreenshotUsageStage.Generation,final.Stage);
        foreach(var receipt in new[]{classify,final}){Assert.Equal(identity,receipt.Identity);Assert.Equal(r.SourceVersion,receipt.SourceVersion);Assert.Equal(r.Image.SourceId,receipt.SourceId);Assert.Equal(protocol,receipt.Protocol);Assert.True(receipt.Reported);}
        Assert.Equal(new TokenUsage(13,3),classify.Tokens);Assert.Equal(new TokenUsage(29,17),final.Tokens);Assert.Equal("usage public output",r.Flow.Results.Text);Assert.True(Assert.Single(r.Flow.Results.Versions).Complete);Assert.Equal(2,r.Events.Count(e=>e.Kind==ScreenshotGenerationEvent.Usage));
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
    public static IEnumerable<object[]> UnknownUsageCases()
    {foreach(var protocol in Enum.GetValues<Protocol>())foreach(string form in new[]{"missing","input","output","invalid"})yield return [protocol,form];}
    [Theory][MemberData(nameof(UnknownUsageCases))]
    public async Task MissingOrPartialUsageHasUnknownFieldsWithoutAffectingCompletedOutput(Protocol protocol,string form)
    {
        await using var r=await Rig.Create(protocol);Assert.True(r.Flow.TryStart(true));await r.Requests(1);await UsageReply(r,ImageClassificationTests.Json(),null);await r.Requests(2);
        string input=protocol==Protocol.OpenAIChat?"prompt_tokens":"input_tokens",output=protocol==Protocol.OpenAIChat?"completion_tokens":"output_tokens";
        string? usage=form switch{"input"=>"{\""+input+"\":0}","output"=>"{\""+output+"\":9}","invalid"=>"{\""+input+"\":\"12\",\""+output+"\":-1}",_=>null};
        await UsageReply(r,"output with unknown usage",usage);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));var snapshot=r.Flow.SelectedSnapshot!;
        Assert.Equal(new TokenUsage(null,null),snapshot.ClassificationUsage!.Tokens);Assert.False(snapshot.ClassificationUsage.Reported);Assert.Equal(new TokenUsage(form=="input"?0:null,form=="output"?9:null),snapshot.GenerationUsage!.Tokens);Assert.Equal(form is "input" or "output",snapshot.GenerationUsage.Reported);Assert.True(Assert.Single(r.Flow.Results.Versions).Complete);Assert.Equal("output with unknown usage",r.Flow.Results.Text);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task ThreeVersionRegenerationReferencesOriginalClassificationReceiptWithoutRecounting(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol);await r.Generate(final:"v1");var original=r.Flow.SelectedSnapshot!.ClassificationUsage!;var invocations=new HashSet<Guid>{r.Flow.SelectedSnapshot.GenerationUsage!.InvocationId};
        for(int i=2;i<=4;i++)
        {await r.Generate(final:"v"+i,regenerate:true);var snapshot=r.Flow.SelectedSnapshot!;Assert.True(snapshot.ClassificationUsageReused);Assert.Same(original,snapshot.ClassificationUsage);Assert.NotEqual(snapshot.ClassificationUsage!.Identity,snapshot.GenerationUsage!.Identity);Assert.Equal(snapshot.GenerationUsage.Identity,r.Flow.Results.Identity);Assert.True(invocations.Add(snapshot.GenerationUsage.InvocationId));}
        Assert.Equal(5,r.Script.Requests);Assert.Equal(new[]{2,3,4},r.Flow.Results.Versions.Select(v=>v.Number));Assert.True(r.Flow.Results.SelectVersion(r.Flow.Results.Versions[0].Identity.VersionId));Assert.Same(original,r.Flow.SelectedSnapshot!.ClassificationUsage);
        Assert.True(r.Flow.SetMode(ImageOutputMode.TranslateText));await r.Generate(final:"v5",regenerate:true);Assert.Equal(6,r.Script.Requests);Assert.Same(original,r.Flow.SelectedSnapshot!.ClassificationUsage);Assert.Equal(ImageOutputMode.TranslateText,r.Flow.SelectedSnapshot.Plan!.Mode);
        await r.Generate(final:"v6",regenerate:true,reclassify:true);Assert.Equal(8,r.Script.Requests);Assert.False(r.Flow.SelectedSnapshot!.ClassificationUsageReused);Assert.NotEqual(original.InvocationId,r.Flow.SelectedSnapshot.ClassificationUsage!.InvocationId);Assert.Equal(r.Flow.Results.Identity,r.Flow.SelectedSnapshot.ClassificationUsage.Identity);
    }
    private static string UsageFrame(Protocol protocol,string usage)=>protocol==Protocol.OpenAIChat?"data: {\"choices\":[],\"usage\":"+usage+"}\n\n":"data: {\"type\":\"message_delta\",\"usage\":"+usage+"}\n\n";
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task RepeatedAndCorrectedReportsReplaceEachFieldWithoutSummingOrInferringCompletion(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol,stream:true);Assert.True(r.Flow.TryStart(true));await r.Requests(1);await UsageReply(r,ImageClassificationTests.Json(),null,true);await r.Requests(2);
        string start=protocol==Protocol.OpenAIChat?"":"data: {\"type\":\"message_start\",\"message\":{}}\n\n";string output=protocol==Protocol.OpenAIChat?"completion_tokens":"output_tokens";
        await r.Script.Write(start+UsageFrame(protocol,Counts(protocol,11,8))+UsageFrame(protocol,Counts(protocol,11,8))+UsageFrame(protocol,Counts(protocol,5,3))+UsageFrame(protocol,"{\""+output+"\":7}"));
        using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(true){bool reported;lock(r.Events)reported=r.Events.Count(e=>e.Kind==ScreenshotGenerationEvent.Usage)==3;if(reported)break;await Task.Delay(5,limit.Token);}
        Assert.Equal(new TokenUsage(5,7),r.Flow.SelectedSnapshot!.GenerationUsage!.Tokens);Assert.Empty(r.Flow.Results.Text);Assert.False(Assert.Single(r.Flow.Results.Versions).Complete);Assert.True(r.Gate.HasRunningGeneration);Assert.Equal(3,r.Events.Count(e=>e.Kind==ScreenshotGenerationEvent.Usage));var frozen=r.Flow.SelectedSnapshot;
        string ending=protocol==Protocol.OpenAIChat?"data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"corrected output\"}}]}\n\ndata: [DONE]\n\n":"data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"corrected output\"}}\n\ndata: {\"type\":\"content_block_stop\",\"index\":0}\n\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"}}\n\ndata: {\"type\":\"message_stop\"}\n\n";
        await r.Script.Write(ending,true);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(new TokenUsage(5,7),r.Flow.SelectedSnapshot!.GenerationUsage!.Tokens);Assert.Equal(new TokenUsage(5,7),frozen!.GenerationUsage!.Tokens);Assert.True(Assert.Single(r.Flow.Results.Versions).Complete);
    }
    public static IEnumerable<object[]> UsageFailures()
    {foreach(var p in Enum.GetValues<Protocol>())foreach(bool generation in new[]{false,true})foreach(bool interrupted in new[]{false,true})yield return [p,generation,interrupted];}
    [Theory][MemberData(nameof(UsageFailures))]
    public async Task UsageSurvivesFailedOrInterruptedStagesWithoutCompletingOrCachingInvalidClassification(Protocol protocol,bool generation,bool interrupted)
    {
        await using var r=await Rig.Create(protocol,stream:interrupted);Assert.True(r.Flow.TryStart(true));await r.Requests(1);if(generation){await UsageReply(r,ImageClassificationTests.Json(),Counts(protocol,2,1),interrupted);await r.Requests(2);}
        await UsageReply(r,generation?"partial public body":ImageClassificationTests.Json(),Counts(protocol,31,8),interrupted,terminal:!interrupted,finish:protocol==Protocol.OpenAIChat?"length":"max_tokens");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));var snapshot=r.Flow.SelectedSnapshot!;
        Assert.Equal(new TokenUsage(31,8),(generation?snapshot.GenerationUsage:snapshot.ClassificationUsage)!.Tokens);Assert.True(Assert.Single(r.Flow.Results.Versions).Stopped);Assert.False(Assert.Single(r.Flow.Results.Versions).Complete);Assert.Equal(generation?2:1,r.Script.Requests);Assert.DoesNotContain(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Completed);
        if(!generation){Assert.Null(snapshot.Classification);Assert.Null(snapshot.GenerationUsage);Assert.Null(snapshot.Plan);}
    }
    public static IEnumerable<object[]> UsageCancellationActions()
    {foreach(var p in Enum.GetValues<Protocol>())foreach(bool generation in new[]{false,true})foreach(string action in new[]{"stop","clear","adopt","lock","hide"})yield return [p,generation,action];}
    [Theory][MemberData(nameof(UsageCancellationActions))]
    public async Task UsageDeliveryCancellationCannotResumeBodyCacheOrTheNextStage(Protocol protocol,bool generation,string action)
    {
        await using var r=await Rig.Create(protocol);bool acted=false;ScreenshotGenerationSnapshot? accepted=null;r.OnUpdate=u=>
        {
            if(u.Kind!=ScreenshotGenerationEvent.Usage||(u.Snapshot.GenerationUsage!=null)!=generation)return;acted=true;accepted=u.Snapshot;r.OnUpdate=null;
            switch(action){case "stop":r.Flow.Stop();break;case "clear":r.Flow.Clear();break;case "adopt":r.Adopt();break;case "lock":r.Flow.LockSession();break;case "hide":r.Flow.Hide();break;}
        };
        Assert.True(r.Flow.TryStart(true));Task running=r.Flow.Running;await r.Requests(1);await UsageReply(r,ImageClassificationTests.Json(),Counts(protocol,7,2));if(generation){await r.Requests(2);await UsageReply(r,"partial cancelled output",Counts(protocol,21,6));}await running.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.True(acted);Assert.NotNull(accepted);Assert.False(r.Gate.HasRunningGeneration);Assert.Equal(generation?2:1,r.Script.Requests);Assert.DoesNotContain(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Completed);
        if(action=="stop"){Assert.Equal(accepted,r.Flow.SelectedSnapshot);Assert.True(Assert.Single(r.Flow.Results.Versions).Stopped);if(!generation)Assert.Null(r.Flow.SelectedSnapshot!.Classification);}
        else {Assert.Empty(r.Flow.Results.Versions);Assert.Null(r.Flow.SelectedSnapshot);}
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task UsageModeChangeLeavesCurrentPlanFrozenAndOnlyAffectsTheNextExplicitGeneration(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol);r.OnUpdate=u=>{if(u.Kind==ScreenshotGenerationEvent.Usage)Assert.True(r.Flow.SetMode(ImageOutputMode.ExplainImage));};await r.Generate();Assert.Equal(ImageOutputMode.TranslateText,r.Flow.SelectedSnapshot!.Plan!.Mode);r.OnUpdate=null;await r.Generate(regenerate:true);Assert.Equal(ImageOutputMode.ExplainImage,r.Flow.SelectedSnapshot!.Plan!.Mode);Assert.True(r.Flow.SelectedSnapshot.ClassificationUsageReused);Assert.Equal(3,r.Script.Requests);
    }
    [Theory][InlineData(false,false)][InlineData(true,false)][InlineData(false,true)][InlineData(true,true)]
    public async Task UsageReentryAdmittingCKeepsItsReceiptsSeparateFromCancelledA(bool generation,bool replaceSource)
    {
        await using var r=await Rig.Create(Protocol.OpenAIChat);RequestIdentity c=default;ScreenshotUsageReceipt? origin=null;r.OnUpdate=u=>
        {
            if(u.Kind!=ScreenshotGenerationEvent.Usage||(u.Snapshot.GenerationUsage!=null)!=generation)return;r.OnUpdate=null;origin=u.Snapshot.ClassificationUsage;if(replaceSource)r.Adopt();Assert.True(r.Flow.TryStart(true));c=r.Flow.Results.Identity!.Value;
        };
        Assert.True(r.Flow.TryStart(true));Task old=r.Flow.Running;await r.Requests(1);await UsageReply(r,ImageClassificationTests.Json(),Counts(r.Protocol,13,3));if(generation){await r.Requests(2);await UsageReply(r,"A partial",Counts(r.Protocol,29,17));}await old.WaitAsync(TimeSpan.FromSeconds(6));Assert.NotEqual(default,c);int cFirst=generation?3:2;await r.Requests(cFirst);
        bool cached=generation&&!replaceSource;if(!cached){await UsageReply(r,ImageClassificationTests.Json(),Counts(r.Protocol,41,4));await r.Requests(cFirst+1);}await UsageReply(r,"C output",Counts(r.Protocol,91,92));await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));var snapshot=r.Flow.SelectedSnapshot!;
        Assert.Equal(c,snapshot.GenerationUsage!.Identity);Assert.Equal(new TokenUsage(91,92),snapshot.GenerationUsage.Tokens);Assert.Equal(r.Image.SourceId,snapshot.GenerationUsage.SourceId);Assert.Equal("C output",r.Flow.Results.Text);Assert.False(r.Flow.Results.Versions.Single(v=>v.Identity==c).Stopped);Assert.Equal(cached,snapshot.ClassificationUsageReused);if(cached)Assert.Same(origin,snapshot.ClassificationUsage);else Assert.Equal(c,snapshot.ClassificationUsage!.Identity);
    }
    [Theory][InlineData(Protocol.OpenAIChat,false)][InlineData(Protocol.AnthropicMessages,false)][InlineData(Protocol.OpenAIChat,true)][InlineData(Protocol.AnthropicMessages,true)]
    public async Task QueuedUsageCannotRecreateClearedOrReplacedSourceMetadata(Protocol protocol,bool replaceSource)
    {
        var queue=new Deferred();await using var r=await Rig.Create(protocol,stream:true,destination:queue);Assert.True(r.Flow.TryStart(true));using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(r.Script.Requests==0){queue.Drain();await Task.Delay(5,limit.Token);}Task old=r.Flow.Running;
        await r.Script.Write((protocol==Protocol.OpenAIChat?"":"data: {\"type\":\"message_start\",\"message\":{}}\n\n")+UsageFrame(protocol,Counts(protocol,77,8)));while(queue.Pending==0)await Task.Delay(5,limit.Token);
        if(replaceSource)r.Adopt();else r.Flow.Clear();queue.Drain();await old.WaitAsync(TimeSpan.FromSeconds(6));Assert.Empty(r.Flow.Results.Versions);Assert.Null(r.Flow.SelectedSnapshot);Assert.DoesNotContain(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Usage);Assert.Equal(1,r.Script.Requests);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task BrowsingAnOldVersionDoesNotRebindTheActiveGenerationReceipt(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol);await r.Generate(final:"old readable version");var old=r.Flow.SelectedSnapshot!;var oldId=r.Flow.Results.Identity!.Value;
        Assert.True(r.Flow.TryStart(true,regenerate:true));var current=r.Flow.Results.Identity!.Value;await r.Requests(3);Assert.True(r.Flow.Results.SelectVersion(oldId.VersionId));Assert.Same(old,r.Flow.SelectedSnapshot);
        await UsageReply(r,"new active version",Counts(protocol,47,19));await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Same(old,r.Flow.SelectedSnapshot);Assert.Equal(new TokenUsage(11,7),old.GenerationUsage!.Tokens);Assert.Equal("old readable version",r.Flow.Results.Text);
        Assert.True(r.Flow.Results.SelectVersion(current.VersionId));Assert.Equal(current,r.Flow.SelectedSnapshot!.GenerationUsage!.Identity);Assert.Equal(new TokenUsage(47,19),r.Flow.SelectedSnapshot.GenerationUsage.Tokens);Assert.Equal("new active version",r.Flow.Results.Text);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task CachedClassificationRetainsItsOriginalProtocolAcrossANewExplicitProfileGeneration(Protocol initialProtocol)
    {
        await using var r=await Rig.Create(initialProtocol);await r.Generate();var original=r.Flow.SelectedSnapshot!.ClassificationUsage!;var next=initialProtocol==Protocol.OpenAIChat?Protocol.AnthropicMessages:Protocol.OpenAIChat;
        var profile=r.Settings.Data.Profiles[0] with{Protocol=next==Protocol.OpenAIChat?KuaiYi.Core.Settings.ApiProtocol.OpenAIChat:KuaiYi.Core.Settings.ApiProtocol.AnthropicMessages};r.Settings=r.Settings with{Data=r.Settings.Data with{Profiles=[profile]},Version="explicit-new-protocol"};r.Gate.ObserveSettingsMetadata(r.Settings);r.Protocol=next;
        Assert.True(r.Flow.TryStart(true));await r.Requests(3);await UsageReply(r,"new protocol generation",Counts(next,53,23));await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));var snapshot=r.Flow.SelectedSnapshot!;
        Assert.Equal(3,r.Script.Requests);Assert.True(snapshot.ClassificationUsageReused);Assert.Same(original,snapshot.ClassificationUsage);Assert.Equal(initialProtocol,snapshot.ClassificationUsage!.Protocol);Assert.Equal(next,snapshot.GenerationUsage!.Protocol);Assert.Equal(new TokenUsage(53,23),snapshot.GenerationUsage.Tokens);Assert.NotEqual(original.Identity,snapshot.GenerationUsage.Identity);Assert.Equal(original.SourceId,snapshot.GenerationUsage.SourceId);
    }
    [Theory][InlineData(Protocol.OpenAIChat,false)][InlineData(Protocol.AnthropicMessages,false)][InlineData(Protocol.OpenAIChat,true)][InlineData(Protocol.AnthropicMessages,true)]
    public async Task ActualClassificationAndGenerationUseOneWhiteNormalizedPngWithTheSameLocalTransform(Protocol protocol,bool downscale)
    {
        await using var r=await Rig.Create(protocol);int width=downscale?4096:8,height=16;var bounds=new PixelRect(-234,91,width-234,height+91);r.AdoptRaw(bounds,ScreenshotNormalizationTests.Solid(width,height,0,0,0,downscale?(byte)128:(byte)0));var original=r.Raw.ToArray();await r.Generate(final:"normalized public output");var snapshot=r.Flow.SelectedSnapshot!;var map=Assert.IsType<ImageTransform>(snapshot.Transform);
        Assert.Equal(ImageTransform.Plan(bounds),map);Assert.Equal(downscale?ScreenshotNormalizationProof.ConstantCompositeDownscale:ScreenshotNormalizationProof.NativeSizeWhiteComposite,snapshot.NormalizationProof);Assert.Equal(original,r.Raw);var bodies=r.Script.Bodies.ToArray();var first=Body(protocol,bodies[0]);var final=Body(protocol,bodies[1]);Assert.Equal(first.Image,final.Image);Assert.Equal(map.NormalizedSize,ImagePolicy.InspectPng(final.Image));var rgb=ScreenshotNormalizationTests.DecodeRgb(final.Image);int stride=(int)map.NormalizedSize.Width*3+1;
        for(int y=0;y<map.NormalizedSize.Height;y++)Assert.All(rgb.AsSpan(y*stride+1,stride-1).ToArray(),b=>Assert.Equal(downscale?127:255,b));Assert.DoesNotContain("ImageTransform",first.System+first.User+final.System+final.User);Assert.DoesNotContain("-234",first.System+first.User+final.System+final.User);
        Assert.Same(map,Assert.Single(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Prepared).Snapshot.Transform);Assert.Same(map,Assert.Single(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Classifying).Snapshot.Transform);Assert.Same(map,Assert.Single(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Generating).Snapshot.Transform);
        var region=Assert.Single(snapshot.Classification!.Regions);var unit=new UnitImageRectangle(region.X,region.Y,region.Width,region.Height);var originalRegion=map.RelativeToOriginal(unit);Assert.InRange(Math.Abs(originalRegion.X-width*region.X),0,1e-8);Assert.InRange(Math.Abs(originalRegion.Height-height*region.Height),0,1e-8);var physicalRegion=map.NormalizedToDesktop(map.RelativeToNormalized(unit));Assert.InRange(Math.Abs(physicalRegion.X-(bounds.Left+width*region.X)),0,1e-8);
        var classification=snapshot.Classification;var receipt=snapshot.ClassificationUsage;await r.Generate(final:"normalized regeneration",regenerate:true);Assert.Equal(3,r.Script.Requests);Assert.Same(classification,r.Flow.SelectedSnapshot!.Classification);Assert.Same(receipt,r.Flow.SelectedSnapshot.ClassificationUsage);Assert.Equal(map,r.Flow.SelectedSnapshot.Transform);Assert.Equal(final.Image,Body(protocol,r.Script.Bodies.Last()).Image);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task NonConstantOversizeAndEncodedCapRequireLocalCropWithNoClassifierOrUsage(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol);r.Adopt(3001,8);var raw=r.Raw.ToArray();Assert.True(r.Flow.TryStart(true));await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(0,r.Script.Requests);Assert.Equal(raw,r.Raw);Assert.False(r.Image.IsDisposed);Assert.Equal("SMALLER_SCREENSHOT_REQUIRED",Assert.Single(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Failed).Reason);Assert.Null(r.Flow.SelectedSnapshot!.Transform);Assert.Null(r.Flow.SelectedSnapshot.ClassificationUsage);Assert.Null(r.Flow.SelectedSnapshot.GenerationUsage);
        r.Events.Clear();byte[] noisy=new byte[256*256*4];new Random(7639).NextBytes(noisy);for(int i=3;i<noisy.Length;i+=4)noisy[i]=255;r.AdoptRaw(new(-11,-13,245,243),noisy);var profile=r.Settings.Data.Profiles[0] with{ImageByteLimit=65536};r.Settings=r.Settings with{Data=r.Settings.Data with{Profiles=[profile]}};r.Gate.ObserveSettingsMetadata(r.Settings);
        Assert.True(r.Flow.TryStart(true));await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(0,r.Script.Requests);Assert.False(r.Image.IsDisposed);Assert.Equal("SMALLER_SCREENSHOT_REQUIRED",Assert.Single(r.Events,e=>e.Kind==ScreenshotGenerationEvent.Failed).Reason);Assert.DoesNotContain(r.Events,e=>e.Kind is ScreenshotGenerationEvent.Prepared or ScreenshotGenerationEvent.Classifying or ScreenshotGenerationEvent.Usage);
    }
    public static IEnumerable<object[]> PreparationCancellationCases()
    {foreach(var p in Enum.GetValues<Protocol>())foreach(string action in new[]{"stop","clear","adopt","lock","hide"})yield return [p,action];}
    [Theory][MemberData(nameof(PreparationCancellationCases))]
    public async Task PreparedCallbackCancellationCannotStartClassificationOrResurrectMappedSource(Protocol protocol,string action)
    {
        await using var r=await Rig.Create(protocol);r.AdoptRaw(new(-17,-19,4079,-3),ScreenshotNormalizationTests.Solid(4096,16,0,0,0,128));var raw=r.Raw;bool acted=false;r.OnUpdate=u=>
        {
            if(u.Kind!=ScreenshotGenerationEvent.Prepared)return;r.OnUpdate=null;acted=true;
            switch(action){case "stop":r.Flow.Stop();break;case "clear":r.Flow.Clear();break;case "adopt":r.Adopt();break;case "lock":r.Flow.LockSession();break;case "hide":r.Flow.Hide();break;}
        };
        Assert.True(r.Flow.TryStart(true));await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.True(acted);Assert.Equal(0,r.Script.Requests);Assert.False(r.Gate.HasRunningGeneration);Assert.DoesNotContain(r.Events,e=>e.Kind is ScreenshotGenerationEvent.Classifying or ScreenshotGenerationEvent.Generating or ScreenshotGenerationEvent.Usage);
        if(action=="stop"){Assert.True(Assert.Single(r.Flow.Results.Versions).Stopped);Assert.NotNull(r.Flow.SelectedSnapshot!.Transform);Assert.Null(r.Flow.SelectedSnapshot.Classification);Assert.False(r.Image.IsDisposed);}
        else {Assert.Empty(r.Flow.Results.Versions);Assert.Null(r.Flow.SelectedSnapshot);Assert.All(raw,b=>Assert.Equal(0,b));}
    }
    [Theory][InlineData(Protocol.OpenAIChat,false)][InlineData(Protocol.AnthropicMessages,false)][InlineData(Protocol.OpenAIChat,true)][InlineData(Protocol.AnthropicMessages,true)]
    public async Task PreparationReentryCReceivesOnlyItsOwnMappingPngAndLaterEvents(Protocol protocol,bool replaceSource)
    {
        await using var r=await Rig.Create(protocol);r.AdoptRaw(new(-117,31,3979,47),ScreenshotNormalizationTests.Solid(4096,16,0,0,0,128));RequestIdentity a=default,c=default;ImageTransform? aMap=null;var cCompleted=new TaskCompletionSource<RequestIdentity>(TaskCreationOptions.RunContinuationsAsynchronously);
        r.OnUpdate=u=>{if(u.Kind!=ScreenshotGenerationEvent.Prepared)return;r.OnUpdate=v=>{if(v.Kind==ScreenshotGenerationEvent.Completed)cCompleted.TrySetResult(v.Identity);};a=u.Identity;aMap=u.Snapshot.Transform;if(replaceSource)r.Adopt();Assert.True(r.Flow.TryStart(true));c=r.Flow.Results.Identity!.Value;};
        Assert.True(r.Flow.TryStart(true));Task old=r.Flow.Running;await r.Requests(1);await r.Reply(ImageClassificationTests.Json());await r.Requests(2);await r.Reply("C prepared output");await old.WaitAsync(TimeSpan.FromSeconds(6));await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(c,await cCompleted.Task.WaitAsync(TimeSpan.FromSeconds(6)));var snapshot=r.Flow.SelectedSnapshot!;
        Assert.NotEqual(a,c);Assert.Equal(c,r.Flow.Results.Identity);Assert.Equal(c,snapshot.GenerationUsage!.Identity);Assert.Equal(r.Image.SourceId,snapshot.SourceId);Assert.Equal(replaceSource?new ImageSize(8,8):new ImageSize(2560,10),snapshot.Transform!.NormalizedSize);if(replaceSource)Assert.NotEqual(aMap,snapshot.Transform);else Assert.Equal(aMap,snapshot.Transform);
        Assert.Equal("C prepared output",r.Flow.Results.Text);Assert.Equal(2,r.Script.Requests);Assert.Equal(Body(protocol,r.Script.Bodies.First()).Image,Body(protocol,r.Script.Bodies.Last()).Image);Assert.DoesNotContain(r.Events,e=>e.Identity==a&&e.Kind!=ScreenshotGenerationEvent.Prepared);Assert.False(r.Flow.Results.Versions.Single(v=>v.Identity==c).Stopped);Assert.True(r.Flow.Results.Versions.Single(v=>v.Identity==c).Complete);Assert.Contains(r.Events,e=>e.Identity==c&&e.Kind==ScreenshotGenerationEvent.Completed);
    }
    [Theory][InlineData(Protocol.OpenAIChat,false)][InlineData(Protocol.AnthropicMessages,false)][InlineData(Protocol.OpenAIChat,true)][InlineData(Protocol.AnthropicMessages,true)]
    public async Task QueuedPreparedMappingAfterClearOrAdoptionCannotPublishOrSend(Protocol protocol,bool adopt)
    {
        var queue=new Deferred();await using var r=await Rig.Create(protocol,destination:queue);r.AdoptRaw(new(-700,81,3396,97),ScreenshotNormalizationTests.Solid(4096,16,0,0,0,128));var raw=r.Raw;Assert.True(r.Flow.TryStart(true));Task old=r.Flow.Running;using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(queue.Pending==0)await Task.Delay(5,limit.Token);
        if(adopt)r.Adopt();else r.Flow.Clear();queue.Drain();await old.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(0,r.Script.Requests);Assert.Empty(r.Flow.Results.Versions);Assert.Null(r.Flow.SelectedSnapshot);Assert.Empty(r.Events);Assert.All(raw,b=>Assert.Equal(0,b));if(adopt)Assert.False(r.Image.IsDisposed);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task PreparedModeEditCannotChangeTheCurrentFrozenPlanOrImage(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol);r.OnUpdate=u=>{if(u.Kind==ScreenshotGenerationEvent.Prepared)Assert.True(r.Flow.SetMode(ImageOutputMode.ExplainImage));};await r.Generate();Assert.Equal(ImageOutputMode.TranslateText,r.Flow.SelectedSnapshot!.Plan!.Mode);r.OnUpdate=null;await r.Generate(regenerate:true);Assert.Equal(ImageOutputMode.ExplainImage,r.Flow.SelectedSnapshot!.Plan!.Mode);Assert.Equal(3,r.Script.Requests);Assert.Equal(Body(protocol,r.Script.Bodies.First()).Image,Body(protocol,r.Script.Bodies.Last()).Image);
    }
    [Theory][InlineData(Protocol.OpenAIChat,false)][InlineData(Protocol.AnthropicMessages,false)][InlineData(Protocol.OpenAIChat,true)][InlineData(Protocol.AnthropicMessages,true)]
    public async Task CachedClassificationPreparationReentryCannotPublishOrSendTheRejectedFinal(Protocol protocol,bool startC)
    {
        await using var r=await Rig.Create(protocol);r.AdoptRaw(new(-17,-19,4079,-3),ScreenshotNormalizationTests.Solid(4096,16,0,0,0,128));await r.Generate(final:"original classified version");var original=r.Flow.SelectedSnapshot!;RequestIdentity b=default,c=default;
        r.OnUpdate=u=>{if(u.Kind!=ScreenshotGenerationEvent.Prepared)return;r.OnUpdate=null;b=u.Identity;if(startC){Assert.True(r.Flow.TryStart(true,regenerate:true));c=r.Flow.Results.Identity!.Value;}else r.Flow.Stop();};
        Assert.True(r.Flow.TryStart(true,regenerate:true));Task rejected=r.Flow.Running;
        if(startC){await r.Requests(3);await r.Reply("C cached final");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));}await rejected.WaitAsync(TimeSpan.FromSeconds(6));Assert.NotEqual(default,b);
        Assert.DoesNotContain(r.Events,e=>e.Identity==b&&e.Kind is ScreenshotGenerationEvent.Generating or ScreenshotGenerationEvent.Classifying or ScreenshotGenerationEvent.Usage);Assert.Equal(startC?3:2,r.Script.Requests);Assert.Same(original.ClassificationUsage,r.Flow.SelectedSnapshot!.ClassificationUsage);Assert.Equal(original.Transform,r.Flow.SelectedSnapshot.Transform);
        if(startC){Assert.Equal(c,r.Flow.Results.Identity);Assert.Equal("C cached final",r.Flow.Results.Text);Assert.Equal(c,r.Flow.SelectedSnapshot.GenerationUsage!.Identity);}else{Assert.True(r.Flow.Results.Versions.Single(v=>v.Identity==b).Stopped);Assert.Null(r.Flow.SelectedSnapshot.GenerationUsage);Assert.False(r.Gate.HasRunningGeneration);}
    }
}
