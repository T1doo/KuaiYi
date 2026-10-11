using System.Buffers.Binary;
using System.IO.Compression;
using KuaiYi.Core;
using KuaiYi.Imaging;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class ScreenshotTests
{
    private static readonly PixelRect Left=new(-40,-16,0,24),Right=new(0,-16,48,24);
    private static ScreenshotTopology Topology(double dpi=1.5)=>new([new("left",Left,new(1)),new("right",Right,new(dpi))]);
    private static ScreenshotPixels Tile(PixelRect b)
    {
        var data=new byte[b.Width*b.Height*4];for(int y=b.Top;y<b.Bottom;y++)for(int x=b.Left;x<b.Right;x++){int i=((y-b.Top)*b.Width+x-b.Left)*4;data[i]=(byte)(x+100);data[i+1]=(byte)(y+50);data[i+2]=(byte)(x+y+100);data[i+3]=255;}return new(b,data);
    }
    private static ScreenshotSnapshot Snapshot(ScreenshotTopology? topology=null)=>new(topology??Topology(),[Tile(Left),Tile(Right)]);
    private static byte[] Rgb(ScreenshotPng png)
    {
        var bytes=png.Bytes.Span;int offset=8;var idat=new MemoryStream();var names=new List<string>();
        while(offset<bytes.Length)
        {
            int n=(int)BinaryPrimitives.ReadUInt32BigEndian(bytes[offset..]);string type=System.Text.Encoding.ASCII.GetString(bytes.Slice(offset+4,4));var data=bytes.Slice(offset+8,n);uint crc=uint.MaxValue;
            foreach(byte b in bytes.Slice(offset+4,n+4)){crc^=b;for(int bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0u);}Assert.Equal(~crc,BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset+8+n,4)));
            names.Add(type);if(type=="IDAT")idat.Write(data);offset+=n+12;
        }
        Assert.Equal(new[]{"IHDR","IDAT","IEND"},names);Assert.Equal(bytes.Length,offset);idat.Position=0;using var zip=new ZLibStream(idat,CompressionMode.Decompress);using var decoded=new MemoryStream();zip.CopyTo(decoded);return decoded.ToArray();
    }
    [Theory][InlineData(1)][InlineData(1.25)][InlineData(1.5)][InlineData(2)][InlineData(3)]
    public void RealMixedDpiNegativeCoordinatePngHasOnlySelectedPhysicalPixels(double dpi)
    {
        var topology=Topology(dpi);using var snapshot=Snapshot(topology);var full=snapshot.Tiles.Select(t=>t.Bytes).ToArray();
        var region=topology.Selection(new(15,12),new(-13,-8));using var crop=snapshot.CropAndRelease(region,topology);using var png=ScreenshotPngEncoder.Encode(crop);
        Assert.Equal(new ImageSize(28,20),ImagePolicy.InspectPng(png.Bytes.Span));foreach(var buffer in full)Assert.All(buffer.ToArray(),b=>Assert.Equal(0,b));
        var decoded=Rgb(png);for(int y=0;y<20;y++){Assert.Equal(0,decoded[y*85]);for(int x=0;x<28;x++){int i=y*85+1+x*3;Assert.Equal((byte)(x-13+y-8+100),decoded[i]);Assert.Equal((byte)(y-8+50),decoded[i+1]);Assert.Equal((byte)(x-13+100),decoded[i+2]);}}
    }
    [Theory][InlineData(0,0,7,8,ScreenshotFailure.RegionTooSmall)][InlineData(0,0,8,7,ScreenshotFailure.RegionTooSmall)][InlineData(-41,0,-30,10,ScreenshotFailure.RegionOutsideDesktop)][InlineData(0,-17,10,0,ScreenshotFailure.RegionOutsideDesktop)][InlineData(40,0,49,10,ScreenshotFailure.RegionOutsideDesktop)]
    public void InvalidSelectionAlwaysReleasesFullDesktop(int l,int t,int r,int b,ScreenshotFailure code)
    {
        using var snapshot=Snapshot();var full=snapshot.Tiles.Select(t=>t.Bytes).ToArray();Assert.Equal(code,Assert.Throws<ScreenshotException>(()=>snapshot.CropAndRelease(new(l,t,r,b),Topology())).Code);foreach(var memory in full)Assert.All(memory.ToArray(),value=>Assert.Equal(0,value));
    }
    [Fact]public void ExactlyEightPhysicalPixelsAndExclusiveEdges(){using var snapshot=Snapshot();using var crop=snapshot.CropAndRelease(new(40,16,48,24),Topology());Assert.Equal(8*8*4,crop.Bytes.Length);Assert.Equal((byte)147,crop.Bytes.Span[^4]);}
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]public void TopologyChangesReleaseFullDesktop(int type)
    {
        using var snapshot=Snapshot();var memory=snapshot.Tiles[0].Bytes;var changed=type switch{0=>new ScreenshotTopology([new("left",Left,new(1)),new("right",Right,new(2))]),1=>new([new("left",Left,new(1))]),_=>new([new("left",Left,new(1)),new("right",new(0,-16,49,24),new(1.5))])};
        Assert.Equal(ScreenshotFailure.TopologyChanged,Assert.Throws<ScreenshotException>(()=>snapshot.CropAndRelease(new(-8,0,8,8),changed)).Code);Assert.All(memory.ToArray(),b=>Assert.Equal(0,b));
    }
    [Fact]public void ScreenGapsAreNeutralPixelsWithoutStretching()
    {
        var a=new PixelRect(-20,0,-4,16);var b=new PixelRect(4,0,20,16);var topology=new ScreenshotTopology([new("a",a,new(1)),new("b",b,new(2))]);using var snapshot=new ScreenshotSnapshot(topology,[Tile(a),Tile(b)]);using var crop=snapshot.CropAndRelease(new(-8,0,8,8),topology);
        for(int y=0;y<8;y++)for(int x=4;x<12;x++)Assert.True(crop.Bytes.Span.Slice((y*16+x)*4,4).SequenceEqual(new byte[]{255,255,255,255}));
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]public void RejectInvalidOrOversizedTopologyBeforeCaptureAllocation(int kind)
    {
        Assert.Throws<ScreenshotException>(()=>new ScreenshotTopology(kind switch{0=>[],1=>[new("a",new(0,0,16,16),new(1)),new("b",new(8,0,24,16),new(1))],_=>[new("a",new(0,0,8000,5000),new(1))]}));
    }
    [Fact]public void RegionPixelLimitBeforeAllocation(){var topology=new ScreenshotTopology([new("a",new(0,0,6000,5000),new(1))]);Assert.Equal(ScreenshotFailure.RegionTooLarge,Assert.Throws<ScreenshotException>(()=>topology.Validate(new(0,0,6000,4001))).Code);topology.Validate(new(0,0,6000,4000));}
    [Fact]public void CancelledCropAndEncoderDoNotLeakOwnedPixels()
    {
        using var snapshot=Snapshot();var full=snapshot.Tiles[0].Bytes;using var cancel=new CancellationTokenSource();cancel.Cancel();Assert.ThrowsAny<OperationCanceledException>(()=>snapshot.CropAndRelease(new(-8,0,8,8),Topology(),cancel.Token));Assert.All(full.ToArray(),b=>Assert.Equal(0,b));
        using var crop=Tile(new(0,0,8,8));Assert.ThrowsAny<OperationCanceledException>(()=>ScreenshotPngEncoder.Encode(crop,cancel.Token));var held=crop.Bytes;crop.Dispose();Assert.All(held.ToArray(),b=>Assert.Equal(0,b));
    }
    [Fact]public void PngDisposeClearsActualReturnedBytes(){using var tile=Tile(new(0,0,8,8));using var png=ScreenshotPngEncoder.Encode(tile);var held=png.Bytes;Assert.NotEqual(0,held.Span[0]);png.Dispose();png.Dispose();Assert.All(held.ToArray(),b=>Assert.Equal(0,b));}
    [Fact]public void FixedSeedPhysicalCompositionProperties()
    {
        var random=new Random(87061);for(int test=0;test<1000;test++)
        {
            int l=random.Next(-40,30),t=random.Next(-16,16),r=random.Next(l+8,49),b=random.Next(t+8,25);var topology=Topology(1+random.Next(0,9)*.25);
            using var snapshot=Snapshot(topology);using var crop=snapshot.CropAndRelease(new(l,t,r,b),topology);
            for(int y=t;y<b;y++)for(int x=l;x<r;x++){int i=((y-t)*(r-l)+x-l)*4;Assert.Equal((byte)(x+100),crop.Bytes.Span[i]);Assert.Equal((byte)(y+50),crop.Bytes.Span[i+1]);}
        }
    }
    [Fact]public void CancelSelectionPreservesOldRequestAndDisablesPassiveAcquisition()
    {
        var gate=new RuntimeGate();var endpoint=new EndpointOrigin(new Uri("https://fixture.invalid"));gate.ConsentTo(endpoint);Assert.True(gate.TryEnable(endpoint,true));Assert.True(gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var old,out _));Assert.True(gate.TryBeginPassiveCapture(TriggerMode.Selection,out _));
        using(var intent=new ScreenshotIntent(gate)){Assert.True(gate.CanApply(old));Assert.False(gate.TryBeginPassiveCapture(TriggerMode.Selection,out _));Assert.True(intent.IsCurrent);}
        Assert.True(gate.CanApply(old));Assert.Equal(Activation.On,gate.Snapshot.Activation);Assert.Equal(Suspension.None,gate.Snapshot.Suspension);
    }
    [Fact]public void OffRequiresExplicitConfirmationAndNeverInheritsNetworkConsent()
    {
        var gate=new RuntimeGate();var endpoint=new EndpointOrigin(new Uri("https://fixture.invalid"));gate.ConsentTo(endpoint);using var intent=new ScreenshotIntent(gate);
        Assert.True(intent.RequiresConfirmation(false));Assert.False(intent.TryCommit(false,Guid.NewGuid(),Guid.NewGuid(),out _,out _));Assert.Equal(Activation.Off,gate.Snapshot.Activation);
        Assert.True(intent.TryCommit(true,Guid.NewGuid(),Guid.NewGuid(),out var identity,out _));Assert.True(gate.CanApply(identity));Assert.False(gate.CanSend(identity,endpoint));Assert.False(gate.TryBeginSend(identity,endpoint,()=>Task.FromResult(1),out _));
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]public void StaleScreenshotCannotOverrideOffLockReplacementOrMode(int action)
    {
        var gate=new RuntimeGate();gate.TryEnableLocalOperation(true);using var intent=new ScreenshotIntent(gate);RequestIdentity newer=default;
        switch(action){case 0:gate.Disable();break;case 1:gate.SetSessionLocked(true);break;case 2:gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out newer,out _);break;case 3:gate.SetMode(TriggerMode.Drop);break;}
        Assert.False(intent.IsCurrent);Assert.False(intent.TryCommit(true,Guid.NewGuid(),Guid.NewGuid(),out _,out _));intent.Dispose();if(action==2)Assert.True(gate.CanApply(newer));
    }
    [Fact]public async Task AtomicCaptureLeaseAllowsExactlyOneConcurrentOwner()
    {
        var gate=new RuntimeGate();using var barrier=new Barrier(3);ScreenshotIntent? a=null,b=null;int failed=0;
        Task Attempt(Action<ScreenshotIntent> accept)=>Task.Run(()=>{barrier.SignalAndWait();try{accept(new(gate));}catch(InvalidOperationException){Interlocked.Increment(ref failed);}});
        var one=Attempt(x=>a=x);var two=Attempt(x=>b=x);barrier.SignalAndWait();await Task.WhenAll(one,two);Assert.Equal(1,failed);(a??b)!.Dispose();Assert.Equal(Suspension.None,gate.Snapshot.Suspension);
    }
    [Theory][InlineData(false)][InlineData(true)]public async Task LateUncancellableCaptureIsObservedAndFullFramesCleared(bool off)
    {
        var gate=new RuntimeGate();using var intent=new ScreenshotIntent(gate);using var cancel=new CancellationTokenSource();var pending=new TaskCompletionSource<ScreenshotSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var acquiring=ScreenshotAcquisition.AcquireAsync(intent,_=>pending.Task,cancel.Token);if(off)gate.Disable();else cancel.Cancel();Assert.False(acquiring.IsCompleted);
        using var snapshot=Snapshot();var bytes=snapshot.Tiles[0].Bytes;pending.SetResult(snapshot);await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>acquiring);Assert.All(bytes.ToArray(),b=>Assert.Equal(0,b));
    }
    [Fact]public async Task GeneratedScreenshotPngEntersActualPrivateImageWorkerProtocol()
    {
        var gate=new RuntimeGate();using var intent=new ScreenshotIntent(gate);using var snapshot=Snapshot();using var crop=snapshot.CropAndRelease(new(-8,0,8,16),Topology());using var png=ScreenshotPngEncoder.Encode(crop);
        Assert.True(intent.TryCommit(true,Guid.NewGuid(),Guid.NewGuid(),out var identity,out _));
        string configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;string assembly=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../src/KuaiYi.ImageWorker/bin",configuration,"net10.0/win-x64/KuaiYi.ImageWorker.dll"));
        await using var client=new ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",assembly);var workflow=new ImageUiWorkflow(gate,new SynchronizationContext());var updates=new List<ImageUiUpdate>();
        Assert.True(await workflow.RunAsync(identity,token=>client.RunAsync(png.Bytes,new(identity.SessionId,1,identity.Epoch,"en-US",png.Bytes.Length),token),update=>updates.Add(update)));
        Assert.Equal(ImageUiEventKind.Started,updates[0].Kind);Assert.Equal(ImageUiEventKind.Failed,updates[^1].Kind);Assert.Equal(OperatingSystem.IsWindows()?nameof(ImageFailure.IdentityMissing):nameof(ImageFailure.UnsupportedPlatform),updates[^1].Status);Assert.Equal(HostExitState.ExitConfirmed,client.ExitState);
    }
    [Fact]public void NormalMouseReleaseIsNotLostCaptureCancellation()
    {
        var gesture=new ScreenshotDrag();gesture.Begin(new(-20,-10));gesture.Move(new(30,20));Assert.True(gesture.Active);
        Assert.Equal(new PixelRect(-20,-10,30,20),gesture.Finish(new(30,20)));Assert.False(gesture.Active);Assert.False(gesture.LostCapture());Assert.Null(gesture.Finish(new(1,1)));
    }
    [Fact]public void UnexpectedCaptureLossCancelsOnlyActiveGesture()
    {
        var gesture=new ScreenshotDrag();gesture.Begin(new(30,20));gesture.Move(new(-20,-10));Assert.True(gesture.LostCapture());Assert.False(gesture.LostCapture());gesture.Reset();Assert.Null(gesture.Region);
    }
    [Fact]public void OnReleaseStartsOnlyLocalRequestWithoutSecondConfirmation()
    {
        var gate=new RuntimeGate();gate.TryEnableLocalOperation(true);gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var old,out var stop);
        using var intent=new ScreenshotIntent(gate);Assert.False(intent.RequiresConfirmation(false));Assert.True(intent.RequiresConfirmation(true));
        Assert.True(intent.TryCommit(false,Guid.NewGuid(),Guid.NewGuid(),out var current,out _));Assert.True(stop.IsCancellationRequested);Assert.False(gate.CanApply(old));Assert.True(gate.CanApply(current));
    }
    [Fact]public void ActualEncodedPngByteLimitRejectsLargeIncompressibleSelection()
    {
        byte[] data=new byte[1920*4000*4];new Random(6328).NextBytes(data);using var crop=new ScreenshotPixels(new(0,0,1920,4000),data);
        Assert.Equal(ScreenshotFailure.RegionTooLarge,Assert.Throws<ScreenshotException>(()=>ScreenshotPngEncoder.Encode(crop)).Code);
    }
    [Fact]public void ActualAllBlackCapturedBufferIsRejectedAndCleared()
    {
        byte[] data=new byte[8*8*4];Assert.Equal(ScreenshotFailure.ProtectedOrBlackFrame,Assert.Throws<ScreenshotException>(()=>ScreenshotPixels.FromCapturedBuffer(new(0,0,8,8),data)).Code);Assert.All(data,b=>Assert.Equal(0,b));
    }
    [Fact]public void PartialBlackPixelsRemainBlackAndCaptureAlphaIsOpaque()
    {
        byte[] data=new byte[8*8*4];data[0]=128;using var image=ScreenshotPixels.FromCapturedBuffer(new(0,0,8,8),data);
        Assert.Equal(128,data[0]);for(int i=4;i<data.Length;i+=4)Assert.Equal(0,data[i]);for(int i=3;i<data.Length;i+=4)Assert.Equal(255,data[i]);
    }
}
