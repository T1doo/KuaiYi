using Xunit;
namespace KuaiYi.Core.Tests;
public sealed partial class InstallationSessionTests
{
    private sealed class ParentWarmWrite(Stream inner,bool flush):Stream
    {
        private bool commit;
        internal readonly TaskCompletionSource Entered=Signal(),Release=Signal();
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush()=>throw new NotSupportedException();public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellationToken=default)
        {
            commit=buffer.Span.StartsWith("WARM_COMMIT|"u8);await inner.WriteAsync(buffer,CancellationToken.None);
            if(commit&&!flush){Entered.TrySetResult();await Release.Task;}
        }
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {if(commit&&flush){Entered.TrySetResult();await Release.Task;}await inner.FlushAsync(CancellationToken.None);}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public Task ControllerCancellationStartsBeforeBlockedWriteLockAndTheOriginalExitWriteMustSettle(bool flush)=>WarmScenario("warm-held-work",false,async test=>
    {
        ParentWarmWrite? blocked=null;test.Port.HoldWarmWrites(inner=>blocked??=new(inner,flush));var request=await test.Warm.ReserveAsync(new()).WaitAsync(Limit);var commit=test.Warm.CommitAsync(request,ImagePath);
        bool released=false;Task? exit=null;Exception? primary=null;
        try
        {
            await blocked!.Entered.Task.WaitAsync(Limit);await test.Row("WAIT_WARM_PREPARE","PUBLIC_SYNTHETIC_WARM_PREPARE");
            await test.Warm.Cancel(request).WaitAsync(Limit);Assert.True(request.Lease.Token.IsCancellationRequested);Assert.False(commit.IsCompleted);Assert.True(test.Warm.Busy);
            // Lifetime cancellation uses the existing exact-child forced-stop owner, not RequestExitAsync.
            // Hold and join the actual graceful STOP write here, on the same production writer as COMMIT/CANCEL.
            test.CloseSent=true;exit=test.Port.RequestExitAsync(test.Port.Children.Single(),CancellationToken.None);await test.Port.ExitRequested.Task.WaitAsync(Limit);
            Assert.False(exit.IsCompleted);
            Assert.False(test.Run.IsCompleted);Assert.False(test.Port.Children.Single().HasExited);test.Rig.Fence();Assert.True(File.Exists(Path.Combine(test.Rig.Root,"PUBLIC_SYNTHETIC_WARM_OWNER")));
            blocked.Release.TrySetResult();await test.Row("WAIT_WARM_CALLBACK","PUBLIC_SYNTHETIC_WARM_CANCEL_HELD");
            Assert.False(test.Run.IsCompleted);Assert.True(File.Exists(Path.Combine(test.Rig.Root,"PUBLIC_SYNTHETIC_WARM_OWNER")));await test.Peer.Send("RELEASE_WARM_WORK");released=true;
            await Assert.ThrowsAsync<IOException>(()=>commit.WaitAsync(Limit));Assert.True(await request.Finish!.WaitAsync(Limit));await exit.WaitAsync(Limit);
        }
        catch(Exception error){primary=error;}
        finally
        {
            blocked?.Release.TrySetResult();List<Exception> cleanup=[];
            try{if(!released)await test.Peer.Send("RELEASE_WARM_WORK");}catch(Exception error){cleanup.Add(error);}
            try{if(exit!=null)await exit.WaitAsync(Limit);}catch(Exception error){cleanup.Add(error);}
            if(primary!=null&&cleanup.Count!=0)throw new AggregateException(new[]{primary}.Concat(cleanup));
            if(primary!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();if(cleanup.Count!=0)throw new AggregateException(cleanup);
        }
    });
}
