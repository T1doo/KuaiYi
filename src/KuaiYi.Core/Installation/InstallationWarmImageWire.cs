using System.Security.Cryptography;
using System.Text.Json;
namespace KuaiYi.Core.Installation;

// Correlation only. Neither these fields nor their echo establish a Windows peer's authority.
internal sealed record InstallationWarmIdentity(Guid Launch,string Proof,Guid Connection,Guid Request,long Ordinal,string ClientNonce,string ServerNonce)
{
    internal void Require(InstallationControlledLaunch proof)
    {
        if(Launch!=proof.Launch||Proof!=proof.Fingerprint||proof.UpgradeRecord!=null||Connection==Guid.Empty||Request==Guid.Empty||Ordinal<=0||!Nonce(ClientNonce)||!Nonce(ServerNonce))throw new InvalidDataException("Invalid warm correlation.");
    }
    internal static bool Nonce(string? value)=>value is {Length:64}&&value.All(char.IsAsciiHexDigitUpper);
    internal static string NewNonce()=>Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
internal sealed record InstallationWarmGrant(InstallationWarmIdentity Identity,string Nonce,long Epoch);
internal sealed record InstallationWarmSubmit(InstallationWarmGrant Grant,string Path);
internal enum InstallationWarmOutcome { Rejected,PreviewReady }
internal sealed record InstallationWarmResult(InstallationWarmIdentity Identity,InstallationWarmOutcome Outcome,Guid Source,long Version);
internal sealed record InstallationWarmAccepted(Guid Source,long Version);
internal static class InstallationWarmImageWire
{
    private static readonly JsonSerializerOptions Options=new(){MaxDepth=8,UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow};
    internal static string Encode<T>(T value)
    {
        Validate(value);byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value,Options);
        if(bytes.Length>32768)throw new InvalidDataException("Warm payload too large.");
        return Convert.ToBase64String(bytes);
    }
    internal static T Decode<T>(string encoded)
    {
        if(encoded.Length is <1 or >49152)throw new InvalidDataException("Warm payload size invalid.");
        byte[] bytes=Convert.FromBase64String(encoded);
        if(encoded!=Convert.ToBase64String(bytes)||bytes.Length>32768)throw new InvalidDataException("Noncanonical warm payload.");
        var value=JsonSerializer.Deserialize<T>(bytes,Options)??throw new InvalidDataException("Missing warm payload.");
        if(!bytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(value,Options)))throw new InvalidDataException("Noncanonical warm fields.");
        Validate(value);return value;
    }
    private static void Validate<T>(T value)
    {
        switch(value)
        {
            case InstallationWarmIdentity i:
                if(i.Launch==Guid.Empty||i.Connection==Guid.Empty||i.Request==Guid.Empty||i.Ordinal<=0||!InstallationWarmIdentity.Nonce(i.Proof)||!InstallationWarmIdentity.Nonce(i.ClientNonce)||!InstallationWarmIdentity.Nonce(i.ServerNonce))throw new InvalidDataException("Invalid warm identity fields.");break;
            case InstallationWarmGrant g:
                if(g.Identity==null||!InstallationWarmIdentity.Nonce(g.Nonce)||g.Epoch<0)throw new InvalidDataException("Invalid warm grant fields.");Validate(g.Identity);break;
            case InstallationWarmSubmit s:
                if(s.Grant==null||s.Path==null)throw new InvalidDataException("Missing warm submit fields.");Validate(s.Grant);OleImageFileDrop.ValidatePath(s.Path);break;
            case InstallationWarmResult r:
                if(r.Identity==null||!Enum.IsDefined(r.Outcome)||r.Outcome==InstallationWarmOutcome.PreviewReady&&(r.Source==Guid.Empty||r.Version<=0)||r.Outcome==InstallationWarmOutcome.Rejected&&(r.Source!=Guid.Empty||r.Version!=0))throw new InvalidDataException("Invalid warm result fields.");Validate(r.Identity);break;
            default:throw new InvalidDataException("Unsupported warm record.");
        }
    }
}
