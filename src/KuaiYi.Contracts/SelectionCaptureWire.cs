namespace KuaiYi.Contracts;
public sealed record SelectionCaptureMetadata(string Status,int RangeCount,int CandidateCount,double ElapsedMs,
    int[]? RuntimeId=null,double[]? RangeRectangles=null,double[]? ControlRectangle=null,long ProcessStartUtcTicks=0,CopyCompatibilityMetadata? Copy=null);
public sealed record CopyCompatibilityMetadata(long FocusHandle,int[] RuntimeId,double[] ControlRectangle,double[] SelectionRectangles,bool AutomaticTrusted);
public sealed record ClipboardStageMetadata(string Status,uint Sequence=0,long ProcessStartUtcTicks=0);
public sealed record ClipboardStartMetadata(string Status,long Timestamp=0,uint InputTime=0);
public sealed record ClipboardResultMetadata(string Status,string Recovery,double ObservedMs);
