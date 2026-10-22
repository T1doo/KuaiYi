using System.Runtime.ExceptionServices;
using System.Text.Json;
using KuaiYi.Core.Installation;
using KuaiYi.Core.Integration;
using KuaiYi.Core.Settings;
using Ownership=KuaiYi.Core.Installation.InstallationHostOwnership;
namespace KuaiYi.NativeHost;

// Exactly two physical HKCU slots. WOW64 views of these keys are not independent slots.
internal sealed record HostRegistrationSlot(bool KeyExists,string? Value,bool ForeignContent=false);
internal interface IHostRegistrationStore
{
    HostRegistrationSlot Read(IntegrationBrowser browser);
    // Recheck expected on the opened key, then write, settle and close. This is NOT atomic CAS.
    void Write(IntegrationBrowser browser,HostRegistrationSlot expected,string? value);
    bool CleanupConfirmed {get;}
    // Retry only the retained original handle owners; never open replacement handles as proof.
    void FinalizeCleanup();
}
internal enum HostRegistrationOutcome { Registered,Withdrawn,AlreadyRegistered,AlreadyWithdrawn }
internal sealed class HostRegistrationConflictException():IOException("Observed host registration changed or is not owned; retained.");
internal sealed class HostRegistrationUnconfirmedException(Exception failure):IOException("Host registration settlement unconfirmed; journal retained and controlled admission blocked.",failure);

