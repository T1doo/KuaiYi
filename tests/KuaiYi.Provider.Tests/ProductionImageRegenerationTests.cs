using System.Reflection;
using System.Text.Json;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Imaging;
using KuaiYi.Providers;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed partial class ProductionScreenshotTranslationTests
{
    // Reflection keeps the feature's absent-entry red case compilable against the original class.
    private static ProductionScreenshotOffer? Regeneration(Rig r,bool current=false,bool reclassify=false,SmartImageApproval? approval=null,ImageOutputMode? mode=null)
    {
        var method=typeof(ProductionScreenshotCoordinator).GetMethod("PrepareRegeneration",BindingFlags.Public|BindingFlags.Instance);
        Assert.NotNull(method);
        try{return (ProductionScreenshotOffer?)method.Invoke(r.Flow,[current,reclassify,approval,mode]);}
        catch(TargetInvocationException e)when(e.InnerException!=null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}
    }
    [Fact]public async Task ProductionRegenerationReusesExactSelectedSourceAnalysisWithOnlyOneFreshGeneration()
    {
        await using var r=await Rig.Create(auth:true);await r.Generate();var original=r.Flow.SelectedSnapshot!;var previous=Assert.Single(r.Results.Versions).Identity;
        var offer=Assert.IsType<ProductionScreenshotOffer>(Regeneration(r));Assert.True(offer.Snapshot.Regeneration);Assert.Same(original.Classification,offer.Snapshot.Classification);
        Assert.Same(original.ClassificationUsage,offer.Snapshot.ClassificationUsage);Assert.True(offer.Snapshot.ClassificationUsageReused);Assert.Null(offer.Snapshot.GenerationUsage);
        Assert.Equal(2,r.Script.Requests);Assert.Equal(2,r.SecretReads);Assert.Contains("一次",offer.ConfirmationText);Assert.True(r.Flow.Submit(offer,true));
        await r.Requests(3);await r.Reply("PUBLIC REGENERATED RESULT");await offer.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));
        var next=r.Flow.SelectedSnapshot!;Assert.Equal(3,r.Script.Requests);Assert.Equal(3,r.SecretReads);Assert.Equal(previous.SessionId,r.Results.Identity!.Value.SessionId);
        Assert.Equal(2,r.Results.Versions.Count);Assert.Equal(2,next.Number);Assert.Equal(original.SourceId,next.SourceId);Assert.Equal(original.SourceVersion,next.SourceVersion);
        Assert.Same(original.ClassificationUsage,next.ClassificationUsage);Assert.NotEqual(original.GenerationUsage!.InvocationId,next.GenerationUsage!.InvocationId);
        Assert.Equal(Body(r.Protocol,r.Script.Bodies.ElementAt(1)).Image,Body(r.Protocol,r.Script.Bodies.ElementAt(2)).Image);Assert.False(r.Image.IsDisposed);
        Assert.Null(r.Flow.Prepare(r.Version,r.Image));Assert.Equal("IMAGE_REUSE_DISABLED",r.Flow.LastRejection);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task ProductionRegenerationFreezesShownVersionOrExplicitCurrentSettings(bool current)
    {
        await using var r=await Rig.Create();await r.Generate();var saved=r.Flow.SelectedSnapshot!;r.Edit(s=>s with{Translation=s.Translation with{Preset=TranslationPreset.Plain}});
        var offer=Assert.IsType<ProductionScreenshotOffer>(Regeneration(r,current));Assert.Same(current?r.Settings:saved.Settings,offer.Snapshot.Settings);
        Assert.Equal(current?TranslationPreset.Plain:TranslationPreset.Professional,offer.RequestedPreset);Assert.Same(r.Settings,offer.AdmissionSettings);
        Assert.True(r.Flow.Submit(offer,true));await r.Requests(3);await r.Reply("PUBLIC FROZEN RESULT");await offer.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.Equal(offer.RequestedPreset,r.Flow.SelectedSnapshot!.Plan!.Preset);Assert.Equal(3,r.Script.Requests);
    }
    [Fact]public async Task ProductionRegenerationUnknownAnalysisRejectsWithoutCancellingOriginalAndExplicitRejudgeIsTwoStages()
    {
        await using var r=await Rig.Create(auth:true);Assert.True(r.Flow.Submit(r.Offer(),true));await r.Requests(1);var running=r.Flow.Running;
        Assert.Null(Regeneration(r));Assert.Equal("IMAGE_CLASSIFICATION_REQUIRED",r.Flow.LastRejection);Assert.Same(running,r.Flow.Running);Assert.True(r.Gate.HasRunningGeneration);
        await r.Reply("{}");await running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Null(Regeneration(r));Assert.Equal(1,r.Script.Requests);
        var offer=Assert.IsType<ProductionScreenshotOffer>(Regeneration(r,reclassify:true));Assert.Null(offer.Snapshot.Classification);Assert.Null(offer.Snapshot.ClassificationUsage);Assert.Contains("两次",offer.ConfirmationText);
        Assert.True(r.Flow.Submit(offer,true));await r.Requests(2);await r.Reply(ImageClassificationTests.Json());await r.Requests(3);await r.Reply("PUBLIC REJUDGED RESULT");await offer.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));Assert.False(r.Flow.SelectedSnapshot!.ClassificationUsageReused);
    }
    [Theory][InlineData("decline")][InlineData("source")][InlineData("clear")][InlineData("profile")][InlineData("off")][InlineData("lock")][InlineData("settings")]
    public async Task ProductionRegenerationConfirmationRetainsOriginalAuthorityAndCanBeRevoked(string change)
    {
        await using var r=await Rig.Create(auth:true);await r.Generate();var offer=Assert.IsType<ProductionScreenshotOffer>(Regeneration(r));
        switch(change){case "source":r.ReplaceImage();break;case "clear":r.Flow.Clear();break;case "profile":r.Edit(s=>s with{Profiles=s.Profiles.SetItem(1,s.Profiles[1] with{Model="PUBLIC_REVOKED"})});break;case "off":r.Gate.Disable();break;case "lock":r.Gate.SetSessionLocked(true);break;case "settings":r.Edit(s=>s with{Overlay=s.Overlay with{FontSizeDip=17}});break;}
        Assert.False(r.Flow.Submit(offer,change!="decline"));r.Flow.Reject(offer);Assert.Equal(2,r.Script.Requests);Assert.Equal(2,r.SecretReads);
    }
    [Theory][InlineData("stop")][InlineData("clear")][InlineData("source")][InlineData("off")][InlineData("lock")]
    public async Task ProductionRegenerationPreparedReentryIsBeforeAnyNewSecretOrSend(string change)
    {
        await using var r=await Rig.Create(auth:true);await r.Generate();bool entered=false;
        r.OnUpdate=u=>{if(u.Kind!=ProductionScreenshotEvent.Prepared||!u.Snapshot.Regeneration)return;entered=true;switch(change){case "stop":r.Flow.Stop();break;case "clear":r.Flow.Clear();break;case "source":r.Current=false;break;case "off":r.Gate.Disable();break;case "lock":r.Gate.SetSessionLocked(true);break;}};
        var offer=Assert.IsType<ProductionScreenshotOffer>(Regeneration(r));Assert.True(r.Flow.Submit(offer,true));await offer.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.True(entered);Assert.Equal(2,r.Script.Requests);Assert.Equal(2,r.SecretReads);Assert.False(r.Gate.HasRunningGeneration);
    }
    [Fact]public async Task ProductionRegenerationAutomaticRequiresNewBoundApprovalAndNeverRevivesOldOcrText()
    {
        await using var r=await Rig.Create();await using var oldLocal=Local(r,new());using var old=await Recognize(r,oldLocal);
        var first=Assert.IsType<ProductionScreenshotOffer>(r.Flow.Prepare(r.Version,r.Image,ImageOutputMode.Automatic,old));Assert.True(r.Flow.Submit(first,true));await r.Requests(1);await r.Reply(ImageClassificationTests.Json("text_only"));await r.Requests(2);await r.Reply("PUBLIC INITIAL OCR RESULT");await first.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.False(old.IsBoundCurrent);Assert.Null(Regeneration(r,approval:old));Assert.Equal("LOCAL_OCR_APPROVAL_REQUIRED",r.Flow.LastRejection);Assert.Null(Regeneration(r));
        var worker=new SmartOcrFixture.Worker{Text="PUBLIC NEW APPROVED OCR TEXT"};await using var freshLocal=Local(r,worker);using var fresh=await Recognize(r,freshLocal);
        var offer=Assert.IsType<ProductionScreenshotOffer>(Regeneration(r,approval:fresh));Assert.Null(offer.Snapshot.Ocr);Assert.True(r.Flow.Submit(offer,true));await r.Requests(3);
        Assert.Contains(worker.Text,r.Script.Bodies.Last());using(var body=JsonDocument.Parse(r.Script.Bodies.Last()))Assert.Contains(PromptBuilder.Read("regenerate"),body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());await r.Reply("PUBLIC NEW OCR RESULT");await offer.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.True(r.Flow.SelectedSnapshot!.Ocr!.UsedForText);Assert.Equal(fresh.PreviewId,r.Flow.SelectedSnapshot.Ocr.PreviewId);Assert.False(fresh.TryReadText(out _));Assert.Equal(3,r.Script.Requests);
    }
    [Fact]public async Task ProductionRegenerationUsesSelectedVersionAndPreservesRollbackWithinThreeVersions()
    {
        await using var r=await Rig.Create();await r.Generate();var first=Assert.Single(r.Results.Versions);var offer=Assert.IsType<ProductionScreenshotOffer>(Regeneration(r,mode:ImageOutputMode.ExplainImage));
        Assert.True(r.Flow.Submit(offer,true));await r.Requests(3);await r.Reply("PUBLIC SECOND VERSION");await offer.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.Equal(ImageOutputMode.ExplainImage,r.Flow.SelectedSnapshot!.RequestedMode);Assert.True(r.Results.SelectVersion(first.Identity.VersionId));
        offer=Assert.IsType<ProductionScreenshotOffer>(Regeneration(r));Assert.Equal(ImageOutputMode.TranslateText,offer.Snapshot.RequestedMode);Assert.True(r.Flow.Submit(offer,true));await r.Requests(4);await r.Reply("PUBLIC FAILED PARTIAL","length");await offer.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.Equal(first.Identity.VersionId,r.Results.SelectedVersionId);Assert.Equal(first.Text,r.Results.Text);Assert.True(r.Results.Versions.Count<=3);
        offer=Assert.IsType<ProductionScreenshotOffer>(Regeneration(r));Assert.Equal(4,offer.Snapshot.Number);Assert.True(r.Flow.Submit(offer,true));await r.Requests(5);await r.Reply("PUBLIC FOURTH VERSION");await offer.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(3,r.Results.Versions.Count);Assert.Equal(4,r.Flow.SelectedSnapshot!.Number);
    }

    [Fact]public async Task ProductionRegenerationCanReuseValidClassificationWithUnknownBillingUsage()
    {
        await using var r=await Rig.Create();Assert.True(r.Flow.Submit(r.Offer(),true));await r.Requests(1);await ReplyClassificationWithoutUsage(r,r.Protocol);await r.Requests(2);await r.Reply("PUBLIC ORIGINAL RESULT");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));
        var receipt=r.Flow.SelectedSnapshot!.ClassificationUsage!;Assert.False(receipt.Reported);Assert.Equal(new TokenUsage(null,null),receipt.Tokens);
        var offer=Assert.IsType<ProductionScreenshotOffer>(Regeneration(r));Assert.Same(receipt,offer.Snapshot.ClassificationUsage);Assert.True(r.Flow.Submit(offer,true));await r.Requests(3);await r.Reply("PUBLIC REUSED CLASSIFICATION");await offer.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(3,r.Script.Requests);
    }
    // Test-only encoder fault port: real production coordinator, source ownership and send gates are unchanged.
    private sealed class ChangedEncodingOwner(ImageSourceOwner original):ImageSourceOwner
    {
        public string? Change;
        public override Guid SourceId=>original.SourceId;public override ImageSourceKind Kind=>original.Kind;public override string? Notice=>original.Notice;
        public override int Width=>original.Width;public override int Height=>original.Height;public override bool IsDisposed=>original.IsDisposed;
        public override ScreenshotPixels CreatePreview(int width,int height,CancellationToken cancellation=default)=>original.CreatePreview(width,height,cancellation);
        public override PreparedImageRequest PrepareRequest(int limit,CancellationToken cancellation=default)=>new ChangedPrepared(original.PrepareRequest(limit,cancellation),Change);
        public override void Dispose()=>original.Dispose();
    }
    private sealed class ChangedPrepared(PreparedImageRequest original,string? change):PreparedImageRequest
    {
        public override Guid SourceId=>original.SourceId;public override ImageSourceKind Kind=>original.Kind;public override string? Notice=>original.Notice;
        public override ImageTransform Transform=>change=="transform"?ImageTransform.Plan(new PixelRect(0,0,8,8)):original.Transform;
        public override ScreenshotNormalizationProof? NormalizationProof=>change=="proof"?ScreenshotNormalizationProof.ConstantCompositeDownscale:original.NormalizationProof;
        public override ReadOnlyMemory<byte> Bytes=>original.Bytes;public override void Dispose()=>original.Dispose();
    }
    [Theory][InlineData("transform")][InlineData("proof")]
    public async Task ProductionRegenerationRevalidatesActualPreparedMappingAndProofBeforeSecretOrSend(string change)
    {
        await using var r=await Rig.Create(auth:true);var source=new ChangedEncodingOwner(r.Image);r.Image=source;await r.Generate();source.Change=change;
        var offer=Assert.IsType<ProductionScreenshotOffer>(Regeneration(r));Assert.True(r.Flow.Submit(offer,true));await offer.OriginalTask.WaitAsync(TimeSpan.FromSeconds(6));
        var failed=Assert.Single(r.Results.Versions,v=>v.Number==2);Assert.Equal("IMAGE_TRANSFORM_CHANGED",r.Flow.FailureForVersion(failed.Identity.VersionId));Assert.Equal(2,r.SecretReads);Assert.Equal(2,r.Script.Requests);Assert.False(r.Image.IsDisposed);
    }
}
