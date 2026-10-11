using KuaiYi.Core;
using Xunit;

namespace KuaiYi.Core.Tests;
public sealed class HostTerminationTests
{
    [Fact] public async Task AlreadyExitedDoesNotRequestAnotherKill()
    {
        var owned = new Lifetime { Exited = true }; var stop = new OwnedHostTermination(owned);
        Assert.Equal(HostExitState.ExitConfirmed, await stop.StopAsync(TimeSpan.FromSeconds(1))); Assert.True(stop.CanRestart); Assert.Equal(0, owned.Kills);
    }
    [Fact] public async Task KillReturningDoesNotPermitRestartWhileExitIsPending()
    {
        var owned = new Lifetime(); var stop = new OwnedHostTermination(owned); var task = stop.StopAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(HostExitState.TerminationRequested, stop.State); Assert.False(stop.CanRestart); Assert.Same(task, stop.StopAsync(TimeSpan.FromSeconds(1))); Assert.Equal(1, owned.Kills);
        owned.Exited = true; owned.Exit.SetResult(); Assert.Equal(HostExitState.ExitConfirmed, await task); Assert.True(stop.CanRestart);
    }
    [Fact] public async Task UnconfirmedExitRetainsOwnershipAndLateObservationCanConfirm()
    {
        var owned = new Lifetime(); var stop = new OwnedHostTermination(owned);
        Assert.Equal(HostExitState.ExitUnconfirmed, await stop.StopAsync(TimeSpan.FromMilliseconds(30))); Assert.False(stop.CanRestart); Assert.False(stop.ObserveExit());
        owned.Exited = true; owned.Exit.SetResult(); Assert.True(stop.ObserveExit()); Assert.True(stop.CanRestart); Assert.Equal(1, owned.Kills);
    }
    [Fact] public async Task TerminationFailureNeverMeansRecovered()
    {
        var owned = new Lifetime { FailKill = true }; var stop = new OwnedHostTermination(owned);
        Assert.Equal(HostExitState.TerminationFailed, await stop.StopAsync(TimeSpan.FromSeconds(1))); Assert.False(stop.CanRestart); Assert.Equal("IOException", stop.FailureCode);
        owned.Exited = true; Assert.True(stop.ObserveExit()); Assert.True(stop.CanRestart);
    }
    [Fact] public async Task WaitCompletingWithoutAuthoritativeExitIsUnconfirmed()
    {
        var owned = new Lifetime(); owned.Exit.SetResult(); var stop = new OwnedHostTermination(owned);
        Assert.Equal(HostExitState.ExitUnconfirmed, await stop.StopAsync(TimeSpan.FromSeconds(1))); Assert.False(stop.CanRestart);
    }
    [Fact] public async Task BrokenOwnershipObservationDoesNotAuthorizeKillOrRestart()
    {
        var owned = new Lifetime { FailObservation = true }; var stop = new OwnedHostTermination(owned);
        Assert.Equal(HostExitState.TerminationFailed, await stop.StopAsync(TimeSpan.FromSeconds(1))); Assert.Equal(0, owned.Kills); Assert.False(stop.CanRestart);
    }
    private sealed class Lifetime : IOwnedHostLifetime
    {
        public bool Exited, FailKill, FailObservation; public int Kills;
        public readonly TaskCompletionSource Exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool HasExited => FailObservation ? throw new IOException("public ownership observation failure") : Exited;
        public void RequestTermination() { Kills++; if (FailKill) throw new IOException("public fixture failure"); }
        public Task WaitForExitAsync(CancellationToken cancellation) => Exit.Task.WaitAsync(cancellation);
    }
}
