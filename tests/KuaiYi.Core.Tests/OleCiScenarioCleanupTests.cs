using KuaiYi.App;
using Xunit;

namespace KuaiYi.Core.Tests;

public sealed class OleCiScenarioCleanupTests
{
    [Fact]
    public async Task SourceCleanupStartsOnlyAfterOwnedPressRelease()
    {
        var order=new List<string>(); bool held=false;
        var press=new OleCiOwnedPress(()=>held=true,()=>{held=false;order.Add("release");});press.Down();
        await OleCiScenarioCleanup.CompleteAsync(null,()=>press.Release(),()=>
        {
            Assert.False(held);order.Add("source");return ValueTask.CompletedTask;
        },_=>throw new Exception("Unexpected cleanup error"));
        Assert.Equal(new[]{"release","source"},order);
    }
    [Fact]
    public async Task PrimaryExceptionAndStackSurviveReleaseAndSourceFailures()
    {
        var primary=new InvalidDataException("primary");var release=new IOException("release");var source=new TimeoutException("source");
        var errors=new List<Exception>();bool sourceCalled=false;
        void ThrowFromScenario()=>throw primary;
        async Task Run()
        {
            Exception? first=null;
            try { ThrowFromScenario(); }
            catch(Exception error) { first=error;throw; }
            finally { await OleCiScenarioCleanup.CompleteAsync(first,()=>throw release,()=>{sourceCalled=true;throw source;},errors.Add); }
        }
        var actual=await Assert.ThrowsAsync<InvalidDataException>(Run);
        Assert.Same(primary,actual);Assert.Contains(nameof(ThrowFromScenario),actual.StackTrace!);
        Assert.True(sourceCalled);Assert.Equal(new Exception[]{release,source},errors);
    }
    [Fact]
    public async Task WithoutPrimaryFirstCleanupFailureFailsAndSecondIsRetained()
    {
        var release=new IOException("release");var source=new TimeoutException("source");var errors=new List<Exception>();
        var actual=await Assert.ThrowsAsync<IOException>(()=>OleCiScenarioCleanup.CompleteAsync(null,()=>throw release,()=>throw source,errors.Add));
        Assert.Same(release,actual);Assert.Equal(new Exception[]{release,source},errors);
    }
    [Fact]
    public void FailedReleaseRetainsOwnedPressAndNeverReleasesUnknownInput()
    {
        bool fail=true;int downs=0,ups=0;
        var press=new OleCiOwnedPress(()=>downs++,()=>{ups++;if(fail)throw new IOException();});
        Assert.False(press.Release());Assert.Equal(0,ups);
        press.Down();Assert.Throws<InvalidOperationException>(press.Down);Assert.Equal(1,downs);
        Assert.Throws<IOException>(()=>press.Release());Assert.True(press.IsHeld);
        fail=false;Assert.True(press.Release());Assert.False(press.IsHeld);
        Assert.False(press.Release());Assert.Equal(2,ups);
    }
    [Fact]
    public void FailedDownNeverClaimsOrReleasesAButton()
    {
        int ups=0;var press=new OleCiOwnedPress(()=>throw new IOException(),()=>ups++);
        Assert.Throws<IOException>(press.Down);Assert.False(press.IsHeld);Assert.False(press.Release());Assert.Equal(0,ups);
    }
    [Fact]
    public async Task NestedInputAndSourceCleanupCannotReplaceScenarioFailure()
    {
        var primary=new InvalidDataException("scenario");var input=new IOException("input release");var source=new TimeoutException("source cleanup");
        var errors=new List<Exception>();
        async Task Run()
        {
            Exception? outer=null;
            try
            {
                Exception? inner=null;
                try { throw primary; }
                catch(Exception error) { inner=error;throw; }
                finally { await OleCiScenarioCleanup.CompleteAsync(inner,()=>throw input,()=>ValueTask.CompletedTask,errors.Add); }
            }
            catch(Exception error) { outer=error;throw; }
            finally { await OleCiScenarioCleanup.CompleteAsync(outer,()=>{},()=>throw source,errors.Add); }
        }
        Assert.Same(primary,await Assert.ThrowsAsync<InvalidDataException>(Run));
        Assert.Equal(new Exception[]{input,source},errors);
    }
}
