using System.Security.Cryptography;
using System.Text;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Imaging;
using KuaiYi.Providers;
using SettingsProfile=KuaiYi.Core.Settings.ApiProfile;
namespace KuaiYi.App;

internal enum ScreenshotGenerationEvent { Classifying, Generating, Delta, Completed, Failed, Usage, Prepared }
internal enum ScreenshotUsageStage { Classification, Generation }
// InvocationId is local correlation for one provider enumeration, not a server receipt/billing identifier.
// Counts are nullable observations, not a total; Reported means at least one valid count was accepted.
internal sealed record ScreenshotUsageReceipt(Guid InvocationId,ScreenshotUsageStage Stage,RequestIdentity Identity,
    Guid SourceVersion,Guid SourceId,KuaiYi.Providers.ApiProtocol Protocol,TokenUsage Tokens,bool Reported=false);
internal sealed record SmartImageOcrMetadata(Guid PreviewId,string Language,bool UsedForText);
internal sealed record ScreenshotGenerationSnapshot(Guid SourceVersion,Guid SourceId,SettingsSnapshot Settings,SettingsProfile Profile,
    KuaiYi.Providers.ApiProfile Transport,RequestProfileBinding Binding,int Number,ImageOutputMode RequestedMode,bool Regeneration,
    ImageClassification? Classification=null,ImageTranslationPlan? Plan=null,ScreenshotUsageReceipt? ClassificationUsage=null,
    ScreenshotUsageReceipt? GenerationUsage=null,bool ClassificationUsageReused=false,ImageTransform? Transform=null,
    ScreenshotNormalizationProof? NormalizationProof=null,ImageSourceKind SourceKind=ImageSourceKind.Screenshot,string? SourceNotice=null,SmartImageOcrMetadata? Ocr=null);
internal sealed record ScreenshotGenerationUpdate(RequestIdentity Identity,ScreenshotGenerationEvent Kind,ScreenshotGenerationSnapshot Snapshot,string? Text=null,string? Reason=null);

