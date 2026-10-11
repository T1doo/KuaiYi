using System.IO;
using System.Diagnostics;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Threading;
using KuaiYi.Contracts;
using KuaiYi.Core;
using KuaiYi.OleTransfer;
using KuaiYi.Windows;
namespace KuaiYi.OleReceiver;
internal sealed class ReceiverHost:IOleReceiverBackend,IOleReceiverControlBackend
{
    private readonly OleReceiverHello hello;private readonly Action<IpcFrame> send;
    private readonly OleReceiverGate gate=new();private readonly NativeDropTarget target;
    private readonly OleTargetMarshal.Callback callback;
    private readonly OleTargetMarshal.Export export;private readonly Process parent;
    private readonly OleSharedActivation.Reader activation;
    private readonly int originThread=Environment.CurrentManagedThreadId;
    private readonly object groupsSync=new();private readonly Dictionary<Guid,(long Epoch,long Started,SelectionWindow? Source,PixelPoint? Point,OleTextFormat Format)> groups=[];private OleReceiverControl? productionControl;
    public Dispatcher Dispatcher{get;}=Dispatcher.CurrentDispatcher;
    public byte[] ExportPacket=>export.Packet;
    public bool Enabled=>gate.Snapshot.Enabled&&activation.Current(gate.Snapshot.Epoch);
    public long Epoch=>gate.Snapshot.Epoch;
    public bool Production=>hello.ProductionTextDrop;
    public void ApplyControl(OleReceiverControl control)
    {
        if(Production&&control.Enabled&&(control.Target?.Valid!=true||control.BlockedApplications is not {Length:<=128}||control.BlockedApplications.Any(value=>value is null||value.Length is <1 or >260)))throw new InvalidDataException("Invalid production Drop control.");
        Volatile.Write(ref productionControl,control);gate.Set(control.Epoch,control.Enabled);
    }
    public bool Hit(OlePoint point)=>!Production||Volatile.Read(ref productionControl) is {} control&&control.Enabled&&control.Epoch==Epoch&&control.Target?.Contains(new(point.X,point.Y))==true;
    public SelectionWindow? CaptureSource()
    {
        var source=SelectionDesktop.Resolve(NativeDesktop.GetForegroundWindow().ToInt64());return source!=null&&SourceCurrent(source)?source:null;
    }
    public bool SourceCurrent(SelectionWindow source)
    {
        var control=Volatile.Read(ref productionControl);if(control==null||control.Epoch!=Epoch||!control.Enabled||!SelectionDesktop.IsCurrent(source))return false;
        var settings=new KuaiYi.Core.Settings.AppSettings{Privacy=new(){BlockedApplications=(control.BlockedApplications??[]).ToImmutableArray()}};
        return !SelectionTextPolicy.Excluded(source,settings,checked((uint)hello.AppProcessId))&&SelectionDesktop.IsDropSourceCurrent(source);
    }
    public void Feedback(Guid transaction,long epoch,bool visible,OlePoint point,SelectionWindow? source,OleTextFormat format)
    {if(Production)Metadata(transaction,epoch,visible?"Hover":"Leave",source:source,point:new(point.X,point.Y),format:format);else Preview(visible);}
    public ReceiverHost(OleReceiverHello hello,Action<IpcFrame> send)
    {
        this.hello=hello;this.send=send;gate.Set(hello.Epoch,false);parent=OleReceiverParent.Verify(hello);
        try{activation=new(hello);}catch{parent.Dispose();throw;}
        target=new(this);callback=Dispatch;
        try{export=new(callback);}catch{activation.Dispose();parent.Dispose();throw;}
    }
    private int Dispatch(int kind,nint data,uint keys,OlePoint point,ref uint effect)
    {
        try
        {
            if(Environment.CurrentManagedThreadId!=originThread||Thread.CurrentThread.GetApartmentState()!=ApartmentState.STA||parent.HasExited)throw new InvalidOperationException("OLE callback apartment/parent mismatch.");
            return kind switch{1=>target.DragEnter(data,keys,point,ref effect),2=>target.DragOver(keys,point,ref effect),3=>target.DragLeave(),4=>target.Drop(data,keys,point,ref effect),_=>OleNative.Failure};
        }
        catch(Exception error){effect=0;gate.Set(long.MaxValue,false);Console.Error.WriteLine("OleReceiver callback failure: "+error.GetType().Name);return OleNative.Failure;}
    }
    public void SetEnabled(long epoch,bool enabled){gate.Set(epoch,enabled);} // No COM/WPF; Off applies even when OLE STA blocks.
    public void Preview(bool visible)
    {
        Guid id;long epoch;lock(groupsSync){var latest=groups.LastOrDefault();id=latest.Key;epoch=latest.Value.Epoch;}
        if(id!=Guid.Empty)Metadata(id,epoch,visible?"Hover":"Leave");
    }
    public void Emit(Guid transaction,string phase,string status,OleEffect effect=OleEffect.None,bool? available=null,bool? mode=null,bool? started=null,int length=0,bool? foreground=null,bool cleanupCompleted=false,SelectionWindow? source=null,PixelPoint? point=null,OleTextFormat format=OleTextFormat.None)
    {
        long epoch;
        lock(groupsSync)
        {
            if(phase is "QueryBegin" or "DropBegin"){if(groups.Count>=8&&!groups.ContainsKey(transaction))throw new InvalidOperationException("Too many OLE groups.");groups[transaction]=(Epoch,Stopwatch.GetTimestamp(),source,point,format);}
            epoch=groups.TryGetValue(transaction,out var value)?value.Epoch:Epoch;
        }
        Metadata(transaction,epoch,phase,cleanupCompleted,effect,length,source,point,format);
        if(phase is "QueryEnd" or "DropEnd")lock(groupsSync)groups.Remove(transaction);
    }
    public void WorkerMetadata(Guid transaction,long epoch,string phase,string status="")=>Metadata(transaction,epoch,phase);
    private void Metadata(Guid transaction,long epoch,string phase,bool clean=false,OleEffect effect=OleEffect.None,int length=0,SelectionWindow? source=null,PixelPoint? point=null,OleTextFormat format=OleTextFormat.None)
    {
        lock(groupsSync)if(groups.TryGetValue(transaction,out var group)){source??=group.Source;point??=group.Point;if(format==OleTextFormat.None)format=group.Format;}
        var item=new OleReceiverEvent(hello.Instance,epoch,transaction,phase,clean,(uint)effect,length,Environment.CurrentManagedThreadId,Thread.CurrentThread.GetApartmentState().ToString(),NativeDesktop.GetCurrentThreadId(),source,point,format);
        send(new(IpcMessageType.OleEvent,transaction,OleReceiverWire.Serialize(item))); // Bounded queue; native call never synchronously waits on App.
    }
    public bool CanCommit(Guid transaction,long epoch)
    {
        lock(groupsSync)
        {
            if(!groups.TryGetValue(transaction,out var value)||value.Epoch!=epoch)return false;
            return OleReceiverCommitAdmission.CanCommit(()=>Stopwatch.GetElapsedTime(value.Started),()=>gate.Current(epoch),()=>activation.Current(epoch),()=>!Production||value.Source is {} source&&SourceCurrent(source));
        }
    }
    public void Result(Guid transaction,long epoch,string? text,string status,nint foreground)
    {
        if(!CanCommit(transaction,epoch))text=null;
        Metadata(transaction,epoch,"CleanupComplete",true);
        if(text!=null)
        {
            byte[] bytes=Encoding.UTF8.GetBytes(text);
            try
            {
                OleReceiverWire.DecodeText(bytes);
                // Encoding/validation also consumes the same receiver-local budget; no late plaintext enqueue.
                if(CanCommit(transaction,epoch)){send(new(IpcMessageType.BinaryChunk,transaction,bytes));bytes=[];}
                else text=null;
            }
            finally{CryptographicOperations.ZeroMemory(bytes);}
        }
        Metadata(transaction,epoch,"DropEnd",true,text==null?OleEffect.None:OleEffect.Copy,text?.Length??0);lock(groupsSync)groups.Remove(transaction);
    }
    public void ReleaseOwned(Guid transaction,long epoch,string stage,Action release){WorkerMetadata(transaction,epoch,stage+"CleanupBegin");release();}
    public async Task ShutdownAsync()
    {
        gate.Set(long.MaxValue,false);
        while(await Dispatcher.InvokeAsync(()=>target.HasPendingTransfer))await Task.Delay(25); // Keep all origin/async references and shared view alive until actual transfer settles; parent can kill blocked receiver.
        await Dispatcher.InvokeAsync(()=>{try{export.Dispose();}finally{activation.Dispose();parent.Dispose();}}); // STA cleanup, supervised by parent deadline/owned-process kill.
    }
}
