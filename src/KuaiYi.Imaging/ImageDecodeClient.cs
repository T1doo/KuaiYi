using System.Buffers.Binary;
using System.Security.Cryptography;
using KuaiYi.Contracts;
namespace KuaiYi.Imaging;

public sealed partial class ImageWorkerClient
{
    internal Action<byte[]>? DecodeAllocationForTest{get;init;}
    internal Action? DecodeReceivedForTest{get;init;}
    public async Task<DecodedRaster> DecodeAsync(ReadOnlyMemory<byte> encoded,ImageDecodeJob job,CancellationToken cancellation=default)
    {
        DecodedRaster? held=null;
        try
        {
            var result=await RunOnceAsync(async()=>
            {
                ImageDecodePolicy.ValidateJob(job);cancellation.ThrowIfCancellationRequested();var header=ImageDecodePolicy.Inspect(encoded.Span);
                if(encoded.Length!=job.ByteLength||ImageDecodePolicy.Hash(encoded.Span)!=job.Sha256)throw new InvalidDataException("Decode input mismatch.");
                return await UseChannelAsync(async(send,receive,id,token)=>
                {
                    await Write(send,new(IpcMessageType.ImageDecodeBegin,id,ImageJson.Serialize(job)),token);
                    int index=0;
                    for(int offset=0;offset<encoded.Length;)
                    {
                        int count=Math.Min(IpcFrameCodec.BinaryLimit-4,encoded.Length-offset);byte[] chunk=new byte[count+4];BinaryPrimitives.WriteUInt32LittleEndian(chunk,(uint)index++);encoded.Slice(offset,count).CopyTo(chunk.AsMemory(4));
                        await Write(send,new(IpcMessageType.BinaryChunk,id,chunk),token);offset+=count;
                    }
                    await Write(send,new(IpcMessageType.ImageCommit,id,[]),token,true);var frame=await Read(receive,token);ImageDecodeReply reply;
                    try{if(frame.Type!=IpcMessageType.ImageDecodeResult||frame.RequestId!=id)throw new InvalidDataException("Decode reply identity mismatch.");reply=ImageJson.Read<ImageDecodeReply>(frame.Payload);}
                    finally{CryptographicOperations.ZeroMemory(frame.Payload);}
                    if(reply.Job!=job)throw new InvalidDataException("Decode reply job mismatch.");ImageDecodePolicy.ValidateReply(reply);
                    if(reply.Failure==ImageFailure.None&&(reply.Format!=header.Format||(uint)reply.Width!=header.Size.Width||(uint)reply.Height!=header.Size.Height))throw new InvalidDataException("Decode header mismatch.");
                    using var receiver=new ImageRasterReceiver(id,job,reply,DecodeAllocationForTest);
                    while(true)
                    {
                        frame=await Read(receive,token);
                        try{if(frame.Type==IpcMessageType.ImageDecodeCommit){held=receiver.Commit(frame);break;}receiver.Chunk(frame);}
                        finally{CryptographicOperations.ZeroMemory(frame.Payload);}
                    }
                    if(held==null)throw new ImageJobException(reply.Failure);DecodeReceivedForTest?.Invoke();return held;
                },cancellation,requireEmptyTail:true).ConfigureAwait(false);
            }).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();held=null;return result;
        }
        finally{held?.Dispose();}
    }
    private async Task RequireEmptyTail(Stream receive,CancellationToken cancellation)
    {
        byte[] tail=new byte[1];Task<int> reading=receive.ReadAsync(tail,cancellation).AsTask();Task settlement=Clear();Track(settlement);
        int count=await reading.WaitAsync(cancellation).ConfigureAwait(false);await settlement.ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
        if(count!=0)throw new InvalidDataException("Extra bytes after raster commit.");
        async Task Clear(){try{await reading.ConfigureAwait(false);}catch(Exception error){IoFailureCode=error.GetType().Name;}finally{CryptographicOperations.ZeroMemory(tail);}}
    }
}
