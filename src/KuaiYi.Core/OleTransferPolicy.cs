using System.Text;

namespace KuaiYi.Core;

[Flags] public enum OleEffect : uint { None = 0, Copy = 1, Move = 2, Link = 4, Scroll = 0x80000000 }
public enum OleExtractionPath { SynchronousBeforeDropReturn, NegotiatedAsync }

public static class OleTransferPolicy
{
    public static OleEffect Preview(bool enabled, OleEffect offered, bool unicodeAdvertised) =>
        enabled && unicodeAdvertised && offered.HasFlag(OleEffect.Copy) ? OleEffect.Copy : OleEffect.None;
    public static OleExtractionPath Extraction(bool capabilityAvailable, bool asyncMode, bool startSucceeded) =>
        capabilityAvailable && asyncMode && startSucceeded ? OleExtractionPath.NegotiatedAsync : OleExtractionPath.SynchronousBeforeDropReturn;
}

/// <summary>A Drop is consumed once, independently from pointer motion. Leave/Off never read data.</summary>
public sealed class OleDropTransaction
{
    private Guid hover, consumed;
    public Guid Enter() => hover = Guid.NewGuid();
    public void Leave() => hover = Guid.Empty;
    public bool TryConsume(Guid id, bool enabled, OleEffect offered, bool unicodeAdvertised)
    {
        if (id == Guid.Empty || id != hover || id == consumed || OleTransferPolicy.Preview(enabled, offered, unicodeAdvertised) != OleEffect.Copy) return false;
        consumed = id; hover = Guid.Empty; return true;
    }
}

/// <summary>Fixed deadline of one native call group. Progress cannot extend it; owners terminate the isolated process.</summary>
public sealed class OleCallDeadline(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;
    private Guid scope;
    private long epoch, started;
    private TimeSpan duration;
    public bool IsArmed => scope != Guid.Empty;
    public bool Begin(Guid id, long operationEpoch, TimeSpan timeout)
    {
        if (id == Guid.Empty || operationEpoch < 0 || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(10)) throw new ArgumentException("Invalid OLE deadline.");
        if (IsArmed) return scope == id && epoch == operationEpoch;
        scope = id; epoch = operationEpoch; duration = timeout; started = time.GetTimestamp(); return true;
    }
    public bool Complete(Guid id, long operationEpoch)
    {
        if (!IsArmed || scope != id || epoch != operationEpoch) return false;
        scope = Guid.Empty; return true;
    }
    public bool Expired => IsArmed && time.GetElapsedTime(started) >= duration;
    public void Reset() => scope = Guid.Empty;
}

public static class OleUnicodeText
{
    public const int MaximumScalars = 50_000;
    public const int MaximumReadUnits = 100_002;
    public const int MaximumGlobalBytes = (MaximumReadUnits + 1) * 2;
    public static string Decode(ReadOnlySpan<byte> globalBytes)
    {
        if (globalBytes.Length is < 2 or > MaximumGlobalBytes || globalBytes.Length % 2 != 0) throw new InvalidDataException("Invalid Unicode HGLOBAL size.");
        int end = -1;
        for (int i = 0; i < globalBytes.Length; i += 2) if (globalBytes[i] == 0 && globalBytes[i + 1] == 0) { end = i; break; }
        if (end < 0) throw new InvalidDataException("Unicode HGLOBAL is not terminated.");
        string text;
        try { text = new UnicodeEncoding(false, false, true).GetString(globalBytes[..end]); }
        catch (DecoderFallbackException error) { throw new InvalidDataException("Invalid source UTF-16.", error); }
        if (text.EnumerateRunes().Count() > MaximumScalars) throw new InvalidDataException("Oversized dropped text.");
        text = text.Trim();
        if (text.Length == 0) throw new InvalidDataException("Empty dropped text.");
        return text;
    }
}
