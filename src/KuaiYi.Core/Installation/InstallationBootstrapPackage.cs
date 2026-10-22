using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
namespace KuaiYi.Core.Installation;

// Exact framework-dependent development bundle, not the missing Windows self-contained distribution.
internal static class InstallationBootstrapPackage
{
    internal const string Mode="FrameworkDependentDevelopment";
    internal static readonly string[] Files=["KuaiYi.Core.dll","KuaiYi.Launcher.deps.json","KuaiYi.Launcher.dll","KuaiYi.Launcher.exe","KuaiYi.Launcher.runtimeconfig.json"];
    internal static (InstallationPackage Package,string Revision) Read(string directory,string digest,InstalledVersion generation,CancellationToken cancellation)
    {
        var package=InstallationPackage.ReadSource(directory,digest,cancellation);
        if(package.Schema!=1||!package.Manifest.Files.Select(f=>f.Path).Order(StringComparer.Ordinal).SequenceEqual(Files)||package.Manifest.Files.Any(f=>f.Bytes is <=0 or >32*1024*1024)||package.Manifest.Files.Sum(f=>f.Bytes)>64*1024*1024)throw new InvalidDataException("Exact bounded development bootstrap files required.");
        string core=Path.Combine(directory,"KuaiYi.Core.dll"),launcher=Path.Combine(directory,"KuaiYi.Launcher.dll");string revision=Managed(core,"KuaiYi.Core");
        if(Managed(launcher,"KuaiYi.Launcher")!=revision||Hash(core)!=Hash(Path.Combine(generation.DirectoryPath,"KuaiYi.Core.dll")))throw new InvalidDataException("Bootstrap and generation Core/build differ.");
        using(var input=File.OpenRead(Path.Combine(directory,"KuaiYi.Launcher.exe")))using(var pe=new PEReader(input))
            if(pe.HasMetadata||pe.PEHeaders.CoffHeader.Machine!=Machine.Amd64||pe.PEHeaders.PEHeader?.Magic!=PEMagic.PE32Plus)throw new InvalidDataException("Windows x64 launcher apphost required.");
        if(File.ReadAllBytes(Path.Combine(directory,"KuaiYi.Launcher.exe")).AsSpan().IndexOf("KuaiYi.Launcher.dll"u8)<0)throw new InvalidDataException("Launcher apphost binding differs.");
        ExactJson(directory,"KuaiYi.Launcher.runtimeconfig.json",Runtime);ExactJson(directory,"KuaiYi.Launcher.deps.json",Dependencies);return(package,revision);
    }
    internal static string Hash(string path){InstallationPaths.Check(path);using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);return Convert.ToHexString(SHA256.HashData(input));}
    private static string Managed(string path,string identity)
    {
        using var input=File.OpenRead(path);using var pe=new PEReader(input);var reader=pe.GetMetadataReader();var definition=reader.GetAssemblyDefinition();
        if(reader.GetString(definition.Name)!=identity)throw new InvalidDataException("Bootstrap assembly identity differs.");
        foreach(var handle in reader.AssemblyReferences)
        {string name=reader.GetString(reader.GetAssemblyReference(handle).Name);if(!name.StartsWith("System.",StringComparison.Ordinal)&&!(identity=="KuaiYi.Launcher"&&name=="KuaiYi.Core"))throw new InvalidDataException("Unexpected bootstrap assembly dependency.");}
        string? informational=null;
        foreach(var handle in definition.GetCustomAttributes())
        {
            var attribute=reader.GetCustomAttribute(handle);if(attribute.Constructor.Kind!=HandleKind.MemberReference)continue;var constructor=reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);if(constructor.Parent.Kind!=HandleKind.TypeReference)continue;var type=reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
            if(reader.GetString(type.Namespace)!="System.Reflection"||reader.GetString(type.Name)!="AssemblyInformationalVersionAttribute")continue;
            if(informational!=null)throw new InvalidDataException("Duplicate bootstrap build revision.");var blob=reader.GetBlobReader(attribute.Value);if(blob.ReadUInt16()!=1)throw new InvalidDataException("Invalid bootstrap build revision.");informational=blob.ReadSerializedString();
        }
        string revision=informational?.Split('+').Last()??"";if(revision.Length!=40||!revision.All(char.IsAsciiHexDigit))throw new InvalidDataException("Full bootstrap source revision required.");return revision;
    }
    private static void ExactJson(string directory,string name,string expected)
    {
        using var actual=JsonDocument.Parse(InstallationPackage.ReadDocument(Path.Combine(directory,name),65536),new(){MaxDepth=16});using var contract=JsonDocument.Parse(expected);
        Unique(actual.RootElement);if(!JsonElement.DeepEquals(actual.RootElement,contract.RootElement))throw new InvalidDataException("Bootstrap runtime/dependency contract differs.");
        static void Unique(JsonElement value)
        {if(value.ValueKind==JsonValueKind.Object){HashSet<string> fields=[];foreach(var field in value.EnumerateObject()){if(!fields.Add(field.Name))throw new InvalidDataException("Duplicate bootstrap dependency field.");Unique(field.Value);}}else if(value.ValueKind==JsonValueKind.Array)foreach(var item in value.EnumerateArray())Unique(item);}
    }
    private const string Runtime="""
    {"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"},"configProperties":{"System.Reflection.Metadata.MetadataUpdater.IsSupported":false,"System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization":false}}}
    """;
    private const string Dependencies="""
    {"runtimeTarget":{"name":".NETCoreApp,Version=v10.0/win-x64","signature":""},"compilationOptions":{},"targets":{".NETCoreApp,Version=v10.0":{},".NETCoreApp,Version=v10.0/win-x64":{"KuaiYi.Launcher/1.0.0":{"dependencies":{"KuaiYi.Core":"1.0.0"},"runtime":{"KuaiYi.Launcher.dll":{}}},"KuaiYi.Core/1.0.0":{"runtime":{"KuaiYi.Core.dll":{"assemblyVersion":"1.0.0.0","fileVersion":"1.0.0.0"}}}}},"libraries":{"KuaiYi.Launcher/1.0.0":{"type":"project","serviceable":false,"sha512":""},"KuaiYi.Core/1.0.0":{"type":"project","serviceable":false,"sha512":""}}}
    """;
}
