using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KuaiYi.App;
using KuaiYi.Contracts;
using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class InstalledImageBoundaryTests
{
    private static readonly TimeSpan Limit=TimeSpan.FromSeconds(5);
    private static TaskCompletionSource Signal()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static InstallationPeerToken Token()=>new("S-1-5-21-111-1001","S-1-5-5-10-11",0x123456,1,8192,false,false,false);
    [Theory][InlineData("user")][InlineData("logon")][InlineData("auth")][InlineData("session")][InlineData("low")][InlineData("high")][InlineData("elevated")][InlineData("container")][InlineData("uiaccess")]
    public void PolicyRejectsEachMismatchedOrPrivilegedPeerInEitherDirection(string field)
    {
        var own=Token();var other=field switch{"user"=>own with{UserSid="S-1-5-21-222-1001"},"logon"=>own with{LogonSid="S-1-5-5-20-21"},"auth"=>own with{AuthenticationId=1},"session"=>own with{Session=2},"low"=>own with{Integrity=4096},"high"=>own with{Integrity=12288},"elevated"=>own with{Elevated=true},"container"=>own with{AppContainer=true},_=>own with{UiAccess=true}};
        InstallationPeerPolicy.RequireSameSession(own,own);Assert.Throws<UnauthorizedAccessException>(()=>InstallationPeerPolicy.RequireSameSession(own,other));Assert.Throws<UnauthorizedAccessException>(()=>InstallationPeerPolicy.RequireSameSession(other,own));
    }
    [Theory][InlineData(IpcMessageType.BinaryChunk)][InlineData(IpcMessageType.ShowSettings)][InlineData(IpcMessageType.SubmitImagePath)]
    public async Task WrongPhaseIsRefusedAfterTheHeaderBeforeAnyPayloadRead(IpcMessageType type)
    {
        using var encoded=new MemoryStream();await IpcFrameCodec.WriteAsync(encoded,new(type,Guid.NewGuid(),new byte[type==IpcMessageType.BinaryChunk?IpcFrameCodec.BinaryLimit:1]));
        using var headerOnly=new MemoryStream(encoded.ToArray()[..IpcFrameCodec.HeaderLength]);await Assert.ThrowsAsync<InvalidDataException>(async()=>await IpcFrameCodec.ReadRestrictedAsync(headerOnly,IpcMessageType.Ping));Assert.Equal(IpcFrameCodec.HeaderLength,headerOnly.Position);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void WireRejectsUnknownFieldsAndNoncanonicalAliasesAndZerosTheExactPayload(bool unknown)
    {
        byte[] bytes=Encoding.UTF8.GetBytes(unknown?"{\"ClientNonce\":\""+new string('A',64)+"\",\"Other\":1}":" {\"ClientNonce\":\""+new string('A',64)+"\"}");
        var frame=new IpcFrame(IpcMessageType.Ping,Guid.NewGuid(),bytes);Assert.ThrowsAny<Exception>(()=>InstallationImagePipeWire.Decode<InstallationImageHello>(frame));Assert.All(bytes,b=>Assert.Equal(0,b));
    }
    [Fact]public async Task CopiedAcceptedProofCannotAuthorizeADifferentLaunchFilenameEvenWithOriginalRecordsAndLeasesValid()
    {
        string root=Path.Combine(Path.GetTempPath(),"ky-public-grant-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            string package=Path.Combine(root,"package"),app=Path.Combine(root,"app"),data=Path.Combine(root,"data");Directory.CreateDirectory(package);Directory.CreateDirectory(data);
            string executable=Path.Combine(package,"KuaiYi.App.exe");File.WriteAllText(executable,"PUBLIC_SYNTHETIC_ROLE_ONLY");
            string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
            File.WriteAllBytes(Path.Combine(package,"package.json"),JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",version="1.0",files=new[]{new{path="KuaiYi.App.exe",bytes=new FileInfo(executable).Length,sha256=Hash(executable)}}}));
            var store=new InstallationStore(app,data,()=>true);var version=await store.ApplyAsync(package,Hash(Path.Combine(package,"package.json")));
            using var admission=store.EnterAdmission();using var owner=store.ClaimExecution(admission);
            var proof=new InstallationControlledLaunch(Guid.NewGuid(),"KuaiYi.App.exe",app,data,version,Convert.ToBase64String(File.ReadAllBytes(Path.Combine(app,"execution-owner.json"))),Convert.ToBase64String(File.ReadAllBytes(Path.Combine(app,"current.json"))),null);
            File.WriteAllBytes(proof.PathFor("prepared"),proof.Bytes());proof.Claim(proof.Launch,version.DirectoryPath,data);proof.RequireImageRecords();
            var identity=new InstallationWarmIdentity(proof.Launch,proof.Fingerprint,Guid.NewGuid(),Guid.NewGuid(),1,InstallationWarmIdentity.NewNonce(),InstallationWarmIdentity.NewNonce());var grant=new InstallationWarmGrant(identity,InstallationWarmIdentity.NewNonce(),0);
            var context=new InstallationImageContext(version.DirectoryPath,data);Assert.Equal(proof,context.RequireGrant(grant));
            Guid copiedLaunch=Guid.NewGuid();File.WriteAllBytes(Path.Combine(app,$"launch-{copiedLaunch:N}-accepted.json"),proof.Bytes());
            var substituted=grant with{Identity=identity with{Launch=copiedLaunch}};proof.RequireImageRecords();
            Assert.Throws<InvalidDataException>(()=>context.RequireGrant(substituted));Assert.Equal(proof,context.RequireGrant(grant));
        }
        finally{Directory.Delete(root,true);}
    }
    private sealed class Actors
    {
        internal readonly InstallationWarmImageClient Client;
        internal readonly InstallationWarmImageControl Child;
        internal readonly Guid Source=Guid.NewGuid(),Version=Guid.NewGuid();internal int Prepared;
        internal Actors()
        {
            var proof=InstallationWarmImageTests.Proof();InstallationWarmImageControl? actor=null;
            Client=new(proof,TimeProvider.System,(line,_)=>{actor!.Handle(line);return Task.CompletedTask;});
            Child=actor=new(proof,TimeProvider.System,()=>42,(admission,lease,_,_)=>{Prepared++;Assert.True(admission.TryAccept(lease,()=>new(Source,1,Version)));return Task.CompletedTask;},_=>ValueTask.FromResult(true),(line,_)=>{Client.Receive(line);return Task.CompletedTask;});
        }
    }
    [Theory][InlineData("beforeHello")][InlineData("afterGrant")][InlineData("wrongPhase")]
    public async Task ASettledDisconnectedConnectionDoesNotPoisonTheOriginalActorOrReusablePipe(string rejected)
    {
        // Real same-process pipe IO + production connection/actors. OS authentication is deliberately NOT tested here.
        var actors=new Actors();string name="ky-public-image-"+Guid.NewGuid().ToString("N");using var pipe=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous);int checks=0;
        for(int attempt=0;attempt<2;attempt++)
        {
            using var caller=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous);Task connected=pipe.WaitForConnectionAsync();await caller.ConnectAsync();await connected;
            var serve=InstallationImagePipeConnection.ServeAsync(pipe,()=>checks++,_=>Task.CompletedTask,actors.Client,CancellationToken.None);
            InstallationWarmGrant? grant=null;
            if(attempt==0&&rejected=="wrongPhase")await IpcFrameCodec.WriteAsync(caller,new(IpcMessageType.SubmitImagePath,Guid.NewGuid(),[]));
            if(attempt==1||rejected=="afterGrant")
            {
                Guid hello=Guid.NewGuid();var nonce=InstallationWarmIdentity.NewNonce();await InstallationImagePipeWire.WriteAsync(caller,IpcMessageType.Ping,hello,new InstallationImageHello(nonce),default);
                var challenge=InstallationImagePipeWire.Decode<InstallationImageChallenge>(await InstallationImagePipeWire.ReadAsync(caller,IpcMessageType.Ping,default));Assert.Equal(hello,challenge.Hello);grant=InstallationWarmImageWire.Decode<InstallationWarmGrant>(challenge.Grant);Assert.Equal(nonce,grant.Identity.ClientNonce);
            }
            if(attempt==0)
            {caller.Dispose();await Assert.ThrowsAsync<InstallationImageConnectionRejectedException>(()=>serve.WaitAsync(Limit));Assert.False(actors.Client.Busy);Assert.True(actors.Client.IsOpen);}
            else
            {
                await InstallationImagePipeWire.WriteAsync(caller,IpcMessageType.SubmitImagePath,grant!.Identity.Request,new InstallationImageSubmit(InstallationWarmImageWire.Encode(grant),"C:\\public\\neutral.gif"),default);
                var frame=await InstallationImagePipeWire.ReadAsync(caller,IpcMessageType.SubmitImagePath,default);var result=InstallationWarmImageWire.Decode<InstallationWarmResult>(InstallationImagePipeWire.Decode<string>(frame));
                Assert.Equal(InstallationWarmOutcome.PreviewReady,result.Outcome);Assert.Equal(actors.Source,result.Source);Assert.Equal(actors.Version,result.SourceVersion);await serve.WaitAsync(Limit);Assert.False(actors.Client.Busy);
            }
            pipe.Disconnect();
        }
        Assert.Equal(1,actors.Prepared);Assert.True(checks>=5);await actors.Child.SettleAsync().WaitAsync(Limit);await actors.Client.JoinAsync();
    }
    [Fact]public async Task CancellingAConnectionStillJoinsItsOriginalNoncooperativeVerification()
    {
        var actors=new Actors();var entered=Signal();var release=Signal();using var cancellation=new CancellationTokenSource();using var pipe=new MemoryStream();
        var serve=InstallationImagePipeConnection.ServeAsync(pipe,()=>{},async token=>{entered.SetResult();await release.Task;token.ThrowIfCancellationRequested();},actors.Client,cancellation.Token);
        await entered.Task;cancellation.Cancel();Assert.False(serve.IsCompleted);Assert.False(actors.Client.Busy);
        release.SetResult();await Assert.ThrowsAsync<InstallationImageConnectionRejectedException>(()=>serve.WaitAsync(Limit));Assert.True(actors.Client.IsOpen);Assert.Equal(0,actors.Prepared);await actors.Child.SettleAsync();
    }
    [Fact]public async Task AsyncUiReserveCannotGrantUntilItsExactUiStampIsFrozenAndCannotGrantAtExpiry()
    {
        var clock=new InstallationWarmImageTests.Clock();var proof=InstallationWarmImageTests.Proof();var issuer=new InstallationWarmImageAdmission(proof,clock);var issued=issuer.Reserve(new(),0);
        InstallationWarmLease? held=null;var entered=Signal();var release=new TaskCompletionSource<long?>(TaskCreationOptions.RunContinuationsAsynchronously);int granted=0;var terminal=Signal();
        var actor=new InstallationWarmImageControl(proof,clock,()=>0,(_,_,_,_)=>throw new InvalidOperationException(),_=>ValueTask.FromResult(true),(line,_)=>{if(line.StartsWith("WARM_GRANTED|"))granted++;if(line.StartsWith("WARM_RESULT|"))terminal.TrySetResult();return Task.CompletedTask;}){ReserveUi=async(lease,_)=>{held=lease;entered.SetResult();return await release.Task;}};
        actor.Handle("WARM_RESERVE|"+InstallationWarmImageWire.Encode(issued.Grant.Identity));await entered.Task;Assert.Equal(0,granted);clock.Advance(10000);await held!.CancelJoined.Task.WaitAsync(Limit);Assert.True(actor.Admission.Busy);Assert.False(actor.OriginalCompletion.IsCompleted);
        release.SetResult(19);await terminal.Task.WaitAsync(Limit);Assert.True(await actor.OriginalCompletion);Assert.Equal(0,granted);Assert.False(actor.Admission.Busy);await actor.SettleAsync();await issuer.Revoke(issued);await issuer.RetireAsync(issued,()=>ValueTask.FromResult(true),_=>Task.CompletedTask);
    }
}
