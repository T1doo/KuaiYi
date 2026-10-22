using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
namespace KuaiYi.Core.Installation;

// Two disjoint bounded contracts. A self-contained contract is not a signature or a publish/execution receipt.
internal static class InstallationBootstrapPackage
{
    internal const string Mode="FrameworkDependentDevelopment",SelfContainedMode="SelfContainedWindowsX64_10_0_13";
    private static readonly JsonElement RuntimeContract=ReadRuntimeContract();
    internal static IReadOnlyList<InstallationFile> RuntimeFiles{get;}=Array.AsReadOnly(RuntimeContract.GetProperty("files").EnumerateArray().Select(f=>new InstallationFile(f.GetProperty("path").GetString()!,f.GetProperty("bytes").GetInt64(),f.GetProperty("sha256").GetString()!)).ToArray());
    internal static readonly string[] Files=["KuaiYi.Core.dll","KuaiYi.Launcher.deps.json","KuaiYi.Launcher.dll","KuaiYi.Launcher.exe","KuaiYi.Launcher.runtimeconfig.json"];
    internal static IReadOnlyList<string> FilesForMode(string mode)=>mode switch
    {
        Mode=>Files,
        SelfContainedMode=>Array.AsReadOnly(Files.Concat(RuntimeFiles.Select(f=>f.Path)).Order(StringComparer.Ordinal).ToArray()),
        _=>throw new InvalidDataException("Unknown bootstrap mode.")
    };
    internal static string ModeOf(InstallationPackage package)
    {
        string[] actual=package.Manifest.Files.Select(f=>f.Path).Order(StringComparer.Ordinal).ToArray();
        if(actual.SequenceEqual(Files,StringComparer.Ordinal))return Mode;
        if(actual.SequenceEqual(FilesForMode(SelfContainedMode),StringComparer.Ordinal))return SelfContainedMode;
        throw new InvalidDataException("Exact known bootstrap closure required; no mode fallback.");
    }
    private static JsonElement ReadRuntimeContract()
    {
        using var stream=typeof(InstallationBootstrapPackage).Assembly.GetManifestResourceStream("KuaiYi.BootstrapRuntime")??throw new InvalidDataException("Missing built-in bootstrap runtime contract.");
        using var doc=JsonDocument.Parse(stream,new(){MaxDepth=16});return doc.RootElement.Clone();
    }
    internal static void RequireRuntimeEntries(InstallationPackage package)
    {
        if(ModeOf(package)!=SelfContainedMode)throw new InvalidDataException("Self-contained bootstrap required.");
        var entries=package.Manifest.Files.ToDictionary(f=>f.Path,StringComparer.Ordinal);
        foreach(var expected in RuntimeFiles)
            if(!entries.TryGetValue(expected.Path,out var actual)||actual.Bytes!=expected.Bytes||!string.Equals(actual.Sha256,expected.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Bootstrap runtime bytes differ from fixed approved input.");
    }
    internal static (InstallationPackage Package,string Revision) Read(string directory,string digest,InstalledVersion generation,CancellationToken cancellation,InstallationPreparationIO? io=null)
    {
        var package=InstallationPackage.ReadSource(directory,digest,cancellation,io);
        string mode=ModeOf(package);long totalLimit=mode==Mode?64L*1024*1024:128L*1024*1024;
        if(package.Schema!=1||package.Manifest.Files.Any(f=>f.Bytes is <=0 or >32*1024*1024)||package.Manifest.Files.Sum(f=>f.Bytes)>totalLimit)throw new InvalidDataException("Exact bounded bootstrap files required.");
        if(mode==SelfContainedMode)RequireRuntimeEntries(package);
        string core=Path.Combine(directory,"KuaiYi.Core.dll"),launcher=Path.Combine(directory,"KuaiYi.Launcher.dll");string revision=Managed(core,"KuaiYi.Core",io);
        if(Managed(launcher,"KuaiYi.Launcher",io)!=revision||Hash(core,io)!=Hash(Path.Combine(generation.DirectoryPath,"KuaiYi.Core.dll"),io))throw new InvalidDataException("Bootstrap and generation Core/build differ.");
        string apphost=Path.Combine(directory,"KuaiYi.Launcher.exe");InspectPe(apphost,io,pe=>
        {if(pe.HasMetadata||pe.PEHeaders.CoffHeader.Machine!=Machine.Amd64||pe.PEHeaders.PEHeader?.Magic!=PEMagic.PE32Plus)throw new InvalidDataException("Windows x64 launcher apphost required.");});
        if(io!=null?!io.ContainsPayload(apphost,"KuaiYi.Launcher.dll"u8):File.ReadAllBytes(apphost).AsSpan().IndexOf("KuaiYi.Launcher.dll"u8)<0)throw new InvalidDataException("Launcher apphost binding differs.");
        RequireConfiguration(directory,mode,io);return(package,revision);
    }
    internal static string Hash(string path)=>Hash(path,null);
    internal static string Hash(string path,InstallationPreparationIO? io){if(io!=null)return io.HashFile(path);InstallationPaths.Check(path);using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);return Convert.ToHexString(SHA256.HashData(input));}
    private static void InspectPe(string path,InstallationPreparationIO? io,Action<PEReader> inspect)
    {if(io!=null){io.ReadPe(path,inspect);return;}using var input=File.OpenRead(path);using var pe=new PEReader(input);inspect(pe);}
    private static string Managed(string path,string identity,InstallationPreparationIO? io)
    {
        string? revision=null;InspectPe(path,io,pe=>revision=ManagedRevision(pe,identity));return revision!;
    }
    private static string ManagedRevision(PEReader pe,string identity)
    {
        var reader=pe.GetMetadataReader();var definition=reader.GetAssemblyDefinition();
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
    internal static void RequireConfiguration(string directory,string mode,InstallationPreparationIO? io=null)
    {
        if(mode==Mode){ExactJson(directory,"KuaiYi.Launcher.runtimeconfig.json",Runtime,io);ExactJson(directory,"KuaiYi.Launcher.deps.json",Dependencies,io);return;}
        if(mode!=SelfContainedMode)throw new InvalidDataException("Unknown bootstrap mode.");
        ExactJson(directory,"KuaiYi.Launcher.runtimeconfig.json",SelfContainedRuntime,io);
        ExactJson(directory,"KuaiYi.Launcher.deps.json",RuntimeContract.GetProperty("dependencies").GetRawText(),io);
    }
    private static void ExactJson(string directory,string name,string expected,InstallationPreparationIO? io)
    {
        using var actual=JsonDocument.Parse(InstallationPackage.ReadDocument(Path.Combine(directory,name),65536,io),new(){MaxDepth=16});using var contract=JsonDocument.Parse(expected);
        Unique(actual.RootElement);if(!JsonElement.DeepEquals(actual.RootElement,contract.RootElement))throw new InvalidDataException("Bootstrap runtime/dependency contract differs.");
        static void Unique(JsonElement value)
        {if(value.ValueKind==JsonValueKind.Object){HashSet<string> fields=[];foreach(var field in value.EnumerateObject()){if(!fields.Add(field.Name))throw new InvalidDataException("Duplicate bootstrap dependency field.");Unique(field.Value);}}else if(value.ValueKind==JsonValueKind.Array)foreach(var item in value.EnumerateArray())Unique(item);}
    }
    internal const string SelfContainedRuntime="""
    {"runtimeOptions":{"tfm":"net10.0","includedFrameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.13"}],"configProperties":{"System.Reflection.Metadata.MetadataUpdater.IsSupported":false,"System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization":false}}}
    """;
    private const string Runtime="""
    {"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"},"configProperties":{"System.Reflection.Metadata.MetadataUpdater.IsSupported":false,"System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization":false}}}
    """;
    private const string Dependencies="""
    {"runtimeTarget":{"name":".NETCoreApp,Version=v10.0/win-x64","signature":""},"compilationOptions":{},"targets":{".NETCoreApp,Version=v10.0":{},".NETCoreApp,Version=v10.0/win-x64":{"KuaiYi.Launcher/1.0.0":{"dependencies":{"KuaiYi.Core":"1.0.0"},"runtime":{"KuaiYi.Launcher.dll":{}}},"KuaiYi.Core/1.0.0":{"runtime":{"KuaiYi.Core.dll":{"assemblyVersion":"1.0.0.0","fileVersion":"1.0.0.0"}}}}},"libraries":{"KuaiYi.Launcher/1.0.0":{"type":"project","serviceable":false,"sha512":""},"KuaiYi.Core/1.0.0":{"type":"project","serviceable":false,"sha512":""}}}
    """;
}
