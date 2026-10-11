using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using KuaiYi.Core;
namespace KuaiYi.Windows;
internal static class ClipboardNative
{
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]internal static extern bool OpenClipboard(nint owner);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]internal static extern bool CloseClipboard();
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]internal static extern bool EmptyClipboard();
    [DllImport("user32.dll")]internal static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")]internal static extern nint GetClipboardOwner();
    [DllImport("user32.dll",SetLastError=true)]internal static extern uint EnumClipboardFormats(uint format);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]internal static extern int GetClipboardFormatNameW(uint format,StringBuilder name,int count);
    [DllImport("user32.dll",SetLastError=true)]internal static extern nint GetClipboardData(uint format);
    [DllImport("user32.dll",SetLastError=true)]internal static extern nint SetClipboardData(uint format,nint memory);
    [DllImport("kernel32.dll",SetLastError=true)]internal static extern nuint GlobalSize(nint memory);
    [DllImport("kernel32.dll",SetLastError=true)]internal static extern nint GlobalLock(nint memory);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]internal static extern bool GlobalUnlock(nint memory);
    [DllImport("kernel32.dll",SetLastError=true)]internal static extern nint GlobalAlloc(uint flags,nuint bytes);
    [DllImport("kernel32.dll",SetLastError=true)]internal static extern nint GlobalFree(nint memory);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]internal static extern nint CreateWindowExW(uint ex,string type,string title,uint style,int x,int y,int width,int height,nint parent,nint menu,nint instance,nint argument);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]internal static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")]internal static extern nint GetAncestor(nint window,uint flags);
    [StructLayout(LayoutKind.Sequential)]internal struct GuiInfo{public uint Size,Flags;public nint Active,Focus,Capture,MenuOwner,MoveSize,Caret;public NativeRect CaretBounds;}
    [StructLayout(LayoutKind.Sequential)]internal struct NativeRect{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]internal static extern bool GetGUIThreadInfo(uint thread,ref GuiInfo info);
    [DllImport("user32.dll")]internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassNameW(nint window,StringBuilder name,int capacity);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]private static extern nint GetWindowLongPtrW(nint window,int index);
    [StructLayout(LayoutKind.Sequential)]internal struct LastInput{public uint Size,Time;}
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]internal static extern bool GetLastInputInfo(ref LastInput value);
    [StructLayout(LayoutKind.Sequential)]private struct Message{public nint Window;public uint Code;public nuint W;public nint L;public uint Time;public NativeDesktop.Point Point;public uint Private;}
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool PeekMessageW(out Message message,nint window,uint first,uint last,uint remove);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")]private static extern nint DispatchMessageW(ref Message message);
    internal static void Pump(){while(PeekMessageW(out var message,0,0,0,1)){TranslateMessage(ref message);DispatchMessageW(ref message);}}
    internal static IOException Error()=>new("Clipboard native operation failed.",new Win32Exception(Marshal.GetLastWin32Error()));
    internal static uint LastInputTime(){var info=new LastInput{Size=(uint)Marshal.SizeOf<LastInput>()};return GetLastInputInfo(ref info)?info.Time:0;}
    internal static bool ModifiersReleased()=>new[]{0x10,0x11,0x12,0x5b,0x5c,0x43}.All(k=>(GetAsyncKeyState(k)&0x8000)==0);
    internal static bool PasswordFocus(long focus){var name=new StringBuilder(128);if(GetClassNameW(new(focus),name,name.Capacity)<=0)return true;return (name.ToString().Equals("Edit",StringComparison.OrdinalIgnoreCase)||name.ToString().StartsWith("RichEdit",StringComparison.OrdinalIgnoreCase))&&(GetWindowLongPtrW(new(focus),-16).ToInt64()&0x20)!=0;}
    internal static bool TrustedFocus(SelectionWindow source,long focus)=>focus!=0&&NativeDesktop.IsWindow(new(focus))&&Focus(source.Handle)==focus&&NativeDesktop.GetWindowThreadProcessId(new(focus),out uint pid)!=0&&pid==source.ProcessId&&GetAncestor(new(focus),2).ToInt64()==source.Handle&&!PasswordFocus(focus);
    internal static long Focus(long source){uint thread=NativeDesktop.GetWindowThreadProcessId(new(source),out _);var info=new GuiInfo{Size=(uint)Marshal.SizeOf<GuiInfo>()};return thread!=0&&GetGUIThreadInfo(thread,ref info)?info.Focus.ToInt64():0;}
}
public sealed class NativeClipboardPort: IClipboardPort,IDisposable
{
    private readonly SelectionWindow source;private readonly long focus;private readonly nint ownerWindow;private readonly int thread;
    public NativeClipboardPort(SelectionWindow source,long focus)
    {
        if(Thread.CurrentThread.GetApartmentState()!=ApartmentState.STA)throw new InvalidOperationException("Clipboard requires isolated STA.");this.source=source;this.focus=focus;thread=Environment.CurrentManagedThreadId;
        ownerWindow=ClipboardNative.CreateWindowExW(0,"STATIC","",0,0,0,0,0,new(-3),0,0,0);if(ownerWindow==0)throw ClipboardNative.Error();
    }
    private void AssertThread(){if(Environment.CurrentManagedThreadId!=thread)throw new InvalidOperationException("Clipboard STA owner mismatch.");}
    public uint Sequence=>ClipboardNative.GetClipboardSequenceNumber();
    public bool SourceCurrent()=>SelectionDesktop.IsCurrent(source)&&ClipboardNative.TrustedFocus(source,focus);
    public bool InputUnchanged(uint inputTime)=>inputTime!=0&&ClipboardNative.LastInputTime()==inputTime;
    private bool OwnerMatches()
    {
        nint owner=ClipboardNative.GetClipboardOwner();return owner!=0&&NativeDesktop.GetWindowThreadProcessId(owner,out uint pid)!=0&&pid==source.ProcessId&&ClipboardNative.GetAncestor(owner,3).ToInt64()==source.Handle;
    }
    private static List<(uint Format,string? Name)> Formats()
    {
        var formats=new List<(uint,string?)>();uint current=0;
        while(true){Marshal.SetLastPInvokeError(0);current=ClipboardNative.EnumClipboardFormats(current);if(current==0){if(Marshal.GetLastPInvokeError()!=0)throw ClipboardNative.Error();return formats;}if(formats.Count>=ClipboardSnapshot.FormatLimit)throw new InvalidDataException("Clipboard format bound.");string? name=null;if(current>=0xc000){var value=new StringBuilder(128);int size=ClipboardNative.GetClipboardFormatNameW(current,value,value.Capacity);if(size<=0||size>=127)throw new InvalidDataException("Unknown clipboard format.");name=value.ToString();}if(!ClipboardSnapshot.Supported(current,name))throw new InvalidDataException("Clipboard format cannot be fully cloned.");formats.Add((current,name));}
    }
    private static byte[] ReadMemory(uint format,int limit)
    {
        nint memory=ClipboardNative.GetClipboardData(format);if(memory==0)throw new InvalidDataException("Unrendered clipboard data.");nuint size=ClipboardNative.GlobalSize(memory);if(size==0||size>(nuint)limit)throw new InvalidDataException("Clipboard allocation bound.");nint pointer=ClipboardNative.GlobalLock(memory);if(pointer==0)throw ClipboardNative.Error();byte[] bytes=new byte[(int)size];try{Marshal.Copy(pointer,bytes,0,bytes.Length);return bytes;}catch{CryptographicOperations.ZeroMemory(bytes);throw;}finally{ClipboardNative.GlobalUnlock(memory);}
    }
    public ClipboardSnapshot Snapshot()
    {
        AssertThread();if(!ClipboardNative.OpenClipboard(ownerWindow))throw ClipboardNative.Error();var blocks=new List<ClipboardBlock>();ClipboardSnapshot? result=null;
        try{uint sequence=Sequence;var formats=Formats();long total=0;foreach(var format in formats){byte[] bytes=ReadMemory(format.Format,ClipboardSnapshot.BlockLimit);blocks.Add(new(format.Format,format.Name,bytes));if((total+=bytes.Length)>ClipboardSnapshot.TotalLimit)throw new InvalidDataException("Clipboard total bound.");}if(Sequence!=sequence||!formats.SequenceEqual(Formats()))throw new InvalidDataException("Clipboard changed while rendering snapshot.");return result=ClipboardSnapshot.Clone(sequence,blocks);}
        finally{foreach(var block in blocks)CryptographicOperations.ZeroMemory(block.Bytes);if(!ClipboardNative.CloseClipboard()){result?.Dispose();throw ClipboardNative.Error();}}
    }
    public ClipboardRead? Read(uint sequence)
    {
        AssertThread();if(!ClipboardNative.OpenClipboard(ownerWindow))throw ClipboardNative.Error();byte[]? bytes=null;ClipboardRead? result=null;
        try{if(Sequence!=sequence||!OwnerMatches()||!SourceCurrent())return null;try{bytes=ReadMemory(13,200006);}catch(Exception error)when(error is InvalidDataException or IOException){bytes=[];}if(Sequence!=sequence||!OwnerMatches()||!SourceCurrent()){CryptographicOperations.ZeroMemory(bytes);bytes=null;return null;}result=new ClipboardRead(sequence,bytes);bytes=null;return result;}
        finally{if(bytes!=null)CryptographicOperations.ZeroMemory(bytes);if(!ClipboardNative.CloseClipboard()){result?.Dispose();throw ClipboardNative.Error();}}
    }
    private static nint Allocate(byte[] bytes)
    {
        nint memory=ClipboardNative.GlobalAlloc(0x42,(nuint)bytes.Length);if(memory==0)throw ClipboardNative.Error();nint pointer=ClipboardNative.GlobalLock(memory);if(pointer==0){ClipboardNative.GlobalFree(memory);throw ClipboardNative.Error();}try{try{Marshal.Copy(bytes,0,pointer,bytes.Length);}finally{ClipboardNative.GlobalUnlock(memory);}}catch{ZeroFree(memory);throw;}return memory;
    }
    private static unsafe void ZeroFree(nint memory)
    {nuint size=ClipboardNative.GlobalSize(memory);nint pointer=ClipboardNative.GlobalLock(memory);if(pointer!=0){try{CryptographicOperations.ZeroMemory(new Span<byte>((void*)pointer,checked((int)size)));}finally{ClipboardNative.GlobalUnlock(memory);}}ClipboardNative.GlobalFree(memory);}
    public ClipboardRecovery Restore(ClipboardSnapshot snapshot,uint copiedSequence)
    {
        AssertThread();var staged=new List<(uint Format,nint Memory)>();bool opened=false;
        try
        {
            // Allocate every format before touching the clipboard. No subset is represented as complete recovery.
            foreach(var block in snapshot.Blocks)staged.Add((block.Format,Allocate(block.Bytes)));
            if(!ClipboardNative.OpenClipboard(ownerWindow))return ClipboardRecovery.Unconfirmed;opened=true;
            if(Sequence!=copiedSequence||!OwnerMatches())return ClipboardRecovery.Changed;
            if(!ClipboardNative.EmptyClipboard())return ClipboardRecovery.Unconfirmed;
            for(int i=0;i<staged.Count;i++){var item=staged[i];if(ClipboardNative.SetClipboardData(item.Format,item.Memory)==0)return ClipboardRecovery.Unconfirmed;staged[i]=(item.Format,0);}
            return ClipboardRecovery.Restored;
        }
        finally{foreach(var item in staged)if(item.Memory!=0)ZeroFree(item.Memory);if(opened&&!ClipboardNative.CloseClipboard())throw ClipboardNative.Error();}
    }
    public void Wait(TimeSpan duration,CancellationToken cancellation){AssertThread();ClipboardNative.Pump();cancellation.WaitHandle.WaitOne(duration);cancellation.ThrowIfCancellationRequested();ClipboardNative.Pump();}
    public void Dispose(){AssertThread();if(!ClipboardNative.DestroyWindow(ownerWindow))throw ClipboardNative.Error();}
}
public static class CopyInputGuard
{
    [StructLayout(LayoutKind.Sequential)]private struct Keyboard{public ushort Key,Scan;public uint Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Explicit,Size=40)]private struct Input{[FieldOffset(0)]public uint Type;[FieldOffset(8)]public Keyboard Keyboard;}
    [DllImport("user32.dll",SetLastError=true)]private static extern uint SendInput(uint count,Input[] inputs,int size);
    private static Input Key(ushort key,bool up,uint time)=>new(){Type=1,Keyboard=new(){Key=key,Flags=up?2u:0u,Time=time}};
    public static long FocusHandle(long source)=>ClipboardNative.Focus(source);
    public static bool IsCurrent(SelectionWindow source,SelectionCopyEvidence evidence)=>SelectionDesktop.IsCurrent(source)&&ClipboardNative.TrustedFocus(source,evidence.FocusHandle);
    public static ClipboardCopyStart? Send(SelectionGesture gesture,SelectionCopyEvidence evidence,uint sequence)
    {
        if(!SelectionDesktop.IsCurrent(gesture.Window)||!ClipboardNative.TrustedFocus(gesture.Window,evidence.FocusHandle)||!ClipboardNative.ModifiersReleased()||ClipboardNative.GetClipboardSequenceNumber()!=sequence)return null;
        long timestamp=TimeProvider.System.GetTimestamp();uint time=unchecked((uint)Environment.TickCount64);if(time==0)return null;
        var keys=new[]{Key(0x11,false,time),Key(0x43,false,time),Key(0x43,true,time),Key(0x11,true,time)};uint sent=SendInput(4,keys,40);
        if(sent!=4){if(sent==0)return null;uint released=SendInput(2,[Key(0x43,true,time),Key(0x11,true,time)],40);if(sent<2||released!=2)throw new IOException("Partial copy input; restoration unconfirmed.");}
        return new(timestamp,time);
    }
}
