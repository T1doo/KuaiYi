using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace KuaiYi.OleTransfer;

public enum OleStartupStage
{
    Entry, Apartment, Dispatcher, HelloRead, HelloValidated, BackendCreate,
    ParentVerify, ActivationOpen, NativeExportCreate, ExportValidate, ExportQueued,
    Controls, ParentStart, ParentHelloWrite, ParentHelloRead, ParentExportRead,
    ParentRegister, ParentReady, ParentRun, ParentShutdown
}
public enum OleExceptionCode
{
    None, Argument, InvalidOperation, InvalidData, Io, FileMissing, DirectoryMissing,
    Unauthorized, DllMissing, BadImage, Com, Cancelled, Disposed, Timeout, Other
}
public sealed record OleStartupDiagnostic(OleStartupStage Stage, OleExceptionCode Exception, int HResult);

/// <summary>Metadata only. Never formats messages, paths, type names, payloads or raw child output.</summary>
public static class OleStartupDiagnostics
{
    public const int LineLimit = 128, RecordLimit = 32;
    private const string Prefix = "KYOD1";
    public static OleExceptionCode Code(Exception error) => error switch
    {
        FileNotFoundException => OleExceptionCode.FileMissing,
        DirectoryNotFoundException => OleExceptionCode.DirectoryMissing,
        InvalidDataException => OleExceptionCode.InvalidData,
        IOException => OleExceptionCode.Io,
        ObjectDisposedException => OleExceptionCode.Disposed,
        InvalidOperationException => OleExceptionCode.InvalidOperation,
        UnauthorizedAccessException => OleExceptionCode.Unauthorized,
        DllNotFoundException => OleExceptionCode.DllMissing,
        BadImageFormatException => OleExceptionCode.BadImage,
        System.Runtime.InteropServices.COMException => OleExceptionCode.Com,
        OperationCanceledException => OleExceptionCode.Cancelled,
        TimeoutException => OleExceptionCode.Timeout,
        ArgumentException => OleExceptionCode.Argument,
        _ => OleExceptionCode.Other
    };
    public static OleStartupDiagnostic Project(OleStartupStage stage, Exception? error = null)
        => new(stage, error is null ? OleExceptionCode.None : Code(error), error?.HResult ?? 0);
    public static string Format(OleStartupDiagnostic item)
    {
        if (!Enum.IsDefined(item.Stage) || !Enum.IsDefined(item.Exception) || item.Exception == OleExceptionCode.None && item.HResult != 0)
            throw new ArgumentException("Invalid diagnostic metadata.");
        return $"{Prefix}|{item.Stage}|{item.Exception}|{item.HResult.ToString(CultureInfo.InvariantCulture)}";
    }
    public static bool TryParse(ReadOnlySpan<byte> bytes, out OleStartupDiagnostic? item)
    {
        item = null;
        if (bytes.Length is < 1 or > LineLimit) return false;
        foreach (byte value in bytes)
            if (!(value is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z' or >= (byte)'0' and <= (byte)'9' or (byte)'|' or (byte)'-')) return false;
        string[] parts = Encoding.ASCII.GetString(bytes).Split('|');
        if (parts.Length != 4 || parts[0] != Prefix ||
            !Enum.TryParse(parts[1], out OleStartupStage stage) || !Enum.IsDefined(stage) || stage.ToString() != parts[1] ||
            !Enum.TryParse(parts[2], out OleExceptionCode code) || !Enum.IsDefined(code) || code.ToString() != parts[2] ||
            !int.TryParse(parts[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int hr) ||
            hr.ToString(CultureInfo.InvariantCulture) != parts[3] || code == OleExceptionCode.None && hr != 0) return false;
        item = new(stage, code, hr); return true;
    }
    public static void Emit(OleStartupStage stage, Exception? error = null)
        => Console.Error.WriteLine(Format(Project(stage, error)));
}

/// <summary>Always drains the owned stderr pipe, including oversized/unknown output. Retains only fixed metadata.</summary>
public sealed class OleStartupDiagnosticDrain
{
    private readonly object sync = new();
    private readonly List<OleStartupDiagnostic> items = [];
    private readonly List<OleExportObservation> exports = [];
    private readonly OleTargetDiagnostics targets = new();
    public OleTargetObservation[] TargetSnapshot => targets.Snapshot;
    public OleExportObservation[] ExportSnapshot { get { lock (sync) return exports.ToArray(); } }
    public OleExceptionCode Failure { get; private set; }
    public OleStartupDiagnostic[] Snapshot { get { lock (sync) return items.ToArray(); } }
    public async Task RunAsync(Stream input, CancellationToken cancellation = default)
    {
        byte[] chunk = new byte[4096], line = new byte[OleStartupDiagnostics.LineLimit];
        int length = 0; bool discard = false;
        try
        {
            int count;
            while ((count = await input.ReadAsync(chunk, cancellation).ConfigureAwait(false)) != 0)
            {
                for (int i = 0; i < count; i++)
                {
                    byte value = chunk[i];
                    if (value == (byte)'\n')
                    {
                        int end = length > 0 && line[length - 1] == (byte)'\r' ? length - 1 : length;
                        if (!discard)
                        {
                            if (OleStartupDiagnostics.TryParse(line.AsSpan(0, end), out var item))
                            { lock (sync) if (items.Count + exports.Count < OleStartupDiagnostics.RecordLimit) items.Add(item!); }
                            else if (OleTargetDiagnostics.TryParse(line.AsSpan(0, end), out var target)) targets.Observe(target);
                            else if (OleExportContract.TryParse(line.AsSpan(0, end), out var export))
                            { lock (sync) if (items.Count + exports.Count < OleStartupDiagnostics.RecordLimit) exports.Add(export!); }
                        }
                        CryptographicOperations.ZeroMemory(line); length = 0; discard = false;
                    }
                    else if (!discard)
                    {
                        if (length == line.Length) { CryptographicOperations.ZeroMemory(line); length = 0; discard = true; }
                        else line[length++] = value;
                    }
                }
                CryptographicOperations.ZeroMemory(chunk);
            }
            // A truncated final record is never accepted.
        }
        catch (Exception error) { Failure = OleStartupDiagnostics.Code(error); }
        finally { CryptographicOperations.ZeroMemory(chunk); CryptographicOperations.ZeroMemory(line); }
    }
}
