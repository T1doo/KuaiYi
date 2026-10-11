using System.Text.Json;
using KuaiYi.Contracts;
using KuaiYi.Core;

// Test-only owned process. Exercises the actual scope/wire protocol; contains no COM or model implementation.
if (args.Length != 4 || args[0] != "--fixture" || !Guid.TryParse(args[1], out Guid instance) || instance == Guid.Empty || args[2] is not ("Query" or "Drop") || args[3] is not ("Normal" or "Block" or "Throw")) return 2;
string kind = args[2], fault = args[3]; Guid operation = Guid.NewGuid();
using var input = Console.OpenStandardInput(); using var output = Console.OpenStandardOutput();
void Emit(string phase, bool cleaned = false)
{
    var item = new OleProbeEvent(instance, 2, phase, "Portable control fixture; no COM", CleanupCompleted: cleaned);
    IpcFrameCodec.WriteAsync(output, new(IpcMessageType.Ping, operation, JsonSerializer.SerializeToUtf8Bytes(item))).AsTask().GetAwaiter().GetResult(); output.Flush();
}
Emit(kind + "Begin"); var scope = new OleCleanupScope();
scope.Own(() =>
{
    Emit("CleanupBegin");
    if (fault == "Block")
    {
        var control = IpcFrameCodec.ReadAsync(input).AsTask().GetAwaiter().GetResult();
        if (control is null) throw new IOException("Owned fixture control closed during cleanup.");
        if (JsonSerializer.Deserialize<OleProbeControl>(control.Payload)?.Command != "Release") throw new InvalidDataException("Expected public release command.");
    }
    if (fault == "Throw") throw new IOException("Public cleanup failure.");
});
try
{
    scope.Run(() => { Emit("BodyFinished"); return 0; });
    Emit("CleanupComplete", scope.CleanupCompleted); Emit(kind + "End", scope.CleanupCompleted);
}
catch (OleCleanupException) { Emit("CleanupFailed"); }
// Remain alive until the parent closes controls or actually terminates this owned fixture.
while (await IpcFrameCodec.ReadAsync(input) is not null) { }
return 0;
