using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class OverlayReviewFixTests
{
    private static RuntimeGate Gate()
    {
        var gate=new RuntimeGate(requireProfileBinding:true);gate.ObserveSettingsMetadata(new(new(){ActiveProfileId="public",Profiles=[new(){Id="public",BaseUrl="https://public.invalid",Model="public",Authentication=ApiAuthentication.None}]},"public-version",SettingsLoadState.Current));var origin=new EndpointOrigin(new Uri("https://public.invalid"));gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));return gate;
    }
    private static RequestIdentity Start(RuntimeGate gate,Guid session)
    {Assert.True(gate.TryGetProfileBinding("public",out var binding));Assert.True(gate.TryStartProfileRequest(binding,session,Guid.NewGuid(),null,out var id,out _));return id;}
    [Theory][InlineData(172)][InlineData(100)][InlineData(68)][InlineData(56)][InlineData(28)][InlineData(20)][InlineData(0)][InlineData(400)]
    public void ToolbarReservesSettingsAndEveryActionHasVisibleOrOverflowRoute(double available)
    {
        var plan=OverlayToolbarLayout.Plan(available,5,4);
        if(available>=28)Assert.Contains(plan.Slots,s=>s.Index==4);
        Assert.Equal(Enumerable.Range(0,5),plan.Slots.Select(s=>s.Index).Concat(plan.Overflow).Order());
        foreach(var slot in plan.Slots){Assert.True(slot.WidthDip>=28);Assert.InRange(slot.OffsetDip,0,available-slot.WidthDip);}
        for(int i=1;i<plan.Slots.Count;i++)Assert.True(plan.Slots[i].OffsetDip>=plan.Slots[i-1].OffsetDip+plan.Slots[i-1].WidthDip);
        if(plan.OverflowOffsetDip is {} offset){Assert.InRange(offset,0,available-28);Assert.True(plan.Slots.All(s=>s.OffsetDip+s.WidthDip<=offset));}
        if(available==172){Assert.Contains(3,plan.Overflow);Assert.NotNull(plan.OverflowOffsetDip);}
    }
    [Theory][InlineData(30,false)][InlineData(300,false)][InlineData(30,true)][InlineData(300,true)]
    public void WaitingOrHoverToBodyMeasuresFinalPlanOnceAndReversalKeepsCorrectHeight(int characters,bool reverse)
    {
        var monitor=new MonitorGeometry("primary",new(-800,-600,1200,1000),new(1));var frame=new OverlayFrames(new(-750,-550,-702,-502),monitor);var preferences=new LayoutPreferences(420,640);
        frame.OpenToolbar();frame.Tick(TimeSpan.FromMilliseconds(220));Assert.True(frame.Current.Width<=220);
        if(reverse){frame.Collapse();frame.Tick(TimeSpan.FromMilliseconds(45));}
        var before=frame.Current;LayoutPlan? measuredPlan=null;int calls=0,height=0;
        // Deterministic public wrap-height fixture isolates the production plan/measurement contract.
        // It does not claim WPF font metrics have been executed on Linux.
        frame.OpenMeasured(preferences,plan=>{calls++;measuredPlan=plan;int perLine=(int)((plan.Dpi.Dips(plan.Target.Width)-32)/15);height=80+(characters+perLine-1)/perLine*24;return height;});
        Assert.Equal(1,calls);Assert.Same(measuredPlan,frame.Plan);Assert.Equal(420,frame.Plan!.Target.Width);Assert.Equal(before,frame.Current);
        for(int i=0;i<500&&frame.NeedsFrame;i++)frame.Tick(TimeSpan.FromMilliseconds(16));
        Assert.Equal(height,frame.Current.Height);Assert.Equal(characters==30?128:368,height);Assert.True(frame.Current.Contains(frame.Anchor));
        frame.Grow(height);Assert.False(frame.NeedsFrame);Assert.Equal(height,frame.Current.Height);
    }
    [Fact]public void GeneratingSlotRollbackAndReadingSelectionStayCoherentThroughRegeneration()
    {
        var gate=Gate();var cache=new OverlayResults(gate);var session=Guid.NewGuid();var ids=new List<RequestIdentity>();
        for(int i=0;i<3;i++){var id=Start(gate,session);ids.Add(id);cache.Begin(id);cache.Append(id,"public"+i);cache.Complete(id);}
        Assert.True(cache.SelectVersion(ids[0].VersionId));var fourth=Start(gate,session);cache.Begin(fourth);
        Assert.Equal(3,cache.Versions.Count);Assert.Contains(cache.Versions,v=>v.Identity==fourth&&v.Text==""&&!v.Complete&&!v.Stopped);Assert.Contains(cache.Versions,v=>v.Identity==ids[0]);Assert.DoesNotContain(cache.Versions,v=>v.Identity==ids[1]);Assert.Equal(ids[0].VersionId,cache.RollbackVersionId);
        Assert.True(cache.Append(fourth,"public fourth"));Assert.True(cache.SelectVersion(ids[0].VersionId));Assert.Equal("public0",cache.Text);Assert.False(cache.TryAutoReveal(fourth));
        Assert.True(cache.Append(fourth," stream"));Assert.Equal("public0",cache.Text);Assert.Equal("public fourth stream",cache.Versions.Single(v=>v.Identity==fourth).Text);
        Assert.True(cache.Complete(fourth));Assert.Null(cache.RollbackVersionId);Assert.Equal("public0",cache.Text);
        var fifth=Start(gate,session);cache.Begin(fifth);Assert.Equal(3,cache.Versions.Count);Assert.Contains(cache.Versions,v=>v.Identity==ids[0]);Assert.Contains(cache.Versions,v=>v.Identity==fifth);
        cache.Append(fifth,"public cancelled");gate.StopGeneration();cache.MarkStopped();Assert.Equal(3,cache.Versions.Count);Assert.True(cache.Versions.Single(v=>v.Identity==fifth).Stopped);Assert.True(cache.SelectVersion(cache.RollbackVersionId!.Value));Assert.Equal("public0",cache.Text);Assert.False(cache.Append(fifth,"late"));Assert.False(cache.Complete(fifth));
        var sixth=Start(gate,session);cache.Begin(sixth);Assert.Equal(3,cache.Versions.Count);Assert.Contains(cache.Versions,v=>v.Identity==ids[0]);Assert.Contains(cache.Versions,v=>v.Identity==fifth&&v.Stopped);Assert.DoesNotContain(cache.Versions,v=>v.Identity==fourth);
        cache.Append(sixth,"public next");Assert.True(cache.SelectVersion(sixth.VersionId));Assert.True(cache.TryAutoReveal(sixth));Assert.Equal("public next",cache.Text);
    }
    [Fact]public void EmptyCancelledVersionOccupiesOneSlotAndValidatedSourceReplacesAllVersions()
    {
        var gate=Gate();var cache=new OverlayResults(gate);var session=Guid.NewGuid();var id=Start(gate,session);cache.Begin(id);gate.StopGeneration();cache.MarkStopped();var empty=Assert.Single(cache.Versions);Assert.Equal("",empty.Text);Assert.True(empty.Stopped);
        Assert.False(cache.SelectVersion(id.VersionId));var next=Start(gate,session);cache.Begin(next);cache.Append(next,"public cached");Assert.Equal(2,cache.Versions.Count);var snapshot=cache.Versions;
        Assert.False(cache.Begin(new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),0)));Assert.Equal(snapshot,cache.Versions);gate.Disable();Assert.True(cache.SelectVersion(next.VersionId));Assert.Equal(Activation.Off,gate.Snapshot.Activation);
        gate.SetSessionLocked(true);Assert.False(cache.SelectVersion(next.VersionId));cache.Clear();Assert.Empty(cache.Versions);Assert.Null(cache.SelectedVersionId);Assert.Null(cache.RollbackVersionId);gate.SetSessionLocked(false);Assert.True(gate.TryEnable(new(new Uri("https://public.invalid")),true));
        var newSource=Start(gate,Guid.NewGuid());cache.Begin(newSource);Assert.Equal(newSource,Assert.Single(cache.Versions).Identity);Assert.Equal("",cache.Text);Assert.Null(cache.RollbackVersionId);
    }
}
