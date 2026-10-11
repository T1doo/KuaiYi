using System.ComponentModel;
using System.Runtime.InteropServices;
using KuaiYi.Core;
using Microsoft.Win32.SafeHandles;

namespace KuaiYi.Windows;

public static class NativeDesktop
{
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; public readonly PixelRect ToPixels() => new(Left, Top, Right, Bottom); }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetWindowRect(nint hwnd, out Rect rectangle);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowExW(uint extendedStyle, string className, string? title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint GetWindowLongPtrW(nint hwnd, int index);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowLongPtrW(nint hwnd, int index, nint value);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);
    [DllImport("user32.dll", SetLastError = true)] private static extern int SetWindowRgn(nint hwnd, nint region, [MarshalAs(UnmanagedType.Bool)] bool redraw);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(nint handle);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int informationClass, nint info, uint bytes, out uint required);
    [DllImport("advapi32.dll")] private static extern nint GetSidSubAuthorityCount(nint sid);
    [DllImport("advapi32.dll")] private static extern nint GetSidSubAuthority(nint sid, uint index);

    public static MonitorGeometry MonitorAt(PixelPoint center, DpiScale dpi)
    {
        nint monitor = MonitorFromPoint(new Point { X = center.X, Y = center.Y }, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new(monitor.ToString("X"), info.Work.ToPixels(), dpi);
    }
    public static MonitorGeometry MonitorAt(PixelPoint center)
    {
        // GetDpiForMonitor is not supported for per-monitor-aware threads. Query an invisible
        // native HWND located on the target monitor instead, under this executable's PMv2 manifest.
        var info = MonitorAt(center, new DpiScale(1));
        PixelPoint point = info.WorkArea.Center;
        nint probe = CreateWindowExW(0x08000080, "STATIC", null, 0x80000000, point.X, point.Y, 1, 1, 0, 0, 0, 0);
        if (probe == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            uint dpi = GetDpiForWindow(probe);
            if (dpi == 0) throw new InvalidOperationException("Cannot determine target monitor DPI.");
            return info with { Dpi = new DpiScale(dpi / 96.0) };
        }
        finally { if (!DestroyWindow(probe)) throw new Win32Exception(Marshal.GetLastWin32Error()); }
    }
    public static PixelRect ClampAnchor(PixelRect anchor, MonitorGeometry monitor)
    {
        PixelRect work = monitor.WorkArea.Inset(monitor.Dpi.CeilingPixels(12));
        int size = monitor.Dpi.CeilingPixels(48);
        if (work.Width < size || work.Height < size) throw new InvalidOperationException("Display work area cannot contain the anchor.");
        int x = Math.Clamp(anchor.Left, work.Left, work.Right - size), y = Math.Clamp(anchor.Top, work.Top, work.Bottom - size);
        return new(x, y, x + size, y + size);
    }
    public static void ApplyBounds(nint hwnd, PixelRect rectangle, int radiusPixels)
    {
        if (!SetWindowPos(hwnd, new nint(-1), rectangle.Left, rectangle.Top, rectangle.Width, rectangle.Height, 0x0010 | 0x0040))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        nint region = CreateRoundRectRgn(0, 0, rectangle.Width + 1, rectangle.Height + 1, radiusPixels * 2, radiusPixels * 2);
        if (region == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        // On success Windows owns the region; on failure ownership stays here.
        if (SetWindowRgn(hwnd, region, true) == 0) { int error = Marshal.GetLastWin32Error(); DeleteObject(region); throw new Win32Exception(error); }
    }
    public static void SetOverlayStyle(nint hwnd, bool allowActivation)
    {
        nint current = GetWindowLongPtrW(hwnd, -20);
        long styles = current.ToInt64() | 0x00000080L; // TOOLWINDOW
        styles = allowActivation ? styles & ~0x08000000L : styles | 0x08000000L;
        Marshal.SetLastPInvokeError(0);
        nint previous = SetWindowLongPtrW(hwnd, -20, new nint(styles));
        if (previous == 0 && Marshal.GetLastWin32Error() != 0) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public static uint IntegrityLevel(uint processId)
    {
        using var process = OpenProcess(0x1000, false, processId);
        if (process.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!OpenProcessToken(process, 8, out var token)) throw new Win32Exception(Marshal.GetLastWin32Error());
        using (token)
        {
            GetTokenInformation(token, 25, 0, 0, out uint size);
            if (size is 0 or > 4096) throw new InvalidOperationException("Invalid token label size.");
            nint buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (!GetTokenInformation(token, 25, buffer, size, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
                nint sid = Marshal.ReadIntPtr(buffer);
                byte count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                if (count == 0) throw new InvalidOperationException("Invalid integrity SID.");
                return unchecked((uint)Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1))));
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
    }
}
