using KuaiYi.Core;
using Xunit;

namespace KuaiYi.Core.Tests;

public sealed class OleWindowOwnershipTests
{
    private static TaskCompletionSource Pending() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static OleWindowOwnership Ready()
    {
        var owner = new OleWindowOwnership(17);
        owner.TransferFrameworkRegistration(() => { });
        return owner;
    }
    [Fact]
    public void FreshAndWrongWindowCannotRegister()
    {
        var owner = new OleWindowOwnership(17);
        Assert.Throws<InvalidOperationException>(() => owner.Reserve(17, Pending().Task));
        owner.TransferFrameworkRegistration(() => { });
        Assert.Throws<InvalidOperationException>(() => owner.Reserve(18, Pending().Task));
        Assert.Throws<InvalidOperationException>(() => owner.Reserve(17, Task.CompletedTask));
    }
    [Fact]
    public void HandoffIsOneShotIncludingFailure()
    {
        var owner = new OleWindowOwnership(17); int calls = 0;
        Assert.Throws<IOException>(() => owner.TransferFrameworkRegistration(() => { calls++; throw new IOException(); }));
        Assert.Throws<InvalidOperationException>(() => owner.TransferFrameworkRegistration(() => calls++));
        Assert.Throws<InvalidOperationException>(() => owner.Reserve(17, Pending().Task));
        Assert.True(owner.TryClose()); Assert.Equal(1, calls);
    }
    [Fact]
    public void HandoffCannotPublishLeaseOrCloseReentrantly()
    {
        var owner = new OleWindowOwnership(17);
        owner.TransferFrameworkRegistration(() =>
        {
            Assert.False(owner.TryClose());
            Assert.Throws<InvalidOperationException>(() => owner.Reserve(17, Pending().Task));
        });
        Assert.True(owner.TryClose());
    }
    [Fact]
    public void DuplicateLeaseAndCloseWaitForOriginalTaskAfterCleanup()
    {
        var owner = Ready(); var original = Pending(); var lease = owner.Reserve(17, original.Task);
        Assert.Throws<InvalidOperationException>(() => owner.Reserve(17, Pending().Task));
        Assert.False(owner.TryClose()); lease.ConfirmCleanup(true);
        Assert.False(owner.TryClose());
        Assert.Throws<InvalidOperationException>(() => owner.Reserve(17, Pending().Task));
        original.SetResult();
        var second = Pending(); var next = owner.Reserve(17, second.Task);
        second.SetResult(); Assert.False(owner.TryClose()); next.ConfirmCleanup(true);
        Assert.True(owner.TryClose());
        Assert.Throws<InvalidOperationException>(() => owner.Reserve(17, Pending().Task));
    }
    [Fact]
    public void OriginalTaskSettlementAloneNeverProvesCleanup()
    {
        var owner = Ready(); var original = Pending(); var lease = owner.Reserve(17, original.Task);
        original.SetResult(); Assert.False(owner.TryClose());
        Assert.Throws<InvalidOperationException>(() => owner.Reserve(17, Pending().Task));
        lease.ConfirmCleanup(false);
        Assert.Throws<InvalidOperationException>(() => lease.ConfirmCleanup(true));
        Assert.False(owner.TryClose());
        Assert.Throws<InvalidOperationException>(() => owner.Reserve(17, Pending().Task));
    }
    [Fact]
    public void DestroyedGenerationCannotRegisterEvenIfNumericHandleIsReused()
    {
        var owner = Ready(); owner.Invalidate();
        Assert.Throws<InvalidOperationException>(() => owner.Reserve(17, Pending().Task));
        Assert.Throws<InvalidOperationException>(() => owner.TransferFrameworkRegistration(() => throw new Exception()));
        var fresh = Ready(); var original = Pending(); fresh.Reserve(17, original.Task);
        Assert.False(fresh.TryClose());
    }
    [Fact]
    public void ConcurrentReservationsHaveExactlyOneOwner()
    {
        var owner = Ready(); int accepted = 0;
        Parallel.For(0, 32, _ => { try { owner.Reserve(17, Pending().Task); Interlocked.Increment(ref accepted); } catch (InvalidOperationException) { } });
        Assert.Equal(1, accepted); Assert.False(owner.TryClose());
    }
}
