using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.MockProvider;
using KuaiYi.Providers;
using Xunit;
using TransportLease=KuaiYi.Providers.SecretLease;
namespace KuaiYi.Provider.Tests;
public sealed class SettingsSecretCommandTests(ServerFixture fixture):IClassFixture<ServerFixture>
{
    private sealed class Destination:SynchronizationContext{public override void Post(SendOrPostCallback action,object? state)=>action(state);}
    private sealed class QueueCompleted:SynchronizationContext
    {
        public Func<bool> Hold=()=>false;public readonly TaskCompletionSource Queued=new(TaskCreationOptions.RunContinuationsAsynchronously);private readonly ConcurrentQueue<Action> queue=new();
        public override void Post(SendOrPostCallback action,object? state)
        {if(Hold()){queue.Enqueue(()=>action(state));Queued.TrySetResult();}else action(state);}
        public void Drain(){while(queue.TryDequeue(out var callback))callback();}
    }
    private sealed class PrivateDirectory:ISecretDirectoryPolicy{public void EnsurePrivate(string directory)=>Directory.CreateDirectory(directory);}
    private sealed class SyntheticProtector:ISecretProtector
    {
        private readonly byte[] key=Enumerable.Repeat((byte)61,32).ToArray();public int Reads;
        public byte[] Protect(ReadOnlySpan<byte> source){byte[] result=new byte[source.Length+28];RandomNumberGenerator.Fill(result.AsSpan(0,12));using var cipher=new AesGcm(key,16);cipher.Encrypt(result.AsSpan(0,12),source,result.AsSpan(28),result.AsSpan(12,16));return result;}
        public byte[] Unprotect(ReadOnlySpan<byte> source){Reads++;byte[] result=new byte[source.Length-28];using var cipher=new AesGcm(key,16);cipher.Decrypt(source[..12],source[28..],source.Slice(12,16),result);return result;}
    }
    private sealed class Rig:IAsyncDisposable
    {
        public readonly string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-secret-command-"+Guid.NewGuid().ToString("N")),Run=Guid.NewGuid().ToString("N");
        public RuntimeGate Gate=new(requireProfileBinding:true);public readonly ProviderClient Provider=new();public SettingsCoordinator Coordinator=null!;public SecretFileStore Store=null!;
        public SettingsDraft Draft=null!;public SettingsRuntimeLink Link=null!;public ApiConnectionTestWorkflow Flow=null!;public SettingsWindow Window=null!;public TransportLease? Lease;public CancellationToken RequestCancellation;
        public readonly SyntheticProtector Protector=new();public readonly List<ApiTestUpdate> Updates=[];public string SavedReference=null!;
        public static async Task<Rig> Create(Uri root,string scenario="held-headers",SynchronizationContext? destination=null)
        {
            var rig=new Rig();var prefix=new Uri(root,$"runs/{rig.Run}/{scenario}/");rig.Store=new(Path.Combine(rig.Root,"secrets"),rig.Protector,new PrivateDirectory());rig.Coordinator=new(new(Path.Combine(rig.Root,"config")),rig.Store);
            using(var seed=await rig.Coordinator.OpenDraftAsync())
            {seed.Update(s=>s with{ActiveProfileId="p",Profiles=[new(){Id="p",Model="PUBLIC_FIXTURE_MODEL",BaseUrl=prefix.AbsoluteUri,AllowLoopbackHttp=true,Authentication=ApiAuthentication.Bearer}]});seed.SetKey("p",Encoding.UTF8.GetBytes(MockProviderHost.FixtureCredential));await rig.Coordinator.SaveAsync(seed);}
            rig.Draft=await rig.Coordinator.OpenDraftAsync();rig.SavedReference=rig.Coordinator.Effective!.Data.Profiles[0].SecretRef!;rig.Link=new(rig.Coordinator,rig.Gate);rig.Link.Initialize(rig.Coordinator.Effective);
            rig.Flow=new(rig.Gate,()=>rig.Coordinator.Effective,rig.Provider,destination??new Destination(),async(id,intent,ct)=>
            {rig.RequestCancellation=ct;using var lease=await rig.Coordinator.OpenRequestSecretAsync(rig.Gate,id,intent.Binding,new(intent.Plan.Transport.ResolveEndpoint()),ct);return rig.Lease=new TransportLease(lease.Bytes.Span);});
            if(destination is QueueCompleted queue)queue.Hold=()=>rig.Updates.Any(u=>u.Event.Kind==ModelEventKind.Usage);rig.Window=new(rig.Draft,rig.Flow);Assert.Equal(0,rig.Protector.Reads);Assert.Equal("",rig.Window.PasswordText);return rig;
        }
        public Task Start(){var task=Flow.Start(Flow.Prepare("p",out _)!,true,Updates.Add)!;Window.ObserveRunning(task);return task;}
        public void AssertDraftOnly(){Assert.Null(Draft.Data.Profiles[0].SecretRef);Assert.True(Draft.HasPendingKey("p"));Assert.Equal(SavedReference,Coordinator.Effective!.Data.Profiles[0].SecretRef);Assert.False(Store.IsRevoked(SavedReference));Assert.Equal(0,Window.PasswordChanges);}
        public async ValueTask DisposeAsync(){Gate.Disable();await Flow.DisposeAsync();Provider.Dispose();Link.Dispose();Draft.Dispose();if(Directory.Exists(Root))Directory.Delete(Root,true);}
    }
    private async Task Barrier(string run)
    {using var http=new HttpClient();using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(true){var reply=await http.GetFromJsonAsync<System.Text.Json.JsonElement>(new Uri(fixture.BaseUri,$"barriers/{run}"),deadline.Token);if(reply.GetProperty("ready").GetBoolean())return;await Task.Delay(10,deadline.Token);}}
    private async Task Release(string run){using var http=new HttpClient();using var response=await http.PostAsync(new Uri(fixture.BaseUri,$"barriers/{run}/release"),null);response.EnsureSuccessStatusCode();}
    private async Task<CapturedRequest[]> Requests(string run){using var http=new HttpClient();return(await http.GetFromJsonAsync<CapturedRequest[]>(new Uri(fixture.BaseUri,$"requests/{run}")))!;}
    [Fact] public async Task EmptyPasswordClearCommandCancelsActiveSavedKeyTestAndReleasesLease()
    {
        await using var rig=await Rig.Create(fixture.BaseUri);var task=rig.Start();await Barrier(rig.Run);Assert.NotNull(rig.Lease);rig.Window.InvokeClearButtonCommand();rig.AssertDraftOnly();
        await Release(rig.Run);await task.WaitAsync(TimeSpan.FromSeconds(6));Assert.True(rig.RequestCancellation.IsCancellationRequested);Assert.DoesNotContain(rig.Updates,u=>u.Event.Kind==ModelEventKind.Completed);Assert.Throws<ObjectDisposedException>(()=>rig.Lease!.HeaderValue());Assert.Single(await Requests(rig.Run));Assert.Equal(1,rig.Protector.Reads);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task SecretClearCommandsInvalidatePendingConfirmationWithNoControlValueChange(bool preset)
    {
        await using var rig=await Rig.Create(fixture.BaseUri);var intent=rig.Flow.Prepare("p",out _)!;if(preset)rig.Window.InvokePresetSecretCommand();else rig.Window.InvokeClearButtonCommand();rig.AssertDraftOnly();
        var task=rig.Flow.Start(intent,true,rig.Updates.Add);if(task!=null){await Barrier(rig.Run);await Release(rig.Run);await task;}Assert.Null(task);Assert.Empty(await Requests(rig.Run));Assert.Equal(0,rig.Protector.Reads);
    }
    [Fact] public async Task ClearCommandRejectsAlreadyQueuedCompletionAndDisposesSavedKeyLease()
    {
        var destination=new QueueCompleted();await using var rig=await Rig.Create(fixture.BaseUri,"fragmented",destination);var task=rig.Start();await destination.Queued.Task.WaitAsync(TimeSpan.FromSeconds(6));rig.Window.InvokeClearButtonCommand();destination.Drain();await task.WaitAsync(TimeSpan.FromSeconds(6));
        rig.AssertDraftOnly();Assert.DoesNotContain(rig.Updates,u=>u.Event.Kind==ModelEventKind.Completed);Assert.Throws<ObjectDisposedException>(()=>rig.Lease!.HeaderValue());Assert.Single(await Requests(rig.Run));
    }
    [Fact] public async Task ClearThenSaveRevokesStoredReferenceUsingOriginalTransaction()
    {
        await using var rig=await Rig.Create(fixture.BaseUri);var task=rig.Start();await Barrier(rig.Run);rig.Window.InvokeClearButtonCommand();await Release(rig.Run);await task.WaitAsync(TimeSpan.FromSeconds(6));Assert.DoesNotContain(rig.Updates,u=>u.Event.Kind==ModelEventKind.Completed);
        await rig.Coordinator.SaveAsync(rig.Draft);Assert.True(rig.Store.IsRevoked(rig.SavedReference));Assert.Null(rig.Coordinator.Effective!.Data.Profiles[0].SecretRef);Assert.Throws<ObjectDisposedException>(()=>rig.Lease!.HeaderValue());Assert.Equal(0,rig.Window.PasswordChanges);
    }
    [Fact] public async Task NoSelectedProfileDoesNotMutateDraftOrCancelUnrelatedTest()
    {
        await using var rig=await Rig.Create(fixture.BaseUri);var task=rig.Start();await Barrier(rig.Run);var window=new SettingsWindow(rig.Draft,rig.Flow,profileId:null);window.InvokeClearButtonCommand();Assert.False(rig.Draft.HasPendingKey("p"));Assert.False(rig.RequestCancellation.IsCancellationRequested);await Release(rig.Run);await task;Assert.Contains(rig.Updates,u=>u.Event.Kind==ModelEventKind.Completed);
    }
}
