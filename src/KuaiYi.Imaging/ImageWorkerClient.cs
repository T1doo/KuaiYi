using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using KuaiYi.Contracts;
using KuaiYi.Core;
namespace KuaiYi.Imaging;

/// <summary>Owned one-shot process. Private inherited stdio is a P0 transport, not the production named-pipe ACL endpoint.</summary>
public sealed class ImageWorkerClient(string executable, string? managedAssembly = null) : IAsyncDisposable
{
    private Process? owned;
    private OwnedHostTermination? termination;
    private bool started;
    public HostExitState? ExitState => termination?.State;
    public bool ObserveExit() => termination?.ObserveExit() ?? true;
    public async Task<ImageReply> RunAsync(ReadOnlyMemory<byte> image, ImageJob job, CancellationToken cancellation = default)
    {
        if (started) throw new InvalidOperationException("Image client is single use.");
        started = true;
        cancellation.ThrowIfCancellationRequested();
        ImagePolicy.InspectPng(image.Span);
        if (image.Length != job.ByteLength) throw new InvalidDataException("Image length mismatch.");
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (managedAssembly != null) info.ArgumentList.Add(managedAssembly); // Test/diagnostic dotnet host; no shell.
        info.ArgumentList.Add("--private-stdio");
        var retained = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP", "PATH", "DOTNET_ROOT", "DOTNET_ROOT_X64" })
            if (info.Environment.TryGetValue(key, out var value) && value != null) retained[key] = value;
        info.Environment.Clear(); // Worker never inherits provider key environment variables.
        foreach (var pair in retained) info.Environment[pair.Key] = pair.Value;
        owned = Process.Start(info) ?? throw new IOException("Worker start failed.");
        termination = new(new StartedHostLifetime(owned));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, deadline.Token);
        Guid id = Guid.NewGuid();
        try
        {
            var send = owned.StandardInput.BaseStream; var receive = owned.StandardOutput.BaseStream;
            byte[] nonce = RandomNumberGenerator.GetBytes(32);
            try
            {
                await IpcFrameCodec.WriteAsync(send, new(IpcMessageType.ImageHello, id, nonce), linked.Token); await send.FlushAsync(linked.Token);
                var hello = await IpcFrameCodec.ReadAsync(receive, linked.Token) ?? throw new EndOfStreamException();
                try { if (hello.Type != IpcMessageType.ImageHello || hello.RequestId != id || !CryptographicOperations.FixedTimeEquals(nonce, hello.Payload)) throw new InvalidDataException("Worker hello mismatch."); }
                finally { CryptographicOperations.ZeroMemory(hello.Payload); }
            }
            finally { CryptographicOperations.ZeroMemory(nonce); }
            byte[] begin = ImageJson.Serialize(job);
            try { await IpcFrameCodec.WriteAsync(send, new(IpcMessageType.ImageBegin, id, begin), linked.Token); }
            finally { CryptographicOperations.ZeroMemory(begin); }
            int index = 0;
            for (int offset = 0; offset < image.Length;)
            {
                int size = Math.Min(IpcFrameCodec.BinaryLimit - 4, image.Length - offset);
                byte[] chunk = new byte[size + 4];
                try { BinaryPrimitives.WriteUInt32LittleEndian(chunk, (uint)index++); image.Span.Slice(offset,size).CopyTo(chunk.AsSpan(4)); await IpcFrameCodec.WriteAsync(send, new(IpcMessageType.BinaryChunk,id,chunk), linked.Token); }
                finally { CryptographicOperations.ZeroMemory(chunk); }
                offset += size;
            }
            await IpcFrameCodec.WriteAsync(send, new(IpcMessageType.ImageCommit,id,[]), linked.Token); await send.FlushAsync(linked.Token);
            var frame = await IpcFrameCodec.ReadAsync(receive, linked.Token) ?? throw new EndOfStreamException();
            ImageReply reply;
            try
            {
                if (frame.Type != IpcMessageType.BinaryChunk || frame.RequestId != id) throw new InvalidDataException("Worker reply mismatch.");
                reply = ImageJson.Read<ImageReply>(frame.Payload);
                if (reply.Job != job || !Enum.IsDefined(reply.Failure) || (reply.Failure == ImageFailure.None) != (reply.Output != null)) throw new InvalidDataException("Worker result identity/status mismatch.");
                if (reply.Output is { } result) ValidateOutput(result, ImagePolicy.InspectPng(image.Span), job.Language);
            }
            finally { CryptographicOperations.ZeroMemory(frame.Payload); }
            owned.StandardInput.Close();
            await owned.WaitForExitAsync(linked.Token);
            if (owned.ExitCode != 0) throw new ImageJobException(ImageFailure.ProtocolFailure);
            if (!termination.ObserveExit()) throw new ImageJobException(ImageFailure.ExitUnconfirmed);
            cancellation.ThrowIfCancellationRequested();
            return reply;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellation.IsCancellationRequested) { throw new ImageJobException(ImageFailure.Timeout); }
        finally
        {
            if (!termination.ObserveExit() && await termination.StopAsync(TimeSpan.FromSeconds(2)) != HostExitState.ExitConfirmed)
                throw new ImageJobException(ImageFailure.ExitUnconfirmed);
        }
    }
    public static void ValidateOutput(OcrOutput result, ImageSize expected, string language)
    {
        if (result.Width != expected.Width || result.Height != expected.Height || !string.Equals(result.Language, language, StringComparison.OrdinalIgnoreCase) ||
            result.Text == null || result.Text.Length > 50_000 || result.Lines == null || result.Lines.Length > 10_000) throw new InvalidDataException("Invalid OCR output.");
        int count = 0;
        foreach (var line in result.Lines)
        {
            if (line == null || line.Text == null || line.Text.Length > 50_000 || line.Words == null || line.Words.Length > 50_000 - count) throw new InvalidDataException("Invalid OCR line.");
            count += line.Words.Length;
            foreach (var word in line.Words)
                if (word == null || word.Text == null || word.Text.Length > 50_000 || !double.IsFinite(word.X) || !double.IsFinite(word.Y) || !double.IsFinite(word.Width) || !double.IsFinite(word.Height) ||
                    word.X < 0 || word.Y < 0 || word.Width < 0 || word.Height < 0 || word.X + word.Width > expected.Width || word.Y + word.Height > expected.Height) throw new InvalidDataException("Invalid OCR word bounds.");
        }
    }
    public async ValueTask DisposeAsync()
    {
        if (termination != null && !termination.ObserveExit() && await termination.StopAsync(TimeSpan.FromSeconds(2)) != HostExitState.ExitConfirmed)
            throw new ImageJobException(ImageFailure.ExitUnconfirmed); // Retain ownership; never call an unconfirmed kill 'closed'.
        owned?.Dispose(); owned = null;
    }
}
