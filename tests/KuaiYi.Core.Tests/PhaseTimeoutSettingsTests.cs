using System.Text;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class PhaseTimeoutSettingsTests
{
    [Theory] [InlineData("connectSeconds",7)] [InlineData("headersSeconds",11)] [InlineData("firstResponseSeconds",13)] [InlineData("idleSeconds",17)]
    public void ValidPhaseSettingMustSurviveActualJsonReadAndWrite(string field,int value)
    {
        var raw=Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"profiles\":[{\"id\":\"p\",\"model\":\"PUBLIC\",\"baseUrl\":\"https://fixture.invalid/v1\",\"timeouts\":{\""+field+"\":"+value+"}}]}");
        var settings=SettingsJson.Read(raw,out _);string saved=Encoding.UTF8.GetString(SettingsJson.Write(settings));Assert.Contains("\""+field+"\": "+value,saved);
    }

    [Theory] [InlineData("connectSeconds",0)] [InlineData("connectSeconds",61)] [InlineData("headersSeconds",0)] [InlineData("headersSeconds",181)] [InlineData("firstResponseSeconds",0)] [InlineData("firstResponseSeconds",301)] [InlineData("idleSeconds",0)] [InlineData("idleSeconds",181)]
    public void InvalidPhaseValuesCannotBeReadAsValidSettings(string field,int value)
    {byte[] raw=Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"profiles\":[{\"id\":\"p\",\"model\":\"PUBLIC\",\"baseUrl\":\"https://fixture.invalid/v1\",\"timeouts\":{\""+field+"\":"+value+"}}]}");Assert.Throws<SettingsValidationException>(()=>SettingsJson.Read(raw,out _));}
    [Fact] public void OldProfileDefaultsRemainCompatibleAndFinite()
    {var settings=SettingsJson.Read(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"profiles\":[{\"id\":\"p\",\"model\":\"PUBLIC\",\"baseUrl\":\"https://fixture.invalid/v1\"}]}"),out _);var value=Assert.Single(settings.Profiles);Assert.Equal(new RequestTimeouts(),value.Timeouts);Assert.Equal(10,value.Timeouts.ConnectSeconds);Assert.Equal(30,value.Timeouts.FirstResponseSeconds);Assert.Equal(20,value.Timeouts.IdleSeconds);Assert.Equal(120,value.TimeoutSeconds);}
    [Fact] public void MaximumValuesAndFrozenRecordSurviveSaveAndSemanticComparison()
    {var old=new ApiProfile{Id="p",Model="PUBLIC",BaseUrl="https://fixture.invalid/v1",Timeouts=new(){ConnectSeconds=60,HeadersSeconds=180,FirstResponseSeconds=300,IdleSeconds=180}};var settings=new AppSettings{Profiles=[old]};var loaded=SettingsJson.Read(SettingsJson.Write(settings),out _);Assert.Equal(old.Timeouts,loaded.Profiles[0].Timeouts);Assert.True(ApiProfilePolicy.RequestEquivalent(old,loaded.Profiles[0]));var changed=old with{Timeouts=old.Timeouts with{ConnectSeconds=1}};Assert.Equal(60,old.Timeouts.ConnectSeconds);Assert.False(ApiProfilePolicy.RequestEquivalent(old,changed));}
    [Fact] public void NullAndUnknownTimeoutFieldsAreRejectedExplicitly()
    {var profile=new ApiProfile{Id="p",Model="PUBLIC",BaseUrl="https://fixture.invalid/v1",Timeouts=null!};Assert.Throws<SettingsValidationException>(()=>ApiProfilePolicy.ValidateParameters(profile));Assert.Throws<System.Text.Json.JsonException>(()=>SettingsJson.Read(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"profiles\":[{\"id\":\"p\",\"model\":\"PUBLIC\",\"baseUrl\":\"https://fixture.invalid/v1\",\"timeouts\":{\"unknownStage\":5}}]}"),out _));}
}
