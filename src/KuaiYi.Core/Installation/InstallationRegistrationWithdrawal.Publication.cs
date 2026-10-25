namespace KuaiYi.Core.Installation;
internal sealed partial class InstallationRegistrationWithdrawal
{
    private static int[] FileSteps=>[1,3,6,7];
    private string RecordPath(int step)=>step switch{1=>InstallationStartupRegistration.OwnerPath(root),3=>Path.Combine(root,"host-registration.json"),6=>InstallationBootstrapDeployment.ActivePath(root),7=>Path.Combine(root,"current.json"),_=>throw new InvalidOperationException()};
    private string Tombstone(string op,int step)=>Path.Combine(root,$"withdrawal-removed-{op}-{step}.json");
    private IEnumerable<string> Tombstones(string op)=>FileSteps.Select(step=>Tombstone(op,step));
    private string[] WritePaths(string op)=>FileSteps.Select(RecordPath).Concat(Tombstones(op)).ToArray();
    private byte[]? RecordBefore(int step)=>Decode(step switch{1=>frozen!.Before.StartupOwner,3=>frozen!.Before.HostReceipt,6=>frozen!.Before.BootstrapActive,7=>frozen!.Before.Current,_=>throw new InvalidOperationException()});
    private static int Limit(int step)=>step==7?4096:8192;
    private void RequireJournal(bool mutation)
    {
        if(!started||OriginalWork.IsCompleted||qualified==null||!ReferenceEquals(qualified.Owner,this)||unknown||io.StorageUnconfirmed||!io.CleanupConfirmed||registry.ConflictObserved||!registry.CleanupConfirmed||mutation&&closing)
            throw new InstallationRecoveryException("Original withdrawal capability is foreign, closed or unsettled.");
        if(mutation)admission.RequireMutation(root);
        io.RequireExactMetadata(pending,ticket!,16384);io.RequireExactMetadata(beforePath,beforeBytes!,131072);io.RequireExactMetadata(planPath,qualified.Bytes,131072);
    }
    private void RequireFrozen()
    {
        io.RequireExactMetadata(Path.Combine(root,"owner.txt"),InstallationStore.InitialOwnerBytes,4096);
        io.RequireExactMetadata(InstallationBootstrapDeployment.OwnerPath(root),Decode(frozen!.Before.BootstrapOwner)!,4096);
        var check=Qualify(frozen.Before);if(check!=frozen)throw new InstallationRecoveryException("Frozen pinned installation closure changed.");
    }
    private static InstallationDeploymentRegistrationSlot Empty(InstallationDeploymentRegistrationSlot before)=>new(before.KeyExists,null);
    private InstallationDeploymentRegistrationSnapshot ExpectedSlots(int count)
    {
        var old=frozen!.Before.Registry;return new(count>=5?Empty(old.Chrome):old.Chrome,count>=4?Empty(old.Edge):old.Edge,count>=2?new(false):old.Startup);
    }
    private async Task CheckPrefixAsync(bool mutation)
    {
        try
        {
            RequireJournal(mutation);RequireFrozen();
            foreach(int step in FileSteps)
            {
                byte[]? bytes=RecordBefore(step);string path=RecordPath(step),retained=Tombstone(Operation,step);
                if(prefix>=step){io.RequireAbsent(path);if(bytes==null)io.RequireAbsent(retained);else io.RequireExactMetadata(retained,bytes,Limit(step));}
                else{if(bytes==null)io.RequireAbsent(path);else io.RequireExactMetadata(path,bytes,Limit(step));io.RequireAbsent(retained);}
            }
            if(await ReadSlots().ConfigureAwait(false)!=ExpectedSlots(prefix))throw new InstallationRecoveryException("Withdrawal registry prefix is foreign.");
            RequireJournal(mutation);
        }
        catch{unknown=true;throw;}
    }
    private Task OwnStep(int step,bool reverse)
    {
        var start=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var original=Task.Run(async()=>
        {
            await start.Task.ConfigureAwait(false);RequireJournal(true);
            if(step==2)
            {
                var before=frozen!.Before.Registry.Startup!;var after=new InstallationStartupValue(false);
                if(before!=after)await registry.WriteStartupAsync(reverse?after:before,reverse?before:after).ConfigureAwait(false);
            }
            else if(step is 4 or 5)
            {
                bool chrome=step==5;var before=chrome?frozen!.Before.Registry.Chrome:frozen!.Before.Registry.Edge;var after=Empty(before);
                if(before!=after)await registry.WriteDefaultAsync(chrome,reverse?after:before,reverse?before.Value:null).ConfigureAwait(false);
            }
            else
            {
                byte[]? bytes=RecordBefore(step);
                if(bytes!=null)
                {
                    string canonical=RecordPath(step),retained=Tombstone(Operation,step);string from=reverse?retained:canonical,to=reverse?canonical:retained;
                    // Only these pre-sealed paths can move; both byte/absence expectations precede the call.
                    if(effects==null||!qualified!.Plan.WritePaths.Contains(from,StringComparer.Ordinal)||!qualified.Plan.WritePaths.Contains(to,StringComparer.Ordinal))throw new InstallationRecoveryException("Record move is outside original withdrawal paths.");
                    io.RequireExactMetadata(from,bytes,Limit(step));io.RequireAbsent(to);InstallationPaths.Check(from);InstallationPaths.Check(to);
                    try{if(faults?.Move!=null)faults.Move(from,to);else effects.MovePublicationRecord(from,to);}
                    catch{unknown=true;effects.MarkUnconfirmed();throw;}
                }
            }
            // This hook denotes a known effect; actual Move failures above stay unknown in the original ledger.
            faults?.AfterEffect?.Invoke(step,reverse);
        });actions.Add(original);start.SetResult();return original;
    }
    private async Task ResolveAttemptAsync(bool reverse)
    {
        if(attempt==0)return;
        try
        {
            RequireJournal(true);int step=attempt;bool after;
            if(step is 2 or 4 or 5)
            {
                var actual=await ReadSlots().ConfigureAwait(false);var before=ExpectedSlots(step-1);var target=ExpectedSlots(step);
                bool isBefore=step==2?actual.Startup==before.Startup:step==4?actual.Edge==before.Edge:actual.Chrome==before.Chrome;
                bool isAfter=step==2?actual.Startup==target.Startup:step==4?actual.Edge==target.Edge:actual.Chrome==target.Chrome;
                if(isBefore&&isAfter)after=!reverse;else if(isAfter)after=true;else if(isBefore)after=false;else throw new InstallationRecoveryException("Attempted named registration is foreign.");
            }
            else
            {
                byte[]? bytes=RecordBefore(step),canonical=Optional(RecordPath(step),Limit(step)),retained=Optional(Tombstone(Operation,step),Limit(step));
                bool Equal(byte[]? a,byte[]? b)=>a==null?b==null:b!=null&&a.AsSpan().SequenceEqual(b);
                bool isBefore=Equal(canonical,bytes)&&retained==null,isAfter=canonical==null&&Equal(retained,bytes);
                if(isBefore&&isAfter)after=!reverse;else if(isAfter)after=true;else if(isBefore)after=false;else throw new InstallationRecoveryException("Attempted original record move is foreign or partial.");
            }
            prefix=after?step:step-1;attempt=0;await CheckPrefixAsync(true).ConfigureAwait(false);
        }
        catch{unknown=true;throw;}
    }
}
