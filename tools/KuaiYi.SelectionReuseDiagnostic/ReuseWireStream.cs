using System.Security.Cryptography;
using KuaiYi.SelectionReusePrototype;
namespace KuaiYi.SelectionReuseDiagnostic;

public static class ReuseWireStream
{
    // Caller owns the only fixed-size buffer. It must not erase while an unfinished read can mutate it.
    public static async Task<bool> FillAsync(Stream stream,byte[] bytes,CancellationToken cancellation)
    {
        if(bytes.Length!=ReuseFrameCodec.Size)throw new ArgumentException("Fixed buffer required.");
        int offset=0;
        while(offset<bytes.Length)
        {
            int count=await stream.ReadAsync(bytes.AsMemory(offset),cancellation).ConfigureAwait(false);
            if(count==0){if(offset==0)return false;throw new InvalidDataException("Truncated frame.");}
            offset+=count;
        }
        return true;
    }
    public static async Task WriteAsync(Stream stream,ReuseFrame frame,CancellationToken cancellation,bool fragmented=false)
    {
        byte[] bytes=ReuseFrameCodec.Encode(frame);
        try
        {
            if(fragmented){for(int i=0;i<bytes.Length;i++)await stream.WriteAsync(bytes.AsMemory(i,1),cancellation).ConfigureAwait(false);}
            else await stream.WriteAsync(bytes,cancellation).ConfigureAwait(false);
            await stream.FlushAsync(cancellation).ConfigureAwait(false);
        }
        finally{CryptographicOperations.ZeroMemory(bytes);}
    }
}
