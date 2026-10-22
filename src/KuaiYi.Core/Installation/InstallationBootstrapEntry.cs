namespace KuaiYi.Core.Installation;

// Holds the exact six original opened files; never reopens replacement handles as proof of cleanup.
internal sealed class InstallationBootstrapEntry:IDisposable
{
    internal string Root{get;}
    internal BootstrapBinding Binding{get;private set;}=null!;
    private readonly List<FileStream> files=[];
    private InstallationAdmission? admission;private Action<InstallationAdmission>? closeForTest;
    internal static IReadOnlyList<InstallationBootstrapEntry> RetainedForTest{get{lock(Retained)return Retained.ToArray();}}
    private static readonly HashSet<InstallationBootstrapEntry> Retained=[];
    private InstallationBootstrapEntry(string root){Root=root;}
    internal static InstallationBootstrapEntry Open(string directory,string data,Action<InstallationAdmission>? closeForTest=null)
    {
        directory=InstallationPaths.Root(directory);data=InstallationPaths.Root(data);string? parent=Path.GetDirectoryName(directory),root=parent==null?null:Path.GetDirectoryName(parent);
        if(root==null||Path.GetFileName(parent)!="bootstrap-bundles"||!Guid.TryParseExact(Path.GetFileName(directory),"N",out var id)||id==Guid.Empty||directory!=InstallationBootstrapDeployment.BundlePath(root,id))throw new InstallationRecoveryException("Owned immutable bootstrap layout required.");
        var entry=new InstallationBootstrapEntry(root){closeForTest=closeForTest};List<Exception> errors=[];
        try
        {
            var store=new InstallationStore(root,data,()=>false);entry.admission=store.EnterAdmission(existing:true);var selected=store.ReadActive()??throw new InstallationRecoveryException("No selected generation.");
            byte[] owner=InstallationPackage.ReadDocument(InstallationBootstrapDeployment.OwnerPath(root),InstallationBootstrapDeployment.OwnerLimit),active=InstallationPackage.ReadDocument(InstallationBootstrapDeployment.ActivePath(root),InstallationBootstrapDeployment.ReceiptLimit);
            entry.Binding=new(Convert.ToBase64String(owner),Convert.ToBase64String(active));
            foreach(string file in InstallationBootstrapPackage.Files.Append(InstallationPackage.CatalogName))
            {string path=Path.Combine(directory,file);InstallationPaths.Check(path);entry.files.Add(new(path,FileMode.Open,FileAccess.Read,FileShare.Read));}
            InstallationBootstrapDeployment.RequireBinding(root,data,selected,entry.Binding,CancellationToken.None);
            if(InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.Receipt>(active).Bundle!=id)throw new InstallationRecoveryException("Stale bootstrap entry rejected.");
        }
        catch(Exception error){errors.Add(error);}
        try{entry.CloseAdmission();}
        catch(Exception error)
        {
            errors.Add(error);
            try{if(!InstallationBootstrapDeployment.Exists(InstallationBootstrapDeployment.PendingPath(root)))KuaiYi.Core.Settings.SettingsStore.WriteFlushed(InstallationBootstrapDeployment.PendingPath(root),System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new{SchemaVersion=1,Product="KuaiYi",Phase="OriginalEntryCleanupUnconfirmed"}));}catch(Exception marker){errors.Add(marker);}
        }
        if(errors.Count!=0){entry.CloseFiles(errors);entry.RetainIfNeeded();InstallationControllerBridge.Throw(errors);}
        return entry;
    }
    private void CloseAdmission()
    {
        if(admission==null)return;
        if(closeForTest!=null)closeForTest(admission);else admission.Dispose();admission=null;
    }
    private void CloseFiles(List<Exception> errors)
    {
        List<FileStream> unsettled=[];
        foreach(var file in files)try{file.Dispose();}catch(Exception error){errors.Add(error);unsettled.Add(file);}
        files.Clear();files.AddRange(unsettled);
    }
    private void RetainIfNeeded(){lock(Retained){if(admission==null&&files.Count==0)Retained.Remove(this);else Retained.Add(this);}}
    public void Dispose()
    {
        List<Exception> errors=[];try{CloseAdmission();}catch(Exception error){errors.Add(error);}CloseFiles(errors);RetainIfNeeded();InstallationControllerBridge.Throw(errors);
    }
}
