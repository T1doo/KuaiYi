using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using KuaiYi.Core;

namespace KuaiYi.Windows;

[StructLayout(LayoutKind.Sequential)] public struct OlePoint { public int X, Y; }
[StructLayout(LayoutKind.Sequential)] public struct OleMedium { public uint Tymed; public nint Union; public nint ReleaseUnknown; }

[ComVisible(true), Guid("00000122-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IOleDropTarget
{
    [PreserveSig] int DragEnter(nint borrowedDataObject, uint keys, OlePoint point, ref uint effect);
    [PreserveSig] int DragOver(uint keys, OlePoint point, ref uint effect);
    [PreserveSig] int DragLeave();
    [PreserveSig] int Drop(nint borrowedDataObject, uint keys, OlePoint point, ref uint effect);
}
[ComVisible(true), Guid("00000121-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IOleDropSource
{
    [PreserveSig] int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool escape, uint keys);
    [PreserveSig] int GiveFeedback(uint effect);
}
[ComVisible(true), Guid("3D8B0590-F691-11D2-8EA9-006097DF5BD4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IOleAsyncCapability
{
    [PreserveSig] int SetAsyncMode([MarshalAs(UnmanagedType.Bool)] bool enabled);
    [PreserveSig] int GetAsyncMode([MarshalAs(UnmanagedType.Bool)] out bool enabled);
    [PreserveSig] int StartOperation(nint reserved);
    [PreserveSig] int InOperation([MarshalAs(UnmanagedType.Bool)] out bool active);
    [PreserveSig] int EndOperation(int result, nint reserved, uint effect);
}

public static class OleNative
{
    public static readonly Guid DataObjectId = new("0000010E-0000-0000-C000-000000000046");
    public static readonly Guid AsyncId = new("3D8B0590-F691-11D2-8EA9-006097DF5BD4");
    public const int NoInterface = unchecked((int)0x80004002), Failure = unchecked((int)0x80004005);
    [DllImport("ole32.dll")] public static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] public static extern void OleUninitialize();
    [DllImport("ole32.dll")] public static extern int RegisterDragDrop(nint hwnd, [MarshalAs(UnmanagedType.Interface)] IOleDropTarget target);
    [DllImport("ole32.dll")] public static extern int RevokeDragDrop(nint hwnd);
    [DllImport("ole32.dll")] public static extern int DoDragDrop([MarshalAs(UnmanagedType.Interface)] IDataObject data, [MarshalAs(UnmanagedType.Interface)] IOleDropSource source, uint allowed, out uint performed);
    [DllImport("ole32.dll")] public static extern void ReleaseStgMedium(ref OleMedium medium);
    [DllImport("ole32.dll")] public static extern int CoMarshalInterThreadInterfaceInStream(ref Guid id, nint value, out nint stream);
    [DllImport("ole32.dll")] public static extern int CoGetInterfaceAndReleaseStream(nint stream, ref Guid id, out nint value);
    [DllImport("ole32.dll")] public static extern int CoReleaseMarshalData(nint stream);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SeekStream(nint self, long offset, uint origin, nint position);
    public static void ReleaseUnusedMarshalPacket(nint stream)
    {
        // Only the originating apartment may release a packet that was not unmarshaled.
        var seek = Marshal.GetDelegateForFunctionPointer<SeekStream>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(stream), 5 * nint.Size));
        Marshal.ThrowExceptionForHR(seek(stream, 0, 0, 0));
        Marshal.ThrowExceptionForHR(CoReleaseMarshalData(stream));
    }
    [DllImport("kernel32.dll", SetLastError = true)] public static extern nint GlobalAlloc(uint flags, nuint bytes);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern nint GlobalLock(nint global);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool GlobalUnlock(nint global);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern nuint GlobalSize(nint global);
    [DllImport("kernel32.dll")] public static extern nint GlobalFree(nint global);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool SetForegroundWindow(nint hwnd);
    public static FORMATETC UnicodeFormat => new() { cfFormat = 13, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL };
}

public sealed class OleApartment : IDisposable
{
    private readonly int thread = Environment.CurrentManagedThreadId;
    private bool disposed;
    public OleApartment() => Marshal.ThrowExceptionForHR(OleNative.OleInitialize(0));
    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != thread) throw new InvalidOperationException("OLE apartment disposed on wrong thread.");
        if (!disposed) { OleNative.OleUninitialize(); disposed = true; }
    }
}

