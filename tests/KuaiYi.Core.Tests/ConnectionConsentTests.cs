using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class ConnectionConsentTests
{
    private static (RuntimeGate Gate,RequestProfileBinding Binding,EndpointOrigin Origin) Create()
    {var gate=new RuntimeGate(requireProfileBinding:true);var origin=new EndpointOrigin(new Uri("https://public-fixture.invalid"));gate.ObserveSettingsMetadata(new(new(){ActiveProfileId="p",Profiles=[new(){Id="p",Model="PUBLIC_MODEL",BaseUrl=origin.Value,Authentication=ApiAuthentication.None}]},"initial",SettingsLoadState.Current));Assert.True(gate.TryGetProfileBinding("p",out var binding));return(gate,binding,origin);}
    [Fact] public void FixedSampleNeedsNoOrdinaryContentGrantAndCreatesNone()
    {
        var(gate,binding,origin)=Create();Assert.True(gate.TryStartConnectionTest(binding,origin,gate.Snapshot.Epoch,out var id,out _));Assert.True(gate.CanSend(id,origin));Assert.False(gate.HasConsentTo(origin));Assert.Equal(Activation.Off,gate.Snapshot.Activation);
        gate.StopRequest(id);Assert.False(gate.TryEnable(origin,true));Assert.False(gate.CanSend(id,origin));
    }
    [Fact] public void TestIdentityCannotUseAnUnrelatedAlreadyConsentedOrigin()
    {
        var(gate,binding,origin)=Create();var other=new EndpointOrigin(new Uri("https://other-fixture.invalid"));gate.ConsentTo(origin);gate.ConsentTo(other);
        Assert.True(gate.TryStartConnectionTest(binding,origin,gate.Snapshot.Epoch,out var id,out _));int sends=0;
        Assert.False(gate.TryBeginSend(id,other,()=>{sends++;return Task.FromResult(1);},out var task));Assert.Null(task);Assert.Equal(0,sends);Assert.True(gate.CanSend(id,origin));Assert.True(gate.HasConsentTo(other));
    }
    [Theory] [InlineData("stop")] [InlineData("off")] [InlineData("lock")] [InlineData("replacement")]
    public void TemporaryDestinationDiesWithOriginalTestIdentity(string action)
    {
        var(gate,binding,origin)=Create();gate.ConsentTo(origin);Assert.True(gate.TryStartConnectionTest(binding,origin,gate.Snapshot.Epoch,out var old,out _));
        if(action=="stop")gate.StopRequest(old);else if(action=="off")gate.Disable();else if(action=="lock")gate.SetSessionLocked(true);
        else{gate.MarkRequestTerminal(old);Assert.True(gate.TryStartConnectionTest(binding,new(new Uri("https://replacement-fixture.invalid")),gate.Snapshot.Epoch,out var next,out _));Assert.False(gate.CanSend(next,origin));}
        Assert.False(gate.CanSend(old,origin));
    }
}
