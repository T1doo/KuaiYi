using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using KuaiYi.Contracts;
using KuaiYi.Core;
using KuaiYi.Windows;

namespace KuaiYi.CaptureBroker;

internal static class Program
{
    [MTAThread]
    public static int Main(string[] args)
    {
        if (args.Length is not (5 or 9 or 10) || !long.TryParse(args[0], out long hwnd) || !uint.TryParse(args[1], out uint pid) ||
            !int.TryParse(args[2], out int x) || !int.TryParse(args[3], out int y) || !Guid.TryParse(args[4], out Guid requestId) || requestId == Guid.Empty) return 2;
        int downX=x,downY=y;long expectedStart=0;bool automatic=false;
        if(args.Length>=9&&(!int.TryParse(args[5],out downX)||!int.TryParse(args[6],out downY)||args[7] is not ("explicit" or "gesture")||!long.TryParse(args[8],out expectedStart)||expectedStart<=0))return 2;
        if(args.Length==10&&args[9]!="--boundary-marker")return 2;
        if(args.Length>=9)automatic=args[7]=="gesture";
        (SelectionCaptureMetadata metadata, string text) result;
        var watch = Stopwatch.StartNew();
        using var boundary=args.Length==10?SelectionBoundaryMap.TryOpen(requestId):null;
        var diagnostic = new DiagnosticContext(boundary);
        try { boundary?.Reach(SelectionBoundary.CaptureReturned,SelectionBoundaryPosition.Enter);result = Capture(new nint(hwnd), pid, x, y, downX,downY,automatic,expectedStart,watch,diagnostic);boundary?.Reach(SelectionBoundary.CaptureReturned,SelectionBoundaryPosition.Returned); }
        catch (ElementNotAvailableException error) { result = (new("Stale", 0, 0, watch.Elapsed.TotalMilliseconds,Diagnostic:diagnostic.Failure(error)), ""); }
        catch (Exception e) when (e is COMException or InvalidOperationException or Win32Exception or UnauthorizedAccessException)
        { result = (new("UnsupportedOrProtected", 0, 0, watch.Elapsed.TotalMilliseconds,Diagnostic:diagnostic.Failure(e)), ""); }
        catch (Exception error) when (error is ArgumentException or NullReferenceException or NotSupportedException or TypeLoadException or FileLoadException)
        { result = (new("BrokerFailure",0,0,watch.Elapsed.TotalMilliseconds,Diagnostic:diagnostic.Failure(error)),""); }
        result.metadata = result.metadata with { Diagnostic = result.metadata.Diagnostic ?? new(diagnostic.Stage,SelectionBrokerException.None) };
        boundary?.Reach(SelectionBoundary.MetadataPrepared,SelectionBoundaryPosition.Enter);
        var prepared=SelectionCaptureOutput.Prepare(result.metadata,result.text,watch.Elapsed.TotalMilliseconds);
        boundary?.Reach(SelectionBoundary.MetadataPrepared,SelectionBoundaryPosition.Returned);
        boundary?.Reach(SelectionBoundary.FramesWritten,SelectionBoundaryPosition.Enter);
        using Stream stdout = Console.OpenStandardOutput();
        IpcFrameCodec.WriteAsync(stdout, new(IpcMessageType.Ping, requestId, prepared.Metadata)).AsTask().GetAwaiter().GetResult();
        IpcFrameCodec.WriteAsync(stdout, new(IpcMessageType.BinaryChunk, requestId, Encoding.UTF8.GetBytes(prepared.Text))).AsTask().GetAwaiter().GetResult();
        boundary?.Reach(SelectionBoundary.FramesWritten,SelectionBoundaryPosition.Returned);
        boundary?.Reach(SelectionBoundary.MainReturning,SelectionBoundaryPosition.Enter);
        return 0;
    }

