using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace KuaiYi.Providers;

public sealed record SseEvent(string Event, string Data);

/// <summary>Bounded SSE framing over arbitrary byte reads; line framing preserves fragmented UTF-8 scalars.</summary>
public static class SseReader
{
    public const int MaximumEventBytes = 1024 * 1024;
    public static async IAsyncEnumerable<SseEvent> ReadAsync(Stream stream,
        [EnumeratorCancellation] CancellationToken cancellation = default,Func<CancellationToken>? readCancellation=null)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
        var line = new ArrayBufferWriter<byte>(256);
        var data = new StringBuilder();
        var encoding = new UTF8Encoding(false, true);
        string eventType = "message";
        bool firstLine = true, skipLf = false, hasData = false;
        int eventBytes = 0;
        try
        {
            while (true)
            {
                int count = await stream.ReadAsync(buffer.AsMemory(0, 4096), (readCancellation?.Invoke()??cancellation)).ConfigureAwait(false);
                if (count == 0) yield break; // EOF never dispatches an event lacking its final blank line.
                for (int i = 0; i < count; i++)
                {
                    byte b = buffer[i];
                    if (skipLf)
                    {
                        skipLf = false;
                        if (b == '\n')
                        {
                            if (eventBytes != 0 && ++eventBytes > MaximumEventBytes) throw new InvalidDataException("SSE event exceeds local limit.");
                            continue;
                        }
                    }
                    if (++eventBytes > MaximumEventBytes) throw new InvalidDataException("SSE event exceeds local limit.");
                    if (b is not (10 or 13))
                    {
                        line.GetSpan(1)[0] = b;
                        line.Advance(1);
                        continue;
                    }
                    skipLf = b == '\r';
                    string text = encoding.GetString(line.WrittenSpan);
                    line.Clear();
                    if (firstLine) { firstLine = false; if (text.StartsWith('\uFEFF')) text = text[1..]; }
                    if (text.Length == 0)
                    {
                        if (hasData) yield return new SseEvent(eventType, data.ToString(0, data.Length - 1));
                        data.Clear(); hasData = false; eventType = "message"; eventBytes = 0;
                    }
                    else if (text[0] != ':')
                    {
                        int colon = text.IndexOf(':');
                        string field = colon >= 0 ? text[..colon] : text;
                        string value = colon >= 0 ? text[(colon + 1)..] : string.Empty;
                        if (value.StartsWith(' ')) value = value[1..];
                        if (field == "data") { hasData = true; data.Append(value).Append('\n'); }
                        else if (field == "event") eventType = value;
                    }
                }
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
    }
}
