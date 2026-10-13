using System.Text;
using KuaiYi.OleTransfer;
using KuaiYi.Contracts;
using System.Buffers.Binary;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;

namespace KuaiYi.Core.Tests;
public sealed class OleTargetDiagnosticsTests
{
    [Fact] public void PublicExternalFixtureIdentityPreservesSelfTerminalAndExplicitBlockExclusions()
    {
        var settings=new AppSettings();var source=new SelectionWindow(123,456,789,"C:\\public\\PublicOleFaultSource.exe");
        Assert.False(SelectionTextPolicy.Excluded(source,settings,999));
        Assert.True(SelectionTextPolicy.Excluded(source with {Executable="C:\\public\\KuaiYi.OleFaultSource.exe"},settings,999));
        Assert.True(SelectionTextPolicy.Excluded(source,settings,456));
        Assert.True(SelectionTextPolicy.Excluded(source with {Executable="C:\\public\\pwsh.exe"},settings,999));
        Assert.True(SelectionTextPolicy.Excluded(source,settings with {Privacy=new(){BlockedApplications=["PublicOleFaultSource.exe"]}},999));
    }
    [Fact] public void OnlyNamedFiniteObservationsRoundTrip()
    {
        foreach(var value in Enum.GetValues<OleTargetObservation>())
        { Assert.True(OleTargetDiagnostics.TryParse(Encoding.ASCII.GetBytes(OleTargetDiagnostics.Format(value)),out var parsed));Assert.Equal(value,parsed); }
        foreach(string text in new[]{"KYOT1|0","KYOT1|PRIVATE_SENTINEL","KYOT1|CallbackEnter|PRIVATE_SENTINEL","KYOT1|CallbackEnter\n","KYOT1|CallbackEnter\0","KYOT1| CallbackEnter"})
            Assert.False(OleTargetDiagnostics.TryParse(Encoding.UTF8.GetBytes(text),out _));
    }
    [Fact] public async Task ChunkedFloodCannotCrowdOutTargetObservationsOrLeakContent()
    {
        string startup=OleStartupDiagnostics.Format(new(OleStartupStage.Entry,OleExceptionCode.None,0));
        string records=string.Concat(Enumerable.Repeat(startup+"\n",1000))+"PRIVATE_SENTINEL\n"+string.Concat(Enumerable.Repeat(OleTargetDiagnostics.Format(OleTargetObservation.CallbackEnter)+"\r\n",1000))+"KYOT1|CallbackDrop";
        using var input=new ChunkStream(Encoding.UTF8.GetBytes(records));var drain=new OleStartupDiagnosticDrain();await drain.RunAsync(input);
        Assert.Equal(32,drain.Snapshot.Length);Assert.Equal(new[]{OleTargetObservation.CallbackEnter},drain.TargetSnapshot);Assert.Equal(input.Length,input.Position);
    }
    [Fact] public async Task BlockedDiagnosticPipeNeverBlocksAdmissionAndRetainsOriginalWriter()
    {
        using var destination=new BlockedOutput();var observations=new OleTargetDiagnostics(destination);
        try
        {
            observations.Observe(OleTargetObservation.CallbackEnter);await destination.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(observations.OriginalOutputTask!.IsCompleted);
            Assert.False(OleTargetDiagnostics.AdmitEnter(()=>false,()=>throw new Exception(),()=>throw new Exception(),()=>throw new Exception(),observations.Observe));
            for(int i=0;i<1000;i++)observations.Observe(OleTargetObservation.CallbackEnter);
            Assert.Equal(2,observations.Snapshot.Length);Assert.False(observations.OriginalOutputTask.IsCompleted);
        }
        finally { observations.CompleteOutput();destination.Release.TrySetResult();await observations.OriginalOutputTask!; }
        Assert.Equal(2,destination.Writes);Assert.Equal(TaskStatus.RanToCompletion,observations.OriginalOutputTask!.Status);
    }
    [Fact] public async Task OutputFailureDoesNotChangeAdmissionAndRetainsFaultedOriginalTask()
    {
        using var destination=new FailedOutput();var observations=new OleTargetDiagnostics(destination);
        Assert.False(OleTargetDiagnostics.AdmitEnter(()=>false,()=>true,()=>false,()=>false,observations.Observe));observations.CompleteOutput();
        await Assert.ThrowsAsync<IOException>(()=>observations.OriginalOutputTask!);
        Assert.Equal(OleExceptionCode.Io,observations.OutputFailure);Assert.Equal(TaskStatus.Faulted,observations.OriginalOutputTask!.Status);
        Assert.Throws<ArgumentOutOfRangeException>(()=>observations.Observe((OleTargetObservation)999));
    }
    [Fact] public async Task DiagnosticParseFailureCannotCancelActualProductionWriter()
    {
        Guid instance=Guid.NewGuid();byte[] hello=OleReceiverWire.Serialize(new OleReceiverHello(instance,1,Environment.ProcessId,1,new byte[32],OleReceiverWire.ActivationName(instance)));
        using var initial=new MemoryStream();await IpcFrameCodec.WriteAsync(initial,new(IpcMessageType.OleHello,instance,hello));
        using var input=new GatedInput(initial.ToArray());using var output=new SignalOutput();var observations=new OleTargetDiagnostics();
        var run=OleReceiverServer.RunAsync(input,output,(h,send)=>
        {
            send(new(IpcMessageType.OleEvent,Guid.NewGuid(),[0xff])); // Deliberately malformed optional diagnostic input.
            return Task.FromResult<IOleReceiverBackend>(new Backend());
        },targetDiagnostic:observations);
        try
        {
            await output.ThreeWrites.Task.WaitAsync(TimeSpan.FromSeconds(2));Assert.False(run.IsCompleted);Assert.Empty(observations.Snapshot);
            using var frames=new MemoryStream(output.ToArray());Assert.Equal(IpcMessageType.OleHello,(await IpcFrameCodec.ReadAsync(frames))!.Type);
            Assert.Equal(IpcMessageType.OleEvent,(await IpcFrameCodec.ReadAsync(frames))!.Type);
            Assert.Equal(IpcMessageType.OleExport,(await IpcFrameCodec.ReadAsync(frames))!.Type);
        }
        finally { input.Release.TrySetResult();await run.WaitAsync(TimeSpan.FromSeconds(2)); }
        Assert.Equal(0,await run);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)]
    public void AdmissionKeepsShortCircuitOrderAndDoesNotRetryChecks(int reject)
    {
        var calls=new List<int>();var events=new List<OleTargetObservation>();
        bool Check(int n){calls.Add(n);return n!=reject;}
        bool admitted=OleTargetDiagnostics.AdmitEnter(()=>Check(0),()=>!Check(1),()=>Check(2),()=>Check(3),events.Add);
        Assert.Equal(reject==4,admitted);Assert.Equal(Enumerable.Range(0,Math.Min(reject+1,4)),calls);
        if(reject==4)Assert.Empty(events);
        else Assert.Equal(new[]{new[]{OleTargetObservation.EnterDisabled,OleTargetObservation.EnterBusy,OleTargetObservation.EnterOutside,OleTargetObservation.EnterNoCopy}[reject]},events);
    }
    [Fact] public void AdmissionExceptionRemainsOriginalAndLaterChecksDoNotRun()
    {
        var failure=new InvalidDataException("PRIVATE_SENTINEL");bool hit=false;
        Assert.Same(failure,Assert.Throws<InvalidDataException>(()=>OleTargetDiagnostics.AdmitEnter(()=>true,()=>throw failure,()=>{hit=true;return true;},()=>true,_=>{})));
        Assert.False(hit);
    }
    private sealed class BlockedOutput:MemoryStream
    {
        public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);public int Writes;
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes,CancellationToken token=default)
        { Entered.TrySetResult();await Release.Task;Writes++;await base.WriteAsync(bytes,token); }
    }
    private sealed class FailedOutput:MemoryStream
    { public override ValueTask WriteAsync(ReadOnlyMemory<byte> bytes,CancellationToken token=default)=>ValueTask.FromException(new IOException("PRIVATE_SENTINEL")); }
    private sealed class GatedInput(byte[] bytes):MemoryStream(bytes)
    {
        public readonly TaskCompletionSource Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)
        { if(Position==Length){await Release.Task.WaitAsync(token);return 0;}return await base.ReadAsync(buffer,token); }
    }
    private sealed class SignalOutput:MemoryStream
    {
        private int flushed;public readonly TaskCompletionSource ThreeWrites=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override Task FlushAsync(CancellationToken token=default){if(Interlocked.Increment(ref flushed)==3)ThreeWrites.TrySetResult();return Task.CompletedTask;}
    }
    private sealed class Backend:IOleReceiverBackend
    {
        public byte[] ExportPacket {get;}=new byte[72];
        public Backend(){BinaryPrimitives.WriteUInt32LittleEndian(ExportPacket,0x574f454d);BinaryPrimitives.WriteUInt32LittleEndian(ExportPacket.AsSpan(4),1);OleReceiverWire.DropTargetId.TryWriteBytes(ExportPacket.AsSpan(8));}
        public void SetEnabled(long epoch,bool enabled){}
        public Task ShutdownAsync()=>Task.CompletedTask;
    }
    private sealed class ChunkStream(byte[] bytes):MemoryStream(bytes)
    { public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)=>base.ReadAsync(buffer[..Math.Min(buffer.Length,7)],token); }
}
