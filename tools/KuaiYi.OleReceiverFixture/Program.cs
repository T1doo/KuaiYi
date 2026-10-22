using System.Buffers.Binary;
using System.Text;
using System.Security.Cryptography;
using KuaiYi.Contracts;
using KuaiYi.Core;
using KuaiYi.OleTransfer;
// Explicit synthetic backend, real child/private pipes/production server. This packet is NEVER unmarshaled.
int statusAt=Array.IndexOf(args,"--test-status");
if(statusAt>=0){if(statusAt+2!=args.Length)return 2;FixtureStatus.Path=args[statusAt+1];args=args[..statusAt];}
string mode=args.LastOrDefault()??"normal";
bool holdTargetOutput=mode=="formal-stale-query-held-diagnostics";
if(holdTargetOutput)mode="formal-stale-query";
if(mode=="startup-pending")
{
    var hello=await IpcFrameCodec.ReadAsync(Console.OpenStandardInput())??throw new EndOfStreamException();
    try { if(hello.Type!=IpcMessageType.OleHello)return 2;FixtureStatus.Write("blocked:startup");await Task.Delay(Timeout.Infinite);return 0; }
    finally { CryptographicOperations.ZeroMemory(hello.Payload); }
}
if(mode=="startup-diagnostic-fail")
{
    var frame=await IpcFrameCodec.ReadAsync(Console.OpenStandardInput())??throw new EndOfStreamException();
    try{if(frame.Type!=IpcMessageType.OleHello)return 2;}
    finally{CryptographicOperations.ZeroMemory(frame.Payload);}
    OleStartupDiagnostics.Emit(OleStartupStage.ParentVerify,new InvalidOperationException("PUBLIC_SENTINEL never projected"));return 3;
}
if(mode=="startup-diagnostic-flood")
{
    using var stderr=Console.OpenStandardError();byte[] output=Enumerable.Repeat((byte)'x',1024*1024).ToArray();
    await stderr.WriteAsync(output);await stderr.WriteAsync(new byte[]{(byte)'\n'});Array.Clear(output);
    OleStartupDiagnostics.Emit(OleStartupStage.Entry);
}
using var targetOutput=Console.OpenStandardError();using var heldOutput=holdTargetOutput?new HeldTargetOutput(targetOutput):null;var observations=new OleTargetDiagnostics((Stream?)heldOutput??targetOutput);
try { return await OleReceiverServer.RunAsync(Console.OpenStandardInput(),Console.OpenStandardOutput(),(hello,send)=>Task.FromResult<IOleReceiverBackend>(new Backend(hello,send,mode,(epoch,enabled)=>{if(!enabled&&epoch==hello.Epoch+2)heldOutput?.Release();})),targetDiagnostic:observations); }
finally { observations.CompleteOutput();if(observations.OriginalOutputTask is {} original)await original; }
sealed class Backend:IOleReceiverBackend,IOleReceiverControlBackend
{
    private readonly OleReceiverHello hello;private readonly Action<IpcFrame> send;private readonly string mode;private readonly Action<long,bool>? diagnosticControl;private int started;
    public byte[] ExportPacket{get;}=new byte[72];
    public Backend(OleReceiverHello hello,Action<IpcFrame> send,string mode,Action<long,bool>? diagnosticControl=null)
    {
        this.hello=hello;this.send=send;this.mode=mode;this.diagnosticControl=diagnosticControl;
        BinaryPrimitives.WriteUInt32LittleEndian(ExportPacket,0x574f454d);BinaryPrimitives.WriteUInt32LittleEndian(ExportPacket.AsSpan(4),1);OleReceiverWire.DropTargetId.TryWriteBytes(ExportPacket.AsSpan(8,16));
        if(mode=="bad-export")BinaryPrimitives.WriteUInt32LittleEndian(ExportPacket.AsSpan(4),4);
    }
    public void ApplyControl(OleReceiverControl control)
    {
        if(hello.ProductionTextDrop&&control.Enabled&&control.Target?.Valid!=true)throw new InvalidDataException("public fixture geometry required");SetEnabled(control.Epoch,control.Enabled);
    }
    public void SetEnabled(long epoch,bool enabled)
    {
        diagnosticControl?.Invoke(epoch,enabled);
        if(!enabled){FixtureStatus.Write("off:"+epoch);return;}
        if(Interlocked.Exchange(ref started,1)==0)_=Task.Run(()=>Transfer(epoch));
    }
    private async Task Transfer(long epoch)
    {
        if(hello.ProductionTextDrop){await Formal(epoch);return;}
        Guid id=Guid.NewGuid();
        void Event(string phase,bool cleaned=false,uint effect=0,int length=0)=>send(new(IpcMessageType.OleEvent,id,OleReceiverWire.Serialize(new OleReceiverEvent(mode=="wrong-instance"?Guid.NewGuid():hello.Instance,epoch,id,phase,cleaned,effect,length))));
        if(mode=="query-stall"){Event("QueryBegin");FixtureStatus.Write("blocked:query");await Task.Delay(Timeout.Infinite);return;}
        Event("DropBegin");
        if(mode=="drop-stall"){FixtureStatus.Write("blocked:drop");await Task.Delay(Timeout.Infinite);return;}
        if(mode=="release-stall"){Event("CleanupFailed");FixtureStatus.Write("blocked:release");await Task.Delay(Timeout.Infinite);return;}
        if(mode=="late")await Task.Delay(500);
        if(mode!="missing-cleanup")Event("CleanupComplete",true);
        byte[] body=mode=="bad-utf8"?[0xff]:Encoding.UTF8.GetBytes("public local fixture 文本");
        send(new(IpcMessageType.BinaryChunk,id,body));
        if(mode=="duplicate")send(new(IpcMessageType.BinaryChunk,id,Encoding.UTF8.GetBytes("duplicate")));
        Event("DropEnd",mode!="cleanup-false",mode=="move"?2u:1u,"public local fixture 文本".Length);
    }
    private async Task Formal(long epoch)
    {
        Guid id=Guid.NewGuid();var source=new SelectionWindow(123,456,789,"public.exe");var point=new PixelPoint(24,24);var format=mode.StartsWith("bitmap-",StringComparison.Ordinal)?OleTextFormat.ImageBitmap:mode.StartsWith("image-",StringComparison.Ordinal)?OleTextFormat.ImageFile:OleTextFormat.Unicode;
        void Event(string phase,bool cleaned=false,uint effect=0,int length=0)
        {
            var origin=mode=="formal-source-swap"&&phase=="DropEnd"?source with{ProcessStartUtcTicks=790}:source;
            var location=mode=="formal-point-swap"&&phase=="CleanupComplete"?new PixelPoint(25,24):point;
            send(new(IpcMessageType.OleEvent,id,OleReceiverWire.Serialize(new OleReceiverEvent(hello.Instance,epoch,id,phase,cleaned,effect,length,Source:mode=="formal-missing-source"?null:origin,Point:location,TextFormat:phase=="QueryBegin"?OleTextFormat.None:format))));
        }
        if(mode=="formal-stale-query")send(new(IpcMessageType.OleEvent,id,OleReceiverWire.Serialize(new OleReceiverEvent(hello.Instance,epoch-1,id,"QueryEnd",true,1))));
        Event("QueryBegin");Event("Hover");Event("QueryEnd",true,1);
        if(mode is "formal-hover-only" or "image-hover-only")return;
        Event("DropBegin",effect:1);
        if(mode=="formal-duplicate-begin")Event("DropBegin",effect:1);
        if(mode=="formal-stall"){FixtureStatus.Write("blocked:formal");await Task.Delay(Timeout.Infinite);return;}
        if(mode=="formal-late")await Task.Delay(350);
        string text=mode.StartsWith("image-",StringComparison.Ordinal)?mode=="image-multi"?"C:\\public\\a.png\0C:\\public\\b.png":mode=="image-url"?"https://public.invalid/a.png":"C:\\public\\image.GIF":mode=="formal-url"?"https://public.invalid/path":"public formal 文本 😀";
        if(mode is not ("formal-missing-cleanup" or "image-missing-cleanup" or "bitmap-missing-cleanup"))Event("CleanupComplete",true);
        if(mode.StartsWith("bitmap-",StringComparison.Ordinal))
        {
            byte[] bitmap=new byte[78];bitmap[0]=(byte)'B';bitmap[1]=(byte)'M';BinaryPrimitives.WriteUInt32LittleEndian(bitmap.AsSpan(2),78);BinaryPrimitives.WriteUInt32LittleEndian(bitmap.AsSpan(10),54);BinaryPrimitives.WriteUInt32LittleEndian(bitmap.AsSpan(14),40);BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(18),3);BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(22),2);bitmap[26]=1;bitmap[28]=24;bitmap[54]=255;
            if(mode=="bitmap-truncated")bitmap=bitmap[..^1];if(mode=="bitmap-large")bitmap=new byte[OleBitmapDrop.TransportLimit+1];
            send(new(IpcMessageType.BinaryChunk,id,bitmap));Event("DropEnd",true,1,mode=="bitmap-codeunits"?1:0);return;
        }
        byte[] body=mode=="formal-bad-utf8"?[0xff]:Encoding.UTF8.GetBytes(text);send(new(IpcMessageType.BinaryChunk,id,body));
        if(mode=="formal-duplicate-payload")send(new(IpcMessageType.BinaryChunk,id,Encoding.UTF8.GetBytes("duplicate")));
        Event("DropEnd",true,mode=="formal-move"?2u:1u,text.Length);
    }
    public Task ShutdownAsync(){Array.Clear(ExportPacket);return Task.CompletedTask;}
}

