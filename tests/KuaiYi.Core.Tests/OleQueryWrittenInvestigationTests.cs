using System.Diagnostics;
using KuaiYi.Core;
using KuaiYi.OleTransfer;
using Xunit;
using Xunit.Abstractions;
namespace KuaiYi.Core.Tests;

public sealed class OleQueryWrittenInvestigationTests(ITestOutputHelper output)
{
    [Fact]public async Task DeliveredStdoutThenOwnedTerminationCannotRecoverUnwrittenChildDiagnostics()
    {
        using var fixture=new Fixture();var client=fixture.Client;var run=fixture.Run();
        try{await fixture.Delivered();await fixture.UntilStatus("diagnostic:held:QueryWritten");Snapshot("before-kill",fixture);Assert.DoesNotContain(OleTargetObservation.QueryWritten,client.TargetObservations);Assert.False(client.DiagnosticReadSettled);}
        finally{await fixture.Cleanup(run);}
        Snapshot("after-kill",fixture);Assert.True(client.ObserveExit());Assert.True(client.DiagnosticReadSettled);Assert.Equal(OleExceptionCode.None,client.DiagnosticReadFailure);Assert.DoesNotContain(OleTargetObservation.QueryWritten,client.TargetObservations);Assert.Contains(OleTargetObservation.QueryRead,client.TargetObservations);Assert.Contains(OleTargetObservation.QueryStaleEpoch,client.TargetObservations);Assert.Contains(OleTargetObservation.QueryAccepted,client.TargetObservations);
        await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(()=>OleTargetReceipt.WaitAsync(client,OleTargetObservation.QueryWritten));
    }
    [Fact]public async Task HeldChildDiagnosticMustReachItsOwnReceiptBeforeOriginalCleanupAssertions()
    {
        using var fixture=new Fixture();var client=fixture.Client;var run=fixture.Run();
        try
        {
            await fixture.Delivered();await fixture.UntilStatus("diagnostic:held:QueryWritten");Snapshot("delivered-before-cleanup",fixture);
            var receipt=OleTargetReceipt.WaitAsync(client,OleTargetObservation.QueryWritten);Assert.False(receipt.IsCompleted);await fixture.Release();await receipt;Snapshot("actual-receipt-before-cleanup",fixture);
        }
        finally{await fixture.Cleanup(run);}
        Snapshot("settled",fixture);Assert.Contains(OleTargetObservation.QueryWritten,client.TargetObservations);Assert.Contains(OleTargetObservation.QueryRead,client.TargetObservations);Assert.Contains(OleTargetObservation.QueryStaleEpoch,client.TargetObservations);Assert.Contains(OleTargetObservation.QueryAccepted,client.TargetObservations);
    }
    private void Snapshot(string stage,Fixture fixture)=>output.WriteLine("{0} ticks={1} pid={2} exit={3} diagnostic={4} failure={5} target={6}",stage,Stopwatch.GetTimestamp(),fixture.Client.OwnedProcessId,fixture.Client.ExitState,fixture.Client.DiagnosticReadTaskStatus,fixture.Client.DiagnosticReadFailure,string.Join(',',fixture.Client.TargetObservations));
    private sealed class Fixture:IOwnedProcessStarter,IDisposable
    {
        private readonly string root=Path.Combine(Path.GetTempPath(),"kuaiyi-query-written-"+Guid.NewGuid().ToString("N"));private string Status=>Path.Combine(root,"status");private readonly List<string> seen=[];private readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously),delivered=new(TaskCreationOptions.RunContinuationsAsynchronously);private readonly Activation activation;private readonly Registrar registrar=new();private Process? child;
        public OleReceiverClient Client{get;}
        public Fixture(){Directory.CreateDirectory(root);Guid id=Guid.NewGuid();activation=new(id);Client=new(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",registrar,activation,Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tools/KuaiYi.OleReceiverFixture/bin",new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name,"net10.0/KuaiYi.OleReceiverFixture.dll")),this);}
        public Process Start(ProcessStartInfo info){info.ArgumentList.Add("formal-stale-query-held-diagnostics");info.ArgumentList.Add("--test-status");info.ArgumentList.Add(Status);return child=SystemOwnedProcessStarter.Instance.Start(info);}
        public Task Run()=>Client.RunAsync(new(activation.Id,1,Environment.ProcessId,1,new byte[32],activation.Name){ProductionTextDrop=true},e=>{if(e.Phase=="Ready")ready.TrySetResult();},(_,_,_,_)=>{delivered.TrySetResult();return Task.CompletedTask;});
        public async Task Delivered(){await ready.Task.WaitAsync(TimeSpan.FromSeconds(4));Assert.True(await Client.SetEnabledAsync(2,true,new(new(0,0,48,48),new(0,0,48,48),24),[]));await delivered.Task.WaitAsync(TimeSpan.FromSeconds(4));}
        public async Task UntilStatus(string expected)
        {
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(4));
            while(true){deadline.Token.ThrowIfCancellationRequested();if(File.Exists(Status)){using var file=new FileStream(Status,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);using var reader=new StreamReader(file);string[] lines=(await reader.ReadToEndAsync(deadline.Token)).Split('\n');seen.Clear();seen.AddRange(lines[..^1].Select(line=>line.TrimEnd('\r')));if(seen.Contains(expected))return;}await Task.Delay(5,deadline.Token);}
        }
        public async Task Release(){Assert.True(await Client.SetEnabledAsync(3,false));await UntilStatus("diagnostic:written:QueryWritten");}
        public async Task Cleanup(Task run)
        {
            Client.Cancel();if(child is {HasExited:false}){child.Kill();await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));}var error=await Record.ExceptionAsync(()=>run.WaitAsync(TimeSpan.FromSeconds(6)));Assert.True(error==null||error is OperationCanceledException or OleReceiverException or IOException,error?.GetType().Name);Assert.True(await Client.TryFinalizeAsync(true));await Client.DisposeAsync();
        }
        public void Dispose(){child?.Dispose();Directory.Delete(root,true);}
    }
    private sealed class Activation(Guid id):IOleActivationLease
    {public Guid Id{get;}=id;public string Name{get;}=OleReceiverWire.ActivationName(id);public void Set(long epoch,bool enabled){}public void Dispose(){}}
    private sealed class Registrar:IOleTargetRegistrar
    {public bool IsSettledAndRevoked=>true;public Task RegisterAsync(long hwnd,byte[] ownedPacket,CancellationToken cancellation){Array.Clear(ownedPacket);return Task.CompletedTask;}public ValueTask<bool> TryFinalizeAsync()=>ValueTask.FromResult(true);}
}

// Test synchronization only: a bounded wait for the parent's actual parsed record, never an inferred observation.
internal static class OleTargetReceipt
{
    public static async Task WaitAsync(OleReceiverClient client,OleTargetObservation expected)
    {
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(4));
        while(!client.TargetObservations.Contains(expected))
        {
            Assert.False(client.DiagnosticReadSettled,"Owned diagnostic EOF arrived without the required record.");
            await Task.Delay(5,deadline.Token);
        }
    }
}