/// <summary>Installer primitive only: no CLI, settings or browser entry calls this class.
/// It does not prove that browser-started NativeHosts are stopped, or authorize an installation.</summary>
internal sealed class HostRegistrationTransaction(string root,string userData,IHostRegistrationStore registry)
{
    private sealed record Intent(int SchemaVersion,string Product,Guid Operation,bool Register,string Manifest,Ownership? Before,HostRegistrationSlot Chrome,HostRegistrationSlot Edge);
    private sealed record Attempt(IntegrationBrowser Browser,HostRegistrationSlot Before,string? After);
    private int busy;
    private static readonly HashSet<HostRegistrationTransaction> Held=[];
    private readonly object cleanupSync=new();
    private InstallationAdmission? retainedAdmission;
    private Task<HostRegistrationOutcome>? originalRun;
    private Task? originalCleanup;
    private readonly List<Task> cleanupAttempts=[];
    private string Receipt=>Path.Combine(root,"host-registration.json");
    private string Pending=>Path.Combine(root,"host-registration-pending.json");
    internal Task<HostRegistrationOutcome> RunAsync(bool register,CancellationToken cancellation=default)
    {
        lock(cleanupSync)
        {
            if(Interlocked.CompareExchange(ref busy,1,0)!=0)return Task.FromException<HostRegistrationOutcome>(new InstallationBusyException());
            var published=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            originalRun=RunCoreAsync(published.Task,register,cancellation);published.SetResult();return originalRun;
        }
    }
    private async Task<HostRegistrationOutcome> RunCoreAsync(Task published,bool register,CancellationToken cancellation)
    {
        await published.ConfigureAwait(false); // Publish the precise Run Task before any original work can retain resources.
        // Cancellation never abandons an original synchronous registry operation or its rollback.
        try{return await Task.Run(()=>Execute(register,cancellation),CancellationToken.None).ConfigureAwait(false);}
        finally{if(retainedAdmission==null)Volatile.Write(ref busy,0);}
    }
    // This settles only the original resources. It never erases the recovery marker,
    // retries registration, or converts the original failed operation to success.
    internal Task FinalizeRetainedCleanupAsync()
    {
        lock(cleanupSync)
        {
            if(originalRun is {IsCompleted:false})return Task.FromException(new InstallationBusyException());
            if(originalCleanup is {IsCompleted:false})return originalCleanup;
            if(retainedAdmission==null)return Task.CompletedTask;
            var owned=retainedAdmission;
            if(originalCleanup!=null)_=originalCleanup.Exception; // Observe and retain every settled failed attempt.
            originalCleanup=Task.Run(()=>
            {
                registry.FinalizeCleanup();if(!registry.CleanupConfirmed)throw new IOException("Original registry handles remain unsettled.");
                owned.Dispose();lock(cleanupSync){retainedAdmission=null;lock(Held)Held.Remove(this);}
            });
            cleanupAttempts.Add(originalCleanup);return originalCleanup;
        }
    }
    private HostRegistrationOutcome Execute(bool register,CancellationToken cancellation)
    {
        InstallationAdmission? admission=null;Action? finish=null;HostRegistrationOutcome result=default;List<Exception> errors=[];
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var store=new InstallationStore(root,userData,()=>false);
            // Never initialize an absent installation merely to register a browser host.
            _=store.ReadActive(cancellation)??throw new InstallationRecoveryException("No selected installation.");
            admission=store.EnterAdmission(mutation:true);var selected=store.ReadActive(cancellation)??throw new InstallationRecoveryException("No selected installation.");
            var payload=InstallationPayload.RequireMaterialized(selected.DirectoryPath,cancellation);
            if(payload.CatalogSha256!=selected.CatalogSha256)throw new InstallationRecoveryException("Selected installed payload digest changed.");
            var status=IntegrationInstalledFiles.ReadAsync(selected.DirectoryPath,cancellation).GetAwaiter().GetResult();
            if(status!=(IntegrationState.Configured,IntegrationReason.LocalConfigurationOnly))throw new InstallationRecoveryException("Installed host metadata or payload is unconfirmed.");
            string manifest=Path.Combine(selected.DirectoryPath,"com.kuaiyi.native_host.json");
            Ownership? ownership=null;byte[]? receiptBytes=null;
            if(Exists(Receipt))
            {
                receiptBytes=InstallationPackage.ReadDocument(Receipt,8192);ownership=Decode<Ownership>(receiptBytes);
                if(ownership.SchemaVersion!=1||ownership.Product!="KuaiYi"||ownership.Owner==Guid.Empty||ownership.Version!=selected||ownership.Manifest!=manifest)throw new InstallationRecoveryException("Unknown or old registration ownership retained.");
            }
            var chrome=registry.Read(IntegrationBrowser.Chrome);var edge=registry.Read(IntegrationBrowser.Edge);
            RequireInitial(chrome,ownership,manifest);RequireInitial(edge,ownership,manifest);
            cancellation.ThrowIfCancellationRequested();
            if(register&&ownership!=null)result=HostRegistrationOutcome.AlreadyRegistered;
            else if(!register&&ownership==null)result=HostRegistrationOutcome.AlreadyWithdrawn;
            else result=Apply(register,selected,manifest,ownership,receiptBytes,chrome,edge,admission,store,cancellation,ref finish);
        }
        catch(Exception error)
        {
            errors.Add(error);
            if(!registry.CleanupConfirmed&&admission!=null)Retain(admission,errors);
        }
        finally
        {
            if(admission!=null&&retainedAdmission==null)
            {
                try{admission.Dispose();}catch(Exception error){errors.Add(error);Retain(admission,errors);}
                // The persistent pending gate prevents new cooperative admission after lease close,
                // until the precise original operation has completed all resource settlement.
                if(retainedAdmission==null&&finish!=null)try{finish();}catch(Exception error){errors.Add(new HostRegistrationUnconfirmedException(error));}
            }
        }
        Throw(errors);return result;
    }
    private void Retain(InstallationAdmission admission,List<Exception> errors)
    {
        retainedAdmission=admission;lock(Held)Held.Add(this);
        try
        {
            if(!Exists(Pending))SettingsStore.WriteFlushed(Pending,JsonSerializer.SerializeToUtf8Bytes(new{SchemaVersion=1,Product="KuaiYi",Operation=Guid.NewGuid(),Phase="OriginalCleanupUnconfirmed"}));
        }
        catch(Exception error){errors.Add(error);}
        Exception failure=errors.Count==1?errors[0]:new AggregateException(errors);errors.Clear();errors.Add(new HostRegistrationUnconfirmedException(failure));
    }
    private HostRegistrationOutcome Apply(bool register,InstalledVersion selected,string manifest,Ownership? before,byte[]? receiptBytes,HostRegistrationSlot chrome,HostRegistrationSlot edge,InstallationAdmission admission,InstallationStore store,CancellationToken cancellation,ref Action? finish)
    {
        var intent=new Intent(1,"KuaiYi",Guid.NewGuid(),register,manifest,before,chrome,edge);byte[] journal=JsonSerializer.SerializeToUtf8Bytes(intent);
        byte[]? nextReceipt=register?JsonSerializer.SerializeToUtf8Bytes(new Ownership(1,"KuaiYi",intent.Operation,selected,manifest)):null;
        if(journal.Length>16384||nextReceipt?.Length>8192)throw new InvalidDataException("Registration metadata exceeds its fixed bound.");
        RequireAbsent(Pending);CheckScope();SettingsStore.WriteFlushed(Pending,journal);
        List<Attempt> attempts=[];bool committed=false;
        try
        {
            foreach(var browser in Enum.GetValues<IntegrationBrowser>())
            {
                cancellation.ThrowIfCancellationRequested();CheckOwnership(cancellation);
                var original=browser==IntegrationBrowser.Chrome?chrome:edge;
                if(registry.Read(browser)!=original)throw new HostRegistrationConflictException();
                string? next=register?manifest:null;attempts.Add(new(browser,original,next));
                registry.Write(browser,original,next);RequireDefault(registry.Read(browser),next);
            }
            cancellation.ThrowIfCancellationRequested();CheckScope();Exact(Pending,journal);
            foreach(var browser in Enum.GetValues<IntegrationBrowser>())RequireDefault(registry.Read(browser),register?manifest:null);
            if(register)
            {
                RequireAbsent(Receipt);SettingsStore.WriteFlushed(Receipt,nextReceipt!);
            }
            else{Exact(Receipt,receiptBytes!);Archive(Receipt,"host-registration-withdrawn-"+intent.Operation.ToString("N")+".json");}
            // Durable ownership creation/archive is the commit point. No post-commit compensation.
            committed=true;finish=()=>FinishJournal("finished");
            return register?HostRegistrationOutcome.Registered:HostRegistrationOutcome.Withdrawn;
        }
        catch(Exception operation)
        {
            if(committed||!registry.CleanupConfirmed)throw new HostRegistrationUnconfirmedException(operation);
            List<Exception> rollback=[];
            foreach(var attempt in attempts.AsEnumerable().Reverse())
            {
                try
                {
                    CheckOwnership(CancellationToken.None);var now=registry.Read(attempt.Browser);
                    // A created empty key is retained; only the default value is in our scope.
                    if(MatchesDefault(now,attempt.Before.Value))continue;
                    RequireDefault(now,attempt.After);registry.Write(attempt.Browser,now,attempt.Before.Value);RequireDefault(registry.Read(attempt.Browser),attempt.Before.Value);
                }
                catch(Exception error){rollback.Add(error);if(!registry.CleanupConfirmed)break;}
            }
            try
            {
                if(receiptBytes==null)RequireAbsent(Receipt);else Exact(Receipt,receiptBytes);
                admission.Require(root);if(store.ReadActive(CancellationToken.None)!=selected)throw new InstallationRecoveryException("Selected host generation changed during compensation.");
                Exact(Pending,journal);if(rollback.Count==0)finish=()=>FinishJournal("failed");
            }
            catch(Exception error){rollback.Add(error);}
            if(rollback.Count>0)throw new HostRegistrationUnconfirmedException(new AggregateException(new[]{operation}.Concat(rollback)));
            ExceptionDispatchInfo.Capture(operation).Throw();throw;
        }
        void CheckScope()
        {admission.Require(root);if(store.ReadActive(cancellation)!=selected)throw new InstallationRecoveryException("Selected host generation changed.");}
        void CheckOwnership(CancellationToken token)
        {
            Exact(Pending,journal);admission.Require(root);
            if(receiptBytes==null)RequireAbsent(Receipt);else Exact(Receipt,receiptBytes);
            if(store.ReadActive(token)!=selected)throw new InstallationRecoveryException("Selected host generation changed before registration write.");
        }
        void FinishJournal(string outcome)
        {
            Exact(Pending,journal);
            if(store.ReadActive(CancellationToken.None)!=selected)throw new InstallationRecoveryException("Selected generation changed before journal settlement.");
            if(committed&&register)Exact(Receipt,nextReceipt!);
            else if(committed||receiptBytes==null)RequireAbsent(Receipt);
            else Exact(Receipt,receiptBytes);
            Archive(Pending,"host-registration-"+outcome+"-"+intent.Operation.ToString("N")+".json");
        }
    }
    private static void RequireInitial(HostRegistrationSlot slot,Ownership? ownership,string manifest)
    {RequireDefault(slot,ownership==null?null:manifest);}
    private static bool MatchesDefault(HostRegistrationSlot slot,string? expected)=>!slot.ForeignContent&&slot.Value==expected&&(slot.KeyExists||expected==null);
    private static void RequireDefault(HostRegistrationSlot slot,string? expected){if(!MatchesDefault(slot,expected))throw new HostRegistrationConflictException();}
    private static T Decode<T>(byte[] bytes)where T:class
    {
        var value=JsonSerializer.Deserialize<T>(bytes)??throw new InstallationRecoveryException("Missing registration ownership.");
        if(!bytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(value)))throw new InstallationRecoveryException("Unknown or noncanonical registration ownership retained.");return value;
    }
    private static bool Exists(string path){InstallationPaths.Check(path);return File.Exists(path)||Directory.Exists(path);}
    private static void RequireAbsent(string path){if(Exists(path))throw new InstallationRecoveryException("Existing registration record retained.");}
    private static void Exact(string path,byte[] bytes)
    {if(!InstallationPackage.ReadDocument(path,16384).AsSpan().SequenceEqual(bytes))throw new InstallationRecoveryException("Registration record changed; retained.");}
    private void Archive(string path,string name)
    {string destination=Path.Combine(root,name);RequireAbsent(destination);File.Move(path,destination);}
    internal static void Throw(List<Exception> errors)
    {if(errors.Count==1)ExceptionDispatchInfo.Capture(errors[0]).Throw();if(errors.Count>1)throw new AggregateException(errors);}
}
