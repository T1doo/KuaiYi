namespace KuaiYi.Contracts;
public sealed record SelectionCaptureMetadata(string Status,int RangeCount,int CandidateCount,double ElapsedMs,
    int[]? RuntimeId=null,double[]? RangeRectangles=null,double[]? ControlRectangle=null,long ProcessStartUtcTicks=0,CopyCompatibilityMetadata? Copy=null,SelectionBrokerDiagnostic? Diagnostic=null,long?[]? PhaseTimings=null,long?[]? ProgressDiagnostic=null);

public enum SelectionBrokerStage { Unknown, SourceIdentity, SensitiveCheck, Root, Focus, Hit, Password, FocusAncestors, HitAncestors, Descendants, CandidatePassword, TextPattern, ControlBounds, SelectionRanges, RangeBounds, RangeText, FinalIdentity, RuntimeId, CopyEvidence, Completed, MetadataSerialize }
public enum SelectionBrokerException { None, Com, InvalidOperation, Win32, AccessDenied, ElementUnavailable, Argument, NullReference, NotSupported, TypeLoad, FileLoad }
public sealed record SelectionBrokerDiagnostic(SelectionBrokerStage Stage,SelectionBrokerException Exception,int? HResult=null);
public sealed record SelectionBrokerObservation(string Status,string Stage,string Exception,int? HResult);
public sealed record CopyCompatibilityMetadata(long FocusHandle,int[] RuntimeId,double[] ControlRectangle,double[] SelectionRectangles,bool AutomaticTrusted);
public sealed record ClipboardStageMetadata(string Status,uint Sequence=0,long ProcessStartUtcTicks=0);
public sealed record ClipboardStartMetadata(string Status,long Timestamp=0,uint InputTime=0);
public sealed record ClipboardResultMetadata(string Status,string Recovery,double ObservedMs);
