using System.Collections.Immutable;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed class EnableModelDisclosureTests
{
    private sealed class Protector:ISecretProtector
    {
        public int Reads;
        public byte[] Protect(ReadOnlySpan<byte> value)=>throw new InvalidOperationException("No secret creation in public fixture.");
        public byte[] Unprotect(ReadOnlySpan<byte> value){Reads++;throw new InvalidOperationException("Enable disclosure must not read secrets.");}
    }
    private sealed class DirectoryPolicy:ISecretDirectoryPolicy{public void EnsurePrivate(string directory)=>Directory.CreateDirectory(directory);}
    private sealed class Fixture:IDisposable
    {
        public readonly string Root=Path.Combine(Path.GetTempPath(),"KuaiYi.EnableDisclosureFixture."+Guid.NewGuid().ToString("N"));
        public readonly Protector Secrets=new();public readonly SettingsCoordinator Settings;public ShellLifecycle Shell{get;private set;}=null!;
        public Fixture(){Settings=new(new SettingsStore(Path.Combine(Root,"config")),new SecretFileStore(Path.Combine(Root,"secrets"),Secrets,new DirectoryPolicy()));}
        public async Task Initialize(TriggerMode mode,ApiProtocol protocol)
        {
            using(var draft=await Settings.OpenDraftAsync())
            {draft.Update(s=>s with{ActiveProfileId="public",TriggerMode=mode,Profiles=[new(){Id="public",Name="public name must not replace model",BaseUrl="https://SYNTHETIC.INVALID:443/proxy/v1",Protocol=protocol,Authentication=ApiAuthentication.None,Model="synthetic-model.公开<&>"}]});await Settings.SaveAsync(draft);}
            Shell=new(Settings);await Shell.InitializeAsync();
        }
        public EnableIntent Prepare(){Assert.True(Shell.TryPrepareEnable(out var intent,out var error),error);return intent!;}
        public async Task Save(Func<ApiProfile,ApiProfile> change)
        {using var draft=await Settings.OpenDraftAsync();draft.Update(s=>s with{Profiles=s.Profiles.Select(change).ToImmutableArray()});await Settings.SaveAsync(draft);}
        public void Dispose(){Shell?.Dispose();Directory.Delete(Root,true);}
    }
    // Reflection intentionally keeps the exact contract tests executable against the parent Core binary.
    private static string Field(EnableIntent intent,string name)
    {var property=typeof(EnableIntent).GetProperty(name);Assert.NotNull(property);return Assert.IsType<string>(property.GetValue(intent));}
    [Theory][InlineData(TriggerMode.Selection,ApiProtocol.OpenAIChat)][InlineData(TriggerMode.Drop,ApiProtocol.OpenAIChat)][InlineData(TriggerMode.Selection,ApiProtocol.AnthropicMessages)][InlineData(TriggerMode.Drop,ApiProtocol.AnthropicMessages)]
    public async Task ActualSavedProfileProducesFrozenModelAndCompleteEnableExplanation(TriggerMode mode,ApiProtocol protocol)
    {
        using var f=new Fixture();await f.Initialize(mode,protocol);var intent=f.Prepare();
        Assert.Equal("synthetic-model.公开<&>",Field(intent,"Model"));
        string text=Field(intent,"ConfirmationText");Assert.Contains("当前模型：synthetic-model.公开<&>",text);Assert.Contains("https://synthetic.invalid",text);Assert.DoesNotContain("/proxy/v1",text);
        Assert.Contains(mode==TriggerMode.Selection?"选中文字":"拖放文字",text);Assert.Contains(mode==TriggerMode.Selection?"选中后将发送所选内容":"投放后将发送投放的文字",text);Assert.DoesNotContain("public name must not replace model",text);
        Assert.Equal(Activation.Off,f.Shell.Gate.Snapshot.Activation);Assert.False(f.Shell.Gate.HasConsentTo(intent.Origin));Assert.Equal(0,f.Secrets.Reads);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task DraftEditAndSavedModelChangeCannotRewriteFrozenExplanation(bool commit)
    {
        using var f=new Fixture();await f.Initialize(TriggerMode.Selection,ApiProtocol.OpenAIChat);var before=f.Prepare();string explanation=before.ConfirmationText;
        using(var draft=await f.Settings.OpenDraftAsync())
        {draft.Update(s=>s with{Profiles=s.Profiles.Select(p=>p with{Model="next-public-model"}).ToImmutableArray()});if(commit)await f.Settings.SaveAsync(draft);}
        var current=f.Prepare();Assert.Equal(explanation,before.ConfirmationText);Assert.Equal("synthetic-model.公开<&>",before.Model);Assert.Equal(commit?"next-public-model":before.Model,current.Model);
        Assert.False(f.Shell.Gate.HasConsentTo(current.Origin));Assert.Equal(Activation.Off,f.Shell.Gate.Snapshot.Activation);
        Assert.Equal(!commit,f.Shell.ConfirmEnable(before,true));
        if(commit){Assert.Equal(Activation.Off,f.Shell.Gate.Snapshot.Activation);Assert.True(f.Shell.ConfirmEnable(current,true));}
        Assert.Equal(0,f.Secrets.Reads);
    }
    [Theory][InlineData("decline")][InlineData("hide")][InlineData("lock")][InlineData("settings")][InlineData("dispose")]
    public async Task DeclinedOrInvalidatedExplanationCannotGrantConsent(string action)
    {
        using var f=new Fixture();await f.Initialize(TriggerMode.Drop,ApiProtocol.AnthropicMessages);var intent=f.Prepare();string text=intent.ConfirmationText;
        switch(action){case "hide":f.Shell.Hide();break;case "lock":f.Shell.SetSessionLocked(true);break;case "settings":await f.Save(p=>p with{Model="other-public-model"});break;case "dispose":f.Shell.Dispose();break;}
        Assert.False(f.Shell.ConfirmEnable(intent,action!="decline"));Assert.Equal(Activation.Off,f.Shell.Gate.Snapshot.Activation);Assert.False(f.Shell.Gate.HasConsentTo(intent.Origin));Assert.Equal(text,intent.ConfirmationText);Assert.Equal(0,f.Secrets.Reads);
    }
    [Fact]public async Task SameOriginModelChangeKeepsExistingSessionConsentButRejectsOldIntent()
    {
        using var f=new Fixture();await f.Initialize(TriggerMode.Selection,ApiProtocol.OpenAIChat);var original=f.Prepare();Assert.True(f.Shell.ConfirmEnable(original,true));f.Shell.Gate.Disable();var pending=f.Prepare();
        await f.Save(p=>p with{Model="new-public-model"});var current=f.Prepare();Assert.Equal(original.Origin,current.Origin);Assert.True(f.Shell.Gate.HasConsentTo(current.Origin));Assert.False(f.Shell.ConfirmEnable(pending,true));Assert.Equal(Activation.Off,f.Shell.Gate.Snapshot.Activation);
        Assert.Equal("new-public-model",current.Model);Assert.Contains("当前模型：new-public-model",current.ConfirmationText);Assert.True(f.Shell.ConfirmEnable(current,true));Assert.Equal(0,f.Secrets.Reads);
    }
    [Theory][InlineData("https://different-synthetic.invalid",false)][InlineData("http://synthetic.invalid",true)]
    public async Task NewOriginOrInsecureSchemeStillNeedsItsOwnConsent(string destination,bool insecure)
    {
        using var f=new Fixture();await f.Initialize(TriggerMode.Selection,ApiProtocol.OpenAIChat);var old=f.Prepare();Assert.True(f.Shell.ConfirmEnable(old,true));f.Shell.Gate.Disable();
        await f.Save(p=>p with{BaseUrl=destination,AllowInsecureHttp=insecure,Model="new-public-model"});var current=f.Prepare();Assert.NotEqual(old.Origin,current.Origin);Assert.True(f.Shell.Gate.HasConsentTo(old.Origin));Assert.False(f.Shell.Gate.HasConsentTo(current.Origin));Assert.Contains(destination,current.ConfirmationText);
        Assert.False(f.Shell.ConfirmEnable(old,true));Assert.False(f.Shell.ConfirmEnable(current,false));Assert.False(f.Shell.Gate.HasConsentTo(current.Origin));Assert.Equal(Activation.Off,f.Shell.Gate.Snapshot.Activation);Assert.Equal(0,f.Secrets.Reads);
    }
    [Fact]public async Task DisclosureDoesNotExposeSecretReferencesAndRestartDoesNotRestoreConsent()
    {
        using var f=new Fixture();await f.Initialize(TriggerMode.Drop,ApiProtocol.OpenAIChat);string reference="key/"+new string('a',32);await f.Save(p=>p with{Authentication=ApiAuthentication.Bearer,SecretRef=reference});var intent=f.Prepare();
        Assert.DoesNotContain(reference,intent.ConfirmationText);Assert.DoesNotContain("key/",intent.ConfirmationText);Assert.True(f.Shell.ConfirmEnable(intent,true));
        using var restart=new ShellLifecycle(f.Settings);await restart.InitializeAsync();Assert.True(restart.TryPrepareEnable(out var next,out var error),error);Assert.Equal(intent.Model,next!.Model);Assert.False(restart.Gate.HasConsentTo(next.Origin));Assert.Equal(Activation.Off,restart.Gate.Snapshot.Activation);Assert.Equal(0,f.Secrets.Reads);
    }
    [Fact]public async Task MaximumValidModelIsDisplayedWithoutTruncationOrMarkupInterpretation()
    {
        using var f=new Fixture();await f.Initialize(TriggerMode.Selection,ApiProtocol.OpenAIChat);string model=new('译',256);await f.Save(p=>p with{Model=model});var intent=f.Prepare();Assert.Equal(model,intent.Model);Assert.Contains("当前模型："+model+"\n",intent.ConfirmationText);Assert.Equal(0,f.Secrets.Reads);
    }
}
