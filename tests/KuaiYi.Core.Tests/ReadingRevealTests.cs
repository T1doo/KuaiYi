using KuaiYi.Core;
using System.Globalization;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class ReadingRevealTests
{
    [Theory][InlineData(2000)][InlineData(100000)]
    public void LargeBurstHasNoExponentialDisplayTail(int length)
    {
        var reveal=new ReadingReveal();var id=Guid.NewGuid();string text=new('中',length);
        Assert.Equal(1,reveal.Advance(id,text,0,false));
        Assert.Equal(length,reveal.Advance(id,text,.198,false));
    }
    [Fact]public void ContinuedArrivalDoesNotResetOldestPendingDeadline()
    {
        var reveal=new ReadingReveal();var id=Guid.NewGuid();reveal.Advance(id,new string('中',2000),0,false);
        int intermediate=reveal.Advance(id,new string('中',4000),.05,false);Assert.True(intermediate<4000);
        Assert.Equal(6000,reveal.Advance(id,new string('中',6000),.198,false));
    }
    [Fact]public void DispatcherStallCatchesUpAtFirstCallback()
    {var reveal=new ReadingReveal();var id=Guid.NewGuid();reveal.Advance(id,"long received text",0,false);Assert.Equal(18,reveal.Advance(id,"long received text",.5,false));}
    [Fact]public void BurstsProgressWithoutSplittingUnicodeTextElements()
    {
        var reveal=new ReadingReveal();var id=Guid.NewGuid();string text="你👩‍💻好e\u0301世界，再见。";
        var boundaries=StringInfo.ParseCombiningCharacters(text).Append(text.Length).ToArray();int previous=0;
        for(int i=0;i<40;i++){int count=reveal.Advance(id,text,i/30d,false);Assert.Contains(count,boundaries);Assert.InRange(count,previous,text.Length);previous=count;}
        Assert.Equal(text.Length,previous);
    }
    [Fact]public void ReducedMotionAndStoppingCanImmediatelyExposeAllAcceptedText()
    {var reveal=new ReadingReveal();var id=Guid.NewGuid();Assert.Equal(1,reveal.Advance(id,"abcdef",0,false));Assert.Equal(6,reveal.Advance(id,"abcdef",.01,true));}
    [Fact]public void NewVersionAndResetCannotInheritOldPosition()
    {var reveal=new ReadingReveal();reveal.Advance(Guid.NewGuid(),"old content",0,true);var next=Guid.NewGuid();Assert.Equal(1,reveal.Advance(next,"new text",1,false));reveal.Reset();Assert.Equal(1,reveal.Advance(next,"new text",2,false));}
    [Fact]public void IdleTimeDoesNotBuildCreditForFutureBurst()
    {var reveal=new ReadingReveal();var id=Guid.NewGuid();reveal.Advance(id,"a",0,false);reveal.Advance(id,"a",10,false);Assert.InRange(reveal.Advance(id,"abcdefghijklmnopqrstuvwxyz",10.01,false),1,3);}
}
