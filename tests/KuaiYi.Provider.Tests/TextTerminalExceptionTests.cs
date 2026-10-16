using System.Security.Cryptography;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests;
public sealed class TextTerminalExceptionTests
{
    private sealed class Immediate:SynchronizationContext{public override void Post(SendOrPostCallback action,object? state)=>action(state);}
    [Theory][InlineData(Protocol.OpenAIChat,true)][InlineData(Protocol.OpenAIChat,false)][InlineData(Protocol.AnthropicMessages,true)][InlineData(Protocol.AnthropicMessages,false)]
    public async Task PreHttpSecretFailureRemainsVisibleWithoutRawExceptionOrCredentials(Protocol protocol,bool cryptographic)
    {
        var w=new ShellWindow();w.InitializeResultFlow();w.WithGeometry();int dials=0,reads=0;using var provider=new ProviderClient(null,null,null,(_,_)=>{dials++;throw new InvalidOperationException("No connection allowed.");});
        var settings=new SettingsSnapshot(new(){ActiveProfileId="terminal-exception",Profiles=[new(){Id="terminal-exception",Model="PUBLIC_NO_MODEL_CALL",BaseUrl="http://127.0.0.1:1/",AllowLoopbackHttp=true,Authentication=ApiAuthentication.Bearer,SecretRef="public-synthetic-unavailable-reference",Protocol=protocol==Protocol.OpenAIChat?KuaiYi.Core.Settings.ApiProtocol.OpenAIChat:KuaiYi.Core.Settings.ApiProtocol.AnthropicMessages}]},"public-synthetic-config",SettingsLoadState.Current);
        var gate=w.MarkdownGate;gate.ObserveSettingsMetadata(settings);var origin=new EndpointOrigin(new Uri(settings.Data.Profiles[0].BaseUrl));gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));string sentinel="PUBLIC_UNTRUSTED_EXCEPTION_<script>_NOT_A_CREDENTIAL";
        await using var flow=new TextTranslationCoordinator(gate,()=>settings,(_,_)=>true,provider,new Immediate(),w.MarkdownResults,w.CoordinatorUpdate,(_,_,_)=>{reads++;throw cryptographic?new CryptographicException(sentinel):new IOException(sentinel);},transportFactory:p=>new(protocol,new(p.BaseUrl),protocol==Protocol.OpenAIChat?"chat/completions":"messages",p.Model,Authentication.Bearer,AllowLoopbackHttp:true));w.UseTranslation(flow,settings);
        var source=new SelectionSourceSnapshot(new(17,92823,132000,@"C:\Synthetic\fixture-editor.exe"),"PUBLIC SOURCE","PUBLIC SOURCE",[new(0,0,2,2)],[1],settings.Version,13,false,SelectionSourceKind.Uia);Assert.Equal(TextSubmission.Started,flow.Submit(flow.Prepare(source)!));await flow.Running.WaitAsync(TimeSpan.FromSeconds(6));var failed=Assert.Single(flow.Results.Versions);Assert.Equal(cryptographic?TranslationFailureReason.Authentication:TranslationFailureReason.RequestIncomplete,failed.Failure);Assert.Empty(failed.Text);Assert.Equal(failed,flow.Results.LastFailure);Assert.Equal(0,dials);Assert.Equal(1,reads);Assert.False(gate.HasRunningGeneration);w.TickForTest();Assert.True(w.Frames!.IsExpanded);Assert.Contains("未完成",w.FailureNoticeFull);Assert.DoesNotContain(sentinel,w.FailureNoticeFull);Assert.DoesNotContain(sentinel,w.FailureAutomation);Assert.NotNull(flow.PrepareRegeneration());Assert.Equal(0,dials);Assert.Equal(1,reads);Assert.True(await w.ClearContentForTest());Assert.Empty(flow.Results.Versions);Assert.Equal("",w.FailureNoticeText);
    }
}
