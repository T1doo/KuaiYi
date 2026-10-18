using KuaiYi.App;
using KuaiYi.Core;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class ShellCloseGateTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ProductionClosingRevokesSendBeforeDeferredCleanupAndRetainsAnImmediateCancellationFailure(bool cancelThrows)
    {
        var w=new ShellWindow();w.InitializeResultFlow();var gate=w.MarkdownGate;var origin=new EndpointOrigin(new Uri("https://public-close.invalid"));
        gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));Assert.True(gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var cancellation));Assert.True(gate.CanSend(id,origin));
        using var registration=cancellation.Register(()=>{if(cancelThrows)throw new IOException("PUBLIC_CLOSE_CANCEL_FAULT");});
        try
        {
            w.RequestCloseForTest();Assert.True(w.ExitingForTest);Assert.NotNull(w.OriginalCloseForTest);Assert.False(w.OriginalCloseForTest.IsCompleted);Assert.Equal(0,w.SelectionExitCalls);
            Assert.Equal(Activation.Off,gate.Snapshot.Activation);Assert.True(cancellation.IsCancellationRequested);Assert.False(gate.CanSend(id,origin));
            int sends=0;Assert.False(gate.TryBeginSend(id,origin,()=>{sends++;return Task.FromResult(0);},out Task<int>? send));Assert.Null(send);Assert.Equal(0,sends);
        }
        finally{w.ReleaseCloseDispatchForTest();if(w.OriginalCloseForTest is {} original)await original.WaitAsync(TimeSpan.FromSeconds(5));}
        Assert.Equal(1,w.SelectionExitCalls);Assert.Equal(cancelThrows?1:0,w.ClearErrors);Assert.Equal(cancelThrows?0:1,w.ClosedForTest);Assert.Equal(cancelThrows?false:true,w.ExitingForTest);
    }
}
