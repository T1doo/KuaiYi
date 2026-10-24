using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using KuaiYi.Core.Settings;
namespace KuaiYi.Core.Installation;

internal sealed partial class InstallationDeploymentSession
{
    internal InstallationStartupRemovalOwner CreateStartupRemovalOwner(InstallationUpgradeCoordinator actual,InstallationAdmission admission,
        InstallationControlledLaunch proof,Process original)
    {
        RequireController(actual);
        lock(sync)
        {
            if(revoked||accepted==null||accepted.Launch!=proof.Launch||accepted.Proof!=proof.Fingerprint||original.HasExited||
                registry is not IInstallationStartupRegistry startup||prepared.Plan.Startup is not {} plan)
                throw new InstallationRecoveryException("Original qualified live READY session is required for startup removal.");
            admission.Require(Root);
            return new(this,actual,admission,proof,original,startup,plan);
        }
    }
    internal Task VerifyStartupRemovalAsync(InstallationUpgradeCoordinator actual,InstallationAdmission admission,
        InstallationControlledLaunch proof,Process original,InstallationStartupPlan expected)
    {
        RequireController(actual);admission.Require(Root);
        lock(sync)if(revoked||accepted==null||accepted.Launch!=proof.Launch||accepted.Proof!=proof.Fingerprint||original.HasExited)
            throw new InstallationRecoveryException("Original startup session has retired.");
        return VerifyCommittedAsync(expected);
    }
    internal Task VerifyStartupRetirementAsync(InstallationUpgradeCoordinator actual,InstallationAdmission admission,InstallationControlledLaunch proof,
        Process original,Task exit,Task originalIo,InstallationStartupPlan expected,InstallationPreparationIO originalReads)
    {
        RequireController(actual);
        actual.RequireOriginalStartupRetirement(this,admission,proof,original,exit,originalIo,originalReads);
        lock(sync)if(revoked||accepted==null||accepted.Launch!=proof.Launch||accepted.Proof!=proof.Fingerprint)
            throw new InstallationRecoveryException("Original startup retirement authority has retired.");
        return VerifyCommittedAsync(expected,originalReads); // Retirement-only reads. This does not reopen live registration permission.
    }
}

// This object is created only by the original qualified, accepted deployment session. No disk/PID/flag factory.
// It borrows the already-held admission. It never takes the coordinator serial or reopens upgrade.lock.
internal sealed class InstallationStartupRemovalOwner
{
    private readonly InstallationDeploymentSession session;
    private readonly InstallationUpgradeCoordinator controller;
    private readonly InstallationAdmission admission;
    private readonly InstallationControlledLaunch proof;
    private readonly Process child;
    private readonly IInstallationStartupRegistry registry;
    private readonly InstallationStartupPlan before;
    private readonly InstallationPreparationIO io;
    private readonly InstallationPreparationIO local;
    private string? pending,backup,terminal,bindingPath,decisionPath,localPath,localArchive;
    private byte[]? intent,bindingBytes,decisionBytes,localBytes;
    private SettingsStartupIntent? settings;
    private SettingsStartupIntentReferences? references;
    private SettingsStartupLocalSettlement? notice;
    private Guid request;
    private string? basis,durable;
    private int prefix,attempt;
    private bool unknown,archived,retirementAttempted,bindingAttempted,applyAttempted;
    private readonly object retirementSync=new();
    private bool closing;
    internal bool OriginalResourcesConfirmed=>!unknown&&!registry.ConflictObserved&&io.CleanupConfirmed&&local.CleanupConfirmed&&registry.CleanupConfirmed;
    internal bool CleanupConfirmed=>OriginalResourcesConfirmed&&(pending==null||archived);
    internal bool CanRetireEmptyReservation=>OriginalResourcesConfirmed&&intent!=null&&request!=Guid.Empty&&durable=="ABORTED"&&
        !bindingAttempted&&!applyAttempted&&attempt==0&&prefix==0&&settings==null&&references==null&&notice==null&&
        bindingBytes==null&&localBytes==null&&localPath==null&&localArchive==null;

