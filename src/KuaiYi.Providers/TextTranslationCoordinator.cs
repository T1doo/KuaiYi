using System.Text;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using SettingsProfile=KuaiYi.Core.Settings.ApiProfile;

namespace KuaiYi.Providers;

public enum TextSubmission { Started, ConfirmationRequired, Reopened, Rejected }
public sealed record TextTranslationSnapshot(SelectionSourceSnapshot Source,SettingsSnapshot Settings,
    SettingsProfile Profile,RequestProfileBinding Binding,ApiProfile Transport,string Prompt,string TemplateVersion);
public sealed class TextTranslationOffer
{
    internal TextTranslationOffer(TextTranslationSnapshot snapshot,CaptureTicket ticket,bool regenerate,Guid session)
    {Snapshot=snapshot;Ticket=ticket;Regenerate=regenerate;Session=session;}
    public TextTranslationSnapshot Snapshot{get;}
    internal CaptureTicket Ticket{get;} internal bool Regenerate{get;} internal Guid Session{get;}
    internal RequestIdentity? Identity{get;set;} internal bool Launched{get;set;} internal bool Finished{get;set;}
    public bool RequiresConfirmation=>Snapshot.Source.Scalars>Snapshot.Settings.Data.Translation.AutoSoftLimitScalars;
}
public sealed record TextTranslationUpdate(RequestIdentity Identity,OverlayResultEvent Presentation,
    TextTranslationSnapshot Snapshot,string? Failure=null);

