using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using KuaiYi.Contracts;
using KuaiYi.Core;
using KuaiYi.Imaging;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class ImageWorkerTests
{
    private static byte[] Fixture() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"kuaiyi-test.png"));
    private static ImageJob Job(int length) => new(Guid.NewGuid(),1,1,"en-US",length);
    private static IpcFrame Begin(Guid id, ImageJob job) => new(IpcMessageType.ImageBegin,id,ImageJson.Serialize(job));
    private static IpcFrame Chunk(Guid id, byte[] bytes, uint index = 0) { byte[] payload = new byte[bytes.Length + 4]; BinaryPrimitives.WriteUInt32LittleEndian(payload,index); bytes.CopyTo(payload,4); return new(IpcMessageType.BinaryChunk,id,payload); }
    [Fact] public void RealFixtureDimensions() => Assert.Equal(new ImageSize(640,160),ImagePolicy.InspectPng(Fixture()));
    [Theory] [InlineData(0,1,ImageFailure.InvalidImage)] [InlineData(1,0,ImageFailure.InvalidImage)] [InlineData(16385,1,ImageFailure.ImageTooLarge)] [InlineData(1,16385,ImageFailure.ImageTooLarge)] [InlineData(6000,4001,ImageFailure.ImageTooLarge)] [InlineData(uint.MaxValue,uint.MaxValue,ImageFailure.ImageTooLarge)]
    public void RejectDimensions(uint width,uint height,ImageFailure failure) => Assert.Equal(failure,Assert.Throws<ImageJobException>(()=>ImagePolicy.ValidateDimensions(width,height)).Code);
    [Fact] public void ExactPixelLimit() { ImagePolicy.ValidateDimensions(6000,4000); ImagePolicy.ValidateDimensions(16384,1); }
    [Theory] [InlineData(0)] [InlineData(8)] [InlineData(32)] public void TruncatedHeader(int size) => Assert.Equal(ImageFailure.InvalidImage,Assert.Throws<ImageJobException>(()=>ImagePolicy.InspectPng(Fixture().AsSpan(0,size))).Code);
    [Theory] [InlineData(0)] [InlineData(8)] [InlineData(12)] public void ForgedSignatureOrHeader(int offset) { var bytes=Fixture(); bytes[offset]^=1; Assert.Throws<ImageJobException>(()=>ImagePolicy.InspectPng(bytes)); }
    [Fact] public void InputByteLimitBeforeHeaderDecode() => Assert.Equal(ImageFailure.ImageTooLarge,Assert.Throws<ImageJobException>(()=>ImagePolicy.InspectPng(new byte[ImagePolicy.ByteLimit+1])).Code);
    [Fact] public void PrivateTransferHasExactIdLengthAndClearsMemory() {
        var data=Fixture(); var id=Guid.NewGuid(); using var receiver=new ImageJobReceiver(); receiver.Begin(Begin(id,Job(data.Length)));
        receiver.Chunk(Chunk(id,data)); var memory=receiver.Commit(new(IpcMessageType.ImageCommit,id,[])); Assert.Equal(data,memory.ToArray()); receiver.Dispose(); Assert.True(memory.Span.IndexOfAnyExcept((byte)0)<0);
    }
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(ImagePolicy.ByteLimit+1)] public void InvalidDeclaredLength(int length) { using var receiver=new ImageJobReceiver(); Assert.Throws<InvalidDataException>(()=>receiver.Begin(Begin(Guid.NewGuid(),Job(length)))); }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void InvalidPrivateJob(int kind) {
        var job=Job(100); job=kind switch {0=>job with {SessionId=Guid.Empty},1=>job with {Version=0},2=>job with {Epoch=0},3=>job with {Language="../en"},_=>job with {Language=new string('x',65)}};
        using var receiver=new ImageJobReceiver(); Assert.Throws<InvalidDataException>(()=>receiver.Begin(Begin(Guid.NewGuid(),job)));
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void AbortWrongChunkClearsPreviouslyWrittenData(int kind) {
        var id=Guid.NewGuid(); using var receiver=new ImageJobReceiver();receiver.Begin(Begin(id,Job(8)));receiver.Chunk(Chunk(id,new byte[]{1,2,3,4}));
        var frame=kind switch {0=>Chunk(Guid.NewGuid(),new byte[]{5},1),1=>Chunk(id,new byte[]{5},2),2=>Chunk(id,new byte[5],1),3=>new IpcFrame(IpcMessageType.SubmitImage,id,new byte[5]),_=>new IpcFrame(IpcMessageType.BinaryChunk,id,new byte[4])};
        Assert.Throws<InvalidDataException>(()=>receiver.Chunk(frame));Assert.Null(receiver.Job);
    }
    [Fact] public void IncompleteCommitRejected() { using var receiver=new ImageJobReceiver();var id=Guid.NewGuid();receiver.Begin(Begin(id,Job(8)));Assert.Throws<InvalidDataException>(()=>receiver.Commit(new(IpcMessageType.ImageCommit,id,[])));Assert.Null(receiver.Job); }
    [Fact] public void CommitAndBeginCannotRepeat() { using var receiver=new ImageJobReceiver();var id=Guid.NewGuid();receiver.Begin(Begin(id,Job(1)));receiver.Chunk(Chunk(id,new byte[]{1}));receiver.Commit(new(IpcMessageType.ImageCommit,id,[]));Assert.Throws<InvalidDataException>(()=>receiver.Begin(Begin(id,Job(1)))); }
    [Theory] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(641)] public void RejectInvalidWordPosition(double x) {
        var result=new OcrOutput(640,160,"en-US","x",[new("x",[new("x",x,0,1,1)])]); Assert.Throws<InvalidDataException>(()=>ImageWorkerClient.ValidateOutput(result,new(640,160),"en-US"));
    }
    [Fact] public void ValidateOutputIdentityAndBounds() {
        var result=new OcrOutput(640,160,"en-US","x",[new("x",[new("x",639,159,1,1)])]);ImageWorkerClient.ValidateOutput(result,new(640,160),"en-US");
        Assert.Throws<InvalidDataException>(()=>ImageWorkerClient.ValidateOutput(result,new(641,160),"en-US"));Assert.Throws<InvalidDataException>(()=>ImageWorkerClient.ValidateOutput(result,new(640,160),"zh-CN"));
    }
    private static string WorkerAssembly() { string configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../src/KuaiYi.ImageWorker/bin",configuration,"net10.0/win-x64/KuaiYi.ImageWorker.dll")); }
    private static Process StartWorker(string argument) { var info=new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet") {UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}; info.ArgumentList.Add(WorkerAssembly());info.ArgumentList.Add(argument);return Process.Start(info)!; }
    [Fact] public async Task RealWorkerOwnPlatformDiagnostic() {
        using var process=StartWorker("--diagnose");var json=await process.StandardOutput.ReadToEndAsync();await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(0,process.ExitCode);
        var diagnosis=ImageJson.Read<WorkerDiagnostic>(System.Text.Encoding.UTF8.GetBytes(json));
        if (!OperatingSystem.IsWindows()) {Assert.False(diagnosis.Identity.Windows);Assert.Equal(ImageFailure.UnsupportedPlatform,diagnosis.Failure);Assert.Empty(diagnosis.Languages);}
        else
        {
            Assert.True(diagnosis.Identity.Windows); // Child diagnosis is independent of this test runner's package identity.
            if (!diagnosis.Identity.Present) Assert.Equal(ImageFailure.IdentityMissing,diagnosis.Failure);
            else Assert.NotNull(diagnosis.Identity.FullName);
        }
    }
    [Fact] public async Task RealPrivateWorkerLoopReportsUnsupportedPlatformOnLinux() {
        var bytes=Fixture();var job=Job(bytes.Length);
        await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",WorkerAssembly());
        var reply=await client.RunAsync(bytes,job).WaitAsync(TimeSpan.FromSeconds(8));Assert.Equal(job,reply.Job);Assert.Equal(OperatingSystem.IsWindows() ? ImageFailure.IdentityMissing : ImageFailure.UnsupportedPlatform,reply.Failure);Assert.Null(reply.Output);Assert.Equal(HostExitState.ExitConfirmed,client.ExitState);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] public async Task RealWorkerRejectsMalformedOrExternalCommand(int kind) {
        using var process=StartWorker("--private-stdio"); var watch=new OwnedHostTermination(new StartedHostLifetime(process));
        try {
            Guid id=Guid.NewGuid();var input=process.StandardInput.BaseStream;
            if (kind==0) await IpcFrameCodec.WriteAsync(input,new(IpcMessageType.SubmitImagePath,id,"{}"u8.ToArray()));
            else {
                await IpcFrameCodec.WriteAsync(input,new(IpcMessageType.ImageHello,id,RandomNumberGenerator.GetBytes(32)));await input.FlushAsync();var hello=await IpcFrameCodec.ReadAsync(process.StandardOutput.BaseStream);Assert.Equal(IpcMessageType.ImageHello,hello!.Type);
                if (kind==1) await IpcFrameCodec.WriteAsync(input,Begin(id,Job(ImagePolicy.ByteLimit+1)));
                else if (kind==3) await IpcFrameCodec.WriteAsync(input,Begin(Guid.NewGuid(),Job(8)));
                else { await IpcFrameCodec.WriteAsync(input,Begin(id,Job(8)));await IpcFrameCodec.WriteAsync(input,new(IpcMessageType.ImageCommit,id,[])); }
            }
            await input.FlushAsync();process.StandardInput.Close();await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(3,process.ExitCode);Assert.True(watch.ObserveExit());Assert.Equal("",await process.StandardOutput.ReadToEndAsync());
        } finally { if (!watch.ObserveExit()) Assert.Equal(HostExitState.ExitConfirmed,await watch.StopAsync(TimeSpan.FromSeconds(2))); }
    }
    [Fact] public async Task RealWaitingImageWorkerCanBeTerminatedAndExitConfirmed() {
        using var process=StartWorker("--private-stdio");var watch=new OwnedHostTermination(new StartedHostLifetime(process));
        await IpcFrameCodec.WriteAsync(process.StandardInput.BaseStream,new(IpcMessageType.ImageHello,Guid.NewGuid(),new byte[32]));await process.StandardInput.BaseStream.FlushAsync();Assert.NotNull(await IpcFrameCodec.ReadAsync(process.StandardOutput.BaseStream));
        Assert.Equal(HostExitState.ExitConfirmed,await watch.StopAsync(TimeSpan.FromSeconds(2)));Assert.True(process.HasExited);
    }
    [Fact] public async Task PreCancelledClientDoesNotStartWorker() {
        await using var client=new ImageWorkerClient("nonexistent-test-executable");using var cancel=new CancellationTokenSource();cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>client.RunAsync(Fixture(),Job(Fixture().Length),cancel.Token));Assert.Null(client.ExitState);
    }
    [Fact] public async Task RealWorkerRejectsUnexpectedPostCommitCommandRegardlessOfCompletionRace() {
        using var process=StartWorker("--private-stdio");var watch=new OwnedHostTermination(new StartedHostLifetime(process));
        try {
            var input=process.StandardInput.BaseStream;var output=process.StandardOutput.BaseStream;var id=Guid.NewGuid();
            await IpcFrameCodec.WriteAsync(input,new(IpcMessageType.ImageHello,id,new byte[32]));await input.FlushAsync();Assert.NotNull(await IpcFrameCodec.ReadAsync(output));
            var bytes=Fixture();await IpcFrameCodec.WriteAsync(input,Begin(id,Job(bytes.Length)));await IpcFrameCodec.WriteAsync(input,Chunk(id,bytes));await IpcFrameCodec.WriteAsync(input,new(IpcMessageType.ImageCommit,id,[]));
            await IpcFrameCodec.WriteAsync(input,new(IpcMessageType.ShowSettings,id,[]));await input.FlushAsync();process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(3,process.ExitCode);Assert.True(watch.ObserveExit());
        } finally {if(!watch.ObserveExit()) Assert.Equal(HostExitState.ExitConfirmed,await watch.StopAsync(TimeSpan.FromSeconds(2)));}
    }
}
