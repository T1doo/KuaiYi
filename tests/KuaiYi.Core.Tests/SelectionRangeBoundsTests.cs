using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class SelectionRangeBoundsTests
{
    private static readonly SelectionRangeRectangle Empty=new(double.PositiveInfinity,double.PositiveInfinity,double.NegativeInfinity,double.NegativeInfinity);
    [Fact]public void Managed_empty_sentinel_is_absent_geometry_without_losing_original_count()
    {
        List<double> output=[];int observed=0;
        Assert.True(SelectionRangeBounds.TryAppend([Empty,new(1,2,3,4),Empty],output,ref observed));
        Assert.Equal(3,observed);Assert.Equal(new double[]{1,2,3,4},output);
    }
    [Theory]
    [InlineData(double.NaN,0,1,1)]
    [InlineData(double.PositiveInfinity,0,1,1)]
    [InlineData(double.NegativeInfinity,double.PositiveInfinity,double.NegativeInfinity,double.NegativeInfinity)]
    [InlineData(0,0,-1,1)]
    [InlineData(0,0,1,-1)]
    public void Malformed_tail_does_not_commit_partial_geometry(double x,double y,double width,double height)
    {
        List<double> output=[10,20,30,40];int observed=1;
        Assert.False(SelectionRangeBounds.TryAppend([new(1,2,3,4),new(x,y,width,height)],output,ref observed));
        Assert.Equal(1,observed);Assert.Equal(new double[]{10,20,30,40},output);
    }
    [Fact]public void Empty_rectangles_still_exhaust_aggregate_limit_across_ranges()
    {
        List<double> output=[];int observed=0;
        Assert.True(SelectionRangeBounds.TryAppend(Enumerable.Repeat(Empty,128),output,ref observed));
        Assert.True(SelectionRangeBounds.TryAppend(Enumerable.Repeat(Empty,128),output,ref observed));
        Assert.Equal(256,observed);Assert.False(SelectionRangeBounds.TryAppend([Empty],output,ref observed));Assert.Equal(256,observed);Assert.Empty(output);
        observed=0;Assert.False(SelectionRangeBounds.TryAppend(Enumerable.Repeat(Empty,257),output,ref observed));Assert.Equal(0,observed);
    }
    [Fact]public void Null_array_is_rejected_and_absent_geometry_cannot_authorize_automatic_selection()
    {
        List<double> output=[];int observed=0;Assert.False(SelectionRangeBounds.TryAppend(null,output,ref observed));
        var config=new SettingsSnapshot(new AppSettings(),"public-settings",SettingsLoadState.Current);
        var source=new SelectionWindow(123,456,789,"public.exe");
        var result=new SelectionCaptureResult(SelectionCaptureStatus.Success,"public word",[],[12,4],new(0,0,500,200));
        var automatic=new SelectionGesture(source,new(3,3),new(70,3),0,1);
        Assert.Null(SelectionTextPolicy.Snapshot(automatic,result,config));
        Assert.NotNull(SelectionTextPolicy.Snapshot(automatic with{Explicit=true},result,config));
    }
}
