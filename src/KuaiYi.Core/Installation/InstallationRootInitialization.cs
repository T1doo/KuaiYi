using System.Text.Json;
using System.Runtime.InteropServices;
namespace KuaiYi.Core.Installation;

// Owned before Scope's acquisition task starts. This is empty foundation creation, never installation success.
internal sealed class InstallationRootInitialization
{
    private readonly string root,staging,operation;
    private readonly byte[] marker;
    private readonly InstallationPreparationIO io;
    private readonly InstallationPreparationFaults? faults;
    private readonly CancellationToken cancellation;
    private bool attempted,touched,moved,handedOff,unknown;
    internal static string Pending(string root)=>Path.Combine(root,"root-initialization-pending.json");
    internal bool Unconfirmed=>unknown||touched&&!handedOff;
    internal bool CleanupConfirmed=>io.CleanupConfirmed;
    internal InstallationRootInitialization(string root,string operation,CancellationToken cancellation,InstallationPreparationFaults? faults)
    {
        this.root=root;this.operation=operation;this.cancellation=cancellation;this.faults=faults;
        staging=Path.Combine(Path.GetDirectoryName(root)!,".kuaiyi-initializing-"+Guid.NewGuid().ToString("N"));
        marker=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,kind="KuaiYiRootInitialization",operation,root});
        if(marker.Length>16384)throw new InvalidDataException("Initial root marker exceeds bound.");
        io=new(Path.GetDirectoryName(root)!,staging,root,Pending(root),cancellation,faults);
    }
    internal void Prepare()
    {
        if(attempted)throw new InvalidOperationException("One initialization attempt per original scope.");attempted=true;
        cancellation.ThrowIfCancellationRequested();InstallationPaths.Check(root);
        try{_=File.GetAttributes(root);return;}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
        // No recursive parent creation or empty-directory adoption.
        io.RequireDirectory(Path.GetDirectoryName(root)!);
        try
        {
            touched=true;InstallationPaths.Check(staging);
            if(OperatingSystem.IsWindows())
            {if(!CreateDirectory(staging,0))throw new IOException("Exclusive initial directory creation failed: "+Marshal.GetLastWin32Error());}
            else if(OperatingSystem.IsLinux())
            {if(MakeDirectory(staging,448)!=0)throw new IOException("Exclusive initial directory creation failed: "+Marshal.GetLastWin32Error());}
            else throw new PlatformNotSupportedException();
            io.WriteMetadata(Path.Combine(staging,Path.GetFileName(Pending(root))),marker,16384);
            io.WriteMetadata(Path.Combine(staging,"owner.txt"),InstallationStore.InitialOwnerBytes,128);
            foreach(string name in new[]{"installation.lock","upgrade.lock","host-activity.lock"})io.CreateEmptyFile(Path.Combine(staging,name));
            Verify(staging);io.MoveToFinal();moved=true;Verify(root);
        }
        catch{unknown=true;throw;}
    }
    private void Verify(string directory)
    {
        io.RequireExactMetadata(Path.Combine(directory,Path.GetFileName(Pending(root))),marker,16384);
        io.RequireExactMetadata(Path.Combine(directory,"owner.txt"),InstallationStore.InitialOwnerBytes,128);
        foreach(string name in new[]{"installation.lock","upgrade.lock","host-activity.lock"})io.RequireEmptyFile(Path.Combine(directory,name));
        if(!io.Entries(directory).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{Path.GetFileName(Pending(root)),"owner.txt","installation.lock","upgrade.lock","host-activity.lock"}))throw new InstallationRecoveryException("Foreign initial root content retained.");
        if(!io.CleanupConfirmed||io.StorageUnconfirmed)throw new InstallationRecoveryException("Initial root originals remain unsettled.");
    }
    internal void RequireSetup(string expected)
    {
        if(expected!=root||!moved||handedOff||unknown||!io.CleanupConfirmed||io.StorageUnconfirmed)throw new InstallationRecoveryException("Original initial root authority is unavailable.");
        io.RequireExactMetadata(Pending(root),marker,16384);
    }
    internal void HandoffAfterTicket(string expected)
    {
        if(!touched)return;
        try
        {
            RequireSetup(expected);string finished=Path.Combine(root,"root-initialized-"+operation+".json");
            var channel=io.CreatePublicationChannel(root,new[]{Pending(root),finished},faults);channel.RequireAbsent(finished);
            channel.MovePublicationRecord(Pending(root),finished);handedOff=true;
        }
        catch{unknown=true;throw;}
    }
    internal void CloseOriginals()=>io.CloseOriginals();
    [DllImport("kernel32.dll",EntryPoint="CreateDirectoryW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]private static extern bool CreateDirectory(string path,nint attributes);
    [DllImport("libc",EntryPoint="mkdir",SetLastError=true)]private static extern int MakeDirectory(string path,uint mode);
    internal static void RejectPending(string root,InstallationRootInitialization? original=null)
    {
        string path=Pending(root);InstallationPaths.Check(path);
        try{_=File.GetAttributes(path);}catch(FileNotFoundException){return;}
        if(original==null)throw new InstallationRecoveryException("Unfinished root initialization retained; no authority reconstructed.");
        original.RequireSetup(root);
    }
}
