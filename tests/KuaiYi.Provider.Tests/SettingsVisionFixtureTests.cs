using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
using Answer=System.Windows.MessageBoxResult;
namespace KuaiYi.Provider.Tests;
public sealed class SettingsVisionFixtureTests
{
    [Theory][MemberData(nameof(VisionFixtureWorkflowTests.SuccessCases),MemberType=typeof(VisionFixtureWorkflowTests))]
    public async Task RealButtonCommandConfirmsBuiltinImageAndReportsIndependentUnknownCapabilities(Protocol protocol,bool stream,Capability capability)
    {
        await using var r=await VisionFixtureRig.Create(protocol,stream,capability);r.Window.ConfirmApi=()=>Answer.OK;r.Job=r.Window.InvokeApiForTest();await r.Requests(1);
        Assert.Equal((false,false,true),r.Window.ApiButtons);Assert.Equal(1,r.Window.ApiConfirmations);Assert.Contains(ApiVisionTestContent.Description,r.Window.LastApiConfirmation);Assert.Contains(r.Prefix.AbsoluteUri,r.Window.LastApiConfirmation);Assert.Contains("不获取选区",r.Window.LastApiConfirmation);
        await r.Reply();Assert.Contains("PUBLIC FIXTURE",r.Window.ApiOutput);Assert.Contains("认证：无需认证（未验证）",r.Window.ApiStatus);Assert.Contains("已收到有效协议事件",r.Window.ApiStatus);Assert.Contains("普通输出：已收到文字",r.Window.ApiStatus);Assert.Contains("视觉能力：Unknown",r.Window.ApiStatus);Assert.Contains("请求完成",r.Window.ApiStatus);Assert.Contains(stream?"实际响应形式和流式能力未知":"请求非流式",r.Window.ApiStatus);Assert.Equal("服务返回用量：输入 14，输出 3",r.Window.ApiUsage);Assert.Equal((true,true,false),r.Window.ApiButtons);Assert.Equal(capability,r.Coordinator.Effective!.Data.Profiles[0].Capabilities.Vision);Assert.Equal(ApiVisionTestContent.CreateImage(),r.Wire().Image);Assert.Equal(0,r.SecretReads);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task RequestedStreamWithActualJsonFallbackNeverClaimsVerifiedStreaming(Protocol protocol)
    {
        await using var r=await VisionFixtureRig.Create(protocol,true);r.Script.Mime="application/json";r.Window.ConfirmApi=()=>Answer.OK;r.Job=r.Window.InvokeApiForTest();await r.Requests(1);await r.Script.Write(r.Response(),true);await r.Job.WaitAsync(TimeSpan.FromSeconds(6));Assert.Contains("实际响应形式和流式能力未知",r.Window.ApiStatus);Assert.Contains("请求完成",r.Window.ApiStatus);Assert.DoesNotContain("已收到 SSE",r.Window.ApiStatus);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task EmptyCompletedResponseDoesNotProveOutputOrVision(Protocol protocol)
    {
        await using var r=await VisionFixtureRig.Create(protocol);r.Window.ConfirmApi=()=>Answer.OK;r.Job=r.Window.InvokeApiForTest();await r.Requests(1);await r.Script.Write(r.Response(""),true);await r.Job.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal("",r.Window.ApiOutput);Assert.Contains("普通输出：未知",r.Window.ApiStatus);Assert.Contains("视觉能力：Unknown",r.Window.ApiStatus);Assert.Contains("请求完成",r.Window.ApiStatus);
    }
    [Theory][InlineData("cancel")][InlineData("edit")][InlineData("close")][InlineData("reentry-cancel")][InlineData("reentry-send")]
    public async Task ModalCommandHasOneOwnerAndRevocationCannotSend(string action)
    {
        await using var r=await VisionFixtureRig.Create();Task? nested=null;
        r.Window.ConfirmApi=()=>{if(action=="edit")r.Window.EditApiForTest();if(action=="close")r.Window.CloseApiForTest();if(action.StartsWith("reentry"))nested=r.Window.InvokeApiForTest(false);return action is "cancel" or "reentry-cancel"?Answer.Cancel:Answer.OK;};
        r.Job=r.Window.InvokeApiForTest();if(action=="reentry-send"){await r.Requests(1);await r.Reply();}else await r.Job;
        if(nested!=null)await nested;Assert.Equal(1,r.Window.ApiConfirmations);Assert.Equal(1,r.Window.CaptureCalls);Assert.Equal(action=="reentry-send"?1:0,r.Script.Requests);Assert.Equal(0,r.SecretReads);
        if(action=="close"){await r.Window.InvokeApiForTest();Assert.Equal(1,r.Window.ApiConfirmations);Assert.Equal(0,r.Script.Requests);}
    }
    [Theory][InlineData("model")][InlineData("pendingkey")][InlineData("no-boundary")]
    public async Task UnapprovedOrUnsavedConfigurationBlocksBeforeConfirmation(string change)
    {
        await using var r=await VisionFixtureRig.Create(boundary:change!="no-boundary");if(change=="model")r.Draft.Update(s=>s with{Profiles=[s.Profiles[0] with{Model="CHANGED_PUBLIC_TEST"}]});if(change=="pendingkey")r.Draft.SetKey("p",System.Text.Encoding.UTF8.GetBytes("PUBLIC_TEST_ONLY"));r.Window.ConfirmApi=()=>Answer.OK;await r.Window.InvokeApiForTest();Assert.Equal(0,r.Window.ApiConfirmations);Assert.Equal(0,r.Script.Requests);Assert.Equal(0,r.SecretReads);Assert.Contains(change=="no-boundary"?"没有发送":"请先保存",r.Window.ApiStatus);
    }
    [Theory][InlineData("stop")][InlineData("edit")][InlineData("select")][InlineData("close")]
    public async Task BusyCommandAndCancellationCannotPublishLateBodyOrRestoreClosedView(string action)
    {
        await using var r=await VisionFixtureRig.Create(headers:false);r.Window.ConfirmApi=()=>Answer.OK;r.Job=r.Window.InvokeApiForTest();await r.Requests(1);await r.Window.InvokeApiForTest(false);Assert.Equal(1,r.Window.ApiConfirmations);
        if(action=="stop")r.Window.StopApiForTest();if(action=="edit")r.Window.EditApiForTest();if(action=="select")r.Window.SelectApiForTest(null);if(action=="close")r.Window.CloseApiForTest();await r.Job.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal("",r.Window.ApiOutput);Assert.Equal(1,r.Script.Requests);Assert.DoesNotContain("请求完成",r.Window.ApiStatus);if(action!="close")Assert.Equal((true,true,false),r.Window.ApiButtons);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task TextButtonRetainsTheOriginalTextOnlyContract(Protocol protocol)
    {
        await using var r=await VisionFixtureRig.Create(protocol);r.Window.ConfirmApi=()=>Answer.OK;r.Job=r.Window.InvokeApiForTest(false);await r.Requests(1);await r.Reply();var wire=r.Wire();Assert.Null(wire.Image);Assert.Equal(ApiConnectionTestContent.Source,wire.Source);Assert.Equal(ApiConnectionTestContent.System,wire.System);Assert.Contains("固定内容文字连接测试",r.Window.LastApiTitle);Assert.Contains("本地测试服务返回完成",r.Window.ApiStatus);Assert.Equal(0,r.SecretReads);
    }
}
