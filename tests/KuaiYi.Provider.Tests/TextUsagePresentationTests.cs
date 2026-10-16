using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests
{
    public sealed class TextUsagePresentationTests
    {
        [Theory][InlineData(Protocol.OpenAIChat,false)][InlineData(Protocol.OpenAIChat,true)][InlineData(Protocol.AnthropicMessages,false)][InlineData(Protocol.AnthropicMessages,true)]
        public async Task FutureCaptureModeOrStyleDoesNotCancelOrRelabelAnAlreadySentVersion(Protocol protocol,bool changeStyle)
        {
            await using var r=await TextUsageRig.Create(protocol,true);var id=r.Start();await r.Requests(1);
            if(changeStyle)Assert.True(r.Flow.SetSessionStyle(TranslationPreset.Professional));else r.Gate.SetMode(TriggerMode.Drop);
            await r.Script.Write(r.StartFrames+r.UsageFrame(r.Counts(9,4)));await TextUsageRig.Wait(()=>r.Events.Any(e=>e.Presentation.Kind==OverlayResultEventKind.Usage));
            Assert.True(r.Gate.CanApply(id));Assert.True(r.Gate.HasRunningGeneration);Assert.Equal(new TranslationUsage(9,4),r.Version(id).Usage);Assert.Equal(TranslationPreset.Faithful,r.Flow.SelectedSnapshot!.Preset);Assert.Contains("忠实翻译",r.Window.ResultLabel);
            if(changeStyle)Assert.Equal(TranslationPreset.Professional,r.Flow.NextGeneration!.Preset);
            await r.Script.Write(r.EndFrames(),true);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.True(r.Version(id).Complete);Assert.Equal(1,r.Script.Requests);
        }
        [Theory][InlineData(Protocol.OpenAIChat,false)][InlineData(Protocol.OpenAIChat,true)][InlineData(Protocol.AnthropicMessages,false)][InlineData(Protocol.AnthropicMessages,true)]
        public async Task ExplicitStopPreservesAcceptedUsageWithOrWithoutBodyAndRejectsLaterMutation(Protocol protocol,bool body)
        {
            await using var r=await TextUsageRig.Create(protocol,true);var id=r.Start();await r.Requests(1);
            await r.Script.Write(r.StartFrames+r.UsageFrame(r.Counts(7,3))+(body?r.EndFrames("public partial",terminal:false):""));await TextUsageRig.Wait(()=>r.Events.Any(e=>e.Presentation.Kind==OverlayResultEventKind.Usage)&&(!body||r.Events.Any(e=>e.Presentation.Kind==OverlayResultEventKind.Delta)));
            r.Window.StopForTest();await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));var version=r.Version(id);Assert.True(version.Stopped);Assert.False(version.Complete);Assert.Equal(new TranslationUsage(7,3),version.Usage);Assert.False(r.Results.ObserveUsage(id,99,88));Assert.Equal(new TranslationUsage(7,3),r.Version(id).Usage);
            Assert.Contains("输入 token：7 · 输出 token：3",r.Window.ResultLabel);Assert.EndsWith(" · 已停止",r.Window.ResultLabel);Assert.Equal(body?"public partial":"",r.Results.Text);Assert.False(r.Gate.HasRunningGeneration);Assert.Single(r.Script.Bodies);
        }
        [Theory][InlineData(Protocol.OpenAIChat,false)][InlineData(Protocol.OpenAIChat,true)][InlineData(Protocol.AnthropicMessages,false)][InlineData(Protocol.AnthropicMessages,true)]
        public async Task StreamInterruptionOrTruncationKeepsReportedUsageWithoutClaimingCompletion(Protocol protocol,bool interrupted)
        {
            await using var r=await TextUsageRig.Create(protocol,true);var id=r.Start();await r.Requests(1);await r.Reply(r.Counts(13,6),"public partial",terminal:!interrupted,finish:interrupted?"stop":protocol==Protocol.OpenAIChat?"length":"max_tokens");
            var version=r.Version(id);Assert.True(version.Stopped);Assert.False(version.Complete);Assert.Equal(new TranslationUsage(13,6),version.Usage);Assert.Contains("输入 token：13 · 输出 token：6",r.Window.ResultLabel);Assert.False(r.Gate.HasRunningGeneration);Assert.DoesNotContain(r.Events,e=>e.Presentation.Kind==OverlayResultEventKind.Complete);Assert.Single(r.Events,e=>e.Presentation.Kind==OverlayResultEventKind.Stopped);
        }
        [Fact] public async Task F04EmptyDeltaFinishThenEmptyChoicesUsageWaitsForDoneAndDoesNotAccumulate()
        {
            await using var r=await TextUsageRig.Create(Protocol.OpenAIChat,true);var id=r.Start();await r.Requests(1);
            await r.Script.Write(r.StartFrames+r.EndFrames("public F04 body",terminal:false)+r.UsageFrame(r.Counts(20,5))+r.UsageFrame(r.Counts(20,5))+r.UsageFrame(r.OutputOnly(0)));
            await TextUsageRig.Wait(()=>r.Events.Count(e=>e.Presentation.Kind==OverlayResultEventKind.Usage)==2);Assert.Equal("public F04 body",r.Results.Text);Assert.Equal(new TranslationUsage(20,0),r.Version(id).Usage);Assert.False(r.Version(id).Complete);Assert.True(r.Gate.HasRunningGeneration);
            await r.Script.Write("data: [DONE]\n\n",true);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.True(r.Version(id).Complete);Assert.Equal(new TranslationUsage(20,0),r.Version(id).Usage);
        }
        [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
        public async Task MetadataForGeneratingVersionDoesNotOverwriteOlderSelectedLabelOrReader(Protocol protocol)
        {
            await using var r=await TextUsageRig.Create(protocol,true);var first=r.Start();await r.Requests(1);await r.Reply(r.Counts(11,1),"public first body");var second=r.Start(regenerate:true);await r.Requests(2);Assert.True(r.Results.SelectVersion(first.VersionId));r.Window.RefreshLabelForTest();r.Window.TickForTest();r.Window.DrainResultDispatchForTest();r.Window.ReaderScroll(145);
            string label=r.Window.ResultLabel;var before=(r.Window.ScrollCalls,r.Window.ScrollOffset,r.Window.ResultRevision,r.Window.FlushStarts,r.Window.Dirty,r.Window.FlushPending,r.Window.Following,r.Window.FrameRequests);
            await r.Script.Write(r.StartFrames+r.UsageFrame(r.Counts(23,8)));await TextUsageRig.Wait(()=>r.Events.Count(e=>e.Presentation.Kind==OverlayResultEventKind.Usage)==2);
            Assert.Equal(label,r.Window.ResultLabel);Assert.Equal(first.VersionId,r.Results.SelectedVersionId);Assert.Equal("public first body",r.Results.Text);Assert.Equal(before,(r.Window.ScrollCalls,r.Window.ScrollOffset,r.Window.ResultRevision,r.Window.FlushStarts,r.Window.Dirty,r.Window.FlushPending,r.Window.Following,r.Window.FrameRequests));
            Assert.Equal(new TranslationUsage(23,8),r.Version(second).Usage);Assert.Contains("输入 token：23 · 输出 token：8",r.Window.UsageVersionMenuForTest(r.Version(second)));Assert.Contains("输入 token：11 · 输出 token：1",r.Window.UsageVersionMenuForTest(r.Version(first)));
            await r.Script.Write(r.EndFrames("public second body"),true);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));
        }
        [Theory][InlineData(null,null,"Unknown（未知）","Unknown（未知）")][InlineData(null,7L,"Unknown（未知）","7")][InlineData(0L,0L,"0","0")][InlineData(1L,long.MaxValue,"1","9223372036854775807")]
        public void ExistingVersionMenuFormatterDisplaysBothFullCountsWithoutRelyingOnTrimmedLabel(long? input,long? output,string expectedInput,string expectedOutput)
        {
            var w=new ShellWindow();w.InitializeResultFlow();var id=new RequestIdentity(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1);var version=new CachedOverlayVersion(id,"public body",true,false,3,new(input,output));string label=w.UsageVersionMenuForTest(version);
            Assert.StartsWith("版本 3",label);Assert.Contains("完成\n服务端报告 · 输入 token："+expectedInput+" · 输出 token："+expectedOutput,label);Assert.DoesNotContain("public body",label);
        }
        [Theory][InlineData("complete")][InlineData("stopped")][InlineData("clear")][InlineData("cancel")][InlineData("newer")]
        public void CoreUsageUsesExactIdentityAndTerminalLifetimeAndNeverTurnsUnknownIntoZero(string terminal)
        {
            var gate=new RuntimeGate();var origin=new EndpointOrigin(new Uri("https://public.invalid/"));gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));Assert.True(gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var id,out _));var results=new OverlayResults(gate);Assert.True(results.Begin(id));
            Assert.False(results.ObserveUsage(id,null,null));Assert.Equal(TranslationUsage.Unknown,Assert.Single(results.Versions).Usage);Assert.True(results.ObserveUsage(id,8,7));var frozen=Assert.Single(results.Versions);Assert.False(results.ObserveUsage(id,8,7));Assert.False(results.ObserveUsage(id,-1,-1));Assert.True(results.ObserveUsage(id,3,0));Assert.Equal(new TranslationUsage(3,0),Assert.Single(results.Versions).Usage);Assert.Equal(new TranslationUsage(8,7),frozen.Usage);
            Assert.False(results.ObserveUsage(id with{RequestId=Guid.NewGuid()},100,100));
            switch(terminal){case "complete":Assert.True(results.Complete(id));break;case "stopped":results.MarkStopped();break;case "clear":results.Clear();break;case "cancel":gate.StopRequest(id);break;case "newer":Assert.True(gate.TryStartRequest(id.SessionId,Guid.NewGuid(),null,out var next,out _));Assert.True(results.Begin(next));break;}
            Assert.False(results.ObserveUsage(id,99,88));Assert.DoesNotContain(results.Versions,v=>v.Usage==new TranslationUsage(99,88));
        }
    }
}
namespace KuaiYi.App
{
    // The formatter is the actual linked production function used by the real version MenuItem.Header.
    internal sealed partial class ShellWindow
    {internal string UsageVersionMenuForTest(CachedOverlayVersion version)=>VersionMenuLabel(version);}
}
