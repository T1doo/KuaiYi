using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using KuaiYi.Core.Installation;
namespace KuaiYi.Core.Tests;

// Source-package fixture only. Real normal managed role assemblies/apphosts and their deps are copied unchanged.
// Unprovided runtime/native/third-party dependency bytes are neutral structural PE fixtures: never execute or call this a runnable Windows bundle.
internal sealed class MaterializedPayloadSource
{
    internal readonly string Root;
    internal string Version;
    internal static string Repo{get{var p=new DirectoryInfo(AppContext.BaseDirectory);while(p!=null&&!File.Exists(Path.Combine(p.FullName,"SPEC.md")))p=p.Parent;return p!.FullName;}}
    internal static string Revision=>typeof(InstallationStore).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[^1];
    internal static readonly string Origin="chrome-extension://"+new string('a',32)+"/"; // Explicit synthetic source input only.
    internal MaterializedPayloadSource(string root,string version="1.0")
    {
        Root=root;Version=version;Directory.CreateDirectory(root);
        foreach(string name in InstallationPayload.ManagedFiles)File.Copy(Product(name),Path.Combine(root,name));
        foreach(string role in InstallationPayload.Roles)
        {
            File.Copy(Product(role+".exe"),Path.Combine(root,role+".exe"));File.Copy(Product(role+".deps.json"),Path.Combine(root,role+".deps.json"));
            var frameworks=new List<object>{new{name="Microsoft.NETCore.App",version="10.0.13"}};if(role is "KuaiYi.App" or "KuaiYi.CaptureBroker" or "KuaiYi.OleReceiver")frameworks.Add(new{name="Microsoft.WindowsDesktop.App",version="10.0.13"});
            Put(role+".runtimeconfig.json",JsonSerializer.SerializeToUtf8Bytes(new{runtimeOptions=new{tfm="net10.0",includedFrameworks=frameworks}}));
        }
        foreach(string name in InstallationPayload.TrustedFiles)if(!File.Exists(Path.Combine(root,name)))Put(name,NativeFixture());
        foreach(string name in InstallationPayload.ManagedFiles)
        {
            using var stream=File.OpenRead(Path.Combine(root,name));using var pe=new PEReader(stream);var reader=pe.GetMetadataReader();
            foreach(var h in reader.AssemblyReferences){string file=reader.GetString(reader.GetAssemblyReference(h).Name)+".dll";if(!File.Exists(Path.Combine(root,file)))Put(file,NativeFixture());}
        }
        foreach(string role in InstallationPayload.Roles)
        {
            using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,role+".deps.json")));var d=doc.RootElement;string target=d.GetProperty("runtimeTarget").GetProperty("name").GetString()!;
            foreach(var lib in d.GetProperty("targets").GetProperty(target).EnumerateObject())foreach(string section in new[]{"runtime","native","resources","runtimeTargets"})
            {
                if(!lib.Value.TryGetProperty(section,out var assets))continue;
                foreach(var asset in assets.EnumerateObject())
                {
                    if(section=="runtimeTargets"&&asset.Value.GetProperty("rid").GetString() is not("win-x64" or "win"))continue;
                    string name=asset.Name.Replace('\\','/').Split('/')[^1];if(name=="_._")continue;if(section=="resources")name=asset.Value.GetProperty("locale").GetString()+"/"+name;
                    if(!File.Exists(Path.Combine(root,name)))Put(name,NativeFixture());
                }
            }
        }
        foreach(string name in InstallationPayload.ResourceFiles)
        {
            string? source=name.StartsWith("identity/",StringComparison.Ordinal)?Path.Combine(Repo,"packaging",name):name.StartsWith("browser-extension/",StringComparison.Ordinal)?Path.Combine(Repo,"extensions/chromium",name[18..]):name.StartsWith("licenses/SkiaSharp",StringComparison.Ordinal)?Path.Combine(Repo,name):null;
            Put(name,source==null?"PUBLIC_RUNTIME_NOTICE_FIXTURE_ONLY"u8.ToArray():File.ReadAllBytes(source));
        }
        Catalog();
    }
    internal void Put(string name,byte[] bytes){string path=Path.Combine(Root,name);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path,bytes);}
    internal void Catalog()
    {
        var files=Directory.GetFiles(Root,"*",SearchOption.AllDirectories).Where(p=>p!=Path.Combine(Root,"package.json")).Order(StringComparer.Ordinal).Select(p=>new{path=Path.GetRelativePath(Root,p).Replace('\\','/'),bytes=new FileInfo(p).Length,sha256=Hash(p)}).ToArray();
        Put("package.json",JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=2,product="KuaiYi",version=Version,files,payload=new{kind=InstallationPayloadDefinition.Kind,runtimeVersion="10.0.13",productSourceRevision=Revision,origins=new[]{Origin}}}));
    }
    internal void Rewrite(Action<JsonObject> change)
    {var doc=JsonNode.Parse(File.ReadAllBytes(Path.Combine(Root,"package.json")))!.AsObject();change(doc);Put("package.json",JsonSerializer.SerializeToUtf8Bytes(doc));}
    internal string Digest=>Hash(Path.Combine(Root,"package.json"));
    internal static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream));}
    private static string Product(string name)
    {
        string project=InstallationPayload.Roles.FirstOrDefault(r=>name.StartsWith(r+".",StringComparison.Ordinal))??Path.GetFileNameWithoutExtension(name);
        string framework=project is "KuaiYi.App" or "KuaiYi.Windows" or "KuaiYi.CaptureBroker" or "KuaiYi.ClipboardHost" or "KuaiYi.CaptureHost" or "KuaiYi.OleReceiver"?"net10.0-windows":"net10.0";
        string folder=Path.Combine(Repo,"src",project,"bin/Release",framework);if(InstallationPayload.Roles.Contains(project))folder=Path.Combine(folder,"win-x64");
        return Path.Combine(folder,name);
    }
    internal static byte[] NativeFixture()
    {
        byte[] bytes=new byte[512];BinaryPrimitives.WriteUInt16LittleEndian(bytes,0x5a4d);BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x3c),0x80);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x80),0x4550);BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x84),0x8664);BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x94),240);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x96),0x2022);BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x98),0x20b);BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x98+108),16);return bytes;
    }
}
