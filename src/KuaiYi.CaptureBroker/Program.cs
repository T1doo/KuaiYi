using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using KuaiYi.Contracts;
using KuaiYi.Windows;

namespace KuaiYi.CaptureBroker;

internal static class Program
{
    [MTAThread]
    public static int Main(string[] args)
    {
        if (args.Length is not (5 or 9) || !long.TryParse(args[0], out long hwnd) || !uint.TryParse(args[1], out uint pid) ||
            !int.TryParse(args[2], out int x) || !int.TryParse(args[3], out int y) || !Guid.TryParse(args[4], out Guid requestId) || requestId == Guid.Empty) return 2;
        int downX=x,downY=y;long expectedStart=0;bool automatic=false;
        if(args.Length==9&&(!int.TryParse(args[5],out downX)||!int.TryParse(args[6],out downY)||args[7] is not ("explicit" or "gesture")||!long.TryParse(args[8],out expectedStart)||expectedStart<=0))return 2;
        if(args.Length==9)automatic=args[7]=="gesture";
        (SelectionCaptureMetadata metadata, string text) result;
        var watch = Stopwatch.StartNew();
        try { result = Capture(new nint(hwnd), pid, x, y, downX,downY,automatic,expectedStart,watch); }
        catch (ElementNotAvailableException) { result = (new("Stale", 0, 0, watch.Elapsed.TotalMilliseconds), ""); }
        catch (Exception e) when (e is COMException or InvalidOperationException or Win32Exception or UnauthorizedAccessException)
        { result = (new("UnsupportedOrProtected", 0, 0, watch.Elapsed.TotalMilliseconds), ""); }
        using Stream stdout = Console.OpenStandardOutput();
        IpcFrameCodec.WriteAsync(stdout, new(IpcMessageType.Ping, requestId, JsonSerializer.SerializeToUtf8Bytes(result.metadata))).AsTask().GetAwaiter().GetResult();
        IpcFrameCodec.WriteAsync(stdout, new(IpcMessageType.BinaryChunk, requestId, Encoding.UTF8.GetBytes(result.text))).AsTask().GetAwaiter().GetResult();
        return 0;
    }

    private static (SelectionCaptureMetadata, string) Capture(nint hwnd, uint pid, int x, int y,int downX,int downY,bool automatic,long expectedStart,Stopwatch watch)
    {
        if (!Current(hwnd, pid)) return (new("Stale", 0, 0, watch.Elapsed.TotalMilliseconds), "");
        using var sourceProcess=Process.GetProcessById((int)pid);long started=sourceProcess.StartTime.ToUniversalTime().Ticks;
        if(expectedStart!=0&&started!=expectedStart)return(new("Stale",0,0,watch.Elapsed.TotalMilliseconds),"");
        string process = sourceProcess.ProcessName;
        if (process.StartsWith("KuaiYi", StringComparison.OrdinalIgnoreCase) || process.ToLowerInvariant() is "cmd" or "powershell" or "pwsh" or "windowsterminal" or "conhost" ||
            NativeDesktop.IntegrityLevel(pid) > NativeDesktop.IntegrityLevel((uint)Environment.ProcessId))
            return (new("SensitiveOrElevated", 0, 0, watch.Elapsed.TotalMilliseconds), "");
        var root = AutomationElement.FromHandle(hwnd);
        var focused=AutomationElement.FocusedElement;var hit=AutomationElement.FromPoint(new Point(x,y));
        if(focused?.Current.IsPassword==true||hit.Current.IsPassword)return(new("Sensitive",0,0,watch.Elapsed.TotalMilliseconds),"");
        var walker = TreeWalker.ControlViewWalker;
        List<AutomationElement> candidates = [];
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
        AddAncestors(focused);
        AddAncestors(hit);
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
            if (candidate.Current.IsPassword) return (new("Sensitive", 0, candidates.Count, watch.Elapsed.TotalMilliseconds), "");
            if (!candidate.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)) continue;
            var control=candidate.Current.BoundingRectangle;
            if(automatic&&(control.IsEmpty||!control.Contains(new Point(downX,downY))||!control.Contains(new Point(x,y))))continue;
            var ranges = ((TextPattern)pattern).GetSelection();
            if (ranges is null || ranges.Length == 0) continue;
            if (ranges.Length > 64) return (new("TooLarge", ranges.Length, candidates.Count, watch.Elapsed.TotalMilliseconds), "");
            List<string> parts = [];List<double> rectangles=[]; int units = 0;
            foreach (var range in ranges)
            {
                if(watch.ElapsedMilliseconds>=250)return(new("SoftTimeout",0,candidates.Count,watch.Elapsed.TotalMilliseconds),"");
                double[] bounds=range.GetBoundingRectangles().SelectMany(r=>new[]{r.X,r.Y,r.Width,r.Height}).ToArray();
                if(bounds.Length>1024-rectangles.Count||bounds.Length%4!=0||bounds.Any(v=>!double.IsFinite(v)))return(new("UnsupportedOrProtected",0,candidates.Count,watch.Elapsed.TotalMilliseconds),"");
                rectangles.AddRange(bounds);
                int remaining = 100_002 - units;
                string part = range.GetText(remaining);
                units += part.Length;
                if (units >= 100_002) return (new("TooLarge", ranges.Length, candidates.Count, watch.Elapsed.TotalMilliseconds), "");
                if (!string.IsNullOrWhiteSpace(part)) parts.Add(part);
            }
            string text = string.Join('\n', parts);
            if (string.IsNullOrWhiteSpace(text)) continue;
            if(automatic&&!Enumerable.Range(0,rectangles.Count/4).Any(i=>new Rect(rectangles[i*4],rectangles[i*4+1],rectangles[i*4+2],rectangles[i*4+3]).Contains(new Point(downX,downY))||new Rect(rectangles[i*4],rectangles[i*4+1],rectangles[i*4+2],rectangles[i*4+3]).Contains(new Point(x,y))))continue;
            if (text.EnumerateRunes().Count() > 50_000 || !ValidUtf16(text)) return (new("TooLargeOrInvalidUnicode", ranges.Length, candidates.Count, watch.Elapsed.TotalMilliseconds), "");
            if (!Current(hwnd, pid)||sourceProcess.HasExited||sourceProcess.StartTime.ToUniversalTime().Ticks!=started) return (new("Stale", 0, candidates.Count, watch.Elapsed.TotalMilliseconds), "");
            if(watch.ElapsedMilliseconds>=250)return(new("SoftTimeout",0,candidates.Count,watch.Elapsed.TotalMilliseconds),"");
            int[] runtime=candidate.GetRuntimeId();if(runtime.Length is 0 or >64)return(new("UnsupportedOrProtected",0,candidates.Count,watch.Elapsed.TotalMilliseconds),"");
            return (new("Success", parts.Count, candidates.Count, watch.Elapsed.TotalMilliseconds,runtime,rectangles.ToArray(),control.IsEmpty?null:[control.X,control.Y,control.Width,control.Height],started), text);
        }
        return (new(watch.ElapsedMilliseconds >= 250 ? "SoftTimeout" : "EmptyOrUnsupported", 0, candidates.Count, watch.Elapsed.TotalMilliseconds), "");
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
