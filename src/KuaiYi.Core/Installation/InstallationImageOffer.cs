using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace KuaiYi.Core.Installation;

// In-memory, inherited-control-stream message only. Never put the image path in an installation record.
public sealed record InstallationImageOffer(Guid Launch,string ProofFingerprint,Guid Request,string Path)
{
    private byte[] Bytes()=>JsonSerializer.SerializeToUtf8Bytes(this);
    [JsonIgnore] public string Fingerprint=>Convert.ToHexString(SHA256.HashData(Bytes()));
    public string Encode(){Validate();return Convert.ToBase64String(Bytes());}
    public static InstallationImageOffer Decode(string encoded)
    {
        if(encoded.Length is <1 or >49152)throw new InvalidDataException("Image offer size invalid.");
        byte[] bytes=Convert.FromBase64String(encoded);
        if(encoded!=Convert.ToBase64String(bytes))throw new InvalidDataException("Noncanonical image offer encoding.");
        var offer=JsonSerializer.Deserialize<InstallationImageOffer>(bytes)??throw new InvalidDataException("Missing image offer.");
        offer.Validate();
        if(bytes.Length>32768||!bytes.AsSpan().SequenceEqual(offer.Bytes()))throw new InvalidDataException("Noncanonical image offer.");
        return offer;
    }
    private void Validate()
    {
        if(Launch==Guid.Empty||Request==Guid.Empty||ProofFingerprint is not {Length:64}||ProofFingerprint.Any(c=>!char.IsAsciiHexDigitUpper(c))||Path==null)throw new InvalidDataException("Invalid image offer identity.");
        OleImageFileDrop.ValidatePath(Path); // Syntax only; the original secure file adapter checks the opened target.
    }
    internal void Require(InstallationControlledLaunch proof)
    {Validate();if(Launch!=proof.Launch||ProofFingerprint!=proof.Fingerprint||proof.UpgradeRecord!=null)throw new InvalidDataException("Image offer differs from admitted normal session.");}
    internal string QueuedReceipt=>$"IMAGE_QUEUED|{Launch:N}|{ProofFingerprint}|{Request:N}|{Fingerprint}";
}
public interface IInstallationImageSessionProtocol:IInstallationSessionProtocol
{
    Task QueueImageAsync(System.Diagnostics.Process owned,string path,CancellationToken cancellation);
}
