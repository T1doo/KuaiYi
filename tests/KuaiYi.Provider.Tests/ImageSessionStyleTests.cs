using System.Windows.Controls;
using KuaiYi.App;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed partial class ProductionScreenshotShellTests
{
    [Fact]public async Task ImageSessionStyleRemainsLocallyEditableAfterFirstImageWasConsumed()
    {
        await using var r=await Rig.Create();await InitialImage(r);var menu=r.Window.ImageSessionStyleMenuForTest();
        Assert.True(menu.IsEnabled);Assert.Equal(2,r.Script.Requests);Assert.False(r.Window.RegenerationQualifiedForTest);
    }

    private static void ChooseImageStyle(ShellWindow window,TranslationPreset preset)=>Assert.IsType<MenuItem>(window.ImageSessionStyleMenuForTest().Items[(int)preset]).Invoke();
    [Theory][InlineData(TranslationPreset.Faithful)][InlineData(TranslationPreset.Plain)][InlineData(TranslationPreset.Professional)][InlineData(TranslationPreset.Explain)]
    public async Task ImageSessionStyleEditsOnlyNextMetadataAndUsesThatStyleOnExplicitRegeneration(TranslationPreset preset)
    {
        await using var r=await Rig.Create();await InitialImage(r);var old=r.Flow.SelectedSnapshot!;var config=r.Config;string label=r.Window.ResultLabel;
        ChooseImageStyle(r.Window,preset);Assert.Equal(preset,r.Window.ImageNextForTest!.Preset);Assert.Equal(2,r.Window.ImageNextForTest.VersionNumber);Assert.Equal(preset,r.Flow.RegenerationStyle);
        Assert.Same(old,r.Flow.SelectedSnapshot);Assert.Same(config,r.Config);Assert.Equal(TranslationPreset.Faithful,r.Config.Data.Translation.Preset);Assert.Equal(label,r.Window.ResultLabel);Assert.Equal(2,r.Script.Requests);Assert.False(r.Window.RegenerationQualifiedForTest);
        var request=r.Window.RegenerateImageForTest(false,false,null,offer=>{Assert.Equal(preset,offer.RequestedPreset);return true;});await r.Requests(3);await r.Reply("PUBLIC SELECTED SESSION STYLE");Assert.True(await request.WaitAsync(TimeSpan.FromSeconds(6)));
        Assert.Equal(preset,r.Flow.SelectedSnapshot!.Plan!.Preset);Assert.Same(old,r.Flow.SnapshotForVersion(r.Window.MarkdownResults.Versions[0].Identity.VersionId));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ImageSessionStyleCurrentSettingsAreAnExplicitSeparateChoice(bool current)
    {
        await using var r=await Rig.Create();await InitialImage(r);ChooseImageStyle(r.Window,TranslationPreset.Professional);
        r.Config=r.Config with{Version="PUBLIC STYLE SETTINGS",Data=r.Config.Data with{WriteId=Guid.NewGuid(),Translation=r.Config.Data.Translation with{Preset=TranslationPreset.Plain}}};r.Window.ScreenshotSettingsForTest(r.Config);
        var request=r.Window.RegenerateImageForTest(current,false,null,offer=>{Assert.Equal(current?TranslationPreset.Plain:TranslationPreset.Professional,offer.RequestedPreset);return true;});await r.Requests(3);await r.Reply("PUBLIC CURRENT OR SESSION STYLE");Assert.True(await request.WaitAsync(TimeSpan.FromSeconds(6)));Assert.Equal(TranslationPreset.Plain,r.Config.Data.Translation.Preset);
    }
    [Fact]public async Task ImageSessionStyleChangeInvalidatesPendingOfferButDoesNotRewriteAnAdmittedJob()
    {
        await using var r=await Rig.Create();await InitialImage(r);var pending=Assert.IsType<ProductionScreenshotOffer>(r.Flow.PrepareRegeneration());ChooseImageStyle(r.Window,TranslationPreset.Plain);
        Assert.False(r.Flow.IsOfferCurrent(pending));Assert.False(r.Flow.Submit(pending,true));Assert.Equal(2,r.Script.Requests);bool edited=false;
        r.Window.RegenerationObserverForTest(update=>{if(update.Kind==ProductionScreenshotEvent.Prepared&&update.Snapshot.Regeneration){edited=true;ChooseImageStyle(r.Window,TranslationPreset.Professional);}});
        var request=r.Window.RegenerateImageForTest(false,false,null,_=>true);await r.Requests(3);await r.Reply("PUBLIC IMMUTABLE RUNNING STYLE");Assert.True(await request.WaitAsync(TimeSpan.FromSeconds(6)));
        Assert.True(edited);Assert.Equal(TranslationPreset.Plain,r.Flow.SelectedSnapshot!.Plan!.Preset);Assert.Equal(TranslationPreset.Professional,r.Window.ImageNextForTest!.Preset);
    }
    [Fact]public async Task ImageSessionStyleResetUsesTheShownVersionAndStructuredOutputStillExplains()
    {
        await using var r=await Rig.Create();await InitialImage(r);var first=r.Window.MarkdownResults.SelectedVersionId!.Value;ChooseImageStyle(r.Window,TranslationPreset.Plain);
        var request=r.Window.RegenerateImageForTest(false,false,null,_=>true);await r.Requests(3);await r.Reply("PUBLIC PLAIN VERSION");Assert.True(await request.WaitAsync(TimeSpan.FromSeconds(6)));var second=r.Window.MarkdownResults.SelectedVersionId!.Value;
        ChooseImageStyle(r.Window,TranslationPreset.Professional);Assert.IsType<MenuItem>(r.Window.ImageSessionStyleMenuForTest().Items[^1]).Invoke();Assert.Null(r.Flow.RegenerationStyle);Assert.Equal(TranslationPreset.Plain,r.Window.ImageNextForTest!.Preset);
        r.Window.SelectScreenshotVersionForTest(first);Assert.Equal(TranslationPreset.Faithful,r.Window.ImageNextForTest!.Preset);ChooseImageStyle(r.Window,TranslationPreset.Plain);r.Window.SelectScreenshotVersionForTest(second);Assert.Equal(TranslationPreset.Plain,r.Window.ImageNextForTest!.Preset);
        request=r.Window.RegenerateImageForTest(false,false,ImageOutputMode.ExplainImage,offer=>{Assert.Equal(TranslationPreset.Plain,offer.RequestedPreset);return true;});await r.Requests(4);await r.Reply("PUBLIC STRUCTURED EXPLANATION");Assert.True(await request.WaitAsync(TimeSpan.FromSeconds(6)));Assert.Equal(TranslationPreset.Explain,r.Flow.SelectedSnapshot!.Plan!.Preset);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ImageSessionStyleStaleMenuCannotChangeAClearedOrReplacedSource(bool replace)
    {
        await using var r=await Rig.Create();await InitialImage(r);ChooseImageStyle(r.Window,TranslationPreset.Explain);var stale=r.Window.ImageSessionStyleMenuForTest();long revision=r.Flow.RegenerationStyleRevision;
        if(replace){var input=r.Window.ImageInputForTest();using(input.Intent)using(input.Cancellation){var image=new KuaiYi.Imaging.OwnedSourceImage(new KuaiYi.Imaging.ScreenshotPixels(new(0,0,8,8),Enumerable.Repeat((byte)255,256).ToArray()));Assert.True(input.Commit(image));r.Window.EndRestoreForTest(input.Cancellation);}}
        else Assert.True(await r.Window.ClearContentForTest());
        Assert.IsType<MenuItem>(stale.Items[(int)TranslationPreset.Professional]).Invoke();Assert.Null(r.Flow.RegenerationStyle);Assert.Null(r.Window.ImageNextForTest);Assert.True(r.Flow.RegenerationStyleRevision>revision);Assert.Equal(2,r.Script.Requests);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ImageSessionStylePrivacyStateRefusesLocalStaleCallbacks(bool locked)
    {
        await using var r=await Rig.Create();await InitialImage(r);var id=r.Window.MarkdownResults.SelectedVersionId!.Value;var stale=r.Window.ImageSessionStyleMenuForTest();long revision=r.Flow.RegenerationStyleRevision;
        if(locked)r.Window.MarkdownGate.SetSessionLocked(true);else r.Window.MarkdownGate.Disable(hide:true);
        Assert.False(r.Flow.SetRegenerationStyle(id,TranslationPreset.Plain));Assert.IsType<MenuItem>(stale.Items[(int)TranslationPreset.Plain]).Invoke();Assert.Null(r.Window.ImageNextForTest);Assert.Equal(revision,r.Flow.RegenerationStyleRevision);Assert.Equal(2,r.Script.Requests);
    }
    [Fact]public async Task ImageSessionStyleRevisionStopsAnOlderOcrIntentWithoutCancellingTheOriginalOwner()
    {
        await using var r=await Rig.Create();await InitialImage(r);var worker=new SmartOcrFixture.Worker{Hold=true};await using var local=LocalImageOcr(r,new SmartOcrFixture.Worker(),worker);
        var request=r.Window.RegenerateImageForTest(false,false,ImageOutputMode.Automatic,_=>throw new InvalidOperationException("Old style intent must not confirm."),FreshImageApproval(r,local));await worker.Entered.Task.WaitAsync(TimeSpan.FromSeconds(6));
        ChooseImageStyle(r.Window,TranslationPreset.Professional);Assert.False(worker.Cancellation.IsCancellationRequested);worker.Release.TrySetResult();
        Assert.False(await request.WaitAsync(TimeSpan.FromSeconds(6)));Assert.Equal(2,r.Script.Requests);Assert.Null(local.Preview);Assert.False(local.CleanupPending);Assert.Equal(TranslationPreset.Professional,r.Window.ImageNextForTest!.Preset);
    }
}
