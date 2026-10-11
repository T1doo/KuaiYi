using System.Security.Cryptography;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class RequestSecretAdmissionTests
{
    private sealed class DirectoryPolicy:ISecretDirectoryPolicy{public void EnsurePrivate(string path)=>Directory.CreateDirectory(path);}
    private sealed class Protector:ISecretProtector
    {
        private readonly byte[] key=Enumerable.Repeat((byte)93,32).ToArray();public int Reads;public byte[]? Plain;public Action? DuringRead;
        public byte[] Protect(ReadOnlySpan<byte> input){byte[] output=new byte[input.Length+28];RandomNumberGenerator.Fill(output.AsSpan(0,12));using var cipher=new AesGcm(key,16);cipher.Encrypt(output.AsSpan(0,12),input,output.AsSpan(28),output.AsSpan(12,16));return output;}
        public byte[] Unprotect(ReadOnlySpan<byte> input){Reads++;Plain=new byte[input.Length-28];using var cipher=new AesGcm(key,16);cipher.Decrypt(input[..12],input[28..],input.Slice(12,16),Plain);DuringRead?.Invoke();return Plain;}
    }
    private sealed class Rig:IAsyncDisposable
    {
        public string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-request-secret-"+Guid.NewGuid().ToString("N"));
        public Protector Protector=new();public SettingsCoordinator Coordinator;public RuntimeGate Gate=new(requireProfileBinding:true);public SettingsRuntimeLink Link;
        public RequestProfileBinding Binding;public RequestIdentity Identity;public EndpointOrigin Origin=new(new Uri("https://fixture.invalid"));
        public Rig(){Coordinator=new(new(Path.Combine(Root,"config")),new(Path.Combine(Root,"secrets"),Protector,new DirectoryPolicy()));Link=new(Coordinator,Gate);}
        public async Task Start()
        {
            using var draft=await Coordinator.OpenDraftAsync();draft.Update(s=>s with{ActiveProfileId="p",Profiles=[new(){Id="p",BaseUrl=Origin.Value,Model="FIXTURE",Authentication=ApiAuthentication.Bearer}]});
            draft.SetKey("p","PUBLIC_FIXTURE_ONLY"u8);var saved=await Coordinator.SaveAsync(draft);Link.Initialize(saved);
            Assert.True(Gate.TryGetProfileBinding("p",out Binding));Gate.ConsentTo(Origin);Assert.True(Gate.TryEnable(Origin,true));Assert.True(Gate.TryStartProfileRequest(Binding,Guid.NewGuid(),Guid.NewGuid(),null,out Identity,out _));
        }
        public ValueTask DisposeAsync(){Gate.Disable();Link.Dispose();if(Directory.Exists(Root))Directory.Delete(Root,true);return ValueTask.CompletedTask;}
    }
    [Fact] public async Task ActualSyntheticEncryptedLeaseOnlyForAdmittedRequest()
    {
        await using var rig=new Rig();await rig.Start();using(var lease=await rig.Coordinator.OpenRequestSecretAsync(rig.Gate,rig.Identity,rig.Binding,rig.Origin))Assert.Equal("PUBLIC_FIXTURE_ONLY"u8.ToArray(),lease.Bytes.ToArray());
        Assert.Equal(1,rig.Protector.Reads);Assert.All(rig.Protector.Plain!,b=>Assert.Equal(0,b));
    }
    [Theory] [InlineData("off")] [InlineData("identity")] [InlineData("binding")] [InlineData("origin")]
    public async Task InvalidAdmissionNeverDecrypts(string kind)
    {
        await using var rig=new Rig();await rig.Start();if(kind=="off")rig.Gate.Disable();
        var identity=kind=="identity"?rig.Identity with{RequestId=Guid.NewGuid()}:rig.Identity;
        var binding=kind=="binding"?rig.Binding with{BindingId=Guid.NewGuid()}:rig.Binding;
        var origin=kind=="origin"?new EndpointOrigin(new Uri("https://elsewhere.invalid")):rig.Origin;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>rig.Coordinator.OpenRequestSecretAsync(rig.Gate,identity,binding,origin));Assert.Equal(0,rig.Protector.Reads);
    }
    [Theory] [InlineData("off")] [InlineData("replacement")] [InlineData("cancel")]
    public async Task InvalidationDuringDecryptionClearsUnpublishedLease(string kind)
    {
        await using var rig=new Rig();await rig.Start();using var cancellation=new CancellationTokenSource();
        rig.Protector.DuringRead=()=>{if(kind=="off")rig.Gate.Disable();else if(kind=="replacement")rig.Gate.TryStartProfileRequest(rig.Binding,Guid.NewGuid(),Guid.NewGuid(),null,out _,out _);else cancellation.Cancel();};
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>rig.Coordinator.OpenRequestSecretAsync(rig.Gate,rig.Identity,rig.Binding,rig.Origin,cancellation.Token));
        Assert.Equal(1,rig.Protector.Reads);Assert.All(rig.Protector.Plain!,b=>Assert.Equal(0,b));
    }
    [Fact] public async Task FinalSendGuardIsAtomicAndDeniedAttemptDoesNotStartTransport()
    {
        var gate=new RuntimeGate();var origin=new EndpointOrigin(new Uri("https://fixture.invalid"));gate.ConsentTo(origin);gate.TryEnable(origin,true);gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var id,out _);
        int sends=0;Assert.False(gate.TryBeginSend(id,origin,()=>false,()=>{sends++;return Task.FromResult(1);},out var denied));Assert.Null(denied);Assert.Equal(0,sends);
        Assert.True(gate.TryBeginSend(id,origin,()=>true,()=>{sends++;return Task.FromResult(2);},out var allowed));Assert.Equal(2,await allowed!);Assert.Equal(1,sends);
    }
    [Theory] [InlineData("off")] [InlineData("replace")] [InlineData("lock")]
    public void SourceGuardCannotReenterAndInvalidateThenSend(string operation)
    {
        var gate=new RuntimeGate();var origin=new EndpointOrigin(new Uri("https://fixture.invalid"));gate.ConsentTo(origin);gate.TryEnable(origin,true);gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var id,out _);int sends=0;
        Assert.False(gate.TryBeginSend(id,origin,()=>{if(operation=="off")gate.Disable();else if(operation=="lock")gate.SetSessionLocked(true);else gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out _,out _);return true;},()=>{sends++;return Task.FromResult(1);},out var task));Assert.Null(task);Assert.Equal(0,sends);
    }
}
