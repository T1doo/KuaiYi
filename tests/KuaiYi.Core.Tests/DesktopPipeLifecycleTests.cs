using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text.Json;
using KuaiYi.Contracts;
using KuaiYi.Core;
using KuaiYi.Core.Installation;
using KuaiYi.Imaging;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed class DesktopPipeLifecycleTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private static readonly TimeSpan Limit=TimeSpan.FromSeconds(8);
    private static TaskCompletionSource Signal()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    [Theory][InlineData(false)][InlineData(true)]
    public async Task RetiredSessionAfterBegunOrHeldChunkCannotSendAnyFurtherChunkOrCommit(bool chunk)
    {
        string name="ky-host-guard-"+Guid.NewGuid().ToString("N");
        await using var server=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
        await using var client=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
        var entered=Signal();var release=Signal();int chunks=0,commits=0,current=1;
        var serve=Serve();await client.ConnectAsync(1000);
        using var stream=new HeldChunk(client,chunk,entered,release);
        var channel=new DesktopPipeChannel(stream,"chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/",requireCurrent:()=>{if(Volatile.Read(ref current)==0)throw new InstallationRecoveryException("PUBLIC_RETIRED_SESSION");});
        var grant=new BrowserGrant(Guid.NewGuid(),new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1),Guid.NewGuid());
        // Protocol bytes only; no decoder/Worker claim. Two chunks make the original IO boundary observable.
        byte[] bytes=Enumerable.Repeat((byte)0x42,ImageTransferAssembler.ChunkLimit+17).ToArray();using var assembler=new ImageTransferAssembler();
        assembler.Begin(grant.OperationId,bytes.Length,"image/png",Convert.ToHexString(SHA256.HashData(bytes)));
        assembler.Chunk(grant.OperationId,0,bytes.AsSpan(0,ImageTransferAssembler.ChunkLimit));assembler.Chunk(grant.OperationId,1,bytes.AsSpan(ImageTransferAssembler.ChunkLimit));using var image=assembler.Commit(grant.OperationId);
        Task original=channel.SubmitAsync(grant,image,new ImageSize(1,1),default).AsTask();Exception? failure=null;
        List<Exception> errors=[];
        try{await entered.Task.WaitAsync(Limit);Assert.False(original.IsCompleted);Interlocked.Exchange(ref current,0);release.TrySetResult();try{await original.WaitAsync(Limit);}catch(Exception error){failure=error;}}
        catch(Exception error){errors.Add(error);}
        finally
        {
            release.TrySetResult();
            try{await channel.DisposeAsync();}catch(Exception error){errors.Add(error);}
            try{await original.WaitAsync(Limit);}catch(Exception error){if(!ReferenceEquals(error,failure))errors.Add(error);}
            try{await serve.WaitAsync(Limit);}catch(Exception error){errors.Add(error);}
            CryptographicOperations.ZeroMemory(bytes);
        }
        if(errors.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if(errors.Count>1)throw new AggregateException(errors);
        output.WriteLine($"PUBLIC_SESSION_RETIRED barrier={(chunk?"originalChunkWrite":"BegunReply")} chunks={chunks} commits={commits} originalSubmit={original.Status}");
        Assert.IsType<InstallationRecoveryException>(failure);Assert.Equal(chunk?1:0,chunks);Assert.Equal(0,commits);Assert.True(original.IsCompleted);
        async Task Serve()
        {
            await server.WaitForConnectionAsync();
            while(await IpcFrameCodec.ReadAsync(server) is{} frame)
            {
                try
                {
                    if(frame.Type==IpcMessageType.BinaryChunk){chunks++;continue;}
                    using var doc=JsonDocument.Parse(frame.Payload);string? phase=doc.RootElement.GetProperty("phase").GetString();
                    if(phase=="BeginImage")
                    {
                        if(!chunk){entered.TrySetResult();await release.Task;}
                        await Reply(frame.RequestId,new{phase="Begun",operationId=doc.RootElement.GetProperty("grant").GetProperty("OperationId").GetGuid()});
                    }
                    else if(phase=="CommitImage"){commits++;await Reply(frame.RequestId,new{phase="Committed",operationId=doc.RootElement.GetProperty("grant").GetProperty("OperationId").GetGuid()});}
                    else throw new InvalidDataException("Unexpected protocol frame.");
                }
                finally{CryptographicOperations.ZeroMemory(frame.Payload);}
            }
        }
        async Task Reply<T>(Guid connection,T value)
        {byte[] payload=JsonSerializer.SerializeToUtf8Bytes(value);try{await IpcFrameCodec.WriteAsync(server,new(IpcMessageType.SubmitImage,connection,payload));await server.FlushAsync();}finally{CryptographicOperations.ZeroMemory(payload);}}
    }
    [Fact]public async Task RetiredSessionStillAllowsExactGrantCancellationCleanup()
    {
        using var wire=new MemoryStream();int calls=0;
        await using var channel=new DesktopPipeChannel(wire,"PUBLIC_SYNTHETIC",requireCurrent:()=>{calls++;throw new InstallationRecoveryException("PUBLIC_RETIRED");});
        var grant=new BrowserGrant(Guid.NewGuid(),new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1),Guid.NewGuid());
        await channel.CancelAsync(grant,default);Assert.Equal(0,calls);wire.Position=0;
        var frame=await IpcFrameCodec.ReadAsync(wire);Assert.NotNull(frame);
        using var doc=JsonDocument.Parse(frame.Payload);Assert.Equal("CancelImage",doc.RootElement.GetProperty("phase").GetString());
        Assert.Equal(grant,doc.RootElement.GetProperty("grant").Deserialize<BrowserGrant>());CryptographicOperations.ZeroMemory(frame.Payload);
    }
    private sealed class HeldChunk(Stream inner,bool hold,TaskCompletionSource entered,TaskCompletionSource release):Stream
    {
        private bool header=true,first=true,binary;
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes,CancellationToken cancellation=default)
        {
            if(header)binary=System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.Span.Slice(6))==(ushort)IpcMessageType.BinaryChunk;
            await inner.WriteAsync(bytes,cancellation);
            if(!header&&binary&&first&&hold){first=false;entered.TrySetResult();await release.Task;}
            header=!header;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> bytes,CancellationToken cancellation=default)=>inner.ReadAsync(bytes,cancellation);
        public override Task FlushAsync(CancellationToken cancellation)=>inner.FlushAsync(cancellation);
        protected override void Dispose(bool disposing){if(disposing)inner.Dispose();base.Dispose(disposing);}
        public override bool CanRead=>true;public override bool CanWrite=>true;public override bool CanSeek=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush()=>inner.Flush();public override int Read(byte[] b,int o,int n)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int n)=>throw new NotSupportedException();public override long Seek(long n,SeekOrigin o)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();
    }
}
