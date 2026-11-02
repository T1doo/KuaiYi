using KuaiYi.Core;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class FrontendControlEdgeTests
{
    [Theory]
    [InlineData(320,false)][InlineData(420,false)][InlineData(600,false)]
    [InlineData(320,true)][InlineData(420,true)][InlineData(600,true)]
    public void ReadingKeepsStopCollapseAndOverflowDirectAndHitAreasDisjoint(double width,bool touch)
    {
        var slots=OverlayReadingToolbar.Plan(width-48,true,true,touch);
        Assert.Contains(slots,s=>s.Command=="停止");Assert.Contains(slots,s=>s.Command=="收起");Assert.Contains(slots,s=>s.Command=="更多");
        Assert.DoesNotContain(slots,s=>s.Command=="重新生成");
        for(int i=0;i<slots.Count;i++)
        {Assert.True(slots[i].WidthDip>=(touch?44:28));Assert.True(slots[i].OffsetDip+slots[i].WidthDip<=width-48);if(i>0)Assert.True(slots[i].OffsetDip>=slots[i-1].OffsetDip+slots[i-1].WidthDip);}
    }
    [Fact]
    public void CompactReopenDoesNotTurnTheAnchorIntoASecondCommand()
    {
        var commands=OverlayReadingToolbar.Plan(172,false,false).Select(s=>s.Command);
        Assert.Equal(new[]{"回看","设置","更多"},commands);
        var reading=OverlayReadingToolbar.Plan(372,true,false).Select(s=>s.Command);
        Assert.Contains("重新生成",reading);Assert.DoesNotContain("停止",reading);
    }
    [Fact]
    public void NarrowTouchControlsMoveSecondaryActionsIntoMore()
    {
        var commands=OverlayReadingToolbar.Plan(272,true,true,true).Select(s=>s.Command);
        Assert.Contains("风格",commands);Assert.DoesNotContain("复制",commands);Assert.DoesNotContain("设置",commands);
    }
}
