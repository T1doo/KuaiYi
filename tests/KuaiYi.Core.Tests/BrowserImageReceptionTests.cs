using System.IO.Pipes;
using System.Security.Cryptography;
using KuaiYi.Contracts;
using KuaiYi.Core;
using KuaiYi.Imaging;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed class BrowserImageReceptionTests
{
    private const string Origin="chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/";
    private sealed class Immediate:SynchronizationContext{public override void Post(SendOrPostCallback action,object? state)=>action(state);}
    private sealed class Decision:ILocalImageConfirmation
    {
        public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Hold,CancellationIgnored,Returned;public int Calls;public Action<CancellationToken>? OnToken=null;
        public async Task<bool> ConfirmAsync(LocalImageConfirmation request,CancellationToken cancellation)
        {Calls++;OnToken?.Invoke(cancellation);Entered.TrySetResult();if(Hold){if(CancellationIgnored)await Release.Task;else await Release.Task.WaitAsync(cancellation);}Returned=true;return true;}
    }
    private sealed class HeldRead(Stream inner):Stream
    {
        public bool HoldCleanup,Disposed,ThrowOnDispose;public readonly IOException DisposeError=new("SYNTHETIC PIPE DISPOSAL");public readonly TaskCompletionSource Pending=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> bytes,CancellationToken cancellation=default)
        {try{return await inner.ReadAsync(bytes,cancellation);}catch(Exception)when(HoldCleanup){Pending.TrySetResult();await Release.Task;throw;}}
        protected override void Dispose(bool disposing){Disposed=true;if(disposing)inner.Dispose();base.Dispose(disposing);if(ThrowOnDispose){ThrowOnDispose=false;throw DisposeError;}}
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> bytes,CancellationToken cancellation=default)=>inner.WriteAsync(bytes,cancellation);
        public override Task FlushAsync(CancellationToken cancellation)=>inner.FlushAsync(cancellation);
        public override bool CanRead=>true;public override bool CanWrite=>true;public override bool CanSeek=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush()=>inner.Flush();public override int Read(byte[] b,int o,int n)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int n)=>throw new NotSupportedException();public override long Seek(long n,SeekOrigin o)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();
    }
    private sealed class Fixture:IAsyncDisposable
    {
        public readonly RuntimeGate Shell=new();public readonly Decision Prompt=new();public readonly BrowserImageReception Reception;
        public readonly NamedPipeServerStream Server;public readonly NamedPipeClientStream Client;public readonly HeldRead Stream;public readonly DesktopReceiverSession Session;
        public long Source,Result;public string Settings="synthetic";public bool Visible=true,HoldPreparation;public int Prepares;public Action<BrowserImagePreparationPermit>? BeforePrepare=null;public ReadOnlyMemory<byte> Borrowed;
        public readonly TaskCompletionSource Preparing=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<bool>? Running;private readonly CancellationTokenSource stop=new();
        public Fixture()
        {
            var name="KuaiYi.BrowserImageReceptionTest."+Guid.NewGuid().ToString("N");Server=new(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);Client=new(".",name,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);Stream=new(Server);
            Reception=new(new Immediate(),()=>{var s=Shell.Snapshot;return new(s.Epoch,s.Activation,Settings,Source,Result,Visible&&s.Suspension==Suspension.None&&s.Presentation!=Presentation.Hidden);},()=>true,Prompt,async (bytes,permit)=>{Assert.True(Stream.Disposed);Borrowed=bytes;BeforePrepare?.Invoke(permit);if(!permit.IsCurrent)return;Prepares++;Preparing.TrySetResult();if(HoldPreparation)await Release.Task;});
            // Explicit synthetic peer only; actual framing/pipe and destination/ownership code below.
            Session=new(Reception.TransferGate,Reception,Reception.Inbox,[Origin],new(123,1,"synthetic","SyntheticNativeHost",1),()=>true);
        }
        public async Task<DesktopPipeChannel> Connect()
        {var listening=Server.WaitForConnectionAsync();await Client.ConnectAsync(1000);await listening;Running=Session.RunAndNotifyAsync(Stream,Reception.CompleteAsync,stop.Token);return new(Client,Origin);}
        public static byte[] Png()=>File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"kuaiyi-test.png"));
        public static async Task Submit(DesktopPipeChannel channel,BrowserGrant grant)
        {byte[] bytes=Png();using var assembler=new ImageTransferAssembler();assembler.Begin(grant.OperationId,bytes.Length,"image/png",Convert.ToHexString(SHA256.HashData(bytes)));assembler.Chunk(grant.OperationId,0,bytes);using var image=assembler.Commit(grant.OperationId);await channel.SubmitAsync(grant,image,ImagePolicy.InspectPng(bytes),default);}
        public async ValueTask DisposeAsync(){Stream.Release.TrySetResult();Release.TrySetResult();Prompt.Release.TrySetResult();stop.Cancel();Client.Dispose();Server.Dispose();if(Running!=null)await Running.WaitAsync(TimeSpan.FromSeconds(5));Reception.Dispose();stop.Dispose();}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ExactCommittedHandoffWaitsForOriginalPendingReadAndPreservesShellActivation(bool on)
    {
        await using var f=new Fixture();var endpoint=new EndpointOrigin(new Uri("https://synthetic.invalid"));f.Shell.ConsentTo(endpoint);if(on)Assert.True(f.Shell.TryEnable(endpoint,true));var state=f.Shell.Snapshot;f.Stream.HoldCleanup=true;
        await using var channel=await f.Connect();var grant=await channel.OfferAsync(Guid.NewGuid(),default);Assert.Equal(state,f.Shell.Snapshot);Assert.False(f.Reception.TransferGate.CanSend(grant.Identity,endpoint));await Fixture.Submit(channel,grant);await f.Stream.Pending.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0,f.Prepares);Assert.False(f.Running!.IsCompleted);Assert.NotNull(f.Reception.Inbox.Current);Assert.Equal(state,f.Shell.Snapshot);f.Stream.Release.SetResult();Assert.True(await f.Running.WaitAsync(TimeSpan.FromSeconds(5)));Assert.Equal(1,f.Prepares);Assert.Equal(state,f.Shell.Snapshot);Assert.Null(f.Reception.Inbox.Current);Assert.True(f.Borrowed.Span.IndexOfAnyExcept((byte)0)<0);Assert.False(f.Reception.TransferGate.CanApply(grant.Identity));
        await f.Reception.CompleteAsync(new(Guid.NewGuid(),grant.Identity));Assert.Equal(1,f.Prepares);
    }
    [Fact]public async Task ClaimedInputRemainsOwnedAcrossReceptionRevocationUntilTheOriginalPreparationTaskEnds()
    {
        await using var f=new Fixture{HoldPreparation=true};await using var channel=await f.Connect();var grant=await channel.OfferAsync(Guid.NewGuid(),default);await Fixture.Submit(channel,grant);await f.Preparing.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.False(f.Running!.IsCompleted);Assert.Null(f.Reception.Inbox.Current);Assert.Equal(Fixture.Png(),f.Borrowed.ToArray());f.Reception.Revoke();Assert.False(f.Running.IsCompleted);Assert.Contains(f.Borrowed.ToArray(),b=>b!=0);f.Release.SetResult();Assert.True(await f.Running);Assert.True(f.Borrowed.Span.IndexOfAnyExcept((byte)0)<0);Assert.Equal(Activation.Off,f.Shell.Snapshot.Activation);
    }
    [Theory][InlineData("source")][InlineData("result")][InlineData("settings")][InlineData("locked")]
    public async Task ShellChangeBeforeSettledCommitCanReceiveButNeverPrepareOrRestoreThatImage(string change)
    {
        await using var f=new Fixture();f.Stream.HoldCleanup=true;await using var channel=await f.Connect();var grant=await channel.OfferAsync(Guid.NewGuid(),default);await Fixture.Submit(channel,grant);await f.Stream.Pending.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if(change=="source")f.Source++;if(change=="result")f.Result++;if(change=="settings")f.Settings="changed";if(change=="locked")f.Shell.SetSessionLocked(true);
        f.Stream.Release.SetResult();Assert.True(await f.Running!.WaitAsync(TimeSpan.FromSeconds(5)));Assert.Equal(0,f.Prepares);Assert.Null(f.Reception.Inbox.Current);Assert.False(f.Reception.Busy);Assert.False(f.Reception.TransferGate.CanApply(grant.Identity));
    }
    [Fact]public async Task ChangedShellDuringDefaultRejectPromptCannotObtainATransferGrant()
    {
        await using var f=new Fixture();f.Prompt.Hold=true;await using var channel=await f.Connect();var offer=channel.OfferAsync(Guid.NewGuid(),default).AsTask();await f.Prompt.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));f.Source++;f.Prompt.Release.SetResult();await Assert.ThrowsAnyAsync<Exception>(()=>offer);Assert.False(await f.Running!.WaitAsync(TimeSpan.FromSeconds(5)));Assert.False(f.Reception.Busy);Assert.Equal(0,f.Prepares);Assert.Equal(Activation.Off,f.Shell.Snapshot.Activation);
    }
    [Fact]public async Task DisconnectRetainsTheOriginalUncooperativeDecisionTaskUntilItActuallyReturns()
    {
        await using var f=new Fixture();f.Prompt.Hold=f.Prompt.CancellationIgnored=true;await using var channel=await f.Connect();var offer=channel.OfferAsync(Guid.NewGuid(),default).AsTask();await f.Prompt.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));f.Client.Dispose();await Assert.ThrowsAnyAsync<Exception>(()=>offer);Assert.False(f.Running!.IsCompleted);Assert.True(f.Reception.Busy);f.Prompt.Release.SetResult();Assert.False(await f.Running.WaitAsync(TimeSpan.FromSeconds(5)));Assert.False(f.Reception.Busy);Assert.Equal(0,f.Prepares);
    }
    [Fact]public async Task BackgroundRevocationInTheLastCheckToTaskInstallationGapRemainsEffective()
    {
        await using var f=new Fixture();f.BeforePrepare=permit=>{Task.Run(f.Reception.Revoke).GetAwaiter().GetResult();Assert.False(permit.IsCurrent);Assert.True(permit.Token.IsCancellationRequested);};
        await using var channel=await f.Connect();var grant=await channel.OfferAsync(Guid.NewGuid(),default);await Fixture.Submit(channel,grant);Assert.True(await f.Running!.WaitAsync(TimeSpan.FromSeconds(5)));Assert.Equal(0,f.Prepares);Assert.True(f.Borrowed.Span.IndexOfAnyExcept((byte)0)<0);Assert.Equal(Activation.Off,f.Shell.Snapshot.Activation);
    }
    [Fact]public async Task OriginalAsyncCancellationCallbacksKeepTheClaimedReceiptOwnedUntilTheySettle()
    {
        await using var f=new Fixture();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);CancellationTokenRegistration registration=default;
        f.BeforePrepare=permit=>{registration=permit.Token.Register(()=>{entered.TrySetResult();release.Task.GetAwaiter().GetResult();});Task.Run(f.Reception.Revoke).GetAwaiter().GetResult();};
        try
        {
            await using var channel=await f.Connect();var grant=await channel.OfferAsync(Guid.NewGuid(),default);await Fixture.Submit(channel,grant);await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.False(f.Running!.IsCompleted);Assert.True(f.Reception.CleanupPending);Assert.Contains(f.Borrowed.ToArray(),b=>b!=0);release.SetResult();Assert.True(await f.Running.WaitAsync(TimeSpan.FromSeconds(5)));Assert.True(f.Borrowed.Span.IndexOfAnyExcept((byte)0)<0);Assert.False(f.Reception.CleanupPending);
        }
        finally{release.TrySetResult();registration.Dispose();}
    }
    [Fact]public async Task FailedPipeDisposalStillJoinsTheOriginalPendingReadBeforeAnySessionCompletionNotification()
    {
        await using var f=new Fixture();f.Stream.HoldCleanup=f.Stream.ThrowOnDispose=true;await using var channel=await f.Connect();var grant=await channel.OfferAsync(Guid.NewGuid(),default);await Fixture.Submit(channel,grant);await f.Stream.Pending.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try{Assert.False(f.Running!.IsCompleted);Assert.True(f.Reception.Busy);Assert.Equal(0,f.Prepares);}
        finally
        {
            f.Stream.Release.TrySetResult();var failure=await Assert.ThrowsAnyAsync<Exception>(()=>f.Running!);if(failure is AggregateException aggregate)Assert.Contains(f.Stream.DisposeError,aggregate.Flatten().InnerExceptions);else Assert.Same(f.Stream.DisposeError,failure);f.Running=null;
        }
        Assert.False(f.Reception.Busy);Assert.Null(f.Reception.Inbox.Current);Assert.Equal(0,f.Prepares);
    }
    [Fact]public async Task ThrowingCancellationCannotSkipTheOriginalUncooperativeDecisionOrLoseItsException()
    {
        await using var f=new Fixture();f.Prompt.Hold=f.Prompt.CancellationIgnored=true;var invoked=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var original=new IOException("SYNTHETIC CANCELLATION");CancellationTokenRegistration registration=default;
        f.Prompt.OnToken=token=>registration=token.Register(()=>{invoked.TrySetResult();throw original;});
        try
        {
            await using var channel=await f.Connect();var offer=channel.OfferAsync(Guid.NewGuid(),default).AsTask();await f.Prompt.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));f.Client.Dispose();await Assert.ThrowsAnyAsync<Exception>(()=>offer);await invoked.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.False(f.Running!.IsCompleted);Assert.False(f.Prompt.Returned);f.Prompt.Release.SetResult();Assert.False(await f.Running.WaitAsync(TimeSpan.FromSeconds(5)));Assert.True(f.Prompt.Returned);var failure=Assert.IsType<AggregateException>(f.Session.PrimaryFailure);Assert.Contains(original,failure.Flatten().InnerExceptions);Assert.Equal(0,f.Prepares);Assert.False(f.Reception.Busy);
        }
        finally{f.Prompt.Release.TrySetResult();registration.Dispose();}
    }
    [Fact]public async Task StateProofReentryCannotInstallOrReviveAnOldConfirmation()
    {
        using var stop=new CancellationTokenSource();BrowserImageReception? reception=null;int prompts=0;var decision=new Decision();
        reception=new(new Immediate(),()=>{reception!.Revoke();return new(1,Activation.Off,"synthetic",0,0,true);},()=>true,decision,(_,_)=>{prompts++;return Task.CompletedTask;});using(reception)
        {Assert.False(await reception.ConfirmAsync(new(Guid.NewGuid(),Guid.NewGuid(),Origin,123),stop.Token));Assert.False(reception.Busy);Assert.Equal(0,decision.Calls);Assert.Equal(0,prompts);}
    }
}
