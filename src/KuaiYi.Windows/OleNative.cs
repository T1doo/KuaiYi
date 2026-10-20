using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using KuaiYi.Core;
using KuaiYi.OleTransfer;

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
    [DllImport("kernel32.dll",SetLastError=true)] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool GlobalUnlock(nint global);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern nuint GlobalSize(nint global);
    [DllImport("kernel32.dll")] public static extern nint GlobalFree(nint global);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern uint RegisterClipboardFormatW(string name);
    public static short Registered(string name){uint value=RegisterClipboardFormatW(name);if(value<0xc000||value>0xffff)throw new Win32Exception(Marshal.GetLastWin32Error());return unchecked((short)value);}
    public static FORMATETC TextFormat(OleTextFormat kind)=>new(){cfFormat=kind switch{OleTextFormat.Unicode=>13,OleTextFormat.Html=>Registered("HTML Format"),OleTextFormat.Rtf=>Registered("Rich Text Format"),_=>throw new InvalidDataException("Unsupported OLE text format.")},dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_HGLOBAL};
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
    private readonly OleTargetDiagnostics? diagnostics;
    private readonly int thread = Environment.CurrentManagedThreadId;
    public static OleDataReference FromBorrowed(nint borrowedPointer,OleTargetDiagnostics? diagnostics=null)
    {
        if (borrowedPointer == 0) throw new ArgumentException("Missing borrowed IDataObject.");
        Marshal.AddRef(borrowedPointer); return new(borrowedPointer,diagnostics);
    }
    public OleDataReference(nint ownedPointer,OleTargetDiagnostics? diagnostics=null)
    {pointer=ownedPointer!=0?ownedPointer:throw new ArgumentException("Missing COM interface.");this.diagnostics=diagnostics;}
    public nint Pointer { get { EnsureThread(); return pointer != 0 ? pointer : throw new ObjectDisposedException(nameof(OleDataReference)); } }
    private void EnsureThread() { if (thread != Environment.CurrentManagedThreadId) throw new InvalidOperationException("Unmarshaled COM interface crossed apartments."); }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int QueryData(nint self, ref FORMATETC format);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetData(nint self, ref FORMATETC format, out OleMedium medium);
    private T Method<T>(int slot) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(Pointer), slot * nint.Size));
    private int ReadMedium(ref FORMATETC format,out OleMedium medium)
    {
        var getData=Method<GetData>(3);nint data=Pointer;
        diagnostics?.Observe(OleTargetObservation.GetDataInvoking); // Nonblocking, immediately before the actual vtable call.
        return getData(data,ref format,out medium);
    }
    public bool OffersUnicode() { var format = OleNative.UnicodeFormat; return Method<QueryData>(5)(Pointer, ref format) == 0; }
    public OleTextFormat OfferedText()
    {
        bool Offers(short format){var descriptor=new FORMATETC{cfFormat=format,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_HGLOBAL};return Method<QueryData>(5)(Pointer,ref descriptor)==0;}
        // File/virtual-file Drop belongs to the image pipeline, never treat its path as body text.
        if(Offers(15))return OleTextFormat.None;
        foreach(string name in new[]{"FileGroupDescriptorW","FileGroupDescriptor","FileContents"})
        {var format=OleNative.Registered(name);if(Offers(format))return OleTextFormat.None;}
        foreach(var kind in new[]{OleTextFormat.Unicode,OleTextFormat.Html,OleTextFormat.Rtf}){var format=OleNative.TextFormat(kind);if(Method<QueryData>(5)(Pointer,ref format)==0)return kind;}return OleTextFormat.None;
    }
    public OleTextFormat OfferedContent(bool allowImageFiles)
    {
        bool Offers(short value,TYMED medium=TYMED.TYMED_HGLOBAL,int index=-1){var f=new FORMATETC{cfFormat=value,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=index,tymed=medium};return Method<QueryData>(5)(Pointer,ref f)==0;}
        foreach(string name in new[]{"FileGroupDescriptorW","FileGroupDescriptor"})if(Offers(OleNative.Registered(name)))return OleTextFormat.None;
        short contents=OleNative.Registered("FileContents");foreach(int index in new[]{0,-1})foreach(var medium in new[]{TYMED.TYMED_HGLOBAL,TYMED.TYMED_ISTREAM,TYMED.TYMED_ISTORAGE})if(Offers(contents,medium,index))return OleTextFormat.None;
        if(Offers(15))return allowImageFiles?OleTextFormat.ImageFile:OleTextFormat.None;
        var bitmap=new FORMATETC{cfFormat=2,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_GDI};
        if(Method<QueryData>(5)(Pointer,ref bitmap)==0||Offers(8)||Offers(17))return OleTextFormat.None;
        foreach(string name in new[]{"PNG","image/png"})foreach(var medium in new[]{TYMED.TYMED_HGLOBAL,TYMED.TYMED_ISTREAM,TYMED.TYMED_ISTORAGE})if(Offers(OleNative.Registered(name),medium))return OleTextFormat.None;
        return OfferedText();
    }
    public string ReadImageFile()
    {
        var format=new FORMATETC{cfFormat=15,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_HGLOBAL};Marshal.ThrowExceptionForHR(ReadMedium(ref format,out var medium));var scope=new OleCleanupScope();scope.Own(()=>OleNative.ReleaseStgMedium(ref medium));
        return scope.Run(()=>
        {
            if(medium.Tymed!=(uint)TYMED.TYMED_HGLOBAL||medium.Union==0)throw new InvalidDataException("Image file Drop requires HGLOBAL.");nuint size=OleNative.GlobalSize(medium.Union);if(size<24||size>OleImageFileDrop.GlobalByteLimit)throw new InvalidDataException("Image file Drop allocation limit.");nint memory=OleNative.GlobalLock(medium.Union);if(memory==0)throw new Win32Exception(Marshal.GetLastWin32Error());
            scope.Own(()=>{Marshal.SetLastPInvokeError(0);bool locked=OleNative.GlobalUnlock(medium.Union);int error=Marshal.GetLastPInvokeError();if(!locked&&error!=0)throw new Win32Exception(error);});byte[]? copy=null;
            try{copy=new byte[(int)size];Marshal.Copy(memory,copy,0,copy.Length);return OleImageFileDrop.DecodeGlobal(copy);}finally{if(copy!=null)CryptographicOperations.ZeroMemory(copy);}
        });
    }
    public string ReadText(OleTextFormat kind)
    {
        var format=OleNative.TextFormat(kind);Marshal.ThrowExceptionForHR(ReadMedium(ref format,out var medium));var scope=new OleCleanupScope();scope.Own(()=>OleNative.ReleaseStgMedium(ref medium));
        return scope.Run(()=>{if(medium.Tymed!=(uint)TYMED.TYMED_HGLOBAL||medium.Union==0)throw new InvalidDataException("Drop text requires HGLOBAL.");nuint size=OleNative.GlobalSize(medium.Union);if(size<1||size>(nuint)OleDroppedText.ByteLimit(kind))throw new InvalidDataException("Drop allocation limit.");nint pointer=OleNative.GlobalLock(medium.Union);if(pointer==0)throw new Win32Exception(Marshal.GetLastWin32Error());scope.Own(()=>{Marshal.SetLastPInvokeError(0);bool stillLocked=OleNative.GlobalUnlock(medium.Union);int error=Marshal.GetLastPInvokeError();if(!stillLocked&&error!=0)throw new Win32Exception(error);});byte[]? copy=null;try{copy=new byte[(int)size];Marshal.Copy(pointer,copy,0,copy.Length);return OleDroppedText.Decode(kind,copy);}finally{if(copy!=null)CryptographicOperations.ZeroMemory(copy);}});
    }
    public string ReadUnicode()
    {
        var format = OleNative.UnicodeFormat;
        Marshal.ThrowExceptionForHR(ReadMedium(ref format,out var medium));
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
