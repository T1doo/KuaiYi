namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private void ClearPrivatePresentation(List<Exception> errors,Action? closeSettings=null,bool hide=true)
    {
        ScreenshotPreviewCleanup.Attempt(errors,()=>imageTranslation?.Clear());ScreenshotPreviewCleanup.Attempt(errors,()=>translation?.Clear());ScreenshotPreviewCleanup.Attempt(errors,ClearSelectionSource);
        if(closeSettings!=null)ScreenshotPreviewCleanup.Attempt(errors,closeSettings);
        ScreenshotPreviewCleanup.Attempt(errors,()=>HidePresentation(clear:true));ScreenshotPreviewCleanup.Attempt(errors,ReleaseRetainedImage);
        if(hide)ScreenshotPreviewCleanup.Attempt(errors,Hide);
    }
    private void PrepareSettingsExit(Action? closeSettings)
    {
        List<Exception> errors=[];ScreenshotPreviewCleanup.Attempt(errors,lifecycle.Hide);
        ClearPrivatePresentation(errors,closeSettings,hide:false);ScreenshotPreviewCleanup.Attempt(errors,Refresh);
        button.ToolTip="正在等待设置草稿和原保存任务结束后退出。";
        if(errors.Count>0)Report("正在等待设置结束；部分内容清理尚未确认",new AggregateException(errors));
    }
    private void ApplySessionPresentation(bool locked,Action? closeSettings=null,List<Exception>? failures=null)
    {
        if(exiting)return;var errors=failures??[];
        ScreenshotPreviewCleanup.Attempt(errors,()=>RecoverHotkeys(locked));ScreenshotPreviewCleanup.Attempt(errors,CancelPointer);
        if(locked)ClearPrivatePresentation(errors,closeSettings);else ScreenshotPreviewCleanup.Attempt(errors,ShowOverlay);
        ScreenshotPreviewCleanup.Attempt(errors,Refresh);
        if(errors.Count>0)Report("会话已更新；部分清理尚未确认",new AggregateException(errors));
    }
    private async Task SettleExitContent()
    {
        List<Exception> errors=[];ScreenshotPreviewCleanup.Attempt(errors,lifecycle.Hide);ScreenshotPreviewCleanup.Attempt(errors,CancelPointer);
        ClearPrivatePresentation(errors);ScreenshotPreviewCleanup.Attempt(errors,Refresh);
        async Task Settle(Func<Task> step){try{await step();}catch(Exception error){errors.Add(error);}}
        await Settle(()=>startup);await Settle(()=>positionSave);await Settle(StopTextDrop);await Settle(StopSelection);
        await Settle(StopHotkeyScreenshot);await Settle(StopHotkeys);await Settle(StopTranslation);
        if(errors.Count>0)throw new AggregateException("Exit cleanup unconfirmed.",errors);
        await SettleInstallationControl(); // Keep the original close-control loop available while content cleanup needs retry.
    }
}
