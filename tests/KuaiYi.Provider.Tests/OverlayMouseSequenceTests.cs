using KuaiYi.Core;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class OverlayMouseSequenceTests
{
    [Theory][InlineData(false,100L,100L)][InlineData(true,100L,351L)][InlineData(true,101L,100L)]
    public void ForeignStaleOrFutureDownCannotBegin(bool hit,long time,long now)
    {var s=new OverlayMouseSequence();Assert.False(s.Accept(1,hit,time,now));Assert.False(s.Accept(2,true,now+1,now+1));}
    [Fact]public void ExternalHeldButtonCannotStartAtOurWindow()
    {var s=new OverlayMouseSequence();Assert.False(s.Accept(0,true,10,10));Assert.False(s.Accept(2,true,11,11));}
    [Fact]public void FreshPhysicalDownMovesOutsideOriginalWindowAndReleasesExactlyOnce()
    {var s=new OverlayMouseSequence();Assert.True(s.Accept(1,true,100,100));Assert.True(s.Accept(0,false,101,101));Assert.True(s.Accept(2,false,102,102));Assert.False(s.Accept(2,true,103,103));}
    [Fact]public void DuplicateDownCannotTurnOneDragIntoTwoClicks()
    {var s=new OverlayMouseSequence();Assert.True(s.Accept(1,true,100,100));Assert.False(s.Accept(1,true,101,101));Assert.True(s.Accept(2,true,102,102));}
    [Fact]public void PreviousGestureUpCannotReleaseNewGesture()
    {var s=new OverlayMouseSequence();Assert.True(s.Accept(1,true,100,100));Assert.True(s.Accept(2,true,101,101));Assert.True(s.Accept(1,true,200,200));Assert.False(s.Accept(2,true,150,201));Assert.True(s.Accept(2,true,202,202));}
    [Fact]public void ForeignRightDownCannotRetireLeftGesture()
    {var s=new OverlayMouseSequence();Assert.True(s.Accept(1,true,100,100));Assert.False(s.Accept(4,false,101,101));Assert.True(s.Accept(2,true,102,102));}
    [Fact]public void RightDownCancelsHeldGestureWithoutASecondLeftUp()
    {var s=new OverlayMouseSequence();Assert.True(s.Accept(1,true,100,100));Assert.True(s.Accept(4,true,101,101));Assert.False(s.Accept(2,true,102,102));Assert.True(s.Accept(1,true,103,103));}
    [Fact]public void RevokedGestureCannotBecomeClickOnLateUp()
    {var s=new OverlayMouseSequence();Assert.True(s.Accept(1,true,100,100));s.Cancel();Assert.False(s.Accept(2,true,101,101));Assert.True(s.Accept(1,true,102,102));}
    [Fact]public void ProducerLateUpCannotDisableNewDown()
    {var t=new OverlayMouseTracking();long a=t.Begin(100),b=t.Begin(200);Assert.NotEqual(a,b);Assert.False(t.Read(150,true,out _,out _));Assert.True(t.Active);Assert.True(t.Read(210,false,out var move,out _));Assert.Equal(b,move);Assert.True(t.Read(220,true,out var up,out _));Assert.Equal(b,up);Assert.False(t.Active);}
    [Fact]public void AmbiguousSameTickUpIsUnknownNotSuccess()
    {var t=new OverlayMouseTracking();t.Begin(100);Assert.False(t.Read(100,true,out _,out var unknown));Assert.True(unknown);Assert.True(t.Active);}
    [Fact]public void ProducerConsumerGapIsExplicitUnknownNotAHungMove()
    {var t=new OverlayMouseTracking();var s=new OverlayMouseSequence();long a=t.Begin(100);Assert.True(s.Accept(1,true,100,100,a));long b=t.Begin(200);Assert.False(s.Accept(1,true,200,200,b));Assert.True(s.OrderUnconfirmed);Assert.False(t.Read(150,true,out _,out _));Assert.True(t.Read(210,true,out var up,out _));Assert.False(s.Accept(2,true,210,210,up));s.Cancel();Assert.False(s.Accept(2,true,211,211,up));}
    [Theory][InlineData(false,200L)][InlineData(true,500L)]
    public void RejectedNewCaptureStillRevealsLostOriginalUp(bool hit,long now)
    {var t=new OverlayMouseTracking();var s=new OverlayMouseSequence();long a=t.Begin(100);Assert.True(s.Accept(1,true,100,100,a));long b=t.Begin(200);Assert.False(s.Accept(1,hit,200,now,b));Assert.True(s.OrderUnconfirmed);}
    [Fact]public void ConsumerRejectsEarlierCaptureEvenWithLaterTimestamp()
    {var s=new OverlayMouseSequence();Assert.True(s.Accept(1,true,100,100,7));Assert.False(s.Accept(2,true,101,101,6));Assert.True(s.Accept(2,true,102,102,7));}
    [Fact]public void AcceptedOriginalCoordinatesUseProductionDragThresholdAndPreserveAnchor()
    {var s=new OverlayMouseSequence();var g=new OverlayPointerGesture();var anchor=new PixelRect(100,100,148,148);Assert.True(s.Accept(1,true,100,100));Assert.Equal(OverlayPointerAction.Pressed,g.Down(new(110,112),anchor,true));Assert.True(s.Accept(0,false,101,101));Assert.Equal(OverlayPointerAction.Moved,g.Move(new(210,212),out var desired,4,4,new DpiScale(1)));Assert.Equal(new PixelPoint(200,200),desired);Assert.True(s.Accept(2,false,102,102));Assert.Equal(OverlayPointerAction.Released,g.Up());}
}
