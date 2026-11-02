using KuaiYi.Core;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class FrontendControlEdgeTests
{
    [Theory]
    [InlineData(320,136,1.25)][InlineData(320,136,2)][InlineData(320,272,2.25)]
    [InlineData(420,372,1.25)][InlineData(420,372,2)][InlineData(600,552,2.25)]
    public void IndependentTextScaleKeepsCriticalActionsBoundedAcrossAnchorAndFullRows(double width,double side,double scale)
    {
        var plan=OverlayReadingToolbar.Responsive(side,width,true,true,true,scale);
        Assert.Contains(plan.Slots,s=>s.Command=="停止");Assert.Contains(plan.Slots,s=>s.Command=="收起");Assert.Contains(plan.Slots,s=>s.Command=="更多");
        foreach(var group in plan.Slots.GroupBy(slot=>slot.Row))
        {
            var row=group.OrderBy(slot=>slot.OffsetDip).ToArray();
            for(int i=0;i<row.Length;i++)
            {Assert.True(row[i].WidthDip>=44);Assert.True(row[i].OffsetDip+row[i].WidthDip<=(group.Key==0?side:width));if(i>0)Assert.True(row[i].OffsetDip>=row[i-1].OffsetDip+row[i-1].WidthDip);}
        }
        Assert.Equal(plan.Slots.Max(slot=>slot.Row)*48,plan.ExtraHeightDip);
    }
    [Fact]
    public void EnlargedAuxiliaryRowsDoNotCollapseAfterAnimationOrMoveAndReanchor()
    {
        var monitor=new MonitorGeometry("synthetic",new(0,0,1920,1080),new(1));
        var frames=new OverlayFrames(new(100,100,148,148),monitor);
        frames.OpenToolbar(220,144);frames.Tick(TimeSpan.FromMilliseconds(220));var settled=frames.Current;
        frames.Tick(TimeSpan.FromMilliseconds(70));Assert.Equal(settled,frames.Current);Assert.Equal(144,frames.Current.Height);
        frames.Reanchor(new(200,200,248,248),monitor,new());frames.Tick(TimeSpan.FromMilliseconds(220));
        Assert.Equal(220,frames.Current.Width);Assert.Equal(144,frames.Current.Height);
        frames.BeginMove();frames.Move(new(300,300),monitor);frames.EndMove(new());frames.Tick(TimeSpan.FromMilliseconds(220));
        Assert.Equal(220,frames.Current.Width);Assert.Equal(144,frames.Current.Height);Assert.True(frames.Current.Contains(frames.Anchor));
    }
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
