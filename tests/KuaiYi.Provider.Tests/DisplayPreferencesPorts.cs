using KuaiYi.Core.Settings;
namespace KuaiYi.App;
// Actual production commands with four-field/control ports; native pickers and WPF remain NotRun.
internal sealed partial class SettingsWindow
{
    private sealed class DisplayButtonPort{public bool IsEnabled;}
    private sealed class DisplayChoicePort{public object? SelectedItem;}
    private readonly TextPort font=new(){Text="15"},width=new(){Text="420"},displayPreview=new();
    private readonly DisplayChoicePort theme=new(){SelectedItem=ThemePreference.FollowSystem},motion=new(){SelectedItem=MotionPreference.FollowSystem};
    private readonly DisplayButtonPort displayApply=new(),displayWrite=new();private bool saving;
    internal Task ImportDisplayForTest(string? path)=>PreviewDisplayImport(path);
    internal void PreviewDisplayForTest()=>PreviewDisplayExport();internal void ApplyDisplayForTest()=>ApplyDisplayImport();
    internal Task<DisplayPreferencesExportResult?> ExportDisplayForTest(string? path)=>WriteDisplayExport(path);
    internal void EditDisplayForTest(string size,string preferred,ThemePreference selectedTheme=ThemePreference.FollowSystem,MotionPreference selectedMotion=MotionPreference.FollowSystem)
    {font.Text=size;width.Text=preferred;theme.SelectedItem=selectedTheme;motion.SelectedItem=selectedMotion;DisplayFieldsEdited();}
    internal void CancelDisplayForTest()=>CancelDisplayPreferences(announce:true);internal void CloseDisplayForTest()=>CloseDisplayPreferencesView();
    internal void SavingDisplayForTest(bool value){CancelDisplayPreferences();saving=value;}
    internal string DisplayPreviewForTest=>displayPreview.Text;internal string DisplayStatusForTest=>status.Text;
    internal void SeedUncapturedPasswordForDisplayTest(string value)=>password.Password=value;
    internal void PrepareDisplayExportForTest(Func<string,ReadOnlyMemory<byte>,CancellationToken,Task<IPreparedDisplayPreferencesExport>> prepare)=>prepareDisplayExport=prepare;
    internal (bool Apply,bool Write) DisplayButtonsForTest=>(displayApply.IsEnabled,displayWrite.IsEnabled);
}
