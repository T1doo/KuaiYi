using System.Diagnostics;
using System.Security.Cryptography;
using KuaiYi.Core;
using KuaiYi.Core.Installation;

internal static class InstallationSessionLaunchFixture
{
    private static KuaiYi.App.InstallationSessionEntry? entry;
    internal static async Task<int> Run(string root,string directory)
    {
        _=Task.Run(async()=>{await Task.Delay(TimeSpan.FromSeconds(40));Environment.Exit(92);});
        try
        {
            var port=new Port();entry=KuaiYi.App.InstallationSessionEntry.TryCreate(directory,Path.Combine(root,"user-data"),port,TimeSpan.FromSeconds(2),port)
                ??throw new InvalidDataException("Fixture requires installed layout.");
            int result=await entry.RunAsync();Console.WriteLine("PUBLIC_SESSION_COMPLETE|"+result);return result;
        }
        catch(Exception error){Console.WriteLine("PUBLIC_SESSION_REFUSED|"+error.GetType().Name);return 3;}
    }
    internal static async Task<int> RunController(Guid nonce)
    {
        string root=Directory.GetParent(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory))!.Parent!.Parent!.FullName;
        try
        {
            var port=new Port(true);entry=KuaiYi.App.InstallationSessionEntry.TryCreate(AppContext.BaseDirectory,Path.Combine(root,"user-data"),port,TimeSpan.FromSeconds(2),port)
                ??throw new InvalidDataException("Controller fixture needs installed layout.");
            return await entry.RunControllerAsync(Console.OpenStandardInput(),Console.OpenStandardOutput(),nonce);
        }
        catch(Exception error){Console.Error.WriteLine("PUBLIC_CONTROLLER_REFUSED|"+error.GetType().Name);return 3;}
    }
    private sealed class Port(bool quiet=false):IInstallationSessionProtocol,IOwnedProcessStarter
    {
        private readonly InstallationApplicationClient client=new();
        private bool retirementReported;
        public Process Start(ProcessStartInfo info)
        {
            string directory=Path.GetDirectoryName(info.FileName)!;
            info.FileName=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet";
            info.ArgumentList.Insert(0,Path.Combine(directory,"KuaiYi.SettingsStoreFixture.dll"));
            Process child=Process.Start(info)??throw new IOException("Actual fixture Start failed.");
            if(!quiet)Console.WriteLine("PUBLIC_SESSION_CHILD_STARTED");return child;
        }
        public Task AdmitLaunchAsync(Process p,InstallationControlledLaunch proof,CancellationToken ct)=>client.AdmitLaunchAsync(p,proof,ct);
        public async Task ConfirmHealthyAsync(Process p,InstalledVersion v,Guid launch,CancellationToken ct)
        {await client.ConfirmHealthyAsync(p,v,launch,ct);if(!quiet)Console.WriteLine("PUBLIC_SESSION_HEALTHY");}
        public Task RequestExitAsync(Process p,CancellationToken ct)=>client.RequestExitAsync(p,ct);
        public async Task SettleAsync(Process p,CancellationToken ct)
        {
            if(!p.HasExited)throw new IOException("Original child exit is not observed.");
            Task original=client.SettleAsync(p,ct);List<Exception> errors=[];
            try{await original;}catch(Exception error){errors.Add(error);}
            // Canceled observation does not establish that the underlying join finished.
            if(original.IsCompletedSuccessfully||original.IsFaulted)
                try{Retired(p,true,original.IsFaulted?"FaultObserved":"Settled");}catch(Exception error){errors.Add(error);}
            if(errors.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if(errors.Count>1)throw new AggregateException(errors);
        }
        public async Task SettleSessionAsync(Process p,CancellationToken ct)
        {
            await client.SettleSessionAsync(p,ct);
            Retired(p);
        }
        private void Retired(Process p,bool cleanup=false,string io="Settled")
        {
            if(quiet||retirementReported)return;retirementReported=true;
            string directory=Path.GetDirectoryName(typeof(InstallationSessionLaunchFixture).Assembly.Location)!;
            string prefix=cleanup?"PUBLIC_SESSION_CHILD_CLEANUP_EXIT":"PUBLIC_SESSION_CHILD_EXIT";
            Console.WriteLine($"{prefix}|{p.ExitCode}|originalIO={io}|driverSha256={Hash(Path.Combine(directory,"KuaiYi.SettingsStoreFixture.dll"))}|coreSha256={Hash(Path.Combine(directory,"KuaiYi.Core.dll"))}");
        }
        private static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }
}
