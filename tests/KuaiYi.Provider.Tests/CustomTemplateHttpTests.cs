using System.Collections.Immutable;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.MockProvider;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Core.Settings.ApiProtocol;
namespace KuaiYi.Provider.Tests;
public sealed class CustomTemplateHttpTests(ServerFixture fixture):IClassFixture<ServerFixture>
{
    private sealed class Destination:SynchronizationContext{public override void Post(SendOrPostCallback callback,object? state)=>callback(state);}
    private sealed class PrivateDirectory:ISecretDirectoryPolicy{public void EnsurePrivate(string directory)=>Directory.CreateDirectory(directory);}
    private sealed class NoSecrets:ISecretProtector{public int Reads;public byte[] Protect(ReadOnlySpan<byte> value)=>throw new InvalidOperationException("No credentials in this fixture.");public byte[] Unprotect(ReadOnlySpan<byte> value){Reads++;throw new InvalidOperationException("No credentials in this fixture.");}}
    private sealed class Rig:IAsyncDisposable
    {
        public string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-template-"+Guid.NewGuid().ToString("N")),Run=Guid.NewGuid().ToString("N");public SettingsCoordinator Coordinator=null!;public SettingsRuntimeLink Link=null!;public SettingsStore Store=null!;public readonly NoSecrets Protector=new();public RuntimeGate Gate=new(requireProfileBinding:true);public ProviderClient Provider=new();public OverlayResults Results=null!;public TextTranslationCoordinator Flow=null!;public List<TextTranslationUpdate> Updates=[];public Protocol Protocol;
        public SettingsSnapshot Settings=>Coordinator.Effective!;
        public static async Task<Rig> Create(Uri server,Protocol protocol,string template="PUBLIC_CUSTOM {{target_language}} {{domain}} {{reader_background}} {{source_text}} {{image_mode}}",string scenario="fragmented")
        {
            var rig=new Rig{Protocol=protocol};try{var prefix=new Uri(server,$"runs/{rig.Run}/{scenario}/");rig.Store=new(Path.Combine(rig.Root,"config"));rig.Coordinator=new(rig.Store,new(Path.Combine(rig.Root,"secrets"),rig.Protector,new PrivateDirectory()));
            await rig.Store.LoadAsync();var data=new AppSettings{ActiveProfileId="p",Profiles=[new(){Id="p",Protocol=protocol,BaseUrl=prefix.AbsoluteUri,Model="PUBLIC_CUSTOM_MODEL",AllowLoopbackHttp=true,Authentication=ApiAuthentication.None}],Translation=new(){Domain="PUBLIC_DOMAIN",ReaderBackground="PUBLIC_BACKGROUND"}};
            var node=JsonNode.Parse(SettingsJson.Write(data))!;node["translation"]!["customTemplates"]=new JsonObject{["Faithful"]=template};await File.WriteAllTextAsync(rig.Store.PrimaryPath,node.ToJsonString());using var draft=await rig.Coordinator.OpenDraftAsync();Assert.False(draft.Basis.RequiresRecovery);
            rig.Link=new(rig.Coordinator,rig.Gate);rig.Link.Initialize(rig.Settings);rig.Gate.ConsentTo(new(prefix));Assert.True(rig.Gate.TryEnable(new(prefix),true));rig.Results=new(rig.Gate);rig.Flow=new(rig.Gate,()=>rig.Settings,(_,_)=>true,rig.Provider,new Destination(),rig.Results,rig.Updates.Add);return rig;}catch{await rig.DisposeAsync();throw;}
        }
        public SelectionSourceSnapshot Source(string text="PUBLIC_ORIGINAL 12.5 mg; version 2; no loss.")=>new(new(18,98213,132000,@"C:\Fixtures\editor.exe"),text,text,[],[7,8],Settings.Version,text.EnumerateRunes().Count(),false);
        public async Task Start(){Assert.Equal(TextSubmission.Started,Flow.Submit(Flow.Prepare(Source())!));await Flow.Running;}
        public async ValueTask DisposeAsync(){Gate.Disable();if(Flow!=null)await Flow.DisposeAsync();Provider.Dispose();Link?.Dispose();if(Directory.Exists(Root))Directory.Delete(Root,true);}
    }
    private async Task<CapturedRequest[]> Requests(Rig rig){using var http=new HttpClient();return(await http.GetFromJsonAsync<CapturedRequest[]>(new Uri(fixture.BaseUri,$"requests/{rig.Run}")))!;}
    private static string SystemPrompt(CapturedRequest request,Protocol protocol){using var json=JsonDocument.Parse(request.Body);return protocol==Protocol.OpenAIChat?json.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!:json.RootElement.GetProperty("system").GetString()!;}
    [Theory] [InlineData(Protocol.OpenAIChat)] [InlineData(Protocol.AnthropicMessages)]
    public async Task SavedCustomTemplateMustReachProductionRequestAfterActualFileRead(Protocol protocol)
    {
        await using var rig=await Rig.Create(fixture.BaseUri,protocol);await rig.Start();var request=Assert.Single(await Requests(rig));var system=SystemPrompt(request,protocol);Assert.Contains("PUBLIC_CUSTOM",system);Assert.Contains("PUBLIC_DOMAIN",system);Assert.DoesNotContain(rig.Source().Text,system);Assert.Equal(0,rig.Protector.Reads);Assert.True(Assert.Single(rig.Results.Versions).Complete);
    }

