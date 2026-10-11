using System.Buffers.Binary;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text.Json;
using KuaiYi.Contracts;
using KuaiYi.Core;
using KuaiYi.Imaging;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed class DesktopReceiverTests
{
    private const string Origin="chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/"; // Test-only declaration, never installed.
    private sealed class Decision:ILocalImageConfirmation
    {
        public TaskCompletionSource<LocalImageConfirmation> Asked {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Answer {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;
        public async Task<bool> ConfirmAsync(LocalImageConfirmation request,CancellationToken cancellation)
        {Calls++;Asked.TrySetResult(request);return await Answer.Task.WaitAsync(cancellation);}
    }
    private sealed class Clock:TimeProvider
    {
        private long ticks;private TimerCallback? callback;private object? state;
        public override long TimestampFrequency=>TimeSpan.TicksPerSecond;
        public override long GetTimestamp()=>ticks;
        public override ITimer CreateTimer(TimerCallback action,object? value,TimeSpan due,TimeSpan period){callback=action;state=value;return new Timer();}
        public void Expire(){ticks+=TimeSpan.FromSeconds(30).Ticks;callback!(state);}
        public void AdvanceOnly()=>ticks+=TimeSpan.FromSeconds(30).Ticks;
        private sealed class Timer:ITimer{public bool Change(TimeSpan due,TimeSpan period)=>true;public void Dispose(){}public ValueTask DisposeAsync()=>ValueTask.CompletedTask;}
    }
    private sealed class Queued:SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback,object?)> queue=new();
        public TaskCompletionSource Posted {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override void Post(SendOrPostCallback action,object? state){lock(queue)queue.Enqueue((action,state));Posted.TrySetResult();}
        public void Drain(){while(true){(SendOrPostCallback,object?) next;lock(queue){if(queue.Count==0)return;next=queue.Dequeue();}next.Item1(next.Item2);}}
    }
    private sealed class TrackingStream(Stream inner):Stream
    {
        public List<Memory<byte>> PayloadBuffers {get;}=[];
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellation=default)
        {
            int read=await inner.ReadAsync(buffer,cancellation);
            if(buffer.Length>IpcFrameCodec.HeaderLength&&read>0)PayloadBuffers.Add(buffer);return read;
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellation=default)=>inner.WriteAsync(buffer,cancellation);
        public override Task FlushAsync(CancellationToken cancellation)=>inner.FlushAsync(cancellation);
        protected override void Dispose(bool disposing){if(disposing)inner.Dispose();base.Dispose(disposing);}
        public override bool CanRead=>true;public override bool CanWrite=>true;public override bool CanSeek=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush()=>inner.Flush();public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
    }
    private sealed class Fixture:IAsyncDisposable
    {
        public RuntimeGate Gate {get;}=new();public Decision Decision {get;}=new();public Clock Clock {get;}=new();
        public LocalImageInbox Inbox {get;}public DesktopReceiverSession Session {get;}
        public NamedPipeServerStream Server {get;}public NamedPipeClientStream Client {get;}
        public Guid Connection {get;}=Guid.NewGuid();public Guid Operation {get;}=Guid.NewGuid();
        public bool PeerCurrent=true;public int Verifications;public Task<bool> Run {get;private set;}=null!;
        public TrackingStream Tracked {get;}
        private readonly CancellationTokenSource lifetime=new();
        public Fixture(SynchronizationContext? destination=null,IReadOnlyList<string>? origins=null)
        {
            string name="KuaiYi.ReceiverTest."+Guid.NewGuid().ToString("N");
            Server=new(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
            Client=new(".",name,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
            Tracked=new(Server);
            Inbox=new(Gate,destination??new SynchronizationContext());
            // Synthetic verified-peer input only: no Windows token/PID/path claim is made by these Linux tests.
            Session=new(Gate,Decision,Inbox,origins??[Origin],new(123,1,"SyntheticSID","SyntheticNativeHost",1),()=>{Verifications++;return PeerCurrent;},Clock);
        }
        public async Task Connect()
        {
            var connecting=Server.WaitForConnectionAsync();await Client.ConnectAsync(1000);await connecting;
            Run=Session.RunAsync(Tracked,lifetime.Token);
        }
        public async Task Send<T>(T value,Guid? connection=null,IpcMessageType type=IpcMessageType.SubmitImage)
        {byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value);try{await IpcFrameCodec.WriteAsync(Client,new(type,connection??Connection,bytes));await Client.FlushAsync();}finally{CryptographicOperations.ZeroMemory(bytes);}}
        public async Task<JsonElement> Read()
        {
            var frame=await IpcFrameCodec.ReadAsync(Client).AsTask().WaitAsync(TimeSpan.FromSeconds(3));Assert.NotNull(frame);
            try{Assert.Equal(Connection,frame.RequestId);Assert.Equal(IpcMessageType.SubmitImage,frame.Type);using var doc=JsonDocument.Parse(frame.Payload);return doc.RootElement.Clone();}
            finally{CryptographicOperations.ZeroMemory(frame.Payload);}
        }
        public Task Offer(string origin=Origin)=>Send(new{phase="OfferImage",operationId=Operation,origin});
        public async Task<BrowserGrant> Authorize()
        {
            await Offer();var asked=await Decision.Asked.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(Connection,asked.ConnectionId);Assert.Equal(Operation,asked.OperationId);Assert.Equal(Origin,asked.DeclaredOrigin);Assert.Equal(123,asked.ClientProcessId);
            Assert.Null(Inbox.Current);Assert.Equal(Activation.Off,Gate.Snapshot.Activation);
            Decision.Answer.SetResult(true);var ready=await Read();Assert.Equal("Ready",ready.GetProperty("phase").GetString());
            return new(ready.GetProperty("operationId").GetGuid(),ready.GetProperty("identity").Deserialize<RequestIdentity>(),ready.GetProperty("nonce").GetGuid());
        }
        public async Task Begin(BrowserGrant grant,byte[] image,int? length=null,string? hash=null,uint? width=null)
        {
            var size=ImagePolicy.InspectPng(image);
            await Send(new{phase="BeginImage",grant,totalLength=length??image.Length,mime="image/png",width=width??size.Width,height=size.Height,sha256=hash??Convert.ToHexString(SHA256.HashData(image))});
        }
        public async Task Chunk(byte[] image,int index=0,int? count=null)
        {
            byte[] data=new byte[(count??image.Length)+4];BinaryPrimitives.WriteInt32LittleEndian(data,index);image.AsSpan(0,data.Length-4).CopyTo(data.AsSpan(4));
            try{await IpcFrameCodec.WriteAsync(Client,new(IpcMessageType.BinaryChunk,Connection,data));await Client.FlushAsync();}finally{CryptographicOperations.ZeroMemory(data);}
        }
        public async Task<bool> Finish()
        {
            bool result=await Run.WaitAsync(TimeSpan.FromSeconds(3));
            foreach(var memory in Tracked.PayloadBuffers)Assert.All(memory.ToArray(),b=>Assert.Equal(0,b));return result;
        }
        public async ValueTask DisposeAsync(){lifetime.Cancel();Client.Dispose();Server.Dispose();if(Run!=null)await Run.WaitAsync(TimeSpan.FromSeconds(3));Inbox.Dispose();lifetime.Dispose();}
    }
    private static byte[] Png()=>File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"kuaiyi-test.png"));

    [Fact]
    public async Task ActualDesktopChannelOneExplicitDecisionCommitsExactLocalBytesWithoutNetworkOrPassivePermission()
    {
        await using var f=new Fixture();await f.Connect();var endpoint=new EndpointOrigin(new Uri("https://public-fixture.invalid"));f.Gate.ConsentTo(endpoint);
        await using var sender=new DesktopPipeChannel(f.Client,Origin);var offer=sender.OfferAsync(f.Operation,default).AsTask();
        await f.Decision.Asked.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.False(offer.IsCompleted);Assert.Null(f.Inbox.Current);f.Decision.Answer.SetResult(true);
        var grant=await offer.WaitAsync(TimeSpan.FromSeconds(3));byte[] bytes=Png();using var assembler=new ImageTransferAssembler();assembler.Begin(f.Operation,bytes.Length,"image/png",Convert.ToHexString(SHA256.HashData(bytes)));assembler.Chunk(f.Operation,0,bytes);using var image=assembler.Commit(f.Operation);
        await sender.SubmitAsync(grant,image,ImagePolicy.InspectPng(bytes),default);Assert.True(await f.Finish());Assert.Equal(bytes,f.Inbox.Current!.Bytes.ToArray());Assert.Equal(1,f.Decision.Calls);Assert.Equal(3,f.Verifications);
        Assert.False(f.Gate.CanSend(grant.Identity,endpoint));int sends=0;Assert.False(f.Gate.TryBeginSend(grant.Identity,endpoint,()=>{sends++;return Task.FromResult(1);},out _));Assert.Equal(0,sends);
        Assert.False(f.Gate.TryBeginPassiveCapture(TriggerMode.Selection,out _));
        var retained=f.Inbox.Current.Bytes;f.Gate.Disable();f.Inbox.Clear();Assert.Null(f.Inbox.Current);Assert.All(retained.ToArray(),b=>Assert.Equal(0,b));
    }
    [Theory][InlineData("reject")][InlineData("off")][InlineData("locked")][InlineData("replaced")][InlineData("suspended")][InlineData("timeout")][InlineData("clock-only")][InlineData("disconnect")][InlineData("early-input")]
    public async Task PendingDesktopDecisionCannotOutliveRejectionOrChangedState(string action)
    {
        await using var f=new Fixture();await f.Connect();await f.Offer();await f.Decision.Asked.Task.WaitAsync(TimeSpan.FromSeconds(3));
        if(action=="off")f.Gate.Disable();if(action=="locked")f.Gate.SetSessionLocked(true);if(action=="suspended")f.Gate.SetSuspension(Suspension.Settings,true);
        if(action=="replaced"){f.Gate.TryEnableLocalOperation(true);f.Gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out _,out _);}
        if(action=="timeout")f.Clock.Expire();if(action=="disconnect")f.Client.Dispose();if(action=="early-input")await f.Send(new{phase="CommitImage"});
        if(action=="clock-only")f.Clock.AdvanceOnly();
        f.Decision.Answer.TrySetResult(action!="reject");Assert.False(await f.Finish());Assert.Null(f.Inbox.Current);Assert.Equal(1,f.Decision.Calls);
        Assert.False(f.Gate.CanSend(default,new(new Uri("https://public-fixture.invalid"))));
    }
    [Theory][InlineData("missing-origin")][InlineData("wrong-origin")][InlineData("wrong-role")][InlineData("unknown-command")][InlineData("locked")]
    public async Task ConfigurationRoleAndSuspensionRejectBeforeAnyDesktopPrompt(string reason)
    {
        await using var f=new Fixture(origins:reason=="missing-origin"?[]:null);await f.Connect();
        if(reason=="wrong-role")f.PeerCurrent=false;if(reason=="locked")f.Gate.SetSessionLocked(true);
        if(reason=="unknown-command")await f.Send(new{phase="GetApiKey"},type:IpcMessageType.Ping);else await f.Offer(reason=="wrong-origin"?"chrome-extension://bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/":Origin);
        Assert.False(await f.Finish());Assert.Equal(0,f.Decision.Calls);Assert.Null(f.Inbox.Current);Assert.Equal(Activation.Off,f.Gate.Snapshot.Activation);
    }
    private static BrowserGrant Mutate(BrowserGrant grant,string field)=>field switch
    {
        "operation"=>grant with{OperationId=Guid.NewGuid()},"nonce"=>grant with{Nonce=Guid.NewGuid()},
        "session"=>grant with{Identity=grant.Identity with{SessionId=Guid.NewGuid()}},"version"=>grant with{Identity=grant.Identity with{VersionId=Guid.NewGuid()}},
        "request"=>grant with{Identity=grant.Identity with{RequestId=Guid.NewGuid()}},"epoch"=>grant with{Identity=grant.Identity with{Epoch=grant.Identity.Epoch+1}},_=>throw new InvalidOperationException()
    };
    [Theory]
    [InlineData("operation",false)][InlineData("nonce",false)][InlineData("session",false)][InlineData("version",false)][InlineData("request",false)][InlineData("epoch",false)]
    [InlineData("operation",true)][InlineData("nonce",true)][InlineData("session",true)][InlineData("version",true)][InlineData("request",true)][InlineData("epoch",true)]
    public async Task EveryGrantFieldIsBoundAtBeginAndCommit(string field,bool atCommit)
    {
        await using var f=new Fixture();await f.Connect();var grant=await f.Authorize();byte[] bytes=Png();
        if(atCommit){await f.Begin(grant,bytes);await f.Read();await f.Chunk(bytes);await f.Send(new{phase="CommitImage",grant=Mutate(grant,field)});}
        else await f.Begin(Mutate(grant,field),bytes);
        Assert.False(await f.Finish());Assert.Null(f.Inbox.Current);Assert.False(f.Gate.CanApply(grant.Identity));Assert.Equal(Activation.Off,f.Gate.Snapshot.Activation);
    }
    [Theory][InlineData("off",false)][InlineData("off",true)][InlineData("lock",false)][InlineData("lock",true)][InlineData("replace",false)][InlineData("replace",true)][InlineData("peer",false)][InlineData("peer",true)][InlineData("deadline",false)][InlineData("deadline",true)]
    public async Task BeginAndCommitRecheckStateDeadlineAndActualPeerRole(string change,bool atCommit)
    {
        await using var f=new Fixture();await f.Connect();var grant=await f.Authorize();byte[] bytes=Png();
        if(atCommit){await f.Begin(grant,bytes);await f.Read();await f.Chunk(bytes);}
        if(change=="off")f.Gate.Disable();if(change=="lock")f.Gate.SetSessionLocked(true);if(change=="replace")f.Gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out _,out _);
        if(change=="peer")f.PeerCurrent=false;if(change=="deadline")f.Clock.Expire();
        if(change=="peer"){if(atCommit)await f.Send(new{phase="CommitImage",grant});else await f.Begin(grant,bytes);}
        Assert.False(await f.Finish());Assert.Null(f.Inbox.Current);Assert.False(f.Gate.CanApply(grant.Identity));
    }
    [Theory][InlineData("connection")][InlineData("cancel")][InlineData("duplicate-begin")][InlineData("duplicate-offer")][InlineData("duplicate-chunk")][InlineData("reorder")][InlineData("short")][InlineData("hash")][InlineData("dimensions")][InlineData("limit")][InlineData("disconnect")]
    public async Task ConnectionOrderingAndImageIntegrityFailWithoutLocalPublication(string kind)
    {
        await using var f=new Fixture();await f.Connect();var grant=await f.Authorize();byte[] bytes=Png();
        if(kind=="connection")await f.Send(new{phase="BeginImage"},Guid.NewGuid());
        else if(kind=="cancel")await f.Send(new{phase="CancelImage",grant});
        else if(kind=="duplicate-offer")await f.Offer();
        else if(kind=="limit")await f.Begin(grant,bytes,ImageTransferAssembler.ImageLimit+1);
        else
        {
            await f.Begin(grant,bytes,hash:kind=="hash"?new string('0',64):null,width:kind=="dimensions"?1u:null);await f.Read();
            if(kind=="disconnect")f.Client.Dispose();
            else if(kind=="duplicate-begin")await f.Begin(grant,bytes);
            else
            {
                await f.Chunk(bytes,kind=="reorder"?1:0,kind=="short"?bytes.Length-1:null);
                if(kind=="duplicate-chunk")await f.Chunk(bytes);else if(kind!="reorder")await f.Send(new{phase="CommitImage",grant});
            }
        }
        Assert.False(await f.Finish());Assert.Null(f.Inbox.Current);Assert.False(f.Gate.CanApply(grant.Identity));
    }
    [Theory][InlineData("off")][InlineData("disconnect")][InlineData("deadline")][InlineData("clock-only")][InlineData("replace")]
    public async Task QueuedCommitCannotSurviveRevocationBeforeActualDestinationApplication(string change)
    {
        var queued=new Queued();await using var f=new Fixture(queued);await f.Connect();var grant=await f.Authorize();byte[] bytes=Png();await f.Begin(grant,bytes);await f.Read();await f.Chunk(bytes);await f.Send(new{phase="CommitImage",grant});
        await queued.Posted.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.Null(f.Inbox.Current);
        if(change=="off")f.Gate.Disable();if(change=="disconnect")f.Client.Dispose();if(change=="deadline")f.Clock.Expire();if(change=="replace")f.Gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out _,out _);
        if(change=="clock-only"){f.Clock.AdvanceOnly();queued.Drain();}
        Assert.False(await f.Finish());queued.Drain();Assert.Null(f.Inbox.Current);
    }
    [Fact]
    public void CleanupForOldIdentityCannotCancelNewerRequest()
    {
        var gate=new RuntimeGate();Assert.True(gate.TryStartConfirmedLocalRequest(0,Guid.NewGuid(),Guid.NewGuid(),out var a,out _));
        Assert.True(gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var b,out var token));gate.StopRequest(a);Assert.True(gate.CanApply(b));Assert.False(token.IsCancellationRequested);
    }
    [Fact]
    public void ProductionAppPolicyRejectsThisLinuxEnvironmentBeforeOpeningAnyReceiver()
    {
        if(OperatingSystem.IsWindows())return;Assert.Equal(BrowserFailure.UnsupportedPlatform,Assert.Throws<BrowserException>(InstalledHostPolicy.LoadForApp).Code);
    }
    [Fact]
    public async Task GrantFromAnotherRealConnectionIsNeverAccepted()
    {
        await using var a=new Fixture();await using var b=new Fixture();await a.Connect();await b.Connect();
        var grantA=await a.Authorize();var grantB=await b.Authorize();Assert.NotEqual(grantA.Nonce,grantB.Nonce);
        await b.Begin(grantA,Png());Assert.False(await b.Finish());Assert.Null(b.Inbox.Current);Assert.True(a.Gate.CanApply(grantA.Identity));
        await a.Send(new{phase="CancelImage",grant=grantA});Assert.False(await a.Finish());
    }
    [Fact]
    public async Task SuccessfulConnectionEndsAfterExactlyOneCommitAndCannotIssueAnotherGrant()
    {
        await using var f=new Fixture();await f.Connect();var grant=await f.Authorize();byte[] bytes=Png();await f.Begin(grant,bytes);await f.Read();await f.Chunk(bytes);await f.Send(new{phase="CommitImage",grant});
        var reply=await f.Read();Assert.Equal("Committed",reply.GetProperty("phase").GetString());Assert.True(await f.Finish());
        Assert.Null(await IpcFrameCodec.ReadAsync(f.Client).AsTask().WaitAsync(TimeSpan.FromSeconds(3)));Assert.Equal(1,f.Decision.Calls);Assert.Equal(grant.Identity,f.Inbox.Current!.Identity);
    }
    [Fact]
    public async Task LocalInboxUseKeepsOwnedBufferAliveAgainstConcurrentClearAndRejectsStaleUse()
    {
        await using var f=new Fixture();await f.Connect();var grant=await f.Authorize();byte[] bytes=Png();await f.Begin(grant,bytes);await f.Read();await f.Chunk(bytes);await f.Send(new{phase="CommitImage",grant});await f.Read();Assert.True(await f.Finish());
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var clearing=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ReadOnlyMemory<byte> owned=f.Inbox.Current!.Bytes;
        var usingImage=Task.Run(()=>f.Inbox.TryUseCurrent(grant.Identity,image=>
        {
            entered.SetResult();release.Task.WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();Assert.Equal(bytes,image.Bytes.ToArray());
        }));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));var clear=Task.Run(()=>{clearing.SetResult();f.Inbox.Clear();});
        try{await clearing.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.False(clear.IsCompleted);}
        finally{release.TrySetResult();}
        Assert.True(await usingImage.WaitAsync(TimeSpan.FromSeconds(3)));await clear.WaitAsync(TimeSpan.FromSeconds(3));Assert.All(owned.ToArray(),b=>Assert.Equal(0,b));
        Assert.False(f.Inbox.TryUseCurrent(grant.Identity,_=>throw new InvalidOperationException()));Assert.Null(f.Inbox.CurrentIdentity);
    }
    [Theory][InlineData("duplicate")][InlineData("extra")][InlineData("missing")][InlineData("bad-uuid")][InlineData("nested-grant")]
    public async Task MalformedOfferNeverAsksForConsent(string kind)
    {
        await using var f=new Fixture();await f.Connect();
        string payload=kind switch
        {
            "duplicate"=>$"{{\"phase\":\"OfferImage\",\"phase\":\"OfferImage\",\"operationId\":\"{f.Operation}\",\"origin\":\"{Origin}\"}}",
            "extra"=>JsonSerializer.Serialize(new{phase="OfferImage",operationId=f.Operation,origin=Origin,consent=true}),
            "missing"=>JsonSerializer.Serialize(new{phase="OfferImage",operationId=f.Operation}),
            "bad-uuid"=>JsonSerializer.Serialize(new{phase="OfferImage",operationId="not-a-guid",origin=Origin}),
            _=>JsonSerializer.Serialize(new{phase="OfferImage",operationId=f.Operation,origin=Origin,grant=new{activation="On"}})
        };
        await IpcFrameCodec.WriteAsync(f.Client,new(IpcMessageType.SubmitImage,f.Connection,System.Text.Encoding.UTF8.GetBytes(payload)));await f.Client.FlushAsync();
        Assert.False(await f.Finish());Assert.Equal(0,f.Decision.Calls);Assert.Null(f.Inbox.Current);
    }
    [Fact]
    public async Task ExplicitLocalReceiveCannotInheritExistingEndpointConsentOrActivatePassiveCapture()
    {
        var gate=new RuntimeGate();var endpoint=new EndpointOrigin(new Uri("https://public-fixture.invalid"));gate.ConsentTo(endpoint);Assert.True(gate.TryEnable(endpoint,true));
        Assert.True(gate.TryBeginPassiveCapture(TriggerMode.Selection,out var oldCapture));
        Assert.True(gate.TryStartConfirmedLocalRequest(gate.Snapshot.Epoch,Guid.NewGuid(),Guid.NewGuid(),out var local,out _));
        Assert.False(gate.IsCaptureCurrent(oldCapture));Assert.False(gate.CanSend(local,endpoint));Assert.False(gate.TryBeginPassiveCapture(TriggerMode.Selection,out _));
        Assert.True(gate.TryEnable(endpoint,true));Assert.True(gate.CanSend(local,endpoint)); // Separate explicit endpoint enable, never automatic in receiving code.
        await Task.CompletedTask;
    }
    [Fact]
    public async Task TruncatedIpcPayloadClearsItsActualAllocatedBuffer()
    {
        using var wire=new MemoryStream();await IpcFrameCodec.WriteAsync(wire,new(IpcMessageType.BinaryChunk,Guid.NewGuid(),Enumerable.Repeat((byte)7,80).ToArray()));
        using var truncated=new MemoryStream(wire.ToArray()[..^4]);using var tracking=new TrackingStream(truncated);
        await Assert.ThrowsAsync<EndOfStreamException>(async()=>await IpcFrameCodec.ReadAsync(tracking));Assert.NotEmpty(tracking.PayloadBuffers);
        foreach(var buffer in tracking.PayloadBuffers)Assert.All(buffer.ToArray(),b=>Assert.Equal(0,b));
    }
}
