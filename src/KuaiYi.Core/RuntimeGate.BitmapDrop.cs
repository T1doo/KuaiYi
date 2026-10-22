namespace KuaiYi.Core;
public readonly record struct LocalBitmapDropTicket(long Epoch);
public sealed partial class RuntimeGate
{
    public bool TryBeginLocalBitmapDrop(out LocalBitmapDropTicket ticket)
    {lock(sync){ticket=new(epoch);return activation==Activation.Off&&mode==TriggerMode.Drop&&suspension==Suspension.None&&presentation!=Presentation.Hidden&&explicitCaptureOwner==null;}}
    public bool IsLocalBitmapDropCurrent(LocalBitmapDropTicket ticket)
    {lock(sync)return activation==Activation.Off&&mode==TriggerMode.Drop&&suspension==Suspension.None&&presentation!=Presentation.Hidden&&explicitCaptureOwner==null&&ticket.Epoch==epoch;}
    public bool TryCommitLocalBitmapDrop(LocalBitmapDropTicket ticket,Func<bool> current,Action commit)
    {lock(sync){if(!IsLocalBitmapDropCurrent(ticket)||!current()||!IsLocalBitmapDropCurrent(ticket))return false;commit();return true;}}
}
