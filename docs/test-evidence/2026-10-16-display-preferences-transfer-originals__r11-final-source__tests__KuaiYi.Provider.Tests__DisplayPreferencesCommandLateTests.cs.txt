using KuaiYi.App;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class DisplayPreferencesCommandLateTests
{
    [Theory][InlineData("cancel")][InlineData("close")][InlineData("saving")][InlineData("new-preview")]
    public async Task LateLinkedExportCannotPublishOrReplaceTheNewViewAfterWithdrawal(string change)
    {
        using var root=new DisplayPreferencesFlowTests.TemporaryDirectory();using var draft=new SettingsDraft(new(new(),"PUBLIC-basis",SettingsLoadState.Current));var window=new SettingsWindow(draft,null);var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.PrepareDisplayExportForTest(async(path,bytes,ct)=>{var prepared=await DisplayPreferencesFiles.PrepareNewAsync(path,bytes,ct);ready.SetResult();await release.Task;return prepared;});window.PreviewDisplayForTest();string target=Path.Combine(root.Path,"display.json");var pending=window.ExportDisplayForTest(target);await ready.Task.WaitAsync(TimeSpan.FromSeconds(6));
        try
        {
            if(change=="cancel")window.CancelDisplayForTest();else if(change=="close")window.CloseDisplayForTest();else if(change=="saving")window.SavingDisplayForTest(true);else{window.EditDisplayForTest("21","800",ThemePreference.Dark,MotionPreference.Reduce);window.PreviewDisplayForTest();}
        }
        finally{release.TrySetResult();}
        string status=window.DisplayStatusForTest,preview=window.DisplayPreviewForTest;Assert.Equal(DisplayPreferencesExportStatus.Cancelled,(await pending)!.Status);Assert.False(File.Exists(target));Assert.Empty(Directory.GetFiles(root.Path));Assert.Equal(status,window.DisplayStatusForTest);Assert.Equal(preview,window.DisplayPreviewForTest);Assert.Equal(15,draft.Data.Overlay.FontSizeDip);Assert.Equal(0,window.CaptureCalls);
        if(change=="new-preview"){Assert.Contains("字号：21",preview);Assert.Equal((false,true),window.DisplayButtonsForTest);}else Assert.Equal((false,false),window.DisplayButtonsForTest);window.CloseDisplayForTest();
    }
}
