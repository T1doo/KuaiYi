using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using KuaiYi.Core;
using KuaiYi.Windows;
using ComDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace KuaiYi.OleFaultSource;

internal static class Program
{
    [STAThread] public static int Main(string[] args)
    {
        bool ci=args.Length==5&&args[0]=="--ci-stdio";if(ci)args=args[1..];
        if (args.Length != 4 || !Enum.TryParse(args[1], true, out Fault fault) || !Enum.IsDefined(fault) || !Enum.TryParse(args[2], true, out Offered offered) || !Enum.IsDefined(offered))
        { Console.Error.WriteLine($"Usage: KuaiYi.OleFaultSource.exe PUBLIC_FIXTURE {string.Join('|', Enum.GetNames<Fault>())} Copy|CopyMove|MoveOnly METADATA_JSONL"); return 2; }
        string text = File.ReadAllText(Path.GetFullPath(args[0]), Encoding.UTF8).Trim();
        if(ci&&(fault!=Fault.GetDataBlocked||offered!=Offered.Copy))return 2;
        var app = new Application(); app.Run(new SourceWindow(text, fault, offered, Path.GetFullPath(args[3]),ci)); return 0;
    }
}
public enum Fault { Sync, Async, AsyncDisabled, StartRejected, GetDataFailure, GetDataBlocked, QueryBlocked, ModeBlocked, Oversized, InvalidUtf16, MissingTerminator, AsyncGetDataBlocked, AsyncEndBlocked, AsyncEndFailure, QueryReleaseBlocked, QueryReleaseFailure, DropReleaseBlocked, DropReleaseFailure, AsyncReleaseBlocked, AsyncReleaseFailure }
public enum Offered { Copy, CopyMove, MoveOnly }

internal sealed class SourceWindow : Window
{
    private readonly Queue<string> metadata = new();
    // A source with InOperation=true must remain alive until EndOperation, not just until DoDragDrop returns.
    private readonly List<FixtureData> retained = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly OleApartment apartment = new();
    public SourceWindow(string text, Fault fault, Offered offered, string output,bool ci=false)
    {
        Title = $"KuaiYi real OLE fault source: {fault} / {offered}"; Width = 620; Height = 300;
        var panel = new StackPanel { Margin = new Thickness(15) }; Content = panel;
        panel.Children.Add(new TextBlock { Text = "公开测试文本；按住下方按钮开始 OLE 拖放。每次动作后退出保存元数据。阻塞场景会故意卡住来源 5 秒。", TextWrapping = TextWrapping.Wrap });
        var button = new Button { Content = "拖动此按钮到快译绿色按钮", Margin = new Thickness(8), Height = 65 }; panel.Children.Add(button); panel.Children.Add(status);
        void Record(object value) { if (metadata.Count == 100_000) metadata.Dequeue(); string line=JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow,timestamp=Stopwatch.GetTimestamp(),frequency=Stopwatch.Frequency,processId=Environment.ProcessId,nativeThreadId=NativeDesktop.GetCurrentThreadId(),managedThreadId = Environment.CurrentManagedThreadId, apartment = Thread.CurrentThread.GetApartmentState().ToString(), item = value });metadata.Enqueue(line);if(ci){Console.WriteLine(line);Console.Out.Flush();} }
        button.PreviewMouseLeftButtonDown += (_, e) =>
        {
            retained.RemoveAll(x => !x.Active);
            if (retained.Count >= 8) { status.Text = "Eight unfinished async operations: restart this owned fault source."; return; }
            FixtureData data = fault is Fault.Async or Fault.AsyncDisabled or Fault.StartRejected or Fault.ModeBlocked or Fault.AsyncGetDataBlocked or Fault.AsyncEndBlocked or Fault.AsyncEndFailure or Fault.AsyncReleaseBlocked or Fault.AsyncReleaseFailure
                ? new AsyncFixtureData(text, fault, Record) : new FixtureData(text, fault, Record);
            if (data is AsyncFixtureData asyncSource) Marshal.ThrowExceptionForHR(asyncSource.SetAsyncMode(fault != Fault.AsyncDisabled));
            retained.Add(data);
            uint effects = offered switch { Offered.Copy => 1, Offered.CopyMove => 3, Offered.MoveOnly => 2, _ => throw new InvalidOperationException() };
            Record(new{kind="DoDragDropBegin"});int result = OleNative.DoDragDrop(data, new FixtureDropSource(), effects, out uint performed);
            // Mark the source as deleted only if a buggy target reports MOVE; public fixture bytes remain immutable.
            bool sourceWouldDelete = performed == 2;
            status.Text = $"HRESULT={result:X8}; effect={performed}; sourceWouldDelete={sourceWouldDelete}; InOperation={data.Active}";
            Record(new { kind = "DoDragDropReturn", fault = fault.ToString(), offered = offered.ToString(), result, performed, sourceWouldDelete, active = data.Active }); e.Handled = true;
        };
        Closed += (_, _) => { Record(new { kind = "SourceClosed", unfinishedAsync = retained.Count(x => x.Active) }); Directory.CreateDirectory(Path.GetDirectoryName(output)!); File.WriteAllLines(output, metadata, new UTF8Encoding(false)); apartment.Dispose(); };
        if(ci)
        {
            Left=100;Top=100;
            Loaded+=(_,_)=>
            {
                Activate();var point=button.PointToScreen(new Point(button.ActualWidth/2,button.ActualHeight/2));
                Record(new{kind="SourceReady",hwnd=new System.Windows.Interop.WindowInteropHelper(this).Handle.ToInt64(),x=(int)Math.Round(point.X),y=(int)Math.Round(point.Y)});
                _=Task.Run(async()=>{string? command=await Console.In.ReadLineAsync();if(command=="close")await Dispatcher.InvokeAsync(Close);});
            };
        }
    }
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class FixtureDropSource : IOleDropSource
{
    public int QueryContinueDrag(bool escape, uint keys) => escape ? 0x40101 : (keys & 1) == 0 ? 0x40100 : 0;
    public int GiveFeedback(uint effect) => 0x40102;
}

