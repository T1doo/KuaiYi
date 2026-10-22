using System.Text.Json;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Imaging;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests;

public sealed partial class ProductionScreenshotTranslationTests
{
    private static OcrOnlyCoordinator Local(Rig r,SmartOcrFixture.Worker worker,SynchronizationContext? destination=null)
        =>SmartOcrFixture.Create(r.Gate,()=>r.Settings,r.Version,r.Image,(version,id)=>r.Current&&version==r.Version&&id==r.Image.SourceId&&!r.Image.IsDisposed,worker,destination);
    private static async Task<OcrImageApproval> Recognize(Rig r,OcrOnlyCoordinator local)
    {
        Assert.True(local.Start(r.Version,r.Image,"en-US"));await local.Running.WaitAsync(TimeSpan.FromSeconds(6));
        var preview=Assert.IsType<OcrTextPreview>(local.Preview);Assert.Null(local.ApproveForImage(preview with{}));
        var approved=Assert.IsType<OcrImageApproval>(local.ApproveForImage(preview));Assert.Null(local.Preview);Assert.Null(local.ApproveForImage(preview));Assert.True(approved.IsPreparedCurrent);return approved;
    }
    [Fact]public async Task SmartAutomaticWithoutLocalApprovalCannotPrepareOrSend()
    {await using var r=await Rig.Create();Assert.Null(r.Flow.Prepare(r.Version,r.Image));Assert.Equal("LOCAL_OCR_APPROVAL_REQUIRED",r.Flow.LastRejection);Assert.Empty(r.Script.Bodies);Assert.Equal(0,r.SecretReads);}

    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task SmartDiscoveryAndChosenLanguageApprovalPrecedeBothHttpStages(Protocol protocol)
    {
        var queue=new Deferred();await using var r=await Rig.Create(protocol,auth:true,destination:queue);var diagnostic=new SmartOcrFixture.Worker();var worker=new SmartOcrFixture.Worker();int calls=0;
        await using var local=new OcrOnlyCoordinator(r.Gate,()=>r.Settings,(v,id)=>v==r.Version&&id==r.Image.SourceId&&!r.Image.IsDisposed,new Immediate(),()=>++calls==1?diagnostic:worker);
        Assert.True(local.Discover(r.Version,r.Image));await local.Running;Assert.True(diagnostic.Disposed);var caps=Assert.IsType<OcrCapabilitiesSnapshot>(local.Capabilities);Assert.False(local.Start(caps,"not-installed"));Assert.Equal(1,calls);Assert.Equal(0,r.Script.Requests);
        Assert.True(local.Start(caps,"zh-Hans"));await local.Running;Assert.True(worker.Disposed);Assert.Equal(2,calls);Assert.True(worker.Loan.Span.IndexOfAnyExcept((byte)0)<0);var preview=Assert.IsType<OcrTextPreview>(local.Preview);Assert.Equal("zh-Hans",preview.Language);Assert.Equal(0,r.Script.Requests);
        using var approved=Assert.IsType<OcrImageApproval>(local.ApproveForImage(preview));var offer=Assert.IsType<ProductionScreenshotOffer>(r.Flow.Prepare(r.Version,r.Image,ImageOutputMode.Automatic,approved));Assert.Contains("分类请求仍发送当前图片",offer.ConfirmationText);Assert.Contains("zh-Hans",offer.ConfirmationText);Assert.Equal("text",r.Settings.Data.ActiveProfileId);Assert.Equal("vision",offer.Snapshot.Binding.ProfileId);Assert.False(r.Flow.Submit(offer,false));Assert.Equal(0,r.Script.Requests);
        Assert.True(r.Flow.Submit(offer,true));Assert.True(approved.IsBoundCurrent);Assert.Equal(offer.Ticket.Epoch+1,r.Results.Identity!.Value.Epoch);Assert.False(approved.TryBind(offer.Ticket,r.Results.Identity.Value,offer.Snapshot.Binding));
        // The replay above revokes the capability: it cannot authorize the first HTTP either.
        using var guard=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(!offer.OriginalTask.IsCompleted){guard.Token.ThrowIfCancellationRequested();queue.Drain();await Task.Yield();}await offer.OriginalTask;Assert.Equal(0,r.Script.Requests);Assert.False(approved.IsBoundCurrent);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task SmartClearTextFinalSendsOnlyExactApprovedTextUsingConfirmedVisionProfile(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol,auth:true);var worker=new SmartOcrFixture.Worker();await using var local=Local(r,worker);using var approved=await Recognize(r,local);
        var offer=Assert.IsType<ProductionScreenshotOffer>(r.Flow.Prepare(r.Version,r.Image,ImageOutputMode.Automatic,approved));Assert.True(r.Flow.Submit(offer,true));await r.Requests(1);Assert.Equal(1,r.SecretReads);Assert.Equal(new ImageSize(8,8),ImagePolicy.InspectPng(Body(protocol,r.Script.Bodies.First()).Image));
        await r.Reply(ImageClassificationTests.Json("text_only"));await r.Requests(2);var body=r.Script.Bodies.Last();using var json=JsonDocument.Parse(body);var root=json.RootElement;Assert.Equal("PUBLIC_SCREENSHOT_MODEL",root.GetProperty("model").GetString());var content=root.GetProperty("messages")[protocol==Protocol.OpenAIChat?1:0].GetProperty("content");Assert.Equal(worker.Text,content.ValueKind==JsonValueKind.String?content.GetString():content[0].GetProperty("text").GetString());Assert.DoesNotContain("base64",body);Assert.DoesNotContain("image_url",body);Assert.DoesNotContain("regions",body);Assert.Equal(2,r.SecretReads);
        await r.Reply("PUBLIC SMART OCR TRANSLATION");await offer.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal("PUBLIC SMART OCR TRANSLATION",r.Results.Text);var snapshot=r.Flow.SelectedSnapshot!;Assert.True(snapshot.Ocr!.UsedForText);Assert.Equal(approved.PreviewId,snapshot.Ocr.PreviewId);Assert.Equal(TranslationPreset.Professional,snapshot.Plan!.Preset);Assert.DoesNotContain(worker.Text,JsonSerializer.Serialize(snapshot));Assert.False(approved.IsBoundCurrent);Assert.False(approved.TryReadText(out _));Assert.Null(local.ApproveForImage(local.Preview??new(Guid.NewGuid(),r.Version,r.Image.SourceId,"fake","en-US",r.Settings.Version,default)));Assert.Null(r.Flow.Prepare(r.Version,r.Image));Assert.Equal("IMAGE_REUSE_DISABLED",r.Flow.LastRejection);
    }
    [Theory][InlineData("diagram_or_mixed","clear")][InlineData("visual_only","clear")][InlineData("uncertain","clear")][InlineData("text_only","partly_unclear")]
    public async Task SmartNonClearTextBranchKeepsOriginalImageAndNeverSendsOcrText(string kind,string readability)
    {
        await using var r=await Rig.Create();var worker=new SmartOcrFixture.Worker();await using var local=Local(r,worker);using var approval=await Recognize(r,local);var offer=Assert.IsType<ProductionScreenshotOffer>(r.Flow.Prepare(r.Version,r.Image,ImageOutputMode.Automatic,approval));Assert.True(r.Flow.Submit(offer,true));await r.Requests(1);await r.Reply(ImageClassificationTests.Json(kind,readability:readability));await r.Requests(2);var first=Body(r.Protocol,r.Script.Bodies.First());var final=Body(r.Protocol,r.Script.Bodies.Last());Assert.Equal(first.Image,final.Image);Assert.DoesNotContain(worker.Text,r.Script.Bodies.Last());await r.Reply("PUBLIC IMAGE OUTPUT");await offer.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));Assert.False(r.Flow.SelectedSnapshot!.Ocr!.UsedForText);Assert.Equal(kind=="text_only"?TranslationPreset.Professional:TranslationPreset.Explain,r.Flow.SelectedSnapshot.Plan!.Preset);
    }
    [Theory][InlineData("{}")][InlineData("unreadable")]
    public async Task SmartInvalidOrUnreadableStillStopsAfterClassification(string value)
    {
        await using var r=await Rig.Create();await using var local=Local(r,new());using var approval=await Recognize(r,local);var offer=Assert.IsType<ProductionScreenshotOffer>(r.Flow.Prepare(r.Version,r.Image,ImageOutputMode.Automatic,approval));Assert.True(r.Flow.Submit(offer,true));await r.Requests(1);await r.Reply(value=="{}"?value:ImageClassificationTests.Json(readability:"unreadable"));await offer.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(1,r.Script.Requests);Assert.False(approval.IsBoundCurrent);Assert.False(Assert.Single(r.Results.Versions).Complete);
    }
    [Theory][InlineData("off")][InlineData("settings")][InlineData("replace")][InlineData("clear")][InlineData("dispose")][InlineData("suspend")]
    public async Task SmartApprovalRevocationBetweenStagesNeverSendsFinal(string change)
    {
        await using var r=await Rig.Create(auth:true);await using var local=Local(r,new());using var approval=await Recognize(r,local);var offer=Assert.IsType<ProductionScreenshotOffer>(r.Flow.Prepare(r.Version,r.Image,ImageOutputMode.Automatic,approval));Assert.True(r.Flow.Submit(offer,true));await r.Requests(1);
        switch(change){case "off":r.Gate.Disable();break;case "settings":r.Edit(s=>s with{Overlay=s.Overlay with{FontSizeDip=17}});break;case "replace":r.ReplaceImage();break;case "clear":local.Clear();break;case "dispose":approval.Dispose();break;case "suspend":r.Gate.SetSuspension(Suspension.Settings,true);break;}
        if(change!="off")await r.Reply(ImageClassificationTests.Json("text_only"));await offer.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(1,r.Script.Requests);Assert.Equal(1,r.SecretReads);Assert.False(approval.IsBoundCurrent);Assert.DoesNotContain(r.Updates,u=>u.Kind==ProductionScreenshotEvent.Completed);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task SmartCleanupFailureCannotIssueApprovalOrStartHttp(bool throws)
    {
        await using var r=await Rig.Create();var worker=new SmartOcrFixture.Worker{Finalized=false,FinalizeThrows=throws};await using var local=Local(r,worker);
        try{Assert.True(local.Start(r.Version,r.Image,"en-US"));await local.Running;Assert.Null(local.Preview);Assert.Equal("CLEANUP_UNCONFIRMED",local.Failure);Assert.True(local.CleanupPending);Assert.False(worker.Disposed);Assert.Null(r.Flow.Prepare(r.Version,r.Image));Assert.Equal(0,r.Script.Requests);}
        finally{worker.Finalized=true;worker.FinalizeThrows=false;await local.ClearAsync();}Assert.True(worker.Disposed);
    }
    [Theory][InlineData("off")][InlineData("settings")][InlineData("replace")][InlineData("clear")]
    public async Task SmartLateWorkerLoanRemainsHeldUntilOriginalReturnAndCannotApprove(string change)
    {
        await using var r=await Rig.Create();var worker=new SmartOcrFixture.Worker{Hold=true};await using var local=Local(r,worker);Assert.True(local.Start(r.Version,r.Image,"en-US"));var original=local.Running;await worker.Entered.Task.WaitAsync(TimeSpan.FromSeconds(6));
        try{if(change=="off")r.Gate.Disable();if(change=="settings")r.Edit(s=>s with{Translation=s.Translation with{Preset=TranslationPreset.Plain}});if(change=="replace")r.ReplaceImage();var clear=local.ClearAsync();Assert.False(clear.IsCompleted);Assert.False(original.IsCompleted);Assert.True(worker.Cancellation.IsCancellationRequested);Assert.Contains(worker.Loan.ToArray(),b=>b!=0);Assert.False(local.Start(r.Version,r.Image,"en-US"));worker.Release.TrySetResult();await clear.WaitAsync(TimeSpan.FromSeconds(6));Assert.True(worker.Disposed);Assert.True(worker.Loan.Span.IndexOfAnyExcept((byte)0)<0);Assert.Null(local.Preview);Assert.Equal(0,r.Script.Requests);}
        finally{worker.Release.TrySetResult();}
    }
    [Theory][InlineData(ImageFailure.UnsupportedPlatform)][InlineData(ImageFailure.LanguageUnavailable)]
    public async Task SmartUnavailableOcrNeverSilentlyFallsBackToHttp(ImageFailure failure)
    {
        await using var r=await Rig.Create();var worker=new SmartOcrFixture.Worker{Failure=failure,Languages=[],Maximum=0};await using var local=Local(r,worker);Assert.True(local.Discover(r.Version,r.Image));await local.Running;Assert.Null(local.Capabilities);Assert.Equal(failure.ToString(),local.Failure);Assert.Null(r.Flow.Prepare(r.Version,r.Image));Assert.Equal(0,r.Script.Requests);Assert.True(worker.Disposed);
    }
    [Fact]public async Task SmartOffCannotCreateLocalWorkerAndQueuedOldPreviewCannotReachNewSource()
    {
        await using var r=await Rig.Create();var worker=new SmartOcrFixture.Worker();var queue=new Deferred();await using var local=Local(r,worker,queue);r.Gate.Disable();Assert.False(local.Discover(r.Version,r.Image));Assert.Equal(0,worker.Diagnoses);Assert.True(r.Gate.TryEnable(new(new Uri(r.Settings.Data.Profiles[0].BaseUrl)),true));Assert.True(local.Start(r.Version,r.Image,"en-US"));var original=local.Running;using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(queue.Pending==0)await Task.Delay(5,timeout.Token);r.ReplaceImage();queue.Drain();await original.WaitAsync(TimeSpan.FromSeconds(6));Assert.Null(local.Preview);Assert.False(local.CleanupPending);Assert.Equal(0,r.Script.Requests);
    }
    [Fact]public async Task SmartBeginStopBeforeOriginalRunStillReleasesApproval()
    {
        await using var r=await Rig.Create();await using var local=Local(r,new());using var approval=await Recognize(r,local);
        var offer=Assert.IsType<ProductionScreenshotOffer>(r.Flow.Prepare(r.Version,r.Image,ImageOutputMode.Automatic,approval));var queue=new Deferred();var previous=SynchronizationContext.Current;
        try{SynchronizationContext.SetSynchronizationContext(queue);r.OnUpdate=u=>{if(u.Kind==ProductionScreenshotEvent.Begin)r.Flow.Stop();};Assert.True(r.Flow.Submit(offer,true));}
        finally{SynchronizationContext.SetSynchronizationContext(previous);}
        queue.Drain();await offer.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(2,approval.State);Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData("stop")][InlineData("clear")][InlineData("style")][InlineData("replace-same")]
    public async Task SmartPendingOfferCancellationPermanentlyRevokesItsApproval(string action)
    {
        await using var r=await Rig.Create();await using var local=Local(r,new());using var approval=await Recognize(r,local);
        var offer=Assert.IsType<ProductionScreenshotOffer>(r.Flow.Prepare(r.Version,r.Image,ImageOutputMode.Automatic,approval));
        if(action=="stop")r.Flow.Stop();if(action=="clear")r.Flow.Clear();if(action=="style")Assert.True(r.Flow.SetSourcePreset(r.Version,r.Image.SourceId,TranslationPreset.Plain));if(action=="replace-same")Assert.Null(r.Flow.Prepare(r.Version,r.Image,ImageOutputMode.Automatic,approval));
        Assert.Equal(2,approval.State);Assert.False(r.Flow.Submit(offer,true));Assert.Null(r.Flow.Prepare(r.Version,r.Image,ImageOutputMode.Automatic,approval));Assert.Equal(0,r.Script.Requests);
    }

    [Theory][InlineData(ImageSourceKind.LocalFile)][InlineData(ImageSourceKind.Browser)][InlineData(ImageSourceKind.Bitmap)]
    public async Task SmartPreparedNonDesktopImagesKeepExactSourceAndOcrLineage(ImageSourceKind kind)
    {
        await using var r=await Rig.Create();r.Image.Dispose();var raw=Enumerable.Repeat((byte)255,256).ToArray();using var pixels=new ScreenshotPixels(new(0,0,8,8),raw.ToArray());var transform=ImageTransform.PlanEncoded(new(8,8),ImageOrientation.ExifTopLeft);
        r.Image=new OwnedFileImage(new(kind==ImageSourceKind.Bitmap?EncodedRasterFormat.Bmp:EncodedRasterFormat.Png,256,new string('a',64),transform,2560),raw,ScreenshotPngEncoder.Encode(pixels),kind);
        await using var local=Local(r,new());using var approval=await Recognize(r,local);Assert.Equal(kind,approval.SourceKind);Assert.Null(approval.SourceNotice);var offer=Assert.IsType<ProductionScreenshotOffer>(r.Flow.Prepare(r.Version,r.Image,ImageOutputMode.Automatic,approval));Assert.True(r.Flow.Submit(offer,true));await r.Requests(1);await r.Reply(ImageClassificationTests.Json("text_only"));await r.Requests(2);Assert.DoesNotContain("base64",r.Script.Bodies.Last());await r.Reply("PUBLIC NONDESKTOP OCR OUTPUT");await offer.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));var snapshot=r.Flow.SelectedSnapshot!;Assert.Equal(kind,snapshot.SourceKind);Assert.Equal(approval.SourceId,snapshot.SourceId);Assert.Equal(approval.SourceVersion,snapshot.SourceVersion);Assert.True(snapshot.Ocr!.UsedForText);Assert.False(snapshot.Transform!.HasDesktopAnchor);Assert.Null(snapshot.NormalizationProof);Assert.DoesNotContain(new string('a',64),r.Script.Bodies.Last());
    }

}
