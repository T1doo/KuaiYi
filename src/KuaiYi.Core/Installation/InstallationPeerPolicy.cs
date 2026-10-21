namespace KuaiYi.Core.Installation;

// Policy values only. Production obtains every field from held OS tokens, never from an IPC message.
internal sealed record InstallationPeerToken(string UserSid,string LogonSid,long AuthenticationId,int Session,int Integrity,bool Elevated,bool AppContainer,bool UiAccess);
internal static class InstallationPeerPolicy
{
    internal static void RequireSameSession(InstallationPeerToken own,InstallationPeerToken peer)
    {
        static bool Eligible(InstallationPeerToken token)=>token.UserSid.StartsWith("S-1-",StringComparison.Ordinal)&&token.LogonSid.StartsWith("S-1-5-5-",StringComparison.Ordinal)&&token.AuthenticationId!=0&&token.Session>=0&&token.Integrity==8192&&!token.Elevated&&!token.AppContainer&&!token.UiAccess;
        if(!Eligible(own)||!Eligible(peer)||own!=peer)throw new UnauthorizedAccessException("Installed image peer token refused.");
    }
}