// Explicit fixture-only scheduler barrier: the production stderr writer has dequeued a fixed enum,
// but not a single byte has reached stderr. Only a separate epoch+2 disabled control releases it.
sealed class HeldTargetOutput(Stream destination):Stream
{
    private readonly TaskCompletionSource release=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Release(){FixtureStatus.Write("diagnostic:release");release.TrySetResult();}
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes,CancellationToken cancellation=default)
    {
        if(bytes.Length<2||bytes.Span[^1]!=(byte)'\n'||!OleTargetDiagnostics.TryParse(bytes.Span[..^1],out var item))throw new InvalidDataException("Public fixture accepts fixed diagnostic metadata only.");
        FixtureStatus.Write("diagnostic:held:"+item);await release.Task.WaitAsync(cancellation);await destination.WriteAsync(bytes,cancellation);FixtureStatus.Write("diagnostic:written:"+item);
    }
    public override Task FlushAsync(CancellationToken cancellation)=>destination.FlushAsync(cancellation);
    public override bool CanRead=>false;public override bool CanWrite=>true;public override bool CanSeek=>false;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
    public override void Flush()=>destination.Flush();public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
}

// Explicit test-owned metadata channel; never used by the production receiver or diagnostic parser.
static class FixtureStatus
{
    public static string? Path;private static readonly object Sync=new();
    public static void Write(string status)
    {
        lock(Sync)if(Path is {} path)
        {
            using var file=new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.ReadWrite);
            using var text=new StreamWriter(file);text.WriteLine(status);
        }
    }
}
