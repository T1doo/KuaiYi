using System.IO;
using KuaiYi.Core.Installation;
using KuaiYi.NativeHost;
namespace KuaiYi.Installer;

internal static class Program
{
    // These exact owners remain rooted through process exit if the original lifetime is unconfirmed.
    private static InstallationInstallerEntry? runtime;
    private static readonly InstallationInstallerCancellation Stop=new();
    internal static bool IsCommand(string[] args)=>args.SequenceEqual(new[]{"--install"});
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
            string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string root=Path.Combine(local,"KuaiYi.Application"),data=Path.Combine(local,"KuaiYi"),source=Path.Combine(AppContext.BaseDirectory,"installer-payload");
            runtime=new(bytes,root,data,Path.Combine(source,"generation"),Path.Combine(source,"bootstrap"),new InstallationDeploymentRegistrySnapshot(new WindowsHostRegistrationStore()),
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
}
