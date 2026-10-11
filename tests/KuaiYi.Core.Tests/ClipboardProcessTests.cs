using System.Diagnostics;
using KuaiYi.Core;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class ClipboardProcessTests
{
    private class Starter(string fault):IOwnedProcessStarter
    {
        public int Starts;public Process? Child;public virtual Process Start(ProcessStartInfo original)
        {
            Interlocked.Increment(ref Starts);string configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;string assembly=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tools/KuaiYi.CaptureControlFixture/bin",configuration,"net10.0/KuaiYi.CaptureControlFixture.dll"));var info=OwnedProcessSession.Info(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",null);info.ArgumentList.Clear();info.ArgumentList.Add(assembly);info.ArgumentList.Add("--clipboard-fixture");info.ArgumentList.Add(fault);foreach(string value in original.ArgumentList)info.ArgumentList.Add(value);return Child=Process.Start(info)??throw new IOException("Public fixture start failed.");
        }
    }
    private static readonly SelectionGesture Gesture=new(new(123,456,789,@"C:\Public\public.exe"),new(3,3),new(70,3),1000,1,true);
    private static readonly SelectionCopyEvidence Proof=new(234,[12,4],new(0,0,500,200),[new(0,0,100,20)],true);
    [Theory][InlineData("normal",SelectionCaptureStatus.Success,1)][InlineData("unsupported",SelectionCaptureStatus.ClipboardUnsupported,0)][InlineData("identity",SelectionCaptureStatus.ProtocolFailure,0)][InlineData("role",SelectionCaptureStatus.ProtocolFailure,0)][InlineData("source",SelectionCaptureStatus.ProtocolFailure,0)][InlineData("invalid",SelectionCaptureStatus.ProtocolFailure,1)][InlineData("failed-text",SelectionCaptureStatus.ProtocolFailure,1)][InlineData("extra",SelectionCaptureStatus.ProtocolFailure,1)][InlineData("expired",SelectionCaptureStatus.ClipboardTimeout,1)][InlineData("crash",SelectionCaptureStatus.ProtocolFailure,0)]
    public async Task ActualOwnedClipboardFixtureEnforcesStageIdentityInputAndSensitivePayloadOwnership(string fault,SelectionCaptureStatus expected,int expectedInputs)
    {
        var starter=new Starter(fault);int inputs=0,released=0;await using var client=new ClipboardCaptureClient("public-fixture",starter){PayloadReleasedForTest=bytes=>{released++;Assert.All(bytes,b=>Assert.Equal(0,b));}};
        var result=await client.CaptureAsync(Gesture,Proof,sequence=>{Assert.Equal(10u,sequence);inputs++;return new(TimeProvider.System.GetTimestamp(),42);},()=>true,default);Assert.Equal(expected,result.Status);Assert.Equal(expectedInputs,inputs);Assert.Equal(1,starter.Starts);
        if(expected==SelectionCaptureStatus.Success){Assert.Equal("public clipboard",result.Text);Assert.Equal(SelectionSourceKind.Clipboard,result.SourceKind);Assert.Equal(ClipboardRecovery.Restored,result.Recovery);}else Assert.Equal("",result.Text);await client.DisposeAsync();Assert.Null(client.ExitState);if(fault!="crash")Assert.True(released>0);
    }
    [Fact]public async Task ActualSnapshotReadyAfterOffCannotIssueCopyAndReturnsNoText()
    {
        var starter=new Starter("normal");bool current=true;int inputs=0;await using var client=new ClipboardCaptureClient("public-fixture",starter){SnapshotReadyForTest=()=>current=false};var result=await client.CaptureAsync(Gesture,Proof,_=>{inputs++;return new(10000,42);},()=>current,default);Assert.Equal(SelectionCaptureStatus.EmptyOrUnsupported,result.Status);Assert.Equal(0,inputs);Assert.Equal("",result.Text);await client.DisposeAsync();Assert.Null(client.ExitState);
    }
    [Fact]public async Task RejectedInputNeverReceivesSuccessfulOldClipboardPayload()
    {
        await using var client=new ClipboardCaptureClient("public-fixture",new Starter("normal"));var result=await client.CaptureAsync(Gesture,Proof,_=>null,()=>true,default);Assert.Equal(SelectionCaptureStatus.EmptyOrUnsupported,result.Status);Assert.Equal("",result.Text);
    }
    [Theory][InlineData("hang-before-ready")][InlineData("hang-after-sent")][InlineData("late")]
    public async Task UncooperativeActualChildHasBoundedParentWaitAndOwnedCleanup(string fault)
    {
        var starter=new Starter(fault);await using var client=new ClipboardCaptureClient("public-fixture",starter);var watch=Stopwatch.StartNew();int inputs=0;var result=await client.CaptureAsync(Gesture,Proof,_=>{inputs++;return new(TimeProvider.System.GetTimestamp(),42);},()=>true,default);Assert.Equal(SelectionCaptureStatus.ClipboardTimeout,result.Status);Assert.Equal("",result.Text);Assert.True(watch.Elapsed<TimeSpan.FromSeconds(3));Assert.Equal(fault=="hang-before-ready"?0:1,inputs);await client.DisposeAsync();Assert.Null(client.ExitState);
    }
    [Fact]public async Task CancellationAfterCopyReleasesControlForRestorationAndNeverReturnsText()
    {
        var starter=new Starter("wait-cancel");using var cancellation=new CancellationTokenSource();await using var client=new ClipboardCaptureClient("public-fixture",starter);int inputs=0;var result=await client.CaptureAsync(Gesture,Proof,_=>{inputs++;cancellation.Cancel();return new(TimeProvider.System.GetTimestamp(),42);},()=>true,cancellation.Token);Assert.Equal(SelectionCaptureStatus.Cancelled,result.Status);Assert.Equal("",result.Text);Assert.Equal(1,inputs);Assert.Equal(ClipboardRecovery.Unconfirmed,result.Recovery);await client.DisposeAsync();Assert.Null(client.ExitState);
    }
    [Fact]public async Task PreCancelledOrIneligibleNeverCreatesAClipboardProcess()
    {
        var starter=new Starter("normal");using var cancellation=new CancellationTokenSource();cancellation.Cancel();await using var client=new ClipboardCaptureClient("public-fixture",starter);Assert.Equal(SelectionCaptureStatus.Cancelled,(await client.CaptureAsync(Gesture,Proof,_=>throw new InvalidOperationException(),()=>true,cancellation.Token)).Status);Assert.Equal(SelectionCaptureStatus.Cancelled,(await client.CaptureAsync(Gesture,Proof,_=>throw new InvalidOperationException(),()=>false,default)).Status);Assert.Equal(0,starter.Starts);Assert.Null(client.ExitState);
    }
    private sealed class DelayedStarter():Starter("normal")
    {public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);public int Entries;public override Process Start(ProcessStartInfo info){Interlocked.Increment(ref Entries);Entered.TrySetResult();Release.Task.GetAwaiter().GetResult();return base.Start(info);}}
    [Fact]public async Task PendingStartupKeepsOwnershipUntilLateHeldChildSettlesAndForbidsSecondWriter()
    {
        var starter=new DelayedStarter();var client=new ClipboardCaptureClient("public-fixture",starter);var capture=client.CaptureAsync(Gesture,Proof,_=>new(TimeProvider.System.GetTimestamp(),42),()=>true,default);await starter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));try{Assert.Equal(SelectionCaptureStatus.ClipboardTimeout,(await capture).Status);Assert.Equal(SelectionCaptureStatus.CleanupPending,(await client.CaptureAsync(Gesture,Proof,_=>throw new InvalidOperationException(),()=>true,default)).Status);Assert.Equal(1,starter.Entries);Assert.Equal(0,starter.Starts);}finally{starter.Release.TrySetResult();await client.DisposeAsync();}Assert.Equal(1,starter.Starts);Assert.Null(client.ExitState);
    }
}
