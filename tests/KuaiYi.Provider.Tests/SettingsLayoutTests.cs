using KuaiYi.App;
using Xunit;

namespace KuaiYi.Provider.Tests;

public sealed class SettingsLayoutTests
{
    [Fact]
    public void NarrowPreferenceReservesBothRowsInsteadOfClippingTheInput()
    {
        var plan=SettingsLayoutPlan.Preference(231,13,180);
        Assert.True(plan.Stacked);
        Assert.Equal(231,plan.LabelWidth);Assert.Equal(231,plan.InputWidth);
        Assert.Equal(94,plan.Height(46,40)); // wrapped labels + gap + original input
    }
    [Fact]
    public void ActualEnlargedFontAndTemplateWidthBothParticipateInReflow()
    {
        Assert.False(SettingsLayoutPlan.Preference(500,13,180).Stacked);
        var enlarged=SettingsLayoutPlan.Preference(500,26,340);
        Assert.True(enlarged.Stacked);Assert.Equal(500,enlarged.InputWidth);
        var wideTemplate=SettingsLayoutPlan.Preference(500,13,350);
        Assert.True(wideTemplate.Stacked);
        var room=SettingsLayoutPlan.Preference(920,26,390);
        Assert.False(room.Stacked);Assert.Equal(390,room.InputWidth);
        Assert.Equal(920,room.LabelWidth+room.Gap+room.InputWidth);
    }
    [Fact]
    public void ExactColumnBoundaryAndReturningToWideDoNotUsePreviousLayoutState()
    {
        var before=SettingsLayoutPlan.Preference(360,13,180);
        Assert.False(before.Stacked);Assert.Equal(160,before.LabelWidth);
        Assert.True(SettingsLayoutPlan.Preference(359.5,13,180).Stacked);
        Assert.Equal(before,SettingsLayoutPlan.Preference(360,13,180));
    }
    [Fact]
    public void CollapsedChildrenDoNotReserveGapsOrMissingRowHeight()
    {
        var onlyLabels=SettingsLayoutPlan.Preference(210,26,0,input:false);
        Assert.Equal(210,onlyLabels.LabelWidth);Assert.Equal(0,onlyLabels.Gap);
        Assert.Equal(80,onlyLabels.Height(80,100));
        var onlyInput=SettingsLayoutPlan.Preference(210,26,180,labels:false);
        Assert.Equal(210,onlyInput.InputWidth);Assert.Equal(0,onlyInput.Gap);
        Assert.Equal(60,onlyInput.Height(100,60));
        Assert.Equal(0,SettingsLayoutPlan.Preference(0,13,0,false,false).Height(0,0));
    }
    [Fact]
    public void LongFeedbackCannotConsumeThePageOrNavigationViewport()
    {
        var narrow=SettingsLayoutPlan.Chrome(280,260,26,2000);
        Assert.True(narrow.Compact);
        Assert.Equal(91,narrow.FooterHeight);Assert.Equal(62.4,narrow.NavigationHeight,6);
        Assert.Equal(106.6,narrow.MainHeight,6);
        Assert.Equal(260,narrow.NavigationHeight+narrow.MainHeight+narrow.FooterHeight,6);
        var wide=SettingsLayoutPlan.Chrome(940,680,13,2000);
        Assert.False(wide.Compact);Assert.Equal(748,wide.MainWidth);
        Assert.Equal(442,wide.MainHeight);Assert.Equal(238,wide.FooterHeight,6);
    }
    [Fact]
    public void CompactExtentCoversExistingHardMinimaWithPaddingAndScrollBar()
    {
        // Profiles 200 + compact page margins 32 + typical vertical scrollbar 17.
        // Recovery 200 + its margin 16 + footer padding 32 + scrollbar 17.
        Assert.True(SettingsLayoutPlan.MinimumWidth(13,17)>=200+32+17);
        Assert.True(SettingsLayoutPlan.MinimumWidth(13,17)>=200+16+32+17);
        Assert.Equal(560,SettingsLayoutPlan.MinimumWidth(26,17));
        Assert.Equal(308,SettingsLayoutPlan.MinimumWidth(13,60));
        Assert.Equal(280,SettingsLayoutPlan.MinimumWidth(13,double.NaN));
        Assert.True(SettingsLayoutPlan.Chrome(940,680,26,100).Compact);
    }
    [Fact]
    public void ZeroAndUnboundedMeasureInputsNeverProduceInvalidGeometry()
    {
        foreach(double width in new[]{0,1,180,280,616,940,double.PositiveInfinity,double.NaN})
        foreach(double height in new[]{0,1,200,740,double.PositiveInfinity})
        foreach(double font in new[]{13,26})
        {
            var row=SettingsLayoutPlan.Preference(width,font,double.PositiveInfinity);
            foreach(double value in new[]{row.Width,row.LabelWidth,row.InputWidth,row.Gap,row.Height(80,60)})
                Assert.True(double.IsFinite(value)&&value>=0);
            if(!row.Stacked)Assert.True(row.LabelWidth+row.Gap+row.InputWidth<=row.Width);
            var chrome=SettingsLayoutPlan.Chrome(width,height,font,10000);
            foreach(double value in new[]{chrome.Width,chrome.Height,chrome.MainWidth,chrome.MainHeight,chrome.NavigationHeight,chrome.FooterHeight})
                Assert.True(double.IsFinite(value)&&value>=0);
            Assert.Equal(chrome.Height,chrome.MainHeight+chrome.NavigationHeight+chrome.FooterHeight,6);
        }
    }
}