    private sealed class DiagnosticContext(SelectionBoundaryMap? boundary)
    {
        public void Reach(SelectionBoundary stage,SelectionBoundaryPosition position)=>boundary?.Reach(stage,position);
        public Action<int,bool>? Integrity(bool own)=>boundary==null?null:(stage,returned)=>Reach((SelectionBoundary)((own?(int)SelectionBoundary.OwnIntegritySid:(int)SelectionBoundary.SourceIntegritySid)+stage),returned?SelectionBoundaryPosition.Returned:SelectionBoundaryPosition.Enter);
        public SelectionBrokerStage Stage = SelectionBrokerStage.SourceIdentity;
        public SelectionBrokerDiagnostic Failure(Exception error) => new(Stage,error switch
        {
            ElementNotAvailableException => SelectionBrokerException.ElementUnavailable,
            COMException => SelectionBrokerException.Com,
            Win32Exception => SelectionBrokerException.Win32,
            UnauthorizedAccessException => SelectionBrokerException.AccessDenied,
            ArgumentException => SelectionBrokerException.Argument,
            NullReferenceException => SelectionBrokerException.NullReference,
            NotSupportedException => SelectionBrokerException.NotSupported,
            TypeLoadException => SelectionBrokerException.TypeLoad,
            FileLoadException => SelectionBrokerException.FileLoad,
            _ => SelectionBrokerException.InvalidOperation
        },error.HResult);
    }
    private static (SelectionCaptureMetadata, string) Capture(nint hwnd, uint pid, int x, int y,int downX,int downY,bool automatic,long expectedStart,Stopwatch watch,DiagnosticContext diagnostic)
    {
        if (!Current(hwnd, pid)) return (new("Stale", 0, 0, watch.Elapsed.TotalMilliseconds), "");
        using var sourceProcess=Process.GetProcessById((int)pid);long started=sourceProcess.StartTime.ToUniversalTime().Ticks;
        if(expectedStart!=0&&started!=expectedStart)return(new("Stale",0,0,watch.Elapsed.TotalMilliseconds),"");
        string process = sourceProcess.ProcessName;
        diagnostic.Stage=SelectionBrokerStage.SensitiveCheck;
        if (process.StartsWith("KuaiYi", StringComparison.OrdinalIgnoreCase) || process.ToLowerInvariant() is "cmd" or "powershell" or "pwsh" or "windowsterminal" or "conhost" ||
            NativeDesktop.IntegrityLevel(pid,diagnostic.Integrity(false)) > NativeDesktop.IntegrityLevel((uint)Environment.ProcessId,diagnostic.Integrity(true)))
            return (new("SensitiveOrElevated", 0, 0, watch.Elapsed.TotalMilliseconds), "");
        diagnostic.Stage=SelectionBrokerStage.Root;var root = AutomationElement.FromHandle(hwnd);
        diagnostic.Stage=SelectionBrokerStage.Focus;var focused=AutomationElement.FocusedElement;
        diagnostic.Stage=SelectionBrokerStage.Hit;var hit=AutomationElement.FromPoint(new Point(x,y));
        diagnostic.Stage=SelectionBrokerStage.Password;
        if(focused?.Current.IsPassword==true||hit.Current.IsPassword)return(new("Sensitive",0,0,watch.Elapsed.TotalMilliseconds),"");
        var walker = TreeWalker.ControlViewWalker;
        List<AutomationElement> candidates = [];
        List<double> copyRanges=[];
        void AddAncestors(AutomationElement? element)
        {
            List<AutomationElement> path = [];
            for (int level = 0; level < 8 && element is not null && watch.ElapsedMilliseconds < 250; level++)
            {
                if (element.Current.ProcessId != (int)pid) break;
                path.Add(element);
                // A process may own several windows. PID equality alone does not bind the selection to this HWND.
                if (element.Equals(root)) { candidates.AddRange(path.Take(64 - candidates.Count)); break; }
                element = walker.GetParent(element);
            }
        }
        diagnostic.Stage=SelectionBrokerStage.FocusAncestors;AddAncestors(focused);
        diagnostic.Stage=SelectionBrokerStage.HitAncestors;AddAncestors(hit);
        diagnostic.Stage=SelectionBrokerStage.Descendants;
        Queue<AutomationElement> queue = new(); queue.Enqueue(root);
        while (queue.Count > 0 && candidates.Count < 64 && watch.ElapsedMilliseconds < 250)
        {
            var parent = queue.Dequeue(); candidates.Add(parent);
            for (var child = walker.GetFirstChild(parent); child is not null && queue.Count + candidates.Count < 64 && watch.ElapsedMilliseconds < 250; child = walker.GetNextSibling(child))
                if (child.Current.ProcessId == (int)pid) queue.Enqueue(child);
        }
        foreach (var candidate in candidates.Take(64))
        {
            if (watch.ElapsedMilliseconds >= 250) break;
            if (!Current(hwnd, pid)) return (new("Stale", 0, candidates.Count, watch.Elapsed.TotalMilliseconds), "");
            diagnostic.Stage=SelectionBrokerStage.CandidatePassword;
            if (candidate.Current.IsPassword) return (new("Sensitive", 0, candidates.Count, watch.Elapsed.TotalMilliseconds), "");
            diagnostic.Stage=SelectionBrokerStage.TextPattern;
            if (!candidate.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)) continue;
            diagnostic.Stage=SelectionBrokerStage.ControlBounds;var control=candidate.Current.BoundingRectangle;
            if(automatic&&(control.IsEmpty||!control.Contains(new Point(downX,downY))||!control.Contains(new Point(x,y))))continue;
            diagnostic.Stage=SelectionBrokerStage.SelectionRanges;diagnostic.Reach(SelectionBoundary.GetSelection,SelectionBoundaryPosition.Enter);var ranges = ((TextPattern)pattern).GetSelection();diagnostic.Reach(SelectionBoundary.GetSelection,SelectionBoundaryPosition.Returned);
            if (ranges is null || ranges.Length == 0) continue;
            if (ranges.Length > 64) return (new("TooLarge", ranges.Length, candidates.Count, watch.Elapsed.TotalMilliseconds), "");
            List<string> parts = [];List<double> rectangles=[]; int units = 0, observedRectangles = 0;
            foreach (var range in ranges)
            {
                if(watch.ElapsedMilliseconds>=250)return(new("SoftTimeout",0,candidates.Count,watch.Elapsed.TotalMilliseconds),"");
                diagnostic.Stage=SelectionBrokerStage.RangeBounds;diagnostic.Reach(SelectionBoundary.GetBoundingRectangles,SelectionBoundaryPosition.Enter);var bounds=range.GetBoundingRectangles();diagnostic.Reach(SelectionBoundary.GetBoundingRectangles,SelectionBoundaryPosition.Returned);
                if(!SelectionRangeBounds.TryAppend(bounds?.Select(r=>new SelectionRangeRectangle(r.X,r.Y,r.Width,r.Height)),rectangles,ref observedRectangles))return(new("UnsupportedOrProtected",0,candidates.Count,watch.Elapsed.TotalMilliseconds),"");
                int remaining = 100_002 - units;
                diagnostic.Stage=SelectionBrokerStage.RangeText;diagnostic.Reach(SelectionBoundary.GetText,SelectionBoundaryPosition.Enter);string part = range.GetText(remaining);diagnostic.Reach(SelectionBoundary.GetText,SelectionBoundaryPosition.Returned);
                units += part.Length;
                if (units >= 100_002) return (new("TooLarge", ranges.Length, candidates.Count, watch.Elapsed.TotalMilliseconds), "");
                if (!string.IsNullOrWhiteSpace(part)) parts.Add(part);
            }
            string text = string.Join('\n', parts);
            if (string.IsNullOrWhiteSpace(text))
            {
                if(candidate.Equals(focused)&&!control.IsEmpty&&control.Contains(new Point(downX,downY))&&control.Contains(new Point(x,y))&&Enumerable.Range(0,rectangles.Count/4).Any(i=>new Rect(rectangles[i*4],rectangles[i*4+1],rectangles[i*4+2],rectangles[i*4+3]).Contains(new Point(downX,downY))||new Rect(rectangles[i*4],rectangles[i*4+1],rectangles[i*4+2],rectangles[i*4+3]).Contains(new Point(x,y))))copyRanges=rectangles;
                continue;
            }
            if(automatic&&!Enumerable.Range(0,rectangles.Count/4).Any(i=>new Rect(rectangles[i*4],rectangles[i*4+1],rectangles[i*4+2],rectangles[i*4+3]).Contains(new Point(downX,downY))||new Rect(rectangles[i*4],rectangles[i*4+1],rectangles[i*4+2],rectangles[i*4+3]).Contains(new Point(x,y))))continue;
            if (text.EnumerateRunes().Count() > 50_000 || !ValidUtf16(text)) return (new("TooLargeOrInvalidUnicode", ranges.Length, candidates.Count, watch.Elapsed.TotalMilliseconds), "");
            diagnostic.Stage=SelectionBrokerStage.FinalIdentity;
            if (!Current(hwnd, pid)||sourceProcess.HasExited||sourceProcess.StartTime.ToUniversalTime().Ticks!=started) return (new("Stale", 0, candidates.Count, watch.Elapsed.TotalMilliseconds), "");
            if(watch.ElapsedMilliseconds>=250)return(new("SoftTimeout",0,candidates.Count,watch.Elapsed.TotalMilliseconds),"");
            diagnostic.Stage=SelectionBrokerStage.RuntimeId;int[] runtime=candidate.GetRuntimeId();if(runtime.Length is 0 or >64)return(new("UnsupportedOrProtected",0,candidates.Count,watch.Elapsed.TotalMilliseconds),"");
            diagnostic.Stage=SelectionBrokerStage.Completed;
            return (new("Success", parts.Count, candidates.Count, watch.Elapsed.TotalMilliseconds,runtime,rectangles.ToArray(),control.IsEmpty?null:[control.X,control.Y,control.Width,control.Height],started), text);
        }
        diagnostic.Stage=SelectionBrokerStage.CopyEvidence;CopyCompatibilityMetadata? copy=null;
        if(watch.ElapsedMilliseconds<250&&Current(hwnd,pid)&&!sourceProcess.HasExited&&focused!=null&&candidates.Contains(focused)&&AutomationElement.FocusedElement.Equals(focused)&&focused.Current.ProcessId==(int)pid&&!focused.Current.IsPassword&&focused.Current.IsEnabled)
        {
            var info=focused.Current;long nativeFocus=CopyInputGuard.FocusHandle(hwnd.ToInt64());var rectangle=info.BoundingRectangle;int[] runtime=focused.GetRuntimeId();
            if(info.NativeWindowHandle!=0&&info.NativeWindowHandle==nativeFocus&&!rectangle.IsEmpty&&runtime.Length is >0 and <=64&&(info.ControlType==ControlType.Edit||info.ControlType==ControlType.Document||info.ControlType==ControlType.Text))copy=new(nativeFocus,runtime,[rectangle.X,rectangle.Y,rectangle.Width,rectangle.Height],copyRanges.ToArray(),copyRanges.Count>0);
        }
        if(!Current(hwnd,pid)||sourceProcess.HasExited||sourceProcess.StartTime.ToUniversalTime().Ticks!=started)return(new("Stale",0,candidates.Count,watch.Elapsed.TotalMilliseconds),"");
        diagnostic.Stage=SelectionBrokerStage.Completed;
        return (new(watch.ElapsedMilliseconds >= 250 ? "SoftTimeout" : "EmptyOrUnsupported", 0, candidates.Count, watch.Elapsed.TotalMilliseconds,ProcessStartUtcTicks:started,Copy:watch.ElapsedMilliseconds<250?copy:null), "");
    }
    private static bool Current(nint hwnd, uint pid) => NativeDesktop.IsWindow(hwnd) && NativeDesktop.GetForegroundWindow() == hwnd &&
        NativeDesktop.GetWindowThreadProcessId(hwnd, out uint actual) != 0 && actual == pid;
    private static bool ValidUtf16(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i])) { if (++i >= text.Length || !char.IsLowSurrogate(text[i])) return false; }
            else if (char.IsLowSurrogate(text[i])) return false;
        }
        return true;
    }
}
