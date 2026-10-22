namespace KuaiYi.Core.Installation;

internal sealed partial class InstallationDeploymentScope
{
    private InstallationDeploymentPublication? publication;
    private Task? publicationTask;
    private bool publicationAbort,commitTerminal;
    internal Task<InstallationDeploymentOutcome> CommitPreparedAsync(InstallationDeploymentPrepared value,InstallationPublicationFaults? publicationFaults=null,InstallationPreparationFaults? publicationIOFaults=null)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock(sync)
        {
            if(publication!=null||!value.IsLiveFor(this)||preparedContext==null||generationIO==null||snapshotOwner is not IInstallationDeploymentPublicationRegistry registry)throw new InvalidOperationException("Commit requires the original live Prepared and original registry owner, once.");
            publication=new(this,preparedContext,value,registry,publicationFaults);
            // Actual target and every allowed retained empty-key/owner state, before any filesystem mutation.
            foreach(var retained in publication.TerminalRetentions())foreach(string variant in new[]{"CommitCandidate","AbortAfterPrepared"})
                if(TerminalBytes(generationRetention,bootstrapRetention,value.Sha256,retained,variant).Length>16384)throw new InvalidDataException("Publication terminal variant exceeds its bound.");
            publication.Attach(generationIO.CreatePublicationChannel(root,publication.WritePaths(),publicationIOFaults));
            var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var owned=publication;
            publicationTask=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);try{await owned.ForwardAsync().ConfigureAwait(false);}finally{RequestFinish(false);}});
            begin.SetResult();return finalization;
        }
    }
    internal void RelocatePublicationBundle(InstallationDeploymentPublication original,InstallationPreparationIO io,string from,string to)
    {if(!ReferenceEquals(original,publication)||bootstrapIO==null||qualifiedPrepared==null||from!=qualifiedPrepared.Plan.Bootstrap.InactiveDirectory||to!=qualifiedPrepared.Plan.Bootstrap.CanonicalDirectory)throw new InstallationRecoveryException("Wrong original B metadata relocation.");io.RelocateRecordedMetadata(bootstrapIO,from,to);}
    internal void CheckPublicationCancellation()
    {lock(sync){if(publicationAbort||cancellation.IsCancellationRequested)throw new OperationCanceledException("Original publication was canceled before Closing.",cancellation);}}
    internal void RequirePublication(InstallationDeploymentPublication original,bool mutation)
    {
        lock(sync)if(!ReferenceEquals(original,publication)||qualifiedPrepared==null||preparedContext==null||mutation&&closing||files.Count!=0)throw new InstallationRecoveryException("Wrong or closed original publication owner.");
        if(mutation)admission.RequireMutation(root);
        RequireExact(pending,ticket);RequireExact(beforePath,before!,131072);RequireExact(preparedContext.Path,System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(qualifiedPrepared.Plan),131072);
    }
    private async Task<InstallationDeploymentOutcome> FinalizePublicationAsync(List<Exception> errors)
    {
        var original=publication!;bool canArchive=true;
        try{await publicationTask!.ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        try{if(faults?.BeforeClosing!=null)await faults.BeforeClosing().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        bool commit=errors.Count==0&&original.TargetComplete;
        if(commit)try{await original.VerifyFinalAsync(true).ConfigureAwait(false);}catch(Exception error){errors.Add(error);commit=false;}
        // Last pre-Closing cancellation/Abort arbitration. After this linearization no new rollback starts.
        lock(sync)
        {
            if(cancellation.IsCancellationRequested||publicationAbort){errors.Add(new OperationCanceledException(cancellation));commit=false;}
            if(commit)closing=true;
        }
        if(!commit)
        {
            try{await original.RevertAsync().ConfigureAwait(false);await original.VerifyFinalAsync(false).ConfigureAwait(false);}catch(Exception error){errors.Add(error);canArchive=false;}
            lock(sync)closing=true;
        }
        commitTerminal=commit;
        try{if(faults?.AfterClosing!=null)await faults.AfterClosing().ConfigureAwait(false);}catch(Exception error){errors.Add(error);canArchive=false;}
        try{generationIO!.CloseOriginals();if(!generationIO.CleanupConfirmed)canArchive=false;}catch(Exception error){errors.Add(error);canArchive=false;}
        // Admission and original registry failures are independent; neither masks the other.
        try{admission.Dispose();}catch(Exception error){errors.Add(error);canArchive=false;}
        if(!admission.CleanupConfirmed)canArchive=false;
        foreach(var owner in reads)
        {
            if(canArchive)try{await owner.VerifyAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);canArchive=false;}
            try{await owner.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);canArchive=false;}
            if(!owner.CleanupConfirmed)canArchive=false;
        }
        try{await snapshotOwner!.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);canArchive=false;}
        if(!snapshotOwner!.CleanupConfirmed)canArchive=false;
        (string Path,FileStream Original)[] held;lock(sync)held=files.ToArray();
        foreach(var file in held)try{Close(file.Path,file.Original);}catch(Exception error){errors.Add(error);canArchive=false;}
        lock(sync)if(files.Count!=0)canArchive=false;
        canArchive&=original.CanFinalize;
        if(canArchive)
        {
            string destination=commit?Path.Combine(root,"deployment-committed-"+Operation+".json"):archive;byte[]? terminal=null;bool moveAttempted=false;
            try
            {
                // These new post-Closing reads are owned and joined by this original finalization Task.
                await original.VerifyFinalAsync(commit).ConfigureAwait(false);generationIO!.VerifyRecordedMetadata();
                if(!generationIO.CleanupConfirmed||generationIO.StorageUnconfirmed)throw new InstallationRecoveryException("Original publication IO is unconfirmed.");
                terminal=TerminalBytes(generationRetention);if(terminal.Length>16384)throw new InvalidDataException("Terminal exceeds its bound.");
                Write(temporary,terminal,false);RequireExact(pending,ticket);RequireExact(temporary,terminal);
                if(SnapshotFilePresent(backup)||SnapshotFilePresent(destination))throw new InstallationRecoveryException("Foreign terminal destination retained.");
                File.Replace(temporary,pending,backup,false);RequireExact(pending,terminal);RequireExact(backup,ticket);
                moveAttempted=true;if(faults?.Archive!=null)faults.Archive(pending,destination);else File.Move(pending,destination);
                // No fallible cleanup or filesystem access after this successful Move.
                lock(Retained)Retained.Remove(this);return new(commit?InstallationDeploymentStatus.Committed:InstallationDeploymentStatus.AbortedAfterPrepared,errors.ToArray());
            }
            catch(Exception error)
            {
                errors.Add(error);
                if(commit&&moveAttempted&&terminal!=null)
                {
                    try
                    {
                        if(!SnapshotFilePresent(pending))
                        {
                            RequireExact(destination,terminal);lock(sync)if(files.Count!=0)throw new IOException("Original historical receipt read is unsettled.");
                            if(!admission.CleanupConfirmed||!snapshotOwner.CleanupConfirmed||!generationIO!.CleanupConfirmed)throw new IOException("Original historical settlement is unconfirmed.");
                            lock(Retained)Retained.Remove(this);return new(InstallationDeploymentStatus.CommittedReceiptUncertain,errors.ToArray());
                        }
                    }
                    catch(Exception history){errors.Add(history);}
                }
            }
        }
        return new(InstallationDeploymentStatus.Unconfirmed,errors.ToArray());
    }
}
