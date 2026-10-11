using KuaiYi.Imaging;
using KuaiYi.Core;
using KuaiYi.Contracts;
using System.Buffers.Binary;
using System.Security.Cryptography;
// Synthetic process fixture, excluded from all staging. Never uses Windows/GDI/WGC, user images or a model.
if(args.Length==11&&args[0]=="--selection-fixture")
{
    string fault=args[1];Guid id=Guid.Parse(args[6]);long start=long.Parse(args[10]);var stream=Console.OpenStandardOutput();
    if(fault is "hang" or "cancel")await Task.Delay(Timeout.Infinite);if(fault=="late")await Task.Delay(900);if(fault=="crash")return 23;
    var meta=new SelectionCaptureMetadata(fault=="error-text"?"EmptyOrUnsupported":"Success",1,3,fault=="soft"?251:5,fault=="runtime"?[]:[12,4],[0,0,fault=="bounds"?-1:100,20],[0,0,500,200],fault=="source"?start+1:start);
    var request=fault=="identity"?Guid.NewGuid():id;
    await IpcFrameCodec.WriteAsync(stream,new(fault=="role"?IpcMessageType.ImageResult:IpcMessageType.Ping,request,System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(meta)));
    await IpcFrameCodec.WriteAsync(stream,new(IpcMessageType.BinaryChunk,request,System.Text.Encoding.UTF8.GetBytes(fault=="oversize"?new string('a',100002):"public selected\n  code")));
    if(fault=="extra")await IpcFrameCodec.WriteAsync(stream,new(IpcMessageType.Ping,id,[]));await stream.FlushAsync();return 0;
}
if(args.Length!=1||args[0]!="--private-stdio")return 2;
string mode=Environment.GetEnvironmentVariable("KUAIYI_CAPTURE_FIXTURE")??"normal";
if(mode=="exit")return 0;
var input=Console.OpenStandardInput();var output=Console.OpenStandardOutput();
if(mode.StartsWith("bad-",StringComparison.Ordinal))return await Bad(mode[4..]);
return await CaptureHostSession.RunAsync(input,output,async(job,_)=>
{
    Console.Error.WriteLine("entered:"+mode);await Console.Error.FlushAsync();
    if(mode is "stall-compositor" or "stall-native")await Task.Delay(Timeout.Infinite); // Ignores cancellation in a REAL owned process.
    if(mode=="late-frame")await Task.Delay(1200);
    if(mode=="hidden-failed")throw new CaptureException(CaptureFailure.HiddenWindowNotVerified);
    var bounds=new PixelRect(-8,0,8,8);byte[] bytes=Enumerable.Range(0,bounds.Width*bounds.Height*4).Select(i=>(byte)(i%251+1)).ToArray();
    return new ScreenshotSnapshot(new ScreenshotTopology([new("public-monitor",bounds,new KuaiYi.Core.DpiScale(1.75))]),[new ScreenshotPixels(bounds,bytes)]);
});
async Task<int> Bad(string fault)
{
    var hello=await IpcFrameCodec.ReadAsync(input)??throw new EndOfStreamException();await IpcFrameCodec.WriteAsync(output,hello);await output.FlushAsync();
    var begin=await IpcFrameCodec.ReadAsync(input)??throw new EndOfStreamException();var job=ImageJson.Read<CaptureJob>(begin.Payload);
    var bounds=fault=="oversize"?new PixelRect(0,0,16384,16384):new PixelRect(-8,0,8,8);byte[] pixels=new byte[512];
    var reply=new CaptureReply(fault=="epoch"?job with{Epoch=job.Epoch+1}:job,CaptureFailure.None,[new("public",bounds,1,checked(bounds.Width*bounds.Height*4),Convert.ToHexString(SHA256.HashData(pixels)))]);
    await IpcFrameCodec.WriteAsync(output,new(fault=="role"?IpcMessageType.ImageResult:IpcMessageType.CaptureResult,fault=="operation"?Guid.NewGuid():job.OperationId,ImageJson.Serialize(reply)));
    if(fault is "hash" or "chunk" or "commit")
    {
        byte[] chunk=new byte[520];BinaryPrimitives.WriteInt32LittleEndian(chunk.AsSpan(4),fault=="chunk"?1:0);if(fault=="hash")chunk[8]=1;
        await IpcFrameCodec.WriteAsync(output,new(IpcMessageType.BinaryChunk,job.OperationId,chunk));
        await IpcFrameCodec.WriteAsync(output,new(fault=="commit"?IpcMessageType.ImageCommit:IpcMessageType.CaptureCommit,job.OperationId,[]));
    }
    await output.FlushAsync();await Task.Delay(Timeout.Infinite);return 0;
}