    internal InstallationStartupRemovalOwner(InstallationDeploymentSession session,InstallationUpgradeCoordinator controller,InstallationAdmission admission,
        InstallationControlledLaunch proof,Process child,IInstallationStartupRegistry registry,InstallationStartupPlan before)
    {this.session=session;this.controller=controller;this.admission=admission;this.proof=proof;this.child=child;this.registry=registry;this.before=before;io=InstallationPreparationIO.ReadOnly(session.Root);local=InstallationPreparationIO.ReadOnly(session.UserData);}
    internal static string PendingPath(string root)=>Path.Combine(root,"startup-settings-pending.json");
    private InstallationPreparationIO? writes;
    private InstallationStartupPlan Expected(int count)=>before with{Target=count>=1?new(false):before.Target,TargetOwner=count>=2?null:before.TargetOwner};
    private async Task Verify(int count)
    {
        try
        {
            if(unknown||registry.ConflictObserved||!OriginalResourcesConfirmed)throw new InstallationRecoveryException("Original startup owner is unknown.");
            await session.VerifyStartupRemovalAsync(controller,admission,proof,child,Expected(count)).ConfigureAwait(false);
            if(intent!=null&&!archived)io.RequireExactMetadata(pending!,intent,16384);
            if(count>=2&&before.TargetOwner!=null)io.RequireExactMetadata(backup!,Convert.FromBase64String(before.TargetOwner),InstallationStartupRegistration.OwnerLimit);
        }
        catch{unknown=true;throw;} // A foreign read/unknown close is sticky even if external bytes later return to Before.
    }
    internal async Task ReserveAsync(Guid request,string basis)
    {
        if(pending!=null)throw new InstallationRecoveryException("One original startup request only.");
        await Verify(0).ConfigureAwait(false);
        this.request=request;this.basis=basis;
        pending=PendingPath(session.Root);backup=Path.Combine(session.Root,"startup-settings-before-"+request.ToString("N")+".json");
        terminal=Path.Combine(session.Root,"startup-settings-finished-"+request.ToString("N")+".json");
        bindingPath=Path.Combine(session.Root,"startup-settings-binding-"+request.ToString("N")+".json");decisionPath=Path.Combine(session.Root,"startup-settings-decision-"+request.ToString("N")+".json");
        foreach(string path in new[]{pending,backup,terminal,bindingPath,decisionPath})io.RequireAbsent(path);
        writes=io.CreateSessionChannel(session.Root,[pending,backup,terminal,bindingPath,decisionPath,InstallationStartupRegistration.OwnerPath(session.Root)]);
        intent=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=3,kind="KuaiYiStartupRemoval",request,proof.Launch,proof.Fingerprint,basis,
            operation=session.Selection.Nonce, before=before.Target,owner=before.TargetOwner});
        try{writes.WritePublicationMetadata(pending,intent,16384);await Verify(0).ConfigureAwait(false);}
        catch{unknown=true;throw;}
    }
    internal Task BindSettingsAsync(SettingsStartupIntent value)
    {
        bindingAttempted=true; // A failed/partial bind cannot later be treated as an empty reservation.
        try
        {
            if(!OriginalResourcesConfirmed||durable!=null||value.Request!=request||value.BasisVersion!=basis||settings!=null&&settings!=value)
                throw new InstallationRecoveryException("Different original settings intent.");
            string config=Path.Combine(session.UserData,"config");local.RequireDirectory(config);
            string path=Path.Combine(config,"secret-txn-"+value.WriteId.ToString("N")+".json");
            byte[] bytes=local.ReadDocument(path,16384);var refs=SettingsCoordinator.ReadOriginalStartupIntent(bytes,value);
            if(settings==null)
            {
                localPath=path;localArchive=Path.Combine(config,"startup-settled-"+request.ToString("N")+"-"+value.WriteId.ToString("N")+".json");local.RequireAbsent(localArchive);
                localBytes=bytes;settings=value;references=refs;bindingBytes=JsonSerializer.SerializeToUtf8Bytes(value,SettingsJson.Options);
                writes!.WritePublicationMetadata(bindingPath!,bindingBytes,4096);
            }
            else if(!bytes.AsSpan().SequenceEqual(localBytes))throw new InstallationRecoveryException("Original v3 bytes changed.");
            io.RequireExactMetadata(bindingPath!,bindingBytes!,4096);return Task.CompletedTask;
        }
        catch{unknown=true;throw;}
    }
    internal void ObserveLocalSettlement(SettingsStartupLocalSettlement value)
    {
        if(notice!=null||settings==null||value.Request!=request||value.WriteId!=settings.WriteId||value.Sha256!=settings.Sha256||value.Outcome!=durable)
        {unknown=true;throw new InstallationRecoveryException("Local settlement differs from the original intent/decision.");}
        notice=value;
        if(!value.CleanupConfirmed){unknown=true;throw new InstallationRecoveryException("Application local cleanup is unconfirmed.");}
    }
    private async Task CloseRegistry(List<Exception> errors)
    {
        try{await registry.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){unknown=true;errors.Add(error);}
        if(!registry.CleanupConfirmed||registry.ConflictObserved)unknown=true;
    }
    private async Task SetValue(InstallationStartupValue from,InstallationStartupValue to)
    {
        var errors=new List<Exception>();
        try{await registry.WriteStartupAsync(from,to).ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        await CloseRegistry(errors).ConfigureAwait(false);
        if(errors.Count!=0)
        {
            // A known write-after-effect is compensable, but an observed foreign compare/close never washes out.
            if(!unknown)
            {
                try
                {
                    var actual=await registry.ReadStartupAsync().ConfigureAwait(false);
                    if(actual==to)prefix=to==before.Target?0:1;
                    else if(actual!=from)unknown=true;
                }
                catch(Exception error){unknown=true;errors.Add(error);}
                await CloseRegistry(errors).ConfigureAwait(false);
            }
            InstallationErrors.Throw(errors);
        }
    }
    internal async Task ApplyAsync()
    {
        applyAttempted=true;
        if(intent==null||settings==null||archived||prefix!=0)throw new InstallationRecoveryException("Startup reservation and settings binding are not prepared.");
        await Verify(0).ConfigureAwait(false);attempt=1;
        await SetValue(before.Target,new(false)).ConfigureAwait(false);prefix=1;
        await Verify(1).ConfigureAwait(false);attempt=2;
        if(before.TargetOwner!=null)
            try{writes!.MovePublicationRecord(InstallationStartupRegistration.OwnerPath(session.Root),backup!);}
            catch{unknown=true;throw;}
        prefix=2;await Verify(2).ConfigureAwait(false);
    }
    internal async Task AbortAsync()
    {
        if(durable!=null||intent==null)return;
        if(unknown||registry.ConflictObserved||!OriginalResourcesConfirmed)throw new InstallationRecoveryException("Unknown original startup operation retained.");
        if(attempt==1)
        {
            var errors=new List<Exception>();InstallationStartupValue? actual=null;
            try{actual=await registry.ReadStartupAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);unknown=true;}
            await CloseRegistry(errors).ConfigureAwait(false);InstallationErrors.Throw(errors);
            if(actual==new InstallationStartupValue(false))prefix=1;
            else if(actual==before.Target)prefix=0;
            else{unknown=true;throw new InstallationRecoveryException("Startup attempt observed foreign value.");}
        }
        await Verify(prefix).ConfigureAwait(false);
        if(prefix>=2)
        {
            if(before.TargetOwner!=null)
                try{writes!.MovePublicationRecord(backup!,InstallationStartupRegistration.OwnerPath(session.Root));}
                catch{unknown=true;throw;}
            prefix=1;
        }
        await Verify(prefix).ConfigureAwait(false);
        if(prefix==1){await SetValue(new(false),before.Target).ConfigureAwait(false);prefix=0;}
        await Verify(0).ConfigureAwait(false);await ObserveDurableDecision("ABORTED").ConfigureAwait(false);
    }
    internal async Task CommitAsync()
    {
        if(prefix!=2||durable!=null)throw new InstallationRecoveryException("Startup effects are not fully applied.");
        await Verify(2).ConfigureAwait(false);await ObserveDurableDecision("COMMITTED").ConfigureAwait(false);
    }
    private async Task CloseAll()
    {
        var errors=new List<Exception>();try{io.CloseOriginals();}catch(Exception error){unknown=true;errors.Add(error);}
        try{local.CloseOriginals();}catch(Exception error){unknown=true;errors.Add(error);}
        await CloseRegistry(errors).ConfigureAwait(false);InstallationErrors.Throw(errors);
        if(!OriginalResourcesConfirmed)throw new InstallationRecoveryException("Original startup cleanup is unconfirmed.");
    }
    private async Task ObserveDurableDecision(string outcome)
    {
        try
        {
            await CloseAll().ConfigureAwait(false);decisionBytes=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,request,outcome,settings});
            writes!.WritePublicationMetadata(decisionPath!,decisionBytes,4096);io.RequireExactMetadata(decisionPath!,decisionBytes,4096);
            await CloseAll().ConfigureAwait(false);durable=outcome; // Still provisional: pending and v3 remain until original exit/IO.
        }
        catch{unknown=true;throw;}
    }
    private byte[]? OptionalLocal(string path,int maximum)
    {if(File.Exists(path))return local.ReadDocument(path,maximum);local.RequireAbsent(path);return null;}
    internal async Task RetireEmptyReservationAsync(Process original,Task exit,Task originalIo)
    {
        if(retirementAttempted)throw new InstallationRecoveryException("Original empty reservation retirement cannot be replayed.");retirementAttempted=true;
        var errors=new List<Exception>();
        try
        {
            if(!ReferenceEquals(original,child)||!CanRetireEmptyReservation)
                throw new InstallationRecoveryException("Only an original never-bound, never-applied aborted reservation can retire without local intent.");
            await session.VerifyStartupRetirementAsync(controller,admission,proof,child,exit,originalIo,Expected(0),io).ConfigureAwait(false);
            io.RequireExactMetadata(pending!,intent!,16384);io.RequireExactMetadata(decisionPath!,decisionBytes!,4096);
            io.RequireAbsent(bindingPath!);io.RequireAbsent(backup!);io.RequireAbsent(terminal!);
        }
        catch(Exception error){unknown=true;errors.Add(error);}
        try{await CloseAll().ConfigureAwait(false);}catch(Exception error){unknown=true;errors.Add(error);}
        InstallationErrors.Throw(errors);
        try
        {
            // No local v3 was bound and no startup effect was attempted. Do not invent a notice, binding or local file ownership.
            lock(retirementSync){if(!CanRetireEmptyReservation)throw new InstallationRecoveryException("Original empty reservation was revoked before Closing.");closing=true;}
            writes!.MovePublicationRecord(pending!,terminal!);archived=true; // All original reads, closes, process and responses settled first.
        }
        catch{unknown=true;throw;}
    }
    internal async Task RetireSettingsAsync(Process original,Task exit,Task originalIo)
    {
        if(retirementAttempted)throw new InstallationRecoveryException("Original settings retirement cannot be replayed.");retirementAttempted=true;
        var errors=new List<Exception>();InstallationPreparationIO? retirement=null;
        try
        {
            if(!ReferenceEquals(original,child)||settings==null||notice==null||references==null||notice.Outcome!=durable||!notice.CleanupConfirmed||!OriginalResourcesConfirmed)
                throw new InstallationRecoveryException("Original settings retirement evidence is incomplete.");
            await session.VerifyStartupRetirementAsync(controller,admission,proof,child,exit,originalIo,Expected(durable=="COMMITTED"?2:0),io).ConfigureAwait(false);
            io.RequireExactMetadata(pending!,intent!,16384);io.RequireExactMetadata(bindingPath!,bindingBytes!,4096);io.RequireExactMetadata(decisionPath!,decisionBytes!,4096);io.RequireAbsent(terminal!);
            if(durable=="COMMITTED"&&before.TargetOwner!=null)io.RequireExactMetadata(backup!,Convert.FromBase64String(before.TargetOwner),InstallationStartupRegistration.OwnerLimit);
            else io.RequireAbsent(backup!);
            local.RequireExactMetadata(localPath!,localBytes!,16384);local.RequireAbsent(localArchive!);
            byte[]? primary=OptionalLocal(Path.Combine(session.UserData,"config","settings.json"),SettingsJson.MaximumBytes),backupConfig=OptionalLocal(Path.Combine(session.UserData,"config","settings.backup.json"),SettingsJson.MaximumBytes);
            string Digest(byte[]? raw)=>raw==null?"absent":Convert.ToHexString(SHA256.HashData(raw));
            string version=Digest(primary)+":"+Digest(backupConfig);var config=primary==null?new AppSettings():SettingsJson.Read(primary,out _);if(backupConfig!=null)_=SettingsJson.Read(backupConfig,out _);
            if(version!=notice.ConfigVersion||config.LaunchAtSignIn||durable=="COMMITTED"&&config.WriteId!=settings.WriteId||durable=="ABORTED"&&version!=settings.BasisVersion)
                throw new InstallationRecoveryException("Configuration does not match the original local result.");
            var live=config.Profiles.Where(p=>p.SecretRef!=null).Select(p=>p.SecretRef!).ToHashSet(StringComparer.Ordinal);
            foreach(string reference in durable=="COMMITTED"?references.RetiredReferences:references.NewReferences)
            {
                if(live.Contains(reference))throw new InstallationRecoveryException("Local cleanup would retire a live binding.");
                string blob=Path.Combine(session.UserData,"secrets",reference[4..]+".bin");local.RequireAbsent(blob);
                if(durable=="COMMITTED")local.RequireExactMetadata(Path.ChangeExtension(blob,".revoked"),"revoked"u8.ToArray(),64);
            }
            retirement=local.CreateSessionChannel(session.UserData,[localPath!,localArchive!]);
        }
        catch(Exception error){unknown=true;errors.Add(error);}
        try{await CloseAll().ConfigureAwait(false);}catch(Exception error){unknown=true;errors.Add(error);}
        InstallationErrors.Throw(errors);
        try
        {
            // Every original read/response/process/IO is settled before these two one-shot moves. No failing cleanup follows.
            lock(retirementSync){if(!OriginalResourcesConfirmed)throw new InstallationRecoveryException("Original retirement was revoked before Closing.");closing=true;}
            retirement!.MovePublicationRecord(localPath!,localArchive!);writes!.MovePublicationRecord(pending!,terminal!);archived=true;
        }
        catch{unknown=true;throw;}
    }
    internal void Disconnected(){lock(retirementSync)if(intent!=null&&!archived&&!closing)unknown=true;}
}
