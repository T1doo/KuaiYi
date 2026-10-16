extern alias ImageProduction;
extern alias WorkerProduction;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using KuaiYi.Contracts;
using KuaiYi.Core;
using Xunit;
using P=ImageProduction::KuaiYi.Imaging;
using Session=WorkerProduction::KuaiYi.ImageWorker.ImageWorkerSession;
namespace KuaiYi.Provider.Tests;

public sealed class ImageWorkerTransferTests
{
    private static TaskCompletionSource<T> Signal<T>()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static P.ImageJob Job(int bytes)=>new(Guid.NewGuid(),1,1,"en-US",bytes);
    private static string WorkerAssembly()
    {
        string configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        string path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../src/KuaiYi.ImageWorker/bin",configuration,"net10.0/win-x64/KuaiYi.ImageWorker.dll"));
        Assert.Equal(SHA256.HashData(File.ReadAllBytes(typeof(Session).Assembly.Location)),SHA256.HashData(File.ReadAllBytes(path)));
        return path;
    }
    private static byte[] Fixture()=>File.ReadAllBytes(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tests/fixtures/images/kuaiyi-test.png")));
    private static IpcFrame Begin(Guid id,P.ImageJob job)=>new(IpcMessageType.ImageBegin,id,P.ImageJson.Serialize(job));
    private static IpcFrame Chunk(Guid id,byte[] data,int index=0)
    {byte[] p=new byte[data.Length+4];BinaryPrimitives.WriteInt32LittleEndian(p,index);data.CopyTo(p,4);return new(IpcMessageType.BinaryChunk,id,p);}
    private static async Task<byte[]> Frames(params IpcFrame[] frames)
    {using var stream=new MemoryStream();foreach(var frame in frames)await IpcFrameCodec.WriteAsync(stream,frame);return stream.ToArray();}
    [Theory][InlineData("dispose")][InlineData("bad-begin")][InlineData("bad-chunk")][InlineData("short-commit")]
    public void TerminalReceiverCannotBeResurrectedByNewBegin(string cause)
    {
        using var receiver=new P.ImageJobReceiver();Guid id=Guid.NewGuid();
        if(cause=="dispose")receiver.Dispose();
        else if(cause=="bad-begin")Assert.Throws<InvalidDataException>(()=>receiver.Begin(Begin(id,Job(0))));
        else
        {
            receiver.Begin(Begin(id,Job(8)));receiver.Chunk(Chunk(id,[1,2,3,4]));
            if(cause=="bad-chunk")Assert.Throws<InvalidDataException>(()=>receiver.Chunk(Chunk(id,[5],2)));
            else Assert.Throws<InvalidDataException>(()=>receiver.Commit(new(IpcMessageType.ImageCommit,id,[])));
        }
        Assert.Throws<InvalidDataException>(()=>receiver.Begin(Begin(Guid.NewGuid(),Job(4))));
        Assert.Null(receiver.Job);Assert.Equal(Guid.Empty,receiver.RequestId);
    }
    private sealed class ScriptedInput(byte[] initial):Stream
    {
        private readonly MemoryStream prefix=new(initial);private MemoryStream? tail;
        internal Memory<byte> TailPayload {get;private set;}
        internal readonly TaskCompletionSource<byte[]> Release=Signal<byte[]>();
        internal Exception? ReadError;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellation=default)
        {
            if(prefix.Position<prefix.Length)return prefix.Read(buffer.Span);
            tail??=new(await Release.Task);if(ReadError!=null)throw ReadError;if(buffer.Length is not (1 or 27))TailPayload=buffer;return tail.Read(buffer.Span);
        }
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override void Flush()=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long l)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
    }
    private sealed class FailingEcho:MemoryStream
    {
        internal ReadOnlyMemory<byte> Echo {get;private set;}private int calls;
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> value,CancellationToken token=default)
        {if(++calls==2){Echo=value;throw new IOException("synthetic echo failure");}return base.WriteAsync(value,token);}
    }
    [Fact] public async Task HelloEchoFailureClearsTheActualNonceBuffer()
    {
        using var input=new MemoryStream(await Frames(new IpcFrame(IpcMessageType.ImageHello,Guid.NewGuid(),Enumerable.Repeat((byte)77,32).ToArray())));using var output=new FailingEcho();
        await Assert.ThrowsAsync<IOException>(()=>Session.RunAsync(input,output,(_,_,_)=>throw new InvalidOperationException("must not execute")));
        Assert.Equal(32,output.Echo.Length);Assert.True(output.Echo.Span.IndexOfAnyExcept((byte)0)<0);
    }
    [Theory][InlineData("wrong-role")][InlineData("read-error")][InlineData("wrong-id")][InlineData("nonempty-cancel")][InlineData("callback-throws")]
    public async Task InvalidControlCannotDisposeTheImageWhileOriginalOperationStillOwnsIt(string fault)
    {
        Guid id=Guid.NewGuid();byte[] pixels=[1,2,3,4,5,6,7,8];var entered=Signal<ReadOnlyMemory<byte>>();var cancelled=Signal<bool>();using var release=new ManualResetEventSlim();
        var begin=await Frames(new(IpcMessageType.ImageHello,id,new byte[32]),Begin(id,Job(pixels.Length)),Chunk(id,pixels),new(IpcMessageType.ImageCommit,id,[]));
        using var input=new ScriptedInput(begin);using var output=new MemoryStream();
        var run=Session.RunAsync(input,output,(image,_,token)=>
        {using var registration=token.Register(()=>{cancelled.TrySetResult(true);if(fault=="callback-throws")throw new InvalidOperationException("synthetic callback failure");});entered.TrySetResult(image);if(!release.Wait(TimeSpan.FromSeconds(8)))throw new TimeoutException("synthetic operation barrier");return new(1,1,"en-US","synthetic",[]);});
        try
        {
            var borrowed=await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            if(fault=="read-error")input.ReadError=new IOException("synthetic read fault");
            input.Release.SetResult(fault=="read-error"?[]:await Frames(new IpcFrame(fault=="wrong-role"?IpcMessageType.ShowSettings:IpcMessageType.ImageCancel,fault=="wrong-id"?Guid.NewGuid():id,fault=="nonempty-cancel"?[99]:[])));
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.NotSame(run,await Task.WhenAny(run,Task.Delay(150)));
            Assert.Equal(pixels,borrowed.ToArray());Assert.Equal(60,output.Length); // Only hello; no reply before settlement.
        }
        finally
        {
            release.Set();var failure=await Record.ExceptionAsync(()=>run.WaitAsync(TimeSpan.FromSeconds(3)));Assert.True(failure is IOException or InvalidDataException or AggregateException,failure?.GetType().Name);
            if(entered.Task.IsCompletedSuccessfully)Assert.True((await entered.Task).Span.IndexOfAnyExcept((byte)0)<0);
        }
    }
    private sealed class Starter:P.IImageProcessStarter
    {
        internal Process? Child;
        public Process Start(ProcessStartInfo info){info.Environment["DOTNET_GENERATE_ASPNET_CERTIFICATE"]="false";return Child=Process.Start(info)!;}
    }
    private sealed class HeldRead(Stream source,int target):Stream
    {
        private int frame;internal readonly TaskCompletionSource<bool> Entered=Signal<bool>(),Release=Signal<bool>();internal Memory<byte> Borrowed {get;private set;}
        internal Exception? LateError {get;set;}
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)
        {if(buffer.Length==1)frame++;int count=await source.ReadAsync(buffer,CancellationToken.None);if(frame==target&&buffer.Length>27){Borrowed=buffer;Entered.TrySetResult(true);await Release.Task;if(LateError!=null)throw LateError;}return count;}
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override void Flush()=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long l)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
    }
    [Theory][InlineData(1)][InlineData(2)]
    public async Task RealWorkerReadCancellationStopsTheOwnedChildAndRetainsLateReadLease(int phase)
        =>await ReadCancellation(phase,false);
    [Fact] public async Task LateReadArbitraryFaultIsObservedAndItsActualBufferCleared()=>await ReadCancellation(2,true);
    private static async Task ReadCancellation(int phase,bool lateFault)
    {
        string assembly=WorkerAssembly();byte[] png=Fixture();
        var starter=new Starter();HeldRead? held=null;var client=new P.ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",assembly,starter){ReadStreamForTest=s=>held=new(s,phase)};
        using var cancel=new CancellationTokenSource();var run=client.RunAsync(png,Job(png.Length),cancel.Token);
        try
        {
            while(held==null){await Task.WhenAny(run,Task.Delay(10));Assert.False(run.IsCompleted);}
            await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));cancel.Cancel();
            if(lateFault)held.LateError=new NotSupportedException("synthetic late read fault");
            var failure=await Record.ExceptionAsync(()=>run.WaitAsync(TimeSpan.FromSeconds(2)));Assert.NotNull(failure);Assert.IsNotType<TimeoutException>(failure);
            Assert.Equal(P.ImageFailure.SettlementUnconfirmed,Assert.IsType<P.ImageJobException>(failure).Code);
            Assert.True(starter.Child!.HasExited);Assert.False(await client.TryFinalizeAsync());
            Assert.Equal(P.ImageFailure.SettlementUnconfirmed,(await Assert.ThrowsAsync<P.ImageJobException>(()=>client.DisposeAsync().AsTask())).Code);
            Assert.True(held.Borrowed.Span.IndexOfAnyExcept((byte)0)>=0);
        }
        finally
        {
            held?.Release.TrySetResult(true);cancel.Cancel();try{await run.WaitAsync(TimeSpan.FromSeconds(3));}catch(Exception error){Assert.True(error is OperationCanceledException or P.ImageJobException or IOException,error.GetType().Name);}
            for(int i=0;i<300&&!await client.TryFinalizeAsync();i++)await Task.Delay(10);
            Assert.True(await client.TryFinalizeAsync());await client.DisposeAsync();
            if(held!=null)Assert.True(held.Borrowed.Span.IndexOfAnyExcept((byte)0)<0);
            if(lateFault)Assert.Equal(nameof(NotSupportedException),client.IoFailureCode);
        }
    }
    private sealed class HeldWrite(Stream destination,int target):MemoryStream
    {
        private int calls;internal readonly TaskCompletionSource<bool> Entered=Signal<bool>(),Release=Signal<bool>();internal ReadOnlyMemory<byte> Borrowed {get;private set;}
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> value,CancellationToken cancellation=default)
        {if(target==0)Borrowed=value;if(++calls==target*2){Borrowed=value;Entered.TrySetResult(true);await Release.Task;}await destination.WriteAsync(value,cancellation);}
        public override async Task FlushAsync(CancellationToken cancellation)
        {if(target==0){Entered.TrySetResult(true);await Release.Task;}await destination.FlushAsync(cancellation);}
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public async Task OwnedWriteBufferRemainsLiveUntilOriginalWriteSettlesAndClientCannotBeReused(int phase)
    {
        string assembly=WorkerAssembly();byte[] png=Fixture();
        var starter=new Starter();HeldWrite? held=null;var client=new P.ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",assembly,starter){WriteStreamForTest=s=>held=new(s,phase)};
        using var cancel=new CancellationTokenSource();var run=client.RunAsync(png,Job(png.Length),cancel.Token);
        try
        {
            while(held==null){await Task.WhenAny(run,Task.Delay(10));Assert.False(run.IsCompleted);}
            await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));byte[] before=held.Borrowed.ToArray();cancel.Cancel();
            var error=await Assert.ThrowsAsync<P.ImageJobException>(()=>run.WaitAsync(TimeSpan.FromSeconds(2)));Assert.Equal(P.ImageFailure.SettlementUnconfirmed,error.Code);
            Assert.True(starter.Child!.HasExited);Assert.True(client.ObserveExit());Assert.False(await client.TryFinalizeAsync());Assert.Equal(before,held.Borrowed.ToArray());
            await Assert.ThrowsAsync<InvalidOperationException>(()=>client.RunAsync(png,Job(png.Length)));
        }
        finally
        {
            held?.Release.TrySetResult(true);cancel.Cancel();try{await run.WaitAsync(TimeSpan.FromSeconds(3));}catch(P.ImageJobException){}
            for(int i=0;i<300&&!await client.TryFinalizeAsync();i++)await Task.Delay(10);
            Assert.True(await client.TryFinalizeAsync());await client.DisposeAsync();if(held!=null)Assert.True(held.Borrowed.Span.IndexOfAnyExcept((byte)0)<0);
        }
    }
    [Theory][InlineData("repeat-commit")][InlineData("late-chunk")][InlineData("wrong-id")][InlineData("nonempty-commit")]
    public void AbortingCommittedTransferClearsTheActualBorrowedImageAndClosesIdentity(string fault)
    {
        using var receiver=new P.ImageJobReceiver();Guid id=Guid.NewGuid();receiver.Begin(Begin(id,Job(4)));receiver.Chunk(Chunk(id,[1,2,3,4]));
        var borrowed=receiver.Commit(new(IpcMessageType.ImageCommit,id,[]));
        if(fault=="late-chunk")Assert.Throws<InvalidDataException>(()=>receiver.Chunk(Chunk(id,[5],1)));
        else Assert.Throws<InvalidDataException>(()=>receiver.Commit(new(IpcMessageType.ImageCommit,fault=="wrong-id"?Guid.NewGuid():id,fault=="nonempty-commit"?[1]:[])));
        Assert.True(borrowed.Span.IndexOfAnyExcept((byte)0)<0);Assert.Null(receiver.Job);Assert.Equal(Guid.Empty,receiver.RequestId);
        Assert.Throws<InvalidDataException>(()=>receiver.Begin(Begin(Guid.NewGuid(),Job(4))));
    }
    private sealed class FailingReply:MemoryStream
    {
        private int calls;internal readonly TaskCompletionSource<bool> Entered=Signal<bool>();internal ReadOnlyMemory<byte> Response {get;private set;}
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> value,CancellationToken cancellation=default)
        {if(++calls==4){Response=value;Entered.TrySetResult(true);throw new IOException("synthetic reply write failure");}return base.WriteAsync(value,cancellation);}
    }
    [Fact] public async Task ReplyWriteFailureStillWaitsForAndClearsTheOriginalLateControlRead()
    {
        Guid id=Guid.NewGuid();var prefix=await Frames(new(IpcMessageType.ImageHello,id,new byte[32]),Begin(id,Job(4)),Chunk(id,[1,2,3,4]),new(IpcMessageType.ImageCommit,id,[]));
        using var input=new ScriptedInput(prefix);using var output=new FailingReply();ReadOnlyMemory<byte> borrowed=default;var diagnostics=new List<string>();
        var run=Session.RunAsync(input,output,(image,_,_)=>{borrowed=image;return new(1,1,"en-US","synthetic",[]);},diagnostics.Add);
        try
        {
            await output.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.NotSame(run,await Task.WhenAny(run,Task.Delay(100)));
            Assert.Equal(new byte[]{1,2,3,4},borrowed.ToArray());Assert.True(output.Response.Span.IndexOfAnyExcept((byte)0)<0);
        }
        finally
        {
            input.Release.TrySetResult(await Frames(new IpcFrame(IpcMessageType.ShowSettings,id,[7,8,9])));
            await Assert.ThrowsAsync<IOException>(()=>run.WaitAsync(TimeSpan.FromSeconds(3)));Assert.True(borrowed.Span.IndexOfAnyExcept((byte)0)<0);Assert.Contains("settlement:InvalidDataException",diagnostics);
            Assert.Equal(3,input.TailPayload.Length);Assert.True(input.TailPayload.Span.IndexOfAnyExcept((byte)0)<0);
        }
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ValidCancelOrEofYieldsCancelledOnlyAfterTheOperationSettles(bool eof)
    {
        Guid id=Guid.NewGuid();var prefix=await Frames(new(IpcMessageType.ImageHello,id,new byte[32]),Begin(id,Job(4)),Chunk(id,[1,2,3,4]),new(IpcMessageType.ImageCommit,id,[]));
        using var input=new ScriptedInput(prefix);using var output=new MemoryStream();var entered=Signal<bool>();var cancelled=Signal<bool>();using var release=new ManualResetEventSlim();
        var run=Session.RunAsync(input,output,(_,_,token)=>{using var registration=token.Register(()=>cancelled.TrySetResult(true));entered.TrySetResult(true);if(!release.Wait(TimeSpan.FromSeconds(8)))throw new TimeoutException();return new(1,1,"en-US","synthetic",[]);});
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));input.Release.TrySetResult(eof?[]:await Frames(new IpcFrame(IpcMessageType.ImageCancel,id,[])));await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.NotSame(run,await Task.WhenAny(run,Task.Delay(100)));Assert.Equal(60,output.Length);
        }
        finally{release.Set();await run.WaitAsync(TimeSpan.FromSeconds(3));}
        output.Position=0;Assert.NotNull(await IpcFrameCodec.ReadAsync(output));var frame=await IpcFrameCodec.ReadAsync(output);var reply=P.ImageJson.Read<P.ImageReply>(frame!.Payload);Assert.Equal(P.ImageFailure.Cancelled,reply.Failure);Assert.Null(reply.Output);
    }
    [Fact] public async Task ActualManagedWorkerCompletesTheOriginalHandshakeAndUnsupportedNativeContract()
    {
        var starter=new Starter();await using var client=new P.ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",WorkerAssembly(),starter);byte[] png=Fixture();var job=Job(png.Length);
        var reply=await client.RunAsync(png,job).WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(job,reply.Job);Assert.Null(reply.Output);
        Assert.Equal(OperatingSystem.IsWindows()?P.ImageFailure.IdentityMissing:P.ImageFailure.UnsupportedPlatform,reply.Failure);
        Assert.True(starter.Child!.HasExited);Assert.Equal(0,starter.Child.ExitCode);Assert.True(client.ObserveExit());Assert.True(await client.TryFinalizeAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>client.RunAsync(png,job));Assert.True(await client.TryFinalizeAsync());
    }
    [Fact] public async Task PrecancelledActualClientNeverStartsAChild()
    {
        var starter=new Starter();await using var client=new P.ImageWorkerClient("synthetic-missing-executable",processStarter:starter);using var stop=new CancellationTokenSource();stop.Cancel();
        byte[] png=Fixture();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>client.RunAsync(png,Job(png.Length),stop.Token));Assert.Null(starter.Child);Assert.True(await client.TryFinalizeAsync());
    }
    [Theory][InlineData("session")][InlineData("version")][InlineData("epoch")][InlineData("language")][InlineData("byte-length")][InlineData("wrong-request")][InlineData("sequence")][InlineData("overrun")][InlineData("short-commit")]
    public void ProtocolBoundsAndCompleteIdentityRemainRequiredAfterFailure(string fault)
    {
        using var receiver=new P.ImageJobReceiver();Guid id=Guid.NewGuid();var job=Job(4);
        if(fault is "session" or "version" or "epoch" or "language" or "byte-length")
        {
            job=fault switch{"session"=>job with{SessionId=Guid.Empty},"version"=>job with{Version=0},"epoch"=>job with{Epoch=0},"language"=>job with{Language="../invalid"},_=>job with{ByteLength=20*1024*1024+1}};
            Assert.Throws<InvalidDataException>(()=>receiver.Begin(Begin(id,job)));
        }
        else
        {
            receiver.Begin(Begin(id,job));
            if(fault=="short-commit")Assert.Throws<InvalidDataException>(()=>receiver.Commit(new(IpcMessageType.ImageCommit,id,[])));
            else Assert.Throws<InvalidDataException>(()=>receiver.Chunk(Chunk(fault=="wrong-request"?Guid.NewGuid():id,fault=="overrun"?[1,2,3,4,5]:[1],fault=="sequence"?1:0)));
        }
        Assert.Null(receiver.Job);Assert.Equal(Guid.Empty,receiver.RequestId);Assert.Throws<InvalidDataException>(()=>receiver.Begin(Begin(Guid.NewGuid(),Job(4))));
    }
    [Fact] public async Task ActualWireRejectsForgedUnsignedLengthBeforePayloadRead()
    {
        byte[] wire=await Frames(new IpcFrame(IpcMessageType.BinaryChunk,Guid.NewGuid(),[1,2,3,4]));BinaryPrimitives.WriteUInt32LittleEndian(wire.AsSpan(8),uint.MaxValue);
        using var stream=new MemoryStream(wire);await Assert.ThrowsAsync<InvalidDataException>(()=>IpcFrameCodec.ReadAsync(stream).AsTask());Assert.Equal(28,stream.Position);
    }
    private sealed class TruncatedPayload(byte[] bytes):MemoryStream(bytes)
    {
        internal Memory<byte> OwnedPayload {get;private set;}
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellation=default)
        {if(Position==28)OwnedPayload=buffer;return base.ReadAsync(buffer,cancellation);}
    }
    [Fact] public async Task ActualWireClearsAllocatedPayloadWhenThePeerTruncatesIt()
    {
        byte[] wire=await Frames(new IpcFrame(IpcMessageType.BinaryChunk,Guid.NewGuid(),[1,2,3,4]));using var stream=new TruncatedPayload(wire[..^1]);
        await Assert.ThrowsAsync<EndOfStreamException>(()=>IpcFrameCodec.ReadAsync(stream).AsTask());Assert.Equal(4,stream.OwnedPayload.Length);Assert.True(stream.OwnedPayload.Span.IndexOfAnyExcept((byte)0)<0);
    }
}
