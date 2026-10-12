namespace KuaiYi.App;
// The button invokes this command body. Tests link the same production source with explicit control ports;
// they do not claim that WPF events or an interactive Windows desktop executed on Linux.
internal sealed partial class SettingsWindow
{
    private void ApiFormEdited(){if(loading)return;apiTests?.DraftEdited();if(!apiTestTask.IsCompleted)status.Text="配置已编辑，原连接测试已取消；请保存后重新测试。";}
    private void ClearApiKey()
    {
        if(selectedId==null)return;ClearDraftApiKey(selectedId);Mask();password.Clear();
        keyStatus.Text="本草稿已清除引用；取消会保留原密钥。";
    }
    private void ClearDraftApiKey(string profileId){ApiFormEdited();draft.ClearKey(profileId);}
}
