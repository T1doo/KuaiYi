using System.Diagnostics;
namespace KuaiYi.Core.Installation;

public sealed record InstallationStableReady(InstalledVersion Version,Guid UiLaunch,string UiProof);
/// <summary>Stable bootstrap only. The selected App remains the original UI controller and warm peer.
/// No PID adoption, restart, cold fallback, execution-marker cleanup or controller termination.</summary>
public sealed class InstallationStableLauncher:IAsyncDisposable
{
    private readonly string root,userData;
    private readonly TimeSpan budget;
    private readonly OwnedProcessSession lifetime;
    private readonly object sync=new();
    private readonly SemaphoreSlim cleanup=new(1,1);
    private readonly CancellationTokenSource stop=new();
    private readonly TaskCompletionSource<InstallationStableReady> ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static readonly HashSet<InstallationStableLauncher> Retained=[];
    private Task<int>? run;
    private Task<InstallationStableSelection>? selectionWork;
    private InstallationStableSelection? selection;
    private Task<Process>? startup;
    private Task? startupObservation,write,output,error,exit,close;
    private Process? process;
    private int revoke,closeRequested;
    private bool closingReceived,exitReadyReceived,finalized,publicationClosed,disposeRequested;
    private Task? stopCancellation;
    internal Func<Stream,Stream>? WriteStreamForTest{get;init;}
    public InstallationStableLauncher(string root,string userData,TimeSpan? budget=null,IOwnedProcessStarter? starter=null)
    {
        this.root=InstallationPaths.Root(root);this.userData=InstallationPaths.Root(userData);this.budget=budget??TimeSpan.FromSeconds(10);
        if(this.budget<=TimeSpan.Zero||this.budget>TimeSpan.FromSeconds(10))throw new ArgumentOutOfRangeException(nameof(budget));
        _=new InstallationStore(this.root,this.userData,()=>false);lifetime=new(starter);
    }
    public Task<InstallationStableReady> Ready=>ready.Task;
    /// <summary>Only this bootstrap's exact controller handle/original tasks; never proof that a failed App released its UI owner fence.</summary>
    public bool CleanupConfirmed{get{lock(sync)return finalized&&(run==null||run.IsCompleted)&&(stopCancellation==null||stopCancellation.IsCompleted);}}
    internal int? ObservedControllerExitCode{get;private set;}
    public Task<int> RunAsync(CancellationToken cancellation=default)
    {
        lock(sync){if(run!=null||disposeRequested)throw new InvalidOperationException("One stable launch per undisposed owner.");return run=Task.Run(()=>Run(cancellation));}
    }
    private async Task<int> Run(CancellationToken cancellation)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,stop.Token);
        using var registration=linked.Token.Register(Revoke);
        try
        {
            selectionWork=Task.Run(()=>InstallationStableSelection.Select(root,userData,linked.Token));selection=await selectionWork.WaitAsync(budget,linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            var info=new ProcessStartInfo(Path.Combine(selection.Version.DirectoryPath,"KuaiYi.App.exe")){WorkingDirectory=selection.Version.DirectoryPath,UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            info.ArgumentList.Add("--installation-controller");info.ArgumentList.Add(selection.Nonce.ToString("N"));
            startup=lifetime.Start(info,linked.Token);startupObservation=ObserveStartup(startup);
            process=await startup.WaitAsync(budget,linked.Token).ConfigureAwait(false);await startupObservation.ConfigureAwait(false);linked.Token.ThrowIfCancellationRequested();
            write=InstallationControlWire.Send(WriteStreamForTest?.Invoke(process.StandardInput.BaseStream)??process.StandardInput.BaseStream,"LAUNCH_CURRENT|"+selection.Encode(),linked.Token);
            lock(sync)publicationClosed=true;
            await write.WaitAsync(budget,linked.Token).ConfigureAwait(false);
            Task first=await Task.WhenAny(ready.Task,exit!,output!,error!).WaitAsync(budget,linked.Token).ConfigureAwait(false);
            if(first!=ready.Task){await first.ConfigureAwait(false);throw new IOException("Controller ended before bound READY.");}
            await ready.Task.ConfigureAwait(false);
            Task terminal=await Task.WhenAny(exit!,output!,error!).WaitAsync(linked.Token).ConfigureAwait(false);
            await terminal.ConfigureAwait(false);await exit!.WaitAsync(budget,linked.Token).ConfigureAwait(false);
            List<Exception> errors=[];
            if(!await FinalizeOriginals(errors).ConfigureAwait(false)){InstallationControllerBridge.Throw(errors);throw new IOException("Controller original work is unconfirmed.");}
            InstallationControllerBridge.Throw(errors);linked.Token.ThrowIfCancellationRequested();
            if(!closingReceived||!exitReadyReceived)throw new IOException("Controller terminal settlement is missing.");
            return 0;
        }
        catch(Exception original)
        {
            lock(sync)publicationClosed=true;
            Revoke();List<Exception> errors=[original];bool settled=await FinalizeOriginals(errors).ConfigureAwait(false);
            Exception failure=errors.Count==1?original:new AggregateException(errors);
            if(!settled){lock(Retained)Retained.Add(this);failure=new InstallationUpgradeUnconfirmedException(failure);}
            ready.TrySetException(failure);System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();throw;
        }
    }
    private async Task ObserveStartup(Task<Process> original)
    {
        Process child=await original.ConfigureAwait(false);
        lock(sync)
        {
            process=child;exit=child.WaitForExitAsync(CancellationToken.None);
            output=ReadOutput(child);error=DrainError(child);
            if(Volatile.Read(ref closeRequested)!=0)CloseInput();
        }
    }
    private void Revoke(){Interlocked.Exchange(ref revoke,1);CloseInput();}
    private void CloseInput()
    {
        Interlocked.Exchange(ref closeRequested,1);
        lock(sync)if(process!=null&&close==null)
        {
            Process original=process;
            // No write lock or flush: cancellation closes this original inherited pipe independently of backpressure.
            close=Task.Run(()=>original.StandardInput.BaseStream.Dispose());
        }
    }
    private async Task ReadOutput(Process child)
    {
        try
        {
            while(await InstallationControlWire.Read(child.StandardOutput.BaseStream,CancellationToken.None).ConfigureAwait(false) is {} row)
            {
                var selected=selection!;string suffix=$"|{selected.Nonce:N}|{selected.Fingerprint}";
                if(row.StartsWith("BOUND_READY|",StringComparison.Ordinal))
                {
                    string[] fields=row.Split('|');
                    if(fields.Length!=5||fields[1]!=selected.Nonce.ToString("N")||fields[2]!=selected.Fingerprint||!Guid.TryParseExact(fields[3],"N",out var launch)||launch==Guid.Empty||!InstallationPackage.HashValid(fields[4])||ready.Task.IsCompleted||closingReceived||Volatile.Read(ref revoke)!=0)throw new InvalidDataException("Unbound, duplicate or late controller READY.");
                    ready.TrySetResult(new(selected.Version,launch,fields[4]));continue;
                }
                if(row=="CONTROLLER_CLOSING"+suffix)
                {if(closingReceived)throw new InvalidDataException("Duplicate controller close.");closingReceived=true;CloseInput();continue;}
                if(row=="CONTROLLER_EXIT_READY"+suffix)
                {if(!closingReceived||!ready.Task.IsCompletedSuccessfully||exitReadyReceived||Volatile.Read(ref revoke)!=0)throw new InvalidDataException("Unordered controller terminal receipt.");exitReadyReceived=true;continue;}
                throw new InvalidDataException("Unexpected controller output.");
            }
            if(!closingReceived||!exitReadyReceived)throw new EndOfStreamException("Incomplete controller lifecycle.");
        }
        catch{Revoke();throw;}
    }
    private async Task DrainError(Process child)
    {
        byte[] bytes=new byte[1024];int total=0;
        try{int count;while((count=await child.StandardError.BaseStream.ReadAsync(bytes).ConfigureAwait(false))!=0){total+=count;if(total>65536)throw new IOException("Controller diagnostics exceed bound.");}}
        catch{Revoke();throw;}
        finally{System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);}
    }
    private async Task<bool> FinalizeOriginals(List<Exception> errors)
    {
        await cleanup.WaitAsync().ConfigureAwait(false);
        try
        {
            lock(sync){if(finalized)return true;if(!publicationClosed)return false;}
            Task?[] first=[selectionWork,startupObservation,stopCancellation];
            foreach(Task task in first.OfType<Task>())
            {
                try{await task.WaitAsync(budget).ConfigureAwait(false);}catch(Exception fault){if(!errors.Contains(fault))errors.Add(fault);if(!task.IsCompleted)return false;}
            }
            if(startup==null){finalized=true;return true;}
            // A failed Start has no Process, but still owns its original observer and startup task.
            if(process==null)
            {
                if(!startup.IsCompleted)return false;
                finalized=await lifetime.TryFinalizeAsync().ConfigureAwait(false);return finalized;
            }
            CloseInput();
            Task[] originals;lock(sync)originals=new[]{write,output,error,exit,close,lifetime.StartupObservation}.OfType<Task>().Distinct().ToArray();
            Task joined=Task.WhenAll(originals);
            try{await joined.WaitAsync(budget).ConfigureAwait(false);}catch(Exception fault){if(!joined.IsCompleted){errors.Add(fault);return false;}}
            foreach(Task task in originals)try{await task.ConfigureAwait(false);}catch(Exception fault){if(!errors.Contains(fault))errors.Add(fault);}
            if(!process.HasExited)return false;ObservedControllerExitCode=process.ExitCode;if(process.ExitCode!=0)errors.Add(new IOException("Controller actual exit was not zero."));
            // Stop/Finalize is forbidden while the controller is alive: it could own a UI child we cannot adopt.
            await lifetime.StopAsync().ConfigureAwait(false);
            finalized=await lifetime.TryFinalizeAsync().ConfigureAwait(false);if(finalized)lock(Retained)Retained.Remove(this);return finalized;
        }
        catch(Exception fault){if(!errors.Contains(fault))errors.Add(fault);return false;}
        finally{cleanup.Release();}
    }
    public async ValueTask DisposeAsync()
    {
        Task cancelled;lock(sync){disposeRequested=true;if(run==null)publicationClosed=true;cancelled=stopCancellation??=stop.CancelAsync();}
        Revoke();List<Exception> errors=[];try{await cancelled.WaitAsync(budget).ConfigureAwait(false);}catch(Exception fault){errors.Add(fault);}
        Task<int>? original;lock(sync)original=run;
        if(original!=null)try{await original.WaitAsync(budget).ConfigureAwait(false);}catch(Exception fault){if(!original.IsCompleted)errors.Add(fault);}
        if(!await FinalizeOriginals(errors).ConfigureAwait(false)||original is {IsCompleted:false})throw new InstallationUpgradeUnconfirmedException(errors.Count==0?new IOException("Original controller lifetime is pending."):new AggregateException(errors));
        InstallationControllerBridge.Throw(errors);
    }
}