/// <summary>Independent offline P6 protocol slice. Never installed in the normal Shell screenshot path.
/// Construction requires immutable credential-free loopback fixture authority, which also pins the owned client.
/// Commands/metadata run on the UI destination; sourceCurrent must be a thread-safe source/version/lifecycle check.</summary>
internal sealed class OfflineScreenshotCoordinator:IAsyncDisposable
{
    private sealed class Source(Guid version,OwnedSourceImage image)
    {public readonly Guid Version=version,Session=Guid.NewGuid();public readonly OwnedSourceImage Image=image;public ImageClassification? Analysis;public ScreenshotUsageReceipt? AnalysisUsage;public ImageTransform? AnalysisTransform;public ImageOutputMode Mode;}
    private sealed class Job(Source source,RequestIdentity identity,long epoch,ScreenshotGenerationSnapshot snapshot)
    {public readonly Source Source=source;public readonly RequestIdentity Identity=identity;public readonly long Epoch=epoch;public ScreenshotGenerationSnapshot Snapshot=snapshot;public bool Terminal;public ScreenshotUsageStage? Stage;}
    private readonly RuntimeGate gate;
    private readonly Func<SettingsSnapshot?> settings;
    private readonly Func<Guid,Guid,bool> sourceCurrent;
    private readonly DevelopmentTranslationBoundary boundary;
    private readonly ProviderClient provider;
    private readonly ProviderEventReceiver receiver;
    private readonly Action<ScreenshotGenerationUpdate> changed;
    private readonly Dictionary<Guid,ScreenshotGenerationSnapshot> snapshots=[];
    private readonly HashSet<Task> jobs=[];private readonly object jobsSync=new();
    private Source? source;private Job? active;private bool disposed;private Exception? deliveryFailure;private long commandRevision;
    public OverlayResults Results {get;}
    public Task Running {get;private set;}=Task.CompletedTask;
    public ScreenshotGenerationSnapshot? SelectedSnapshot=>Results.SelectedVersionId is {} version?snapshots.GetValueOrDefault(version):null;
    public ImageOutputMode Mode=>source?.Mode??ImageOutputMode.Automatic;
    public OfflineScreenshotCoordinator(RuntimeGate gate,Func<SettingsSnapshot?> settings,Func<Guid,Guid,bool> sourceCurrent,
        DevelopmentTranslationBoundary boundary,SynchronizationContext destination,Action<ScreenshotGenerationUpdate> changed)
    {
        ArgumentNullException.ThrowIfNull(boundary);this.gate=gate;this.settings=settings;this.sourceCurrent=sourceCurrent;this.boundary=boundary;this.changed=changed;
        provider=new(boundary);receiver=new(gate,destination);Results=new(gate);
    }
    /// <summary>Transfers only the existing final screenshot owner. Adoption is local and sends nothing, including while Off.</summary>
    public void AdoptScreenshot(Guid version,OwnedSourceImage owned)
    {
        ArgumentNullException.ThrowIfNull(owned);
        if(ReferenceEquals(source?.Image,owned))throw new InvalidOperationException("SCREENSHOT_ALREADY_OWNED");
        if(disposed||version==Guid.Empty||owned.IsDisposed){owned.Dispose();throw new InvalidOperationException("SCREENSHOT_UNAVAILABLE");}
        try{Clear();source=new(version,owned);}catch{owned.Dispose();throw;}
    }
    public bool SetMode(ImageOutputMode mode)
    {if(disposed||source==null||!Enum.IsDefined(mode))return false;commandRevision++;source.Mode=mode;return true;}
    // Explicit action authorizes the two bounded offline phases; adopting a screenshot never calls this.
    public bool TryStart(bool userAction,bool regenerate=false,bool reclassify=false)
    {
        if(!userAction||disposed||source is not {} selected||selected.Image.IsDisposed||
            gate.Snapshot.Activation!=Activation.On||gate.Snapshot.Suspension!=Suspension.None||gate.Snapshot.Presentation==Presentation.Hidden||
            !sourceCurrent(selected.Version,selected.Image.SourceId)||settings() is not {} current||current.ReadOnly||current.RequiresRecovery)return false;
        var selectedSnapshot=regenerate?SelectedSnapshot:null;var frozen=regenerate?selectedSnapshot?.Settings:current;if(frozen==null)return false;
        var profile=frozen.Data.Profiles.FirstOrDefault(p=>p.Id==frozen.Data.ActiveProfileId);
        var live=current.Data.Profiles.FirstOrDefault(p=>p.Id==current.Data.ActiveProfileId);
        if(profile==null||live==null||profile.Id!=live.Id||!ApiProfilePolicy.RequestEquivalent(profile,live)||!boundary.Allows(profile)||!boundary.Allows(live)||
            profile.Capabilities.Vision!=Capability.Supported||!gate.TryGetProfileBinding(profile.Id,out var binding))return false;
        KuaiYi.Providers.ApiProfile transport;
        try{transport=ApiProfileMapper.Plan(profile,boundary).Transport;}catch(Exception error)when(error is ArgumentException or SettingsValidationException){return false;}
        if(!gate.HasConsentTo(new(transport.ResolveEndpoint()))||!gate.TryBeginPassiveCapture(gate.Snapshot.Mode,out var ticket))return false;
        var analysis=reclassify?null:regenerate?selectedSnapshot?.Classification:selected.Analysis;
        var analysisUsage=analysis==null?null:regenerate?selectedSnapshot?.ClassificationUsage:selected.AnalysisUsage;
        var analysisTransform=analysis==null?null:regenerate?selectedSnapshot?.Transform:selected.AnalysisTransform;
        int number=Results.NextVersionNumber(selected.Session);
        long admission=++commandRevision;var requestedMode=selected.Mode;var previous=active;RequestIdentity identity=default;
        try
        {
            if(!gate.TryStartProfileRequest(binding,selected.Session,Guid.NewGuid(),ticket,out identity,out _))return false;
            Job? admitted=null;
            // Cancelling A can synchronously Clear/Stop/Adopt/Dispose or admit C after Gate installs B.
            // Commit only if this exact command, source and identity still own the canonical destination.
            gate.TryApply(identity,()=>
            {
                if(!AdmissionCurrent(selected,frozen,profile,transport,binding,identity,admission,regenerate))return;
                var snapshot=new ScreenshotGenerationSnapshot(selected.Version,selected.Image.SourceId,frozen,profile,transport,binding,number,requestedMode,regenerate,analysis,
                    ClassificationUsage:analysisUsage,ClassificationUsageReused:analysis!=null,Transform:analysisTransform);
                if(!Results.Begin(identity))return;
                if(reclassify){selected.Analysis=null;selected.AnalysisUsage=null;selected.AnalysisTransform=null;}
                admitted=new(selected,identity,identity.Epoch,snapshot);active=admitted;snapshots[identity.VersionId]=snapshot;Prune();
            });
            if(admitted==null){RejectAdmission(identity,previous);return false;}
            Running=Run(admitted);lock(jobsSync)jobs.Add(Running);_=Observe(Running);return true;
        }
        catch(Exception error)
        {
            // Gate installs the new identity before synchronously cancelling its predecessor.
            // If that cancellation throws, this generation has no Job yet: settle its actual identity.
            List<Exception> errors=[error];if(identity!=default)ScreenshotPreviewCleanup.Attempt(errors,()=>RejectAdmission(identity,previous));
            throw new AggregateException("Offline image request admission failed.",errors);
        }
    }
    private void RejectAdmission(RequestIdentity identity,Job? previous)
    {
        try{gate.StopRequest(identity);}
        finally
        {
            // B cancelled A even when B cannot commit. Mark only that retained A, never a callback's C.
            if(previous!=null&&ReferenceEquals(active,previous)&&Results.Identity==previous.Identity&&!gate.CanApply(previous.Identity))
            {active=null;Results.MarkStopped();}
        }
    }
    private bool AdmissionCurrent(Source selected,SettingsSnapshot frozen,SettingsProfile profile,KuaiYi.Providers.ApiProfile transport,
        RequestProfileBinding binding,RequestIdentity identity,long admission,bool regenerate)
    {
        if(disposed||!ReferenceEquals(source,selected)||selected.Image.IsDisposed||settings() is not {} current||current.ReadOnly||current.RequiresRecovery||
            !regenerate&&current.Version!=frozen.Version)return false;
        var live=current.Data.Profiles.FirstOrDefault(p=>p.Id==current.Data.ActiveProfileId);
        if(live==null||live.Id!=profile.Id||!ApiProfilePolicy.RequestEquivalent(profile,live)||!boundary.Allows(live)||!boundary.Allows(transport)||transport.VisionCapability!=Capability.Supported)return false;
        bool sameSource=sourceCurrent(selected.Version,selected.Image.SourceId);var state=gate.Snapshot;
        return sameSource&&commandRevision==admission&&!disposed&&ReferenceEquals(source,selected)&&!selected.Image.IsDisposed&&
            state.Epoch==identity.Epoch&&state.Activation==Activation.On&&state.Suspension==Suspension.None&&state.Presentation!=Presentation.Hidden&&
            gate.IsRequestProfileBindingCurrent(identity,binding)&&gate.HasConsentTo(new(transport.ResolveEndpoint()));
    }
    private bool Eligible(Job job)
    {
        if(disposed||!ReferenceEquals(active,job)||!ReferenceEquals(source,job.Source)||job.Source.Image.IsDisposed||
            gate.Snapshot.Epoch!=job.Epoch||gate.Snapshot.Suspension!=Suspension.None||gate.Snapshot.Presentation==Presentation.Hidden||
            !sourceCurrent(job.Snapshot.SourceVersion,job.Snapshot.SourceId)||!gate.IsRequestProfileBindingCurrent(job.Identity,job.Snapshot.Binding)||
            settings() is not {} current||current.ReadOnly||current.RequiresRecovery)return false;
        var profile=current.Data.Profiles.FirstOrDefault(p=>p.Id==current.Data.ActiveProfileId);
        return profile!=null&&profile.Id==job.Snapshot.Profile.Id&&ApiProfilePolicy.RequestEquivalent(profile,job.Snapshot.Profile)&&
            boundary.Allows(profile)&&boundary.Allows(job.Snapshot.Transport)&&job.Snapshot.Transport.VisionCapability==Capability.Supported&&
            gate.HasConsentTo(new(job.Snapshot.Transport.ResolveEndpoint()));
    }
    private async Task Run(Job job)
    {
        await Task.Yield();if(!gate.TryLinkRequestCancellation(job.Identity,default,out var linked))return;
        using var lifetime=linked!;var budget=provider.FreezeBudget(job.Snapshot.Transport);using var timeout=budget.Link(lifetime.Token);
        PreparedScreenshot? prepared=null;byte[]? bytes=null;
        try
        {
            if(!Eligible(job))return;
            prepared=await Task.Run(()=>job.Source.Image.PrepareRequest(job.Snapshot.Transport.ImageByteLimit,timeout.Token),timeout.Token).ConfigureAwait(false);
            timeout.Check();if(!Eligible(job))return;bytes=prepared.Bytes.ToArray();
            if(job.Snapshot.Classification==null)
            {
                if(!await State(job,prepared,lifetime.Token,budget).ConfigureAwait(false))return;
                var content=new StringBuilder();bool complete=false;string? failure=null;
                var request=new TranslationRequest(job.Snapshot.Transport,ImageTranslationPlanner.ClassificationPrompt,"仅分类本次明确选择的截图，不生成正文。",bytes);
                await foreach(var envelope in provider.GenerateEnvelopesAsync(request,gate,job.Identity,null,timeout.Token,()=>Eligible(job),budget).ConfigureAwait(false))
                {
                    if(!await Receive(job,envelope,value=>
                    {
                        if(value.Kind==ModelEventKind.Usage)ApplyUsage(job,ScreenshotUsageStage.Classification,value.Usage);
                        else if(value.Kind==ModelEventKind.TextDelta)
                        {if(value.Text!.Length>ImageClassification.MaximumCharacters-content.Length)failure="CLASSIFICATION_TOO_LARGE";else content.Append(value.Text);}
                        else if(value.Kind==ModelEventKind.Completed)complete=true;
                        else if(value.Kind==ModelEventKind.Failed)failure=value.Reason??"CLASSIFICATION_FAILED";
                    },lifetime.Token,budget).ConfigureAwait(false))return;
                    if(complete||failure!=null)break;
                }
                if(failure!=null||!complete||!ImageClassification.TryParse(content.ToString(),out var classification))
                {await Fail(job,failure??(!complete?"CLASSIFICATION_INCOMPLETE":"CLASSIFICATION_INVALID"),lifetime.Token,budget).ConfigureAwait(false);return;}
                // Commit cache only on the canonical destination after full protocol completion and JSON validation.
                if(!await Receive(job,new(job.Identity,new(ModelEventKind.Started)),_=>
                {job.Source.Analysis=classification;job.Source.AnalysisUsage=job.Snapshot.ClassificationUsage;job.Source.AnalysisTransform=job.Snapshot.Transform;job.Snapshot=job.Snapshot with{Classification=classification};snapshots[job.Identity.VersionId]=job.Snapshot;},lifetime.Token,budget).ConfigureAwait(false))return;
            }
            if(job.Snapshot.Classification!.Readability==ImageReadability.Unreadable)
            {await Fail(job,"CLEARER_IMAGE_REQUIRED",lifetime.Token,budget).ConfigureAwait(false);return;}
            var plan=ImageTranslationPlanner.Create(job.Snapshot.Classification,job.Snapshot.Settings.Data.Translation,job.Snapshot.RequestedMode,job.Snapshot.Regeneration);
            if(!await Receive(job,new(job.Identity,new(ModelEventKind.Started)),_=>
            {if(job.Snapshot.NormalizationProof==null&&!CommitPrepared(job,prepared))return;job.Snapshot=job.Snapshot with{Plan=plan};BeginUsageStage(job,ScreenshotUsageStage.Generation);Publish(job,ScreenshotGenerationEvent.Generating);},lifetime.Token,budget).ConfigureAwait(false))return;
            var final=new TranslationRequest(job.Snapshot.Transport,plan.System,plan.User,bytes);
            await foreach(var envelope in provider.GenerateEnvelopesAsync(final,gate,job.Identity,null,timeout.Token,()=>Eligible(job),budget).ConfigureAwait(false))
            {
                if(!await Receive(job,envelope,value=>
                {
                    if(value.Kind==ModelEventKind.Usage)ApplyUsage(job,ScreenshotUsageStage.Generation,value.Usage);
                    else if(value.Kind==ModelEventKind.TextDelta&&Results.Append(job.Identity,value.Text!))Publish(job,ScreenshotGenerationEvent.Delta,value.Text);
                    else if(value.Kind==ModelEventKind.Completed&&Results.Complete(job.Identity)){Finish(job);Publish(job,ScreenshotGenerationEvent.Completed);}
                    else if(value.Kind==ModelEventKind.Failed&&Results.Fail(job.Identity)){Finish(job);Publish(job,ScreenshotGenerationEvent.Failed,reason:value.Reason);}
                },lifetime.Token,budget).ConfigureAwait(false))return;
                if(job.Terminal)return;
            }
            if(!job.Terminal)await Fail(job,"GENERATION_INCOMPLETE",lifetime.Token,budget).ConfigureAwait(false);
        }
        catch(OperationCanceledException)when(lifetime.IsCancellationRequested){ /* Canonical cancellation admits no late UI or second phase. */ }
        catch(ScreenshotNormalizationException){await Fail(job,"SMALLER_SCREENSHOT_REQUIRED",lifetime.Token,budget).ConfigureAwait(false);}
        catch(Exception error)when(error is not DestinationFailure)
        {await Fail(job,error is OperationCanceledException?"REQUEST_TIMEOUT":"IMAGE_PIPELINE_FAILED",lifetime.Token,budget).ConfigureAwait(false);}
        finally{if(bytes!=null)CryptographicOperations.ZeroMemory(bytes);prepared?.Dispose();if(!job.Terminal)gate.StopRequest(job.Identity);}
    }
    private async Task<bool> Receive(Job job,ModelEventEnvelope envelope,Action<ModelEvent> apply,CancellationToken cancellation,ProviderRequestBudget budget)
    {
        bool accepted=false;bool delivered=await receiver.ReceiveAsync(envelope,value=>
        {
            if(!Eligible(job))return;
            // A synthetic state/cache commit is subject to the same absolute budget as HTTP events.
            if(envelope.Event.Kind==ModelEventKind.Started&&value.Kind==ModelEventKind.Failed)
            {if(Results.Fail(job.Identity)){Finish(job);Publish(job,ScreenshotGenerationEvent.Failed,reason:value.Reason);}return;}
            accepted=true;apply(value);
        },cancellation,budget).ConfigureAwait(false);
        return delivered&&accepted&&!job.Terminal&&Eligible(job);
    }
    private Task<bool> State(Job job,PreparedScreenshot prepared,CancellationToken cancellation,ProviderRequestBudget budget)
        =>Receive(job,new(job.Identity,new(ModelEventKind.Started)),_=>{if(!CommitPrepared(job,prepared))return;BeginUsageStage(job,ScreenshotUsageStage.Classification);Publish(job,ScreenshotGenerationEvent.Classifying);},cancellation,budget);
    private bool CommitPrepared(Job job,PreparedScreenshot prepared)
    {
        if(prepared.SourceId!=job.Snapshot.SourceId||job.Snapshot.Classification!=null&&job.Snapshot.Transform!=prepared.Transform)
        {if(Results.Fail(job.Identity)){Finish(job);Publish(job,ScreenshotGenerationEvent.Failed,reason:"IMAGE_TRANSFORM_CHANGED");}return false;}
        job.Snapshot=job.Snapshot with{Transform=prepared.Transform,NormalizationProof=prepared.Proof};snapshots[job.Identity.VersionId]=job.Snapshot;Publish(job,ScreenshotGenerationEvent.Prepared);
        return !job.Terminal&&Eligible(job); // A Prepared notification may synchronously Clear/Adopt/Stop or admit C.
    }
    private void BeginUsageStage(Job job,ScreenshotUsageStage stage)
    {
        job.Stage=stage;var receipt=new ScreenshotUsageReceipt(Guid.NewGuid(),stage,job.Identity,job.Snapshot.SourceVersion,job.Snapshot.SourceId,job.Snapshot.Transport.Protocol,new(null,null));
        job.Snapshot=stage==ScreenshotUsageStage.Classification?job.Snapshot with{ClassificationUsage=receipt,ClassificationUsageReused=false}:job.Snapshot with{GenerationUsage=receipt};
        snapshots[job.Identity.VersionId]=job.Snapshot;
    }
    private void ApplyUsage(Job job,ScreenshotUsageStage stage,TokenUsage? usage)
    {
        if(job.Stage!=stage||usage==null)return;
        var previous=stage==ScreenshotUsageStage.Classification?job.Snapshot.ClassificationUsage:job.Snapshot.GenerationUsage;if(previous==null)return;
        long? input=usage.InputTokens is >=0?usage.InputTokens:null,output=usage.OutputTokens is >=0?usage.OutputTokens:null;
        if(input==null&&output==null)return;
        // Each field is the last valid server report. No summing, estimating, maximum, or completion inference.
        var next=previous with{Tokens=new(input??previous.Tokens.InputTokens,output??previous.Tokens.OutputTokens),Reported=true};if(next==previous)return;
        job.Snapshot=stage==ScreenshotUsageStage.Classification?job.Snapshot with{ClassificationUsage=next}:job.Snapshot with{GenerationUsage=next};
        snapshots[job.Identity.VersionId]=job.Snapshot;Publish(job,ScreenshotGenerationEvent.Usage);
    }
    private Task<bool> Fail(Job job,string reason,CancellationToken cancellation,ProviderRequestBudget budget)
        =>Receive(job,new(job.Identity,new(ModelEventKind.Failed,Reason:reason)),value=>
        {if(Results.Fail(job.Identity)){Finish(job);Publish(job,ScreenshotGenerationEvent.Failed,reason:value.Reason);}},cancellation,budget);
    private void Finish(Job job){job.Terminal=true;gate.MarkRequestTerminal(job.Identity);Prune();}
    private sealed class DestinationFailure(Exception inner):Exception("Offline image destination failed.",inner);
    private void Publish(Job job,ScreenshotGenerationEvent kind,string? text=null,string? reason=null)
    {try{changed(new(job.Identity,kind,job.Snapshot,text,reason));}catch(Exception error){deliveryFailure=error;gate.StopRequest(job.Identity);throw new DestinationFailure(error);}}
    private async Task Observe(Task task)
    {try{await task.ConfigureAwait(false);}catch(Exception error){deliveryFailure=error;}finally{lock(jobsSync)jobs.Remove(task);}}
    private void Prune(){var retained=Results.RetainedVersionIds;foreach(var id in snapshots.Keys.Where(id=>!retained.Contains(id)).ToArray())snapshots.Remove(id);}
    public void Stop(){commandRevision++;var job=active;active=null;if(job!=null)try{gate.StopRequest(job.Identity);}finally{Results.MarkStopped();}}
    public void Clear()
    {
        List<Exception> errors=[];ScreenshotPreviewCleanup.Attempt(errors,Stop);var previous=source;source=null;snapshots.Clear();Results.Clear();
        ScreenshotPreviewCleanup.Attempt(errors,()=>previous?.Image.Dispose());if(errors.Count>0)throw new AggregateException("Offline image clear incomplete.",errors);
    }
    public async Task ClearAsync()
    {
        List<Exception> errors=[];ScreenshotPreviewCleanup.Attempt(errors,Clear);Task[] pending;lock(jobsSync)pending=jobs.ToArray();
        try{await Task.WhenAll(pending).ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        if(errors.Count>0)throw new AggregateException("Offline image cleanup incomplete.",errors);
    }
    public void LockSession()=>ChangeLifecycle(()=>gate.SetSessionLocked(true));
    public void Hide()=>ChangeLifecycle(()=>gate.Disable(hide:true));
    private void ChangeLifecycle(Action change)
    {List<Exception> errors=[];ScreenshotPreviewCleanup.Attempt(errors,change);ScreenshotPreviewCleanup.Attempt(errors,Clear);if(errors.Count>0)throw new AggregateException("Offline image lifecycle cleanup incomplete.",errors);}
    public async ValueTask DisposeAsync()
    {disposed=true;try{await ClearAsync().ConfigureAwait(false);}finally{provider.Dispose();}if(deliveryFailure!=null)throw new InvalidOperationException("Offline image destination delivery failed.",deliveryFailure);}
}
