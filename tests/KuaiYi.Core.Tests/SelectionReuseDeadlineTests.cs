using System.Diagnostics;
using KuaiYi.Core;
using KuaiYi.SelectionReuseDiagnostic;
using Xunit;
namespace KuaiYi.Core.Tests;
public partial class SelectionReuseHostTests
{
    [Fact]public async Task SyntheticDeadlineClockCannotStartANativeWorker()
    {
        var starter=new NeverStarter();await using var host=new ReuseDiagnosticHost("synthetic","synthetic",enabled:true,starter:starter,request:Spec(),nativeWorker:true){DeadlineClockForTest=new InstallationWarmImageTests.Clock()};
        await Assert.ThrowsAsync<InvalidOperationException>(()=>host.RunAsync(1));Assert.Equal(0,starter.Calls);
    }
    private sealed class CrashStarter:IOwnedProcessStarter
    {
        internal Process? Original;
        public Process Start(ProcessStartInfo info)=>Original=Process.Start(info)!;
    }
    private sealed class OriginalEof(Stream inner):Stream
    {
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)
        {int n=await inner.ReadAsync(buffer,CancellationToken.None);if(n!=0)throw new InvalidDataException("PUBLIC_EXPECTED_ORIGINAL_EOF");buffer.Span.Fill(0xa5);Entered.TrySetResult();await Release.Task;return n;}
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override void Flush()=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
    }
    [Theory][InlineData(599,false,ReuseRoundReason.Crash)][InlineData(600,false,ReuseRoundReason.HardTimeout)][InlineData(600,true,ReuseRoundReason.HardTimeout)]
    public async Task SyntheticDeadlineClockControlsAbsoluteCheckAndTimerWithoutReleasingOriginalIoEarly(long elapsed,bool fire,ReuseRoundReason expected)
    {
        var clock=new InstallationWarmImageTests.Clock();var starter=new CrashStarter();await using var host=Host("crash",starter,clock);OriginalEof? held=null;
        var installed=new TaskCompletionSource<OriginalEof>(TaskCreationOptions.RunContinuationsAsynchronously);
        var buffers=new List<byte[]>();host.ReadBufferAllocatedForTest=buffers.Add;host.OutputForTest=stream=>{held=new(stream);installed.TrySetResult(held);return held;};var running=host.RunAsync(2);
        try
        {
            await host.StartedForTest.Task.WaitAsync(TimeSpan.FromSeconds(3));
            held=await installed.Task.WaitAsync(TimeSpan.FromSeconds(3));await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await starter.Original!.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));Assert.Equal(23,starter.Original.ExitCode);Assert.Single(clock.Timers);Assert.False(running.IsCompleted);
            clock.Advance(elapsed,fire);
            if(fire){await running.WaitAsync(TimeSpan.FromSeconds(3));Assert.Null(host.Summary);Assert.True(host.HoldsOwnership);Assert.False(host.CanReplace);}
            else Assert.False(running.IsCompleted);
            held.Release.TrySetResult();await running.WaitAsync(TimeSpan.FromSeconds(3));await Settle(host);
            Assert.Equal(expected,host.Summary!.Reason);Assert.Equal(WorkerTermination.NaturalNonZero,host.Summary.Termination);Assert.True(host.Summary.ExitConfirmed&&host.Summary.BuffersCleared);Assert.True(host.CanReplace);Assert.All(buffers,b=>Assert.All(b,x=>Assert.Equal(0,x)));
        }
        finally{held?.Release.TrySetResult();if(!running.IsCompleted)host.ParentDisconnected();await running;}
    }
}
