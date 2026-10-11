namespace KuaiYi.Core;
/// <summary>Only a fresh explicit floating-button action grants this acquisition scope. Other local workflows cannot grant it.</summary>
public sealed class OlePresentationConsent
{
    private int granted;
    public bool IsGranted=>Volatile.Read(ref granted)==1;
    public void Revoke()=>Interlocked.Exchange(ref granted,0);
    public bool TryEnable(RuntimeGate gate)
    {
        if(gate.Snapshot.Suspension!=Suspension.None)return false;
        gate.SetMode(TriggerMode.Drop);bool allowed=gate.TryEnableLocalOperation(true);Interlocked.Exchange(ref granted,allowed?1:0);return allowed;
    }
    public bool Allows(RuntimeGate gate,bool visible)
    {
        var current=gate.Snapshot;
        if(!visible||current.Activation!=Activation.On||current.Mode!=TriggerMode.Drop)Revoke();
        return IsGranted&&visible&&current.Activation==Activation.On&&current.Mode==TriggerMode.Drop&&current.Suspension==Suspension.None;
    }
}
