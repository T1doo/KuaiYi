using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
namespace KuaiYi.Core.Diagnostics;

public enum DiagnosticEvent { Started, ApplicationFailure, CaptureFinished, TranslationStarted, TranslationFinished, SettingsCommitted, ContentCleared }
public enum DiagnosticError { None, Cancelled, Timeout, AccessDenied, Io, InvalidInput, InvalidData, Authentication, RateLimited, Provider, Unexpected }
public enum DiagnosticOs { Windows, Linux, Other }
public enum DiagnosticComponent { App, Core, Providers, NativeHost, Imaging }
public enum DiagnosticCapability { WindowsInteraction, UiaSelection, ClipboardCompatibility, OleDrop, LocalOcr, ImageTranslation, RealModel }
public enum DiagnosticCapabilityState { NotRun, Available, Unavailable }
public sealed record DiagnosticMonitor(int Left,int Top,int Width,int Height,double DpiX,double DpiY);
public sealed record DiagnosticComponentVersion(DiagnosticComponent Component,Version Version);
public sealed record DiagnosticCapabilityResult(DiagnosticCapability Capability,DiagnosticCapabilityState State);
public sealed record DiagnosticEnvironment(DiagnosticOs Os,Version Build,ImmutableArray<DiagnosticMonitor> Monitors,ImmutableArray<DiagnosticComponentVersion> Components,ImmutableArray<DiagnosticCapabilityResult> Capabilities);
public sealed record DiagnosticEntry(DateTimeOffset Utc,Guid? RequestId,DiagnosticEvent Event,long? ElapsedMilliseconds,long? ByteCount,DiagnosticError Error,int? RetryCount);

/// <summary>Session-only metadata. No API accepts source text, URLs, paths, exception messages or server bodies.</summary>
public sealed class DiagnosticLog(Version applicationVersion,TimeProvider? clock=null):IDisposable
{
    public const int MaximumEntries=256;
    private readonly object sync=new();private readonly Queue<DiagnosticEntry> entries=[];private bool disposed;
    public Version ApplicationVersion {get;}=applicationVersion??throw new ArgumentNullException(nameof(applicationVersion));
    public bool Record(DiagnosticEvent kind,DiagnosticError error=DiagnosticError.None,Guid? requestId=null,long? elapsedMilliseconds=null,long? byteCount=null,int? retryCount=null)
    {
        if(!Enum.IsDefined(kind)||!Enum.IsDefined(error)||elapsedMilliseconds is <0 or >86_400_000||byteCount is <0 or >1_073_741_824||retryCount is <0 or >10)return false;
        lock(sync)
        {
            if(disposed)return false;
            while(entries.Count>=MaximumEntries)entries.Dequeue();
            entries.Enqueue(new((clock??TimeProvider.System).GetUtcNow(),requestId==Guid.Empty?null:requestId,kind,elapsedMilliseconds,byteCount,error,retryCount));return true;
        }
    }
    public bool RecordFailure(Exception error)=>Record(DiagnosticEvent.ApplicationFailure,Classify(error));
    public static DiagnosticError Classify(Exception error)=>error switch
    {
        OperationCanceledException=>DiagnosticError.Cancelled,TimeoutException=>DiagnosticError.Timeout,
        UnauthorizedAccessException=>DiagnosticError.AccessDenied,JsonException or InvalidDataException=>DiagnosticError.InvalidData,
        IOException=>DiagnosticError.Io,ArgumentException=>DiagnosticError.InvalidInput,_=>DiagnosticError.Unexpected
    };
    public ImmutableArray<DiagnosticEntry> Snapshot(){lock(sync)return entries.ToImmutableArray();}
    public void Clear(){lock(sync)entries.Clear();}
    public void Dispose(){lock(sync){disposed=true;entries.Clear();}}
    public DiagnosticReport CreateReport(DiagnosticEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if(!Enum.IsDefined(environment.Os)||environment.Build==null||environment.Monitors.IsDefault||environment.Monitors.Length>16||environment.Components.IsDefault||environment.Components.Length>5||environment.Capabilities.IsDefault||environment.Capabilities.Length>7)throw new ArgumentException("Invalid diagnostic metadata.");
        if(environment.Monitors.Any(m=>m==null||m.Left is <-2_000_000 or >2_000_000||m.Top is <-2_000_000 or >2_000_000||m.Width is <1 or >32768||m.Height is <1 or >32768||!double.IsFinite(m.DpiX)||!double.IsFinite(m.DpiY)||m.DpiX is <48 or >960||m.DpiY is <48 or >960)||environment.Components.Any(c=>c==null||!Enum.IsDefined(c.Component)||c.Version==null)||environment.Capabilities.Any(c=>c==null||!Enum.IsDefined(c.Capability)||!Enum.IsDefined(c.State)))throw new ArgumentException("Invalid diagnostic metadata.");
        var text=new StringBuilder();
        text.AppendLine("KuaiYi local diagnostic report v1");text.AppendLine("Session metadata only; no source, translation, image, clipboard, path, endpoint, model or secret. No automatic upload.");
        text.AppendLine("AppVersion="+ApplicationVersion);text.AppendLine("OS="+environment.Os+" Build="+environment.Build);
        for(int i=0;i<environment.Monitors.Length;i++)
        {var m=environment.Monitors[i];text.AppendLine(FormattableString.Invariant($"Monitor[{i}]={m.Left},{m.Top},{m.Width},{m.Height} DPI={m.DpiX:0.###},{m.DpiY:0.###}"));}
        foreach(var component in environment.Components)text.AppendLine("Component="+component.Component+" Version="+component.Version);
        foreach(var capability in environment.Capabilities)text.AppendLine("Capability="+capability.Capability+" Result="+capability.State);
        text.AppendLine("Capability results describe recorded checks only; NotRun is not a successful platform or model test.");
        foreach(var entry in Snapshot())text.AppendLine(string.Create(CultureInfo.InvariantCulture,$"{entry.Utc:O} Request={entry.RequestId?.ToString("D")??"None"} Event={entry.Event} ElapsedMs={entry.ElapsedMilliseconds?.ToString(CultureInfo.InvariantCulture)??"Unknown"} Bytes={entry.ByteCount?.ToString(CultureInfo.InvariantCulture)??"Unknown"} Error={entry.Error} Retries={entry.RetryCount?.ToString(CultureInfo.InvariantCulture)??"Unknown"}"));
        return new(text.ToString());
    }
}
public sealed class DiagnosticReport
{
    public const int MaximumBytes=131072;
    public string Text {get;}
    internal DiagnosticReport(string text){if(Encoding.UTF8.GetByteCount(text)>MaximumBytes)throw new InvalidOperationException("Diagnostic report limit.");Text=text;}
}
