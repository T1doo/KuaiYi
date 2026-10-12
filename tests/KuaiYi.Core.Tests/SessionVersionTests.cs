using KuaiYi.Core;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class SessionVersionTests
{
    private static RuntimeGate Gate(){var gate=new RuntimeGate();var endpoint=new EndpointOrigin(new Uri("https://fixture.invalid"));gate.ConsentTo(endpoint);Assert.True(gate.TryEnable(endpoint,true));return gate;}
    private static RequestIdentity Start(RuntimeGate gate,Guid session){Assert.True(gate.TryStartRequest(session,Guid.NewGuid(),null,out var identity,out _));return identity;}
    [Fact] public void SameSourceFourthBeginRetainsOnlyThreeActualVersionReferences()
    {
        var gate=Gate();var results=new OverlayResults(gate);var session=Guid.NewGuid();
        for(int i=0;i<3;i++){var id=Start(gate,session);results.Begin(id);results.Append(id,"PUBLIC"+i);results.Complete(id);}
        var fourth=Start(gate,session);results.Begin(fourth);Assert.Equal(3,results.Versions.Count);Assert.Equal(3,results.RetainedVersionIds.Count);
    }

    [Theory] [InlineData(4)] [InlineData(8)] [InlineData(20)]
    public void OrdinalsRemainMonotonicWhenOldestVersionsAreEvicted(int count)
    {
        var gate=Gate();var results=new OverlayResults(gate);var session=Guid.NewGuid();
        for(int i=1;i<=count;i++){Assert.Equal(i,results.NextVersionNumber(session));var id=Start(gate,session);results.Begin(id);Assert.Equal(i,results.Versions.Single(v=>v.Identity==id).Number);results.Append(id,"PUBLIC"+i);results.Complete(id);Assert.InRange(results.RetainedVersionIds.Count,1,3);}
        Assert.Equal(new[]{count-2,count-1,count},results.Versions.Select(v=>v.Number));Assert.Equal(count+1,results.NextVersionNumber(session));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void NewSourceFailureRestoresOldDisplayAndKeepsItsSessionSequence(bool selectOld)
    {
        var gate=Gate();var results=new OverlayResults(gate);var session=Guid.NewGuid();
        for(int i=1;i<=3;i++){var id=Start(gate,session);results.Begin(id);results.Append(id,"PUBLIC"+i);results.Complete(id);}
        if(selectOld)Assert.True(results.SelectVersion(results.Versions[0].Identity.VersionId));var old=results.SelectedVersionId;
        var failed=Start(gate,Guid.NewGuid());results.Begin(failed);Assert.Equal(1,results.Versions[0].Number);results.Fail(failed);Assert.Equal(old,results.SelectedVersionId);Assert.Equal(4,results.NextVersionNumber(session));
        var fourth=Start(gate,session);results.Begin(fourth);Assert.Equal(4,results.Versions.Single(v=>v.Identity==fourth).Number);results.Fail(fourth);Assert.Equal(old,results.SelectedVersionId);Assert.InRange(results.RetainedVersionIds.Count,1,3);
    }
    [Fact] public void BrowsedRollbackVersionSurvivesNextSlotAndFailure()
    {
        var gate=Gate();var results=new OverlayResults(gate);var session=Guid.NewGuid();
        for(int i=1;i<=3;i++){var id=Start(gate,session);results.Begin(id);results.Append(id,"PUBLIC"+i);results.Complete(id);}
        var first=results.Versions[0];results.SelectVersion(first.Identity.VersionId);var fourth=Start(gate,session);results.Begin(fourth);Assert.Equal(first.Identity.VersionId,results.RollbackVersionId);Assert.Contains(results.Versions,v=>v.Identity==first.Identity);
        var third=results.Versions.Single(v=>v.Number==3);results.SelectVersion(third.Identity.VersionId);results.Append(fourth,"PUBLIC partial");Assert.Equal(third.Text,results.Text);results.Fail(fourth);Assert.Equal(first.Identity.VersionId,results.SelectedVersionId);Assert.Equal(first.Text,results.Text);Assert.Equal(5,results.NextVersionNumber(session));
    }
    [Fact] public void ClearResetsOrdinalAndNoOldIdentityCanAppend()
    {var gate=Gate();var results=new OverlayResults(gate);var session=Guid.NewGuid();var id=Start(gate,session);results.Begin(id);results.Append(id,"PUBLIC");results.Clear();Assert.Equal(1,results.NextVersionNumber(session));Assert.False(results.Append(id,"late"));}
}
