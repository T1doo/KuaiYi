namespace KuaiYi.Core.Installation;
// File effects only. Production supplies the original live withdrawal capability, never a receipt/path based owner.
internal sealed class InstallationMaintenanceFiles(InstallationPreparationIO io,Action requireOriginal,Action? commitBeforeMove=null)
{
    internal static InstallationFile[] Capture(InstallationPreparationIO io,string directory,IReadOnlyList<InstallationFile> allowed,bool allowMissing,IReadOnlyList<InstallationFile>? directoryShape=null)
    {
        io.RequireDirectory(directory);var expected=allowed.ToDictionary(f=>f.Path,StringComparer.Ordinal);var parents=new HashSet<string>(StringComparer.Ordinal);
        foreach(var file in directoryShape??allowed){string? parent=Path.GetDirectoryName(file.Path);while(!string.IsNullOrEmpty(parent)){parents.Add(parent.Replace('\\','/'));parent=Path.GetDirectoryName(parent);}}
        List<InstallationFile> found=[];var directories=new Stack<string>();directories.Push(directory);int visited=0;long total=0;
        while(directories.TryPop(out var current))foreach(string path in io.Entries(current))
        {
            if(++visited>8192)throw new InstallationRecoveryException("Maintenance directory bound exceeded.");InstallationPaths.Check(path);string relative=Path.GetRelativePath(directory,path).Replace('\\','/');
            if(Directory.Exists(path)){if(!parents.Contains(relative))throw new InstallationRecoveryException("Unowned directory retained.");directories.Push(path);continue;}
            if(!expected.ContainsKey(relative))throw new InstallationRecoveryException("Unowned file retained.");
            var file=io.DescribeMaintenanceFile(path,relative);total=checked(total+file.Bytes);if(total>1024L*1024*1024)throw new InstallationRecoveryException("Maintenance closure exceeds package bound.");found.Add(file);
        }
        if(!allowMissing&&found.Count!=allowed.Count)throw new InstallationRecoveryException("Owned file set is incomplete.");
        if(!allowMissing&&found.Any(f=>!Equal(f,expected[f.Path])))throw new InstallationRecoveryException("Owned file bytes changed.");
        return found.OrderBy(f=>f.Path,StringComparer.Ordinal).ToArray();
    }
    private static bool Equal(InstallationFile a,InstallationFile b)=>a.Path==b.Path&&a.Bytes==b.Bytes&&string.Equals(a.Sha256,b.Sha256,StringComparison.OrdinalIgnoreCase);
    internal static void Verify(InstallationPreparationIO io,string directory,IReadOnlyList<InstallationFile> before,IReadOnlyList<InstallationFile>? directoryShape=null)
    {var actual=Capture(io,directory,before,false,directoryShape);if(actual.Length!=before.Count)throw new InstallationRecoveryException("Original file snapshot changed.");}
    internal void Move(string from,string to,IReadOnlyList<InstallationFile> before,IReadOnlyList<InstallationFile>? directoryShape=null)
    {
        requireOriginal();Verify(io,from,before,directoryShape);io.RequireAbsent(to);
        // An actual Move exception is permanently UNKNOWN in this original ledger, including an after-effect exception.
        requireOriginal();commitBeforeMove?.Invoke();io.MoveMaintenanceDirectory(from,to);Verify(io,to,before,directoryShape);io.RequireAbsent(from);requireOriginal();
    }
}
