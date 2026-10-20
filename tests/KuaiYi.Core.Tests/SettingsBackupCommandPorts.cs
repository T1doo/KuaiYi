using KuaiYi.Core.Settings;
namespace KuaiYi.App;
// The linked production command owns the actual file task; only WPF controls are replaced.
internal sealed partial class SettingsBackupWindow
{
    private sealed class TextPort{public string Text="";public void Clear()=>Text="";}
    private sealed class ButtonPort{public bool IsEnabled=true;}
    private readonly SettingsBackupSession session;private bool closed;
    private readonly TextPort note=new(),text=new();private readonly ButtonPort export=new();
    public SettingsBackupWindow(SettingsBackupSession session){this.session=session;text.Text=session.Preview!.Text;}
    internal Task ExportForTest(string path)=>ExportBackup(path);
    internal void CancelForTest()=>CancelBackup();internal void DisposeForTest()=>DisposeBackupView();
    internal string StatusForTest=>note.Text;internal string PreviewForTest=>text.Text;internal bool ExportEnabledForTest=>export.IsEnabled;
    internal void FilePortForTest(Func<string,ReadOnlyMemory<byte>,CancellationToken,Task<IPreparedDisplayPreferencesExport>> value)=>prepareBackupFile=value;
}
