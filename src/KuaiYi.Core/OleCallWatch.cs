namespace KuaiYi.Core;

public enum OleWatchDecision { Ignored, Progress, Completed, Rejected }
public sealed class OleCallWatch(TimeProvider? timeProvider = null)
{
    private readonly OleCallDeadline deadline = new(timeProvider);
    private string? kind;
    private Guid scope;
    private long epoch;
    private bool cleanupConfirmed;
    public bool IsArmed => deadline.IsArmed;
    public bool Expired => deadline.Expired;
    public void Start(Guid instance, long generation) { Reset(); kind = "Startup"; scope = instance; epoch = generation; deadline.Begin(instance, generation, TimeSpan.FromSeconds(10)); }
    public void Reset() { deadline.Reset(); kind = null; scope = Guid.Empty; cleanupConfirmed = false; }
    public OleWatchDecision Accept(Guid id, long generation, string phase, bool cleanupCompleted)
    {
        if (Expired) return OleWatchDecision.Rejected;
        if (phase is "QueryBegin" or "DropBegin" or "ShutdownBegin")
        {
            string next = phase[..^5];
            if (IsArmed && (kind != next || scope != id || epoch != generation || cleanupConfirmed)) return OleWatchDecision.Rejected;
            if (!deadline.Begin(id, generation, TimeSpan.FromMilliseconds(1500))) return OleWatchDecision.Rejected;
            kind = next; scope = id; epoch = generation; return OleWatchDecision.Progress;
        }
        if (id != scope || generation != epoch || !IsArmed) return OleWatchDecision.Ignored;
        if (phase == "CleanupFailed") { cleanupConfirmed = false; return OleWatchDecision.Progress; }
        if (phase == "CleanupComplete" && cleanupCompleted) { cleanupConfirmed = true; return OleWatchDecision.Progress; }
        if (phase == "Ready" && kind == "Startup" || cleanupCompleted && (phase == "QueryEnd" && kind == "Query" || phase == "ShutdownEnd" && kind == "Shutdown" || phase == "DropEnd" && kind == "Drop" && cleanupConfirmed))
        { deadline.Complete(id, generation); kind = null; return OleWatchDecision.Completed; }
        return OleWatchDecision.Progress;
    }
    public bool CanReadPayload(Guid id, long generation) => kind == "Drop" && scope == id && epoch == generation && cleanupConfirmed && !Expired;
}
