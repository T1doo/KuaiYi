using System.Security.Cryptography;
using KuaiYi.Contracts;
using KuaiYi.Core;
using KuaiYi.Imaging;
namespace KuaiYi.NativeHost;

public sealed record LocalImageConfirmation(Guid ConnectionId,Guid OperationId,string DeclaredOrigin,int ClientProcessId);
public interface ILocalImageConfirmation
{
    // The production implementation is a cancellable desktop prompt, never browser-supplied consent.
    Task<bool> ConfirmAsync(LocalImageConfirmation request,CancellationToken cancellation);
}
public sealed class LocalAcceptedImage : IDisposable
{
    private byte[]? bytes;
    public RequestIdentity Identity {get;}
    public ImageSize Size {get;}
    public ReadOnlyMemory<byte> Bytes=>bytes??throw new ObjectDisposedException(nameof(LocalAcceptedImage));
    internal LocalAcceptedImage(RequestIdentity identity,ImageSize size,byte[] owned){Identity=identity;Size=size;bytes=owned;}
    public void Dispose(){var owned=Interlocked.Exchange(ref bytes,null);if(owned!=null)CryptographicOperations.ZeroMemory(owned);}
}
/// <summary>One local image in memory. No file persistence, OCR or provider/network operation.</summary>
public sealed class LocalImageInbox(RuntimeGate gate,SynchronizationContext destination):IDisposable
{
    private readonly object sync=new();
    private readonly IdentityEventReceiver<LocalAcceptedImage> receiver=new(gate,destination);
    private LocalAcceptedImage? current;
    private bool disposed;
    internal LocalAcceptedImage? Current {get{lock(sync)return current;}} // Friend tests inspect owned bytes; App consumes only through the bounded callback below.
    public RequestIdentity? CurrentIdentity {get{lock(sync)return current?.Identity;}}
    /// <summary>Destination-thread bounded synchronous use. Gate then inbox lock order matches publication;
    /// clearing cannot dispose the buffer midway through the callback.</summary>
    public bool TryUseCurrent(RequestIdentity identity,Action<LocalAcceptedImage> use)
    {
        ArgumentNullException.ThrowIfNull(use);bool used=false;
        gate.TryApply(identity,()=>
        {
            lock(sync){if(!disposed&&current?.Identity==identity){use(current);used=true;}}
        });
        return used;
    }
    internal async Task<bool> PublishAsync(RequestIdentity identity,ReceivedImage image,ImageSize size,CancellationToken cancellation,Func<bool> eligible)
    {
        var owned=new LocalAcceptedImage(identity,size,image.Bytes.ToArray());bool accepted=false,assigned=false;
        try
        {
            bool delivered=await receiver.ReceiveAsync(identity,owned,value=>
            {
                if(!eligible())return;
                lock(sync)
                {
                    if(disposed)throw new ObjectDisposedException(nameof(LocalImageInbox));
                    current?.Dispose();current=value;assigned=true;
                }
            },cancellation).ConfigureAwait(false);
            accepted=delivered&&assigned;
            return accepted;
        }
        finally{if(!accepted)owned.Dispose();}
    }
    /// <summary>One exact, destination-thread ownership transfer after the original receiving session settles.</summary>
    public LocalAcceptedImage? TakeCurrent(RequestIdentity identity)
    {
        LocalAcceptedImage? taken=null;gate.TryApply(identity,()=>{lock(sync){if(!disposed&&current?.Identity==identity){taken=current;current=null;}}});return taken;
    }
    public void Clear(RequestIdentity? identity=null)
    {
        lock(sync){if(identity==null||current?.Identity==identity){current?.Dispose();current=null;}}
    }
    public void Dispose(){lock(sync){disposed=true;current?.Dispose();current=null;}}
}
