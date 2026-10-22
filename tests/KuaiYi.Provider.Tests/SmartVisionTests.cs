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
    private sealed class VisionWorker:ILocalOcrCapabilitiesWorker
    {
        internal ImageFailure Failure;
        internal string[] Languages=[];
        internal string Text="";
        internal string? Fault;
        internal bool Finalized=true,FinalizeThrows,DisposeThrows,HoldFinalize,Disposed,Hold;
        internal ReadOnlyMemory<byte> Loan;
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously),FinalizeEntered=new(TaskCreationOptions.RunContinuationsAsynchronously),FinalizeRelease=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<OcrDiagnosticReply> DiagnoseAsync(OcrDiagnosticJob job,CancellationToken cancellation=default)
        {
            var identity=new IdentityDiagnostic(true,true,PackageIdentity.DevelopmentName+"_PUBLIC",PackageIdentity.DevelopmentName+"_PUBLIC!ImageWorker",ImageFailure.None);
            if(Fault=="identity")identity=identity with{Failure=ImageFailure.IdentityMismatch};
            if(Fault=="role")identity=identity with{ApplicationUserModelId=PackageIdentity.DevelopmentName+"_PUBLIC!App"};
            if(Fault=="platform")identity=identity with{Windows=false};
            return Task.FromResult(new OcrDiagnosticReply(Fault=="job"?job with{Epoch=job.Epoch+1}:job,new(identity,Failure==ImageFailure.None?Languages:[],Failure==ImageFailure.None?4096U:0,Failure)));
        }
        public async Task<ImageReply> RunAsync(ReadOnlyMemory<byte> bytes,ImageJob job,CancellationToken cancellation=default)
        {
            Loan=bytes;Entered.TrySetResult();if(Hold)await Release.Task;
            if(Fault=="throw")throw new ImageJobException(ImageFailure.LanguageUnavailable);
            var size=ImagePolicy.InspectPng(bytes.Span);var output=new OcrOutput(size.Width,size.Height,job.Language,Text,[]);
            if(Fault=="bounds")output=output with{Width=size.Width+1};
            if(Fault=="language")output=output with{Language="other"};
            if(Fault=="lines")output=output with{Lines=[new("PUBLIC CONTRADICTORY LINE",[])]};
            return new(Fault=="job"?job with{Epoch=job.Epoch+1}:job,Failure,Failure==ImageFailure.None||Fault=="failure-output"?output:null);
        }
        public async ValueTask<bool> TryFinalizeAsync()
        {FinalizeEntered.TrySetResult();if(HoldFinalize)await FinalizeRelease.Task;if(FinalizeThrows)throw new IOException("PUBLIC CLEANUP FAILURE");return Finalized;}
        public ValueTask DisposeAsync(){if(DisposeThrows)throw new IOException("PUBLIC DISPOSE FAILURE");Disposed=true;return ValueTask.CompletedTask;}
    }
    private static OcrOnlyCoordinator VisionLocal(Rig r,VisionWorker worker,SynchronizationContext? destination=null)
        =>new(r.Gate,()=>r.Settings,(v,id)=>r.Current&&v==r.Version&&id==r.Image.SourceId&&!r.Image.IsDisposed,destination??new Immediate(),()=>worker);
    private static async Task<VisionContinuationCandidate> DiscoverVision(Rig r,OcrOnlyCoordinator local)
    {Assert.True(local.Discover(r.Version,r.Image));await local.Running.WaitAsync(TimeSpan.FromSeconds(6));return Assert.IsType<VisionContinuationCandidate>(local.VisionCandidate);}
    private static async Task<VisionContinuationCandidate> RecognizeVision(Rig r,OcrOnlyCoordinator local)
    {
        Assert.True(local.Discover(r.Version,r.Image));await local.Running.WaitAsync(TimeSpan.FromSeconds(6));var caps=Assert.IsType<OcrCapabilitiesSnapshot>(local.Capabilities);
        Assert.True(local.Start(caps,"en-US"));await local.Running.WaitAsync(TimeSpan.FromSeconds(6));return Assert.IsType<VisionContinuationCandidate>(local.VisionCandidate);
    }
    [Theory][InlineData(Protocol.OpenAIChat,"text_only")][InlineData(Protocol.AnthropicMessages,"visual_only")][InlineData(Protocol.OpenAIChat,"diagram_or_mixed")][InlineData(Protocol.AnthropicMessages,"uncertain")]
    public async Task SmartVisionExplicitLocalChoiceAndHttpConsentKeepOriginalImageInBothStages(Protocol protocol,string kind)
    {
        await using var r=await Rig.Create(protocol,auth:true);var worker=new VisionWorker{Languages=["en-US"]};await using var local=VisionLocal(r,worker);
        var candidate=await RecognizeVision(r,local);Assert.Equal(VisionContinuationReason.EmptyResult,candidate.Reason);Assert.Null(local.Preview);Assert.True(worker.Disposed);Assert.True(worker.Loan.Span.IndexOfAnyExcept((byte)0)<0);Assert.Equal(0,r.Script.Requests);
        Assert.Null(r.Flow.Prepare(r.Version,r.Image));using var approval=Assert.IsType<VisionImageApproval>(local.ApproveVision(candidate));Assert.Null(local.ApproveVision(candidate));Assert.Null(local.VisionCandidate);
        var offer=Assert.IsType<ProductionScreenshotOffer>(r.Flow.Prepare(r.Version,r.Image,ImageOutputMode.Automatic,approval));Assert.Contains("没有批准 OCR 文字",offer.ConfirmationText);Assert.Contains("PUBLIC_SCREENSHOT_MODEL",offer.ConfirmationText);Assert.False(r.Flow.Submit(offer,false));Assert.Equal(0,r.SecretReads);Assert.Equal(0,r.Script.Requests);
        Assert.True(r.Flow.Submit(offer,true));await r.Requests(1);await r.Reply(ImageClassificationTests.Json(kind));await r.Requests(2);Assert.Equal(Body(protocol,r.Script.Bodies.First()).Image,Body(protocol,r.Script.Bodies.Last()).Image);Assert.DoesNotContain("PUBLIC CONTRADICTORY",r.Script.Bodies.Last());await r.Reply("PUBLIC VISUAL OUTPUT");await offer.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));
        var snapshot=r.Flow.SelectedSnapshot!;Assert.Null(snapshot.Ocr);Assert.Equal(candidate.Reason,snapshot.VisionContinuation);Assert.Equal(kind=="text_only"?TranslationPreset.Professional:TranslationPreset.Explain,snapshot.Plan!.Preset);Assert.Equal(2,r.SecretReads);Assert.Equal(2,approval.State);Assert.Null(r.Flow.Prepare(r.Version,r.Image));Assert.Equal("IMAGE_REUSE_DISABLED",r.Flow.LastRejection);
    }
    [Theory][InlineData(ImageFailure.None)][InlineData(ImageFailure.LanguageUnavailable)][InlineData(ImageFailure.NativeUnavailable)]
    public async Task SmartVisionBenignDiscoveryStillRequiresExactCandidateAndExplicitApproval(ImageFailure failure)
    {
        await using var r=await Rig.Create();var worker=new VisionWorker{Failure=failure};await using var local=VisionLocal(r,worker);var candidate=await DiscoverVision(r,local);
        Assert.Equal(failure==ImageFailure.NativeUnavailable?VisionContinuationReason.EngineUnavailable:VisionContinuationReason.NoInstalledLanguage,candidate.Reason);Assert.True(worker.Disposed);Assert.Null(r.Flow.Prepare(r.Version,r.Image));Assert.Equal(0,r.Script.Requests);
        var clone=new VisionContinuationCandidate(candidate.SourceVersion,candidate.SourceId,candidate.SettingsVersion,candidate.Identity,candidate.SourceKind,candidate.SourceNotice,candidate.Reason);Assert.Null(local.ApproveVision(clone));Assert.True(local.IsVisionCandidateCurrent(candidate));local.RejectVision(candidate);Assert.Null(local.ApproveVision(candidate));Assert.Null(local.VisionCandidate);Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData("identity")][InlineData("role")][InlineData("platform")][InlineData("job")]
    public async Task SmartVisionUntrustedDiagnosticCannotOfferContinuation(string fault)
    {await using var r=await Rig.Create();await using var local=VisionLocal(r,new(){Failure=ImageFailure.NativeUnavailable,Fault=fault});Assert.True(local.Discover(r.Version,r.Image));await local.Running;Assert.Null(local.VisionCandidate);Assert.Null(r.Flow.Prepare(r.Version,r.Image));Assert.Equal(0,r.Script.Requests);}
    [Theory][InlineData(ImageFailure.IdentityMissing)][InlineData(ImageFailure.IdentityMismatch)][InlineData(ImageFailure.UnsupportedPlatform)][InlineData(ImageFailure.Cancelled)][InlineData(ImageFailure.Timeout)][InlineData(ImageFailure.NativeFailure)]
    public async Task SmartVisionOtherFailuresCannotOfferContinuation(ImageFailure failure)
    {await using var r=await Rig.Create();await using var local=VisionLocal(r,new(){Failure=failure});Assert.True(local.Discover(r.Version,r.Image));await local.Running;Assert.Null(local.VisionCandidate);Assert.Equal(0,r.Script.Requests);}
    [Theory][InlineData("bounds")][InlineData("language")][InlineData("lines")][InlineData("job")][InlineData("throw")][InlineData("whitespace")][InlineData("failure-output")]
    public async Task SmartVisionMalformedOrThrownRecognitionIsNotAnEmptyResult(string fault)
    {
        await using var r=await Rig.Create();var worker=new VisionWorker{Languages=["en-US"]};await using var local=VisionLocal(r,worker);Assert.True(local.Discover(r.Version,r.Image));await local.Running;var caps=Assert.IsType<OcrCapabilitiesSnapshot>(local.Capabilities);worker.Fault=fault;worker.Text=fault=="whitespace"?" \t\r\n":"";worker.Failure=fault=="failure-output"?ImageFailure.LanguageUnavailable:ImageFailure.None;
        Assert.True(local.Start(caps,"en-US"));await local.Running;Assert.Null(local.VisionCandidate);Assert.Null(local.Preview);Assert.Equal(fault=="throw"?"LanguageUnavailable":"OCR_FAILED",local.Failure);Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData(ImageFailure.LanguageUnavailable)][InlineData(ImageFailure.NativeUnavailable)]
    public async Task SmartVisionRecognitionFailureRequiresOriginalChosenCapabilityLineage(ImageFailure failure)
    {
        await using var r=await Rig.Create();var worker=new VisionWorker{Languages=["en-US"],Failure=failure};await using var local=VisionLocal(r,worker);
        Assert.True(local.Start(r.Version,r.Image,"en-US"));await local.Running;Assert.Null(local.VisionCandidate);worker.Failure=ImageFailure.None;Assert.True(local.Discover(r.Version,r.Image));await local.Running;var caps=Assert.IsType<OcrCapabilitiesSnapshot>(local.Capabilities);worker.Failure=failure;Assert.True(local.Start(caps,"en-US"));await local.Running;Assert.NotNull(local.VisionCandidate);Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData("off")][InlineData("lock")][InlineData("hidden")][InlineData("settings")][InlineData("source")][InlineData("clear")][InlineData("epoch")]
    public async Task SmartVisionHeldCandidateCannotSurviveInvalidation(string change)
    {
        await using var r=await Rig.Create();await using var local=VisionLocal(r,new());var candidate=await DiscoverVision(r,local);
        if(change=="off")r.Gate.Disable();if(change=="lock")r.Gate.SetSessionLocked(true);if(change=="hidden")r.Gate.Disable(hide:true);if(change=="settings")r.Edit(s=>s with{Translation=s.Translation with{Preset=TranslationPreset.Plain}});if(change=="source")r.ReplaceImage();if(change=="clear")await local.ClearAsync();if(change=="epoch")r.Gate.SetMode(TriggerMode.Drop);
        Assert.Null(local.ApproveVision(candidate));Assert.Null(local.VisionCandidate);Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData("pending")][InlineData("finalize-throw")][InlineData("dispose-throw")]
    public async Task SmartVisionCleanupMustActuallySettleBeforeOutcomeIsPublic(string failure)
    {
        await using var r=await Rig.Create();var worker=new VisionWorker{HoldFinalize=true,Finalized=failure!="pending",FinalizeThrows=failure=="finalize-throw",DisposeThrows=failure=="dispose-throw"};await using var local=VisionLocal(r,worker);
        try{Assert.True(local.Discover(r.Version,r.Image));var original=local.Running;await worker.FinalizeEntered.Task.WaitAsync(TimeSpan.FromSeconds(6));Assert.False(original.IsCompleted);Assert.Null(local.VisionCandidate);Assert.False(worker.Disposed);worker.FinalizeRelease.TrySetResult();await original;Assert.Null(local.VisionCandidate);Assert.Equal("CLEANUP_UNCONFIRMED",local.Failure);Assert.True(local.CleanupPending);Assert.Equal(0,r.Script.Requests);}
        finally{worker.Finalized=true;worker.FinalizeThrows=false;worker.DisposeThrows=false;worker.FinalizeRelease.TrySetResult();await local.ClearAsync();}
    }
    [Fact]public async Task SmartVisionPendingOriginalTaskDoesNotDestroyLaterSettledCandidate()
    {
        await using var r=await Rig.Create();var queue=new Deferred();var worker=new VisionWorker();await using var local=VisionLocal(r,worker,queue);
        Assert.True(local.Discover(r.Version,r.Image));var original=local.Running;using var guard=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(queue.Pending==0){guard.Token.ThrowIfCancellationRequested();await Task.Yield();}Assert.True(worker.Disposed);Assert.False(original.IsCompleted);
        Assert.True(r.Gate.TryBeginPassiveCapture(r.Gate.Snapshot.Mode,out var ticket));
        Assert.True(r.Gate.TryCommitCapture(ticket,()=>true,()=>
        {
            // Apply the outcome, but hold the Gate that the original Run finally must join.
            queue.Drain();Assert.False(original.IsCompleted);Assert.Null(local.VisionCandidate);
        }));
        await original.WaitAsync(TimeSpan.FromSeconds(6));var candidate=Assert.IsType<VisionContinuationCandidate>(local.VisionCandidate);using var approval=Assert.IsType<VisionImageApproval>(local.ApproveVision(candidate));Assert.True(approval.IsPreparedCurrent);Assert.Equal(0,r.Script.Requests);
    }
    [Theory][InlineData("source")][InlineData("off")][InlineData("clear")]
    public async Task SmartVisionLateEmptyResultCannotAuthorizeAndOriginalLoanIsZeroed(string change)
    {
        await using var r=await Rig.Create();var worker=new VisionWorker{Hold=true};await using var local=VisionLocal(r,worker);Assert.True(local.Start(r.Version,r.Image,"en-US"));var original=local.Running;await worker.Entered.Task.WaitAsync(TimeSpan.FromSeconds(6));
        try{if(change=="source")r.ReplaceImage();if(change=="off")r.Gate.Disable();var clear=local.ClearAsync();Assert.False(clear.IsCompleted);Assert.Contains(worker.Loan.ToArray(),x=>x!=0);worker.Release.TrySetResult();await clear.WaitAsync(TimeSpan.FromSeconds(6));Assert.True(original.IsCompleted);Assert.True(worker.Disposed);Assert.True(worker.Loan.Span.IndexOfAnyExcept((byte)0)<0);Assert.Null(local.VisionCandidate);Assert.Equal(0,r.Script.Requests);}
        finally{worker.Release.TrySetResult();}
    }
}
