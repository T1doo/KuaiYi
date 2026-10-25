using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.GrowthHarness;
using KuaiYi.Providers;
using Xunit;

namespace KuaiYi.Provider.Tests;

// Explicit collection ownership: restore the exact prior process object after every test
// has awaited its original fixture. No other test collection overlaps this global setting.
[CollectionDefinition("UiPolishIsolatedProxy",DisableParallelization=true)]
public sealed class UiPolishProxyCollection:ICollectionFixture<UiPolishProxyScope>;
public sealed class UiPolishProxyScope:IDisposable
{
    private readonly System.Net.IWebProxy original=System.Net.Http.HttpClient.DefaultProxy;
    public UiPolishProxyScope()=>System.Net.Http.HttpClient.DefaultProxy=new System.Net.WebProxy();
    public void Dispose()=>System.Net.Http.HttpClient.DefaultProxy=original;
}
[Collection("UiPolishIsolatedProxy")]
public sealed class UiPolishProbeTests
{
    private static Dictionary<string,string> Allowed()=>new(){["KUAIYI_UI_POLISH_ALLOW_WINDOWS"]="1",["GITHUB_ACTIONS"]="true",["GITHUB_REPOSITORY"]="T1doo/KuaiYi",["GITHUB_REF"]=UiPolishContract.Branch,["GITHUB_RUN_ATTEMPT"]="1"};
    private static readonly string[] Arguments=["--ci-ui-polish","exclusive-output","after","overlay"];
    [Fact]public void LinuxAndDefaultEnvironmentRefuseBeforeAnyApplicationExists()
    {
        Assert.Throws<InvalidOperationException>(()=>UiPolishContract.Authorize(Arguments,false,key=>Allowed().GetValueOrDefault(key)));
        Assert.Throws<InvalidOperationException>(()=>UiPolishContract.Authorize(Arguments,true,_=>null));
    }
    [Theory][InlineData("KUAIYI_UI_POLISH_ALLOW_WINDOWS")][InlineData("GITHUB_ACTIONS")][InlineData("GITHUB_REPOSITORY")][InlineData("GITHUB_REF")][InlineData("GITHUB_RUN_ATTEMPT")]
    public void EachIndependentOptInRequirementIsNecessary(string key)
    {var values=Allowed();values[key]="wrong";Assert.Throws<InvalidOperationException>(()=>UiPolishContract.Authorize(Arguments,true,k=>values.GetValueOrDefault(k)));}
    [Fact]public void MalformedNewCommandCannotFallThroughToOldWindowProbe()
    {
        foreach(var args in new[]{new[]{"--ci-ui-polish"},new[]{"--ci-ui-polish-typo","out","after","overlay"},new[]{"--ci-ui-polish","out","after","source"}})
        {Assert.True(UiPolishContract.IsCommand(args));Assert.Throws<ArgumentException>(()=>UiPolishContract.Authorize(args,true,k=>Allowed().GetValueOrDefault(k)));}
        Assert.False(UiPolishContract.IsCommand(["--ci-overlay-stages","out"]));
    }
    [Theory][InlineData("before",UiPolishContract.Before,UiPolishContract.After)][InlineData("after",UiPolishContract.After,UiPolishContract.Before)]
    public void ComparisonSidesPinActualProductRevision(string side,string expected,string other)
    {
        var request=UiPolishContract.Authorize(["--ci-ui-polish","out",side,"settings"],true,k=>Allowed().GetValueOrDefault(k));
        Assert.Equal(expected,request.ProductHead);UiPolishContract.RequireProductVersion(request,"1.0.0+"+expected);
        Assert.Throws<InvalidOperationException>(()=>UiPolishContract.RequireProductVersion(request,"1.0.0+"+other));
        Assert.Throws<InvalidOperationException>(()=>UiPolishContract.RequireProductVersion(request,"1.0.0"));
    }
    private sealed class Immediate:SynchronizationContext{public override void Post(SendOrPostCallback callback,object? state)=>callback(state);}
    private static (RuntimeGate Gate,OverlayResults Results,SettingsSnapshot Settings) State()
    {
        var settings=new SettingsSnapshot(UiPolishTextFixture.Configuration(ThemePreference.Light,MotionPreference.Reduce),"public-fixture",SettingsLoadState.Current);
        var gate=new RuntimeGate();gate.ObserveSettingsMetadata(settings);var origin=new EndpointOrigin(new Uri(UiPolishTextFixture.Endpoint));gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));
        return(gate,new(gate),settings);
    }
    [Fact]public async Task RealCoordinatorCreatesSnapshotAndCompletionSurvivesOriginalConnectorCancellation()
    {
        var s=State();var updates=new List<TextTranslationUpdate>();
        await using var fixture=new UiPolishTextFixture(s.Gate,s.Results,s.Settings,new Immediate(),updates.Add);
        using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(5));var id=await fixture.Start(budget.Token);
        Assert.Equal(1,fixture.Attempts);Assert.False(fixture.OriginalConnectors.Single().IsCompleted);
        var snapshot=fixture.Flow.SnapshotForVersion(id.VersionId);Assert.NotNull(snapshot);Assert.Same(fixture.Source,snapshot.Source);Assert.Same(s.Settings,snapshot.Settings);
        Assert.Equal(UiPolishTextFixture.Model,snapshot.Profile.Model);Assert.Null(snapshot.Transport.DevelopmentBoundary);Assert.False(snapshot.Transport.IsDevelopmentFixture);
        Assert.Equal(OverlayResultEventKind.Begin,Assert.Single(updates).Presentation.Kind);
        Assert.True(s.Results.Append(id,UiPolishTextFixture.ResultText));Assert.True(s.Results.ObserveUsage(id,24,18));Assert.True(s.Results.Complete(id));
        await fixture.CancelAndJoin(id);Assert.All(fixture.OriginalRuns,t=>Assert.True(t.IsCompletedSuccessfully));Assert.True(fixture.OriginalConnectors.Single().IsCanceled);
        Assert.True(Assert.Single(s.Results.Versions).Complete);Assert.False(s.Results.Versions.Single().Stopped);Assert.Null(s.Results.Versions.Single().Failure);
        Assert.Same(snapshot,fixture.Flow.SelectedSnapshot);Assert.False(s.Gate.CanApply(id));
    }
    [Fact]public async Task SingleIdentityProviderRefusesSecondStartEvenAfterFirstSettlement()
    {
        var s=State();await using var fixture=new UiPolishTextFixture(s.Gate,s.Results,s.Settings,new Immediate(),_=>{});
        using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(5));var id=await fixture.Start(budget.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>fixture.Start(budget.Token));
        Assert.True(s.Gate.CanApply(id));await fixture.CancelAndJoin(id);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>fixture.Start(budget.Token));Assert.Equal(1,fixture.Attempts);_ = Assert.Single(fixture.OriginalRuns);
    }
    [Fact]public async Task UnattachedFixtureStillOwnsAndSettlesOriginalRequestBeforeProviderDisposal()
    {
        var s=State();var fixture=new UiPolishTextFixture(s.Gate,s.Results,s.Settings,new Immediate(),_=>{});
        try
        {
            using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(5));var id=await fixture.Start(budget.Token);
            Assert.Throws<InvalidOperationException>(fixture.DisposeProviderAfterOriginalClose);
            await fixture.DisposeAsync();Assert.False(s.Gate.CanApply(id));Assert.All(fixture.OriginalRuns,t=>Assert.True(t.IsCompleted));Assert.True(fixture.OriginalConnectors.Single().IsCanceled);
        }
        finally{await fixture.DisposeAsync();}
    }
    [Theory][InlineData("credentials")][InlineData("headers")][InlineData("endpoint")][InlineData("protocol")]
    public void FixtureRejectsProfilesOutsideTheFixedPublicNoCredentialContract(string change)
    {
        var s=State();var p=s.Settings.Data.Profiles.Single();p=change switch
        {"credentials"=>p with{SecretRef="forbidden"},"headers"=>p with{Headers=p.Headers.Add("x-api-key","synthetic-forbidden")},"endpoint"=>p with{BaseUrl="https://example.invalid/"},_=>p with{Protocol=KuaiYi.Core.Settings.ApiProtocol.AnthropicMessages}};
        var changed=s.Settings with{Data=s.Settings.Data with{Profiles=[p]}};
        Assert.Throws<InvalidOperationException>(()=>new UiPolishTextFixture(s.Gate,s.Results,changed,new Immediate(),_=>{}));
    }
}
