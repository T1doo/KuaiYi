using System.Security.Cryptography;
using System.Text;
namespace KuaiYi.Core;
/// <summary>OS-owned, crash-released lease. Windows caller supplies token SID and process session, not display names.</summary>
public sealed class SessionSingleInstance:IDisposable
{
    private readonly Mutex mutex;private readonly int ownerThread;private bool owned;
    public string Scope{get;}
    public bool IsOwner=>owned;
    public SessionSingleInstance(string userAuthority,int sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userAuthority);if(sessionId<0)throw new ArgumentOutOfRangeException(nameof(sessionId));
        Scope="KuaiYi.Session."+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userAuthority+"\n"+sessionId.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        mutex=new Mutex(false,(OperatingSystem.IsWindows()?"Local\\":"")+Scope);ownerThread=Environment.CurrentManagedThreadId;
        try{owned=mutex.WaitOne(0);}catch(AbandonedMutexException){owned=true;}catch{mutex.Dispose();throw;}
    }
    public void Dispose()
    {
        if(owned){if(Environment.CurrentManagedThreadId!=ownerThread)throw new InvalidOperationException("Instance lease must close on its acquiring thread.");mutex.ReleaseMutex();owned=false;}
        mutex.Dispose();
    }
}
