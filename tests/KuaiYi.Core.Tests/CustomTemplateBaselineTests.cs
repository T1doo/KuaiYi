using System.Text;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class CustomTemplateBaselineTests
{
    [Theory] [InlineData("Faithful")] [InlineData("Plain")] [InlineData("Professional")] [InlineData("Explain")]
    public void ValidCustomStyleMustRoundTripThroughActualSettingsJson(string mode)
    {
        byte[] original=Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"translation\":{\"customTemplates\":{\""+mode+"\":\"PUBLIC_CUSTOM {{domain}} {{source_text}}\"}}}");
        var config=SettingsJson.Read(original,out var migrated);Assert.False(migrated);var saved=SettingsJson.Write(config);Assert.Contains("PUBLIC_CUSTOM",Encoding.UTF8.GetString(saved));var reloaded=SettingsJson.Read(saved,out _);Assert.Equal(config.Translation.Preset,reloaded.Translation.Preset);
    }
}
