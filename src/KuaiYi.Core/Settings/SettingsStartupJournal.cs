using KuaiYi.Core.Installation;
namespace KuaiYi.Core.Settings;

// A v3 intent has its own original IO ledger before any create/write/flush/close. It is never adopted on restart.
internal sealed class SettingsStartupJournal
{
    private static readonly HashSet<SettingsStartupJournal> Retained=[];
    private readonly InstallationPreparationIO io;
    private readonly string path;
    private readonly byte[] bytes;
    private bool unknown,deleted;
    internal SettingsStartupJournal(string directory,string path,byte[] bytes,InstallationPreparationFaults? faults)
    {this.path=path;this.bytes=bytes.ToArray();io=InstallationPreparationIO.ReadOnly(directory,faults).CreateSessionChannel(directory,[path]);lock(Retained)Retained.Add(this);}
    internal void Write()
    {
        try{io.RequireAbsent(path);io.WritePublicationMetadata(path,bytes,16384);io.RequireExactMetadata(path,bytes,16384);}
        catch{unknown=true;throw;}
    }
    internal void RemoveAfterOriginalSettlement()
    {
        RequireSettledIntent();
        try
        {
            File.Delete(path);deleted=true;lock(Retained)Retained.Remove(this);
        }
        catch{unknown=true;throw;}
    }
    internal void RequireSettledIntent()
    {
        if(unknown||deleted||io.StorageUnconfirmed)throw new SettingsTransactionUnconfirmedException();
        try{io.RequireExactMetadata(path,bytes,16384);io.CloseOriginals();if(!io.CleanupConfirmed)throw new SettingsTransactionUnconfirmedException();}
        catch{unknown=true;throw;}
    }
}
