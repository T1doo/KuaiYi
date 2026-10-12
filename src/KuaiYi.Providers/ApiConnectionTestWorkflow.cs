using KuaiYi.Core;
using KuaiYi.Core.Settings;
using SettingsProfile=KuaiYi.Core.Settings.ApiProfile;
namespace KuaiYi.Providers;
public sealed record ApiTestIntent(SettingsSnapshot Settings,SettingsProfile Profile,ApiRequestPlan Plan,RequestProfileBinding Binding,long Epoch,long DraftRevision=0);
public sealed record ApiTestUpdate(RequestIdentity Identity,ModelEvent Event,ApiRequestPlan Plan);
/// <summary>Explicit fixed public sample only. Shares admission with generation, never captures or enables On.</summary>
public sealed class ApiConnectionTestWorkflow:IAsyncDisposable
{
    private readonly RuntimeGate gate;private readonly Func<SettingsSnapshot?> settings;private readonly ProviderClient provider;
    private readonly ProviderEventReceiver receiver;private readonly DevelopmentTranslationBoundary? boundary;
    private readonly Func<RequestIdentity,ApiTestIntent,CancellationToken,Task<SecretLease?>> openSecret;
    private RequestIdentity? active;private Task running=Task.CompletedTask;private bool disposed;
    private readonly object sync=new();
    private long draftRevision;
    public ApiConnectionTestWorkflow(RuntimeGate gate,Func<SettingsSnapshot?> settings,ProviderClient provider,SynchronizationContext destination,
        Func<RequestIdentity,ApiTestIntent,CancellationToken,Task<SecretLease?>> openSecret,DevelopmentTranslationBoundary? boundary=null)
    {this.gate=gate;this.settings=settings;this.provider=provider;receiver=new(gate,destination);this.openSecret=openSecret;this.boundary=boundary;}
    public ApiTestIntent? Prepare(string profileId,out string reason)
    {
        reason="";if(disposed||!running.IsCompleted){reason="TEST_ALREADY_RUNNING";return null;}
        if(gate.HasRunningGeneration){reason="STOP_GENERATION_FIRST";return null;}
        if(settings() is not {} config||config.ReadOnly||config.RequiresRecovery){reason="SETTINGS_UNAVAILABLE";return null;}
        var profile=config.Data.Profiles.FirstOrDefault(p=>p.Id==profileId);
        if(profile==null||!gate.TryGetProfileBinding(profileId,out var binding)){reason="PROFILE_REQUIRED";return null;}
        if(profile.Authentication!=ApiAuthentication.None&&profile.SecretRef==null){reason="KEY_REQUIRED";return null;}
        try{return new(config,profile,ApiProfileMapper.Plan(profile,boundary),binding,gate.Snapshot.Epoch,Volatile.Read(ref draftRevision));}
        catch(Exception error)when(error is ArgumentException or SettingsValidationException){reason="CONFIG_INVALID";return null;}
    }
    private bool Current(ApiTestIntent intent)=>!disposed&&intent.DraftRevision==Volatile.Read(ref draftRevision)&&settings()?.Version==intent.Settings.Version&&
        gate.TryGetProfileBinding(intent.Profile.Id,out var binding)&&binding==intent.Binding&&
        (boundary==null||boundary.Allows(intent.Profile)&&boundary.Allows(intent.Plan.Transport));
    public Task? Start(ApiTestIntent intent,bool confirmed,Action<ApiTestUpdate> apply)
    {
        if(!confirmed||!Current(intent)||!running.IsCompleted||gate.HasRunningGeneration)return null;
        var origin=new EndpointOrigin(intent.Plan.Transport.ResolveEndpoint());
        if(!gate.TryStartConnectionTest(intent.Binding,origin,intent.Epoch,out var identity,out _))return null;
        lock(sync)active=identity;return running=RunAsync(intent,identity,apply);
    }
    private async Task RunAsync(ApiTestIntent intent,RequestIdentity identity,Action<ApiTestUpdate> apply)
    {
        await Task.Yield();if(!gate.TryLinkRequestCancellation(identity,CancellationToken.None,out var linked))return;
        using var lifetime=linked!;var budget=provider.FreezeBudget(intent.Plan.Transport);using var timeout=budget.Link(lifetime.Token);SecretLease? secret=null;
        try
        {
            if(!Current(intent))return;
            if(intent.Plan.Transport.Authentication!=Authentication.None)secret=await openSecret(identity,intent,timeout.Token).ConfigureAwait(false);
            await foreach(var item in provider.GenerateEnvelopesAsync(new(intent.Plan.Transport,ApiConnectionTestContent.System,ApiConnectionTestContent.Source),gate,identity,secret,timeout.Token,()=>Current(intent),budget).ConfigureAwait(false))
            {
                bool terminal=false;
                if(!await receiver.ReceiveAsync(item,value=>terminal=ApplyCurrent(intent,identity,item,value,budget,lifetime.Token,apply),lifetime.Token,budget).ConfigureAwait(false)||terminal)break;
            }
        }
        catch(OperationCanceledException)when(lifetime.IsCancellationRequested){ /* Explicit cancellation/Off/profile replacement owns the final state. */ }
        catch(Exception error)
        {
            string reason=error is OperationCanceledException?"REQUEST_TIMEOUT":"TEST_REQUEST_FAILED";
            var failure=new ModelEventEnvelope(identity,new(ModelEventKind.Failed,Reason:reason));
            await receiver.ReceiveAsync(failure,value=>ApplyCurrent(intent,identity,failure,value,budget,lifetime.Token,apply),lifetime.Token,budget).ConfigureAwait(false);
        }
        finally{secret?.Dispose();gate.MarkRequestTerminal(identity);gate.StopRequest(identity);lock(sync)if(active==identity)active=null;}
    }
    private bool ApplyCurrent(ApiTestIntent intent,RequestIdentity identity,ModelEventEnvelope envelope,ModelEvent value,
        ProviderRequestBudget budget,CancellationToken lifetime,Action<ApiTestUpdate> apply)
    {
        if(!Current(intent))return false;
        bool terminal=false;
        // Current may consult external settings code. Recheck identity/cancellation and deadline after it returns.
        gate.TryApply(identity,()=>
        {
            if(lifetime.IsCancellationRequested)return;
            var admitted=ProviderEventReceiver.Admit(envelope,value,budget);
            apply(new(identity,admitted,intent.Plan));terminal=admitted.Kind is ModelEventKind.Completed or ModelEventKind.Failed;
        });return terminal;
    }
    public void Cancel(){RequestIdentity? identity;lock(sync)identity=active;if(identity is {} id)gate.StopRequest(id);}
    public void DraftEdited(){Interlocked.Increment(ref draftRevision);Cancel();}
    public async ValueTask DisposeAsync(){disposed=true;Cancel();await running.ConfigureAwait(false);}
}
