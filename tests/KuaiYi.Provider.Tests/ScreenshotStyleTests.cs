using System.Text.Json;
using System.Windows.Controls;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Imaging;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
using SecretLease=KuaiYi.Providers.SecretLease;
namespace KuaiYi.Provider.Tests;
public sealed class ScreenshotStyleTests
{
    private sealed class Immediate:SynchronizationContext{public override void Post(SendOrPostCallback callback,object? state)=>callback(state);}
    private sealed class Rig:IAsyncDisposable
    {
        public readonly PhaseHttpFixture Server=new();public PhaseHttpFixture.Script Script=null!;public readonly ShellWindow Window=new();public readonly ProviderClient Provider=new();
        public SettingsSnapshot Config=null!;public ProductionScreenshotCoordinator Flow=null!;public OwnedSourceImage Image=null!;public Protocol Protocol;public readonly List<SecretLease> Leases=[];
        public static async Task<Rig> Create(Protocol protocol=Protocol.OpenAIChat)
        {
            var r=new Rig{Protocol=protocol};await r.Server.InitializeAsync();r.Script=r.Server.Add("style","application/json");var w=r.Window;w.InitializeResultFlow();w.WithGeometry();
            r.Config=new(new(){ActiveProfileId="vision",VisionProfileId="vision",Profiles=[new(){Id="vision",Protocol=protocol==Protocol.OpenAIChat?KuaiYi.Core.Settings.ApiProtocol.OpenAIChat:KuaiYi.Core.Settings.ApiProtocol.AnthropicMessages,BaseUrl=new Uri(r.Server.BaseUri,"style/").AbsoluteUri,AllowLoopbackHttp=true,Authentication=protocol==Protocol.OpenAIChat?ApiAuthentication.Bearer:ApiAuthentication.ApiKey,SecretRef="key/"+new string('a',32),Model="PUBLIC_STYLE_MODEL",Streaming=false,Capabilities=new(){Vision=Capability.Supported}}],Translation=new(){Preset=TranslationPreset.Faithful}},"public-style-settings",SettingsLoadState.Current);
            r.Flow=w.ConfigureScreenshotForTest(r.Config,r.Provider,new Immediate(),(identity,snapshot,_)=>
            {
                Assert.Same(r.Config,snapshot.Settings);Assert.Same(r.Config.Data.Profiles[0],snapshot.Profile);Assert.True(w.MarkdownGate.IsRequestProfileBindingCurrent(identity,snapshot.Binding));
                var lease=new SecretLease(System.Text.Encoding.UTF8.GetBytes("PUBLIC_SYNTHETIC_STYLE_SECRET"));r.Leases.Add(lease);return Task.FromResult<SecretLease?>(lease);
            });var origin=new EndpointOrigin(new Uri("https://public-text.invalid"));w.MarkdownGate.ConsentTo(origin);Assert.True(w.MarkdownGate.TryEnable(origin,true));r.NewImage();return r;
        }
        public void NewImage()
        {
            var input=Window.ImageInputForTest();using(input.Intent)using(input.Cancellation)
            {Image=new(new ScreenshotPixels(new(0,0,8,8),Enumerable.Range(0,256).Select(i=>(byte)(i%4==3?255:i)).ToArray()));Assert.True(input.Commit(Image));Window.EndRestoreForTest(input.Cancellation);}
        }
        public MenuItem Menu()=>Window.ScreenshotStyleMenuForTest();
        public void Choose(TranslationPreset preset)=>Assert.IsType<MenuItem>(Menu().Items[(int)preset]).Invoke();
        public async Task Requests(int count){using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(Script.Requests<count)await Task.Delay(5,limit.Token);}
        public Task Reply(string text)=>Script.Write(Protocol==Protocol.OpenAIChat?JsonSerializer.Serialize(new{choices=new[]{new{index=0,message=new{content=text},finish_reason="stop"}},usage=new{prompt_tokens=3,completion_tokens=4}}):JsonSerializer.Serialize(new{type="message",content=new[]{new{type="text",text}},stop_reason="end_turn",usage=new{input_tokens=3,output_tokens=4}}),true);
        public string SystemBody(int index){using var doc=JsonDocument.Parse(Script.Bodies.ElementAt(index));return Protocol==Protocol.OpenAIChat?doc.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!:doc.RootElement.GetProperty("system").GetString()!;}
        public void Drain(){for(int i=0;i<8&&Window.FlushPending;i++)Window.TickForTest();Assert.False(Window.FlushPending);}
        public async ValueTask DisposeAsync(){try{await Flow.DisposeAsync();await Window.ClearContentForTest();}finally{Provider.Dispose();await Server.DisposeAsync();}}
    }
    private static string Label(TranslationPreset p)=>p switch{TranslationPreset.Faithful=>"忠实翻译",TranslationPreset.Plain=>"通俗翻译",TranslationPreset.Professional=>"专业翻译",_=>"解释"};
    [Theory]
    [InlineData(Protocol.OpenAIChat,TranslationPreset.Faithful)][InlineData(Protocol.OpenAIChat,TranslationPreset.Plain)][InlineData(Protocol.OpenAIChat,TranslationPreset.Professional)][InlineData(Protocol.OpenAIChat,TranslationPreset.Explain)]
    [InlineData(Protocol.AnthropicMessages,TranslationPreset.Faithful)][InlineData(Protocol.AnthropicMessages,TranslationPreset.Plain)][InlineData(Protocol.AnthropicMessages,TranslationPreset.Professional)][InlineData(Protocol.AnthropicMessages,TranslationPreset.Explain)]
    public async Task UnsentSourceStyleFreezesActualPromptConfirmationLabelsAndRealSavedBinding(Protocol protocol,TranslationPreset preset)
    {
        await using var r=await Rig.Create(protocol);var w=r.Window;var saved=r.Config;var original=w.SelectedSourceVersionForTest!.Value;var oldMenu=r.Menu();r.Choose(preset);Assert.Equal(preset,w.ScreenshotPresetForTest);Assert.Contains(Label(preset),w.ResultLabel);Assert.True(Assert.IsType<MenuItem>(r.Menu().Items[(int)preset]).IsChecked);Assert.Empty(r.Script.Bodies);Assert.Empty(r.Leases);
        ProductionScreenshotOffer? seen=null;Assert.True(w.SendScreenshotForTest(ImageOutputMode.Automatic,o=>{seen=o;Assert.Equal(preset,o.RequestedPreset);Assert.Contains(Label(preset),o.ConfirmationText);Assert.Same(saved,o.Snapshot.Settings);Assert.Same(saved.Data.Profiles[0],o.Snapshot.Profile);return true;}));
        var id=w.MarkdownResults.Identity!.Value;Assert.Equal(preset,r.Flow.PresetForVersion(id.VersionId));Assert.Contains(Label(preset),w.ResultLabel);Assert.False(r.Flow.SetSourcePreset(original,r.Image.SourceId,TranslationPreset.Plain));Assert.IsType<MenuItem>(oldMenu.Items[(int)TranslationPreset.Plain]).Invoke();Assert.Equal(preset,seen!.RequestedPreset);
        await r.Requests(1);await r.Reply(ImageClassificationTests.Json("text_only"));await r.Requests(2);await r.Reply("PUBLIC_STYLED_OUTPUT");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));r.Drain();var snapshot=r.Flow.SelectedSnapshot!;var expected=PromptBuilder.Compose(saved.Data.Translation with{Preset=preset},"PUBLIC APPROVED OCR TEXT");
        Assert.Equal(expected.System,r.SystemBody(1));Assert.Contains(PromptBuilder.Build(preset.ToString().ToLowerInvariant()),r.SystemBody(1));Assert.Equal(expected.FullTemplateVersion,snapshot.Plan!.TemplateVersion);Assert.True(snapshot.Ocr!.UsedForText);Assert.DoesNotContain("PUBLIC APPROVED OCR TEXT",snapshot.Plan.User);Assert.Equal(preset,snapshot.Plan.Preset);Assert.Same(saved,snapshot.Settings);Assert.Equal(TranslationPreset.Faithful,saved.Data.Translation.Preset);Assert.Contains(Label(preset),w.ResultLabel);Assert.Contains(Label(preset),w.VersionDescriptionForTest(id.VersionId));Assert.Equal("PUBLIC_STYLED_OUTPUT",w.MarkdownResults.Text);Assert.Equal(2,r.Leases.Count);Assert.All(r.Leases,l=>Assert.Throws<ObjectDisposedException>(()=>l.HeaderValue()));
        Assert.False(w.SendScreenshotForTest(ImageOutputMode.TranslateText,_=>true));Assert.False(r.Menu().IsEnabled);Assert.Equal(2,r.Script.Requests);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task AutomaticStructuredImageStillUsesExplainAfterExplicitPlainSelection(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol);r.Choose(TranslationPreset.Plain);Assert.True(r.Window.SendScreenshotForTest(ImageOutputMode.Automatic,o=>{Assert.Contains("通俗翻译",o.ConfirmationText);Assert.Contains("按图文解释时使用解释风格",o.ConfirmationText);return true;}));await r.Requests(1);await r.Reply(ImageClassificationTests.Json("diagram_or_mixed"));await r.Requests(2);await r.Reply("PUBLIC_EXPLANATION");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));r.Drain();Assert.Equal(TranslationPreset.Explain,r.Flow.SelectedSnapshot!.Plan!.Preset);Assert.Equal(ImageOutputMode.ExplainImage,r.Flow.SelectedSnapshot.Plan.Mode);Assert.Contains(PromptBuilder.Read("image-explain"),r.SystemBody(1));Assert.Contains("解释",r.Window.ResultLabel);Assert.Equal(TranslationPreset.Faithful,r.Config.Data.Translation.Preset);
    }
    [Theory][InlineData(Protocol.OpenAIChat,"decline")][InlineData(Protocol.OpenAIChat,"off")][InlineData(Protocol.OpenAIChat,"clear")][InlineData(Protocol.OpenAIChat,"replace")][InlineData(Protocol.OpenAIChat,"settings")][InlineData(Protocol.OpenAIChat,"style")]
    [InlineData(Protocol.AnthropicMessages,"decline")][InlineData(Protocol.AnthropicMessages,"off")][InlineData(Protocol.AnthropicMessages,"clear")][InlineData(Protocol.AnthropicMessages,"replace")][InlineData(Protocol.AnthropicMessages,"settings")][InlineData(Protocol.AnthropicMessages,"style")]
    public async Task StyledOfferRevocationOrDeclineSendsZeroHttp(Protocol protocol,string action)
    {
        await using var r=await Rig.Create(protocol);var w=r.Window;r.Choose(TranslationPreset.Professional);var stale=r.Menu();ProductionScreenshotOffer? offered=null;
        Assert.False(w.SendScreenshotForTest(ImageOutputMode.TranslateText,o=>
        {
            offered=o;if(action=="off")w.MarkdownGate.Disable();else if(action=="clear")w.ClearImagePresentationForTest();else if(action=="replace"){r.NewImage();Assert.IsType<MenuItem>(stale.Items[(int)TranslationPreset.Explain]).Invoke();Assert.Equal(TranslationPreset.Faithful,w.ScreenshotPresetForTest);}else if(action=="settings")w.ScreenshotSettingsForTest(r.Config with{Version="changed-style-settings",Data=r.Config.Data with{WriteId=Guid.NewGuid(),Translation=r.Config.Data.Translation with{Preset=TranslationPreset.Explain}}});else if(action=="style")r.Choose(TranslationPreset.Plain);return action!="decline";
        }));Assert.Equal(TranslationPreset.Professional,offered!.RequestedPreset);Assert.Empty(r.Script.Bodies);Assert.Equal(0,r.Script.Requests);Assert.Empty(r.Leases);Assert.False(w.MarkdownGate.HasRunningGeneration);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task NewSourceAndClearDiscardTheOverrideAndRejectStaleStyleMenus(bool clear)
    {
        await using var r=await Rig.Create();var w=r.Window;r.Choose(TranslationPreset.Explain);var stale=r.Menu();var source=w.SelectedSourceVersionForTest!.Value;var image=r.Image;
        Assert.False(r.Flow.SetSourcePreset(Guid.NewGuid(),image.SourceId,TranslationPreset.Plain));Assert.False(r.Flow.SetSourcePreset(source,Guid.NewGuid(),TranslationPreset.Plain));Assert.Equal(TranslationPreset.Explain,w.ScreenshotPresetForTest);
        if(clear){Assert.True(await w.ClearContentForTest());Assert.Null(w.ScreenshotPresetForTest);Assert.Null(r.Flow.PresetForSource(source,image.SourceId));}
        r.NewImage();Assert.NotEqual(source,w.SelectedSourceVersionForTest);Assert.NotEqual(image.SourceId,r.Image.SourceId);Assert.True(image.IsDisposed);Assert.Equal(TranslationPreset.Faithful,w.ScreenshotPresetForTest);Assert.IsType<MenuItem>(stale.Items[(int)TranslationPreset.Professional]).Invoke();Assert.Equal(TranslationPreset.Faithful,w.ScreenshotPresetForTest);
        r.Choose(TranslationPreset.Plain);Assert.IsType<MenuItem>(r.Menu().Items[^1]).Invoke();Assert.Equal(TranslationPreset.Faithful,w.ScreenshotPresetForTest);Assert.False(r.Flow.SetSourcePreset(w.SelectedSourceVersionForTest!.Value,r.Image.SourceId,(TranslationPreset)999));Assert.Empty(r.Script.Bodies);
    }
}
