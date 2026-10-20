using System.Buffers.Binary;
using System.Security.Cryptography;
using KuaiYi.Contracts;
namespace KuaiYi.Imaging;

public sealed partial class ImageWorkerClient
{
    internal Action<byte[]>? DecodeAllocationForTest{get;init;}
    internal Action? DecodeReceivedForTest{get;init;}
    public Task<DecodedRaster> DecodeAsync(ReadOnlyMemory<byte> encoded,ImageDecodeJob job,CancellationToken cancellation=default)
    {
        if(job.PrepareLongEdge!=null)throw new InvalidDataException("Raw decode requires a raw job.");
        return DecodeCoreAsync(encoded,job,cancellation,static(raster,_)=>Task.FromResult(raster));
    }
    public Task<DecodedRaster> PrepareAsync(ReadOnlyMemory<byte> encoded,ImageDecodeJob job,int maximumLongEdge=ImageTransform.LongEdgeLimit,CancellationToken cancellation=default)
    {
        if(job.PrepareLongEdge!=null)throw new InvalidDataException("Preparation requires a raw source job.");
        return DecodeCoreAsync(encoded,job with{PrepareLongEdge=maximumLongEdge},cancellation,static(raster,_)=>Task.FromResult(raster));
    }
    private Task<TResult> DecodeCoreAsync<TResult>(ReadOnlyMemory<byte> encoded,ImageDecodeJob job,CancellationToken cancellation,Func<DecodedRaster,CancellationToken,Task<TResult>> project,bool totalDeadline=false) where TResult:class,IDisposable
        =>RunOnceAsync(async()=>
        {
            using var deadline=totalDeadline?new CancellationTokenSource(TimeSpan.FromSeconds(15)):null;
            using var linked=deadline==null?null:CancellationTokenSource.CreateLinkedTokenSource(cancellation,deadline.Token);
            var token=linked?.Token??cancellation;DecodedRaster? held=null;TResult? output=null;
            try
            {
                ImageDecodePolicy.ValidateJob(job);token.ThrowIfCancellationRequested();var header=ImageDecodePolicy.Inspect(encoded.Span,token);
                if(encoded.Length!=job.ByteLength||ImageDecodePolicy.Hash(encoded.Span)!=job.Sha256)throw new InvalidDataException("Decode input mismatch.");
                GifFirstFrameInfo? expectedGif=header.Format==EncodedRasterFormat.Gif?GifDecodePolicy.Inspect(encoded.Span,token).FirstFrame:null;
                ImagePreparedInfo? expectedPrepared=null;
                if(job.PrepareLongEdge!=null)expectedPrepared=new((int)header.Size.Width,(int)header.Size.Height,EncodedImageOrientation.Read(encoded.Span,header.Format,token));
                var raster=await UseChannelAsync(async(send,receive,id,channelToken)=>
                {
                    await Write(send,new(IpcMessageType.ImageDecodeBegin,id,ImageJson.Serialize(job)),channelToken);
                    int index=0;
                    for(int offset=0;offset<encoded.Length;)
                    {
                        int count=Math.Min(IpcFrameCodec.BinaryLimit-4,encoded.Length-offset);byte[] chunk=new byte[count+4];BinaryPrimitives.WriteUInt32LittleEndian(chunk,(uint)index++);encoded.Slice(offset,count).CopyTo(chunk.AsMemory(4));
                        await Write(send,new(IpcMessageType.BinaryChunk,id,chunk),channelToken);offset+=count;
                    }
                    await Write(send,new(IpcMessageType.ImageCommit,id,[]),channelToken,true);var frame=await Read(receive,channelToken);ImageDecodeReply reply;
                    try{if(frame.Type!=IpcMessageType.ImageDecodeResult||frame.RequestId!=id)throw new InvalidDataException("Decode reply identity mismatch.");reply=ImageJson.Read<ImageDecodeReply>(frame.Payload);}
                    finally{CryptographicOperations.ZeroMemory(frame.Payload);}
                    if(reply.Job!=job)throw new InvalidDataException("Decode reply job mismatch.");ImageDecodePolicy.ValidateReply(reply);
                    if(reply.Failure==ImageFailure.None)
                    {
                        if(reply.Format!=header.Format||reply.PreparedInfo!=expectedPrepared||reply.GifInfo!=expectedGif)throw new InvalidDataException("Decode source mismatch.");
                        var expectedSize=expectedPrepared==null?header.Size:ImageTransform.PlanEncoded(header.Size,expectedPrepared.SourceOrientation,job.PrepareLongEdge!.Value).NormalizedSize;
                        if((uint)reply.Width!=expectedSize.Width||(uint)reply.Height!=expectedSize.Height)throw new InvalidDataException("Decode header mismatch.");
                    }
                    using var receiver=new ImageRasterReceiver(id,job,reply,DecodeAllocationForTest);
                    while(true)
                    {
                        frame=await Read(receive,channelToken);
                        try{if(frame.Type==IpcMessageType.ImageDecodeCommit){held=receiver.Commit(frame);break;}receiver.Chunk(frame);}
                        finally{CryptographicOperations.ZeroMemory(frame.Payload);}
                    }
                    if(held==null)throw new ImageJobException(reply.Failure);DecodeReceivedForTest?.Invoke();return held;
                },token,requireEmptyTail:true).ConfigureAwait(false);
                // UseChannel including its finally has confirmed original IO and real child exit.
                // Keep started=1 and the same raster until the original projection task settles.
                output=await project(raster,token).ConfigureAwait(false)??throw new InvalidDataException("Empty image projection.");
                if(!ReferenceEquals(output,raster)){raster.Dispose();held=null;}
                token.ThrowIfCancellationRequested();var delivery=output;output=null;
                if(ReferenceEquals(delivery,held))held=null;return delivery;
            }
            catch(OperationCanceledException)when(deadline?.IsCancellationRequested==true&&!cancellation.IsCancellationRequested)
            {throw new ImageJobException(ImageFailure.Timeout);}
            finally
            {
                if(ReferenceEquals(output,held))held=null;
                try{output?.Dispose();}finally{held?.Dispose();}
            }
        });
    private async Task RequireEmptyTail(Stream receive,CancellationToken cancellation)
    {
        byte[] tail=new byte[1];Task<int> reading=receive.ReadAsync(tail,cancellation).AsTask();Task settlement=Clear();Track(settlement);
        int count=await reading.WaitAsync(cancellation).ConfigureAwait(false);await settlement.ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
        if(count!=0)throw new InvalidDataException("Extra bytes after raster commit.");
        async Task Clear(){try{await reading.ConfigureAwait(false);}catch(Exception error){IoFailureCode=error.GetType().Name;}finally{CryptographicOperations.ZeroMemory(tail);}}
    }
}
