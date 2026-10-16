using KuaiYi.Core;
using KuaiYi.Core.Settings;
using SettingsProfile=KuaiYi.Core.Settings.ApiProfile;
namespace KuaiYi.Providers;
public sealed record ApiTestIntent(SettingsSnapshot Settings,SettingsProfile Profile,ApiRequestPlan Plan,RequestProfileBinding Binding,long Epoch,long DraftRevision=0,ApiTestPurpose Purpose=ApiTestPurpose.TextConnection);
public sealed record ApiTestUpdate(RequestIdentity Identity,ModelEvent Event,ApiRequestPlan Plan,ApiTestPurpose Purpose=ApiTestPurpose.TextConnection);
/// <summary>Explicit fixed public samples. Vision is local-fixture-only; shares admission without capturing or enabling On.</summary>
public sealed class ApiConnectionTestWorkflow:IAsyncDisposable
{
    private readonly RuntimeGate gate;private readonly Func<SettingsSnapshot?> settings;private readonly ProviderClient provider;
    private readonly ProviderEventReceiver receiver;private readonly DevelopmentTranslationBoundary? boundary;
    private readonly Func<RequestIdentity,ApiTestIntent,CancellationToken,Task<SecretLease?>> openSecret;
    private RequestIdentity? active;private Task running=Task.CompletedTask;private volatile bool disposed;
    private readonly object sync=new();private ApiTestIntent? offered;
    private long draftRevision,commandRevision;
    public ApiConnectionTestWorkflow(RuntimeGate gate,Func<SettingsSnapshot?> settings,ProviderClient provider,SynchronizationContext destination,
        Func<RequestIdentity,ApiTestIntent,CancellationToken,Task<SecretLease?>> openSecret,DevelopmentTranslationBoundary? boundary=null)
    {this.gate=gate;this.settings=settings;this.provider=provider;receiver=new(gate,destination);this.openSecret=openSecret;this.boundary=boundary;}
    public ApiTestIntent? Prepare(string profileId,out string reason)=>PrepareCore(profileId,ApiTestPurpose.TextConnection,out reason);
    public ApiTestIntent? PrepareVision(string profileId,out string reason)=>PrepareCore(profileId,ApiTestPurpose.VisionFixture,out reason);
    private ApiTestIntent? PrepareCore(string profileId,ApiTestPurpose purpose,out string reason)
    {
        Interlocked.Increment(ref commandRevision);offered=null;
        reason="";if(disposed||!running.IsCompleted){reason="TEST_ALREADY_RUNNING";return null;}
        if(purpose==ApiTestPurpose.VisionFixture&&boundary==null){reason="VISION_TEST_LOCAL_FIXTURE_ONLY";return null;}
        if(gate.HasRunningGeneration){reason="STOP_GENERATION_FIRST";return null;}
        if(settings() is not {} config||config.ReadOnly||config.RequiresRecovery){reason="SETTINGS_UNAVAILABLE";return null;}
        var profile=config.Data.Profiles.FirstOrDefault(p=>p.Id==profileId);
        if(profile==null||!gate.TryGetProfileBinding(profileId,out var binding)){reason="PROFILE_REQUIRED";return null;}
        if(profile.Authentication!=ApiAuthentication.None&&profile.SecretRef==null){reason="KEY_REQUIRED";return null;}
        try
        {
            var plan=ApiProfileMapper.Plan(profile,boundary);
            if(purpose==ApiTestPurpose.VisionFixture&&(!boundary!.Allows(profile)||!boundary.Allows(plan.Transport))){reason="VISION_TEST_LOCAL_FIXTURE_ONLY";return null;}
            return offered=new(config,profile,plan,binding,gate.Snapshot.Epoch,Volatile.Read(ref draftRevision),purpose);
        }
        catch(Exception error)when(error is ArgumentException or SettingsValidationException){reason="CONFIG_INVALID";return null;}
    }
    private bool Current(ApiTestIntent intent)
    {
        var config=settings();var profile=config?.Data.Profiles.FirstOrDefault(p=>p.Id==intent.Profile.Id);
        return !disposed&&intent.DraftRevision==Volatile.Read(ref draftRevision)&&config!=null&&!config.ReadOnly&&!config.RequiresRecovery&&config.Version==intent.Settings.Version&&
            profile!=null&&ApiProfilePolicy.RequestEquivalent(profile,intent.Profile)&&gate.TryGetProfileBinding(intent.Profile.Id,out var binding)&&binding==intent.Binding&&
            (boundary==null||boundary.Allows(intent.Profile)&&boundary.Allows(intent.Plan.Transport))&&
            (intent.Purpose==ApiTestPurpose.TextConnection||intent.Purpose==ApiTestPurpose.VisionFixture&&boundary!=null&&intent.Profile.Authentication==ApiAuthentication.None&&intent.Profile.SecretRef==null);
    }
    public bool IsIntentCurrent(ApiTestIntent intent)=>Current(intent);
    public void Reject(ApiTestIntent intent){if(ReferenceEquals(offered,intent)){offered=null;Interlocked.Increment(ref commandRevision);}}
    public Task? Start(ApiTestIntent intent,bool confirmed,Action<ApiTestUpdate> apply)
    {
        ArgumentNullException.ThrowIfNull(intent);ArgumentNullException.ThrowIfNull(apply);
        if(!confirmed||!ReferenceEquals(offered,intent)||!running.IsCompleted||gate.HasRunningGeneration)return null;
        long command=Interlocked.Increment(ref commandRevision);
        if(!Current(intent)||command!=Volatile.Read(ref commandRevision)||!ReferenceEquals(offered,intent))return null;
        offered=null;RequestIdentity identity=default;Task? pending=null;
        try
        {
            var endpoint=intent.Plan.Transport.ResolveEndpoint();
            bool started=intent.Purpose==ApiTestPurpose.VisionFixture?
                gate.TryStartVisionTest(intent.Binding,endpoint,intent.Profile.Protocol,intent.Epoch,out identity,out _):
                gate.TryStartConnectionTest(intent.Binding,new(endpoint),intent.Epoch,out identity,out _);
            if(!started)return null;
            gate.TryApply(identity,()=>
            {
                bool eligible=Current(intent);
                lock(sync)
                {
                    if(!eligible||disposed||command!=Volatile.Read(ref commandRevision)||!gate.CanApply(identity))return;
                    active=identity;pending=running=RunAsync(intent,identity,apply);
                }
            });
            if(pending==null)gate.StopRequest(identity);return pending;
        }
        catch(Exception original)
        {
            try{if(identity!=default)gate.StopRequest(identity);}
            catch(Exception cleanup){throw new AggregateException("API test admission and cleanup failed.",original,cleanup);}
            throw;
        }
    }
    private async Task RunAsync(ApiTestIntent intent,RequestIdentity identity,Action<ApiTestUpdate> apply)
    {
        await Task.Yield();if(!gate.TryLinkRequestCancellation(identity,CancellationToken.None,out var linked))return;
        using var lifetime=linked!;var budget=provider.FreezeBudget(intent.Plan.Transport);using var timeout=budget.Link(lifetime.Token);SecretLease? secret=null;
        try
        {
            if(!Current(intent))return;
            if(intent.Plan.Transport.Authentication!=Authentication.None)secret=await openSecret(identity,intent,timeout.Token).ConfigureAwait(false);
            var request=intent.Purpose==ApiTestPurpose.VisionFixture?
                new TranslationRequest(intent.Plan.Transport,ApiVisionTestContent.System,ApiVisionTestContent.Source,ApiVisionTestContent.CreateImage(),ApiVisionTestContent.Mime):
                new TranslationRequest(intent.Plan.Transport,ApiConnectionTestContent.System,ApiConnectionTestContent.Source);
            await foreach(var item in provider.GenerateEnvelopesAsync(request,gate,identity,secret,timeout.Token,()=>Current(intent),budget).ConfigureAwait(false))
            {
                bool terminal=false;
                if(!await receiver.ReceiveAsync(item,value=>terminal=ApplyCurrent(intent,identity,item,value,budget,lifetime.Token,apply),lifetime.Token,budget).ConfigureAwait(false)||terminal)break;
            }
        }
        catch(OperationCanceledException)when(lifetime.IsCancellationRequested){ /* The exact cancelled test owns settlement; no late result or new capability. */ }
        catch(Exception error)
        {
            string reason=error is OperationCanceledException?"REQUEST_TIMEOUT":"TEST_REQUEST_FAILED";
            var failure=new ModelEventEnvelope(identity,new(ModelEventKind.Failed,Reason:reason));
            await receiver.ReceiveAsync(failure,value=>ApplyCurrent(intent,identity,failure,value,budget,lifetime.Token,apply),lifetime.Token,budget).ConfigureAwait(false);
        }
        finally
        {
            secret?.Dispose();gate.MarkRequestTerminal(identity);
            try{gate.StopRequest(identity);}finally{lock(sync)if(active==identity)active=null;}
        }
    }
    private bool ApplyCurrent(ApiTestIntent intent,RequestIdentity identity,ModelEventEnvelope envelope,ModelEvent value,
        ProviderRequestBudget budget,CancellationToken lifetime,Action<ApiTestUpdate> apply)
    {
        if(!Current(intent))return false;
        bool terminal=false;
        gate.TryApply(identity,()=>
        {
            if(lifetime.IsCancellationRequested)return;
            var admitted=ProviderEventReceiver.Admit(envelope,value,budget);
            apply(new(identity,admitted,intent.Plan,intent.Purpose));terminal=admitted.Kind is ModelEventKind.Completed or ModelEventKind.Failed;
        });return terminal;
    }
    public void Cancel(){RequestIdentity? identity;lock(sync)identity=active;if(identity is {} id)gate.StopRequest(id);}
    public void DraftEdited(){Interlocked.Increment(ref draftRevision);Interlocked.Increment(ref commandRevision);offered=null;Cancel();}
    public async ValueTask DisposeAsync(){disposed=true;offered=null;Interlocked.Increment(ref commandRevision);Cancel();await running.ConfigureAwait(false);}
}
