extern alias ImageProduction;
extern alias WorkerProduction;
using System.Buffers.Binary;
using System.Security.Cryptography;
using KuaiYi.Contracts;
using Xunit;
using P=ImageProduction::KuaiYi.Imaging;
using Session=WorkerProduction::KuaiYi.ImageWorker.ImageWorkerSession;
namespace KuaiYi.Provider.Tests;

public sealed class OcrCapabilitiesProtocolTests
{
    private static P.OcrDiagnosticJob Job()=>new(Guid.NewGuid(),1,1);
    private static TaskCompletionSource<T> Signal<T>()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static async Task<byte[]> Frames(params IpcFrame[] frames){using var stream=new MemoryStream();foreach(var frame in frames)await IpcFrameCodec.WriteAsync(stream,frame);return stream.ToArray();}
    private abstract class ReadOnlyStream:Stream
    {
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override void Flush()=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long l)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
    }
    private sealed class TailInput(byte[] prefix):ReadOnlyStream
    {
        private readonly MemoryStream initial=new(prefix);private MemoryStream? tail;public readonly TaskCompletionSource<byte[]> Release=Signal<byte[]>();
        public Memory<byte> RequestPayload{get;private set;}public Memory<byte> ControlPayload{get;private set;}
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellation=default)
        {
            if(initial.Position<initial.Length){if(buffer.Length>32)RequestPayload=buffer;return initial.Read(buffer.Span);}
            tail??=new(await Release.Task);if(buffer.Length is >0 and <27&&tail.Position>=28)ControlPayload=buffer;return tail.Read(buffer.Span);
        }
    }
    private sealed class Sink:MemoryStream
    {
        public readonly TaskCompletionSource<bool> Replied=Signal<bool>();public bool FailReplyFlush;private int flushes,writes;
        public ReadOnlyMemory<byte> ResponsePayload{get;private set;}
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellation=default)
        {if(++writes==4)ResponsePayload=buffer;return base.WriteAsync(buffer,cancellation);}
        public override Task FlushAsync(CancellationToken cancellation)
        {if(++flushes==2){Replied.TrySetResult(true);if(FailReplyFlush)throw new IOException("synthetic diagnostic flush failure");}return Task.CompletedTask;}
    }
    [Fact] public async Task ActualWorkerSessionReturnsBoundedCapabilitiesWithoutRecognizingAnyImage()
    {
        Guid id=Guid.NewGuid();var job=Job();using var input=new TailInput(await Frames(new(IpcMessageType.ImageHello,id,new byte[32]),new(IpcMessageType.ImageDiagnose,id,P.ImageJson.Serialize(job))));using var output=new Sink();int diagnostics=0,recognitions=0;
        var run=Session.RunAsync(input,output,(_,_,_)=>{recognitions++;throw new InvalidOperationException("must not recognize");},diagnoseOcr:()=>{diagnostics++;return OcrCapabilitiesDiscoveryTests.Good();});
        try
        {
            await output.Replied.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.False(run.IsCompleted);using var bytes=new MemoryStream(output.ToArray());Assert.Equal(IpcMessageType.ImageHello,(await IpcFrameCodec.ReadAsync(bytes))!.Type);
            var frame=(await IpcFrameCodec.ReadAsync(bytes))!;Assert.Equal(IpcMessageType.ImageDiagnostic,frame.Type);Assert.Equal(id,frame.RequestId);var reply=P.ImageJson.Read<P.OcrDiagnosticReply>(frame.Payload);Assert.Equal(job,reply.Job);Assert.Equal(new[]{"en-US","zh-Hans"},reply.Diagnostic.Languages);Assert.True(frame.Payload.Length<=IpcFrameCodec.JsonLimit);Assert.Equal(bytes.Length,bytes.Position);Assert.Equal(1,diagnostics);Assert.Equal(0,recognitions);
        }
        finally{input.Release.TrySetResult([]);await run.WaitAsync(TimeSpan.FromSeconds(3));}
        Assert.True(input.RequestPayload.Span.IndexOfAnyExcept((byte)0)<0);Assert.True(output.ResponsePayload.Span.IndexOfAnyExcept((byte)0)<0);
    }
    [Theory][InlineData("wrong-role")][InlineData("wrong-id")][InlineData("nonempty-cancel")][InlineData("cancel")][InlineData("eof")]
    public async Task DiagnosticControlCannotEndSessionUntilTheOriginalNativeOperationSettles(string fault)
    {
        Guid id=Guid.NewGuid();using var input=new TailInput(await Frames(new(IpcMessageType.ImageHello,id,new byte[32]),new(IpcMessageType.ImageDiagnose,id,P.ImageJson.Serialize(Job()))));using var output=new Sink();var entered=Signal<bool>();using var release=new ManualResetEventSlim();
        var run=Session.RunAsync(input,output,(_,_,_)=>throw new InvalidOperationException("no image"),diagnoseOcr:()=>{entered.TrySetResult(true);if(!release.Wait(TimeSpan.FromSeconds(8)))throw new TimeoutException("synthetic diagnostic barrier");return OcrCapabilitiesDiscoveryTests.Good();});
        Exception? error;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));input.Release.TrySetResult(fault=="eof"?[]:await Frames(new IpcFrame(fault=="wrong-role"?IpcMessageType.ShowSettings:IpcMessageType.ImageCancel,fault=="wrong-id"?Guid.NewGuid():id,fault is "nonempty-cancel" or "wrong-role"?[7,8,9]:[])));
            Assert.NotSame(run,await Task.WhenAny(run,Task.Delay(150)));Assert.Equal(60,output.Length);
        }
        finally{release.Set();error=await Record.ExceptionAsync(()=>run.WaitAsync(TimeSpan.FromSeconds(3)));}
        if(fault is "cancel" or "eof")
        {Assert.Null(error);using var bytes=new MemoryStream(output.ToArray());await IpcFrameCodec.ReadAsync(bytes);var frame=(await IpcFrameCodec.ReadAsync(bytes))!;Assert.Equal(P.ImageFailure.Cancelled,P.ImageJson.Read<P.OcrDiagnosticReply>(frame.Payload).Diagnostic.Failure);}
        else Assert.IsType<InvalidDataException>(error);
        Assert.True(input.RequestPayload.Span.IndexOfAnyExcept((byte)0)<0);Assert.True(input.ControlPayload.Span.IndexOfAnyExcept((byte)0)<0);
    }
    [Fact] public async Task DiagnosticReplyFlushFailureStillJoinsAndClearsTheOriginalControlRead()
    {
        Guid id=Guid.NewGuid();using var input=new TailInput(await Frames(new(IpcMessageType.ImageHello,id,new byte[32]),new(IpcMessageType.ImageDiagnose,id,P.ImageJson.Serialize(Job()))));using var output=new Sink{FailReplyFlush=true};
        var run=Session.RunAsync(input,output,(_,_,_)=>throw new InvalidOperationException("no image"),diagnoseOcr:()=>OcrCapabilitiesDiscoveryTests.Good());
        try{await output.Replied.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.NotSame(run,await Task.WhenAny(run,Task.Delay(150)));}
        finally{input.Release.TrySetResult([]);Assert.IsType<IOException>(await Record.ExceptionAsync(()=>run.WaitAsync(TimeSpan.FromSeconds(3))));}
        Assert.True(output.ResponsePayload.Span.IndexOfAnyExcept((byte)0)<0);Assert.True(input.RequestPayload.Span.IndexOfAnyExcept((byte)0)<0);
    }
    [Theory][InlineData(IpcMessageType.ImageDiagnose)][InlineData(IpcMessageType.ImageDiagnostic)]
    public async Task DiagnosticRolesUseJsonLimitAndRejectHugeUnsignedLengthsBeforePayloadRead(IpcMessageType type)
    {
        using var output=new MemoryStream();await Assert.ThrowsAsync<InvalidDataException>(()=>IpcFrameCodec.WriteAsync(output,new(type,Guid.NewGuid(),new byte[IpcFrameCodec.JsonLimit+1])).AsTask());Assert.Equal(0,output.Length);
        byte[] bytes=await Frames(new IpcFrame(type,Guid.NewGuid(),[]));BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8),uint.MaxValue);using var input=new MemoryStream(bytes);await Assert.ThrowsAsync<InvalidDataException>(()=>IpcFrameCodec.ReadAsync(input).AsTask());Assert.Equal(28,input.Position);
    }
    private class FramedInput(Stream source,Func<IpcFrame,IpcFrame>? transform=null):ReadOnlyStream
    {
        private byte[] current=[];private int position;public readonly List<IpcMessageType> Types=[];
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellation=default)
        {
            if(position==current.Length)
            {
                var frame=await IpcFrameCodec.ReadAsync(source,cancellation);if(frame==null)return 0;Types.Add(frame.Type);IpcFrame modified=frame;
                try{modified=transform?.Invoke(frame)??frame;current=await Frames(modified);position=0;}
                finally{CryptographicOperations.ZeroMemory(frame.Payload);if(!ReferenceEquals(frame.Payload,modified.Payload))CryptographicOperations.ZeroMemory(modified.Payload);}
            }
            int count=Math.Min(buffer.Length,current.Length-position);current.AsMemory(position,count).CopyTo(buffer);position+=count;
            if(position==current.Length)CryptographicOperations.ZeroMemory(current);return count;
        }
    }
    private sealed class HeldPayload(Stream source):FramedInput(source)
    {
        private int frames;public readonly TaskCompletionSource<bool> Entered=Signal<bool>(),Release=Signal<bool>();public Memory<byte> Borrowed{get;private set;}
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellation=default)
        {if(buffer.Length==1)frames++;int count=await base.ReadAsync(buffer,CancellationToken.None);if(frames==2&&buffer.Length>27){Borrowed=buffer;Entered.TrySetResult(true);await Release.Task;}return count;}
    }
    private static P.ImageWorkerClient Client(OcrCapabilitiesDiscoveryTests.Starter starter)=>new(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",OcrCapabilitiesDiscoveryTests.WorkerAssembly(),starter);
    private sealed class SentFrames(Stream destination):MemoryStream
    {
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellation=default)
        {if(Length+buffer.Length>4096)throw new InvalidDataException("Diagnostic test expected small control frames only.");await base.WriteAsync(buffer,cancellation);await destination.WriteAsync(buffer,cancellation);}
        public override Task FlushAsync(CancellationToken cancellation)=>destination.FlushAsync(cancellation);
    }
    [Fact] public async Task RealLinuxPrivateDiagnosticIsOneShotAndDoesNotSendImageFrames()
    {
        var starter=new OcrCapabilitiesDiscoveryTests.Starter();FramedInput? seen=null;SentFrames? sent=null;await using var client=new P.ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",OcrCapabilitiesDiscoveryTests.WorkerAssembly(),starter){ReadStreamForTest=s=>seen=new(s),WriteStreamForTest=s=>sent=new(s)};
        var job=Job();var reply=await client.DiagnoseAsync(job);Assert.Equal(job,reply.Job);Assert.Equal(P.ImageFailure.UnsupportedPlatform,reply.Diagnostic.Failure);Assert.Empty(reply.Diagnostic.Languages);Assert.Equal((uint)0,reply.Diagnostic.MaxImageDimension);Assert.Equal(new[]{IpcMessageType.ImageHello,IpcMessageType.ImageDiagnostic},seen!.Types);Assert.True(starter.Child!.HasExited);
        byte[] wire=sent!.ToArray();try{using var input=new MemoryStream(wire);var hello=(await IpcFrameCodec.ReadAsync(input))!;var diagnostic=(await IpcFrameCodec.ReadAsync(input))!;Assert.Equal(IpcMessageType.ImageHello,hello.Type);Assert.Equal(32,hello.Payload.Length);Assert.Equal(IpcMessageType.ImageDiagnose,diagnostic.Type);Assert.Equal(hello.RequestId,diagnostic.RequestId);Assert.Equal(job,P.ImageJson.Read<P.OcrDiagnosticJob>(diagnostic.Payload));Assert.Equal(input.Length,input.Position);}finally{CryptographicOperations.ZeroMemory(wire);sent.Dispose();}
        await Assert.ThrowsAsync<InvalidOperationException>(()=>client.DiagnoseAsync(Job()));await Assert.ThrowsAsync<InvalidOperationException>(()=>client.RunAsync(Array.Empty<byte>(),new(Guid.NewGuid(),1,1,"en-US",0)));
    }
    [Theory][InlineData("type")][InlineData("id")][InlineData("job")][InlineData("fields")][InlineData("capabilities")]
    public async Task RealClientRejectsWrongDiagnosticRoleIdentityJobOrUntrustedPayload(string fault)
    {
        var starter=new OcrCapabilitiesDiscoveryTests.Starter();await using var client=new P.ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",OcrCapabilitiesDiscoveryTests.WorkerAssembly(),starter){ReadStreamForTest=s=>new FramedInput(s,frame=>
        {
            if(frame.Type!=IpcMessageType.ImageDiagnostic)return frame;
            if(fault=="type")return frame with{Type=IpcMessageType.ImageCancel};if(fault=="id")return frame with{RequestId=Guid.NewGuid()};
            var reply=P.ImageJson.Read<P.OcrDiagnosticReply>(frame.Payload);
            if(fault=="job")return frame with{Payload=P.ImageJson.Serialize(reply with{Job=reply.Job with{Epoch=reply.Job.Epoch+1}})};
            if(fault=="capabilities")return frame with{Payload=P.ImageJson.Serialize(reply with{Diagnostic=reply.Diagnostic with{Languages=["en-US"]}})};
            return frame with{Payload=P.ImageJson.Serialize(new{job=reply.Job,diagnostic=reply.Diagnostic,unexpected=true})};
        })};
        var error=await Record.ExceptionAsync(()=>client.DiagnoseAsync(Job()));Assert.True(error is InvalidDataException or System.Text.Json.JsonException,error?.GetType().Name);Assert.True(starter.Child!.HasExited);Assert.True(await client.TryFinalizeAsync());
    }
    [Fact] public async Task LateNoncooperativeDiagnosticReadRetainsOwnerUntilItsActualPayloadIsCleared()
    {
        var starter=new OcrCapabilitiesDiscoveryTests.Starter();HeldPayload? held=null;var client=new P.ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",OcrCapabilitiesDiscoveryTests.WorkerAssembly(),starter){ReadStreamForTest=s=>held=new(s)};using var cancel=new CancellationTokenSource();var run=client.DiagnoseAsync(Job(),cancel.Token);
        try
        {
            using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(held==null)await Task.Delay(5,limit.Token);await held.Entered.Task.WaitAsync(limit.Token);cancel.Cancel();
            Assert.Equal(P.ImageFailure.SettlementUnconfirmed,(await Assert.ThrowsAsync<P.ImageJobException>(()=>run.WaitAsync(TimeSpan.FromSeconds(3)))).Code);Assert.True(starter.Child!.HasExited);Assert.False(await client.TryFinalizeAsync());Assert.True(held.Borrowed.Span.IndexOfAnyExcept((byte)0)>=0);
        }
        finally
        {
            held?.Release.TrySetResult(true);cancel.Cancel();try{await run;}catch(Exception error){Assert.IsType<P.ImageJobException>(error);}
            for(int i=0;i<300&&!await client.TryFinalizeAsync();i++)await Task.Delay(10);Assert.True(await client.TryFinalizeAsync());await client.DisposeAsync();if(held!=null)Assert.True(held.Borrowed.Span.IndexOfAnyExcept((byte)0)<0);
        }
    }
    private sealed class DelayedWrite(Stream destination,bool flush):MemoryStream
    {
        private int calls,flushes;public readonly TaskCompletionSource<bool> Entered=Signal<bool>(),Release=Signal<bool>();public ReadOnlyMemory<byte> Borrowed{get;private set;}
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> value,CancellationToken cancellation=default)
        {if(++calls==4){Borrowed=value;if(!flush){Entered.TrySetResult(true);await Release.Task;}}await destination.WriteAsync(value,cancellation);}
        public override async Task FlushAsync(CancellationToken cancellation)
        {if(++flushes==2&&flush){Entered.TrySetResult(true);await Release.Task;}await destination.FlushAsync(cancellation);}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task DiagnosticOriginalWriteAndFlushKeepTheirPayloadLeaseAcrossCancellation(bool flush)
    {
        var starter=new OcrCapabilitiesDiscoveryTests.Starter();DelayedWrite? held=null;var client=new P.ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",OcrCapabilitiesDiscoveryTests.WorkerAssembly(),starter){WriteStreamForTest=s=>held=new(s,flush)};using var cancel=new CancellationTokenSource();var run=client.DiagnoseAsync(Job(),cancel.Token);
        try
        {
            using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(held==null)await Task.Delay(5,limit.Token);await held.Entered.Task.WaitAsync(limit.Token);cancel.Cancel();
            Assert.Equal(P.ImageFailure.SettlementUnconfirmed,(await Assert.ThrowsAsync<P.ImageJobException>(()=>run.WaitAsync(TimeSpan.FromSeconds(3)))).Code);Assert.True(starter.Child!.HasExited);Assert.False(await client.TryFinalizeAsync());Assert.True(held.Borrowed.Span.IndexOfAnyExcept((byte)0)>=0);
        }
        finally
        {
            held?.Release.TrySetResult(true);cancel.Cancel();try{await run;}catch(Exception error){Assert.IsType<P.ImageJobException>(error);}
            for(int i=0;i<300&&!await client.TryFinalizeAsync();i++)await Task.Delay(10);Assert.True(await client.TryFinalizeAsync());await client.DisposeAsync();if(held!=null)Assert.True(held.Borrowed.Span.IndexOfAnyExcept((byte)0)<0);
        }
    }
}
