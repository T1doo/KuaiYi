using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using KuaiYi.Core;
using KuaiYi.Windows;
namespace KuaiYi.CompatibilityProbe;

// CI fixture processes only. Assignment precedes resume, including on nested-job runners.
internal sealed class OwnedApplicationJob
{
    private SafeFileHandle? job;
    private SafeProcessHandle? root;
    private bool assigned;
    private static readonly List<OwnedApplicationJob> Unsettled = [];
    private OwnedApplicationJob() { }

    internal static async Task<OwnedApplicationJob> StartAsync(string executable, string[] arguments, string profile)
    {
        var owner = new OwnedApplicationJob();
        try
        {
            owner.job = CreateJobObjectW(0, null);
            if (owner.job.IsInvalid) throw new Win32Exception();
            var limits = new ExtendedLimits { Basic = new BasicLimits { Flags = 0x2000 } };
            if (!SetInformationJobObject(owner.job, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimits>())) throw new Win32Exception();
            var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() };
            var command = new StringBuilder(string.Join(" ", new[] { executable }.Concat(arguments).Select(Quote)));
            // Do not pass CI tokens, credentials, repository or account environment to the browser.
            var environment = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string key in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP", "PATH" })
                if (Environment.GetEnvironmentVariable(key) is { } value) environment[key] = value;
            environment["LOCALAPPDATA"] = Path.Combine(profile, "local");
            environment["APPDATA"] = Path.Combine(profile, "roaming");
            string block = string.Join('\0', environment.Select(p => p.Key + "=" + p.Value)) + "\0\0";
            nint buffer = Marshal.StringToHGlobalUni(block);
            ProcessInformation created;
            try
            {
                if (!CreateProcessW(executable, command, 0, 0, false, 0x4 | 0x400, buffer, profile, ref startup, out created))
                    throw new Win32Exception();
            }
            finally { Marshal.FreeHGlobal(buffer); }
            owner.root = new SafeProcessHandle(created.Process, true);
            using var thread = new SafeWaitHandle(created.Thread, true);
            if (!AssignProcessToJobObject(owner.job, owner.root)) throw new Win32Exception();
            owner.assigned = true;
            if (ResumeThread(thread) == uint.MaxValue) throw new Win32Exception();
            return owner;
        }
        catch
        {
            if (!await owner.StopAsync()) throw new IOException("Owned startup cleanup unconfirmed.");
            throw;
        }
    }
    internal static string Quote(string value)
    {
        if (value.IndexOf('\0') >= 0) throw new ArgumentException("NUL argument.");
        var result = new StringBuilder("\""); int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes).Append(c); slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
    internal bool Owns(nint window)
    {
        if (job == null || job.IsClosed || job.IsInvalid || !NativeDesktop.IsWindow(window)) return false;
        NativeDesktop.GetWindowThreadProcessId(window, out uint pid);
        using var process = OpenProcess(0x1000, false, pid);
        return !process.IsInvalid && IsProcessInJob(process, job, out bool member) && member;
    }
    internal bool OwnsSource(SelectionWindow source)
    {
        if (job == null || job.IsClosed || job.IsInvalid || !NativeDesktop.IsWindow(new(source.Handle))) return false;
        NativeDesktop.GetWindowThreadProcessId(new(source.Handle), out uint actual);
        if (actual != source.ProcessId) return false;
        using var process = OpenProcess(0x1000, false, source.ProcessId);
        if (process.IsInvalid || !IsProcessInJob(process, job, out bool member) || !member || !GetProcessTimes(process, out long created, out _, out _, out _)) return false;
        NativeDesktop.GetWindowThreadProcessId(new(source.Handle), out actual);
        return actual == source.ProcessId && DateTime.FromFileTimeUtc(created).Ticks == source.ProcessStartUtcTicks;
    }
    internal nint FindWindow(string marker)
    {
        nint found = 0;
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window) || !Owns(window)) return true;
            var title = new StringBuilder(256);
            if (GetWindowTextW(window, title, title.Capacity) > 0 && title.ToString().StartsWith(marker, StringComparison.Ordinal) && Owns(window))
            { found = window; return false; }
            return true;
        }, 0);
        return found;
    }
    internal string? CaseMarker(nint window, string prefix)
    {
        if (!Owns(window)) return null;
        var title = new StringBuilder(256);
        if (GetWindowTextW(window, title, title.Capacity) <= 0 || !Owns(window)) return null;
        string value = title.ToString();
        if (!value.StartsWith(prefix, StringComparison.Ordinal)) return null;
        string suffix = value[prefix.Length..];
        return suffix.Length >= 2 ? suffix[..2] : null;
    }
    internal bool Activate(nint window) => Owns(window) && SetForegroundWindow(window) && Owns(window);
    internal async Task<bool> StopAsync()
    {
        bool requested = true;
        if (root != null && !root.IsClosed)
            requested = assigned && job != null ? TerminateJobObject(job, 0) : TerminateProcess(root, 3);
        var watch = Stopwatch.StartNew(); bool settled;
        do
        {
            bool rootExited = root == null || root.IsClosed || WaitForSingleObject(root, 0) == 0;
            bool groupEmpty = !assigned || job != null && QueryInformationJobObject(job, 1, out BasicAccounting accounting, (uint)Marshal.SizeOf<BasicAccounting>(), 0) && accounting.ActiveProcesses == 0;
            settled = rootExited && groupEmpty;
            if (settled) break;
            await Task.Delay(50);
        } while (watch.Elapsed < TimeSpan.FromSeconds(5));
        if (settled) { root?.Dispose(); job?.Dispose(); root = null; job = null; return true; }
        lock (Unsettled) if (!Unsettled.Contains(this)) Unsettled.Add(this);
        _ = requested; // The exit/group observations, not termination's return value, establish cleanup.
        return false;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits { public long ProcessTime, JobTime; public uint Flags; public nuint MinWorking, MaxWorking; public uint ActiveLimit; public nuint Affinity; public uint Priority, Scheduling; }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits { public BasicLimits Basic; public IoCounters Io; public nuint ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory; }
    [StructLayout(LayoutKind.Sequential)] private struct BasicAccounting { public long UserTime, KernelTime, PeriodUserTime, PeriodKernelTime; public uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfo { public int Size; public string? Reserved, Desktop, Title; public uint X, Y, Width, Height, XChars, YChars, Fill, Flags; public ushort ShowWindow, ReservedBytes; public nint ReservedData, Input, Output, Error; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInformation { public nint Process, Thread; public uint ProcessId, ThreadId; }
    private delegate bool EnumWindowsCallback(nint window, nint parameter);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateJobObjectW(nint attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetInformationJobObject(SafeFileHandle job, int kind, ref ExtendedLimits value, uint length);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryInformationJobObject(SafeFileHandle job, int kind, out BasicAccounting value, uint length, nint returned);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AssignProcessToJobObject(SafeFileHandle job, SafeProcessHandle process);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsProcessInJob(SafeProcessHandle process, SafeFileHandle job, [MarshalAs(UnmanagedType.Bool)] out bool member);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetProcessTimes(SafeProcessHandle process, out long creation, out long exit, out long kernel, out long user);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateProcessW(string executable, StringBuilder command, nint processAttributes, nint threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, nint environment, string directory, ref StartupInfo startup, out ProcessInformation created);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(SafeWaitHandle thread);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TerminateJobObject(SafeFileHandle job, uint exit);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TerminateProcess(SafeProcessHandle process, uint exit);
    [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(SafeProcessHandle process, uint milliseconds);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(nint window, StringBuilder title, int size);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(nint window);
}
