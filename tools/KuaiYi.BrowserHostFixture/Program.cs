using System.Buffers.Binary;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text.Json;
using KuaiYi.Contracts;
using KuaiYi.Core;
using KuaiYi.NativeHost;
using KuaiYi.Imaging;
// Test executable only, never deployed/registered. Exact production stdio + IPC codecs, controlled desktop authority.
if(args.Length==2&&args[0]=="host-lifetime")return await BrowserHostLifetimeFixture.Run(args[1]);
if(args.SequenceEqual(new[]{"receiver-confirmation"}))return await ReceiverConfirmationFixture.Run();
if(args.Length!=1)return 2;
using var input=Console.OpenStandardInput();using var output=Console.OpenStandardOutput();
string name="KuaiYi.TestFixture."+Guid.NewGuid().ToString("N");
await using var server=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
await using var client=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
var authority=new PublicFixtureDesktop(args[0]);var serving=authority.Serve(server);
await client.ConnectAsync(1000);
int exit=await new BrowserSession(new DesktopPipeChannel(client,PublicFixtureDesktop.Origin),args[0]=="timeout"?TimeSpan.FromMilliseconds(250):null).RunAsync(input,output);
await serving.WaitAsync(TimeSpan.FromSeconds(2));
Console.Error.WriteLine(JsonSerializer.Serialize(new{fixture=true,realLocalPipe=true,authority.Offers,authority.Submitted,authority.Bytes,authority.Digest,authority.Cancelled,authority.Disposed}));return exit;
sealed class PublicFixtureDesktop(string mode)
{
    public const string Origin="chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/"; // Synthetic only; never a deployment origin.
    private readonly RuntimeGate gate=new();private BrowserGrant? current;
    public int Offers,Submitted,Bytes,Cancelled;public bool Disposed;public string? Digest;
    public async Task Serve(NamedPipeServerStream pipe)
    {
        byte[]? bytes=null;int offset=0,index=0;Guid connection=Guid.Empty;string? expectedHash=null,mime=null;uint width=0,height=0;
        try
        {
            await pipe.WaitForConnectionAsync();
            while(true)
            {
                var frame=await IpcFrameCodec.ReadAsync(pipe);if(frame==null)break;
                try
                {
                    if(connection==Guid.Empty)connection=frame.RequestId;
                    if(connection!=frame.RequestId)throw new InvalidDataException("Test IPC identity changed.");
                    if(frame.Type==IpcMessageType.BinaryChunk)
                    {
                        if(bytes==null||frame.Payload.Length<=4||BinaryPrimitives.ReadInt32LittleEndian(frame.Payload)!=index++||frame.Payload.Length-4>bytes.Length-offset)throw new InvalidDataException("Test IPC chunk invalid.");
                        frame.Payload.AsSpan(4).CopyTo(bytes.AsSpan(offset));offset+=frame.Payload.Length-4;continue;
                    }
                    if(frame.Type!=IpcMessageType.SubmitImage)throw new InvalidDataException("Test IPC role invalid.");
                    using var doc=JsonDocument.Parse(frame.Payload);var root=doc.RootElement;string? phase=root.GetProperty("phase").GetString();
                    if(phase=="OfferImage")
                    {
                        BrowserEnvelope.Fields(root,"phase","operationId","origin");if(root.GetProperty("origin").GetString()!=Origin||current!=null)throw new InvalidDataException("Test offer origin.");Offers++;
                        if(mode=="off"){await Reply(new{phase="Denied"});continue;}
                        if(!gate.TryEnableLocalOperation(true)||!gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var identity,out _))throw new InvalidDataException("Test gate failed.");
                        current=new(BrowserEnvelope.Id(root,"operationId"),identity,Guid.NewGuid());
                        await Reply(new{phase="Ready",operationId=mode=="wrong-grant"?Guid.NewGuid():current.OperationId,identity=current.Identity,nonce=current.Nonce});
                    }
                    else if(phase=="BeginImage")
                    {
                        BrowserEnvelope.Fields(root,"phase","grant","totalLength","mime","width","height","sha256");
                        if(mode=="ipc-off-at-begin")gate.Disable();
                        if(mode=="ipc-disconnect"){pipe.Dispose();break;}
                        var grant=root.GetProperty("grant").Deserialize<BrowserGrant>();
                        if(grant!=current||!gate.CanApply(grant!.Identity)){await Reply(new{phase="Stale",operationId=current!.OperationId});continue;}
                        int length=root.GetProperty("totalLength").GetInt32();if(bytes!=null||length is <=0 or >ImageTransferAssembler.ImageLimit||(mime=root.GetProperty("mime").GetString()) is not("image/png" or "image/jpeg" or "image/bmp" or "image/webp" or "image/gif"))throw new InvalidDataException("Test begin invalid.");bytes=new byte[length];expectedHash=root.GetProperty("sha256").GetString();width=root.GetProperty("width").GetUInt32();height=root.GetProperty("height").GetUInt32();
                        await Reply(new{phase="Begun",operationId=mode=="ipc-wrong-begun-id"?Guid.NewGuid():current!.OperationId});
                    }
                    else if(phase=="CommitImage")
                    {
                        BrowserEnvelope.Fields(root,"phase","grant");var grant=root.GetProperty("grant").Deserialize<BrowserGrant>();
                        if(mode=="stale-off")gate.Disable();if(mode=="stale-b")gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out _,out _);if(mode=="locked")gate.SetSessionLocked(true);
                        if(bytes==null||offset!=bytes.Length||grant!=current||Convert.ToHexString(SHA256.HashData(bytes))!=expectedHash||BrowserEncodedImagePolicy.Inspect(bytes,mime!)!=new ImageSize(width,height)||!gate.TryApply(grant!.Identity,()=>{Submitted++;Bytes=bytes.Length;Digest=Convert.ToHexString(SHA256.HashData(bytes));})){await Reply(new{phase="Stale",operationId=current!.OperationId});continue;}
                        await Reply(new{phase="Committed",operationId=mode=="ipc-wrong-commit-id"?Guid.NewGuid():current!.OperationId});CryptographicOperations.ZeroMemory(bytes);bytes=null;
                    }
                    else if(phase=="CancelImage"){BrowserEnvelope.Fields(root,"phase","grant");Cancelled++;gate.StopGeneration();current=null;if(bytes!=null){CryptographicOperations.ZeroMemory(bytes);bytes=null;}}
                    else throw new InvalidDataException("Test command invalid.");
                }
                finally{CryptographicOperations.ZeroMemory(frame.Payload);}
            }
        }
        finally{if(bytes!=null)CryptographicOperations.ZeroMemory(bytes);gate.Disable();Disposed=true;}
        async ValueTask Reply<T>(T data)
        {
            byte[] payload=JsonSerializer.SerializeToUtf8Bytes(data);
            try{await IpcFrameCodec.WriteAsync(pipe,new(IpcMessageType.SubmitImage,mode=="ipc-wrong-connection"?Guid.NewGuid():connection,payload));await pipe.FlushAsync();}
            finally{CryptographicOperations.ZeroMemory(payload);}
        }
    }
}
