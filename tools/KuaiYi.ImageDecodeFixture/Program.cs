using KuaiYi.Contracts;
using KuaiYi.ImageWorker;
using KuaiYi.Imaging;
namespace KuaiYi.ImageDecodeFixture;
// Public synthetic process fixture. Normal branch compiles and invokes the actual worker Program.
internal static class EntryPoint
{
    public static async Task<int> Main(string[] args)
    {
        string? fault=Environment.GetEnvironmentVariable("KUAIYI_CODEC_FIXTURE_FAULT");
        if(fault==null)return await KuaiYi.ImageWorker.Program.Main(args);
        if(!args.SequenceEqual(new[]{"--private-stdio"})||fault is not("extra-byte" or "second-commit" or "exit7" or "hold-exit" or "prepared-source" or "prepared-alpha"))return 2;
        await using var input=Console.OpenStandardInput();await using var output=Console.OpenStandardOutput();
        try
        {
            await ImageWorkerSession.RunAsync(input,output,NativeOcr.Recognize,type=>Console.Error.WriteLine(type),NativeOcr.Diagnose,Decode);
            if(fault=="extra-byte")await output.WriteAsync(new byte[]{1});
            if(fault=="second-commit")await IpcFrameCodec.WriteAsync(output,new(IpcMessageType.ImageDecodeCommit,Guid.NewGuid(),[]));
            await output.FlushAsync();if(fault=="hold-exit")await Task.Delay(Timeout.Infinite);
            return fault=="exit7"?7:0;
        }
        catch(Exception error){Console.Error.WriteLine(error.GetType().Name);return 3;}
        DecodedRaster Decode(ReadOnlyMemory<byte> encoded,ImageDecodeJob job,CancellationToken token)
        {
            if(fault is not("prepared-source" or "prepared-alpha"))return ImageRasterDecoder.Decode(encoded,job,token);
            using var real=ImageRasterDecoder.Decode(encoded,job,token);byte[] pixels=real.Bytes.ToArray();
            var metadata=real.Metadata;
            if(fault=="prepared-source")metadata=metadata with{PreparedInfo=metadata.PreparedInfo! with{SourceOrientation=ImageOrientation.ExifTopRight}};
            else{pixels[3]=0;metadata=metadata with{Sha256=ImageDecodePolicy.Hash(pixels)};}
            return new(metadata,pixels);
        }
    }
}
