using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

// Read-only capability inventory for CI. Never changes desktop, token, DPI context or UAC.
public static class KuaiYiCiWindowsFacts
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct OsVersion
    {
        public uint Size, Major, Minor, Build, Platform;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string ServicePack;
        public ushort ServicePackMajor, ServicePackMinor, SuiteMask;
        public byte ProductType, Reserved;
    }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct MonitorInfo { public uint Size; public Rect Monitor, Work; public uint Flags; }
    public sealed class MonitorFact
    {
        public Rect Bounds, WorkArea;
        public uint DpiX, DpiY;
        public int DpiHResult;
        public bool Primary;
    }
    private delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data);
    [DllImport("ntdll.dll", CharSet = CharSet.Unicode)] private static extern int RtlGetVersion(ref OsVersion info);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll")] private static extern IntPtr GetProcessWindowStation();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder value, uint length, out uint needed);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")] private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("dwmapi.dll")] private static extern int DwmIsCompositionEnabled(out bool enabled);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(IntPtr token, int kind, IntPtr buffer, int length, out int needed);
    [DllImport("advapi32.dll")] private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")] private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
    public static uint IntegrityLevel(IntPtr token)
    {
        GetTokenInformation(token, 25, IntPtr.Zero, 0, out int size);
        if (size <= 0 || size > 4096) throw new InvalidOperationException("Invalid token integrity size");
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!GetTokenInformation(token, 25, buffer, size, out _)) throw new InvalidOperationException("Token integrity query failed");
            IntPtr sid = Marshal.ReadIntPtr(buffer); byte count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
            if (count == 0) throw new InvalidOperationException("Empty integrity SID");
            return unchecked((uint)Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)count - 1)));
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    public static OsVersion ReadOs()
    {
        var value = new OsVersion { Size = (uint)Marshal.SizeOf<OsVersion>(), ServicePack = "" };
        int status = RtlGetVersion(ref value);
        if (status != 0) throw new InvalidOperationException("RtlGetVersion failed: " + status);
        return value;
    }
    private static string ObjectName(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return "Unavailable";
        var name = new StringBuilder(256);
        if (!GetUserObjectInformation(handle, 2, name, 512, out _)) return "Unavailable:" + Marshal.GetLastWin32Error();
        return name.ToString();
    }
    public static string WindowStation() => ObjectName(GetProcessWindowStation());
    public static string InputDesktop()
    {
        IntPtr handle = OpenInputDesktop(0, false, 1);
        if (handle == IntPtr.Zero) return "Unavailable:" + Marshal.GetLastWin32Error();
        try { return ObjectName(handle); } finally { CloseDesktop(handle); }
    }
    public static MonitorFact[] Monitors()
    {
        var monitors = new List<MonitorFact>();
        bool failed = false;
        if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, dc, rect, data) =>
        {
            var value = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref value)) { failed = true; return false; }
            int dpi = GetDpiForMonitor(monitor, 0, out uint x, out uint y);
            monitors.Add(new MonitorFact { Bounds = value.Monitor, WorkArea = value.Work, Primary = (value.Flags & 1) != 0, DpiHResult = dpi, DpiX = x, DpiY = y });
            return true;
        }, IntPtr.Zero) || failed) throw new InvalidOperationException("Monitor inventory failed");
        return monitors.ToArray();
    }
    public static int DpiAwareness() => GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext());
    public static bool HasShell() => GetShellWindow() != IntPtr.Zero;
    public static bool DwmEnabled()
    {
        int result = DwmIsCompositionEnabled(out bool enabled);
        if (result < 0) throw new InvalidOperationException("DwmIsCompositionEnabled failed: " + result);
        return enabled;
    }
}