/// <summary>Owns one native reference on this apartment; never transfer it as a raw interface pointer.</summary>
public sealed class OleDataReference : IDisposable
{
    private nint pointer;
    private readonly int thread = Environment.CurrentManagedThreadId;
    public static OleDataReference FromBorrowed(nint borrowedPointer)
    {
        if (borrowedPointer == 0) throw new ArgumentException("Missing borrowed IDataObject.");
        Marshal.AddRef(borrowedPointer); return new(borrowedPointer);
    }
    public OleDataReference(nint ownedPointer) => pointer = ownedPointer != 0 ? ownedPointer : throw new ArgumentException("Missing COM interface.");
    public nint Pointer { get { EnsureThread(); return pointer != 0 ? pointer : throw new ObjectDisposedException(nameof(OleDataReference)); } }
    private void EnsureThread() { if (thread != Environment.CurrentManagedThreadId) throw new InvalidOperationException("Unmarshaled COM interface crossed apartments."); }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int QueryData(nint self, ref FORMATETC format);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetData(nint self, ref FORMATETC format, out OleMedium medium);
    private T Method<T>(int slot) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(Pointer), slot * nint.Size));
    public bool OffersUnicode() { var format = OleNative.UnicodeFormat; return Method<QueryData>(5)(Pointer, ref format) == 0; }
    public string ReadUnicode()
    {
        var format = OleNative.UnicodeFormat;
        Marshal.ThrowExceptionForHR(Method<GetData>(3)(Pointer, ref format, out var medium));
        var mediumScope = new OleCleanupScope(); mediumScope.Own(() => OleNative.ReleaseStgMedium(ref medium));
        return mediumScope.Run(() =>
        {
            if (medium.Tymed != (uint)TYMED.TYMED_HGLOBAL || medium.Union == 0) throw new InvalidDataException("Unexpected dropped medium.");
            nuint size = OleNative.GlobalSize(medium.Union);
            if (size is < 2 || size > OleUnicodeText.MaximumGlobalBytes) throw new InvalidDataException("Dropped HGLOBAL exceeds limit.");
            nint memory = OleNative.GlobalLock(medium.Union);
            if (memory == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            byte[] copy = new byte[(int)size];
            try { Marshal.Copy(memory, copy, 0, copy.Length); return OleUnicodeText.Decode(copy); }
            finally { CryptographicOperations.ZeroMemory(copy); OleNative.GlobalUnlock(medium.Union); }
        });
    }
    public nint MarshalToStream()
    {
        Guid id = OleNative.DataObjectId;
        Marshal.ThrowExceptionForHR(OleNative.CoMarshalInterThreadInterfaceInStream(ref id, Pointer, out nint stream)); return stream;
    }
    public OleAsyncReference? QueryAsync()
    {
        Guid id = OleNative.AsyncId; int result = Marshal.QueryInterface(Pointer, in id, out nint value);
        if (result == OleNative.NoInterface) return null;
        Marshal.ThrowExceptionForHR(result); return new(value);
    }
    public void Dispose() { EnsureThread(); if (pointer != 0) { Marshal.Release(pointer); pointer = 0; } }
}

public sealed class OleAsyncReference(nint ownedPointer) : IDisposable
{
    private nint pointer = ownedPointer;
    private readonly int thread = Environment.CurrentManagedThreadId;
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Mode(nint self, out int enabled);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Start(nint self, nint reserved);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int End(nint self, int result, nint reserved, uint effect);
    private T Method<T>(int slot) where T : Delegate
    {
        if (thread != Environment.CurrentManagedThreadId || pointer == 0) throw new InvalidOperationException("Invalid async capability apartment/lifetime.");
        return Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(pointer), slot * nint.Size));
    }
    public bool Enabled { get { Marshal.ThrowExceptionForHR(Method<Mode>(4)(pointer, out int enabled)); return enabled != 0; } }
    public int StartOperation() => Method<Start>(5)(pointer, 0);
    public int EndOperation(int result, OleEffect effect) => Method<End>(7)(pointer, result, 0, (uint)effect);
    public void Dispose()
    {
        if (thread != Environment.CurrentManagedThreadId) throw new InvalidOperationException("Invalid async capability release apartment.");
        if (pointer != 0) { Marshal.Release(pointer); pointer = 0; }
    }
}
