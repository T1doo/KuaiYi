using KuaiYi.Imaging;
using KuaiYi.Core;
using KuaiYi.Contracts;
using System.Buffers.Binary;
using System.Security.Cryptography;
// Synthetic process fixture, excluded from all staging. Never uses Windows/GDI/WGC, user images or a model.
if(args.Length==7&&args[0]=="--clipboard-fixture")
{
    string fault=args[1];Guid id=Guid.Parse(args[6]);long started=long.Parse(args[4]);using var clipboardOutput=Console.OpenStandardOutput();using var clipboardInput=Console.OpenStandardInput();
    if(fault=="hang-before-ready")await Task.Delay(Timeout.Infinite);
    if(fault=="crash")return 23;
    var identity=fault=="identity"?Guid.NewGuid():id;bool ready=fault!="unsupported";
    await IpcFrameCodec.WriteAsync(clipboardOutput,new(fault=="role"?IpcMessageType.ImageResult:IpcMessageType.Ping,identity,System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new ClipboardStageMetadata(ready?"Ready":"Finished",10,fault=="source"?started+1:started))));
    ClipboardStartMetadata? command=null;
    if(ready){var frame=await IpcFrameCodec.ReadAsync(clipboardInput);if(frame==null)return 0;command=System.Text.Json.JsonSerializer.Deserialize<ClipboardStartMetadata>(frame.Payload);CryptographicOperations.ZeroMemory(frame.Payload);}
    if(fault=="hang-after-sent")await Task.Delay(Timeout.Infinite);
    if(fault=="late")await Task.Delay(1300);
    if(fault=="wait-cancel"){await clipboardInput.CopyToAsync(Stream.Null);return 0;}
    string status=fault=="unsupported"?"Unsupported":command?.Status=="Sent"?"Success":"Denied";
    if(fault=="failed-text")status="Denied";
    var meta=new ClipboardResultMetadata(status,"Restored",fault=="expired"?350:5);
    await IpcFrameCodec.WriteAsync(clipboardOutput,new(IpcMessageType.Ping,identity,System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(meta)));
    byte[] text=fault=="invalid"?[0xff]:status=="Success"||fault=="failed-text"?"public clipboard"u8.ToArray():[];
    await IpcFrameCodec.WriteAsync(clipboardOutput,new(IpcMessageType.BinaryChunk,identity,text));CryptographicOperations.ZeroMemory(text);
    if(fault=="extra")await IpcFrameCodec.WriteAsync(clipboardOutput,new(IpcMessageType.BinaryChunk,id,"public extra"u8.ToArray()));return 0;
}
if(args.Length==11&&args[0]=="--selection-fixture")
{
    string fault=args[1];Guid id=Guid.Parse(args[6]);long start=long.Parse(args[10]);var stream=Console.OpenStandardOutput();
    bool timing=fault.StartsWith("phase-",StringComparison.Ordinal);if(timing)fault=fault[6..];
    if(fault is "hang" or "cancel")await Task.Delay(Timeout.Infinite);if(fault=="late")await Task.Delay(900);if(fault=="crash")return 23;
    var meta=new SelectionCaptureMetadata(fault=="error-text"?"EmptyOrUnsupported":"Success",1,3,fault=="soft"?251:5,fault=="runtime"?[]:[12,4],[0,0,fault=="bounds"?-1:100,20],[0,0,500,200],fault=="source"?start+1:start);
    if(fault=="broker-failure")meta=new("BrokerFailure",0,0,5,Diagnostic:new(SelectionBrokerStage.MetadataSerialize,SelectionBrokerException.Argument,unchecked((int)0x80070057)));
    if(fault.StartsWith("copy-",StringComparison.Ordinal))meta=new(fault=="copy-status"?"Unknown":"EmptyOrUnsupported",0,3,5,ProcessStartUtcTicks:start,Copy:new(fault=="copy-focus"?0:234,fault=="copy-runtime"?[]:[12,4],fault=="copy-control"?[0,0,-1,20]:[0,0,500,200],fault=="copy-association"?[1000,1000,100,20]:[0,0,100,20],true));
    var request=fault=="identity"?Guid.NewGuid():id;
    if(timing)meta=meta with{PhaseTimings=[0,200,200,210,210,240,240,245,245,251,251,252,252,253,253,254,254,255]};
    await IpcFrameCodec.WriteAsync(stream,new(fault=="role"?IpcMessageType.ImageResult:IpcMessageType.Ping,request,System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(meta)));
    await IpcFrameCodec.WriteAsync(stream,new(IpcMessageType.BinaryChunk,request,System.Text.Encoding.UTF8.GetBytes(fault.StartsWith("copy-",StringComparison.Ordinal)||fault=="broker-failure"?"":fault=="oversize"?new string('a',100002):"public selected\n  code")));
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
