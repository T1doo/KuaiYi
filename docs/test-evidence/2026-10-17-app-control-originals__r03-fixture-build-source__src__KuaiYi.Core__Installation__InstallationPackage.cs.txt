using System.Security.Cryptography;
using System.Text.Json;
using KuaiYi.Core.Settings;
namespace KuaiYi.Core.Installation;

public sealed record InstallationFile(string Path,long Bytes,string Sha256);
public sealed record InstallationManifest(string Version,IReadOnlyList<InstallationFile> Files);
// Integrity of caller-supplied package bytes is not a signature or permission to register/install them.
internal sealed record InstallationPackage(InstallationManifest Manifest,byte[] Catalog,string CatalogSha256)
{
    internal const string CatalogName="package.json";
    private const int CatalogLimit=1024*1024,FileLimit=4096;
    private const long MaximumFileBytes=256L*1024*1024,MaximumTotalBytes=1024L*1024*1024;
    internal static InstallationPackage Read(string directory,CancellationToken cancellation)
    {
        InstallationPaths.Check(directory);byte[] bytes=ReadDocument(Path.Combine(directory,CatalogName),CatalogLimit);var manifest=ReadManifest(bytes);
        var package=new InstallationPackage(manifest,bytes,Convert.ToHexString(SHA256.HashData(bytes)));package.Verify(directory,cancellation);return package;
    }
    private static InstallationManifest ReadManifest(byte[] bytes)
    {
        try
        {
            using var doc=JsonDocument.Parse(bytes,new(){MaxDepth=8});var root=doc.RootElement;
            Fields(root,"schemaVersion","product","version","files");
            if(root.GetProperty("schemaVersion").GetInt32()!=1||root.GetProperty("product").GetString()!="KuaiYi"||root.GetProperty("version").GetString() is not {Length:>0 and <=80} version||version.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not ('.' or '-' or '_')))throw new InvalidDataException("Invalid installation package identity.");
            var entries=root.GetProperty("files");if(entries.ValueKind!=JsonValueKind.Array||entries.GetArrayLength() is <1 or >FileLimit)throw new InvalidDataException("Invalid installation package size.");
            var files=new List<InstallationFile>();var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
            foreach(var item in entries.EnumerateArray())
            {
                Fields(item,"path","bytes","sha256");string path=item.GetProperty("path").GetString()??"";ValidateRelative(path);long count=item.GetProperty("bytes").GetInt64();string hash=item.GetProperty("sha256").GetString()??"";
                if(!paths.Add(path)||count<0||count>MaximumFileBytes||count>MaximumTotalBytes-total||!HashValid(hash))throw new InvalidDataException("Invalid installation file entry.");total+=count;files.Add(new(path,count,hash));
            }
            return new(version,files.AsReadOnly());
        }
        catch(Exception error)when(error is InvalidOperationException or FormatException or OverflowException)
        {throw new InvalidDataException("Invalid installation catalog field type or number.",error);}
    }
    internal void Verify(string directory,CancellationToken cancellation)
    {
        InstallationPaths.Check(directory);var expected=Manifest.Files.ToDictionary(f=>f.Path,StringComparer.OrdinalIgnoreCase);expected.Add(CatalogName,new(CatalogName,Catalog.Length,CatalogSha256));
        var directories=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string path in expected.Keys){string? parent=Path.GetDirectoryName(path);while(!string.IsNullOrEmpty(parent)){directories.Add(parent.Replace('\\','/'));parent=Path.GetDirectoryName(parent);}}
        var seenDirectories=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var pending=new Stack<string>();pending.Push(directory);var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int visited=0;
        while(pending.TryPop(out string? current))foreach(string path in Directory.EnumerateFileSystemEntries(current))
        {
            cancellation.ThrowIfCancellationRequested();if(++visited>FileLimit*17)throw new InvalidDataException("Installation tree too large.");InstallationPaths.Check(path);string relative=Path.GetRelativePath(directory,path).Replace('\\','/');var attributes=File.GetAttributes(path);
            if((attributes&FileAttributes.Directory)!=0){if(!directories.Contains(relative)||!seenDirectories.Add(relative))throw new InvalidDataException("Unlisted or aliased installation directory.");pending.Push(path);continue;}
            if((attributes&FileAttributes.Device)!=0||!expected.TryGetValue(relative,out var entry)||!seen.Add(relative))throw new InvalidDataException("Unlisted installation file.");
            using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);VerifyStream(input,entry,cancellation);
        }
        if(seen.Count!=expected.Count)throw new InvalidDataException("Installation package is incomplete.");
    }
    internal void CopyTo(string source,string target,CancellationToken cancellation)
    {
        SettingsStore.WriteFlushed(Path.Combine(target,CatalogName),Catalog);
        foreach(var entry in Manifest.Files)
        {
            cancellation.ThrowIfCancellationRequested();string from=Path.Combine(source,entry.Path),to=Path.Combine(target,entry.Path);InstallationPaths.Check(from);Directory.CreateDirectory(Path.GetDirectoryName(to)!);InstallationPaths.Check(Path.GetDirectoryName(to)!);
            using var input=new FileStream(from,FileMode.Open,FileAccess.Read,FileShare.Read);using var output=new FileStream(to,FileMode.CreateNew,FileAccess.Write,FileShare.None);
            if(input.Length!=entry.Bytes)throw new InvalidDataException("Package file changed.");using var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] buffer=new byte[65536];long total=0;int count;
            while((count=input.Read(buffer))!=0){cancellation.ThrowIfCancellationRequested();total+=count;if(total>entry.Bytes)throw new InvalidDataException("Package file changed.");digest.AppendData(buffer,0,count);output.Write(buffer,0,count);}
            if(total!=entry.Bytes||!string.Equals(Convert.ToHexString(digest.GetHashAndReset()),entry.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Package file digest changed.");output.Flush(true);InstallationPaths.Check(from);
        }
        Verify(target,cancellation);
    }
    private static void VerifyStream(FileStream input,InstallationFile file,CancellationToken cancellation)
    {
        if(input.Length!=file.Bytes)throw new InvalidDataException("Installation file length mismatch.");using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] buffer=new byte[65536];long read=0;int count;
        while((count=input.Read(buffer))!=0){cancellation.ThrowIfCancellationRequested();read+=count;if(read>file.Bytes)throw new InvalidDataException("Installation file changed.");hash.AppendData(buffer,0,count);}
        if(read!=file.Bytes||!string.Equals(Convert.ToHexString(hash.GetHashAndReset()),file.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Installation file hash mismatch.");
    }
    internal static byte[] ReadDocument(string path,int maximum=CatalogLimit)
    {
        InstallationPaths.Check(path);using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);if(input.Length is <1||input.Length>maximum)throw new InvalidDataException("Installation metadata size invalid.");byte[] bytes=new byte[(int)input.Length];input.ReadExactly(bytes);return bytes;
    }
    internal static bool HashValid(string hash)=>hash.Length==64&&hash.All(char.IsAsciiHexDigit);
    internal static void Fields(JsonElement item,params string[] fields)
    {
        if(item.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Installation metadata object required.");var seen=new HashSet<string>(StringComparer.Ordinal);foreach(var field in item.EnumerateObject())if(!fields.Contains(field.Name,StringComparer.Ordinal)||!seen.Add(field.Name))throw new InvalidDataException("Unexpected or duplicate installation field.");if(seen.Count!=fields.Length)throw new InvalidDataException("Missing installation field.");
    }
    private static void ValidateRelative(string path)
    {
        if(path.Length is <1 or >240||path.Equals(CatalogName,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid installation path.");string[] parts=path.Split('/');if(parts.Length>16)throw new InvalidDataException("Installation path too deep.");
        foreach(string part in parts)
        {
            if(part.Length==0||part is "." or ".."||part.EndsWith('.')||part.EndsWith(' ')||part.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not ('.' or '-' or '_' or ' ')))throw new InvalidDataException("Unsafe installation path.");
            string stem=part.Split('.')[0].ToUpperInvariant();if(stem is "CON" or "PRN" or "AUX" or "NUL"||stem.Length==4&&(stem.StartsWith("COM",StringComparison.Ordinal)||stem.StartsWith("LPT",StringComparison.Ordinal))&&stem[3] is >= '1' and <= '9')throw new InvalidDataException("Reserved installation path.");
        }
    }
}
internal static class InstallationPaths
{
    internal static string Root(string path)
    {
        if(!Path.IsPathFullyQualified(path)||path.StartsWith("\\\\",StringComparison.Ordinal)||path.StartsWith("//",StringComparison.Ordinal))throw new InvalidDataException("Local absolute installation path required.");string full=Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));if(full==Path.GetPathRoot(full)||OperatingSystem.IsWindows()&&new DriveInfo(Path.GetPathRoot(full)!).DriveType==DriveType.Network)throw new InvalidDataException("Dedicated local installation directory required.");Check(full);return full;
    }
    internal static void Check(string path)
    {for(string? part=Path.GetFullPath(path);part!=null;part=Path.GetDirectoryName(part))try{SettingsStore.RejectLink(part);}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}}
    internal static bool Contains(string parent,string child)=>string.Equals(parent,child,StringComparison.OrdinalIgnoreCase)||child.StartsWith(parent+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
}
