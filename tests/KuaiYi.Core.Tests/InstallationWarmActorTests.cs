using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class InstallationWarmActorTests
{
    private static readonly TimeSpan Guard=TimeSpan.FromSeconds(5);
    private static TaskCompletionSource Signal()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    [Fact]public async Task ANewAttemptWhilePreparingIsRejectedWithoutRevokingOrQueueingTheOriginal()
    {
        var proof=InstallationWarmImageTests.Proof();var issuer=new InstallationWarmImageAdmission(proof,new InstallationWarmImageTests.Clock());var issued=issuer.Reserve(new(),3);
        var granted=new TaskCompletionSource<InstallationWarmGrant>(TaskCreationOptions.RunContinuationsAsynchronously);var rejected=new TaskCompletionSource<InstallationWarmIdentity>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered=Signal();var release=Signal();InstallationWarmLease? original=null;int prepares=0,rejects=0;
        var actor=new InstallationWarmImageControl(proof,new InstallationWarmImageTests.Clock(),()=>3,async(admission,lease,_,_)=>
        {original=lease;Interlocked.Increment(ref prepares);entered.TrySetResult();await release.Task;Assert.True(admission.TryAccept(lease,()=>new(Guid.NewGuid(),1)));},_=>ValueTask.FromResult(true),(line,_)=>
        {
            if(line.StartsWith("WARM_GRANTED|",StringComparison.Ordinal))granted.TrySetResult(InstallationWarmImageWire.Decode<InstallationWarmGrant>(line[13..]));
            if(line.StartsWith("WARM_REJECTED|",StringComparison.Ordinal)){Interlocked.Increment(ref rejects);rejected.TrySetResult(InstallationWarmImageWire.Decode<InstallationWarmIdentity>(line[14..]));}return Task.CompletedTask;
        });
        actor.Handle("WARM_RESERVE|"+InstallationWarmImageWire.Encode(issued.Grant.Identity));var grant=await granted.Task.WaitAsync(Guard);
        var busy=issued.Grant.Identity with{Connection=Guid.NewGuid(),Request=Guid.NewGuid(),Ordinal=2,ClientNonce=InstallationWarmIdentity.NewNonce(),ServerNonce=InstallationWarmIdentity.NewNonce()};
        try
        {
            actor.Handle("WARM_COMMIT|"+InstallationWarmImageWire.Encode(new InstallationWarmSubmit(grant,"C:\\public\\a.png")));await entered.Task.WaitAsync(Guard);
            Assert.True(actor.Handle("WARM_RESERVE|"+InstallationWarmImageWire.Encode(busy)));Assert.Equal(busy,await rejected.Task.WaitAsync(Guard));
            Assert.False(original!.Token.IsCancellationRequested);Assert.Equal(InstallationWarmPhase.Preparing,actor.Admission.Phase(original));Assert.True(actor.Admission.Busy);Assert.Equal(1,prepares);Assert.Equal(1,rejects);
            release.TrySetResult();Assert.True(await actor.OriginalCompletion.WaitAsync(Guard));Assert.False(actor.Admission.Busy);
            Assert.Throws<InvalidDataException>(()=>actor.Handle("WARM_RESERVE|"+InstallationWarmImageWire.Encode(busy)));Assert.Equal(1,prepares);Assert.Equal(1,rejects);
        }
        finally{release.TrySetResult();await actor.SettleAsync().WaitAsync(Guard);await issuer.Revoke(issued);Assert.True(await issuer.RetireAsync(issued,()=>ValueTask.FromResult(true),_=>Task.CompletedTask));}
    }
    [Theory][InlineData(14999,true)][InlineData(15000,false)][InlineData(15001,false)]
    public async Task ClientReceiptCannotReportSuccessPastItsOwnDeadlineWhenTimerDeliveryIsHeld(long elapsed,bool accepted)
    {
        var proof=InstallationWarmImageTests.Proof();var clock=new InstallationWarmImageTests.Clock();var reserved=new TaskCompletionSource<InstallationWarmIdentity>(TaskCreationOptions.RunContinuationsAsynchronously);var committed=Signal();
        var client=new InstallationWarmImageClient(proof,clock,(line,_)=>
        {if(line.StartsWith("WARM_RESERVE|",StringComparison.Ordinal))reserved.TrySetResult(InstallationWarmImageWire.Decode<InstallationWarmIdentity>(line[13..]));if(line.StartsWith("WARM_COMMIT|",StringComparison.Ordinal))committed.TrySetResult();return Task.CompletedTask;});
        var reserve=client.ReserveAsync(new());var identity=await reserved.Task.WaitAsync(Guard);var grant=new InstallationWarmGrant(identity,InstallationWarmIdentity.NewNonce(),3);
        client.Receive("WARM_GRANTED|"+InstallationWarmImageWire.Encode(grant));var request=await reserve.WaitAsync(Guard);var commit=client.CommitAsync(request,"C:\\public\\a.png");await committed.Task.WaitAsync(Guard);
        clock.Advance(elapsed,false);Assert.False(request.Lease.Token.IsCancellationRequested);var source=Guid.NewGuid();
        client.Receive("WARM_RESULT|"+InstallationWarmImageWire.Encode(new InstallationWarmResult(identity,InstallationWarmOutcome.PreviewReady,source,1)));
        if(accepted){var result=await commit.WaitAsync(Guard);Assert.Equal(source,result.Source);Assert.False(request.Cancelled);}
        else{await Assert.ThrowsAsync<IOException>(()=>commit.WaitAsync(Guard));Assert.True(request.Cancelled);Assert.Equal(source,(await request.Result.Task).Source);}
        Assert.True(await request.Finish!.WaitAsync(Guard));Assert.False(client.Busy);Assert.All(request.Lease.Originals,t=>Assert.True(t.IsCompleted));await client.JoinAsync().WaitAsync(Guard);
    }
    [Fact]public async Task QuiesceHasAlreadyRevokedBeforeTheCancellationSourceStartsAndCannotLoseToAccept()
    {
        var admission=new InstallationWarmImageAdmission(InstallationWarmImageTests.Proof(),new InstallationWarmImageTests.Clock());var lease=admission.Reserve(new(),4);admission.Commit(lease,new(lease.Grant,"C:\\public\\a.png"));
        var before=Signal();var release=Signal();lease.BeforeCancellationForTest=()=>{before.TrySetResult();release.Task.GetAwaiter().GetResult();};
        var quiesce=Task.Run(async()=>await admission.Quiesce());
        try
        {
            await before.Task.WaitAsync(Guard);Assert.False(lease.Token.IsCancellationRequested);Assert.Equal(InstallationWarmPhase.Revoked,admission.Phase(lease));
            int invoked=0;Assert.False(admission.TryAccept(lease,()=>{invoked++;return new(Guid.NewGuid(),1);}));Assert.Equal(0,invoked);
        }
        finally{release.TrySetResult();await quiesce.WaitAsync(Guard);}
        Assert.True(await admission.RetireAsync(lease,()=>ValueTask.FromResult(true),_=>Task.CompletedTask));
    }
    [Fact]public async Task QuiescePublishesTheOriginalWriteBeforeCallingASynchronouslyBlockingSender()
    {
        var entered=Signal();var release=Signal();var actor=new InstallationWarmImageControl(InstallationWarmImageTests.Proof(),new InstallationWarmImageTests.Clock(),()=>3,(_,_,_,_)=>Task.CompletedTask,_=>ValueTask.FromResult(true),(line,_)=>(line.StartsWith("WARM_QUIESCE|",StringComparison.Ordinal)?Block():Task.CompletedTask));
        Task Block(){entered.TrySetResult();release.Task.GetAwaiter().GetResult();return Task.CompletedTask;}
        Task quiesce=Task.Run(actor.Quiesce);await entered.Task.WaitAsync(Guard);
        try{await quiesce.WaitAsync(Guard);var settling=actor.SettleAsync();Assert.False(settling.IsCompleted);release.TrySetResult();await settling.WaitAsync(Guard);}
        finally{release.TrySetResult();await actor.SettleAsync().WaitAsync(Guard);}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ChildDeadlinesSettleWithoutAClientTimerOrCancellationMessage(bool preparing)
    {
        var proof=InstallationWarmImageTests.Proof();var clock=new InstallationWarmImageTests.Clock();var issuer=new InstallationWarmImageAdmission(proof,new InstallationWarmImageTests.Clock());var issued=issuer.Reserve(new(),3);
        var grant=new TaskCompletionSource<InstallationWarmGrant>(TaskCreationOptions.RunContinuationsAsynchronously);var terminal=new TaskCompletionSource<InstallationWarmResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered=Signal();var release=Signal();int accepts=0;
        var actor=new InstallationWarmImageControl(proof,clock,()=>3,async(admission,lease,_,_)=>
        {entered.TrySetResult();await release.Task;admission.TryAccept(lease,()=>{accepts++;return new(Guid.NewGuid(),1);});},_=>ValueTask.FromResult(true),(line,_)=>
        {if(line.StartsWith("WARM_GRANTED|",StringComparison.Ordinal))grant.TrySetResult(InstallationWarmImageWire.Decode<InstallationWarmGrant>(line[13..]));if(line.StartsWith("WARM_RESULT|",StringComparison.Ordinal))terminal.TrySetResult(InstallationWarmImageWire.Decode<InstallationWarmResult>(line[12..]));return Task.CompletedTask;});
        actor.Handle("WARM_RESERVE|"+InstallationWarmImageWire.Encode(issued.Grant.Identity));var bound=await grant.Task.WaitAsync(Guard);
        try
        {
            Task change=actor.NextCompletionChange;
            if(preparing)
            {
                actor.Handle("WARM_COMMIT|"+InstallationWarmImageWire.Encode(new InstallationWarmSubmit(bound,"C:\\public\\a.png")));await entered.Task.WaitAsync(Guard);
                clock.Advance(15000);Assert.True(actor.Admission.Busy);Assert.False(terminal.Task.IsCompleted);release.TrySetResult();
            }
            else{clock.Advance(10000);await change.WaitAsync(Guard);}
            Assert.Equal(InstallationWarmOutcome.Rejected,(await terminal.Task.WaitAsync(Guard)).Outcome);Assert.True(await actor.OriginalCompletion.WaitAsync(Guard));Assert.False(actor.Admission.Busy);Assert.Equal(0,accepts);
        }
        finally{release.TrySetResult();await actor.SettleAsync().WaitAsync(Guard);await issuer.Revoke(issued);Assert.True(await issuer.RetireAsync(issued,()=>ValueTask.FromResult(true),_=>Task.CompletedTask));}
    }
}
