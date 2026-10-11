using KuaiYi.OleTransfer;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class OleReceiverCommitAdmissionTests
{
    private sealed class Harness
    {
        public long Elapsed;public bool Active=true,Source=true;public readonly OleReceiverGate Gate=new();public readonly CancellationTokenSource Cancellation=new();public int SourceCalls;public Action? DuringSource;
        public Harness(){Assert.True(Gate.Set(1,true));}
        public bool Commit()=>OleReceiverCommitAdmission.CanCommit(()=>TimeSpan.FromMilliseconds(Elapsed),()=>Gate.Current(1),()=>Active,()=>{SourceCalls++;DuringSource?.Invoke();return Source;},Cancellation.Token);
    }
    [Theory][InlineData(1499,true)][InlineData(1500,false)][InlineData(1501,false)]
    public void ReceiverLocalNormalBoundaryIsStrict(int elapsed,bool expected)
    {var h=new Harness{Elapsed=elapsed};Assert.Equal(expected,h.Commit());Assert.Equal(expected?1:0,h.SourceCalls);}
    [Theory][InlineData(1498,1,true)][InlineData(1499,1,false)][InlineData(1499,2,false)][InlineData(0,1500,false)]
    public void SourceValidationCannotCrossOriginalReceiverDeadline(int before,int consumed,bool expected)
    {var h=new Harness{Elapsed=before};h.DuringSource=()=>h.Elapsed+=consumed;Assert.Equal(expected,h.Commit());Assert.Equal(1,h.SourceCalls);}
    [Theory][InlineData("gate-off")][InlineData("epoch")][InlineData("activation")][InlineData("cancel")]
    public void SourceValidationCannotRevokeEligibilityThenCommit(string change)
    {var h=new Harness{Elapsed=10};h.DuringSource=()=>{switch(change){case "gate-off":Assert.True(h.Gate.Set(long.MaxValue,false));break;case "epoch":Assert.True(h.Gate.Set(2,true));break;case "activation":h.Active=false;break;default:h.Cancellation.Cancel();break;}};Assert.False(h.Commit());Assert.Equal(1,h.SourceCalls);}
    [Theory][InlineData("gate")][InlineData("activation")][InlineData("cancel")][InlineData("source")]
    public void InvalidReceiverEligibilityDoesNotExposeText(string change)
    {var h=new Harness();switch(change){case "gate":h.Gate.Set(long.MaxValue,false);break;case "activation":h.Active=false;break;case "cancel":h.Cancellation.Cancel();break;default:h.Source=false;break;}Assert.False(h.Commit());Assert.Equal(change=="source"?1:0,h.SourceCalls);}
    [Fact]public void ReceiverElapsedTimeUsesOnlyReceiverClockRegardlessOfOtherProcessOrigin()
    {long receiverOrigin=9_000_000,parentOrigin=-4_000_000;long receiverNow=receiverOrigin+1499;var gate=new OleReceiverGate();gate.Set(1,true);Assert.True(OleReceiverCommitAdmission.CanCommit(()=>TimeSpan.FromMilliseconds(receiverNow-receiverOrigin),()=>gate.Current(1),()=>true,()=>true));receiverNow++;Assert.False(OleReceiverCommitAdmission.CanCommit(()=>TimeSpan.FromMilliseconds(receiverNow-receiverOrigin),()=>gate.Current(1),()=>true,()=>true));Assert.Equal(-4_000_000,parentOrigin);}
    [Fact]public void FailedSourceValidationNeverBecomesAdmissionAtLaterValidTime()
    {var h=new Harness();h.DuringSource=()=>{h.Source=false;h.Elapsed=1499;};Assert.False(h.Commit());}
}
