using KuaiYi.Core.Settings;
namespace KuaiYi.App;
internal sealed partial class SettingsImportWindow
{
    private sealed class TextPort{public string Text="";public void Clear()=>Text="";}
    private sealed class ButtonPort{public bool IsEnabled;}
    private readonly SettingsImportSession session;private readonly SettingsImportBasis basis;private readonly string path="PUBLIC-file";private readonly Func<string?> currentSavedVersion;
    private readonly TextPort preview=new(),note=new();private readonly ButtonPort apply=new();private bool closed;
    public bool Applied{get;private set;}
    public SettingsImportWindow(SettingsImportSession session,SettingsImportBasis basis,Func<string?> version){this.session=session;this.basis=basis;currentSavedVersion=version;}
    internal Task ReadForTest()=>StartImportRead();internal bool ApplyForTest()=>ApplyImport();internal void CloseForTest()=>DisposeImportView();
    internal string PreviewForTest=>preview.Text;internal bool ApplyEnabledForTest=>apply.IsEnabled;
    internal void ReaderForTest(Func<string,CancellationToken,Task<SettingsImportData>> value)=>readImportFile=value;
}
