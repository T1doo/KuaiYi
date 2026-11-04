using KuaiYi.App;
using KuaiYi.Core;
using Xunit;

namespace KuaiYi.Provider.Tests;

public sealed class OverlayUnreadTests
{
    private static async Task<(ShellWindow Window,RequestIdentity Identity)> Ready()
    {
        var window=new ShellWindow();window.InitializeResultFlow();
        var origin=new EndpointOrigin(new Uri("https://public.invalid"));window.MarkdownGate.ConsentTo(origin);
        Assert.True(window.MarkdownGate.TryEnable(origin,true));
        Assert.True(window.MarkdownGate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var id,out _));
        Assert.True(await window.ReceivePresentationAsync(id,new(OverlayResultEventKind.Begin)));
        Assert.True(await window.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"public first paragraph")));
        window.TickForTest();window.DrainResultDispatchForTest();return(window,id);
    }
    [Fact]public async Task SourceDisclosureWhileReadingDoesNotClaimNewGeneratedText()
    {
        var (window,_)=await Ready();window.ReaderScroll(120);
        window.SourceDisclosureChangedForTest();window.TickForTest();
        Assert.False(window.NewContentVisible);
    }
    [Fact]public async Task NewDeltaWhileReadingShowsBadgeAndExplicitFollowClearsIt()
    {
        var (window,id)=await Ready();window.ReaderScroll(120);
        Assert.True(await window.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta," appended output")));
        window.TickForTest();Assert.True(window.NewContentVisible);
        Assert.Equal("有新内容",window.UnreadNoticeForTest);
        window.ReaderFollowEnd();Assert.False(window.NewContentVisible);Assert.True(window.Following);
    }
    [Fact]public async Task SourceDisclosureDoesNotClearAnExistingUnreadDelta()
    {
        var (window,id)=await Ready();window.ReaderScroll(120);
        Assert.True(await window.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta," unread output")));
        window.TickForTest();Assert.True(window.NewContentVisible);
        window.SourceDisclosureChangedForTest();window.TickForTest();Assert.True(window.NewContentVisible);
    }
    [Fact]public async Task TerminalStateWhileReadingHasAnAccurateStatusLabel()
    {
        var (window,_)=await Ready();window.ReaderScroll(120);window.StopForTest();window.TickForTest();
        Assert.True(window.NewContentVisible);Assert.Equal("状态已更新",window.UnreadNoticeForTest);
        window.SourceDisclosureChangedForTest();window.TickForTest();Assert.Equal("状态已更新",window.UnreadNoticeForTest);
    }
}
