using System.IO;
using KuaiYi.Core.Installation;
using KuaiYi.NativeHost;
namespace KuaiYi.Installer;

internal static class Program
{
    // These exact owners remain rooted through process exit if the original lifetime is unconfirmed.
    private static InstallationInstallerEntry? runtime;
    private static InstallationMaintenanceEntry? maintenance;
    private static readonly InstallationInstallerCancellation Stop=new();
    internal static bool IsCommand(string[] args)=>InstallationCommandLine.Parse(args)!=null;
    // Internal withdrawal remains separate from public maintenance observations; no observation can invoke it.
    internal static InstallationRegistrationWithdrawal PrepareRegistrationWithdrawal()
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        using var resource=typeof(Program).Assembly.GetManifestResourceStream("KuaiYi.InstallerSourcePins")??throw new InvalidDataException("Embedded installer trust is absent.");
        if(resource.Length is <1 or >4096)throw new InvalidDataException("Embedded installer trust exceeds bound.");
        byte[] bytes=new byte[(int)resource.Length];resource.ReadExactly(bytes);_=InstallationInstallerPins.Read(bytes);
        string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new(bytes,Path.Combine(local,"KuaiYi.Application"),Path.Combine(local,"KuaiYi"),InstallationDeploymentRegistrySnapshot.CreateWindows());
    }
    public static int Main(string[] args)
    {
        if(!IsCommand(args)||!OperatingSystem.IsWindows())return 2;
        try
        {
            var assembly=typeof(Program).Assembly;
            using var resource=assembly.GetManifestResourceStream("KuaiYi.InstallerSourcePins")??throw new InvalidDataException("Embedded installer trust is absent.");
            if(resource.Length is <1 or >4096)throw new InvalidDataException("Embedded installer trust exceeds bound.");
            byte[] bytes=new byte[(int)resource.Length];resource.ReadExactly(bytes);
            _=InstallationInstallerPins.Read(bytes); // Fail closed before resolving destinations or constructing platform owners.
            var command=InstallationCommandLine.Parse(args)!.Value;
            if(command!=InstallationPublicCommand.Install)return RunMaintenance(bytes,command);
            string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string root=Path.Combine(local,"KuaiYi.Application"),data=Path.Combine(local,"KuaiYi"),source=Path.Combine(AppContext.BaseDirectory,"installer-payload");
            runtime=new(bytes,root,data,Path.Combine(source,"generation"),Path.Combine(source,"bootstrap"),InstallationDeploymentRegistrySnapshot.CreateWindows(),
                _=>Task.FromResult(new InstallationUpgradeCoordinator(root,data,new InstallationApplicationClient(),TimeSpan.FromSeconds(10))),
                result=>{Console.WriteLine("INSTALL_STAGE|"+result.Status);return Task.CompletedTask;},Stop.Token);
            ConsoleCancelEventHandler cancel=(_,args)=>{args.Cancel=true;_=Stop.Request();};
            Console.CancelKeyPress+=cancel;
            InstallationInstallerSettlement settled;IReadOnlyList<Exception> cancellationErrors;
            try{_=runtime.Start();settled=runtime.OriginalWork.GetAwaiter().GetResult();}
            finally
            {
                // Keep suppressing further Ctrl+C while the sealed original cancellation callback is still pending.
                try{cancellationErrors=Stop.SealAndJoinAsync().GetAwaiter().GetResult();}
                finally{Console.CancelKeyPress-=cancel;}
            }
            // No exit at stage READY and no generic Dispose of a controller that may own a live UI.
            Console.WriteLine("INSTALL_SETTLED|"+(settled.CleanupConfirmed?"Confirmed":"Unconfirmed"));
            foreach(var error in cancellationErrors)Console.Error.WriteLine("Installer cancellation failed: "+error.GetType().Name);
            return cancellationErrors.Count==0?settled.ExitCode:3;
        }
        catch(Exception error){Console.Error.WriteLine("Installer entry refused: "+error.GetType().Name);return 3;}
    }
    private static int RunMaintenance(byte[] bytes,InstallationPublicCommand command)
    {
        var intent=command==InstallationPublicCommand.Repair?InstallationMaintenanceIntent.Repair:InstallationMaintenanceIntent.Uninstall;
        maintenance=new(bytes,intent,(_,requested)=>
        {
            string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return InstallationRegistrationWithdrawal.ForMaintenanceExecution(bytes,Path.Combine(local,"KuaiYi.Application"),Path.Combine(local,"KuaiYi"),
                InstallationDeploymentRegistrySnapshot.CreateWindows(),requested,Path.Combine(AppContext.BaseDirectory,"installer-payload"),Stop.Token);
        },()=>Path.Combine(AppContext.BaseDirectory,"installer-payload"),Stop.Token);
        ConsoleCancelEventHandler cancel=(_,args)=>{args.Cancel=true;_=Stop.Request();};Console.CancelKeyPress+=cancel;
        InstallationMaintenanceOutcome result;IReadOnlyList<Exception> errors;
        try{result=maintenance.Start().GetAwaiter().GetResult();}
        finally{try{errors=Stop.SealAndJoinAsync().GetAwaiter().GetResult();}finally{Console.CancelKeyPress-=cancel;}}
        foreach(var line in InstallationMaintenanceReceiptText.Lines(result))Console.WriteLine(line);
        foreach(var error in result.Errors.Concat(errors))Console.Error.WriteLine("Maintenance refused/pending: "+error.GetType().Name);
        // Qualified read-only observations are useful, but never a successful repair/uninstall receipt.
        return InstallationMaintenanceReceiptText.ExitCode(result,errors.Count>0);
    }

}
