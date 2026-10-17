using KuaiYi.App;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class ShellCloseRequestsTests
{
    private static TaskCompletionSource<T> Signal<T>()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    [Fact]public async Task ManyPendingRequestsWaitForTheOriginalTaskAndCoalesceToOneRetryWithoutAutomaticRetryOnFailure()
    {
        var first=Signal<bool>();var second=Signal<bool>();var enteredFirst=Signal<bool>();var enteredSecond=Signal<bool>();int calls=0,active=0;
        var requests=new ShellCloseRequests(async()=>
        {
            Assert.Equal(1,Interlocked.Increment(ref active));int n=Interlocked.Increment(ref calls);
            try{Assert.InRange(n,1,2);if(n==1){enteredFirst.TrySetResult(true);return await first.Task;}enteredSecond.TrySetResult(true);return await second.Task;}
            finally{Interlocked.Decrement(ref active);}
        });
        requests.Request();await enteredFirst.Task.WaitAsync(TimeSpan.FromSeconds(5));Task original=requests.Current!;
        for(int i=0;i<32;i++)requests.Request();Assert.Same(original,requests.Current);Assert.False(original.IsCompleted);Assert.True(requests.Pending);
        first.SetResult(false);await enteredSecond.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.True(original.IsCompletedSuccessfully);Task retry=requests.Current!;Assert.NotSame(original,retry);Assert.False(requests.Pending);
        second.SetResult(false);await retry.WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(2,calls);Assert.Equal(0,active);Assert.False(requests.Pending);Assert.Same(retry,requests.Current);
    }
    [Fact]public async Task SuccessfulCloseDiscardsPendingRequestsAndNeverStartsAnotherAttempt()
    {
        var gate=Signal<bool>();var entered=Signal<bool>();int calls=0;
        var requests=new ShellCloseRequests(async()=>{Interlocked.Increment(ref calls);entered.SetResult(true);return await gate.Task;});
        requests.Request();await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));Task original=requests.Current!;
        for(int i=0;i<32;i++)requests.Request();Assert.True(requests.Pending);gate.SetResult(true);await original.WaitAsync(TimeSpan.FromSeconds(5));
        requests.Request();Assert.False(requests.Pending);Assert.Same(original,requests.Current);Assert.Equal(1,calls);
    }
    [Fact]public async Task SynchronousReentrantCallbackSeesThePublishedOriginalTaskAndOnlyQueuesOneRetry()
    {
        var finish=Signal<bool>();var retried=Signal<bool>();ShellCloseRequests? requests=null;Task? seen=null;int calls=0;
        requests=new ShellCloseRequests(async()=>
        {
            int n=Interlocked.Increment(ref calls);
            if(n==1)return await finish.Task;
            Assert.Equal(2,n);Assert.True(seen!.IsCompletedSuccessfully);retried.SetResult(true);return true;
        });
        requests.Request(()=>{seen=requests.Current;Assert.NotNull(seen);Assert.False(seen.IsCompleted);for(int i=0;i<8;i++)requests.Request();});Task original=requests.Current!;finish.SetResult(false);await retried.Task.WaitAsync(TimeSpan.FromSeconds(5));await requests.Current!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(original,seen);Assert.Equal(2,calls);Assert.False(requests.Pending);
    }
    [Fact]public async Task FaultedOriginalTaskRemainsObservableAndDoesNotRetryWithoutAnExplicitRequest()
    {
        int calls=0;var requests=new ShellCloseRequests(()=>Interlocked.Increment(ref calls)==1?Task.FromException<bool>(new IOException("PUBLIC_CLOSE_FAULT")):Task.FromResult(true));
        requests.Request();Task original=requests.Current!;await Assert.ThrowsAsync<IOException>(()=>original);Assert.Same(original,requests.Current);Assert.Equal(1,calls);Assert.False(requests.Pending);
        requests.Request();Task next=requests.Current!;Assert.NotSame(original,next);await next.WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(2,calls);
    }
    [Fact]public async Task AdmissionCallbackFailureIsOwnedByThePublishedOriginalWithoutRunningCleanupOrRetryingIt()
    {
        int calls=0;var requests=new ShellCloseRequests(()=>{Interlocked.Increment(ref calls);return Task.FromResult(true);});Task? published=null;
        Assert.Throws<IOException>(()=>requests.Request(()=>{published=requests.Current;throw new IOException("PUBLIC_ADMISSION_FAULT");}));
        Assert.NotNull(published);Assert.Same(published,requests.Current);await Assert.ThrowsAsync<IOException>(()=>published);Assert.Equal(0,calls);Assert.False(requests.Pending);
        requests.Request();await requests.Current!.WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(1,calls);
    }
}
