using System.Security.Cryptography;
namespace KuaiYi.Core.Settings;
public interface ISecretProtector{byte[] Protect(ReadOnlySpan<byte> plain);byte[] Unprotect(ReadOnlySpan<byte> encrypted);}
public interface ISecretDirectoryPolicy{void EnsurePrivate(string directory);}
public sealed class SecretLease(byte[] owned):IDisposable
{
    private byte[]? bytes=owned;
    public ReadOnlyMemory<byte> Bytes=>bytes??throw new ObjectDisposedException(nameof(SecretLease));
    public void Dispose(){byte[]? b=Interlocked.Exchange(ref bytes,null);if(b!=null)CryptographicOperations.ZeroMemory(b);}
}
/// <summary>Only encrypted immutable blobs and durable revocations. Production supplies current-user DPAPI and restrictive Windows ACLs.</summary>
public sealed class SecretFileStore(string directory,ISecretProtector protector,ISecretDirectoryPolicy policy)
{
    private readonly string root=Path.GetFullPath(directory);
    public static bool ValidReference(string? value)=>value!=null&&value.StartsWith("key/",StringComparison.Ordinal)&&value.Length==36&&value.AsSpan(4).IndexOfAnyExcept("0123456789abcdef")<0;
    private string PathFor(string reference,string suffix=".bin"){if(!ValidReference(reference))throw new SettingsValidationException("secretRef");return Path.Combine(root,reference[4..]+suffix);}
    public Task<string> SaveNewAsync(ReadOnlyMemory<byte> plain,CancellationToken cancellation=default)=>Task.Run(()=>
    {
        cancellation.ThrowIfCancellationRequested();if(plain.Length is <1 or >8192)throw new SettingsValidationException("apiKeyLength");
        policy.EnsurePrivate(root);SettingsStore.RejectLink(root);byte[] encrypted=protector.Protect(plain.Span);string reference="key/"+Guid.NewGuid().ToString("N");
        try{cancellation.ThrowIfCancellationRequested();SettingsStore.WriteFlushed(PathFor(reference),encrypted);return reference;}finally{CryptographicOperations.ZeroMemory(encrypted);}
    },cancellation);
    public Task<SecretLease> OpenAsync(string reference,CancellationToken cancellation=default)=>Task.Run(()=>
    {
        cancellation.ThrowIfCancellationRequested();string path=PathFor(reference);if(IsRevoked(reference))throw new CryptographicException("Secret reference revoked.");SettingsStore.RejectLink(path);
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(file.Length is <1 or >32768)throw new CryptographicException("Invalid encrypted secret size.");
        byte[] encrypted=new byte[(int)file.Length];file.ReadExactly(encrypted);byte[]? plain=null;
        try{plain=protector.Unprotect(encrypted);cancellation.ThrowIfCancellationRequested();if(IsRevoked(reference))throw new CryptographicException("Secret reference revoked.");var lease=new SecretLease(plain);plain=null;return lease;}finally{CryptographicOperations.ZeroMemory(encrypted);if(plain!=null)CryptographicOperations.ZeroMemory(plain);}
    },cancellation);
    public bool IsRevoked(string reference)=>File.Exists(PathFor(reference,".revoked"));
    public Task RevokeAsync(string reference)=>Task.Run(()=>{policy.EnsurePrivate(root);string path=PathFor(reference,".revoked");if(File.Exists(path))SettingsStore.RejectLink(path);else SettingsStore.WriteFlushed(path,"revoked"u8);string blob=PathFor(reference);if(File.Exists(blob)){SettingsStore.RejectLink(blob);File.Delete(blob);}});
    internal Task DeleteUnpublishedAsync(string reference)=>Task.Run(()=>{string path=PathFor(reference);if(File.Exists(path)){SettingsStore.RejectLink(path);File.Delete(path);}});
}
