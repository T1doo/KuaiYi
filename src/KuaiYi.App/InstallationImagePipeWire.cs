using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using KuaiYi.Contracts;
using KuaiYi.Core.Installation;
namespace KuaiYi.App;
internal sealed record InstallationImageHello(string ClientNonce);
internal sealed record InstallationImageChallenge(Guid Hello,string Grant);
internal sealed record InstallationImageSubmit(string Grant,string Path);
internal static class InstallationImagePipeWire
{
    private static readonly JsonSerializerOptions Options=new(){MaxDepth=8,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    internal static T Decode<T>(IpcFrame frame)
    {
        byte[]? canonical=null;
        try
        {
            var value=JsonSerializer.Deserialize<T>(frame.Payload,Options)??throw new InvalidDataException("Missing installed image frame.");canonical=JsonSerializer.SerializeToUtf8Bytes(value,Options);
            if(!frame.Payload.AsSpan().SequenceEqual(canonical))throw new InvalidDataException("Noncanonical installed image frame.");Validate(value);return value;
        }
        finally{CryptographicOperations.ZeroMemory(frame.Payload);if(canonical!=null)CryptographicOperations.ZeroMemory(canonical);}
    }
    internal static async Task WriteAsync<T>(Stream pipe,IpcMessageType type,Guid request,T body,CancellationToken cancellation)
    {
        Validate(body);byte[] payload=JsonSerializer.SerializeToUtf8Bytes(body,Options);
        try{await IpcFrameCodec.WriteAsync(pipe,new(type,request,payload),cancellation).ConfigureAwait(false);await pipe.FlushAsync(cancellation).ConfigureAwait(false);}
        finally{CryptographicOperations.ZeroMemory(payload);}
    }
    internal static async Task<IpcFrame> ReadAsync(Stream pipe,IpcMessageType expected,CancellationToken token)
        =>await IpcFrameCodec.ReadRestrictedAsync(pipe,expected,token).ConfigureAwait(false)??throw new EndOfStreamException("Installed image peer disconnected.");
    private static void Validate<T>(T value)
    {
        static bool Encoded(string? text)=>text is {Length:>0 and <=49152};
        switch(value)
        {
            case InstallationImageHello hello when InstallationWarmIdentity.Nonce(hello.ClientNonce):return;
            case InstallationImageChallenge challenge when challenge.Hello!=Guid.Empty&&Encoded(challenge.Grant):return;
            case InstallationImageSubmit submit when Encoded(submit.Grant)&&submit.Path!=null:KuaiYi.Core.OleImageFileDrop.ValidatePath(submit.Path);return;
            case string encoded when Encoded(encoded):return;
            default:throw new InvalidDataException("Invalid installed image fields.");
        }
    }
}
