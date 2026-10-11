using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
namespace KuaiYi.Core;
public enum ClipboardCaptureStatus { Success,Denied,Unsupported,Busy,Timeout,Unrelated,InvalidText,Cancelled,Failure }
public enum ClipboardRecovery { NotNeeded,Restored,Changed,Unconfirmed }
public sealed record ClipboardBlock(uint Format,string? Name,byte[] Bytes);
public sealed class ClipboardSnapshot:IDisposable
{
    public const int FormatLimit=32,BlockLimit=4*1024*1024,TotalLimit=8*1024*1024;
    public uint Sequence{get;} public ImmutableArray<ClipboardBlock> Blocks{get;}
    private ClipboardSnapshot(uint sequence,ImmutableArray<ClipboardBlock> blocks){Sequence=sequence;Blocks=blocks;}
    public static bool Supported(uint format,string? name)=>format is 1 or 7 or 8 or 13 or 15 or 16 or 17||format>=0xc000&&name is "HTML Format" or "Rich Text Format" or "PNG";
    public static ClipboardSnapshot Clone(uint sequence,IEnumerable<ClipboardBlock> source)
    {
        if(sequence==0)throw new InvalidDataException("Clipboard sequence unavailable.");var result=ImmutableArray.CreateBuilder<ClipboardBlock>();long total=0;var seen=new HashSet<uint>();
        try{foreach(var block in source){if(result.Count>=FormatLimit||!seen.Add(block.Format)||!Supported(block.Format,block.Name)||block.Bytes.Length is <1 or >BlockLimit||(total+=block.Bytes.Length)>TotalLimit)throw new InvalidDataException("Unsupported complete clipboard snapshot.");result.Add(block with{Bytes=(byte[])block.Bytes.Clone()});}return new(sequence,result.ToImmutable());}
        catch{foreach(var block in result)CryptographicOperations.ZeroMemory(block.Bytes);throw;}
    }
    public void Dispose(){foreach(var block in Blocks)CryptographicOperations.ZeroMemory(block.Bytes);}
}
public sealed record ClipboardCopyStart(long Timestamp,uint InputTime);
public sealed class ClipboardRead(uint sequence,byte[] utf16):IDisposable
{
    public uint Sequence{get;}=sequence;public byte[] Utf16{get;}=utf16;
    public void Dispose()=>CryptographicOperations.ZeroMemory(Utf16);
}
public sealed class ClipboardCapture(ClipboardCaptureStatus status,byte[]? utf8=null,ClipboardRecovery recovery=ClipboardRecovery.NotNeeded):IDisposable
{
    public ClipboardCaptureStatus Status{get;}=status;public byte[] Utf8{get;}=utf8??[];public ClipboardRecovery Recovery{get;internal set;}=recovery;
    public double ObservedMs{get;internal set;}
    public void Dispose()=>CryptographicOperations.ZeroMemory(Utf8);
}
public interface IClipboardPort
{
    uint Sequence{get;}
    ClipboardSnapshot Snapshot(); // Owns complete cloned memory, never an IDataObject or foreign handle.
    bool SourceCurrent();
    bool InputUnchanged(uint inputTime);
    ClipboardRead? Read(uint sequence); // Locked sequence + source owner check, exact UTF16 bytes.
    ClipboardRecovery Restore(ClipboardSnapshot snapshot,uint copiedSequence); // Compare sequence+owner inside the same clipboard lock.
    void Wait(TimeSpan duration,CancellationToken cancellation); // STA message pump; bounded wait.
}
/// <summary>Production STA transaction. Only the parent may authorize/send input; no old clipboard content becomes text.</summary>
public static class ClipboardTransaction
{
    public static readonly TimeSpan CopyBudget=TimeSpan.FromMilliseconds(350);
    public static ClipboardCapture Run(IClipboardPort port,Func<uint,ClipboardCopyStart?> issueCopy,CancellationToken cancellation,TimeProvider? provider=null)
    {
        var clock=provider??TimeProvider.System;ClipboardSnapshot? snapshot=null;ClipboardCapture? result=null;uint observed=0;bool copied=false;
        try
        {
            cancellation.ThrowIfCancellationRequested();if(!port.SourceCurrent())return new(ClipboardCaptureStatus.Denied);
            snapshot=port.Snapshot();cancellation.ThrowIfCancellationRequested();
            if(!port.SourceCurrent()||port.Sequence!=snapshot.Sequence)return new(ClipboardCaptureStatus.Unrelated);
            var sent=issueCopy(snapshot.Sequence);if(sent==null)return new(ClipboardCaptureStatus.Denied);
            copied=true;
            if(sent.Timestamp>clock.GetTimestamp()||sent.InputTime==0)return result=new(ClipboardCaptureStatus.Denied);
            while(true)
            {
                cancellation.ThrowIfCancellationRequested();
                if(clock.GetElapsedTime(sent.Timestamp)>=CopyBudget)return result=new(ClipboardCaptureStatus.Timeout);
                if(!port.SourceCurrent())return result=new(ClipboardCaptureStatus.Unrelated);
                uint sequence=port.Sequence;
                if(sequence!=snapshot.Sequence)
                {
                    if(!port.InputUnchanged(sent.InputTime))return result=new(ClipboardCaptureStatus.Unrelated);
                    using var read=port.Read(sequence);if(read==null)return result=new(ClipboardCaptureStatus.Unrelated);
                    // The read was source-owned/current at observation. Only this exact write may be restored, even on subsequent cancellation.
                    observed=read.Sequence;
                    cancellation.ThrowIfCancellationRequested();
                    if(clock.GetElapsedTime(sent.Timestamp)>=CopyBudget)return result=new(ClipboardCaptureStatus.Timeout);
                    if(!port.SourceCurrent()||!port.InputUnchanged(sent.InputTime)||port.Sequence!=observed)return result=new(ClipboardCaptureStatus.Unrelated);
                    byte[]? text=Decode(read.Utf16);if(text==null)return result=new(ClipboardCaptureStatus.InvalidText);
                    result=new(ClipboardCaptureStatus.Success,text){ObservedMs=clock.GetElapsedTime(sent.Timestamp).TotalMilliseconds};
                    if(cancellation.IsCancellationRequested||clock.GetElapsedTime(sent.Timestamp)>=CopyBudget||!port.SourceCurrent()||!port.InputUnchanged(sent.InputTime)||port.Sequence!=observed){result.Dispose();result=new(cancellation.IsCancellationRequested?ClipboardCaptureStatus.Cancelled:clock.GetElapsedTime(sent.Timestamp)>=CopyBudget?ClipboardCaptureStatus.Timeout:ClipboardCaptureStatus.Unrelated);}
                    return result;
                }
                var remaining=CopyBudget-clock.GetElapsedTime(sent.Timestamp);if(remaining<=TimeSpan.Zero)return result=new(ClipboardCaptureStatus.Timeout);
                port.Wait(remaining<TimeSpan.FromMilliseconds(10)?remaining:TimeSpan.FromMilliseconds(10),cancellation);
            }
        }
        catch(OperationCanceledException)when(cancellation.IsCancellationRequested){result?.Dispose();return result=new(ClipboardCaptureStatus.Cancelled);}
        catch(InvalidDataException){result?.Dispose();return result=new(ClipboardCaptureStatus.Unsupported);}
        catch(IOException){result?.Dispose();return result=new(ClipboardCaptureStatus.Busy);}
        finally
        {
            try{if(snapshot!=null&&observed!=0){ClipboardRecovery recovery;try{recovery=port.Restore(snapshot,observed);}catch(Exception){recovery=ClipboardRecovery.Unconfirmed;}if(result!=null)result.Recovery=recovery;}else if(snapshot!=null&&copied&&result!=null&&port.Sequence!=snapshot.Sequence)result.Recovery=result.Status==ClipboardCaptureStatus.Unrelated?ClipboardRecovery.Changed:ClipboardRecovery.Unconfirmed;}
            catch{result?.Dispose();throw;}
            finally{snapshot?.Dispose();}
        }
    }
    private static byte[]? Decode(byte[] bytes)
    {
        if(bytes.Length is <2 or >200006||bytes.Length%2!=0)return null;int end=-1;
        for(int i=0;i<bytes.Length;i+=2)if(bytes[i]==0&&bytes[i+1]==0){end=i;break;}
        if(end<0||end>=200004)return null;
        string text;try{text=new UnicodeEncoding(false,false,true).GetString(bytes,0,end);}catch(DecoderFallbackException){return null;}
        if(string.IsNullOrWhiteSpace(text)||text.EnumerateRunes().Count()>50000)return null;return new UTF8Encoding(false,true).GetBytes(text);
    }
}
public static class ClipboardApplicationConsent
{
    // Exact local Windows executable path, never inherited by basename or another application.
    public static string Normalize(string path)
    {
        string value=path.Replace('/','\\');if(value.Length is <7 or >260||!char.IsAsciiLetter(value[0])||value[1]!=':'||value[2]!='\\'||!value.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||value[3..].Split('\\').Any(p=>p is "" or "." or ".."||p.Any(c=>char.IsControl(c)||c is ':' or '*' or '?' or '"' or '<' or '>' or '|')))throw new InvalidDataException("Exact local executable path required.");return char.ToUpperInvariant(value[0])+value[1..];
    }
    public static bool Allowed(string executable,Settings.PrivacySettings settings)
    {try{return settings.ClipboardAllowedApplications.Any(p=>string.Equals(Normalize(p),Normalize(executable),StringComparison.OrdinalIgnoreCase));}catch(InvalidDataException){return false;}}
}
public sealed record SelectionCopyEvidence(long FocusHandle,ImmutableArray<int> RuntimeId,PixelRect ControlBounds,ImmutableArray<PixelRect> SelectionBounds,bool AutomaticTrusted);
public interface ISelectionCopyPlatform
{
    bool IsCopyCurrent(SelectionWindow source,SelectionCopyEvidence evidence);
    Task<SelectionCaptureResult> CaptureCopiedAsync(SelectionGesture gesture,SelectionCopyEvidence evidence,Func<uint,ClipboardCopyStart?> issueCopy,Func<bool> current,CancellationToken cancellation);
    ClipboardCopyStart? TryIssueCopy(SelectionGesture gesture,SelectionCopyEvidence evidence,uint sequence);
}
