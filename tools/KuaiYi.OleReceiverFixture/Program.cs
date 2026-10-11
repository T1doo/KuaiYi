using System.Buffers.Binary;
using System.Text;
using KuaiYi.Contracts;
using KuaiYi.Core;
using KuaiYi.OleTransfer;
// Explicit synthetic backend, real child/private pipes/production server. This packet is NEVER unmarshaled.
string mode=args.LastOrDefault()??"normal";
return await OleReceiverServer.RunAsync(Console.OpenStandardInput(),Console.OpenStandardOutput(),(hello,send)=>Task.FromResult<IOleReceiverBackend>(new Backend(hello,send,mode)));
sealed class Backend:IOleReceiverBackend,IOleReceiverControlBackend
{
    private readonly OleReceiverHello hello;private readonly Action<IpcFrame> send;private readonly string mode;private int started;
    public byte[] ExportPacket{get;}=new byte[72];
    public Backend(OleReceiverHello hello,Action<IpcFrame> send,string mode)
    {
        this.hello=hello;this.send=send;this.mode=mode;
        BinaryPrimitives.WriteUInt32LittleEndian(ExportPacket,0x574f454d);BinaryPrimitives.WriteUInt32LittleEndian(ExportPacket.AsSpan(4),1);OleReceiverWire.DropTargetId.TryWriteBytes(ExportPacket.AsSpan(8,16));
        if(mode=="bad-export")BinaryPrimitives.WriteUInt32LittleEndian(ExportPacket.AsSpan(4),4);
    }
    public void ApplyControl(OleReceiverControl control)
    {
        if(hello.ProductionTextDrop&&control.Enabled&&control.Target?.Valid!=true)throw new InvalidDataException("public fixture geometry required");SetEnabled(control.Epoch,control.Enabled);
    }
    public void SetEnabled(long epoch,bool enabled)
    {
        if(!enabled){Console.Error.WriteLine("off:"+epoch);return;}
        if(Interlocked.Exchange(ref started,1)==0)_=Task.Run(()=>Transfer(epoch));
    }
    private async Task Transfer(long epoch)
    {
        if(hello.ProductionTextDrop){await Formal(epoch);return;}
        Guid id=Guid.NewGuid();
        void Event(string phase,bool cleaned=false,uint effect=0,int length=0)=>send(new(IpcMessageType.OleEvent,id,OleReceiverWire.Serialize(new OleReceiverEvent(mode=="wrong-instance"?Guid.NewGuid():hello.Instance,epoch,id,phase,cleaned,effect,length))));
        if(mode=="query-stall"){Event("QueryBegin");Console.Error.WriteLine("blocked:query");await Task.Delay(Timeout.Infinite);return;}
        Event("DropBegin");
        if(mode=="drop-stall"){Console.Error.WriteLine("blocked:drop");await Task.Delay(Timeout.Infinite);return;}
        if(mode=="release-stall"){Event("CleanupFailed");Console.Error.WriteLine("blocked:release");await Task.Delay(Timeout.Infinite);return;}
        if(mode=="late")await Task.Delay(500);
        if(mode!="missing-cleanup")Event("CleanupComplete",true);
        byte[] body=mode=="bad-utf8"?[0xff]:Encoding.UTF8.GetBytes("public local fixture 文本");
        send(new(IpcMessageType.BinaryChunk,id,body));
        if(mode=="duplicate")send(new(IpcMessageType.BinaryChunk,id,Encoding.UTF8.GetBytes("duplicate")));
        Event("DropEnd",mode!="cleanup-false",mode=="move"?2u:1u,"public local fixture 文本".Length);
    }
    private async Task Formal(long epoch)
    {
        Guid id=Guid.NewGuid();var source=new SelectionWindow(123,456,789,"public.exe");var point=new PixelPoint(24,24);var format=OleTextFormat.Unicode;
        void Event(string phase,bool cleaned=false,uint effect=0,int length=0)
        {
            var origin=mode=="formal-source-swap"&&phase=="DropEnd"?source with{ProcessStartUtcTicks=790}:source;
            var location=mode=="formal-point-swap"&&phase=="CleanupComplete"?new PixelPoint(25,24):point;
            send(new(IpcMessageType.OleEvent,id,OleReceiverWire.Serialize(new OleReceiverEvent(hello.Instance,epoch,id,phase,cleaned,effect,length,Source:mode=="formal-missing-source"?null:origin,Point:location,TextFormat:phase=="QueryBegin"?OleTextFormat.None:format))));
        }
        Event("QueryBegin");Event("Hover");Event("QueryEnd",true,1);
        if(mode=="formal-hover-only")return;
        Event("DropBegin",effect:1);
        if(mode=="formal-duplicate-begin")Event("DropBegin",effect:1);
        if(mode=="formal-stall"){Console.Error.WriteLine("blocked:formal");await Task.Delay(Timeout.Infinite);return;}
        if(mode=="formal-late")await Task.Delay(350);
        string text=mode=="formal-url"?"https://public.invalid/path":"public formal 文本 😀";
        if(mode!="formal-missing-cleanup")Event("CleanupComplete",true);
        byte[] body=mode=="formal-bad-utf8"?[0xff]:Encoding.UTF8.GetBytes(text);send(new(IpcMessageType.BinaryChunk,id,body));
        if(mode=="formal-duplicate-payload")send(new(IpcMessageType.BinaryChunk,id,Encoding.UTF8.GetBytes("duplicate")));
        Event("DropEnd",true,mode=="formal-move"?2u:1u,text.Length);
    }
    public Task ShutdownAsync(){Array.Clear(ExportPacket);return Task.CompletedTask;}
}
