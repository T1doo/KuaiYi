using System.Text.Json;
using KuaiYi.Core.Settings;
namespace KuaiYi.Core.Installation;

internal enum BootstrapStage { JournalPublished,StoreOwned,BundleCopied,BundlePublished,ReceiptPublished,BeforeSettlement }
internal enum BootstrapOutcome { Deployed,Withdrawn,AlreadyWithdrawn }
internal sealed record BootstrapBinding(string Owner,string Active);
internal sealed class BootstrapUnconfirmedException(Exception error):IOException("Bootstrap settlement unconfirmed; owned files and pending admission retained.",error);
// Internal installer primitive only. No registry, shortcut, fixed locator, system installation or process manipulation.
internal sealed class InstallationBootstrapDeployment(string root,string userData)
{
    internal const int OwnerLimit=4096,ReceiptLimit=8192,JournalLimit=16384;
    internal static byte[] EncodeOwner(StoreOwner owner)=>Bounded(owner,OwnerLimit);
    internal static byte[] EncodeReceipt(Receipt receipt)=>Bounded(receipt,ReceiptLimit);
    private static byte[] Bounded<T>(T value,int limit){byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value);if(bytes.Length>limit)throw new InvalidDataException("Bootstrap metadata exceeds consumer bound.");return bytes;}
    internal static void RequireTransport(string root,string data,InstalledVersion generation,byte[] current,byte[] owner,byte[] active)
        =>_=new InstallationStableSelection(Guid.NewGuid(),root,data,generation,Convert.ToBase64String(current),new(Convert.ToBase64String(owner),Convert.ToBase64String(active))).Encode();
    internal sealed record StoreOwner(int SchemaVersion,string Product,Guid Owner,string Root,string UserData);
    internal sealed record Receipt(int SchemaVersion,string Product,string Mode,Guid Owner,Guid Bundle,string CatalogSha256,string Revision,InstalledVersion Generation);
    private sealed record Intent(int SchemaVersion,string Product,Guid Operation,bool Deploy,StoreOwner Owner,string Current,string? Before,Guid Bundle,string? CatalogSha256);
    internal Action<BootstrapStage>? StageForTest{get;init;}
    private int busy;private InstallationAdmission? retained;private Task<BootstrapOutcome>? original;
    private readonly object cleanupSync=new();private Task? cleanupTask;private readonly List<Task> cleanupAttempts=[];
    private static readonly HashSet<InstallationBootstrapDeployment> Held=[];
    internal static string ActivePath(string root)=>Path.Combine(root,"stable-bootstrap.json");
    internal static string PendingPath(string root)=>Path.Combine(root,"bootstrap-deployment-pending.json");
    internal static string OwnerPath(string root)=>Path.Combine(root,"bootstrap-store.json");
    internal static string BundlesPath(string root)=>Path.Combine(root,"bootstrap-bundles");
    internal static string BundlePath(string root,Guid bundle)=>Path.Combine(BundlesPath(root),bundle.ToString("N"));
    internal Task<BootstrapOutcome> DeployAsync(string source,string digest,CancellationToken cancellation=default)=>Run(source,digest,cancellation);
    internal Task<BootstrapOutcome> WithdrawAsync(CancellationToken cancellation=default)=>Run(null,null,cancellation);
    private Task<BootstrapOutcome> Run(string? source,string? digest,CancellationToken cancellation)
    {
        if(Interlocked.CompareExchange(ref busy,1,0)!=0)return Task.FromException<BootstrapOutcome>(new InstallationBusyException());
        // Publish the original owner before synchronous work can acquire a file lease; cancellation never drops that work.
        var start=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        original=Task.Run(async()=>{await start.Task.ConfigureAwait(false);try{return Execute(source,digest,cancellation);}finally{if(retained==null)Volatile.Write(ref busy,0);}});start.SetResult();return original;
    }
    internal Task FinalizeOriginalCleanupAsync()
    {
        lock(cleanupSync)
        {
            if(original is {IsCompleted:false})return Task.FromException(new InstallationBusyException());
            if(cleanupTask is {IsCompleted:false})return cleanupTask;
            if(retained==null)return Task.CompletedTask;
            var held=retained;if(cleanupTask!=null)_ =cleanupTask.Exception;
            cleanupTask=Task.Run(()=>{held.Dispose();lock(cleanupSync)retained=null;lock(Held)Held.Remove(this);});cleanupAttempts.Add(cleanupTask);return cleanupTask;
            // The persistent journal stays blocked; this never retries or declares the failed transaction successful.
        }
    }
    private BootstrapOutcome Execute(string? source,string? digest,CancellationToken cancellation)
    {
        InstallationAdmission? admission=null;Action? finish=null;List<Exception> errors=[];BootstrapOutcome outcome=default;
        try
        {
            if(root!=InstallationPaths.Root(root)||userData!=InstallationPaths.Root(userData))throw new InvalidDataException("Canonical bootstrap roots required.");
            var store=new InstallationStore(root,userData,()=>false);_=store.ReadActive(cancellation)??throw new InstallationRecoveryException("No selected generation.");
            admission=store.EnterAdmission(mutation:true,existing:true);var generation=store.ReadActive(cancellation)??throw new InstallationRecoveryException("No selected generation.");
            if(InstallationPayload.RequireMaterialized(generation.DirectoryPath,cancellation).CatalogSha256!=generation.CatalogSha256)throw new InstallationRecoveryException("Generation qualification differs.");
            byte[] current=store.SnapshotRecord(admission)!;byte[]? ownerBefore=ReadOptional(OwnerPath(root),OwnerLimit);byte[]? before=ReadOptional(ActivePath(root));StoreOwner owner;
            if(ownerBefore!=null){owner=Decode<StoreOwner>(ownerBefore);RequireOwner(owner,root,userData);if(!Directory.Exists(BundlesPath(root)))throw new InstallationRecoveryException("Owned bootstrap directory missing.");}
            else{if(Exists(BundlesPath(root))||before!=null)throw new InstallationRecoveryException("Unknown bootstrap files retained.");owner=new(1,"KuaiYi",Guid.NewGuid(),root,userData);}
            Receipt? previous=before==null?null:RequireReceipt(root,userData,owner,before,cancellation);
            if(source==null&&before==null){outcome=BootstrapOutcome.AlreadyWithdrawn;}
            else
            {
            InstallationPackage? package=null;string? revision=null;
            if(source!=null)
            {
                source=InstallationPaths.Root(source);if(InstallationPaths.Contains(root,source)||InstallationPaths.Contains(source,root)||InstallationPaths.Contains(userData,source)||InstallationPaths.Contains(source,userData))throw new InvalidDataException("Bootstrap source must be separate.");
                (package,revision)=InstallationBootstrapPackage.Read(source,digest!,generation,cancellation);
            }
            cancellation.ThrowIfCancellationRequested();Guid operation=Guid.NewGuid(),bundle=Guid.NewGuid();byte[] ownerBytes=EncodeOwner(owner);
            byte[]? next=package==null?null:EncodeReceipt(new(1,"KuaiYi",InstallationBootstrapPackage.Mode,owner.Owner,bundle,package.CatalogSha256,revision!,generation));
            if(next!=null)RequireTransport(root,userData,generation,current,ownerBytes,next);
            var intent=new Intent(1,"KuaiYi",operation,source!=null,owner,Convert.ToBase64String(current),before==null?null:Convert.ToBase64String(before),bundle,package?.CatalogSha256);byte[] journal=JsonSerializer.SerializeToUtf8Bytes(intent);
            if(journal.Length>JournalLimit)throw new InvalidDataException("Bootstrap journal exceeds bound.");
            CheckCurrent();RequireOptional(OwnerPath(root),ownerBefore,OwnerLimit);RequireOptional(ActivePath(root),before);Absent(PendingPath(root));SettingsStore.WriteFlushed(PendingPath(root),journal);
            bool changed=false,owned=ownerBefore!=null;
            try
            {
                StageForTest?.Invoke(BootstrapStage.JournalPublished);cancellation.ThrowIfCancellationRequested();Check();
                if(!owned)
                {
                    Absent(BundlesPath(root));Directory.CreateDirectory(BundlesPath(root));SettingsStore.WriteFlushed(OwnerPath(root),ownerBytes);owned=true;
                }
                StageForTest?.Invoke(BootstrapStage.StoreOwned);Check();
                if(package!=null)
                {
                    string staging=Path.Combine(BundlesPath(root),"staging-"+operation.ToString("N")),destination=BundlePath(root,bundle);Absent(staging);Absent(destination);Directory.CreateDirectory(staging);
                    package.CopyTo(source!,staging,cancellation);StageForTest?.Invoke(BootstrapStage.BundleCopied);
                    _=InstallationBootstrapPackage.Read(staging,package.CatalogSha256,generation,cancellation);Check();cancellation.ThrowIfCancellationRequested();Directory.Move(staging,destination);StageForTest?.Invoke(BootstrapStage.BundlePublished);
                    _=RequireReceipt(root,userData,owner,next!,cancellation);
                }
                Check();cancellation.ThrowIfCancellationRequested();Publish(next,before,operation);changed=true;StageForTest?.Invoke(BootstrapStage.ReceiptPublished);cancellation.ThrowIfCancellationRequested();Check();RequireOptional(ActivePath(root),next);
                if(next!=null)_=RequireReceipt(root,userData,owner,next,cancellation);StageForTest?.Invoke(BootstrapStage.BeforeSettlement);cancellation.ThrowIfCancellationRequested();
                outcome=source==null?BootstrapOutcome.Withdrawn:BootstrapOutcome.Deployed;finish=()=>Finish("finished",next);
            }
            catch(Exception operationError)
            {
                List<Exception> rollback=[];
                try
                {
                    Check();if(changed)Publish(before,next,Guid.NewGuid());RequireOptional(ActivePath(root),before);
                    if(previous!=null)_=RequireReceipt(root,userData,owner,before!,CancellationToken.None);
                    finish=()=>Finish("failed",before);
                }
                catch(Exception error){rollback.Add(error);}
                if(rollback.Count!=0)throw new BootstrapUnconfirmedException(new AggregateException(new[]{operationError}.Concat(rollback)));
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(operationError).Throw();throw;
            }
            void CheckCurrent(){admission.RequireMutation(root);InstallationControlledLaunch.Exact(Path.Combine(root,"current.json"),current,4096);}
            void Check(){CheckCurrent();InstallationControlledLaunch.Exact(PendingPath(root),journal,16384);RequireOptional(OwnerPath(root),owned?ownerBytes:ownerBefore,OwnerLimit);}
            void Finish(string suffix,byte[]? expected)
            {
                // After original admission close, pending still excludes every cooperative entrant.
                InstallationControlledLaunch.Exact(Path.Combine(root,"current.json"),current,4096);InstallationControlledLaunch.Exact(PendingPath(root),journal,16384);RequireOptional(OwnerPath(root),owned?ownerBytes:ownerBefore,OwnerLimit);RequireOptional(ActivePath(root),expected);
                if(expected!=null)_=RequireReceipt(root,userData,owner,expected,CancellationToken.None);
                string archive=Path.Combine(root,"bootstrap-"+suffix+"-"+operation.ToString("N")+".json");Absent(archive);File.Move(PendingPath(root),archive);
            }
            }
        }
        catch(Exception error){errors.Add(error);}
        finally
        {
            if(admission!=null)
            {
                try{admission.Dispose();}
                catch(Exception error){errors.Add(error);retained=admission;lock(Held)Held.Add(this);
                    try{if(!Exists(PendingPath(root)))SettingsStore.WriteFlushed(PendingPath(root),JsonSerializer.SerializeToUtf8Bytes(new{SchemaVersion=1,Product="KuaiYi",Phase="OriginalCleanupUnconfirmed"}));}catch(Exception marker){errors.Add(marker);}}
                if(retained==null&&finish!=null)try{finish();}catch(Exception error){errors.Add(new BootstrapUnconfirmedException(error));}
            }
        }
        InstallationControllerBridge.Throw(errors);return outcome;
    }
    private void Publish(byte[]? next,byte[]? expected,Guid operation)
    {
        RequireOptional(ActivePath(root),expected);
        if(next==null)
        {if(expected!=null){string prior=Path.Combine(root,"bootstrap-prior-"+operation.ToString("N")+".json");Absent(prior);File.Move(ActivePath(root),prior);}return;}
        string temporary=Path.Combine(root,"bootstrap-active-"+operation.ToString("N")+".tmp");Absent(temporary);SettingsStore.WriteFlushed(temporary,next);RequireOptional(ActivePath(root),expected);
        if(expected==null)File.Move(temporary,ActivePath(root));else File.Replace(temporary,ActivePath(root),null,false);
    }
    internal static void RequireBinding(string root,string data,InstalledVersion selected,BootstrapBinding binding,CancellationToken cancellation)
    {
        Absent(PendingPath(root));if(binding.Owner.Length>((OwnerLimit+2)/3)*4||binding.Active.Length>((ReceiptLimit+2)/3)*4)throw new InvalidDataException("Bootstrap binding exceeds encoded bound.");byte[] ownerBytes=Convert.FromBase64String(binding.Owner),active=Convert.FromBase64String(binding.Active);if(ownerBytes.Length>OwnerLimit||active.Length>ReceiptLimit)throw new InvalidDataException("Bootstrap binding exceeds bound.");
        InstallationControlledLaunch.Exact(OwnerPath(root),ownerBytes,OwnerLimit);InstallationControlledLaunch.Exact(ActivePath(root),active,ReceiptLimit);var owner=Decode<StoreOwner>(ownerBytes);RequireOwner(owner,root,data);
        if(RequireReceipt(root,data,owner,active,cancellation).Generation!=selected)throw new InstallationRecoveryException("Bootstrap selection generation differs.");
        InstallationControlledLaunch.Exact(OwnerPath(root),ownerBytes,OwnerLimit);InstallationControlledLaunch.Exact(ActivePath(root),active,ReceiptLimit);Absent(PendingPath(root));
    }
    internal static Receipt RequireReceipt(string root,string data,StoreOwner owner,byte[] bytes,CancellationToken cancellation)
    {
        var value=Decode<Receipt>(bytes);RequireOwner(owner,root,data);
        if(value.SchemaVersion!=1||value.Product!="KuaiYi"||value.Mode!=InstallationBootstrapPackage.Mode||value.Owner!=owner.Owner||value.Bundle==Guid.Empty||!InstallationPackage.HashValid(value.CatalogSha256)||value.Generation==null||InstallationNormalEntry.FindInstallationRoot(value.Generation.DirectoryPath)!=root)throw new InstallationRecoveryException("Unknown bootstrap ownership retained.");
        if(InstallationPayload.RequireMaterialized(value.Generation.DirectoryPath,cancellation).CatalogSha256!=value.Generation.CatalogSha256)throw new InstallationRecoveryException("Bootstrap generation changed.");
        var (_,revision)=InstallationBootstrapPackage.Read(BundlePath(root,value.Bundle),value.CatalogSha256,value.Generation,cancellation);if(revision!=value.Revision)throw new InstallationRecoveryException("Bootstrap build binding differs.");return value;
    }
    private static void RequireOwner(StoreOwner owner,string root,string data)
    {if(owner.SchemaVersion!=1||owner.Product!="KuaiYi"||owner.Owner==Guid.Empty||owner.Root!=root||owner.UserData!=data)throw new InstallationRecoveryException("Unknown bootstrap store owner retained.");}
    internal static T Decode<T>(byte[] bytes)where T:class
    {var value=JsonSerializer.Deserialize<T>(bytes)??throw new InstallationRecoveryException("Missing bootstrap metadata.");if(!bytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(value)))throw new InstallationRecoveryException("Unknown/noncanonical bootstrap metadata retained.");return value;}
    private static byte[]? ReadOptional(string path,int limit=ReceiptLimit)=>Exists(path)?InstallationPackage.ReadDocument(path,limit):null;
    internal static void RequireOptional(string path,byte[]? expected,int limit=ReceiptLimit){if(expected==null)Absent(path);else InstallationControlledLaunch.Exact(path,expected,limit);}
    internal static bool Exists(string path){InstallationPaths.Check(path);return File.Exists(path)||Directory.Exists(path);}
    internal static void Absent(string path){if(Exists(path))throw new InstallationRecoveryException("Unexpected bootstrap file retained.");}
}
