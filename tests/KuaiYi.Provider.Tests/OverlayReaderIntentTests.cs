using KuaiYi.App;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class OverlayReaderIntentTests
{
    [Fact] public void ObservedInputPausesBeforeLayoutOrScrollChanged()
    {
        var window=new ShellWindow();window.ObserveReaderInput();
        Assert.False(window.Following);Assert.Equal(0,window.ScrollCalls);
    }
    [Fact] public void ReadingWithConcurrentExtentChangeUsesFinalGeometry()
    {
        var window=new ShellWindow();window.ChangeGeometry(1200,200,1000);window.ObserveReaderInput();
        window.ChangeGeometry(1500,260,120);window.Deliver();
        Assert.False(window.Following);Assert.True(window.NewContentVisible);Assert.Equal(0,window.ScrollCalls);
    }
    [Theory] [InlineData(976,true)] [InlineData(975,false)]
    public void ReaderReturnsToBottomUsingExistingThreshold(double offset,bool following)
    {
        var window=new ShellWindow();window.ObserveReaderInput();window.ChangeGeometry(1200,200,offset);window.Deliver();
        Assert.Equal(following,window.Following);Assert.Equal(!following,window.NewContentVisible);
    }
    [Fact] public void InterveningFlushConservativelyKeepsReaderPaused()
    {
        var window=new ShellWindow();window.ObserveReaderInput();window.QueueScroll(false,120);
        window.ChangeGeometry(1200,200,1000);window.Deliver();
        Assert.False(window.Following);window.Deliver();Assert.Equal(120,window.ScrollOffset);
    }
    [Fact] public void HideThenNewResultCannotBeResumedByPriorReaderObservation()
    {
        var window=new ShellWindow();window.ObserveReaderInput();window.HideOrInvalidate();window.ReplaceResult();
        window.ChangeGeometry(1200,200,1000);window.Deliver();Assert.False(window.Following);
    }
}
