using System.Text.Json;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Imaging;
using KuaiYi.Providers;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed partial class ProductionScreenshotShellTests
{
    private static async Task InitialImage(Rig r,string kind="text_only")
    {Assert.True(r.Window.SendScreenshotForTest(ImageOutputMode.TranslateText,_=>true));await r.Requests(1);await r.Reply(ImageClassificationTests.Json(kind));await r.Requests(2);await r.Reply("PUBLIC ORIGINAL IMAGE RESULT");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));r.Drain();}
    private static async Task NewImageVersion(Rig r)
    {int before=r.Script.Requests;var next=r.Window.RegenerateImageForTest(false,false,null,_=>true);await r.Requests(before+1);await r.Reply("PUBLIC NEW IMAGE VERSION");Assert.True(await next.WaitAsync(TimeSpan.FromSeconds(6)));r.Drain();}
    private static OcrOnlyCoordinator LocalImageOcr(Rig r,params SmartOcrFixture.Worker[] workers)
    {
        int index=0;
        return new(r.Window.MarkdownGate,()=>r.Config,r.Window.RetainedImageCurrentForTest,new Immediate(),()=>workers[index++]);
    }
    private static Func<Func<bool>,Func<OcrOnlyCoordinator,Task,RequestIdentity,bool>,Func<OcrOnlyCoordinator,bool,bool>,Func<OcrOnlyCoordinator,Task,bool,Task<bool>>,Task<SmartImageApproval?>> FreshImageApproval(Rig r,OcrOnlyCoordinator local,bool allowVision=true,Action? afterDiscovery=null)
        =>async(current,advance,admitted,settle)=>
        {
            if(!current()||!admitted(local,local.Discover(r.Window.RetainedImageVersionForTest!.Value,r.Image,out var identity)))return null;
            var original=local.Running;bool advanced=advance(local,original,identity);if(!await settle(local,original,advanced)||!current())return null;
            afterDiscovery?.Invoke();if(!current())return null;
            if(local.Capabilities is not {} capabilities)
                return allowVision&&local.VisionCandidate is {} candidate?local.ApproveVision(candidate):null;
            if(!admitted(local,local.Start(capabilities,"en-US",out identity)))return null;
            original=local.Running;advanced=advance(local,original,identity);if(!await settle(local,original,advanced)||!current())return null;
            return local.Preview is {} preview?local.ApproveForImage(preview):null;
        };
    [Fact]public async Task ImageUiDeploymentQualificationStaysClosedWithoutTestOverride()
    {
        await using var r=await Rig.Create();await InitialImage(r);int called=0;
        Assert.False(r.Window.RegenerationQualifiedForTest);await r.Window.QualifiedImageRegenerationForTest(()=>called++);
        Assert.Equal(0,called);Assert.Equal(2,r.Script.Requests);Assert.Contains("当前保持关闭",r.Window.RegenerationStatusForTest);
    }
    [Fact]public async Task ImageUiWorkflowSubmitsSelectedOwnerWithExplicitModeAndWaitsItsOriginalGeneration()
    {
        await using var r=await Rig.Create();await InitialImage(r);var old=r.Window.MarkdownResults.SelectedVersionId;int decisions=0;
        var request=r.Window.RegenerateImageForTest(false,false,ImageOutputMode.ExplainImage,offer=>{decisions++;Assert.Contains("一次",offer.ConfirmationText);Assert.Equal(ImageOutputMode.ExplainImage,offer.Snapshot.RequestedMode);return true;});
        var tracked=r.Window.SmartOriginalForTest;await r.Requests(3);Assert.False(request.IsCompleted);Assert.False(tracked.IsCompleted);await r.Reply("PUBLIC UI REGENERATED BODY");
        Assert.True(await request.WaitAsync(TimeSpan.FromSeconds(6)));Assert.True(tracked.IsCompletedSuccessfully);r.Drain();
        Assert.Equal(1,decisions);Assert.Equal(3,r.Script.Requests);Assert.Equal(2,r.Window.MarkdownResults.Versions.Count);Assert.Contains(r.Window.MarkdownResults.Versions,v=>v.Identity.VersionId==old&&v.Complete);
        Assert.Equal("PUBLIC UI REGENERATED BODY",r.Window.MarkdownResults.Text);Assert.Equal(ImageOutputMode.ExplainImage,r.Flow.SelectedSnapshot!.RequestedMode);Assert.Same(r.Image,r.Window.SelectedImageForTest);Assert.False(r.Image.IsDisposed);
        Assert.False(r.Window.SendScreenshotForTest(ImageOutputMode.TranslateText,_=>true));
    }
    [Fact]public async Task ImageUiExplicitRejudgeMakesTwoCallsAndRetainsTheOldReadableVersion()
    {
        await using var r=await Rig.Create();await InitialImage(r);var original=r.Flow.SelectedSnapshot!;
        var request=r.Window.RegenerateImageForTest(false,true,null,offer=>{Assert.Null(offer.Snapshot.Classification);Assert.Contains("两次",offer.ConfirmationText);return true;});
        await r.Requests(3);await r.Reply(ImageClassificationTests.Json("diagram_or_mixed"));await r.Requests(4);await r.Reply("PUBLIC REJUDGED BODY");Assert.True(await request.WaitAsync(TimeSpan.FromSeconds(6)));r.Drain();
        Assert.NotEqual(original.ClassificationUsage!.InvocationId,r.Flow.SelectedSnapshot!.ClassificationUsage!.InvocationId);Assert.False(r.Flow.SelectedSnapshot.ClassificationUsageReused);Assert.Equal(2,r.Window.MarkdownResults.Versions.Count);
    }
    [Theory][InlineData("decline")][InlineData("off")][InlineData("settings")][InlineData("clear")][InlineData("version-aba")]
    public async Task ImageUiConfirmationCannotSwitchSelectedVersionSettingsOrSource(string change)
    {
        await using var r=await Rig.Create();await InitialImage(r);if(change=="version-aba")await NewImageVersion(r);
        var selected=r.Window.MarkdownResults.SelectedVersionId!.Value;var first=r.Window.MarkdownResults.Versions[0].Identity.VersionId;int before=r.Script.Requests;
        var request=r.Window.RegenerateImageForTest(false,false,null,_=>
        {
            if(change=="off")r.Window.MarkdownGate.Disable();
            if(change=="settings"){r.Config=r.Config with{Version="PUBLIC UI CHANGED"};r.Window.ScreenshotSettingsForTest(r.Config);}
            if(change=="clear")r.Window.ClearImagePresentationForTest();
            if(change=="version-aba"){r.Window.SelectScreenshotVersionForTest(first);r.Window.SelectScreenshotVersionForTest(selected);}
            return change!="decline";
        });
        Assert.False(await request.WaitAsync(TimeSpan.FromSeconds(6)));Assert.Equal(before,r.Script.Requests);
    }
    [Theory][InlineData("off-on")][InlineData("replace")][InlineData("version-aba")]
    public async Task ImageUiOriginalYieldCannotAdoptAReplacementOrAnEpochAba(string change)
    {
        await using var r=await Rig.Create();await InitialImage(r);if(change=="version-aba")await NewImageVersion(r);int before=r.Script.Requests,decisions=0;
        var selected=r.Window.MarkdownResults.SelectedVersionId!.Value;var first=r.Window.MarkdownResults.Versions[0].Identity.VersionId;
        var queue=new SmartQueue();var previous=SynchronizationContext.Current;Task<bool> original;
        try{SynchronizationContext.SetSynchronizationContext(queue);original=r.Window.RegenerateImageForTest(false,false,null,_=>{decisions++;return true;});}
        finally{SynchronizationContext.SetSynchronizationContext(previous);}
        if(change=="off-on"){r.Window.MarkdownGate.Disable();var origin=new EndpointOrigin(new Uri("https://different-text.public.invalid"));r.Window.MarkdownGate.ConsentTo(origin);Assert.True(r.Window.MarkdownGate.TryEnable(origin,true));}
        if(change=="version-aba"){r.Window.SelectScreenshotVersionForTest(first);r.Window.SelectScreenshotVersionForTest(selected);}
        if(change=="replace")
        {var input=r.Window.ImageInputForTest();using(input.Intent)using(input.Cancellation){var image=new OwnedSourceImage(new ScreenshotPixels(new(0,0,8,8),Enumerable.Repeat((byte)255,256).ToArray()));Assert.True(input.Commit(image));r.Window.EndRestoreForTest(input.Cancellation);}}
        queue.Drain();Assert.False(await original.WaitAsync(TimeSpan.FromSeconds(6)));Assert.Equal(0,decisions);Assert.Equal(before,r.Script.Requests);
    }
    [Fact]public async Task ImageUiAutomaticWorkflowUsesFreshCapabilitiesApprovalAndSeparateSendDecision()
    {
        await using var r=await Rig.Create();await InitialImage(r);Assert.NotEqual(r.Window.RetainedImageVersionForTest,r.Window.MarkdownResults.SelectedVersionId);
        var discover=new SmartOcrFixture.Worker();var recognize=new SmartOcrFixture.Worker{Text="PUBLIC FRESH UI OCR TEXT"};await using var local=LocalImageOcr(r,discover,recognize);int decisions=0;
        var request=r.Window.RegenerateImageForTest(false,false,ImageOutputMode.Automatic,offer=>{decisions++;Assert.Equal(r.Window.RetainedImageVersionForTest,offer.Snapshot.SourceVersion);Assert.Contains("一次",offer.ConfirmationText);return true;},FreshImageApproval(r,local));
        await r.Requests(3);using(var body=JsonDocument.Parse(r.Script.Bodies.Last()))Assert.Contains(recognize.Text,body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
        await r.Reply("PUBLIC APPROVED UI BODY");Assert.True(await request.WaitAsync(TimeSpan.FromSeconds(6)));Assert.Equal(1,decisions);Assert.Equal(3,r.Script.Requests);Assert.True(r.Flow.SelectedSnapshot!.Ocr!.UsedForText);Assert.True(discover.Disposed);Assert.True(recognize.Disposed);Assert.Null(local.Preview);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ImageUiVisionContinuationStillNeedsItsOwnDecisionAndSendConfirmation(bool approveVision)
    {
        await using var r=await Rig.Create();await InitialImage(r);var discover=new SmartOcrFixture.Worker{Languages=[]};await using var local=LocalImageOcr(r,discover);int decisions=0;
        var request=r.Window.RegenerateImageForTest(false,false,ImageOutputMode.Automatic,offer=>{decisions++;Assert.Contains("没有批准 OCR 文字",offer.ConfirmationText);return true;},FreshImageApproval(r,local,approveVision));
        if(approveVision){await r.Requests(3);await r.Reply("PUBLIC EXPLICIT VISION BODY");}
        Assert.Equal(approveVision,await request.WaitAsync(TimeSpan.FromSeconds(6)));Assert.Equal(approveVision?1:0,decisions);Assert.Equal(approveVision?3:2,r.Script.Requests);
    }
    [Fact]public async Task ImageUiVersionAbaDuringFreshOcrCannotObtainNewSendAuthority()
    {
        await using var r=await Rig.Create();await InitialImage(r);await NewImageVersion(r);var selected=r.Window.MarkdownResults.SelectedVersionId!.Value;var first=r.Window.MarkdownResults.Versions[0].Identity.VersionId;int decisions=0;
        await using var local=LocalImageOcr(r,new SmartOcrFixture.Worker());
        var request=r.Window.RegenerateImageForTest(false,false,ImageOutputMode.Automatic,_=>{decisions++;return true;},FreshImageApproval(r,local,afterDiscovery:()=>{r.Window.SelectScreenshotVersionForTest(first);r.Window.SelectScreenshotVersionForTest(selected);}));
        Assert.False(await request.WaitAsync(TimeSpan.FromSeconds(6)));Assert.Equal(0,decisions);Assert.Equal(3,r.Script.Requests);
    }
    [Fact]public async Task ImageUiUnknownOriginalOcrCleanupStaysFaultedAndCannotBeSilentlyRetried()
    {
        await using var r=await Rig.Create();await InitialImage(r);var worker=new SmartOcrFixture.Worker{Finalized=false};await using var local=LocalImageOcr(r,worker);
        try
        {
            var request=r.Window.RegenerateImageForTest(false,false,ImageOutputMode.Automatic,_=>true,FreshImageApproval(r,local));await Assert.ThrowsAnyAsync<Exception>(()=>request);
            Assert.True(local.CleanupPending);Assert.True(r.Window.SmartOriginalForTest.IsFaulted);Assert.False(worker.Disposed);
            Assert.False(await r.Window.RegenerateImageForTest(false,false,null,_=>throw new InvalidOperationException("Must not retry.")));Assert.Equal(2,r.Script.Requests);
        }
        finally{worker.Finalized=true;await local.ClearAsync();}
    }
    [Fact]public async Task ImageUiBeginReentryClearWaitsOriginalOfferTaskAndDropsNoOwnedCrop()
    {
        await using var r=await Rig.Create();await InitialImage(r);Task<bool>? clear=null;
        r.Window.RegenerationObserverForTest(update=>{if(update.Kind==ProductionScreenshotEvent.Begin&&update.Snapshot.Regeneration)clear=r.Window.ClearContentForTest();});
        var request=r.Window.RegenerateImageForTest(false,false,null,_=>true);Assert.True(await request.WaitAsync(TimeSpan.FromSeconds(6)));Assert.NotNull(clear);Assert.True(await clear.WaitAsync(TimeSpan.FromSeconds(6)));
        Assert.True(r.Window.SmartOriginalForTest.IsCompletedSuccessfully);Assert.True(r.Image.IsDisposed);Assert.Empty(r.Window.MarkdownResults.Versions);Assert.Equal(2,r.Script.Requests);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task ImageUiExplicitCurrentSettingsNeverSilentlyReplacesShownVersionPreferences(bool current)
    {
        await using var r=await Rig.Create();await InitialImage(r);var original=r.Flow.SelectedSnapshot!;
        r.Config=r.Config with{Version="PUBLIC NEW UI PREFERENCES",Data=r.Config.Data with{WriteId=Guid.NewGuid(),Translation=r.Config.Data.Translation with{Preset=TranslationPreset.Plain}}};r.Window.ScreenshotSettingsForTest(r.Config);
        var request=r.Window.RegenerateImageForTest(current,false,null,offer=>{Assert.Same(current?r.Config:original.Settings,offer.Snapshot.Settings);Assert.Equal(current?TranslationPreset.Plain:original.Settings.Data.Translation.Preset,offer.RequestedPreset);return true;});
        await r.Requests(3);await r.Reply("PUBLIC CHOSEN UI PREFERENCES");Assert.True(await request.WaitAsync(TimeSpan.FromSeconds(6)));
    }
    [Fact]public async Task ImageUiExistingUnknownOcrOwnerCannotBeReportedAsSuccessfulAdmissionRejection()
    {
        await using var r=await Rig.Create();await InitialImage(r);var worker=new SmartOcrFixture.Worker{Finalized=false};await using var local=LocalImageOcr(r,worker);
        try
        {
            Assert.True(local.Discover(r.Window.RetainedImageVersionForTest!.Value,r.Image));var original=local.Running;await original.WaitAsync(TimeSpan.FromSeconds(6));Assert.True(local.CleanupPending);
            var request=r.Window.RegenerateImageForTest(false,false,ImageOutputMode.Automatic,_=>true,FreshImageApproval(r,local));await Assert.ThrowsAnyAsync<Exception>(()=>request);
            Assert.Same(original,local.Running);Assert.Equal(1,worker.Diagnoses);Assert.False(worker.Disposed);Assert.True(local.CleanupPending);Assert.True(r.Window.SmartOriginalForTest.IsFaulted);Assert.Equal(2,r.Script.Requests);
        }
        finally{worker.Finalized=true;await local.ClearAsync();}
    }
    [Theory][InlineData("off-on")][InlineData("version-aba")]
    public async Task ImageUiExternalChangeDuringOriginalRecognitionCannotBecomeAnOwnedEpochAdvance(string change)
    {
        await using var r=await Rig.Create();await InitialImage(r);if(change=="version-aba")await NewImageVersion(r);int before=r.Script.Requests;
        var selected=r.Window.MarkdownResults.SelectedVersionId!.Value;var first=r.Window.MarkdownResults.Versions[0].Identity.VersionId;
        var discovery=new SmartOcrFixture.Worker();var recognition=new SmartOcrFixture.Worker{Hold=true};await using var local=LocalImageOcr(r,discovery,recognition);
        var request=r.Window.RegenerateImageForTest(false,false,ImageOutputMode.Automatic,_=>throw new InvalidOperationException("Stale OCR must not reach confirmation."),FreshImageApproval(r,local));
        await recognition.Entered.Task.WaitAsync(TimeSpan.FromSeconds(6));
        try
        {
            if(change=="off-on"){r.Window.MarkdownGate.Disable();var origin=new EndpointOrigin(new Uri("https://different-text.public.invalid"));r.Window.MarkdownGate.ConsentTo(origin);Assert.True(r.Window.MarkdownGate.TryEnable(origin,true));}
            else{r.Window.SelectScreenshotVersionForTest(first);r.Window.SelectScreenshotVersionForTest(selected);}
        }
        finally{recognition.Release.TrySetResult();}
        Assert.False(await request.WaitAsync(TimeSpan.FromSeconds(6)));Assert.Equal(before,r.Script.Requests);Assert.False(local.CleanupPending);Assert.Null(local.Preview);Assert.True(recognition.Disposed);
    }
    [Fact]public async Task ImageUiUnknownClassificationDoesNotCancelOriginalOrStartFreshOcr()
    {
        await using var r=await Rig.Create();Assert.True(r.Window.SendScreenshotForTest(ImageOutputMode.TranslateText,_=>true));await r.Requests(1);var original=r.Flow.Running;
        Assert.False(await r.Window.RegenerateImageForTest(false,false,ImageOutputMode.Automatic,_=>throw new InvalidOperationException("Missing classification must not confirm.")));Assert.Same(original,r.Flow.Running);Assert.True(r.Window.MarkdownGate.HasRunningGeneration);Assert.Equal(1,r.Script.Requests);
        await r.Reply(ImageClassificationTests.Json());await r.Requests(2);await r.Reply("PUBLIC ORIGINAL STILL COMPLETES");await original.WaitAsync(TimeSpan.FromSeconds(6));
    }

    [Theory][InlineData("capabilities")][InlineData("vision")][InlineData("preview")]
    public async Task ImageUiSynchronousOcrAdmissionAbaStillReleasesOnlyThatOriginalOutcome(string outcome)
    {
        await using var r=await Rig.Create();await InitialImage(r);await NewImageVersion(r);var selected=r.Window.MarkdownResults.SelectedVersionId!.Value;var first=r.Window.MarkdownResults.Versions[0].Identity.VersionId;
        var discovery=new SmartOcrFixture.Worker();if(outcome=="vision")discovery.Languages=[];
        await using var local=LocalImageOcr(r,discovery,new SmartOcrFixture.Worker());CancellationTokenSource? original=null;CancellationTokenRegistration callback=default;bool changed=false;
        void Register(RequestIdentity identity)
        {
            Assert.True(r.Window.MarkdownGate.TryLinkRequestCancellation(identity,default,out original));
            callback=original!.Token.Register(()=>{changed=true;r.Window.SelectScreenshotVersionForTest(first);r.Window.SelectScreenshotVersionForTest(selected);});
        }
        if(outcome!="preview")Register(r.Window.MarkdownResults.Identity!.Value);
        try
        {
            var request=r.Window.RegenerateImageForTest(false,false,ImageOutputMode.Automatic,_=>throw new InvalidOperationException("Admission ABA must not confirm."),FreshImageApproval(r,local,afterDiscovery:outcome=="preview"?()=>Register(local.Capabilities!.Identity):null));
            Assert.False(await request.WaitAsync(TimeSpan.FromSeconds(6)));Assert.True(changed);Assert.False(local.CleanupPending);
            Assert.Null(local.Capabilities);Assert.Null(local.Preview);Assert.Null(local.VisionCandidate);Assert.Equal(3,r.Script.Requests);
        }
        finally{callback.Dispose();original?.Dispose();}
    }
}