/// <summary>Real IDataObject with intentionally no async interface in the synchronous class.</summary>
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public class FixtureData(string text, Fault fault, Action<object> record) : ComDataObject
{
    protected readonly Fault Scenario = fault;
    protected readonly Action<object> Record = record;
    public bool Active { get; protected set; }
    private int queries, reads;
    protected static void Throw(int hr) => throw new COMException("Public OLE fault fixture.", hr);
    public void GetData(ref FORMATETC format, out STGMEDIUM medium)
    {
        medium = default; reads++; Record(new { kind = "GetData", reads, queries, active = Active });
        if (QueryFormat(format) != 0) Throw(unchecked((int)0x80040064));
        if (Scenario is Fault.GetDataBlocked or Fault.AsyncGetDataBlocked)
        {
            Record(new{kind="GetDataBlocking",delayMs=5000});Thread.Sleep(5000);Record(new{kind="GetDataBlockReturned"}); // Real synchronous source call; deliberate test-only fault.
        }
        if (Scenario == Fault.GetDataFailure) Throw(OleNative.Failure);
        byte[] bytes = Scenario switch
        {
            Fault.Oversized => new byte[OleUnicodeText.MaximumGlobalBytes + 2],
            Fault.InvalidUtf16 => [0x00, 0xD8, 0, 0],
            Fault.MissingTerminator => [0x61, 0],
            _ => Encoding.Unicode.GetBytes(text + '\0')
        };
        nint global = OleNative.GlobalAlloc(0x42, (nuint)bytes.Length); // MOVEABLE | ZEROINIT; target owns returned medium.
        if (global == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        nint memory = OleNative.GlobalLock(global);
        if (memory == 0) { int error = Marshal.GetLastWin32Error(); OleNative.GlobalFree(global); throw new Win32Exception(error); }
        bool copied = false;
        try
        {
            if (Scenario == Fault.MissingTerminator)
            {
                // GlobalAlloc may round the allocation up. Fill the actual size, including padding.
                int actualSize = checked((int)OleNative.GlobalSize(global));
                byte[] unterminated = Enumerable.Repeat((byte)0x61, actualSize).ToArray();
                Marshal.Copy(unterminated, 0, memory, actualSize); Array.Clear(unterminated);
            }
            else Marshal.Copy(bytes, 0, memory, bytes.Length);
            copied = true;
        }
        finally { OleNative.GlobalUnlock(global); if (!copied) OleNative.GlobalFree(global); Array.Clear(bytes); }
        medium = new() { tymed = TYMED.TYMED_HGLOBAL, unionmember = global, pUnkForRelease = null };
    }
    private static int QueryFormat(FORMATETC format) => format.cfFormat == 13 && format.dwAspect == DVASPECT.DVASPECT_CONTENT && format.lindex == -1 && (format.tymed & TYMED.TYMED_HGLOBAL) != 0 ? 0 : unchecked((int)0x80040064);
    public int QueryGetData(ref FORMATETC format)
    {
        queries++; Record(new { kind = "QueryGetData", queries, reads });
        if (Scenario == Fault.QueryBlocked) Thread.Sleep(5000);
        return QueryFormat(format);
    }
    public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium) => Throw(unchecked((int)0x80040069));
    public int GetCanonicalFormatEtc(ref FORMATETC input, out FORMATETC output) { output = input; output.ptd = 0; return 0x40130; }
    public void SetData(ref FORMATETC format, ref STGMEDIUM medium, bool release) => Throw(unchecked((int)0x80004001));
    public IEnumFORMATETC EnumFormatEtc(DATADIR direction) => direction == DATADIR.DATADIR_GET ? new FixtureFormatEnumerator() : throw new COMException("No writable formats.", unchecked((int)0x80004001));
    public int DAdvise(ref FORMATETC format, ADVF flags, IAdviseSink sink, out int connection) { connection = 0; return unchecked((int)0x80040003); }
    public void DUnadvise(int connection) => Throw(unchecked((int)0x80040003));
    public int EnumDAdvise(out IEnumSTATDATA enumerator) { enumerator = null!; return unchecked((int)0x80040003); }
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class AsyncFixtureData(string text, Fault fault, Action<object> record) : FixtureData(text, fault, record), IOleAsyncCapability
{
    private bool enabled;
    private int starts, ends;
    public int SetAsyncMode(bool value) { enabled = value; Record(new { kind = "SetAsyncMode", enabled }); return 0; }
    public int GetAsyncMode(out bool value) { Record(new { kind = "GetAsyncMode", enabled }); if (Scenario == Fault.ModeBlocked) Thread.Sleep(5000); value = enabled; return 0; }
    public int StartOperation(nint reserved) { starts++; Record(new { kind = "StartOperation", starts, active = Active }); if (Scenario == Fault.StartRejected) return OleNative.Failure; if (Active) return OleNative.Failure; Active = true; return 0; }
    public int InOperation(out bool active) { active = Active; return 0; }
    public int EndOperation(int result, nint reserved, uint effect) { ends++; Record(new { kind = "EndOperation", ends, starts, result, effect, wasActive = Active }); if (Scenario == Fault.AsyncEndBlocked) Thread.Sleep(5000); if (!Active) return OleNative.Failure; Active = false; return Scenario == Fault.AsyncEndFailure ? OleNative.Failure : 0; }
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class FixtureFormatEnumerator : IEnumFORMATETC
{
    private int offset;
    public int Next(int count, FORMATETC[] formats, int[]? fetched)
    {
        if (count < 0 || formats.Length < count || (count != 1 && fetched is null)) return unchecked((int)0x80070057);
        int returned = count > 0 && offset == 0 ? 1 : 0;
        if (returned == 1) { formats[0] = OleNative.UnicodeFormat; offset = 1; }
        if (fetched is { Length: > 0 }) fetched[0] = returned;
        return returned == count ? 0 : 1;
    }
    public int Skip(int count) { if (count < 0) return unchecked((int)0x80070057); int available = 1 - offset; offset = count >= available ? 1 : offset + count; return count <= available ? 0 : 1; }
    public int Reset() { offset = 0; return 0; }
    public void Clone(out IEnumFORMATETC clone) => clone = new FixtureFormatEnumerator { offset = offset };
}
