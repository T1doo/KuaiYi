using System.IO;
using System.Text.Json;
using KuaiYi.Core.Installation;
namespace KuaiYi.App;

// Compiled into the explicit session-test build only. No environment override or arbitrary data path.
internal static class IsolatedSessionLayout
{
    internal const string Marker="session-test.json",Prefix="KuaiYi-SessionTest-";
    internal static bool AllowsEntry(string[] args)=>args.Length==0||args.Length==2&&args[0]=="--installation-handshake"&&Guid.TryParseExact(args[1],"N",out var id)&&id!=Guid.Empty;
    internal static byte[] Descriptor(Guid run)=>JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,kind="KuaiYiIsolatedSessionTest",runId=run.ToString("N")});
    internal static string Select(string executableDirectory,string productionData)
    {
        string directory=LocalRoot(executableDirectory);
        string installation=InstallationNormalEntry.FindInstallationRoot(directory)??throw new InvalidDataException("Session test requires a selected installation layout.");
        if(Path.GetFileName(installation)!="application")throw new InvalidDataException("Session test installation directory mismatch.");
        string workspace=LocalRoot(Path.GetDirectoryName(installation)!);RequireSeparate(workspace,productionData);
        byte[] marker=Read(Path.Combine(workspace,Marker));Guid run=RunId(marker);
        if(Path.GetFileName(workspace)!=Prefix+run.ToString("N")||!Read(Path.Combine(directory,Marker)).AsSpan().SequenceEqual(marker))throw new InvalidDataException("Session test descriptor mismatch.");
        string data=LocalRoot(Path.Combine(workspace,"user-data"));
        if(!Directory.Exists(data))throw new InvalidDataException("Session test data directory is missing; retained without repair.");
        Check(Path.Combine(data,"config"));Check(Path.Combine(data,"secrets"));return data;
    }
    internal static void RequireSeparate(string workspace,string productionData)
    {
        workspace=LocalRoot(workspace);productionData=LocalRoot(productionData);
        if(Contains(workspace,productionData)||Contains(productionData,workspace))throw new InvalidDataException("Session test overlaps production data.");
    }
    internal static string LocalRoot(string path)
    {
        if(!Path.IsPathFullyQualified(path)||path.StartsWith("\\\\",StringComparison.Ordinal)||path.StartsWith("//",StringComparison.Ordinal))throw new InvalidDataException("Dedicated local absolute session test path required.");
        string full=Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if(full==Path.GetPathRoot(full)||OperatingSystem.IsWindows()&&new DriveInfo(Path.GetPathRoot(full)!).DriveType==DriveType.Network)throw new InvalidDataException("Dedicated local session test directory required.");
        foreach(string part in Path.GetRelativePath(Path.GetPathRoot(full)!,full).Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar))
            if(part.EndsWith('.')||part.EndsWith(' ')||part.Contains('~')||part.Contains(':'))throw new InvalidDataException("Ambiguous session test path refused.");
        Check(full);return full;
    }
    internal static void Check(string path)
    {
        for(string? part=Path.GetFullPath(path);part!=null;part=Path.GetDirectoryName(part))
            try{if((File.GetAttributes(part)&FileAttributes.ReparsePoint)!=0)throw new IOException("Session test paths cannot contain reparse points.");}
            catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
    }
    private static bool Contains(string parent,string child)=>string.Equals(parent,child,StringComparison.OrdinalIgnoreCase)||child.StartsWith(parent+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    private static byte[] Read(string path)
    {
        Check(path);using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);
        if(input.Length is <1 or >512)throw new InvalidDataException("Session test descriptor size invalid.");byte[] bytes=new byte[(int)input.Length];input.ReadExactly(bytes);return bytes;
    }
    private static Guid RunId(byte[] bytes)
    {
        try
        {
            using var doc=JsonDocument.Parse(bytes,new(){MaxDepth=4});var root=doc.RootElement;
            if(root.ValueKind!=JsonValueKind.Object||root.GetProperty("schemaVersion").GetInt32()!=1||root.GetProperty("kind").GetString()!="KuaiYiIsolatedSessionTest"||!Guid.TryParseExact(root.GetProperty("runId").GetString(),"N",out var run)||run==Guid.Empty||!bytes.AsSpan().SequenceEqual(Descriptor(run)))throw new InvalidDataException("Session test descriptor invalid.");
            return run;
        }
        catch(Exception error)when(error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException){throw new InvalidDataException("Session test descriptor invalid.",error);}
    }
}
