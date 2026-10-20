using System.Buffers.Binary;
using System.Text;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class ImageFileDropTests
{
    private static byte[] Global(string list){byte[] body=Encoding.Unicode.GetBytes(list);byte[] b=new byte[20+body.Length];BinaryPrimitives.WriteUInt32LittleEndian(b,20);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(16),1);body.CopyTo(b,20);return b;}
    [Theory][InlineData("C:\\public\\image.png")][InlineData("D:\\中😀\\IMAGE.GIF")][InlineData("E:\\p.webp")][InlineData("C:\\p.jpeg")][InlineData("C:\\p.bmp")]
    public void SingleWideImageListAndSeparatePathTransportAgree(string path)
    {var bytes=Global(path+"\0\0");Assert.Equal(path,OleImageFileDrop.DecodeGlobal(bytes));Assert.Equal(path,OleImageFileDrop.DecodeGlobal([..bytes,0,0]));Assert.Equal(path,OleImageFileDrop.DecodeTransport(Encoding.UTF8.GetBytes(path)));Assert.Throws<InvalidDataException>(()=>OleDroppedText.Decode(OleTextFormat.ImageFile,Encoding.UTF8.GetBytes(path)));}
    [Theory][InlineData("C:\\a.png\0C:\\b.png\0\0")][InlineData("C:\\a.png\0")][InlineData("\0\0")][InlineData("C:\\a.png\0\0x")]
    public void MultiEmptyIncompleteAndNonzeroTailListsAreRejected(string list)=>Assert.ThrowsAny<Exception>(()=>OleImageFileDrop.DecodeGlobal(Global(list)));
    [Theory][InlineData("offset")][InlineData("max-offset")][InlineData("ansi")][InlineData("wide-reserved")][InlineData("nc")][InlineData("short")][InlineData("odd")][InlineData("large")][InlineData("surrogate")]
    public void HeaderAndAllocationAreBoundedBeforePathDecode(string fault)
    {
        byte[] b=Global("C:\\public\\a.png\0\0");switch(fault){case "offset":BinaryPrimitives.WriteUInt32LittleEndian(b,22);break;case "max-offset":BinaryPrimitives.WriteUInt32LittleEndian(b,uint.MaxValue);break;case "ansi":b[16]=0;break;case "wide-reserved":b[16]=2;break;case "nc":b[12]=2;break;case "short":b=b[..19];break;case "odd":b=b[..^1];break;case "large":b=new byte[16386];break;case "surrogate":b[26]=0;b[27]=0xd8;break;}
        Assert.ThrowsAny<Exception>(()=>OleImageFileDrop.DecodeGlobal(b));
    }
    [Theory][InlineData("https://public.invalid/a.png")][InlineData("\\\\server\\a.png")][InlineData("\\\\?\\C:\\a.png")][InlineData("C:a.png")][InlineData("C:\\a.png:stream")][InlineData("C:\\..\\a.png")][InlineData("C:\\.\\a.png")][InlineData("C:\\\\a.png")][InlineData("C:/a.png")][InlineData("C:\\a.svg")][InlineData("C:\\a.pdf")][InlineData("C:\\a.png\0")][InlineData("C:\\a\n.png")]
    public void PathsCannotBeRemoteDevicesAdsRelativeOrUnsupported(string path)=>Assert.Throws<InvalidDataException>(()=>OleImageFileDrop.DecodeTransport(Encoding.UTF8.GetBytes(path)));
    [Fact]public void ExactPathAndTransportLimitsAreChecked()
    {string path="C:\\"+new string('a',4089)+".png";Assert.Equal(4096,path.Length);Assert.Equal(path,OleImageFileDrop.DecodeGlobal(Global(path+"\0\0")));Assert.Throws<InvalidDataException>(()=>OleImageFileDrop.ValidatePath("C:\\a"+path[3..]));Assert.Throws<InvalidDataException>(()=>OleImageFileDrop.DecodeTransport(new byte[OleImageFileDrop.TransportByteLimit+1]));Assert.Throws<DecoderFallbackException>(()=>OleImageFileDrop.DecodeTransport([0xff]));}
    private static readonly SelectionWindow Source=new(123,456,789,"public.exe");
    private static readonly DropTargetGeometry Target=new(new(0,0,48,48),new(0,0,48,48),24);
    private sealed class Clock:TimeProvider{internal long Now=1000;public override long TimestampFrequency=>1000;public override long GetTimestamp()=>Now;}
    private sealed class Destination:SynchronizationContext{internal readonly Queue<Action> Items=[];public override void Post(SendOrPostCallback cb,object? state)=>Items.Enqueue(()=>cb(state));internal void Drain(){while(Items.TryDequeue(out var cb))cb();}}
    private sealed class Rig:IAsyncDisposable
    {
        internal readonly RuntimeGate Gate=new();internal SettingsSnapshot Settings=new(new(),"public-v1",SettingsLoadState.Current);internal readonly Clock Time=new();internal readonly Destination Queue=new();internal bool Current=true,Visible=true,Receiver=true;internal Action? DuringSource;internal readonly List<string> Paths=[];internal int Texts;internal readonly DropCaptureCoordinator Capture;internal readonly DropCaptureLease Lease;
        internal Rig(){Gate.SetMode(TriggerMode.Drop);var origin=new EndpointOrigin(new Uri("https://public.invalid"));Gate.ConsentTo(origin);Assert.True(Gate.TryEnable(origin,true));Capture=new(Gate,()=>Settings,s=>{DuringSource?.Invoke();return Current&&s==Source;},()=>Visible,999,Queue,(_,_)=>Texts++,_=>{},Time);Lease=Capture.Open(Guid.NewGuid(),Gate.Snapshot.Epoch,Target)!;}
        internal Guid Begin(){var id=Guid.NewGuid();Assert.True(Capture.Enter(Lease,id,Source,new(24,24),OleEffect.Copy));Assert.True(Capture.Begin(Lease,id,Source,new(24,24),OleEffect.Copy));return id;}
        internal Task Receive(Guid id,CancellationToken token=default)=>Capture.ReceiveImageFileAsync(Lease,id,Encoding.UTF8.GetBytes("C:\\public\\a.gif"),a=>{if(!Receiver)return false;a();return true;},Paths.Add,token,()=>Receiver);
        public ValueTask DisposeAsync()=>Capture.DisposeAsync();
    }
    [Fact]public async Task PathTransfersOnceWithoutAnyTextSnapshotAndClearsQueuedReference()
    {await using var r=new Rig();Guid id=r.Begin();var task=r.Receive(id);await r.Receive(id);Assert.Equal(1,r.Capture.PendingFileCountForTest);r.Queue.Drain();await task;Assert.Equal("C:\\public\\a.gif",Assert.Single(r.Paths));Assert.Equal(0,r.Texts);Assert.Equal(0,r.Capture.PendingFileCountForTest);Assert.False(r.Capture.Enter(r.Lease,id,Source,new(24,24),OleEffect.Copy));}
    [Theory][InlineData("off")][InlineData("off-on")][InlineData("mode")][InlineData("settings")][InlineData("source")][InlineData("hidden")][InlineData("receiver")][InlineData("anchor")][InlineData("cancel")][InlineData("invalidate")][InlineData("new-hover")][InlineData("deadline")]
    public async Task QueuedPathCannotBorrowExpiredDropAdmission(string change)
    {
        await using var r=new Rig();using var stop=new CancellationTokenSource();var task=r.Receive(r.Begin(),stop.Token);Assert.Equal(1,r.Capture.PendingFileCountForTest);
        switch(change){case "off":r.Gate.Disable();break;case "off-on":r.Gate.Disable();Assert.True(r.Gate.TryEnable(new(new Uri("https://public.invalid")),true));break;case "mode":r.Gate.SetMode(TriggerMode.Selection);break;case "settings":r.Settings=r.Settings with{Version="changed"};break;case "source":r.Current=false;break;case "hidden":r.Visible=false;break;case "receiver":r.Receiver=false;break;case "anchor":r.Capture.UpdateGeometry(new(new(10,0,58,48),new(10,0,58,48),24));break;case "cancel":stop.Cancel();break;case "invalidate":r.Capture.Invalidate();break;case "new-hover":r.Capture.Enter(r.Lease,Guid.NewGuid(),Source,new(24,24),OleEffect.Copy);break;case "deadline":r.Time.Now+=1500;break;}
        if(change is "cancel" or "invalidate" or "anchor" or "new-hover")Assert.Equal(0,r.Capture.PendingFileCountForTest);r.Queue.Drain();await task;Assert.Empty(r.Paths);Assert.Equal(0,r.Texts);Assert.Equal(0,r.Capture.PendingFileCountForTest);
    }
    [Theory][InlineData(1499,true)][InlineData(1500,false)]
    public async Task OriginalDropBudgetEndsAtPathTransferAndNotAtLaterDecode(int elapsed,bool accepted)
    {await using var r=new Rig();var task=r.Receive(r.Begin());r.Time.Now+=elapsed;r.Queue.Drain();await task;Assert.Equal(accepted,r.Paths.Count==1);}
    [Theory][InlineData("gate")][InlineData("time")][InlineData("settings")][InlineData("receiver")]
    public async Task FinalSourceCheckCannotChangeAuthorizationBeforePathCommit(string change)
    {await using var r=new Rig();var task=r.Receive(r.Begin());r.DuringSource=()=>{switch(change){case "gate":r.Gate.Disable();break;case "time":r.Time.Now+=1500;break;case "settings":r.Settings=r.Settings with{Version="changed"};break;case "receiver":r.Receiver=false;break;}};r.Queue.Drain();await task;Assert.Empty(r.Paths);}
    [Fact]public async Task InvalidationReleasesQueuedPathWithoutPumpingDestination()
    {var r=new Rig();var task=r.Receive(r.Begin());await r.DisposeAsync();Assert.Equal(0,r.Capture.PendingFileCountForTest);r.Queue.Drain();await task;Assert.Empty(r.Paths);}
}
