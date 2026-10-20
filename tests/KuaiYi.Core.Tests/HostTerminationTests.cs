using KuaiYi.Core;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace KuaiYi.Core.Tests;
public sealed class HostTerminationTests(ITestOutputHelper output)
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
    [Theory][InlineData("success")][InlineData("fault")][InlineData("cancelled")]
    public async Task ActualExitCannotReleaseAnUnsettledOriginalWait(string completion)
    {
        var owned=new Lifetime{IgnoreCancellation=true};var stop=new OwnedHostTermination(owned);
        try
        {
            Assert.Equal(HostExitState.ExitUnconfirmed,await stop.StopAsync(TimeSpan.FromMilliseconds(30)));
            owned.Exited=true;Assert.False(stop.ObserveExit());Assert.False(stop.CanRestart);
            Assert.Equal(HostExitState.ExitUnconfirmed,await stop.StopAsync(TimeSpan.FromMilliseconds(30)));
            Assert.Equal(1,owned.Waits);Assert.Equal(1,owned.Kills);
            if(completion=="fault")owned.Exit.SetException(new IOException("PUBLIC_LATE_EXIT_WAIT_FAILURE"));
            else if(completion=="cancelled")owned.Exit.SetCanceled();else owned.Exit.SetResult();
            Assert.True(stop.ObserveExit());Assert.True(stop.CanRestart);
            Assert.Equal(completion=="fault"?"IOException":completion=="cancelled"?"TaskCanceledException":null,stop.FailureCode);
        }
        finally{owned.Exit.TrySetResult();await Record.ExceptionAsync(()=>owned.Exit.Task);}
    }
    [Fact]
    public async Task ReentrantObservationCannotReleaseTheLifetimeBeforeWaitPublication()
    {
        var owned=new Lifetime{IgnoreCancellation=true};var stop=new OwnedHostTermination(owned);bool? observed=null;HostExitState? nested=null;
        owned.Terminating=()=>{owned.Exited=true;observed=stop.ObserveExit();nested=stop.StopAsync(TimeSpan.FromMilliseconds(30)).GetAwaiter().GetResult();};
        try
        {
            Assert.Equal(HostExitState.ExitUnconfirmed,await stop.StopAsync(TimeSpan.FromMilliseconds(30)));
            Assert.False(observed);Assert.Equal(HostExitState.ExitUnconfirmed,nested);Assert.Equal(1,owned.Waits);Assert.Equal(1,owned.Kills);
            owned.Exit.SetResult();Assert.True(stop.ObserveExit());
        }
        finally{owned.Exit.TrySetResult();}
    }
    [Fact]
    public async Task RealExitedChildHandleRemainsOwnedUntilItsOriginalExitWaitSettles()
    {
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);HeldExit? held=null;var session=new OwnedProcessSession{LifetimeForTest=p=>held=new(p,release.Task)};
        string assembly=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tools/KuaiYi.SelectionReuseDiagnostic/bin/Release/net10.0/KuaiYi.SelectionReuseDiagnostic.dll"));
        var info=OwnedProcessSession.Info(ManagedFixtureHost.Executable,null);info.ArgumentList.Clear();info.ArgumentList.Add(assembly);info.ArgumentList.Add("--synthetic-child");info.ArgumentList.Add("hang");
        Process child=await session.Start(info,default).WaitAsync(TimeSpan.FromSeconds(3));
        try
        {
            Task<HostExitState> stop=session.StopAsync();await held!.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(child.HasExited);int pid=child.Id,exit=child.ExitCode;Assert.False(held.Original!.IsCompleted);
            bool finalized=await session.TryFinalizeAsync();output.WriteLine($"PUBLIC_EXIT_WAIT actualPid={pid} actualExit={exit} originalWait={held.Original.Status} finalized={finalized} waitCalls={held.Waits}");
            Assert.False(finalized);Assert.False(session.ObserveExit());Assert.True(child.HasExited);Assert.Equal(HostExitState.ExitUnconfirmed,await stop);
            release.TrySetResult();await held.Original.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(await session.TryFinalizeAsync(retry:true));Assert.Equal(1,held.Waits);
        }
        finally{release.TrySetResult();if(held?.Original is {} original)await Record.ExceptionAsync(()=>original.WaitAsync(TimeSpan.FromSeconds(3)));await session.TryFinalizeAsync(retry:true);}
    }
    private sealed class HeldExit(Process child,Task release):IOwnedHostLifetime
    {
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task? Original;internal int Waits;
        public bool HasExited=>child.HasExited;
        public void RequestTermination()=>child.Kill();
        public Task WaitForExitAsync(CancellationToken cancellation){Waits++;return Original=Wait();}
        private async Task Wait(){await child.WaitForExitAsync();Entered.TrySetResult();await release;Assert.True(child.HasExited);}
    }
    private sealed class Lifetime : IOwnedHostLifetime
    {
        public bool Exited, FailKill, FailObservation,IgnoreCancellation; public int Kills,Waits;
        public Action? Terminating;
        public readonly TaskCompletionSource Exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool HasExited => FailObservation ? throw new IOException("public ownership observation failure") : Exited;
        public void RequestTermination() { Kills++;Terminating?.Invoke(); if (FailKill) throw new IOException("public fixture failure"); }
        public Task WaitForExitAsync(CancellationToken cancellation){Waits++;return IgnoreCancellation?Exit.Task:Exit.Task.WaitAsync(cancellation);}
    }
}
