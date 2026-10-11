using System.Security.Cryptography;
using System.Text;
using KuaiYi.Core;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class ClipboardTransactionTests
{
    private sealed class Clock:TimeProvider{public long Now=10000;public override long TimestampFrequency=>1000;public override long GetTimestamp()=>Now;}
    private sealed class Port(Clock clock):IClipboardPort
    {
        public uint Value=10;public bool Trusted=true,Marker=true,Owned=true;public int Reads,Restores,Writes,Snapshots;public ClipboardSnapshot? Saved;public byte[]? ReadBuffer;public Action? DuringRead,BeforeRestore,DuringWait;public List<ClipboardBlock> Formats=[new(13,null,Encoding.Unicode.GetBytes("OLD\0")),new(0xc001,"HTML Format","public HTML\0"u8.ToArray()),new(0xc002,"Rich Text Format","{public RTF}\0"u8.ToArray()),new(15,null,[1,2,3,4]),new(8,null,[4,3,2,1])];public List<ClipboardBlock>? Restored;public byte[] NewText=Encoding.Unicode.GetBytes("NEW\n  code\0");public bool RestoreThrows;
        public uint Sequence=>Value;public bool SourceCurrent()=>Trusted;public bool InputUnchanged(uint time)=>Marker&&time==42;
        public Action? DuringSnapshot;
        public ClipboardSnapshot Snapshot(){Snapshots++;Saved=ClipboardSnapshot.Clone(Value,Formats);DuringSnapshot?.Invoke();return Saved;}
        public ClipboardRead? Read(uint sequence){Reads++;if(Value!=sequence||!Owned)return null;ReadBuffer=(byte[])NewText.Clone();DuringRead?.Invoke();return new(sequence,ReadBuffer);}
        public ClipboardRecovery Restore(ClipboardSnapshot snapshot,uint sequence){Restores++;BeforeRestore?.Invoke();if(RestoreThrows)throw new IOException("public restore failure");if(Value!=sequence||!Owned)return ClipboardRecovery.Changed;Restored=snapshot.Blocks.Select(b=>b with{Bytes=(byte[])b.Bytes.Clone()}).ToList();Writes++;Value++;return ClipboardRecovery.Restored;}
        public void Wait(TimeSpan duration,CancellationToken cancellation){clock.Now+=(long)duration.TotalMilliseconds;DuringWait?.Invoke();cancellation.ThrowIfCancellationRequested();}
    }
    [Theory][InlineData(1,null)][InlineData(7,null)][InlineData(8,null)][InlineData(13,null)][InlineData(15,null)][InlineData(16,null)][InlineData(17,null)][InlineData(0xc001,"HTML Format")][InlineData(0xc002,"Rich Text Format")][InlineData(0xc003,"PNG")]
    public void EverySupportedMemoryFormatIsDeepCopiedAndOwnedBuffersCleared(uint format,string? name)
    {
        byte[] original=[83,84,85];using var snapshot=ClipboardSnapshot.Clone(10,[new(format,name,original)]);Assert.NotSame(original,snapshot.Blocks[0].Bytes);original[0]=99;Assert.Equal(83,snapshot.Blocks[0].Bytes[0]);snapshot.Dispose();Assert.All(snapshot.Blocks[0].Bytes,b=>Assert.Equal(0,b));Assert.Equal(99,original[0]);
    }
    [Theory][InlineData(2,null)][InlineData(3,null)][InlineData(9,null)][InlineData(14,null)][InlineData(0x80,null)][InlineData(0x200,null)][InlineData(0xc010,"FileGroupDescriptorW")][InlineData(0xc011,"arbitrary OLE")]
    public void UnsupportedOrVirtualFormatsRejectEntireSnapshotBeforeCopy(uint format,string? name)
    {var clock=new Clock();var port=new Port(clock);port.Formats.Add(new(format,name,[83]));int sends=0;using var result=ClipboardTransaction.Run(port,_=>{sends++;return new(clock.Now,42);},default,clock);Assert.Equal(ClipboardCaptureStatus.Unsupported,result.Status);Assert.Equal(0,sends);Assert.Equal(0,port.Reads);Assert.Equal(0,port.Writes);}
    [Theory][InlineData("count")][InlineData("block")][InlineData("total")][InlineData("duplicate")][InlineData("zero-sequence")]
    public void SnapshotBoundsRejectCompleteCombination(string kind)
    {
        var blocks=kind switch{"count"=>Enumerable.Range(0,33).Select(i=>new ClipboardBlock((uint)(0xc000+i),"HTML Format",[83])).ToList(),"block"=>[new(13,null,new byte[ClipboardSnapshot.BlockLimit+1])],"total"=>[new(13,null,new byte[ClipboardSnapshot.BlockLimit]),new(8,null,new byte[ClipboardSnapshot.BlockLimit]),new(15,null,[83])],"duplicate"=>[new(13,null,[83]),new(13,null,[84])],_=>new List<ClipboardBlock>()};Assert.Throws<InvalidDataException>(()=>ClipboardSnapshot.Clone(kind=="zero-sequence"?0u:10u,blocks));
    }
    [Fact]public void FullCombinationRestoresEveryOriginalFormatAndClearsTransientBuffers()
    {
        var clock=new Clock();var port=new Port(clock);int sends=0;using var result=ClipboardTransaction.Run(port,_=>{sends++;port.Value=11;return new(clock.Now,42);},default,clock);Assert.Equal(ClipboardCaptureStatus.Success,result.Status);Assert.Equal("NEW\n  code",Encoding.UTF8.GetString(result.Utf8));Assert.Equal(ClipboardRecovery.Restored,result.Recovery);Assert.Equal(1,sends);Assert.Equal(port.Formats.Count,port.Restored!.Count);
        foreach(var original in port.Formats){var restored=Assert.Single(port.Restored,b=>b.Format==original.Format);Assert.Equal(original.Name,restored.Name);Assert.Equal(original.Bytes,restored.Bytes);}Assert.All(port.Saved!.Blocks,b=>Assert.All(b.Bytes,v=>Assert.Equal(0,v)));Assert.All(port.ReadBuffer!,b=>Assert.Equal(0,b));byte[] output=result.Utf8;result.Dispose();Assert.All(output,b=>Assert.Equal(0,b));
    }
    [Theory][InlineData("no-change",ClipboardCaptureStatus.Timeout)][InlineData("unknown-owner",ClipboardCaptureStatus.Unrelated)][InlineData("foreground",ClipboardCaptureStatus.Unrelated)][InlineData("input",ClipboardCaptureStatus.Unrelated)][InlineData("snapshot-overwritten",ClipboardCaptureStatus.Unrelated)]
    public void OldOrUncertainClipboardNeverBecomesText(string reason,ClipboardCaptureStatus expected)
    {
        var clock=new Clock();var port=new Port(clock);if(reason=="snapshot-overwritten")port.DuringSnapshot=()=>port.Value=99;int sends=0;
        using var result=ClipboardTransaction.Run(port,_=>{sends++;if(reason!="no-change")port.Value++;if(reason=="unknown-owner")port.Owned=false;if(reason=="foreground")port.Trusted=false;if(reason=="input")port.Marker=false;return new(clock.Now,42);},default,clock);
        Assert.Equal(expected,result.Status);Assert.Empty(result.Utf8);Assert.Equal(0,port.Writes);Assert.All(port.Saved!.Blocks,b=>Assert.All(b.Bytes,v=>Assert.Equal(0,v)));Assert.Equal(reason=="snapshot-overwritten"?0:1,sends);
    }
    [Theory][InlineData("sequence")][InlineData("owner")]
    public void RestoreChecksSequenceAndOwnerTogetherWithoutOverwritingConcurrentCopy(string change)
    {
        var clock=new Clock();var port=new Port(clock){BeforeRestore=()=>{}};port.BeforeRestore=()=>{if(change=="sequence")port.Value=99;else port.Owned=false;};using var result=ClipboardTransaction.Run(port,_=>{port.Value=11;return new(clock.Now,42);},default,clock);Assert.Equal(ClipboardCaptureStatus.Success,result.Status);Assert.Equal(ClipboardRecovery.Changed,result.Recovery);Assert.Equal(0,port.Writes);Assert.Null(port.Restored);
    }
    [Fact]public void FailedCompleteRestoreIsExplicitAndStillClearsAllOriginalMemory()
    {
        var clock=new Clock();var port=new Port(clock){RestoreThrows=true};using var result=ClipboardTransaction.Run(port,_=>{port.Value=11;return new(clock.Now,42);},default,clock);Assert.Equal(ClipboardRecovery.Unconfirmed,result.Recovery);Assert.All(port.Saved!.Blocks,b=>Assert.All(b.Bytes,v=>Assert.Equal(0,v)));Assert.All(port.ReadBuffer!,v=>Assert.Equal(0,v));
    }
    [Theory][InlineData("cancel",ClipboardCaptureStatus.Cancelled)][InlineData("deadline",ClipboardCaptureStatus.Timeout)][InlineData("source",ClipboardCaptureStatus.Unrelated)][InlineData("sequence",ClipboardCaptureStatus.Unrelated)]
    public void LateReadIsNeverDeliveredAndExactOwnWriteMayStillBeRestored(string change,ClipboardCaptureStatus expected)
    {
        var clock=new Clock();using var cancellation=new CancellationTokenSource();var port=new Port(clock);port.DuringRead=()=>{switch(change){case "cancel":cancellation.Cancel();break;case "deadline":clock.Now+=350;break;case "source":port.Trusted=false;break;default:port.Value=99;break;}};
        using var result=ClipboardTransaction.Run(port,_=>{port.Value=11;return new(clock.Now,42);},cancellation.Token,clock);Assert.Equal(expected,result.Status);Assert.Empty(result.Utf8);Assert.All(port.ReadBuffer!,v=>Assert.Equal(0,v));Assert.Equal(change=="sequence"?0:1,port.Writes);Assert.Equal(change=="sequence"?ClipboardRecovery.Changed:ClipboardRecovery.Restored,result.Recovery);
    }
    [Theory][InlineData("unterminated")][InlineData("odd")][InlineData("surrogate")][InlineData("space")][InlineData("limit")][InlineData("empty")]
    public void InvalidOrIncompleteUnicodeNeverBecomesSource(string kind)
    {
        var clock=new Clock();var port=new Port(clock){NewText=kind switch{"unterminated"=>Encoding.Unicode.GetBytes("public"),"odd"=>[83,0,0],"surrogate"=>[0,0xd8,0,0],"space"=>Encoding.Unicode.GetBytes(" \0"),"empty"=>[],_=>Encoding.Unicode.GetBytes(new string('a',50001)+"\0")}};using var result=ClipboardTransaction.Run(port,_=>{port.Value=11;return new(clock.Now,42);},default,clock);Assert.Equal(ClipboardCaptureStatus.InvalidText,result.Status);Assert.Empty(result.Utf8);Assert.All(port.ReadBuffer!,v=>Assert.Equal(0,v));Assert.Equal(ClipboardRecovery.Restored,result.Recovery);
    }
    [Theory][InlineData(349,ClipboardCaptureStatus.Success)][InlineData(350,ClipboardCaptureStatus.Timeout)][InlineData(351,ClipboardCaptureStatus.Timeout)]
    public void CompletedReadUsesAbsolute350msCopyTime(int elapsed,ClipboardCaptureStatus expected)
    {
        var clock=new Clock();var port=new Port(clock){DuringRead=()=>clock.Now+=elapsed};using var result=ClipboardTransaction.Run(port,_=>{port.Value=11;return new(clock.Now,42);},default,clock);Assert.Equal(expected,result.Status);if(expected!=ClipboardCaptureStatus.Success)Assert.Empty(result.Utf8);Assert.All(port.ReadBuffer!,v=>Assert.Equal(0,v));
    }
    [Fact]public void PreCancelledOrUntrustedSourceDoesNotSnapshotOrSend()
    {
        var clock=new Clock();var port=new Port(clock);using var cancellation=new CancellationTokenSource();cancellation.Cancel();using var cancelled=ClipboardTransaction.Run(port,_=>throw new InvalidOperationException(),cancellation.Token,clock);Assert.Equal(ClipboardCaptureStatus.Cancelled,cancelled.Status);port.Trusted=false;using var denied=ClipboardTransaction.Run(port,_=>throw new InvalidOperationException(),default,clock);Assert.Equal(ClipboardCaptureStatus.Denied,denied.Status);Assert.Equal(0,port.Snapshots);Assert.Equal(0,port.Reads);
    }
    [Fact]public void ClipboardSnapshotIsRecheckedBeforeCopyAndLateUpdateAfterTimeoutIsNotReplayed()
    {
        var clock=new Clock();var port=new Port(clock);int sends=0;using var result=ClipboardTransaction.Run(port,_=>{sends++;return new(clock.Now,42);},default,clock);Assert.Equal(ClipboardCaptureStatus.Timeout,result.Status);int reads=port.Reads;port.Value=11;Assert.Equal(reads,port.Reads);Assert.Empty(result.Utf8);Assert.Equal(1,sends);Assert.Equal(0,port.Writes);
    }
    [Fact]public void WaitingForOwnInjectedInputBeforeClipboardChangeDoesNotDiscardValidCopy()
    {
        var clock=new Clock();var port=new Port(clock){Marker=false};port.DuringWait=()=>{port.Marker=true;port.Value=11;};using var result=ClipboardTransaction.Run(port,_=>new(clock.Now,42),default,clock);Assert.Equal(ClipboardCaptureStatus.Success,result.Status);Assert.Equal(10,result.ObservedMs);Assert.Equal(1,port.Reads);Assert.Equal(ClipboardRecovery.Restored,result.Recovery);
    }
    [Fact]public void OriginallyEmptyClipboardRestoresCompleteEmptyState()
    {var clock=new Clock();var port=new Port(clock){Formats=[]};using var result=ClipboardTransaction.Run(port,_=>{port.Value=11;return new(clock.Now,42);},default,clock);Assert.Equal(ClipboardCaptureStatus.Success,result.Status);Assert.Equal(ClipboardRecovery.Restored,result.Recovery);Assert.NotNull(port.Restored);Assert.Empty(port.Restored);Assert.Equal(1,port.Writes);}
}
