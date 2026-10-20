using KuaiYi.Core.Settings;
namespace KuaiYi.App;

internal sealed partial class SettingsImportWindow
{
    private Task importReadTask=Task.CompletedTask;private bool importWithdrawn;
    private Func<string,CancellationToken,Task<SettingsImportData>> readImportFile=SettingsImportFiles.ReadAsync;
    public bool ReadPending=>!importReadTask.IsCompleted||session.Busy;
    private Task StartImportRead()
    {
        if(closed||importWithdrawn||ReadPending)return Task.CompletedTask;
        return importReadTask=ReadImport();
    }
    private async Task ReadImport()
    {
        await Task.Yield(); // Publish the original UI task before the file reader can reenter.
        if(closed||importWithdrawn)return;
        var result=await session.ReadAsync(basis,ct=>readImportFile(path,ct));
        if(closed)return;
        if(result.Status==SettingsImportStatus.Preview&&result.Preview is {} plan&&ReferenceEquals(session.Preview,plan)&&currentSavedVersion()==basis.SavedVersion)
        {
            preview.Text=plan.Text;apply.IsEnabled=true;
            note.Text=$"已验证完整配置；新增 {plan.AddedProfiles} 个配置档，原配置档、凭据、规则和已选默认配置保留。\n此预览隐藏本机凭据引用、请求头、授权和位置；这些本机数据不从文件导入。导入配置档使用新 ID、无凭据/头，能力重置为未知。\n普通偏好和热键组合将进入草稿，保存时才检查注册并生效；不启用、不发送。"+
                (plan.RequiresHttpConfirmation?"\n包含 HTTP 地址；其风险开关已清除，应用后须在原 API 高级设置手动重新确认，否则保存会被原校验拒绝。":"");
        }
        else{preview.Clear();apply.IsEnabled=false;note.Text=result.Status==SettingsImportStatus.Cancelled?"读取已取消，未修改草稿或配置。":"未能导入：文件须为本版完整、无敏感字段的普通配置备份，且当前设置须保持不变。没有修改草稿或配置。";}
    }
    private bool ApplyImport()
    {
        if(closed||ReadPending||session.Preview is not {} plan||currentSavedVersion() is not {} version||!session.Apply(plan,true,version))
        {note.Text="预览或目标草稿已失效，请取消后重新打开导入。";apply.IsEnabled=false;return false;}
        Applied=true;return true;
    }
    public Task SettleForCloseAsync(){importWithdrawn=true;session.Invalidate();preview.Clear();apply.IsEnabled=false;return importReadTask;}
    private void DisposeImportView(){closed=true;importWithdrawn=true;session.Dispose();preview.Clear();apply.IsEnabled=false;}
}
