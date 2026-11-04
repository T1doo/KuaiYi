using System.Text;
using KuaiYi.Core.Settings;
using Xunit;

namespace KuaiYi.Core.Tests;

public sealed class OverlayAppearanceSettingsTests
{
    [Fact]
    public void ExistingConfigurationGetsAppearanceDefaultsWithoutMigration()
    {
        var settings=SettingsJson.Read(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"overlay\":{\"fontSizeDip\":17}}"),out bool migrated);
        Assert.False(migrated);
        Assert.Equal(17,settings.Overlay.FontSizeDip);
        Assert.Equal(OverlayColorStyle.Aurora,settings.Overlay.ColorStyle);
        Assert.Equal(OverlayBackgroundMotion.Flow,settings.Overlay.BackgroundMotion);
        Assert.Equal(OverlayTypeface.Modern,settings.Overlay.Typeface);
    }

    [Fact]
    public void AppearanceRoundTripsWithoutChangingPrivacyOrTriggerSettings()
    {
        var original=new AppSettings();
        foreach(var color in Enum.GetValues<OverlayColorStyle>())
        foreach(var motion in Enum.GetValues<OverlayBackgroundMotion>())
        {
            var expected=original with{Overlay=original.Overlay with{ColorStyle=color,BackgroundMotion=motion,Typeface=OverlayTypeface.System,Outline=OverlayOutline.Cloud,CustomColor="#aB12fF"}};
            var actual=SettingsJson.Read(SettingsJson.Write(expected),out bool migrated);
            Assert.False(migrated);Assert.Equal(expected.Overlay,actual.Overlay);
            Assert.Equal(original.TriggerMode,actual.TriggerMode);
            Assert.False(actual.Privacy.PersistHistory);Assert.False(actual.Privacy.TelemetryEnabled);
            Assert.False(actual.Privacy.ClipboardFallbackDefault);Assert.Empty(actual.Profiles);
        }
    }

    [Theory]
    [InlineData("red")][InlineData("#123")][InlineData("#GG0000")][InlineData("#12345678")][InlineData("#123456\n")]
    public void InvalidCustomColorsAreRejectedBeforeRendering(string color)
    {
        var value=new AppSettings{Overlay=new(){CustomColor=color}};
        Assert.Throws<SettingsValidationException>(()=>SettingsJson.Write(value));
    }

    [Fact]
    public void UndefinedAppearanceEnumIsRejected()
    {
        Assert.Throws<SettingsValidationException>(()=>SettingsJson.Write(new(){Overlay=new(){ColorStyle=(OverlayColorStyle)999}}));
    }
}
