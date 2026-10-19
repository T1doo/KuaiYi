using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using KuaiYi.App;
using KuaiYi.Core.Installation;

// Preparation only: no execution, registration, environment changes, cleanup or acceptance claims.
internal static class IsolatedSessionPackage
{
    internal sealed record Prepared(string Workspace,InstalledVersion Version,int DeclaredDependencyFiles);
    internal static string CreateBuildWorkspace(string outputParent,string productionData)
    {
        outputParent=IsolatedSessionLayout.LocalRoot(outputParent);IsolatedSessionLayout.RequireSeparate(outputParent,productionData);
        string work=Path.Combine(outputParent,"KuaiYi-SessionBuild-"+Guid.NewGuid().ToString("N"));
        if(Directory.Exists(work)||File.Exists(work))throw new IOException("Build workspace exists; retained.");Directory.CreateDirectory(work);return work;
    }
    internal static async Task<Prepared> PrepareAsync(string publish,string outputParent,string productionData)
    {
        publish=IsolatedSessionLayout.LocalRoot(publish);outputParent=IsolatedSessionLayout.LocalRoot(outputParent);
        // Privacy checks precede enumeration, metadata reads and any new workspace creation.
        IsolatedSessionLayout.RequireSeparate(publish,productionData);IsolatedSessionLayout.RequireSeparate(publish,outputParent);
        var files=Files(publish);int declared=RequireClosure(publish);
        Guid run=Guid.NewGuid();string root=Path.Combine(outputParent,IsolatedSessionLayout.Prefix+run.ToString("N"));
        IsolatedSessionLayout.RequireSeparate(root,productionData);
        if(Directory.Exists(root)||File.Exists(root))throw new IOException("Session test workspace already exists; retained.");
        Directory.CreateDirectory(root);string package=Path.Combine(root,"package"),data=Path.Combine(root,"user-data"),application=Path.Combine(root,"application");Directory.CreateDirectory(package);Directory.CreateDirectory(data);
        // Any interruption retains this exact workspace. A later attempt always uses a new run id.
        byte[] descriptor=IsolatedSessionLayout.Descriptor(run);WriteNew(Path.Combine(root,IsolatedSessionLayout.Marker),descriptor);
        foreach(string source in files)
        {
            IsolatedSessionLayout.Check(source);string target=Path.Combine(package,Path.GetRelativePath(publish,source));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(source,target,false);
        }
        WriteNew(Path.Combine(package,IsolatedSessionLayout.Marker),descriptor);
        if(RequireClosure(package)!=declared)throw new InvalidDataException("Copied dependency declarations changed.");
        var entries=Files(package,true).Order(StringComparer.Ordinal).Select(p=>new{path=Path.GetRelativePath(package,p).Replace('\\','/'),bytes=new FileInfo(p).Length,sha256=Hash(p)}).ToArray();
        string catalog=Path.Combine(package,"package.json");WriteNew(catalog,JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",version="session-test-"+run.ToString("N"),files=entries}));
        var store=new InstallationStore(application,data,()=>true);var selected=await store.ApplyAsync(package,Hash(catalog));
        if(IsolatedSessionLayout.Select(selected.DirectoryPath,productionData)!=data)throw new InvalidDataException("Prepared session layout mismatch.");return new(root,selected,declared);
    }
    internal static int RequireClosure(string publish)
    {
        // Check the actual managed build and x64 apphost, not a caller-provided label or an empty EXE.
        using(var input=File.OpenRead(Path.Combine(publish,"KuaiYi.App.exe")))using(var pe=new PEReader(input))
            if(pe.PEHeaders.CoffHeader.Machine!=Machine.Amd64)throw new InvalidDataException("Session test requires a Windows x64 apphost.");
        using(var input=File.OpenRead(Path.Combine(publish,"KuaiYi.App.dll")))using(var pe=new PEReader(input))
        {
            var metadata=pe.GetMetadataReader();
            if(!metadata.TypeDefinitions.Any(h=>{var t=metadata.GetTypeDefinition(h);return metadata.GetString(t.Namespace)=="KuaiYi.App"&&metadata.GetString(t.Name)=="IsolatedSessionLayout";}))throw new InvalidDataException("Production App is not a session-test build.");
        }
        string config=Path.Combine(publish,"KuaiYi.App.runtimeconfig.json");IsolatedSessionLayout.Check(config);
        using(var input=new FileStream(config,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(input.Length is <1 or >16384)throw new InvalidDataException("Session runtime configuration size invalid.");
            using var doc=JsonDocument.Parse(input);var options=doc.RootElement.GetProperty("runtimeOptions");
            if(options.TryGetProperty("framework",out _)||options.TryGetProperty("frameworks",out _)||!options.TryGetProperty("includedFrameworks",out var frameworks)||frameworks.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("Session test requires self-contained output; framework-dependent output refused.");
            var required=new HashSet<string>(StringComparer.Ordinal){"Microsoft.NETCore.App","Microsoft.WindowsDesktop.App"};
            foreach(var framework in frameworks.EnumerateArray())
                if(framework.GetProperty("version").GetString()!="10.0.13"||!required.Remove(framework.GetProperty("name").GetString()!))throw new InvalidDataException("Unexpected or unpatched session runtime framework.");
            if(required.Count!=0)throw new InvalidDataException("Session runtime frameworks incomplete.");
        }
        foreach(string name in new[]{"hostfxr.dll","hostpolicy.dll","coreclr.dll","System.Private.CoreLib.dll","PresentationFramework.dll","PresentationCore.dll","wpfgfx_cor3.dll","KuaiYi.Core.dll","KuaiYi.Providers.dll","KuaiYi.Imaging.dll","KuaiYi.Windows.dll","KuaiYi.App.deps.json"})
        {
            string file=Path.Combine(publish,name);IsolatedSessionLayout.Check(file);if(!File.Exists(file)||new FileInfo(file).Length==0)throw new InvalidDataException("Session runtime closure incomplete.");
        }
        return RequireDeclaredDependencies(publish);
    }
    internal static int RequireDeclaredDependencies(string publish)
    {
        string path=Path.Combine(publish,"KuaiYi.App.deps.json");IsolatedSessionLayout.Check(path);
        using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(input.Length is <1 or >4*1024*1024)throw new InvalidDataException("Dependency declarations size invalid.");
        using var doc=JsonDocument.Parse(input,new(){MaxDepth=32});var root=doc.RootElement;string targetName=root.GetProperty("runtimeTarget").GetProperty("name").GetString()??throw new InvalidDataException("Missing dependency target.");
        if(!targetName.EndsWith("/win-x64",StringComparison.Ordinal))throw new InvalidDataException("Unexpected dependency target.");
        var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int visited=0;
        foreach(var library in root.GetProperty("targets").GetProperty(targetName).EnumerateObject())
        foreach(string section in new[]{"runtime","native","resources","runtimeTargets"})
        {
            if(!library.Value.TryGetProperty(section,out var assets))continue;
            foreach(var asset in assets.EnumerateObject())
            {
                if(++visited>8192)throw new InvalidDataException("Too many dependency declarations.");
                if(section=="runtimeTargets"&&asset.Value.GetProperty("rid").GetString() is not ("win-x64" or "win"))continue;
                string key=asset.Name.Replace('\\','/');string[] parts=key.Split('/');
                if(parts.Any(p=>p.Length==0||p is "." or ".."||p.Contains(':')))throw new InvalidDataException("Unsafe dependency declaration.");
                string name=parts[^1];if(name=="_._")continue;
                if(section=="resources")
                {
                    string locale=asset.Value.GetProperty("locale").GetString()??throw new InvalidDataException("Missing resource locale.");
                    if(locale.Length is <1 or >32||locale.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new InvalidDataException("Unsafe resource locale.");name=Path.Combine(locale,name);
                }
                string file=Path.Combine(publish,name);IsolatedSessionLayout.Check(file);
                if(!File.Exists(file)||new FileInfo(file).Length==0)throw new InvalidDataException("Declared dependency file missing.");files.Add(name);
            }
        }
        if(files.Count==0)throw new InvalidDataException("Dependency declarations are empty.");return files.Count;
    }
    private static string[] Files(string root,bool withDescriptor=false)
    {
        var result=new List<string>();var pending=new Stack<string>();pending.Push(root);long bytes=0;int visited=0;
        while(pending.TryPop(out var directory))foreach(string path in Directory.EnumerateFileSystemEntries(directory))
        {
            if(++visited>8192)throw new InvalidDataException("Session publish tree too large.");IsolatedSessionLayout.Check(path);
            if(Directory.Exists(path)){pending.Push(path);continue;}
            long size=new FileInfo(path).Length;bytes+=size;if(size>256L*1024*1024||bytes>1024L*1024*1024||result.Count>=4094)throw new InvalidDataException("Session publish files too large.");
            string relative=Path.GetRelativePath(root,path).Replace('\\','/');
            if(relative=="package.json"||!withDescriptor&&relative==IsolatedSessionLayout.Marker)throw new InvalidDataException("Publish input already contains session metadata.");result.Add(path);
        }
        return result.ToArray();
    }
    private static string Hash(string path){using var input=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(input));}
    private static void WriteNew(string path,byte[] bytes){using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);output.Write(bytes);output.Flush(true);}
}
