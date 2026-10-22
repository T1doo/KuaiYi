using System.Text.Json;
using KuaiYi.Core.Installation;

// Explicit synthetic child only. Never referenced by App/NativeHost/Launcher and never accesses registry.
internal static class InstallationDeploymentCrashFixture
{
    internal static async Task<int> Run(string root,string generationPin,string bootstrapPin,string phase)
    {
        root=Path.GetFullPath(root);if(!Path.GetFileName(root).StartsWith("kuaiyi-publication-",StringComparison.Ordinal)||File.ReadAllText(Path.Combine(root,"PUBLIC_CRASH_ALLOWED"))!="PUBLIC_SYNTHETIC_ONLY"||File.ReadAllText(Path.Combine(root,"data/PUBLIC_OFF"))!="PUBLIC_PRESERVED")return 2;
        if(phase is not("Current" or "Active" or "Chrome" or "Edge" or "HostReceipt" or "Closing" or "Archive"))return 2;
        void Crash(){Console.WriteLine("PUBLIC_DEPLOYMENT_CRASH_"+phase);Console.Out.Flush();Environment.Exit(73);}
        var faults=new InstallationDeploymentFaults{AfterClosing=()=>{if(phase=="Closing")Crash();return Task.CompletedTask;},Archive=(from,to)=>{File.Move(from,to);if(phase=="Archive")Crash();}};
        var registry=new SyntheticRegistry(Path.Combine(root,"PUBLIC_SYNTHETIC_REGISTRY.json"));var scope=new InstallationDeploymentScope(Path.Combine(root,"app"),Path.Combine(root,"data"),generationPin,bootstrapPin,faults:faults);await scope.AcquireAsync();await scope.CaptureBeforeAsync(registry);var g=await scope.PrepareGenerationAsync(Path.Combine(root,"generation-source"));var b=await scope.PrepareBootstrapAsync(Path.Combine(root,"bootstrap-source"),g);var p=await scope.SealPreparedAsync(g,b);await scope.CommitPreparedAsync(p,new(){Stage=at=>{if(at.ToString()==phase)Crash();}});return 3;
    }
    private sealed class SyntheticRegistry(string path):IInstallationDeploymentPublicationRegistry
    {
        private InstallationDeploymentRegistrationSnapshot? original;
        public bool CleanupConfirmed=>true; // File convenience APIs below synchronously own/close their synthetic fixture handles.
        private InstallationDeploymentRegistrationSnapshot Read()=>File.Exists(path)?JsonSerializer.Deserialize<InstallationDeploymentRegistrationSnapshot>(File.ReadAllBytes(path))!:new(new(false,null),new(false,null));
        public Task<InstallationDeploymentRegistrationSnapshot> CaptureAsync(){original=Read();File.WriteAllBytes(path,JsonSerializer.SerializeToUtf8Bytes(original));return Task.FromResult(original);}
        public Task<InstallationDeploymentRegistrationSnapshot> ReadForPreparationAsync()=>Task.FromResult(Read());
        public Task<InstallationDeploymentRegistrationSnapshot> ReadPublishedAsync()=>Task.FromResult(Read());
        public Task WriteDefaultAsync(bool chrome,InstallationDeploymentRegistrationSlot expected,string? value)
        {
            var now=Read();if((chrome?now.Chrome:now.Edge)!=expected)throw new IOException("PUBLIC_SYNTHETIC_CONFLICT");var next=chrome?now with{Chrome=new(true,value)}:now with{Edge=new(true,value)};File.WriteAllBytes(path,JsonSerializer.SerializeToUtf8Bytes(next));return Task.CompletedTask;
        }
        public Task VerifyAsync(){if(Read()!=original)throw new IOException("PUBLIC_SYNTHETIC_CONFLICT");return Task.CompletedTask;}
        public Task CloseOriginalsAsync()=>Task.CompletedTask;
    }
}
