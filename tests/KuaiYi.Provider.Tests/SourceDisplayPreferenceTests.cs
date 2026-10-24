using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;
namespace KuaiYi.Provider.Tests;

// Native-property/event ports and real offline coordinators; not WPF pixels or physical input.
public sealed class SourceDisplayPreferenceTests
{
    private static Expander Source(RichTextBox host)=>Assert.IsType<Expander>(Assert.IsType<BlockUIContainer>(host.Document.Blocks[^1]).Child);
    private static TextBox Text(Expander source)=>Assert.IsType<TextBox>(source.Content);
    [Fact]public void InitialPreferenceDoesNotBecomeReaderIntentOrOverrideManualFoldAcrossRefreshAndRetirement()
    {
        var host=new RichTextBox();int reads=0,changes=0;var view=new MarkdownFlowView(host,()=>reads++,sourceChanged:()=>changes++);var id=Guid.NewGuid();view.RetainTextSourceChoices(new HashSet<Guid>{id});
        view.SetSource(id,"PUBLIC RAW",initialExpanded:true);var source=Source(host);Assert.True(source.IsExpanded);Assert.Equal("PUBLIC RAW",Text(source).Text);Assert.Equal(0,reads);Assert.Equal(0,changes);Assert.Equal(0,source.BringIntoViewCalls);
        source.IsExpanded=false;Assert.Equal(1,reads);Assert.Equal(1,changes);
        view.SetSource(id,"PUBLIC RAW",initialExpanded:true);Assert.Same(source,Source(host));Assert.False(source.IsExpanded);
        view.ClearSource();Assert.Null(source.Content);view.SetSource(id,"PUBLIC RAW",initialExpanded:true);Assert.False(Source(host).IsExpanded);Assert.Empty(Text(Source(host)).Text);view.Clear();
    }
    [Fact]public void ChoicesBelongOnlyToRetainedVersionsAndClearDropsThem()
    {
        var host=new RichTextBox();var view=new MarkdownFlowView(host);var a=Guid.NewGuid();var b=Guid.NewGuid();view.RetainTextSourceChoices(new HashSet<Guid>{a,b});
        view.SetSource(a,"A",initialExpanded:true);var old=Source(host);old.IsExpanded=false;Action late=old.CaptureExpand();
        view.SetSource(b,"B",initialExpanded:true);Assert.True(Source(host).IsExpanded);late();view.SetSource(a,"A",initialExpanded:true);Assert.False(Source(host).IsExpanded);
        view.RetainTextSourceChoices(new HashSet<Guid>{b});view.ClearSource();view.RetainTextSourceChoices(new HashSet<Guid>{a});view.SetSource(a,"A",initialExpanded:true);Assert.True(Source(host).IsExpanded);
        Source(host).IsExpanded=false;view.Clear();view.RetainTextSourceChoices(new HashSet<Guid>{a});view.SetSource(a,"A",initialExpanded:true);Assert.True(Source(host).IsExpanded);view.Clear();
    }
    [Fact]public void DeniedInitializationDoesNotConsumeTheLaterReadableInitialPreference()
    {
        bool readable=false;var host=new RichTextBox();var id=Guid.NewGuid();var view=new MarkdownFlowView(host,canReadSource:_=>readable);view.RetainTextSourceChoices(new HashSet<Guid>{id});
        view.SetSource(id,"PUBLIC RAW",initialExpanded:true);Assert.Empty(host.Document.Blocks);
        readable=true;view.SetSource(id,"PUBLIC RAW",initialExpanded:true);Assert.True(Source(host).IsExpanded);Assert.Equal("PUBLIC RAW",Text(Source(host)).Text);view.Clear();
    }
    [Fact]public void ProgrammaticExpansionRechecksTheReadGateAfterReentrantExpansion()
    {
        bool readable=true;using var region=new MarkdownSourceRegion("PUBLIC RAW",null,null,()=>readable);
        region.View.Expanded+=(_,_)=>readable=false;region.SetExpanded(true);Assert.False(region.View.IsExpanded);Assert.Empty(region.Text.Text);
    }
    [Fact]public void DeniedManualExpansionDoesNotLeaveARegionThatConsumesItsRetainedChoice()
    {
        bool readable=true;var host=new RichTextBox();var id=Guid.NewGuid();var view=new MarkdownFlowView(host,canReadSource:_=>readable);view.RetainTextSourceChoices(new HashSet<Guid>{id});
        view.SetSource(id,"PUBLIC RAW");Source(host).IsExpanded=true;view.ClearSource();readable=false;
        view.SetSource(id,"PUBLIC RAW");Assert.Empty(host.Document.Blocks);
        readable=true;view.SetSource(id,"PUBLIC RAW");Assert.True(Source(host).IsExpanded);Assert.Equal("PUBLIC RAW",Text(Source(host)).Text);view.Clear();
    }
    [Fact]public void RevokedDuringInitializationRetiresOnlyThatRegionAndAllowsLaterReadableInitialization()
    {
        bool readable=true,attached=false;var host=new RichTextBox();var id=Guid.NewGuid();var view=new MarkdownFlowView(host,canReadSource:_=>
        {
            if(!attached&&host.Document.Blocks.LastOrDefault() is BlockUIContainer b&&b.Child is Expander source)
            {attached=true;source.Expanded+=(_,_)=>readable=false;}
            return readable;
        });view.RetainTextSourceChoices(new HashSet<Guid>{id});
        view.SetSource(id,"PUBLIC RAW",initialExpanded:true);Assert.Empty(host.Document.Blocks);
        readable=true;view.SetSource(id,"PUBLIC RAW",initialExpanded:true);Assert.True(Source(host).IsExpanded);Assert.Equal("PUBLIC RAW",Text(Source(host)).Text);view.Clear();
    }
    [Fact]public void ReentrantInitializationCannotRetireANewerInstalledVersion()
    {
        var host=new RichTextBox();var a=Guid.NewGuid();var b=Guid.NewGuid();bool attached=false;MarkdownFlowView? view=null;
        view=new(host,canReadSource:id=>
        {
            if(id==a&&!attached&&host.Document.Blocks.LastOrDefault() is BlockUIContainer block&&block.Child is Expander source)
            {attached=true;source.Expanded+=(_,_)=>view!.SetSource(b,"PUBLIC B",initialExpanded:true);}
            return true;
        });view.RetainTextSourceChoices(new HashSet<Guid>{a,b});view.SetSource(a,"PUBLIC A",initialExpanded:true);
        Assert.True(Source(host).IsExpanded);Assert.Equal("PUBLIC B",Text(Source(host)).Text);Assert.Single(host.Document.Blocks);view.Clear();
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ActualSelectedSnapshotUsesItsPreferenceInsteadOfLiveSettingsAndDoesNotSendAgain(bool preferred)
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig(preferred);var w=rig.Window;var id=await rig.Start(rawSource:"PUBLIC RAW");
        Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"PUBLIC RESULT")));int sends=rig.ConnectionAttempts;
        w.UseTranslation(rig.Flow,rig.Settings with{Data=rig.Settings.Data with{Overlay=rig.Settings.Data.Overlay with{ShowSource=!preferred}}});w.RenderMarkdown();
        Assert.Equal(preferred,Source(w.MarkdownBody).IsExpanded);Assert.Equal(preferred?"PUBLIC RAW":"",Text(Source(w.MarkdownBody)).Text);Assert.Equal(preferred,Source(w.MarkdownMeasurement).IsExpanded);Assert.True(w.Following);
        Assert.Equal(0,Source(w.MarkdownBody).BringIntoViewCalls);Assert.Equal(0,Source(w.MarkdownMeasurement).BringIntoViewCalls);
        if(preferred){Source(w.MarkdownBody).IsExpanded=false;w.RenderMarkdown();Assert.False(Source(w.MarkdownBody).IsExpanded);Assert.False(Source(w.MarkdownMeasurement).IsExpanded);w.CollapseForTest();w.RenderMarkdown();Assert.False(Source(w.MarkdownBody).IsExpanded);}
        Assert.Equal(id.VersionId,w.MarkdownResults.SelectedVersionId);Assert.Equal("PUBLIC RESULT",w.MarkdownResults.Text);Assert.Equal(sends,rig.ConnectionAttempts);
    }
    [Theory][InlineData("locked")][InlineData("hidden")][InlineData("exiting")]
    public async Task AutomaticPreferenceDoesNotRevealOrWakeAnUnavailableWindow(string state)
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig(true);var w=rig.Window;var id=await rig.Start();Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"PUBLIC RESULT")));w.RenderMarkdown();var old=Source(w.MarkdownBody);var text=Text(old);int sends=rig.ConnectionAttempts;
        if(state=="locked")w.Lock();else if(state=="hidden")w.LinkHidden();else w.Exit();w.RenderMarkdown();
        Assert.DoesNotContain(w.MarkdownBody.Document.Blocks,b=>b is BlockUIContainer block&&block.Child is Expander);Assert.Empty(text.Text);Assert.Null(old.Content);Assert.Equal(sends,rig.ConnectionAttempts);
        if(state=="hidden")Assert.Equal(Presentation.Hidden,w.MarkdownGate.Snapshot.Presentation);
    }
    private sealed class NoSecrets:ISecretProtector
    {public byte[] Protect(ReadOnlySpan<byte> value)=>throw new InvalidOperationException("No keys in this fixture.");public byte[] Unprotect(ReadOnlySpan<byte> value)=>throw new InvalidOperationException("No keys in this fixture.");}
    private sealed class PrivateFixtureDirectory:ISecretDirectoryPolicy{public void EnsurePrivate(string path)=>Directory.CreateDirectory(path);}
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ExistingSettingsTransactionCancelsOrAtomicallySavesThePreference(bool failBeforePublish)
    {
        string root=Path.Combine(Path.GetTempPath(),"KuaiYi.SourcePreference."+Guid.NewGuid().ToString("N"));bool fail=false;
        var store=new SettingsStore(Path.Combine(root,"config")){WriteStageForTest=stage=>{if(fail&&stage==SettingsWriteStage.BeforePublish)throw new IOException("PUBLIC synthetic before publish.");}};
        var coordinator=new SettingsCoordinator(store,new(Path.Combine(root,"secrets"),new NoSecrets(),new PrivateFixtureDirectory()));
        try
        {
            using(var seed=await coordinator.OpenDraftAsync()){Assert.False(seed.Data.Overlay.ShowSource);await coordinator.SaveAsync(seed);}
            byte[] before=await File.ReadAllBytesAsync(store.PrimaryPath);
            using(var cancelled=await coordinator.OpenDraftAsync()){cancelled.Update(s=>s with{Overlay=s.Overlay with{ShowSource=true}});Assert.False(coordinator.Effective!.Data.Overlay.ShowSource);}
            Assert.Equal(before,await File.ReadAllBytesAsync(store.PrimaryPath));using var draft=await coordinator.OpenDraftAsync();Assert.False(draft.Data.Overlay.ShowSource);draft.Update(s=>s with{Overlay=s.Overlay with{ShowSource=true}});fail=failBeforePublish;
            if(fail){await Assert.ThrowsAsync<IOException>(()=>coordinator.SaveAsync(draft));Assert.Equal(before,await File.ReadAllBytesAsync(store.PrimaryPath));Assert.False(coordinator.Effective!.Data.Overlay.ShowSource);}
            else{await coordinator.SaveAsync(draft);Assert.True((await new SettingsStore(store.DirectoryPath).LoadAsync()).Data.Overlay.ShowSource);Assert.True(coordinator.Effective!.Data.Overlay.ShowSource);}
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
