namespace KuaiYi.Core.Settings;
/// <summary>Actual settings publication to runtime gate; never reads a secret or starts a request.</summary>
public sealed class SettingsRuntimeLink:IDisposable
{
    private readonly SettingsCoordinator coordinator;private readonly RuntimeGate gate;
    private int disposed;
    public SettingsRuntimeLink(SettingsCoordinator coordinator,RuntimeGate gate)
    {this.coordinator=coordinator;this.gate=gate;coordinator.Committed+=Apply;}
    public void Initialize(SettingsSnapshot snapshot)=>gate.TryApplyCommittedSettings(snapshot,null,()=>IsCurrent(snapshot));
    private void Apply(SettingsSnapshot snapshot,SettingsChange change)
    {if(gate.TryApplyCommittedSettings(snapshot,change,()=>IsCurrent(snapshot)))gate.SetMode(snapshot.Data.TriggerMode);}
    private bool IsCurrent(SettingsSnapshot snapshot)=>Volatile.Read(ref disposed)==0&&(coordinator.Effective is not {} current||current.Version==snapshot.Version);
    public void Dispose(){if(Interlocked.Exchange(ref disposed,1)==0)coordinator.Committed-=Apply;}
}
