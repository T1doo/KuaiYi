using System.Diagnostics;
using System.Text.Json;
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
    private string? pending,backup,terminal;
    private byte[]? intent;
    private int prefix,attempt;
    private bool unknown,archived;
    internal bool CleanupConfirmed=>!unknown&&!registry.ConflictObserved&&io.CleanupConfirmed&&registry.CleanupConfirmed;
    internal InstallationStartupRemovalOwner(InstallationDeploymentSession session,InstallationUpgradeCoordinator controller,InstallationAdmission admission,
        InstallationControlledLaunch proof,Process child,IInstallationStartupRegistry registry,InstallationStartupPlan before)
    {this.session=session;this.controller=controller;this.admission=admission;this.proof=proof;this.child=child;this.registry=registry;this.before=before;io=InstallationPreparationIO.ReadOnly(session.Root);}
    internal static string PendingPath(string root)=>Path.Combine(root,"startup-settings-pending.json");
    private InstallationPreparationIO? writes;
    private InstallationStartupPlan Expected(int count)=>before with{Target=count>=1?new(false):before.Target,TargetOwner=count>=2?null:before.TargetOwner};
    private async Task Verify(int count)
    {
        try
        {
            if(unknown||registry.ConflictObserved||!CleanupConfirmed)throw new InstallationRecoveryException("Original startup owner is unknown.");
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
        pending=PendingPath(session.Root);backup=Path.Combine(session.Root,"startup-settings-before-"+request.ToString("N")+".json");
        terminal=Path.Combine(session.Root,"startup-settings-finished-"+request.ToString("N")+".json");
        io.RequireAbsent(pending);io.RequireAbsent(backup);io.RequireAbsent(terminal);
        writes=io.CreateSessionChannel(session.Root,[pending,backup,terminal,InstallationStartupRegistration.OwnerPath(session.Root)]);
        intent=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=3,kind="KuaiYiStartupRemoval",request,proof.Launch,proof.Fingerprint,basis,
            operation=session.Selection.Nonce, before=before.Target,owner=before.TargetOwner});
        try{writes.WritePublicationMetadata(pending,intent,16384);await Verify(0).ConfigureAwait(false);}
        catch{unknown=true;throw;}
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
        if(intent==null||archived||prefix!=0)throw new InstallationRecoveryException("Startup reservation is not prepared.");
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
        if(archived||intent==null)return;
        if(unknown||registry.ConflictObserved||!CleanupConfirmed)throw new InstallationRecoveryException("Unknown original startup operation retained.");
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
        await Verify(0).ConfigureAwait(false);await Finish().ConfigureAwait(false);
    }
    internal async Task CommitAsync()
    {
        if(prefix!=2||archived)throw new InstallationRecoveryException("Startup effects are not fully applied.");
        await Verify(2).ConfigureAwait(false);await Finish().ConfigureAwait(false);
    }
    private async Task Finish()
    {
        var errors=new List<Exception>();try{io.CloseOriginals();}catch(Exception error){unknown=true;errors.Add(error);}
        await CloseRegistry(errors).ConfigureAwait(false);InstallationErrors.Throw(errors);
        if(!CleanupConfirmed)throw new InstallationRecoveryException("Original startup cleanup is unconfirmed.");
        // All original resources have closed. Nothing after this terminal move may roll effects back.
        try{writes!.MovePublicationRecord(pending!,terminal!);archived=true;}catch{unknown=true;throw;}
    }
    internal void Disconnected(){if(intent!=null&&!archived)unknown=true;}
}