/// <summary>Prepare/submit/confirm/regenerate run on the destination thread. Transport runs off-thread;
/// every queued event and every actual HTTP attempt crosses the canonical gate. Memory only.</summary>
public sealed class TextTranslationCoordinator: IAsyncDisposable
{
    private readonly RuntimeGate gate;private readonly Func<SettingsSnapshot?> settings;
    private readonly Func<SelectionSourceSnapshot,bool,bool> sourceCurrent;
    private readonly ProviderClient provider;private readonly ProviderEventReceiver receiver;
    private readonly Func<RequestIdentity,TextTranslationSnapshot,CancellationToken,Task<SecretLease?>> openSecret;
    private readonly Action<TextTranslationUpdate> changed;private readonly DevelopmentTranslationBoundary? developmentBoundary;
    private readonly Func<SettingsProfile,ApiProfile>? transportFactory;
    private readonly Dictionary<Guid,TextTranslationSnapshot> snapshots=[];
    private readonly HashSet<Task> jobs=[];private readonly object jobsSync=new();
    private TextTranslationOffer? offered,submitted;private bool disposed;private Exception? deliveryFailure;
    private sealed record LiveRequest(RequestIdentity Identity,TextTranslationSnapshot Snapshot);
    private LiveRequest? liveRequest;
    private sealed class DestinationFailure(Exception inner):Exception("Translation destination failed.",inner);
    public OverlayResults Results{get;}
    public string? LastRejection{get;private set;}
    public Task Running{get;private set;}=Task.CompletedTask;
    public int RetainedSnapshotCount=>snapshots.Count;
    public TextTranslationCoordinator(RuntimeGate gate,Func<SettingsSnapshot?> settings,
        Func<SelectionSourceSnapshot,bool,bool> sourceCurrent,ProviderClient provider,SynchronizationContext destination,
        OverlayResults results,Action<TextTranslationUpdate> changed,
        Func<RequestIdentity,TextTranslationSnapshot,CancellationToken,Task<SecretLease?>>? openSecret=null,
        bool allowDevelopmentLoopback=false,DevelopmentTranslationBoundary? developmentBoundary=null,
        Func<SettingsProfile,ApiProfile>? transportFactory=null)
    {
        this.gate=gate;this.settings=settings;this.sourceCurrent=sourceCurrent;this.provider=provider;
        receiver=new(gate,destination);Results=results;this.changed=changed;
        this.openSecret=openSecret??((_,_,_)=>throw new InvalidOperationException("Credential store unavailable."));
        this.developmentBoundary=developmentBoundary??(allowDevelopmentLoopback?DevelopmentTranslationBoundary.FromProfile(settings()?.Data.Profiles.FirstOrDefault(p=>p.Id==settings()?.Data.ActiveProfileId)??throw new ArgumentException("Initial test profile required.")):null);
        this.transportFactory=transportFactory;
    }
    public TextTranslationSnapshot? SelectedSnapshot=>Results.SelectedVersionId is {} id&&snapshots.TryGetValue(id,out var value)?value:null;
    public bool IsOfferCurrent(TextTranslationOffer offer)=>!disposed&&ReferenceEquals(offered,offer);
    public TextTranslationOffer? Prepare(SelectionSourceSnapshot source)=>PrepareCore(source,false);
    private TextTranslationOffer? PrepareCore(SelectionSourceSnapshot source,bool explicitUse)
    {
        LastRejection=null;offered=null;
        if(disposed||settings() is not {} config||config.ReadOnly||config.RequiresRecovery||config.Version!=source.SettingsVersion)return Reject("SETTINGS_CHANGED");
        if(!DevelopmentSettingsAllowed(config))return Reject("TEST_BOUNDARY_REJECTED");
        if(!ValidText(source,config.Data.Translation.HardLimitScalars))return Reject("SOURCE_INVALID_OR_TOO_LARGE");
        if(SelectionTextPolicy.Excluded(source.Window,config.Data,(uint)Environment.ProcessId)||!sourceCurrent(source,explicitUse))return Reject("SOURCE_CHANGED_OR_EXCLUDED");
        if(!gate.TryBeginPassiveCapture(gate.Snapshot.Mode,out var ticket))return Reject("ENABLE_REQUIRED");
        var profile=config.Data.Profiles.FirstOrDefault(p=>p.Id==config.Data.ActiveProfileId);
        if(profile==null||!gate.TryGetProfileBinding(profile.Id,out var binding))return Reject("PROFILE_REQUIRED");
        try
        {
            var transport=transportFactory?.Invoke(profile)??ProfileTransport(profile,developmentBoundary!=null,developmentBoundary);
            if(developmentBoundary!=null&&!developmentBoundary.Allows(transport))return Reject("TEST_BOUNDARY_REJECTED");
            string style=config.Data.Translation.Preset.ToString().ToLowerInvariant();
            var snapshot=new TextTranslationSnapshot(source,config,profile,binding,transport,
                PromptBuilder.Build(style,config.Data.Translation.Domain,config.Data.Translation.ReaderBackground),$"base@1+{style}@1");
            return offered=new(snapshot,ticket,false,Same(snapshot,submitted?.Snapshot)?submitted!.Session:Guid.NewGuid());
        }
        catch(Exception error)when(error is ArgumentException or SettingsValidationException){return Reject("PROFILE_INVALID");}
    }
    private TextTranslationOffer? Reject(string reason){LastRejection=reason;return null;}
    public TextTranslationOffer? PrepareRegeneration(bool useCurrentSettings=false)
    {
        LastRejection=null;offered=null;
        if(disposed||SelectedSnapshot is not {} snapshot)return Reject("NO_RETAINED_SOURCE");
        if(useCurrentSettings)
        {
            var current=PrepareCore(snapshot.Source with{SettingsVersion=settings()?.Version??""},true);
            if(current==null)return null;
            return offered=new(current.Snapshot,current.Ticket,true,Results.Versions.First(v=>v.Identity.VersionId==Results.SelectedVersionId).Identity.SessionId);
        }
        if(!gate.TryBeginPassiveCapture(gate.Snapshot.Mode,out var ticket)||!Eligible(snapshot,true))return Reject("REGENERATION_NOT_ELIGIBLE");
        var version=Results.Versions.First(v=>v.Identity.VersionId==Results.SelectedVersionId);
        return offered=new(snapshot,ticket,true,version.Identity.SessionId);
    }
    public TextSubmission Submit(TextTranslationOffer offer,bool confirmed=false)
    {
        if(disposed||!ReferenceEquals(offered,offer)||offer.Launched)return TextSubmission.Rejected;
        var snapshot=offer.Snapshot;
        bool explicitUse=confirmed||offer.Regenerate;
        if(!Eligible(snapshot,explicitUse)||!offer.Regenerate&&settings()?.Version!=snapshot.Settings.Version)return TextSubmission.Rejected;
        if(offer.Identity is {} pending)
        {
            if(!gate.CanApply(pending))return TextSubmission.Rejected;
            if(!confirmed)return TextSubmission.ConfirmationRequired;
            Launch(offer,pending,true);return TextSubmission.Started;
        }
        if(!gate.IsCaptureCurrent(offer.Ticket))return TextSubmission.Rejected;
        if(!offer.Regenerate&&Same(snapshot,submitted?.Snapshot)&&
            (submitted is{Finished:false,Identity:{} existing}&&gate.CanApply(existing)||Results.HasText&&SelectedSnapshot is {} selected&&Same(snapshot,selected)))
        {Results.Reopen();return TextSubmission.Reopened;}
        if(!gate.TryStartProfileRequest(snapshot.Binding,offer.Session,Guid.NewGuid(),offer.Ticket,out var identity,out _))return TextSubmission.Rejected;
        offer.Identity=identity;submitted=offer;
        // A valid new source supersedes the previous request even while awaiting length confirmation.
        if(offer.RequiresConfirmation&&!confirmed)return TextSubmission.ConfirmationRequired;
        Launch(offer,identity,explicitUse);return TextSubmission.Started;
    }
    public void RejectConfirmation(TextTranslationOffer offer)
    {if(ReferenceEquals(offered,offer)&&!offer.Launched){if(offer.Identity is {} id)gate.StopRequest(id);offered=submitted=null;}}
    private void Launch(TextTranslationOffer offer,RequestIdentity identity,bool explicitUse)
    {
        offer.Launched=true;snapshots[identity.VersionId]=offer.Snapshot;
        if(!Results.Begin(identity))return;
        Volatile.Write(ref liveRequest,new(identity,offer.Snapshot));
        PruneSnapshots();
        Publish(new(identity,new(OverlayResultEventKind.Begin),offer.Snapshot));
        Running=RunAsync(identity,offer.Snapshot,offer.Regenerate,explicitUse);
        lock(jobsSync)jobs.Add(Running);_=ObserveJob(Running);
    }
    private async Task ObserveJob(Task job)
    {try{await job.ConfigureAwait(false);}catch(Exception error){deliveryFailure=error;}finally{lock(jobsSync)jobs.Remove(job);}}
    private async Task RunAsync(RequestIdentity identity,TextTranslationSnapshot snapshot,bool regenerate,bool explicitUse)
    {
        await Task.Yield();
        if(!gate.TryLinkRequestCancellation(identity,CancellationToken.None,out var linked))return;
        using var lifetime=linked!;using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(snapshot.Profile.TimeoutSeconds));
        SecretLease? secret=null;bool terminal=false;
        try
        {
            bool SendEligible()=>Eligible(snapshot,explicitUse)&&(regenerate||settings()?.Version==snapshot.Settings.Version)&&gate.IsRequestProfileBindingCurrent(identity,snapshot.Binding);
            if(!SendEligible()||!gate.CanApply(identity)){await FailAsync(identity,snapshot,"SOURCE_OR_SETTINGS_CHANGED",lifetime.Token).ConfigureAwait(false);return;}
            if(snapshot.Transport.Authentication!=Authentication.None)secret=await openSecret(identity,snapshot,timeout.Token).ConfigureAwait(false);
            await foreach(var envelope in provider.GenerateEnvelopesAsync(new(snapshot.Transport,snapshot.Prompt,snapshot.Source.Text),gate,identity,secret,timeout.Token,
                SendEligible).ConfigureAwait(false))
            {
                bool received=await receiver.ReceiveAsync(envelope,value=>
                {
                    OverlayResultEvent? presentation=null;
                    if(value.Kind==ModelEventKind.TextDelta&&Results.Append(identity,value.Text!))presentation=new(OverlayResultEventKind.Delta,value.Text!);
                    else if(value.Kind==ModelEventKind.Completed&&Results.Complete(identity)){presentation=new(OverlayResultEventKind.Complete);terminal=true;MarkFinished(identity);PruneSnapshots();}
                    else if(value.Kind==ModelEventKind.Failed&&Results.Fail(identity)){presentation=new(OverlayResultEventKind.Stopped);terminal=true;MarkFinished(identity);PruneSnapshots();}
                    if(presentation!=null)Publish(new(identity,presentation,snapshot,value.Kind==ModelEventKind.Failed?timeout.IsCancellationRequested&&!lifetime.IsCancellationRequested?"REQUEST_TIMEOUT":value.Reason:null));
                },lifetime.Token).ConfigureAwait(false);
                if(!received)break;
            }
            if(!terminal&&gate.CanApply(identity))await FailAsync(identity,snapshot,"REQUEST_NOT_COMPLETED",lifetime.Token).ConfigureAwait(false);
        }
        catch(OperationCanceledException)when(lifetime.IsCancellationRequested){ /* Replaced/Off/lock/stop: gate rejects every late event. */ }
        catch(Exception error)when(error is not DestinationFailure)
        {
            string reason=error is OperationCanceledException?"REQUEST_TIMEOUT":error is System.Security.Cryptography.CryptographicException?"SECRET_UNAVAILABLE":"CLIENT_FAILURE";
            await FailAsync(identity,snapshot,reason,lifetime.Token).ConfigureAwait(false);
        }
        finally{secret?.Dispose();}
    }
    private Task<bool> FailAsync(RequestIdentity identity,TextTranslationSnapshot snapshot,string reason,CancellationToken cancellation)
        =>receiver.ReceiveAsync(new(identity,new(ModelEventKind.Failed,Reason:reason)),_=>
        {if(Results.Fail(identity)){MarkFinished(identity);PruneSnapshots();Publish(new(identity,new(OverlayResultEventKind.Stopped),snapshot,reason));}},cancellation);
    private void Publish(TextTranslationUpdate update)
    {try{changed(update);}catch(Exception error){deliveryFailure=error;gate.StopRequest(update.Identity);throw new DestinationFailure(error);}}
    private void MarkFinished(RequestIdentity identity){gate.MarkRequestTerminal(identity);if(submitted?.Identity==identity)submitted.Finished=true;var live=Volatile.Read(ref liveRequest);if(live?.Identity==identity)Interlocked.CompareExchange(ref liveRequest,null,live);}
    private bool Eligible(TextTranslationSnapshot snapshot,bool explicitUse)
    {
        var current=settings();if(disposed||current==null||current.ReadOnly||current.RequiresRecovery||
            !DevelopmentSettingsAllowed(current)||developmentBoundary!=null&&(!developmentBoundary.Allows(snapshot.Profile)||!developmentBoundary.Allows(snapshot.Transport))||
            !gate.TryGetProfileBinding(snapshot.Profile.Id,out var binding)||binding!=snapshot.Binding||
            !gate.HasConsentTo(new(snapshot.Transport.ResolveEndpoint()))||SelectionTextPolicy.Excluded(snapshot.Source.Window,current.Data,(uint)Environment.ProcessId))return false;
        if(snapshot.Source.SourceKind==SelectionSourceKind.Clipboard&&!ClipboardApplicationConsent.Allowed(snapshot.Source.Window.Executable,current.Data.Privacy))return false;
        return sourceCurrent(snapshot.Source,explicitUse);
    }
    private bool DevelopmentSettingsAllowed(SettingsSnapshot current)
    {
        bool allowed=developmentBoundary==null||!current.ReadOnly&&!current.RequiresRecovery&&current.Data.Profiles.FirstOrDefault(p=>p.Id==current.Data.ActiveProfileId) is {} profile&&developmentBoundary.Allows(profile);
        if(!allowed)gate.Disable();return allowed;
    }
    public void ObserveSettings(SettingsSnapshot current)
    {
        DevelopmentSettingsAllowed(current);var live=Volatile.Read(ref liveRequest);if(live==null)return;
        var profile=current.Data.Profiles.FirstOrDefault(p=>p.Id==live.Snapshot.Profile.Id);
        if(current.Data.ActiveProfileId!=live.Snapshot.Profile.Id||profile==null||!ApiProfilePolicy.RequestEquivalent(profile,live.Snapshot.Profile))gate.StopRequest(live.Identity);
    }
    public bool CanUseSettings(SettingsSnapshot current)=>DevelopmentSettingsAllowed(current);
    private void PruneSnapshots()
    {var retained=Results.RetainedVersionIds;foreach(var id in snapshots.Keys.Where(id=>!retained.Contains(id)).ToArray())snapshots.Remove(id);}
    public void Clear(){Volatile.Write(ref liveRequest,null);if(submitted?.Identity is {} id)gate.StopRequest(id);offered=submitted=null;snapshots.Clear();Results.Clear();}
    public async ValueTask DisposeAsync()
    {
        disposed=true;if(submitted?.Identity is {} id)gate.StopRequest(id);offered=submitted=null;
        Task[] pending;lock(jobsSync)pending=jobs.ToArray();await Task.WhenAll(pending).ConfigureAwait(false);
        snapshots.Clear();if(deliveryFailure!=null)throw new InvalidOperationException("Translation destination delivery failed.",deliveryFailure);
    }
    private static bool Same(TextTranslationSnapshot a,TextTranslationSnapshot? b)=>b!=null&&a.Profile with{Headers=b.Profile.Headers}==b.Profile&&a.Profile.Headers.OrderBy(p=>p.Key).SequenceEqual(b.Profile.Headers.OrderBy(p=>p.Key))&&a.Binding==b.Binding&&
        a.Settings.Data.Translation==b.Settings.Data.Translation&&a.Source.Window==b.Source.Window&&a.Source.Text==b.Source.Text&&a.Source.SourceKind==b.Source.SourceKind&&
        a.Source.RuntimeId.SequenceEqual(b.Source.RuntimeId)&&a.Source.Ranges.SequenceEqual(b.Source.Ranges)&&a.Source.DropReceiver==b.Source.DropReceiver;
    private static bool ValidText(SelectionSourceSnapshot source,int hardLimit)
    {
        if(string.IsNullOrWhiteSpace(source.Text)||source.Text.Length>=100002||source.RuntimeId.IsDefault||source.Ranges.IsDefault)return false;
        int scalars=0;for(int i=0;i<source.Text.Length;i++,scalars++){char c=source.Text[i];if(char.IsHighSurrogate(c)){if(++i>=source.Text.Length||!char.IsLowSurrogate(source.Text[i]))return false;}else if(char.IsLowSurrogate(c))return false;}
        return scalars==source.Scalars&&scalars<=hardLimit&&scalars<=50000;
    }
    public static ApiProfile ProfileTransport(SettingsProfile profile,bool allowDevelopmentLoopback=false,DevelopmentTranslationBoundary? developmentBoundary=null)
    {
        var boundary=developmentBoundary??(allowDevelopmentLoopback?DevelopmentTranslationBoundary.FromProfile(profile):null);
        try{return ApiProfileMapper.Plan(profile,boundary).Transport;}catch(SettingsValidationException error){throw new ArgumentException("Invalid profile: "+error.Field,error);}
    }
}
