namespace KuaiYi.OleTransfer;
/// <summary>Receiver-local admission. Elapsed time is measured by the receiver's own original group clock; no cross-process timestamp comparison.</summary>
public static class OleReceiverCommitAdmission
{
    public static readonly TimeSpan Budget=TimeSpan.FromMilliseconds(1500);
    public static bool CanCommit(Func<TimeSpan> elapsed,Func<bool> gateCurrent,Func<bool> activationCurrent,Func<bool> sourceCurrent,CancellationToken cancellation=default)
    {
        if(cancellation.IsCancellationRequested||!gateCurrent()||!activationCurrent()||elapsed()>=Budget||!sourceCurrent())return false;
        // Source validation may take time or observe a concurrent revoke. Recheck all local eligibility after it returns.
        return !cancellation.IsCancellationRequested&&gateCurrent()&&activationCurrent()&&elapsed()<Budget;
    }
}
