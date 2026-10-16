using System.Security.Cryptography;
using KuaiYi.Contracts;
using KuaiYi.Imaging;
namespace KuaiYi.ImageWorker;
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.SequenceEqual(new[] { "--diagnose" })) { Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(NativeOcr.Diagnose(), ImageJson.Options)); return 0; }
        if (!args.SequenceEqual(new[] { "--private-stdio" })) { Console.Error.WriteLine("ImageWorker: private inherited stdio or --diagnose only."); return 2; }
        // Streams are inherited from the owned parent; no caller-provided named pipe, filesystem image or API secret.
        await using var input = Console.OpenStandardInput();
        await using var output = Console.OpenStandardOutput();
        using var receiver = new ImageJobReceiver();
        try
        {
            var hello = await IpcFrameCodec.ReadAsync(input) ?? throw new EndOfStreamException();
            if (hello.Type != IpcMessageType.ImageHello || hello.Payload.Length != 32) throw new InvalidDataException("Private hello required.");
            await IpcFrameCodec.WriteAsync(output, new(IpcMessageType.ImageHello, hello.RequestId, hello.Payload));
            await output.FlushAsync();
            CryptographicOperations.ZeroMemory(hello.Payload);
            var begin = await IpcFrameCodec.ReadAsync(input) ?? throw new EndOfStreamException();
            try { if (begin.RequestId != hello.RequestId) throw new InvalidDataException("Begin/hello identity mismatch."); receiver.Begin(begin); }
            finally { CryptographicOperations.ZeroMemory(begin.Payload); }
            ReadOnlyMemory<byte> image;
            while (true)
            {
                var frame = await IpcFrameCodec.ReadAsync(input) ?? throw new EndOfStreamException();
                try { if (frame.Type == IpcMessageType.ImageCommit) { image = receiver.Commit(frame); break; } receiver.Chunk(frame); }
                finally { CryptographicOperations.ZeroMemory(frame.Payload); }
            }
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var recognize = Task.Run(() => NativeOcr.Recognize(image, receiver.Job!.Language, cancellation.Token));
            // Reader must consume EOF/late control even when OCR wins the race; native cancellation must not hide queued commands.
            var disconnect = Task.Run(async () => await IpcFrameCodec.ReadAsync(input));
            ImageReply reply;
            try
            {
                if (await Task.WhenAny(recognize, disconnect) == disconnect)
                {
                    var control = await disconnect;
                    if (control != null)
                    {
                        try { if (control.Type != IpcMessageType.ImageCancel || control.RequestId != receiver.RequestId || control.Payload.Length != 0) throw new InvalidDataException("Invalid cancel."); }
                        finally { CryptographicOperations.ZeroMemory(control.Payload); }
                    }
                    cancellation.Cancel();
                }
                var result = await recognize;
                reply = new(receiver.Job!, ImageFailure.None, result);
            }
            catch (OperationCanceledException) { reply = new(receiver.Job!, ImageFailure.Cancelled, null); }
            catch (ImageJobException e) { reply = new(receiver.Job!, e.Code, null); }
            catch (Exception e) { Console.Error.WriteLine("ImageWorker OCR: " + e.GetType().Name); reply = new(receiver.Job!, ImageFailure.NativeFailure, null); }
            finally { cancellation.Cancel(); }
            byte[] response = ImageJson.Serialize(reply);
            try
            {
                if (response.Length > ImagePolicy.ResultLimit) throw new ImageJobException(ImageFailure.ImageTooLarge);
                // Output is binary JSON, bounded at 1 MiB; never escapes the inherited private channel.
                await IpcFrameCodec.WriteAsync(output, new(IpcMessageType.BinaryChunk, receiver.RequestId, response));
                await output.FlushAsync();
            }
            finally { CryptographicOperations.ZeroMemory(response); }
            // Send result before joining a possibly synchronous console read. Parent closes stdin after receiving it.
            try
            {
                var last = await disconnect;
                if (last != null)
                {
                    try { if (last.Type != IpcMessageType.ImageCancel || last.RequestId != receiver.RequestId || last.Payload.Length != 0) throw new InvalidDataException("Invalid late control."); }
                    finally { CryptographicOperations.ZeroMemory(last.Payload); }
                }
            }
            catch (OperationCanceledException) { }
            return 0;
        }
        catch (Exception e)
        { Console.Error.WriteLine("ImageWorker: " + e.GetType().Name); return 3; }
    }
}
