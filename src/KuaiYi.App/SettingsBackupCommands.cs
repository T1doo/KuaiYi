using KuaiYi.Core.Settings;
namespace KuaiYi.App;

internal sealed partial class SettingsBackupWindow
{
    private Task backupExportTask=Task.CompletedTask;
    private Func<string,ReadOnlyMemory<byte>,CancellationToken,Task<IPreparedDisplayPreferencesExport>> prepareBackupFile=SettingsBackupFiles.PrepareNewAsync;
    public bool ExportPending=>!backupExportTask.IsCompleted||session.Busy;
    private Task ExportBackup(string path)
    {
        if(closed||ExportPending||session.Preview is not {} preview)return Task.CompletedTask;
        export.IsEnabled=false;
        return backupExportTask=RunBackupExport(path,preview);
    }
    private async Task RunBackupExport(string path,SettingsBackupPreview preview)
    {
        await Task.Yield(); // Track the original command before a file port or notification can reenter.
        try
        {
            var result=await session.ExportAsync(preview,(bytes,ct)=>prepareBackupFile(path,bytes,ct));
            if(!closed)note.Text=result.Status switch
            {
                SettingsBackupExportStatus.Published=>"所示普通配置已导出到新文件；草稿和已保存设置均未更改。",
                SettingsBackupExportStatus.PublishedCleanupUnconfirmed=>"所示配置已导出；文件资源或取消清理尚未确认。",
                SettingsBackupExportStatus.Cancelled=>"导出已取消；没有发布配置文件。",
                _=>"配置未导出；请选择不存在且可写的本地文件。无法确认归属的暂存文件保留。"
            };
        }
        finally{if(!closed)export.IsEnabled=session.Preview!=null&&!session.Busy;}
    }
    private void CancelBackup()
    {session.Invalidate();text.Clear();export.IsEnabled=false;note.Text="未提交导出已撤销；实际文件任务仍会结算，已完成的文件保留。可关闭后重新预览。";}
    public Task SettleForCloseAsync(){CancelBackup();return backupExportTask;}
    private void DisposeBackupView(){closed=true;CancelBackup();session.Dispose();}
}
