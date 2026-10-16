using KuaiYi.App;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class DisplayPreferencesCommandConcurrencyTests
{
    [Fact]public async Task DuplicateLinkedExportCannotStealTheFirstPublicationStatusOwner()
    {
        using var root=new DisplayPreferencesFlowTests.TemporaryDirectory();using var draft=new SettingsDraft(new(new(),"PUBLIC-basis",SettingsLoadState.Current));var window=new SettingsWindow(draft,null);var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int preparations=0;
        window.PrepareDisplayExportForTest(async(path,bytes,ct)=>{Interlocked.Increment(ref preparations);var prepared=await DisplayPreferencesFiles.PrepareNewAsync(path,bytes,ct);ready.SetResult();await release.Task;return prepared;});window.PreviewDisplayForTest();string path=Path.Combine(root.Path,"display.json");var first=window.ExportDisplayForTest(path);await ready.Task.WaitAsync(TimeSpan.FromSeconds(6));
        try{var duplicate=await window.ExportDisplayForTest(Path.Combine(root.Path,"other.json"));Assert.Equal(DisplayPreferencesExportStatus.Busy,duplicate!.Status);Assert.Equal(1,preparations);}
        finally{release.TrySetResult();}
        Assert.Equal(DisplayPreferencesExportStatus.Published,(await first)!.Status);Assert.Contains("已导出",window.DisplayStatusForTest);Assert.Equal(new(15,420,ThemePreference.FollowSystem,MotionPreference.FollowSystem),await DisplayPreferencesFiles.ReadAsync(path));Assert.Single(Directory.GetFiles(root.Path));window.CloseDisplayForTest();
    }
}
