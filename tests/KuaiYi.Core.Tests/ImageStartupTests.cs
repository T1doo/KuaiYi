using System.Diagnostics;
using KuaiYi.Core;
using KuaiYi.Imaging;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class ImageStartupTests
{
    private sealed class DelayStarter : IImageProcessStarter, IDisposable
    {
        private readonly ManualResetEventSlim release=new(false);
        public TaskCompletionSource<int> Entered {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<Process> Created {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Fail {get;init;}
        public Process Start(ProcessStartInfo info)
        {
            Entered.SetResult(Environment.CurrentManagedThreadId);
            if(!release.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("public startup fixture");
            if(Fail){info.FileName="kuaiyi-nonexistent-public-fixture";}
            var child=SystemImageProcessStarter.Instance.Start(info);Created.SetResult(child);return child;
        }
        public void Release()=>release.Set();
        public void Dispose(){release.Set();release.Dispose();}
    }
    private sealed class UiQueue : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback,object?)> pending=[];
        public override void Post(SendOrPostCallback callback,object? state)=>pending.Enqueue((callback,state));
        public void Drain(){while(pending.TryDequeue(out var item))item.Item1(item.Item2);}
    }
    private static ImageWorkerClient Client(DelayStarter starter)
    {
        string configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        string assembly=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../src/KuaiYi.ImageWorker/bin",configuration,"net10.0/win-x64/KuaiYi.ImageWorker.dll"));
        return new(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",assembly,starter);
    }
    private static byte[] Fixture()=>File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"kuaiyi-test.png"));
    private static ImageJob Job(byte[] image)=>new(Guid.NewGuid(),1,1,"en-US",image.Length);
    private static async Task WaitConfirmed(ImageWorkerClient client)
    {
        var deadline=DateTime.UtcNow.AddSeconds(5);while(!client.ObserveExit()&&DateTime.UtcNow<deadline)await Task.Delay(10);Assert.True(client.ObserveExit());Assert.Equal(HostExitState.ExitConfirmed,client.ExitState);
    }
    [Fact] public async Task ActualStartBoundaryIsOffCallerContextAndUiQueueCanContinueWhileDelayed()
    {
        using var starter=new DelayStarter();await using var client=Client(starter);var queue=new UiQueue();var bytes=Fixture();
        var returned=new TaskCompletionSource<(Task<ImageReply> Run,int UiThread,int Dispatches)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var uiThread=new Thread(()=>
        {
            SynchronizationContext.SetSynchronizationContext(queue);
            try
            {
                var pending=client.RunAsync(bytes,Job(bytes));
                if(!starter.Entered.Task.Wait(TimeSpan.FromSeconds(3)))throw new TimeoutException("public startup did not enter");
                int dispatches=0;queue.Post(_=>dispatches++,null);queue.Drain();
                returned.SetResult((pending,Environment.CurrentManagedThreadId,dispatches));
            }
            catch(Exception error){returned.TrySetException(error);}
            finally{SynchronizationContext.SetSynchronizationContext(null);}
        }){IsBackground=true,Name="KuaiYi portable test UI thread"};
        uiThread.Start();
        try
        {
            var ready=await returned.Task.WaitAsync(TimeSpan.FromSeconds(4));
            Assert.NotEqual(ready.UiThread,await starter.Entered.Task);Assert.Equal(1,ready.Dispatches);Assert.False(ready.Run.IsCompleted);
            starter.Release();var reply=await ready.Run.WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(OperatingSystem.IsWindows()?ImageFailure.IdentityMissing:ImageFailure.UnsupportedPlatform,reply.Failure);Assert.True(client.ObserveExit());
        }
        finally{starter.Release();Assert.True(uiThread.Join(TimeSpan.FromSeconds(2)));}
    }
    [Fact] public async Task CancelDuringActualStartRetainsPendingOwnershipAndKillsLateRealChild()
    {
        using var starter=new DelayStarter();var client=Client(starter);using var cancel=new CancellationTokenSource();var bytes=Fixture();var pending=client.RunAsync(bytes,Job(bytes),cancel.Token);
        try
        {
            await starter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));cancel.Cancel();
            var failure=await Assert.ThrowsAsync<ImageJobException>(()=>pending.WaitAsync(TimeSpan.FromSeconds(2)));Assert.Equal(ImageFailure.ExitUnconfirmed,failure.Code);Assert.False(client.ObserveExit());Assert.Equal(HostExitState.ExitUnconfirmed,client.ExitState);
            Assert.Equal(ImageFailure.ExitUnconfirmed,(await Assert.ThrowsAsync<ImageJobException>(()=>client.DisposeAsync().AsTask())).Code);
            starter.Release();var child=await starter.Created.Task.WaitAsync(TimeSpan.FromSeconds(3));await WaitConfirmed(client);Assert.True(child.HasExited);await client.DisposeAsync();
        }finally{starter.Release();await WaitConfirmed(client);await client.DisposeAsync();}
    }
    [Fact] public async Task CancelledPendingStartThatLaterFailsIsObservedAndSettledWithoutInventingChild()
    {
        using var starter=new DelayStarter{Fail=true};var client=Client(starter);using var cancel=new CancellationTokenSource();var bytes=Fixture();var pending=client.RunAsync(bytes,Job(bytes),cancel.Token);
        try
        {
            await starter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));cancel.Cancel();Assert.Equal(ImageFailure.ExitUnconfirmed,(await Assert.ThrowsAsync<ImageJobException>(()=>pending.WaitAsync(TimeSpan.FromSeconds(2)))).Code);
            Assert.False(client.ObserveExit());starter.Release();await WaitConfirmed(client);Assert.False(starter.Created.Task.IsCompleted);Assert.NotNull(client.StartupFailureCode);await client.DisposeAsync();
        }finally{starter.Release();await WaitConfirmed(client);await client.DisposeAsync();}
    }
}
