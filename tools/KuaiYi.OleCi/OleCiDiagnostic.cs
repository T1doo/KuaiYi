using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace KuaiYi.App;

public enum OleCiTraceEvent
{
    ReceiverReady, ReceiverQueryBegin, ReceiverQueryEndCopy, ReceiverQueryEndNoCopy, ReceiverDropBegin,
    SourceReady, SourceDragBegin, SourceDragReturn, SourceQuery, SourceGetData, SourceGetDataBlocking, SourceGetDataReturned,
    OwnedPressDown, OwnedPressUp, OwnedInputCleanupSettled, OwnedInputCleanupFailed,
    SourceCleanupBegin, SourceCleanupCompleted, SourceCleanupFailed
}

// CI-only checkpoint. It is an observation, never a substitute for the final contract receipt.
public sealed class OleCiOwnerDiagnostic
{
    public string Kind { get; set; } = "Receiver";
    public string ProcessState { get; set; } = "UNKNOWN";
    public int? ExitCode { get; set; }
    public string Run { get; set; } = "UNKNOWN";
    public string Control { get; set; } = "UNKNOWN";
    public string MetadataRead { get; set; } = "UNKNOWN";
    public string ErrorRead { get; set; } = "UNKNOWN";
    public string CloseWrite { get; set; } = "UNKNOWN";
    public string CloseFlush { get; set; } = "UNKNOWN";
    public string ExitWait { get; set; } = "UNKNOWN";
    public string RegistrarTask { get; set; } = "UNKNOWN";
    public string RegistrarStage { get; set; } = "UNKNOWN";
    public string RegistrarFailureStage { get; set; } = "UNKNOWN";
    public OleCiStartupFailure? StartupFailure { get; set; }
}
public sealed class OleCiStartupFailure
{
    public string Stage { get; set; } = "UNKNOWN";
    public string Exception { get; set; } = "UNKNOWN";
    public int HResult { get; set; }
}
public sealed class OleCiDiagnostic
{
    private readonly string path;
    private readonly object sync = new();
    private string stage = "Entry", failureStage = "UNKNOWN", failure = "UNKNOWN", cleanup = "UNKNOWN";
    private int? hresult;
    private readonly List<OleCiTraceEvent> trace = new();
    private string drop = "UNKNOWN", getData = "UNKNOWN", cancel = "UNKNOWN", recovery = "UNKNOWN", close = "UNKNOWN";
    public Func<OleCiOwnerDiagnostic[]> Owners { private get; set; } = () => Array.Empty<OleCiOwnerDiagnostic>();
    public OleCiDiagnostic(string path) { this.path = path; }
    // Unique observations, not a complete event timeline or proof that absent events never occurred.
    public void Trace(OleCiTraceEvent value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        lock(sync) { if (!trace.Contains(value)) { trace.Add(value); Save(); } }
    }
    public static string TaskState(Task? task) => task == null ? "UNKNOWN" : !task.IsCompleted ? "PENDING" : task.IsCanceled ? "CANCELED" : task.IsFaulted ? "FAULTED" : "RAN_TO_COMPLETION";
    public static string StatusState(TaskStatus? status) => status==null ? "UNKNOWN" : status==TaskStatus.RanToCompletion ? "RAN_TO_COMPLETION" : status==TaskStatus.Faulted ? "FAULTED" : status==TaskStatus.Canceled ? "CANCELED" : "PENDING";
    public static string TasksState(Task[] tasks)
    {
        if(tasks.Length==0)return "UNKNOWN";
        if(tasks.Any(task=>!task.IsCompleted))return "PENDING";
        if(tasks.Any(task=>task.IsFaulted))return "FAULTED";
        if(tasks.Any(task=>task.IsCanceled))return "CANCELED";
        return "RAN_TO_COMPLETION";
    }
    public static string Code(Exception error) => error is TimeoutException ? "Timeout" : error is OperationCanceledException ? "Cancelled" : error is InvalidDataException ? "InvalidData" : error is IOException ? "Io" : error is UnauthorizedAccessException ? "Unauthorized" : error is InvalidOperationException ? "InvalidOperation" : "Other";
    public void At(string value) { lock (sync) { stage = value; Save(); } }
    public void Observed(string value)
    {
        lock (sync)
        {
            switch (value) { case "Drop": drop="OBSERVED"; break; case "GetData": getData="OBSERVED"; break; case "Cancel": cancel="OBSERVED"; break; case "Recovery": recovery="OBSERVED"; break; case "Close": close="OBSERVED"; break; default: throw new ArgumentException("Unknown observation"); }
            Save();
        }
    }
    public void Fail(Exception error, bool duringCleanup=false, string? atStage=null)
    {
        lock (sync)
        {
            if (duringCleanup) { if (cleanup=="UNKNOWN") cleanup=Code(error); }
            else if (failureStage=="UNKNOWN") { failureStage=atStage??stage;failure=Code(error);hresult=error.HResult; }
            Save();
        }
    }
    public void Snapshot() { lock (sync) Save(); }
    private void Save()
    {
        // No raw errors, identifiers, paths, COM bytes or content enter this payload.
        OleCiOwnerDiagnostic[] owners;
        try { owners=Owners().Take(8).ToArray(); } catch { owners=Array.Empty<OleCiOwnerDiagnostic>(); }
        var value=new { Version=1,Boundary="Probe",Stage=stage,FailureStage=failureStage,FailureCode=failure,HResult=hresult,CleanupCode=cleanup,ProcessState="UNKNOWN",ChildExitCode=(int?)null,CommandExitCode=(int?)null,StdoutTask="UNKNOWN",StderrTask="UNKNOWN",ReceiptState="UNKNOWN",DropObserved=drop,GetDataObserved=getData,CancelConfirmed=cancel,RecoveryConfirmed=recovery,CloseConfirmed=close,Trace=trace.Select(item=>item.ToString()).ToArray(),Owners=owners };
        try
        {
            byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value);
            if(bytes.Length>32768)throw new InvalidDataException("Checkpoint bound");
            File.WriteAllBytes(path+".tmp",bytes);File.Move(path+".tmp",path,true);
        }
        catch { if(cleanup=="UNKNOWN")cleanup="DiagnosticWriteFailed"; } // Preserve first cleanup failure; missing file stays UNKNOWN.
    }
}
