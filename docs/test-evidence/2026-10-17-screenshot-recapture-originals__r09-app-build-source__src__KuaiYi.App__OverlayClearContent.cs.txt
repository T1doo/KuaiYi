namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private async Task<bool> ClearCurrentContent()
    {
        if(exiting)return false; // Exit already owns cancellation and release; never reopen its UI.
        List<Exception> errors=[];
        ScreenshotPreviewCleanup.Attempt(errors,lifecycle.Gate.StopGeneration); // Fence even if a cancellation callback throws.
        resultRevision++;Task translationCleanup=Task.CompletedTask,imageCleanup=Task.CompletedTask;
        ScreenshotPreviewCleanup.Attempt(errors,()=>imageCleanup=imageTranslation?.ClearAsync()??Task.CompletedTask);
        ScreenshotPreviewCleanup.Attempt(errors,()=>translationCleanup=translation?.ClearAsync()??Task.CompletedTask);
        ScreenshotPreviewCleanup.Attempt(errors,ClearSelectionSource);ScreenshotPreviewCleanup.Attempt(errors,()=>RevokeTextDrop(clearSource:true));
        externalDropHover=restoreDropBody=restoreDropPeek=false;ScreenshotPreviewCleanup.Attempt(errors,()=>dropHint.Visibility=System.Windows.Visibility.Collapsed);
        ScreenshotPreviewCleanup.Attempt(errors,()=>HidePresentation(clear:true));
        ScreenshotPreviewCleanup.Attempt(errors,ReleaseRetainedImage);ScreenshotPreviewCleanup.Attempt(errors,overlayResults.Clear);ScreenshotPreviewCleanup.Attempt(errors,ClearMarkdown);
        if(lifecycle.Gate.Snapshot.Presentation!=KuaiYi.Core.Presentation.Hidden)lifecycle.Gate.Collapse();
        dirty=false;pendingReveal=false;ScreenshotPreviewCleanup.Attempt(errors,()=>newContent.Visibility=System.Windows.Visibility.Collapsed);ResetBodyReaderFollow();
        translationStatus=null;ScreenshotPreviewCleanup.Attempt(errors,()=>connectionStatus.Text="正在连接…");ScreenshotPreviewCleanup.Attempt(errors,RefreshResultLabel);
        long revision=resultRevision;
        button.ToolTip="当前文字内容已清除 · 正在结束当前任务";
        Task screenshotCleanup=Task.CompletedTask,dropCleanup=Task.CompletedTask;
        ScreenshotPreviewCleanup.Attempt(errors,()=>screenshotCleanup=StopHotkeyScreenshot());ScreenshotPreviewCleanup.Attempt(errors,()=>dropCleanup=SettleClearedTextDrop());
        Task ocrCleanup=Task.CompletedTask;ScreenshotPreviewCleanup.Attempt(errors,()=>CollectOcrCleanup(ref ocrCleanup));
        var cleanup=Task.WhenAll(translationCleanup,imageCleanup,screenshotCleanup,dropCleanup,ocrCleanup);
        try{await cleanup;}catch(Exception error){errors.Add(cleanup.Exception??error);}
        bool current=!exiting&&revision==resultRevision&&CanReadOriginalSource()&&!lifecycle.Gate.HasRunningGeneration&&
            !lifecycle.Gate.Snapshot.Suspension.HasFlag(KuaiYi.Core.Suspension.Capture);
        if(current){if(errors.Count==0)button.ToolTip="当前内容已清除";else Report("文字已清除；当前任务清理尚未确认",new AggregateException(errors));}
        return errors.Count==0;
    }
}
