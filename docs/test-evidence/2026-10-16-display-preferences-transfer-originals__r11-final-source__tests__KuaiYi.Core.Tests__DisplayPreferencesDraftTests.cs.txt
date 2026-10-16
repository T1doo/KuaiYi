using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed class DisplayPreferencesDraftTests
{
    private static readonly DisplayPreferences Imported=new(21,800,ThemePreference.Light,MotionPreference.Reduce);
    [Theory][InlineData(false)][InlineData(true)]
    public void SaveAdmissionInvalidatesOldPreviewEvenWhenSaveFails(bool committed)
    {
        using var draft=new SettingsDraft(new(new(),"PUBLIC-basis",SettingsLoadState.Current));var old=draft.CaptureDisplayPreferences();var before=draft.Data;draft.BeginSave();
        Assert.Throws<InvalidOperationException>(()=>draft.CaptureDisplayPreferences());Assert.Throws<InvalidOperationException>(()=>draft.TryApplyDisplayPreferences(old,Imported));Assert.Same(before,draft.Data);
        draft.EndSave(committed);Assert.Equal(committed,draft.IsClosed);
        if(committed)Assert.Throws<InvalidOperationException>(()=>draft.TryApplyDisplayPreferences(old,Imported));
        else{Assert.False(draft.TryApplyDisplayPreferences(old,Imported));Assert.True(draft.TryApplyDisplayPreferences(draft.CaptureDisplayPreferences(),Imported));Assert.Equal(Imported,draft.CaptureDisplayPreferences().Value);}
    }
    [Fact]public void UnknownSchemaIsReadOnlyForDisplayCaptureAndApply()
    {
        using var draft=new SettingsDraft(new(new(),"PUBLIC-future",SettingsLoadState.ReadOnlyUnknown));using var other=new SettingsDraft(new(new(),"PUBLIC-current",SettingsLoadState.Current));
        Assert.Throws<UnknownSettingsSchemaException>(()=>draft.CaptureDisplayPreferences());Assert.Throws<UnknownSettingsSchemaException>(()=>draft.TryApplyDisplayPreferences(other.CaptureDisplayPreferences(),Imported));Assert.Equal(15,draft.Data.Overlay.FontSizeDip);
    }
}
