using KuaiYi.Core.Settings;
using KuaiYi.Core.Installation;
internal static partial class InstallationAppControlFixture
{
    private static async Task<string> RunStartupSave(string root,ISettingsStartupRemovalParticipant startup,string behavior,CancellationToken cancellation)
    {
        string directory=Path.Combine(root,"user-data","config");
        var store=new SettingsStore(directory){WriteStageForTest=stage=>
        {
            if(behavior=="before-fail"&&stage==SettingsWriteStage.BeforePublish||behavior=="after-fail"&&stage==SettingsWriteStage.AfterPublish)
                throw new IOException("PUBLIC_STARTUP_CONFIG_WRITE_FAILURE");
        }};
        var coordinator=new SettingsCoordinator(store,new(Path.Combine(root,"user-data","secrets"),new StartupProtector(),new StartupDirectory()));
        int notices=0;if(behavior=="observer-fail")coordinator.Committed+=(_,_)=>throw new IOException("PUBLIC_STARTUP_OBSERVER_FAILURE");
        coordinator.Committed+=(_,_)=>notices++;
        using var draft=await coordinator.OpenDraftAsync();draft.Update(s=>s with{Overlay=s.Overlay with{FontSizeDip=19}});
        Exception? failure=null;string status="NotCompleted";
        var participant=behavior is "missing-notice" or "wrong-notice"?new ModifiedStartupNotice(startup,behavior):startup;
        try{status=(await coordinator.SaveWithStartupRemovalAsync(draft,participant,cancellation:cancellation)).State;}
        catch(Exception error){failure=error;}
        var saved=await store.LoadAsync();
        return $"PUBLIC_STARTUP_SAVE|{(failure==null?status:failure.GetType().Name)}|{saved.Data.Overlay.FontSizeDip}|{draft.IsClosed}|{notices}|{Directory.GetFiles(directory,"secret-txn-*.json").Length}";
    }
    private sealed class ModifiedStartupNotice(ISettingsStartupRemovalParticipant inner,string mode):ISettingsStartupRemovalParticipant
    {
        public async Task<ISettingsStartupRemoval> ReserveAsync(string basis,CancellationToken cancellation)=>new Reservation(await inner.ReserveAsync(basis,cancellation),mode);
        private sealed class Reservation(ISettingsStartupRemoval inner,string mode):ISettingsStartupRemoval
        {
            private SettingsStartupIntent? binding;
            public Guid Request=>inner.Request;public bool SaveStarted=>inner.SaveStarted;
            public void SettingsSaveStarted()=>inner.SettingsSaveStarted();
            public void BindIntent(string directory,SettingsStartupIntent intent){binding=intent;inner.BindIntent(directory,intent);}
            public Task ApplyAsync(CancellationToken cancellation)=>inner.ApplyAsync(cancellation);
            public Task CommitAsync(SettingsSnapshot snapshot)=>inner.CommitAsync(snapshot);
            public Task AbortAsync()=>inner.AbortAsync();
            public Task LocalSettledAsync(string outcome,string version,bool cleanup)
            {
                if(mode=="missing-notice")return Task.CompletedTask;
                string hash=binding!.Sha256;hash=(hash[0]=='0'?"1":"0")+hash[1..];
                var notice=new SettingsStartupLocalSettlement(Request,binding.WriteId,hash,outcome,version,cleanup);
                return InstallationControlWire.Send(Console.OpenStandardOutput(),InstallationStartupRemovalWire.Encode(control!.Proof,Request,"LOCAL_SETTLED",InstallationStartupRemovalWire.Payload(notice)),CancellationToken.None);
            }
        }
    }
    // No keys are read or staged by these synthetic cases.
    private sealed class StartupProtector:ISecretProtector
    {public byte[] Protect(ReadOnlySpan<byte> value)=>throw new InvalidOperationException("No fixture secret operation authorized.");public byte[] Unprotect(ReadOnlySpan<byte> value)=>throw new InvalidOperationException("No fixture secret operation authorized.");}
    private sealed class StartupDirectory:ISecretDirectoryPolicy
    {public void EnsurePrivate(string path)=>Directory.CreateDirectory(path);}
}
