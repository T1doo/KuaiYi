using KuaiYi.Core;
using KuaiYi.Core.Settings;
using SettingsProfile=KuaiYi.Core.Settings.ApiProfile;
namespace KuaiYi.Providers;
public sealed record ApiTestIntent(SettingsSnapshot Settings,SettingsProfile Profile,ApiRequestPlan Plan,RequestProfileBinding Binding,long Epoch);
public sealed record ApiTestUpdate(RequestIdentity Identity,ModelEvent Event,ApiRequestPlan Plan);
/// <summary>Explicit fixed public sample only. Shares admission with generation, never captures or enables On.</summary>
public sealed class ApiConnectionTestWorkflow:IAsyncDisposable
{
    private readonly RuntimeGate gate;private readonly Func<SettingsSnapshot?> settings;private readonly ProviderClient provider;
    private readonly ProviderEventReceiver receiver;private readonly DevelopmentTranslationBoundary? boundary;
    private readonly Func<RequestIdentity,ApiTestIntent,CancellationToken,Task<SecretLease?>> openSecret;
    private RequestIdentity? active;private Task running=Task.CompletedTask;private bool disposed;
    private readonly object sync=new();
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
        try{return new(config,profile,ApiProfileMapper.Plan(profile,boundary),binding,gate.Snapshot.Epoch);}
        catch(Exception error)when(error is ArgumentException or SettingsValidationException){reason="CONFIG_INVALID";return null;}
    }
    private bool Current(ApiTestIntent intent)=>!disposed&&settings()?.Version==intent.Settings.Version&&
        gate.TryGetProfileBinding(intent.Profile.Id,out var binding)&&binding==intent.Binding&&
        (boundary==null||boundary.Allows(intent.Profile)&&boundary.Allows(intent.Plan.Transport));
    public Task? Start(ApiTestIntent intent,bool confirmed,Action<ApiTestUpdate> apply)
    {
        if(!confirmed||!Current(intent)||!running.IsCompleted||gate.HasRunningGeneration)return null;
        var origin=new EndpointOrigin(intent.Plan.Transport.ResolveEndpoint());gate.ConsentTo(origin);
        if(!gate.TryStartConnectionTest(intent.Binding,origin,intent.Epoch,out var identity,out _))return null;
        lock(sync)active=identity;return running=RunAsync(intent,identity,apply);
    }
    private async Task RunAsync(ApiTestIntent intent,RequestIdentity identity,Action<ApiTestUpdate> apply)
    {
        await Task.Yield();if(!gate.TryLinkRequestCancellation(identity,CancellationToken.None,out var linked))return;
        using var lifetime=linked!;using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(intent.Profile.TimeoutSeconds));SecretLease? secret=null;
        try
        {
            if(!Current(intent))return;
            if(intent.Plan.Transport.Authentication!=Authentication.None)secret=await openSecret(identity,intent,timeout.Token).ConfigureAwait(false);
            await foreach(var item in provider.GenerateEnvelopesAsync(new(intent.Plan.Transport,ApiConnectionTestContent.System,ApiConnectionTestContent.Source),gate,identity,secret,timeout.Token,()=>Current(intent)).ConfigureAwait(false))
            {
                if(!await receiver.ReceiveAsync(item,value=>{if(Current(intent))apply(new(identity,value,intent.Plan));},lifetime.Token).ConfigureAwait(false))break;
            }
        }
        catch(OperationCanceledException)when(lifetime.IsCancellationRequested){ /* Explicit cancellation/Off/profile replacement owns the final state. */ }
        catch(Exception error)
        {
            string reason=error is OperationCanceledException?"REQUEST_TIMEOUT":"TEST_REQUEST_FAILED";
            await receiver.ReceiveAsync(new(identity,new(ModelEventKind.Failed,Reason:reason)),value=>{if(Current(intent))apply(new(identity,value,intent.Plan));},lifetime.Token).ConfigureAwait(false);
        }
        finally{secret?.Dispose();gate.MarkRequestTerminal(identity);gate.StopRequest(identity);lock(sync)if(active==identity)active=null;}
    }
    public void Cancel(){RequestIdentity? identity;lock(sync)identity=active;if(identity is {} id)gate.StopRequest(id);}
    public async ValueTask DisposeAsync(){disposed=true;Cancel();await running.ConfigureAwait(false);}
}
