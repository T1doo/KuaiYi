using KuaiYi.Core.Settings;
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
        Exception? failure=null;
        try{await coordinator.SaveWithStartupRemovalAsync(draft,startup,cancellation:cancellation);}
        catch(Exception error){failure=error;}
        var saved=await store.LoadAsync();
        return $"PUBLIC_STARTUP_SAVE|{(failure==null?"PASS":failure.GetType().Name)}|{saved.Data.Overlay.FontSizeDip}|{draft.IsClosed}|{notices}|{Directory.GetFiles(directory,"secret-txn-*.json").Length}";
    }
    // No keys are read or staged by these synthetic cases.
    private sealed class StartupProtector:ISecretProtector
    {public byte[] Protect(ReadOnlySpan<byte> value)=>throw new InvalidOperationException("No fixture secret operation authorized.");public byte[] Unprotect(ReadOnlySpan<byte> value)=>throw new InvalidOperationException("No fixture secret operation authorized.");}
    private sealed class StartupDirectory:ISecretDirectoryPolicy
    {public void EnsurePrivate(string path)=>Directory.CreateDirectory(path);}
}
