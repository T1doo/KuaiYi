namespace KuaiYi.Contracts;
public sealed record SelectionCaptureMetadata(string Status,int RangeCount,int CandidateCount,double ElapsedMs,
    int[]? RuntimeId=null,double[]? RangeRectangles=null,double[]? ControlRectangle=null,long ProcessStartUtcTicks=0);
