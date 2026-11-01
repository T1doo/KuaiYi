using KuaiYi.App;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class OverlayInputLifetimeTests
{
    [Fact]public async Task ClosingCannotFinishWhileOriginalInputOwnerIsHeld()
    {
        var w=new ShellWindow();w.InitializeResultFlow();var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int calls=0;
        w.InputOwnerForTest(()=>{calls++;entered.SetResult();return release.Task;});var close=w.ExitContentForTest();await entered.Task;Assert.False(close.IsCompleted);Assert.Equal(1,calls);release.SetResult();await close;Assert.Equal(1,w.TranslationExitCalls);
    }
    [Fact]public async Task InputFailureDoesNotSkipOtherOriginalContentOwners()
    {
        var w=new ShellWindow();w.InitializeResultFlow();var error=new IOException("PUBLIC_INPUT_OWNER_UNCONFIRMED");w.InputOwnerForTest(()=>Task.FromException(error));
        var failure=await Assert.ThrowsAsync<AggregateException>(()=>w.ExitContentForTest());Assert.Contains(error,failure.Flatten().InnerExceptions);Assert.Equal(1,w.SelectionExitCalls);Assert.Equal(1,w.TranslationExitCalls);Assert.Equal(1,w.HotkeyExitCalls);
    }
}
