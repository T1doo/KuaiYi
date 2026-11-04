using KuaiYi.Core;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class FocusedReadingToolbarTests
{
    [Fact]public void NormalReadingKeepsOnlyStyleCopyAndOverflow()
    {
        var layout=OverlayReadingToolbar.Focused(372,420,true,false,false,1);
        Assert.Equal(new[]{"风格","复制","更多"},layout.Slots.Select(s=>s.Command));Assert.Equal(0,layout.ExtraHeightDip);
    }
    [Fact]public void GeneratingKeepsStopImmediatelyAvailable()
    {Assert.Contains(OverlayReadingToolbar.Focused(372,420,true,true,false,1).Slots,s=>s.Command=="停止");}
    [Theory][InlineData(88,240,2)][InlineData(172,220,2)][InlineData(372,420,1)][InlineData(20,80,2)]
    public void OverflowRemainsReachableWithoutClipping(double side,double width,double scale)
    {
        var layout=OverlayReadingToolbar.Focused(side,width,true,false,true,scale);
        Assert.Contains(layout.Slots,s=>s.Command=="更多");
        foreach(var slot in layout.Slots){Assert.True(slot.OffsetDip>=0);Assert.True(slot.OffsetDip+slot.WidthDip<=(slot.Row==0?side:width));}
    }
}
