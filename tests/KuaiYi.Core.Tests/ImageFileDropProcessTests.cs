using System.Diagnostics;
using System.Runtime.InteropServices;
using KuaiYi.Core;
using KuaiYi.OleTransfer;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class ImageFileDropProcessTests
{
    private sealed class Starter(string mode):IOwnedProcessStarter{internal Process? Child;public Process Start(ProcessStartInfo info){info.ArgumentList.Add(mode);return Child=SystemOwnedProcessStarter.Instance.Start(info);}}
    private sealed class Activation(Guid id):IOleActivationLease{public string Name=>OleReceiverWire.ActivationName(id);public void Set(long epoch,bool on){}public void Dispose(){}}
    private sealed class Registrar:IOleTargetRegistrar{public bool IsSettledAndRevoked=>true;public Task RegisterAsync(long hwnd,byte[] packet,CancellationToken token){Array.Clear(packet);return Task.CompletedTask;}public ValueTask<bool> TryFinalizeAsync()=>ValueTask.FromResult(true);}
    private static string Worker()=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tools/KuaiYi.OleReceiverFixture/bin",new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name,"net10.0/KuaiYi.OleReceiverFixture.dll"));
    private static async Task Settle(OleReceiverClient client,Task run)
    {client.Cancel();_=await Record.ExceptionAsync(()=>run.WaitAsync(TimeSpan.FromSeconds(5)));Assert.True(client.ObserveExit());await client.DeliverySettlementForTest.WaitAsync(TimeSpan.FromSeconds(5));await client.RevocationSettlementForTest.WaitAsync(TimeSpan.FromSeconds(5));if(client.LateReadObserverForTest is {} late)await late.WaitAsync(TimeSpan.FromSeconds(5));Assert.True(await client.TryFinalizeAsync(true));await client.DisposeAsync();}
    [Theory][InlineData("image-normal",true)][InlineData("image-disabled",false)][InlineData("image-no-callback",true)][InlineData("image-hover-only",true)][InlineData("image-multi",true)][InlineData("image-url",true)][InlineData("image-missing-cleanup",true)]
    public async Task ActualOwnedProtocolSeparatesFilePathFromTextAndRequiresFrozenOptinAndCleanup(string mode,bool enabled)
    {
        Guid id=Guid.NewGuid();var activation=new Activation(id);var starter=new Starter(mode);var client=new OleReceiverClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,new Registrar(),activation,Worker(),starter);var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var query=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var dropped=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int texts=0,files=0;byte[]? actual=null;string? path=null;
        var run=client.RunAsync(new(id,1,Environment.ProcessId,1,new byte[32],activation.Name,ProductionTextDrop:true,AllowImageFiles:enabled),e=>{if(e.Phase=="Ready")ready.TrySetResult();if(e.Phase=="QueryEnd")query.TrySetResult();if(e.Phase=="DropEnd")dropped.TrySetResult();},(_,_,_,_)=>{texts++;return Task.CompletedTask;},deliverImageFile:mode=="image-no-callback"?null:(_,_,bytes,_)=>{files++;Assert.True(MemoryMarshal.TryGetArray(bytes,out var array));actual=array.Array;path=OleImageFileDrop.DecodeTransport(bytes.Span);return Task.CompletedTask;});
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.True(await client.SetEnabledAsync(1,true,new(new(0,0,48,48),new(0,0,48,48),24),[]));
            if(mode=="image-normal"){await dropped.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(1,files);Assert.Equal("C:\\public\\image.GIF",path);Assert.NotNull(actual);Assert.All(actual,b=>Assert.Equal(0,b));}
            else if(mode=="image-hover-only"){await query.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.True(client.IsReady);Assert.Equal(0,files);}
            else{Assert.NotNull(await Record.ExceptionAsync(()=>run.WaitAsync(TimeSpan.FromSeconds(5))));Assert.Equal(0,files);}
            Assert.Equal(0,texts);
        }
        finally{await Settle(client,run);}
    }
    [Fact]public async Task CancelledTypedDeliveryRetainsItsActualPathBytesUntilOriginalCallbackSettles()
    {
        Guid id=Guid.NewGuid();var activation=new Activation(id);var client=new OleReceiverClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!,new Registrar(),activation,Worker(),new Starter("image-normal"));var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);byte[]? actual=null;
        var run=client.RunAsync(new(id,1,Environment.ProcessId,1,new byte[32],activation.Name,ProductionTextDrop:true,AllowImageFiles:true),e=>{if(e.Phase=="Ready")ready.TrySetResult();},(_,_,_,_)=>throw new InvalidOperationException("Must not route file to text."),deliverImageFile:async(_,_,bytes,_)=>{MemoryMarshal.TryGetArray(bytes,out var a);actual=a.Array;entered.SetResult();await release.Task;});
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));await client.SetEnabledAsync(1,true,new(new(0,0,48,48),new(0,0,48,48),24),[]);await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));client.Cancel();Assert.NotNull(await Record.ExceptionAsync(()=>run.WaitAsync(TimeSpan.FromSeconds(5))));Assert.False(await client.TryFinalizeAsync());Assert.Contains(actual!,b=>b!=0);release.SetResult();await client.DeliverySettlementForTest.WaitAsync(TimeSpan.FromSeconds(5));Assert.All(actual!,b=>Assert.Equal(0,b));
        }
        finally{release.TrySetResult();await Settle(client,run);}
    }
    [Fact]public void FileOptinCannotBeUsedOutsideTheProductionProtocol(){var id=Guid.NewGuid();Assert.Throws<InvalidDataException>(()=>OleReceiverWire.Validate(new(id,1,1,1,new byte[32],OleReceiverWire.ActivationName(id),AllowImageFiles:true)));}
}
