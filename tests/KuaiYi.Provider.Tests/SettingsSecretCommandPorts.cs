using KuaiYi.Core.Settings;
using KuaiYi.Providers;
namespace KuaiYi.App;
// Test-only ports for the exact production command source. Empty Clear produces no change event;
// this models the reported zero-delta case, rather than purporting to execute WPF on Linux.
internal sealed partial class SettingsWindow
{
    internal sealed class PasswordPort
    {public string Password="";public int Changes;public Action? Changed;public void Clear(){if(Password.Length==0)return;Password="";Changes++;Changed?.Invoke();}}
    internal sealed class TextPort{public string Text="";}
    private readonly SettingsDraft draft;private readonly ApiConnectionTestWorkflow? apiTests;
    private string? selectedId;private readonly PasswordPort password=new();private readonly TextPort status=new(),keyStatus=new();
    private readonly bool loading=false;private Task apiTestTask=Task.CompletedTask;
    internal SettingsWindow(SettingsDraft draft,ApiConnectionTestWorkflow? apiTests,string? profileId="p")
    {this.draft=draft;this.apiTests=apiTests;selectedId=profileId;password.Changed=ApiFormEdited;}
    private void Mask(){} // UI presentation port; no saved key ever loaded into it.
    internal void InvokeClearButtonCommand()=>ClearApiKey();
    internal void InvokePresetSecretCommand()=>ClearDraftApiKey(selectedId!);
    internal void ObserveRunning(Task task)=>apiTestTask=task;
    internal int PasswordChanges=>password.Changes;
    internal string PasswordText=>password.Password;
}