    private async Task Barrier(Rig rig)
    {using var http=new HttpClient();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(true){var reply=await http.GetFromJsonAsync<JsonElement>(new Uri(fixture.BaseUri,$"barriers/{rig.Run}"),timeout.Token);if(reply.GetProperty("ready").GetBoolean())return;await Task.Delay(10,timeout.Token);}}
    private async Task Release(Rig rig){using var http=new HttpClient();using var reply=await http.PostAsync(new Uri(fixture.BaseUri,$"barriers/{rig.Run}/release"),null);reply.EnsureSuccessStatusCode();}
    [Theory]
    [InlineData(Protocol.OpenAIChat,TranslationPreset.Faithful)] [InlineData(Protocol.OpenAIChat,TranslationPreset.Plain)] [InlineData(Protocol.OpenAIChat,TranslationPreset.Professional)] [InlineData(Protocol.OpenAIChat,TranslationPreset.Explain)]
    [InlineData(Protocol.AnthropicMessages,TranslationPreset.Faithful)] [InlineData(Protocol.AnthropicMessages,TranslationPreset.Plain)] [InlineData(Protocol.AnthropicMessages,TranslationPreset.Professional)] [InlineData(Protocol.AnthropicMessages,TranslationPreset.Explain)]
    public async Task FourSavedModeTemplatesPreviewExactlyMatchesActualProtocol(Protocol protocol,TranslationPreset mode)
    {
        await using var rig=await Rig.Create(fixture.BaseUri,protocol);using(var draft=await rig.Coordinator.OpenDraftAsync()){draft.Update(s=>s with{Translation=s.Translation with{Preset=mode,CustomTemplates=s.Translation.CustomTemplates.SetItem(mode,"PUBLIC_"+mode+" {{target_language}} {{domain}} {{reader_background}} {{source_text}} {{image_mode}}")}});await rig.Coordinator.SaveAsync(draft);}
        var composition=PromptBuilder.Compose(rig.Settings.Data.Translation,rig.Source().Text);await rig.Start();var request=Assert.Single(await Requests(rig));Assert.Equal(composition.System,SystemPrompt(request,protocol));Assert.Equal(composition.TemplateVersion,rig.Flow.SelectedSnapshot!.TemplateVersion);Assert.StartsWith("base@1+custom-"+mode.ToString().ToLowerInvariant()+"@1:",composition.TemplateVersion);Assert.Equal(PromptBuilder.Read("base"),composition.BuiltInConstraints);Assert.DoesNotContain(rig.Source().Text,composition.System);
        using var body=JsonDocument.Parse(request.Body);string user=protocol==Protocol.OpenAIChat?body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!:body.RootElement.GetProperty("messages")[0].GetProperty("content")[0].GetProperty("text").GetString()!;Assert.Equal(composition.User,user);Assert.Equal(rig.Source().Text,user);
    }
    [Theory] [InlineData(Protocol.OpenAIChat)] [InlineData(Protocol.AnthropicMessages)]
    public async Task SavedRestartRestoreAndOldRegenerationKeepTheirActualTemplates(Protocol protocol)
    {
        await using var rig=await Rig.Create(fixture.BaseUri,protocol);await rig.Start();var old=rig.Flow.SelectedSnapshot!;var oldId=rig.Results.SelectedVersionId!.Value;byte[] original=await File.ReadAllBytesAsync(rig.Store.PrimaryPath);
        var restarted=new SettingsStore(rig.Store.DirectoryPath);var loaded=await restarted.LoadAsync();Assert.Equal(old.Settings.Data.Translation.CustomTemplates[TranslationPreset.Faithful],loaded.Data.Translation.CustomTemplates[TranslationPreset.Faithful]);Assert.Equal(original,await File.ReadAllBytesAsync(rig.Store.PrimaryPath));
        using(var draft=await rig.Coordinator.OpenDraftAsync()){var editor=new PromptTemplateEditor(()=>draft,()=>{});editor.SetText("PUBLIC_NEW_TEMPLATE {{domain}}");await rig.Coordinator.SaveAsync(draft);}
        Assert.Equal(old.Prompt,rig.Flow.SnapshotForVersion(oldId)!.Prompt);var offer=rig.Flow.PrepareRegeneration()!;Assert.Equal(old.TemplateVersion,offer.Snapshot.TemplateVersion);rig.Flow.Submit(offer);await rig.Flow.Running;Assert.Contains("PUBLIC_CUSTOM",rig.Flow.SelectedSnapshot!.Prompt);Assert.DoesNotContain("PUBLIC_NEW_TEMPLATE",rig.Flow.SelectedSnapshot.Prompt);Assert.Equal(1,rig.Flow.SelectedSnapshot.Prompt.Split(PromptBuilder.Read("regenerate")).Length-1);
        offer=rig.Flow.PrepareRegeneration(true)!;rig.Flow.Submit(offer);await rig.Flow.Running;Assert.Contains("PUBLIC_NEW_TEMPLATE",rig.Flow.SelectedSnapshot!.Prompt);Assert.NotEqual(old.TemplateVersion,rig.Flow.SelectedSnapshot.TemplateVersion);
        using(var draft=await rig.Coordinator.OpenDraftAsync()){var editor=new PromptTemplateEditor(()=>draft,()=>{});editor.RestoreDefault();await rig.Coordinator.SaveAsync(draft);}
        offer=rig.Flow.PrepareRegeneration(true)!;rig.Flow.Submit(offer);await rig.Flow.Running;Assert.Equal("base@1+faithful@1",rig.Flow.SelectedSnapshot!.TemplateVersion);Assert.DoesNotContain("PUBLIC_NEW_TEMPLATE",rig.Flow.SelectedSnapshot.Prompt);Assert.Empty((await restarted.LoadAsync()).Data.Translation.CustomTemplates);Assert.Equal(4,(await Requests(rig)).Length);Assert.Equal(3,rig.Results.Versions.Count);Assert.Equal(3,rig.Flow.RetainedSnapshotCount);
    }
    [Fact] public async Task UnsavedCustomDraftIsCancelledWithoutChangingFileOrGlobalConfig()
    {
        await using var rig=await Rig.Create(fixture.BaseUri,Protocol.OpenAIChat);byte[] before=await File.ReadAllBytesAsync(rig.Store.PrimaryPath);var effective=rig.Settings;using(var draft=await rig.Coordinator.OpenDraftAsync()){var editor=new PromptTemplateEditor(()=>draft,()=>{});editor.SetText("PUBLIC_UNSAVED_TEMPLATE");Assert.Contains("PUBLIC_UNSAVED_TEMPLATE",editor.Preview("PUBLIC_PREVIEW_SAMPLE").System);}
        Assert.Equal(before,await File.ReadAllBytesAsync(rig.Store.PrimaryPath));Assert.Equal(effective.Data.Translation.CustomTemplates[TranslationPreset.Faithful],rig.Settings.Data.Translation.CustomTemplates[TranslationPreset.Faithful]);Assert.DoesNotContain("PUBLIC_PREVIEW_SAMPLE",System.Text.Encoding.UTF8.GetString(before));Assert.Empty(await Requests(rig));
    }
    [Theory] [InlineData("copy")] [InlineData("edit")] [InlineData("restore")] [InlineData("context")]
    public async Task ActualTemplateCommandsCancelSavedProfileConnectionTestBeforeDraftMutation(string action)
    {
        await using var rig=await Rig.Create(fixture.BaseUri,Protocol.OpenAIChat,scenario:"held-headers");rig.Gate.Disable();var fixedGate=new RuntimeGate(requireProfileBinding:true);fixedGate.ObserveSettingsMetadata(rig.Settings);using var draft=await rig.Coordinator.OpenDraftAsync();var updates=new List<ApiTestUpdate>();await using var test=new ApiConnectionTestWorkflow(fixedGate,()=>rig.Settings,rig.Provider,new Destination(),(_,_,_)=>throw new InvalidOperationException("No key configured."));var task=test.Start(test.Prepare("p",out _)!,true,updates.Add)!;await Barrier(rig);
        var editor=new PromptTemplateEditor(()=>draft,test.DraftEdited);if(action=="copy")editor.CopyBuiltIn();else if(action=="edit")editor.SetText("PUBLIC_EDIT");else if(action=="restore")editor.RestoreDefault();else editor.UpdateContext(TranslationPreset.Explain,"NEW_DOMAIN","NEW_BACKGROUND");await Release(rig);await task;Assert.DoesNotContain(updates,u=>u.Event.Kind==ModelEventKind.Completed);var request=Assert.Single(await Requests(rig));Assert.Equal(ApiConnectionTestContent.System,SystemPrompt(request,Protocol.OpenAIChat));Assert.False(fixedGate.HasConsentTo(new(new Uri(rig.Settings.Data.Profiles[0].BaseUrl))));Assert.Equal(0,rig.Protector.Reads);
    }
    [Fact] public async Task ConnectionFixedSampleIgnoresEvenExplicitSavedCustomTemplate()
    {await using var rig=await Rig.Create(fixture.BaseUri,Protocol.AnthropicMessages,"PUBLIC_ATTEMPT_TO_CHANGE_TEST_CONTENT");rig.Gate.Disable();var fixedGate=new RuntimeGate(requireProfileBinding:true);fixedGate.ObserveSettingsMetadata(rig.Settings);await using var test=new ApiConnectionTestWorkflow(fixedGate,()=>rig.Settings,rig.Provider,new Destination(),(_,_,_)=>throw new InvalidOperationException("No key."));await test.Start(test.Prepare("p",out _)!,true,_=>{})!;var request=Assert.Single(await Requests(rig));Assert.Equal(ApiConnectionTestContent.System,SystemPrompt(request,Protocol.AnthropicMessages));Assert.DoesNotContain("PUBLIC_ATTEMPT_TO_CHANGE_TEST_CONTENT",request.Body);Assert.False(fixedGate.HasConsentTo(new(new Uri(rig.Settings.Data.Profiles[0].BaseUrl))));}
    [Theory] [InlineData("off")] [InlineData("consent")]
    public async Task CustomTemplateCannotGrantUploadOrStartAnyRequestByInstruction(string block)
    {
        await using var rig=await Rig.Create(fixture.BaseUri,Protocol.OpenAIChat,"PUBLIC_RULE_REQUEST_TO_UPLOAD_AND_READ_KEYS");if(block=="off")rig.Gate.Disable();else{rig.Gate.Disable();rig.Gate=new(requireProfileBinding:true);rig.Link.Dispose();rig.Link=new(rig.Coordinator,rig.Gate);rig.Link.Initialize(rig.Settings);Assert.True(rig.Gate.TryEnableLocalOperation(true));Assert.Equal(Activation.On,rig.Gate.Snapshot.Activation);await rig.Flow.DisposeAsync();rig.Flow=new(rig.Gate,()=>rig.Settings,(_,_)=>true,rig.Provider,new Destination(),new(rig.Gate),_=>{});}Assert.Null(rig.Flow.Prepare(rig.Source()));Assert.Empty(await Requests(rig));Assert.Equal(0,rig.Protector.Reads);
    }
    [Fact] public async Task PreviewIsTheSamePureComposerAndCannotReadOriginalFromWindowOrEnvironment()
    {
        await using var rig=await Rig.Create(fixture.BaseUri,Protocol.OpenAIChat,"PUBLIC $KUAIYI_PROMPT_TEMPLATE_PUBLIC_CANARY $(PUBLIC_NOT_EXECUTED) {{source_text}}");using var draft=await rig.Coordinator.OpenDraftAsync();var editor=new PromptTemplateEditor(()=>draft,()=>{});var composed=editor.Preview("PUBLIC_SAMPLE_ONLY");Assert.Equal(PromptBuilder.Compose(draft.Data.Translation,"PUBLIC_SAMPLE_ONLY"),composed);Assert.DoesNotContain(rig.Source().Text,composed.System);Assert.Contains("$KUAIYI_PROMPT_TEMPLATE_PUBLIC_CANARY $(PUBLIC_NOT_EXECUTED)",composed.System);Assert.DoesNotContain(rig.Source().Window.Executable,composed.System);Assert.Empty(await Requests(rig));Assert.Equal(0,rig.Protector.Reads);
    }
    [Fact] public async Task SavedTemplateChangeDuringFlightKeepsOldPromptAndLateEventsFrozen()
    {
        await using var rig=await Rig.Create(fixture.BaseUri,Protocol.OpenAIChat,scenario:"held-headers");var first=rig.Flow.Prepare(rig.Source())!;rig.Flow.Submit(first);var old=rig.Flow.Running;await Barrier(rig);using(var draft=await rig.Coordinator.OpenDraftAsync()){var editor=new PromptTemplateEditor(()=>draft,()=>{});editor.SetText("PUBLIC_NEW_GENERATION_TEMPLATE");await rig.Coordinator.SaveAsync(draft);}
        await Release(rig);await old;Assert.Equal(first.Snapshot.Prompt,rig.Flow.SelectedSnapshot!.Prompt);Assert.Contains("PUBLIC_CUSTOM",rig.Flow.SelectedSnapshot.Prompt);Assert.DoesNotContain("PUBLIC_NEW_GENERATION_TEMPLATE",rig.Flow.SelectedSnapshot.Prompt);Assert.All(rig.Updates,u=>Assert.Same(first.Snapshot,u.Snapshot));Assert.Single(await Requests(rig));
    }
    [Fact] public async Task PendingOfferFromBeforeSavedTemplateEditCannotSend()
    {await using var rig=await Rig.Create(fixture.BaseUri,Protocol.OpenAIChat);var offer=rig.Flow.Prepare(rig.Source())!;using(var draft=await rig.Coordinator.OpenDraftAsync()){var editor=new PromptTemplateEditor(()=>draft,()=>{});editor.SetText("PUBLIC_LATER_TEMPLATE");await rig.Coordinator.SaveAsync(draft);}Assert.Equal(TextSubmission.Rejected,rig.Flow.Submit(offer));Assert.Empty(await Requests(rig));}
    [Fact] public async Task TemplateMapReloadDeduplicatesTheSameSourceWithoutNewHttp()
    {await using var rig=await Rig.Create(fixture.BaseUri,Protocol.OpenAIChat);await rig.Start();using var reload=await rig.Coordinator.OpenDraftAsync();Assert.Equal(TextSubmission.Reopened,rig.Flow.Submit(rig.Flow.Prepare(rig.Source())!));Assert.Single(await Requests(rig));}
}
