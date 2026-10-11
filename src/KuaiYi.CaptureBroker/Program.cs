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

internal sealed record CaptureMetadata(string Status, int RangeCount, int CandidateCount, double ElapsedMs);

internal static class Program
{
    [MTAThread]
    public static int Main(string[] args)
    {
        if (args.Length != 5 || !long.TryParse(args[0], out long hwnd) || !uint.TryParse(args[1], out uint pid) ||
            !int.TryParse(args[2], out int x) || !int.TryParse(args[3], out int y) || !Guid.TryParse(args[4], out Guid requestId) || requestId == Guid.Empty) return 2;
        (CaptureMetadata metadata, string text) result;
        var watch = Stopwatch.StartNew();
        try { result = Capture(new nint(hwnd), pid, x, y, watch); }
        catch (ElementNotAvailableException) { result = (new("Stale", 0, 0, watch.Elapsed.TotalMilliseconds), ""); }
        catch (Exception e) when (e is COMException or InvalidOperationException or Win32Exception or UnauthorizedAccessException)
        { result = (new("UnsupportedOrProtected", 0, 0, watch.Elapsed.TotalMilliseconds), ""); }
        using Stream stdout = Console.OpenStandardOutput();
        IpcFrameCodec.WriteAsync(stdout, new(IpcMessageType.Ping, requestId, JsonSerializer.SerializeToUtf8Bytes(result.metadata))).AsTask().GetAwaiter().GetResult();
        IpcFrameCodec.WriteAsync(stdout, new(IpcMessageType.BinaryChunk, requestId, Encoding.UTF8.GetBytes(result.text))).AsTask().GetAwaiter().GetResult();
        return 0;
    }

    private static (CaptureMetadata, string) Capture(nint hwnd, uint pid, int x, int y, Stopwatch watch)
    {
        if (!Current(hwnd, pid)) return (new("Stale", 0, 0, watch.Elapsed.TotalMilliseconds), "");
        string process = Process.GetProcessById((int)pid).ProcessName;
        if (process.StartsWith("KuaiYi", StringComparison.OrdinalIgnoreCase) || process.ToLowerInvariant() is "cmd" or "powershell" or "pwsh" or "windowsterminal" or "conhost" ||
            NativeDesktop.IntegrityLevel(pid) > NativeDesktop.IntegrityLevel((uint)Environment.ProcessId))
            return (new("SensitiveOrElevated", 0, 0, watch.Elapsed.TotalMilliseconds), "");
        var root = AutomationElement.FromHandle(hwnd);
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
        AddAncestors(AutomationElement.FocusedElement);
        AddAncestors(AutomationElement.FromPoint(new Point(x, y)));
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
            var ranges = ((TextPattern)pattern).GetSelection();
            if (ranges is null || ranges.Length == 0) continue;
            if (ranges.Length > 64) return (new("TooLarge", ranges.Length, candidates.Count, watch.Elapsed.TotalMilliseconds), "");
            List<string> parts = []; int units = 0;
            foreach (var range in ranges)
            {
                int remaining = 100_002 - units;
                string part = range.GetText(remaining);
                units += part.Length;
                if (units >= 100_002) return (new("TooLarge", ranges.Length, candidates.Count, watch.Elapsed.TotalMilliseconds), "");
                if (!string.IsNullOrWhiteSpace(part)) parts.Add(part);
            }
            string text = string.Join('\n', parts).Trim();
            if (text.Length == 0) continue;
            if (text.EnumerateRunes().Count() > 50_000 || !ValidUtf16(text)) return (new("TooLargeOrInvalidUnicode", ranges.Length, candidates.Count, watch.Elapsed.TotalMilliseconds), "");
            if (!Current(hwnd, pid)) return (new("Stale", 0, candidates.Count, watch.Elapsed.TotalMilliseconds), "");
            return (new("Success", parts.Count, candidates.Count, watch.Elapsed.TotalMilliseconds), text);
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
