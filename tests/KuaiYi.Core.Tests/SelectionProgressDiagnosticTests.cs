using KuaiYi.Contracts;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class SelectionProgressDiagnosticTests
{
    [Fact]public void FrozenTimeoutAndLastBatchSourcesRemainSeparate()
    {var p=SelectionProgressProjection.Project([4,1,2,3,1,230,255])!;Assert.Equal("BeforeRange",p.Checkpoint);Assert.Equal("Focus",p.TimeoutSource);Assert.Equal("Hit",p.BatchSource);Assert.Equal(3,p.RangeTotal);Assert.Equal(1,p.RangeCompleted);Assert.Equal(25,p.TextDurationMs);}
    [Theory][InlineData(65,1)][InlineData(1,2)][InlineData(-1,0)]
    public void InvalidCountsAreUnknownNotClamped(int total,int completed)
    {var p=SelectionProgressProjection.Project([3,0,1,total,completed,null,null])!;Assert.Equal("Unknown",p.CountState);Assert.Null(p.RangeTotal);Assert.Null(p.RangeCompleted);}
    [Theory][InlineData(-1)][InlineData(601)][InlineData(long.MaxValue)]
    public void TextTimesOutsideBoundsBecomeOnlyOverLimit(long tick)
    {var p=SelectionProgressProjection.Project([4,1,1,2,1,200,tick])!;Assert.Equal("OverLimit",p.TextState);Assert.Null(p.TextStartMs);Assert.Null(p.TextEndMs);Assert.Null(p.TextDurationMs);}
    [Fact]public void NullStartedMalformedAndUnknownEnumsCannotCreatePrivateOutput()
    {Assert.Equal("NotRun",SelectionProgressProjection.Project([1,0,0,null,null,null,null])!.TextState);Assert.Equal("Started",SelectionProgressProjection.Project([0,0,1,1,0,1,null])!.TextState);Assert.Null(SelectionProgressProjection.Project([99,0,0,null,null,null,null]));Assert.Null(SelectionProgressProjection.Project([0,0,99,null,null,null,null]));Assert.Null(SelectionProgressProjection.Project([0,1,1,1,1,1,2]));Assert.Null(SelectionProgressProjection.Project([0,0,1,1,1,2,1]));Assert.Null(SelectionProgressProjection.Project(new long?[8]));}
}
