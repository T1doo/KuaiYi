using System.Collections.Immutable;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class SettingsEnableGuideTests
{
    private static ApiProfile Profile()=>new(){Id="public",Name="Public fixture",Model="public-model",BaseUrl="https://public.invalid/v1",Authentication=ApiAuthentication.None};
    private static SettingsSnapshot Snapshot(ApiProfile? profile=null,string? active="public",SettingsLoadState state=SettingsLoadState.Current)
        =>new(new(){ActiveProfileId=active,Profiles=profile==null?[]:[profile]},"public-version",state);
    [Fact]public void FirstUseLocatesCreateProfileInsteadOfTheGeneralPage()
    {var guide=SettingsEnableGuide.Plan(Snapshot());Assert.Equal(SettingsEnableTarget.CreateProfile,guide.Target);Assert.Equal(1,guide.Page);}
    [Theory][InlineData(null)][InlineData("unknown")]
    public void ExistingProfilesWithoutAValidDefaultPointToTheDefaultSelector(string? active)
    {Assert.Equal(SettingsEnableTarget.ChooseTextProfile,SettingsEnableGuide.Plan(Snapshot(Profile(),active)).Target);}
    [Fact]public void MissingModelAndMissingKeyLocateTheActiveProfileWithoutReadingAnySecret()
    {
        var model=SettingsEnableGuide.Plan(Snapshot(Profile() with{Model=""}));Assert.Equal(SettingsEnableTarget.Model,model.Target);Assert.Equal("public",model.ProfileId);
        var key=SettingsEnableGuide.Plan(Snapshot(Profile() with{Authentication=ApiAuthentication.Bearer}));Assert.Equal(SettingsEnableTarget.Key,key.Target);Assert.Equal("public",key.ProfileId);
        Assert.Equal(SettingsEnableTarget.Parameters,SettingsEnableGuide.Plan(Snapshot(Profile() with{Authentication=ApiAuthentication.Bearer,SecretRef="key/"+new string('a',32)})).Target);
    }
    [Fact]public void InvalidEndpointPointsToAddressButNeverEnablesOrChangesTheDraft()
    {
        var snapshot=Snapshot(Profile() with{BaseUrl="not-an-endpoint"});using var draft=new SettingsDraft(snapshot);var before=draft.Data;
        var guide=SettingsEnableGuide.Plan(snapshot);Assert.Equal(SettingsEnableTarget.Address,guide.Target);Assert.Same(before,draft.Data);
        var gate=new RuntimeGate(requireProfileBinding:true);gate.ObserveSettingsMetadata(Snapshot(Profile()));
        Assert.Equal(Activation.Off,gate.Snapshot.Activation);Assert.False(gate.HasConsentTo(new(new Uri("https://public.invalid"))));
    }
    [Theory][InlineData(SettingsLoadState.ReadOnlyUnknown)][InlineData(SettingsLoadState.RecoverableBackup)][InlineData(SettingsLoadState.RecoverableDefaults)]
    public void RecoveryAlwaysWinsOverApiGuidance(SettingsLoadState state)
    {var guide=SettingsEnableGuide.Plan(Snapshot(Profile(),state:state));Assert.Equal(SettingsEnableTarget.Recovery,guide.Target);Assert.Equal(0,guide.Page);Assert.Null(guide.ProfileId);}
    [Fact]public void LocatingTheActiveProfileDoesNotSelectADifferentProfileOrLeakItsValues()
    {
        var p=Profile() with{Name="PRIVATE_NAME",Model="PRIVATE_MODEL"};var snapshot=Snapshot(p) with{Data=Snapshot(p).Data with{Profiles=ImmutableArray.Create(Profile() with{Id="other"},p)}};
        var guide=SettingsEnableGuide.Plan(snapshot);Assert.Equal("public",guide.ProfileId);Assert.DoesNotContain("PRIVATE",guide.Notice);Assert.Equal("public",snapshot.Data.ActiveProfileId);
    }
}
