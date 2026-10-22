using System.Text.Json;
namespace KuaiYi.Core.Installation;

internal interface IInstallationDeploymentPublicationRegistry:IInstallationDeploymentPreparedRead
{
    Task<InstallationDeploymentRegistrationSnapshot> ReadPublishedAsync();
    Task WriteDefaultAsync(bool chrome,InstallationDeploymentRegistrationSlot expected,string? value);
}
internal enum DeploymentPublicationStage { ContainerCreated,OwnerWritten,BundleMoved,Current,Active,Chrome,Edge,HostReceipt,Verified,BeforeReverse,AfterReverse }
internal sealed class InstallationPublicationFaults
{
    internal Action<DeploymentPublicationStage>? Stage {get;init;}
    internal Action<string,string,bool>? Publish {get;init;}
    internal Action<string,string>? MoveBundle {get;init;}
    internal Action<int,bool>? BeforeMutation {get;init;}
}
internal sealed record InstallationPublicationRetention(string? Owner,string CanonicalBundle,bool CanonicalReady,InstallationDeploymentRegistrationSnapshot Registry);

// Same-scope provisional publication. No independent admission, installer entry or process launch.
internal sealed class InstallationDeploymentPublication
{
    private readonly InstallationDeploymentScope scope;
    private readonly PreparedPreparationContext context;
    private readonly InstallationDeploymentPrepared prepared;
    private readonly IInstallationDeploymentPublicationRegistry registry;
    private readonly InstallationPublicationFaults? faults;
    private readonly string[] paths;
    private readonly byte[]?[] before,after;
    private readonly Dictionary<string,byte[]> remnants=[];
    private InstallationPreparationIO io=null!;
    private int prefix,attempt;
    private bool canonicalReady,canonicalAttempt,ownerWritten,unknown;
    private InstallationDeploymentRegistrationSnapshot slots;
    internal InstallationPublicationRetention Retention=>new(ownerWritten?prepared.Plan.Bootstrap.Owner:context.Before.BootstrapOwner,prepared.Plan.Bootstrap.CanonicalDirectory,canonicalReady,slots);
    internal IEnumerable<InstallationPublicationRetention> TerminalRetentions()
    {
        var old=context.Before.Registry;var target=prepared.Plan.Host.Targets;
        InstallationDeploymentRegistrationSlot Empty(InstallationDeploymentRegistrationSlot value)=>value.KeyExists?value:new(true,null);
        foreach(var chrome in new[]{old.Chrome,Empty(old.Chrome),target.Chrome})foreach(var edge in new[]{old.Edge,Empty(old.Edge),target.Edge})
        foreach(var owner in new[]{context.Before.BootstrapOwner,prepared.Plan.Bootstrap.Owner})foreach(bool ready in new[]{false,true})
            yield return new(owner,prepared.Plan.Bootstrap.CanonicalDirectory,ready,new(chrome,edge));
    }
    internal bool CanFinalize=>!unknown&&io.CleanupConfirmed&&!io.StorageUnconfirmed&&registry.CleanupConfirmed;
    internal bool TargetComplete=>prefix==5&&attempt==0;
    internal InstallationDeploymentPublication(InstallationDeploymentScope scope,PreparedPreparationContext context,InstallationDeploymentPrepared prepared,IInstallationDeploymentPublicationRegistry registry,InstallationPublicationFaults? faults)
    {
        this.scope=scope;this.context=context;this.prepared=prepared;this.registry=registry;this.faults=faults;slots=context.Before.Registry;
        paths=[Path.Combine(context.Root,"current.json"),InstallationBootstrapDeployment.ActivePath(context.Root),Path.Combine(context.Root,"host-registration.json")];
        before=[Decode(context.Before.Current),Decode(context.Before.BootstrapActive),Decode(context.Before.HostReceipt)];after=[Decode(prepared.Plan.TargetCurrent),Decode(prepared.Plan.Bootstrap.Receipt),Decode(prepared.Plan.Host.Receipt)];
    }
    internal IEnumerable<string> WritePaths()=>paths.Concat([InstallationBootstrapDeployment.OwnerPath(context.Root)]).Concat(Enumerable.Range(1,5).Where(n=>n is 1 or 2 or 5).SelectMany(n=>new[]{Temp(n,false),Temp(n,true),Removed(n)}));
    internal void Attach(InstallationPreparationIO original){if(io!=null)throw new InvalidOperationException();io=original;}
    private string Temp(int step,bool reverse)=>Path.Combine(context.Root,$"deployment-publish-{context.Operation}-{step}-{(reverse?"reverse":"forward")}.tmp");
    private string Removed(int step)=>Path.Combine(context.Root,$"deployment-publish-{context.Operation}-{step}-removed.json");
    private static byte[]? Decode(string? value)=>value==null?null:Convert.FromBase64String(value);
    private int Index(int step)=>step switch{1=>0,2=>1,5=>2,_=>throw new InvalidOperationException()};
    private void Journal(bool mutation)
    {
        if(unknown||io.StorageUnconfirmed||!io.CleanupConfirmed||!registry.CleanupConfirmed)throw new InstallationRecoveryException("Original publication resources or storage remain unconfirmed.");
        try{scope.RequirePublication(this,mutation);}catch{unknown=true;throw;}
    }
    private void Optional(string path,byte[]? bytes,int maximum)
    {if(bytes==null)io.RequireAbsent(path);else io.RequireExactMetadata(path,bytes,maximum);}
    private void Candidates()
    {
        var p=prepared.Plan;
        var(current,old)=InstallationStore.PlanCurrent(context.Root,Decode(context.Before.Current),p.Generation,CancellationToken.None,io);
        if(!current.AsSpan().SequenceEqual(Decode(p.TargetCurrent)))throw new InstallationRecoveryException("Original current plan differs.");
        if(context.Before.BootstrapActive!=null)
        {
            var owner=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.StoreOwner>(Decode(context.Before.BootstrapOwner)??throw new InstallationRecoveryException("Original B owner missing."));
            var previous=InstallationBootstrapDeployment.RequireReceipt(context.Root,context.Data,owner,Decode(context.Before.BootstrapActive)!,CancellationToken.None,io);
            if(previous.Generation!=old)throw new InstallationRecoveryException("Original B restore target differs from original current.");
        }
        var g=InstallationPackage.ReadInstalled(p.Generation.DirectoryPath,CancellationToken.None,expectedDigest:p.Generation.CatalogSha256,io:io);
        if(g.Schema!=3||g.SourceCatalogSha256!=p.GenerationSourceSha256)throw new InstallationRecoveryException("Published G qualification changed.");
        string bundle=canonicalReady?p.Bootstrap.CanonicalDirectory:p.Bootstrap.InactiveDirectory;
        if(InstallationBootstrapPackage.Read(bundle,p.BootstrapSourceSha256,p.Generation,CancellationToken.None,io).Revision!=p.BootstrapRevision)throw new InstallationRecoveryException("Published B qualification changed.");
    }
    private async Task<InstallationDeploymentRegistrationSnapshot> ReadSlots()
    {
        var errors=new List<Exception>();InstallationDeploymentRegistrationSnapshot? value=null;
        try{value=await registry.ReadPublishedAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        try{await registry.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        if(!registry.CleanupConfirmed)errors.Add(new IOException("Original publication registry resources are unsettled."));InstallationErrors.Throw(errors);return value!;
    }
    private static bool MatchesSlot(InstallationDeploymentRegistrationSlot actual,InstallationDeploymentRegistrationSlot expected,bool allowEmpty)
        =>actual==expected||allowEmpty&&!expected.KeyExists&&actual==new InstallationDeploymentRegistrationSlot(true,null);
    private InstallationDeploymentRegistrationSnapshot ExpectedSlots(int count)=>new(count>=3?prepared.Plan.Host.Targets.Chrome:context.Before.Registry.Chrome,count>=4?prepared.Plan.Host.Targets.Edge:context.Before.Registry.Edge);
    private async Task CheckPrefix(bool mutation)
    {
        try
        {
        Journal(mutation);Candidates();
        Optional(InstallationBootstrapDeployment.OwnerPath(context.Root),ownerWritten?Decode(prepared.Plan.Bootstrap.Owner):Decode(context.Before.BootstrapOwner),4096);
        foreach(int step in new[]{1,2,5})Optional(paths[Index(step)],prefix>=step?after[Index(step)]:before[Index(step)],step==1?4096:8192);
        var actual=await ReadSlots().ConfigureAwait(false);var expected=ExpectedSlots(prefix);
        if(!MatchesSlot(actual.Chrome,expected.Chrome,prefix<3&&chromeAttempted)||!MatchesSlot(actual.Edge,expected.Edge,prefix<4&&edgeAttempted))throw new InstallationRecoveryException("Registry publication prefix changed or is foreign.");slots=actual;
        foreach(var row in remnants)io.RequireExactMetadata(row.Key,row.Value,8192);
        if(canonicalReady){io.RequireDirectory(InstallationBootstrapDeployment.BundlesPath(context.Root));io.RequireDirectory(prepared.Plan.Bootstrap.CanonicalDirectory);io.RequireAbsent(prepared.Plan.Bootstrap.InactiveDirectory);}
        Journal(mutation);
        }
        catch{unknown=true;throw;}
    }
    private bool chromeAttempted,edgeAttempted;
    private void Canonical()
    {
        var p=prepared.Plan.Bootstrap;Journal(true);scope.CheckPublicationCancellation();canonicalAttempt=true;
        try
        {
            string container=InstallationBootstrapDeployment.BundlesPath(context.Root);
            if(context.Before.BootstrapOwner==null)
            {
                io.RequireAbsent(container);io.RequireAbsent(InstallationBootstrapDeployment.OwnerPath(context.Root));Directory.CreateDirectory(container);io.RequireDirectory(container);if(io.Entries(container).Any())throw new InstallationRecoveryException("New canonical container is not empty.");faults?.Stage?.Invoke(DeploymentPublicationStage.ContainerCreated);
                io.WritePublicationMetadata(InstallationBootstrapDeployment.OwnerPath(context.Root),Decode(p.Owner)!,4096);io.RequireExactMetadata(InstallationBootstrapDeployment.OwnerPath(context.Root),Decode(p.Owner)!,4096);ownerWritten=true;faults?.Stage?.Invoke(DeploymentPublicationStage.OwnerWritten);
            }
            else{io.RequireDirectory(container);ownerWritten=true;}
            io.RequireAbsent(p.CanonicalDirectory);Journal(true);
            if(faults?.MoveBundle!=null)faults.MoveBundle(p.InactiveDirectory,p.CanonicalDirectory);else Directory.Move(p.InactiveDirectory,p.CanonicalDirectory);
            scope.RelocatePublicationBundle(this,io,p.InactiveDirectory,p.CanonicalDirectory);canonicalReady=true;Candidates();faults?.Stage?.Invoke(DeploymentPublicationStage.BundleMoved);
        }
        catch{unknown=true;io.MarkUnconfirmed();throw;}
    }
    internal async Task ForwardAsync()
    {
        await CheckPrefix(true).ConfigureAwait(false);Canonical();await CheckPrefix(true).ConfigureAwait(false);
        for(int step=1;step<=5;step++)
        {
            scope.CheckPublicationCancellation();await CheckPrefix(true).ConfigureAwait(false);attempt=step;faults?.BeforeMutation?.Invoke(step,false); // Intent precedes any actual mutation.
            if(step is 3 or 4)
            {
                bool chrome=step==3;if(chrome)chromeAttempted=true;else edgeAttempted=true;
                var expected=chrome?slots.Chrome:slots.Edge;await registry.WriteDefaultAsync(chrome,expected,(chrome?prepared.Plan.Host.Targets.Chrome:prepared.Plan.Host.Targets.Edge).Value).ConfigureAwait(false);
            }
            else WriteRecord(step,false);
            await ResolveAttempt(false).ConfigureAwait(false);if(prefix!=step)throw new IOException("Publication did not reach its exact target.");
            faults?.Stage?.Invoke((DeploymentPublicationStage)((int)DeploymentPublicationStage.Current+step-1));
        }
        await CheckPrefix(true).ConfigureAwait(false);scope.CheckPublicationCancellation();faults?.Stage?.Invoke(DeploymentPublicationStage.Verified);
    }
    private void WriteRecord(int step,bool reverse)
    {
        int index=Index(step);byte[]? next=reverse?before[index]:after[index],expected=reverse?after[index]:before[index];string path=paths[index];
        Optional(path,expected,step==1?4096:8192);
        string temporary=next==null?Removed(step):Temp(step,reverse);io.RequireAbsent(temporary);
        if(next!=null){io.WritePublicationMetadata(temporary,next,step==1?4096:8192);io.RequireExactMetadata(temporary,next,step==1?4096:8192);remnants.Add(temporary,next);}
        Journal(true);Optional(path,expected,step==1?4096:8192);
        if(faults?.Publish!=null)faults.Publish(temporary,path,next!=null&&expected!=null);
        else if(next==null)File.Move(path,temporary);else if(expected==null)File.Move(temporary,path);else File.Replace(temporary,path,null,false);
        if(next==null)remnants.Add(temporary,expected!);else remnants.Remove(temporary);
    }
    private InstallationRecoveryException Conflict(string message){unknown=true;return new(message);}
    private async Task ResolveAttempt(bool reverse)
    {
        if(attempt==0)return;Journal(true);int step=attempt;bool isAfter;
        if(step is 3 or 4)
        {
            var actual=await ReadSlots().ConfigureAwait(false);var slot=step==3?actual.Chrome:actual.Edge;var old=step==3?context.Before.Registry.Chrome:context.Before.Registry.Edge;var target=step==3?prepared.Plan.Host.Targets.Chrome:prepared.Plan.Host.Targets.Edge;
            if(slot==target)isAfter=true;else if(MatchesSlot(slot,old,true))isAfter=false;else throw Conflict("Attempted registry write is foreign.");slots=actual;
        }
        else
        {
            int index=Index(step);byte[]? actual=null;try{actual=io.ReadDocument(paths[index],step==1?4096:8192);}catch(FileNotFoundException){io.RequireAbsent(paths[index]);}
            bool Equal(byte[]? value)=>actual==null?value==null:value!=null&&actual.AsSpan().SequenceEqual(value);
            if(Equal(after[index]))isAfter=true;else if(Equal(before[index]))isAfter=false;else throw Conflict("Attempted record write is foreign or partial.");
            // A move/replace may have performed its side effect before reporting an error.
            string temp=Temp(step,reverse);if(isAfter!=reverse)remnants.Remove(temp);
            if(reverse&&before[index]==null&&!isAfter)remnants[Removed(step)]=after[index]!;
        }
        prefix=isAfter?step:step-1;attempt=0;await CheckPrefix(true).ConfigureAwait(false);
    }
    internal async Task RevertAsync()
    {
        if(canonicalAttempt&&!canonicalReady){unknown=true;throw new InstallationRecoveryException("Canonical B ownership is unconfirmed; dependencies retained.");}
        try
        {
            await ResolveAttempt(false).ConfigureAwait(false);await CheckPrefix(true).ConfigureAwait(false);
            while(prefix>0)
            {
                int step=prefix;Journal(true);faults?.Stage?.Invoke(DeploymentPublicationStage.BeforeReverse);await CheckPrefix(true).ConfigureAwait(false);attempt=step;faults?.BeforeMutation?.Invoke(step,true);
                if(step is 3 or 4){bool chrome=step==3;await registry.WriteDefaultAsync(chrome,chrome?slots.Chrome:slots.Edge,(chrome?context.Before.Registry.Chrome:context.Before.Registry.Edge).Value).ConfigureAwait(false);}
                else WriteRecord(step,true);
                await ResolveAttempt(true).ConfigureAwait(false);if(prefix!=step-1)throw new IOException("Reverse publication did not reach its exact before state.");faults?.Stage?.Invoke(DeploymentPublicationStage.AfterReverse);
            }
            await CheckPrefix(true).ConfigureAwait(false);
        }
        catch{unknown=true;throw;}
    }
    internal async Task VerifyFinalAsync(bool commit)
    {
        if(!CanFinalize||attempt!=0||prefix!=(commit?5:0))throw new InstallationRecoveryException("Publication has no known complete terminal state.");
        await CheckPrefix(false).ConfigureAwait(false);
    }
}
