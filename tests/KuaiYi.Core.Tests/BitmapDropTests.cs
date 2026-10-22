using System.Buffers.Binary;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.OleTransfer;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class BitmapDropTests
{
    internal static byte[] Dib(int width=3,int height=2)
    {
        int stride=(width*3+3)&~3;byte[] b=new byte[40+stride*Math.Abs(height)];
        BinaryPrimitives.WriteUInt32LittleEndian(b,40);BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(4),width);BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(8),height);b[12]=1;b[14]=24;
        b.AsSpan(40).Fill(0x73);return b;
    }
    private sealed class Medium(byte[] source):IOleBitmapMedium
    {
        internal int Locks,Unlocks,Releases,Copies;internal bool UnlockFail,ReleaseFail,Mutate;internal int ThrowCopy=-1;internal byte[]? Output;
        public ulong Size=>(ulong)source.Length;
        public void Lock()=>Locks++;
        public void Copy(int offset,byte[] dest,int at,int n)
        {if(at>=54)Output=dest;if(Copies++==ThrowCopy)throw new IOException("copy");source.AsSpan(offset,n).CopyTo(dest.AsSpan(at,n));if(Mutate&&offset==0&&Copies>1)dest[4]^=1;}
        public void Unlock(){Unlocks++;if(UnlockFail)throw new IOException("unlock");}
        public void Release(){Releases++;if(ReleaseFail)throw new IOException("release");}
    }
    [Theory][InlineData(2)][InlineData(-2)]
    public void CopyKeepsOrientationPixelsAndClearsPaddingAndAllocatorTail(int height)
    {
        var original=Dib(height:height);var medium=new Medium([..original,0x94,0x95]);byte[] bmp=OleBitmapCopy.Read(medium,()=>true);OleBitmapDrop.ValidateBmp(bmp);
        Assert.Equal(78,bmp.Length);Assert.Equal(height,BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(22)));Assert.Equal(0x73,bmp[54]);Assert.Equal(0,bmp[63]);Assert.Equal(0,bmp[64]);Assert.Equal(0,bmp[65]);Assert.Equal(1,medium.Unlocks);Assert.Equal(1,medium.Releases);Assert.Equal(0x73,original[49]);
        using var owner=new OwnedOleBitmap(bmp);var held=owner.Bytes;owner.Dispose();Assert.All(held.ToArray(),b=>Assert.Equal(0,b));Assert.Contains(bmp,b=>b!=0);
    }
    [Theory][InlineData("header")][InlineData("planes")][InlineData("depth")][InlineData("compression")][InlineData("palette")][InlineData("important")][InlineData("zero-width")][InlineData("zero-height")][InlineData("min-height")][InlineData("overflow")][InlineData("side")][InlineData("image-size")][InlineData("truncated")][InlineData("large")]
    public void InvalidAndOversizedSourcesFailBeforePixelAllocation(string fault)
    {
        byte[] b=Dib();switch(fault){case "header":b[0]=108;break;case "planes":b[12]=2;break;case "depth":b[14]=32;break;case "compression":b[16]=1;break;case "palette":b[32]=1;break;case "important":b[36]=1;break;case "zero-width":b[4]=0;break;case "zero-height":b[8]=0;break;case "min-height":BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(8),int.MinValue);break;case "overflow":BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(4),int.MaxValue);break;case "side":BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(4),16385);break;case "image-size":b[20]=23;break;case "truncated":b=b[..^1];break;case "large":b=new byte[OleBitmapDrop.AllocationLimit+1];break;}
        var m=new Medium(b);Assert.Throws<InvalidDataException>(()=>OleBitmapCopy.Read(m,()=>true));Assert.Null(m.Output);Assert.Equal(1,m.Releases);
    }
    [Theory][InlineData("cancel")][InlineData("copy")][InlineData("mutation")][InlineData("unlock")][InlineData("release")][InlineData("both")]
    public void LateFailuresClearCopiedPixelsAndAttemptEveryOriginalCleanup(string fault)
    {
        var m=new Medium(Dib());m.ThrowCopy=fault=="copy"?2:-1;m.Mutate=fault=="mutation";m.UnlockFail=fault is "unlock" or "both";m.ReleaseFail=fault is "release" or "both";int checks=0;
        var error=Record.Exception(()=>OleBitmapCopy.Read(m,()=>fault!="cancel"||++checks<6));Assert.NotNull(error);Assert.NotNull(m.Output);Assert.All(m.Output,b=>Assert.Equal(0,b));Assert.Equal(1,m.Unlocks);Assert.Equal(1,m.Releases);
        if(fault=="both")Assert.Equal(2,Assert.IsType<OleCleanupException>(error).InnerExceptions.Count);
    }
    private sealed class Clock:TimeProvider{internal long Now=1000;public override long TimestampFrequency=>1000;public override long GetTimestamp()=>Now;}
    private sealed class Queue:SynchronizationContext{internal readonly List<Action> Items=[];public override void Post(SendOrPostCallback cb,object? state)=>Items.Add(()=>cb(state));internal void Drain(){foreach(var cb in Items)cb();Items.Clear();}}
    [Theory][InlineData("accept")][InlineData("cancel")][InlineData("replacement")][InlineData("on")][InlineData("source")][InlineData("settings")][InlineData("hide")][InlineData("1499")][InlineData("1500")]
    public async Task OffBitmapAdmissionIsDistinctAndRejectsLateOrReplacedDelivery(string change)
    {
        var gate=new RuntimeGate();gate.SetMode(TriggerMode.Drop);var settings=new SettingsSnapshot(new(),"v1",SettingsLoadState.Current);bool source=true,visible=true;var queue=new Queue();var clock=new Clock();int texts=0,files=0;OwnedOleBitmap? accepted=null;
        await using var capture=new DropCaptureCoordinator(gate,()=>settings,_=>source,()=>visible,999,queue,(_,_)=>texts++,_=>{},clock);var geometry=new DropTargetGeometry(new(0,0,48,48),new(0,0,48,48),24);var lease=capture.OpenBitmapPreview(Guid.NewGuid(),gate.Snapshot.Epoch,geometry)!;
        Assert.NotNull(lease);Assert.NotNull(lease.BitmapPreview);Assert.False(gate.TryBeginPassiveCapture(TriggerMode.Drop,out _));Assert.Null(capture.Open(Guid.NewGuid(),1,geometry));lease=capture.OpenBitmapPreview(Guid.NewGuid(),gate.Snapshot.Epoch,geometry)!;
        var origin=new SelectionWindow(123,456,789,"public.exe");Guid tx=Guid.NewGuid();Assert.True(capture.Enter(lease,tx,origin,new(24,24),OleEffect.Copy));Assert.True(capture.Begin(lease,tx,origin,new(24,24),OleEffect.Copy));
        await capture.ReceiveAsync(lease,tx,"text"u8.ToArray(),a=>{a();return true;});await capture.ReceiveImageFileAsync(lease,tx,"C:\\x.bmp"u8.ToArray(),a=>{a();return true;},_=>files++);
        var bmp=OleBitmapCopy.Read(new Medium(Dib()),()=>true);using var stop=new CancellationTokenSource();var task=capture.ReceiveBitmapAsync(lease,tx,bmp,a=>{a();return true;},owner=>accepted=owner,stop.Token);
        switch(change){case "cancel":stop.Cancel();break;case "replacement":capture.Enter(lease,Guid.NewGuid(),origin,new(24,24),OleEffect.Copy);break;case "on":gate.TryEnableLocalOperation(true);break;case "source":source=false;break;case "settings":settings=settings with{Version="v2"};break;case "hide":visible=false;break;case "1499":clock.Now+=1499;break;case "1500":clock.Now+=1500;break;}
        queue.Drain();await task;Assert.Equal(0,texts);Assert.Equal(0,files);Assert.Equal(change is "accept" or "1499",accepted!=null);if(accepted!=null){OleBitmapDrop.ValidateBmp(accepted.Bytes.Span);accepted.Dispose();}Assert.Equal(change=="on"?Activation.On:Activation.Off,gate.Snapshot.Activation);
    }
}
