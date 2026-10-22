namespace KuaiYi.Core.Installation;

public sealed partial class InstallationUpgradeCoordinator
{
    internal async Task<IReadOnlyList<Exception>> JoinInstallerOriginalsAsync(InstallationDeploymentSession? deployment,InstallationRestoredSession? restored)
    {
        bool same=deployment!=null&&ReferenceEquals(deploymentSession,deployment)||restored!=null&&ReferenceEquals(restoredSession,restored);
        if(!same)
        {
            if(roles.Count!=0)throw new InstallationRecoveryException("Cannot join another controller's process originals.");
            return [];
        }
        if(deployment!=null&&!deployment.Completion.IsCompleted||restored!=null&&!restored.Completion.IsCompleted)throw new InstallationRecoveryException("Original session producer is not sealed.");
        var errors=new List<Exception>();
        async Task Join(Task? original){if(original!=null)try{await original.ConfigureAwait(false);}catch(Exception error){errors.Add(error);}}
        // RunSession and its finally have completed: no role/start/protocol producer may be reopened here.
        foreach(var role in roles.ToArray())
        {
            await Join(role.Startup).ConfigureAwait(false);await Join(role.Lifetime.StartupObservation).ConfigureAwait(false);
            foreach(var task in role.Tasks)await Join(task).ConfigureAwait(false);
            errors.AddRange(await role.Lifetime.JoinOriginalExitAsync().ConfigureAwait(false));
        }
        await Join(stablePublicationOriginal).ConfigureAwait(false);return errors;
    }
}

internal sealed partial class InstallationOperation
{
    private Task<IReadOnlyList<Exception>>? originalExitFence;
    internal Task<IReadOnlyList<Exception>> JoinInstallerOriginalsAsync()
    {
        lock(sync)return originalExitFence??=Task.Run(async()=>
        {
            await OriginalWork.ConfigureAwait(false);var errors=new List<Exception>();
            Task[] originals;lock(sync)originals=controllerTasks.ToArray();
            foreach(var task in originals)try{await task.ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
            if(NewSession!=null&&NewController!=null)errors.AddRange(await NewController.JoinInstallerOriginalsAsync(NewSession,null).ConfigureAwait(false));
            if(PreviousSession!=null&&PreviousController!=null)errors.AddRange(await PreviousController.JoinInstallerOriginalsAsync(null,PreviousSession).ConfigureAwait(false));
            return (IReadOnlyList<Exception>)errors;
        });
    }
}
