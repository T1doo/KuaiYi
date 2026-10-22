using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using KuaiYi.Core.Settings;
namespace KuaiYi.Core.Installation;

// A pinned source describes bytes to materialize, not a signature, OS identity, registration or execution permission.
internal sealed record InstallationPayloadDefinition(string RuntimeVersion,string ProductSourceRevision,IReadOnlyList<string> Origins)
{
    internal const string Kind="MaterializedDevelopmentPayload";
    internal static InstallationPayloadDefinition Read(JsonElement value)
    {
        InstallationPackage.Fields(value,"kind","runtimeVersion","productSourceRevision","origins");
        if(value.GetProperty("kind").GetString()!=Kind||value.GetProperty("runtimeVersion").GetString()!="10.0.13"||
            value.GetProperty("productSourceRevision").GetString() is not{Length:40} revision||!revision.All(char.IsAsciiHexDigit))throw new InvalidDataException("Unsupported payload contract or source revision.");
        var list=value.GetProperty("origins");if(list.ValueKind!=JsonValueKind.Array||list.GetArrayLength() is <1 or >2)throw new InvalidDataException("Explicit extension origins required.");
        var origins=list.EnumerateArray().Select(v=>v.GetString()??"").ToArray();
        if(origins.Any(v=>!InstallationPayload.IsOrigin(v))||origins.Distinct(StringComparer.Ordinal).Count()!=origins.Length)throw new InvalidDataException("Invalid extension origin.");
        return new("10.0.13",revision,Array.AsReadOnly(origins));
    }
    internal object Json()=>new{kind=Kind,runtimeVersion=RuntimeVersion,productSourceRevision=ProductSourceRevision,origins=Origins};
}
internal static class InstallationPayload
{
    internal const string SourceCatalogName="source-package.json",ProvenanceName="installed-source.json",HostManifestName="com.kuaiyi.native_host.json",TrustName="native-host-trust.json";
    internal static IReadOnlyList<string> Roles{get;}=Array.AsReadOnly(new[]{"KuaiYi.App","KuaiYi.NativeHost","KuaiYi.CaptureBroker","KuaiYi.ClipboardHost","KuaiYi.CaptureHost","KuaiYi.OleReceiver","KuaiYi.ImageWorker"});
    internal static IReadOnlyList<string> ManagedFiles{get;}=Array.AsReadOnly(Roles.Select(v=>v+".dll").Concat(new[]{"KuaiYi.Core.dll","KuaiYi.Contracts.dll","KuaiYi.Imaging.dll","KuaiYi.Windows.dll","KuaiYi.Providers.dll","KuaiYi.OleTransfer.dll"}).ToArray());
    internal static IReadOnlyList<string> NativeFiles{get;}=Array.AsReadOnly(Roles.Select(v=>v+".exe").Concat(new[]{"KuaiYi.CaptureNative.dll","KuaiYi.OcrNative.dll","KuaiYi.OleNativeBridge.dll","KuaiYi.ShellExtension.dll","libSkiaSharp.dll","hostfxr.dll","hostpolicy.dll","coreclr.dll","wpfgfx_cor3.dll"}).ToArray());
    internal static IReadOnlyList<string> TrustedFiles{get;}=Array.AsReadOnly(Roles.SelectMany(v=>new[]{v+".exe",v+".dll",v+".deps.json",v+".runtimeconfig.json"})
        .Concat(ManagedFiles).Concat(NativeFiles).Concat(new[]{"SkiaSharp.dll","System.Private.CoreLib.dll","PresentationFramework.dll","PresentationCore.dll","WindowsBase.dll"}).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    internal static IReadOnlyList<string> ResourceFiles{get;}=Array.AsReadOnly(new[]{"identity/AppxManifest.xml","identity/Assets/storelogo.png","identity/Assets/Square44x44Logo.png","identity/Assets/Square150x150Logo.png","identity/Assets/KuaiYi.ico",
        "browser-extension/manifest.json","browser-extension/service-worker.js","browser-extension/protocol.js","browser-extension/acquire.js","browser-extension/options.html","browser-extension/options.js","browser-extension/icon.png",
        "licenses/SkiaSharp-4.153.1/LICENSE.txt","licenses/SkiaSharp-4.153.1/THIRD-PARTY-NOTICES.txt","licenses/dotnet/LICENSE.txt","licenses/dotnet/THIRD-PARTY-NOTICES.txt"});
    private static readonly string[] Reserved=[SourceCatalogName,ProvenanceName,HostManifestName,TrustName];
    internal static bool IsOrigin(string? origin)=>origin is {Length:52}&&Regex.IsMatch(origin,@"\Achrome-extension://[a-p]{32}/\z",RegexOptions.CultureInvariant);
    internal static void ValidateSource(InstallationPackage package,string directory,CancellationToken cancellation,InstallationPreparationIO? io=null)
    {
        try{ValidateSourceCore(package,directory,cancellation,io);}
        catch(Exception error)when(error is BadImageFormatException or KeyNotFoundException or XmlException or InvalidOperationException or FormatException or OverflowException)
        {throw new InvalidDataException("Malformed payload closure metadata or PE.",error);}
    }
    private static void ValidateSourceCore(InstallationPackage package,string directory,CancellationToken cancellation,InstallationPreparationIO? io)
    {
        if(package.Schema!=2||package.Payload==null)throw new InvalidDataException("A materializable source catalog is required.");
        var entries=package.Manifest.Files.ToDictionary(x=>x.Path,StringComparer.Ordinal);
        if(package.Manifest.Files.Any(f=>Reserved.Contains(f.Path,StringComparer.OrdinalIgnoreCase)))throw new InvalidDataException("Source may not supply derived metadata.");
        if(package.Manifest.Files.Count+Reserved.Length>4096)throw new InvalidDataException("Derived files exceed installation file limit.");
        foreach(string name in TrustedFiles.Concat(ResourceFiles))if(!entries.TryGetValue(name,out var entry)||entry.Bytes<=0)throw new InvalidDataException("Payload closure is incomplete: "+name);
        foreach(string name in NativeFiles){cancellation.ThrowIfCancellationRequested();RequireNative(Path.Combine(directory,name),io);}
        foreach(string name in ManagedFiles){cancellation.ThrowIfCancellationRequested();RequireManaged(Path.Combine(directory,name),Path.GetFileNameWithoutExtension(name),package.Payload.ProductSourceRevision,name=="KuaiYi.App.dll",entries,io);}
        foreach(string role in Roles){cancellation.ThrowIfCancellationRequested();RequireRuntime(directory,role,package.Payload.RuntimeVersion,io);RequireDependencies(directory,role,entries,cancellation,io);}
        RequireIdentity(directory,io);RequireExtension(directory,io);
    }
    internal static void Materialize(InstallationPackage source,string staging,string destination,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();var metadata=Derived(source,destination);
        InstallationControlledLaunch.Exact(Path.Combine(staging,SourceCatalogName),source.Catalog,1024*1024);
        foreach(var item in metadata)if(item.Key!=SourceCatalogName){cancellation.ThrowIfCancellationRequested();SettingsStore.WriteFlushed(Path.Combine(staging,item.Key),item.Value);}
        SettingsStore.WriteFlushed(Path.Combine(staging,InstallationPackage.CatalogName),InstalledCatalog(source,metadata));
    }
    internal static InstallationPackage RequireMaterialized(string directory,CancellationToken cancellation=default)
    {
        var package=InstallationPackage.ReadInstalled(directory,cancellation);
        if(package.Schema!=3)throw new InstallationRecoveryException("Materialized development payload required; generic catalogs grant no payload qualification.");return package;
    }
    internal static void ValidateInstalled(InstallationPackage installed,string directory,string finalDirectory,CancellationToken cancellation,InstallationPreparationIO? io=null)
    {
        byte[] bytes=InstallationPackage.ReadDocument(Path.Combine(directory,SourceCatalogName),io:io);var source=InstallationPackage.Parse(bytes);
        if(source.Schema!=2||source.Payload==null||!string.Equals(source.CatalogSha256,installed.SourceCatalogSha256,StringComparison.OrdinalIgnoreCase)||installed.Manifest.Version!=source.Manifest.Version)throw new InvalidDataException("Materialized source provenance mismatch.");
        ValidateSource(source,directory,cancellation,io);var metadata=Derived(source,finalDirectory);
        foreach(var item in metadata){cancellation.ThrowIfCancellationRequested();int limit=item.Key==SourceCatalogName?1024*1024:65536;if(io!=null)io.RequireExactMetadata(Path.Combine(directory,item.Key),item.Value,limit);else InstallationControlledLaunch.Exact(Path.Combine(directory,item.Key),item.Value,limit);}
        if(!installed.Catalog.AsSpan().SequenceEqual(InstalledCatalog(source,metadata)))throw new InvalidDataException("Derived catalog is not the exact source and closed metadata set.");
    }
    internal static Dictionary<string,byte[]> Derived(InstallationPackage source,string destination)
    {
        string final=InstallationPaths.Root(destination);var generation=new DirectoryInfo(final);
        if(generation.Parent?.Name!="versions"||!Guid.TryParseExact(generation.Name,"N",out _))throw new InvalidDataException("Noncanonical materialized generation.");
        var definition=source.Payload??throw new InvalidDataException("Missing payload definition.");var entries=source.Manifest.Files.ToDictionary(x=>x.Path,StringComparer.Ordinal);
        var trust=TrustedFiles.ToDictionary(name=>name,name=>entries[name].Sha256,StringComparer.Ordinal);
        return new(StringComparer.Ordinal)
        {
            [SourceCatalogName]=source.Catalog,
            [ProvenanceName]=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,kind=InstallationPayloadDefinition.Kind,generation=generation.Name,directory=final,sourceCatalogSha256=source.CatalogSha256}),
            [HostManifestName]=JsonSerializer.SerializeToUtf8Bytes(new{name="com.kuaiyi.native_host",description="KuaiYi local image bridge",path=Path.Combine(final,"KuaiYi.NativeHost.exe"),type="stdio",allowed_origins=definition.Origins}),
            [TrustName]=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,files=trust})
        };
    }
    internal static byte[] InstalledCatalog(InstallationPackage source,Dictionary<string,byte[]> metadata)
    {
        var files=source.Manifest.Files.Concat(metadata.Select(k=>new InstallationFile(k.Key,k.Value.Length,Convert.ToHexString(SHA256.HashData(k.Value))))).OrderBy(f=>f.Path,StringComparer.Ordinal).Select(f=>new{path=f.Path,bytes=f.Bytes,sha256=f.Sha256}).ToArray();
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=3,product="KuaiYi",version=source.Manifest.Version,files,sourceCatalogSha256=source.CatalogSha256});
        // Apply the original entry/total/catalog bounds to the derived result as well as the source.
        if(bytes.Length>1024*1024)throw new InvalidDataException("Derived catalog exceeds metadata limit.");_=InstallationPackage.Parse(bytes);return bytes;
    }
    private static void RequireNative(string path,InstallationPreparationIO? io)
    {
        InspectPe(path,io,pe=>
        {
        if(pe.PEHeaders.CoffHeader.Machine!=Machine.Amd64||pe.PEHeaders.PEHeader?.Magic!=PEMagic.PE32Plus||pe.HasMetadata)throw new InvalidDataException("Windows x64 native PE required.");
        });
    }
    private static void RequireManaged(string path,string identity,string revision,bool app,Dictionary<string,InstallationFile> entries,InstallationPreparationIO? io)
    {
        InspectPe(path,io,pe=>
        {
        var reader=pe.GetMetadataReader();var assembly=reader.GetAssemblyDefinition();
        if(reader.GetString(assembly.Name)!=identity)throw new InvalidDataException("Managed role identity mismatch.");
        foreach(var handle in reader.AssemblyReferences)
        {string name=reader.GetString(reader.GetAssemblyReference(handle).Name)+".dll";if(!entries.TryGetValue(name,out var entry)||entry.Bytes<=0)throw new InvalidDataException("Managed assembly reference missing: "+name);}
        string? informational=null;
        foreach(var handle in assembly.GetCustomAttributes())
        {
            var attribute=reader.GetCustomAttribute(handle);if(attribute.Constructor.Kind!=HandleKind.MemberReference)continue;
            var constructor=reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);if(constructor.Parent.Kind!=HandleKind.TypeReference)continue;
            var type=reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
            if(reader.GetString(type.Namespace)!="System.Reflection"||reader.GetString(type.Name)!="AssemblyInformationalVersionAttribute")continue;
            if(informational!=null)throw new InvalidDataException("Duplicate managed source revision.");var blob=reader.GetBlobReader(attribute.Value);
            if(blob.ReadUInt16()!=1)throw new InvalidDataException("Invalid managed revision metadata.");informational=blob.ReadSerializedString();
        }
        if(informational==null||!informational.EndsWith("+"+revision,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Mixed or unknown product build revision.");
        if(app&&reader.TypeDefinitions.Any(h=>{var t=reader.GetTypeDefinition(h);return reader.GetString(t.Namespace)=="KuaiYi.App"&&reader.GetString(t.Name) is "IsolatedSessionLayout" or "HostedOleProbe" or "HostedOleMarshalProbe";}))throw new InvalidDataException("Acceptance-only App build cannot be installed as the normal product.");
        });
    }
    private static void InspectPe(string path,InstallationPreparationIO? io,Action<PEReader> inspect)
    {
        if(io!=null){io.ReadPe(path,inspect);return;}
        using var input=File.OpenRead(path);using var pe=new PEReader(input);inspect(pe);
    }
    private static void RequireRuntime(string directory,string role,string version,InstallationPreparationIO? io)
    {
        using var doc=JsonDocument.Parse(InstallationPackage.ReadDocument(Path.Combine(directory,role+".runtimeconfig.json"),65536,io),new(){MaxDepth=16});var options=doc.RootElement.GetProperty("runtimeOptions");
        if(options.TryGetProperty("framework",out _)||options.TryGetProperty("frameworks",out _)||!options.TryGetProperty("includedFrameworks",out var frameworks)||frameworks.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("Self-contained runtime configuration required.");
        var required=new HashSet<string>(StringComparer.Ordinal){"Microsoft.NETCore.App"};if(role is "KuaiYi.App" or "KuaiYi.CaptureBroker" or "KuaiYi.OleReceiver")required.Add("Microsoft.WindowsDesktop.App");
        foreach(var f in frameworks.EnumerateArray())if(f.GetProperty("version").GetString()!=version||!required.Remove(f.GetProperty("name").GetString()??""))throw new InvalidDataException("Runtime framework contract mismatch.");
        if(required.Count!=0)throw new InvalidDataException("Runtime frameworks incomplete.");
    }
    private static void RequireDependencies(string directory,string role,Dictionary<string,InstallationFile> entries,CancellationToken cancellation,InstallationPreparationIO? io)
    {
        using var doc=JsonDocument.Parse(InstallationPackage.ReadDocument(Path.Combine(directory,role+".deps.json"),4*1024*1024,io),new(){MaxDepth=32});var root=doc.RootElement;
        string target=root.GetProperty("runtimeTarget").GetProperty("name").GetString()??"";if(!target.EndsWith("/win-x64",StringComparison.Ordinal))throw new InvalidDataException("Windows x64 dependency target required.");
        var libraries=root.GetProperty("targets").GetProperty(target);var names=new HashSet<string>(StringComparer.Ordinal);int visited=0;
        foreach(var library in libraries.EnumerateObject())
        {
            if(!names.Add(library.Name))throw new InvalidDataException("Duplicate dependency library.");
            foreach(string section in new[]{"runtime","native","resources","runtimeTargets"})
            {
                if(!library.Value.TryGetProperty(section,out var assets))continue;var seen=new HashSet<string>(StringComparer.Ordinal);
                foreach(var asset in assets.EnumerateObject())
                {
                    cancellation.ThrowIfCancellationRequested();if(++visited>8192||!seen.Add(asset.Name))throw new InvalidDataException("Dependency declaration bound or duplicate.");
                    if(section=="runtimeTargets"&&asset.Value.GetProperty("rid").GetString() is not("win-x64" or "win"))continue;
                    string[] parts=asset.Name.Replace('\\','/').Split('/');if(parts.Any(p=>p.Length==0||p is "." or ".."||p.Contains(':')))throw new InvalidDataException("Unsafe dependency declaration.");
                    string name=parts[^1];if(name=="_._")continue;
                    if(section=="resources")
                    {
                        string locale=asset.Value.GetProperty("locale").GetString()??"";if(locale.Length is <1 or >32||locale.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new InvalidDataException("Invalid resource locale.");name=locale+"/"+name;
                    }
                    if(!entries.TryGetValue(name,out var entry)||entry.Bytes<=0)throw new InvalidDataException("Declared payload dependency missing: "+name);
                    if((section=="native"||section=="runtimeTargets"&&asset.Value.GetProperty("assetType").GetString()=="native")&&name.EndsWith(".dll",StringComparison.OrdinalIgnoreCase))RequireNative(Path.Combine(directory,name),io);
                }
            }
        }
        if(visited==0)throw new InvalidDataException("Empty dependency closure.");
    }
    private static void RequireIdentity(string directory,InstallationPreparationIO? io)
    {
        using var stream=new MemoryStream(InstallationPackage.ReadDocument(Path.Combine(directory,"identity/AppxManifest.xml"),io:io));
        using var xml=XmlReader.Create(stream,new(){DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1024*1024});var root=XDocument.Load(xml).Root??throw new InvalidDataException("Missing identity manifest.");XNamespace ns="http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        var identities=root.Elements(ns+"Identity").ToArray();var containers=root.Elements(ns+"Applications").ToArray();
        if(root.Name!=ns+"Package"||identities.Length!=1||identities[0].Attribute("ProcessorArchitecture")?.Value!="x64"||identities[0].Attribute("Name")?.Value!="KuaiYi.Development"||identities[0].Attribute("Publisher")?.Value!="CN=KuaiYi Development"||containers.Length!=1)throw new InvalidDataException("Development identity contract mismatch.");
        var expected=new Dictionary<string,string>(StringComparer.Ordinal){["KuaiYi"]="KuaiYi.App.exe",["ImageWorker"]="KuaiYi.ImageWorker.exe"};
        foreach(var app in containers[0].Elements(ns+"Application"))
        {
            string id=app.Attribute("Id")?.Value??"";
            if(!expected.Remove(id,out string? executable)||app.Attribute("Executable")?.Value!=executable)throw new InvalidDataException("Identity application id/executable mismatch or duplicate.");
        }
        if(expected.Count!=0)throw new InvalidDataException("App and worker identity declarations required.");
    }
    private static void RequireExtension(string directory,InstallationPreparationIO? io)
    {
        using var doc=JsonDocument.Parse(InstallationPackage.ReadDocument(Path.Combine(directory,"browser-extension/manifest.json"),65536,io),new(){MaxDepth=16});var root=doc.RootElement;
        // A new caller-pinned source digest grants byte integrity, not additional extension capabilities.
        if(root.TryGetProperty("host_permissions",out _)||root.TryGetProperty("optional_host_permissions",out _)||root.TryGetProperty("optional_permissions",out _)||root.TryGetProperty("content_scripts",out _))throw new InvalidDataException("Unexpected extension permission contract.");
        InstallationPackage.Fields(root,"manifest_version","name","version","description","permissions","background","action","options_page","icons","content_security_policy");
        if(root.GetProperty("manifest_version").GetInt32()!=3||root.GetProperty("version").GetString()!="0.0.1")throw new InvalidDataException("Unsupported extension manifest version.");
        Display(root,"name",128);Display(root,"description",1024);
        var permissions=root.GetProperty("permissions").EnumerateArray().Select(v=>v.GetString()).ToArray();
        if(permissions.Length!=3||!new HashSet<string?>(permissions,StringComparer.Ordinal).SetEquals(new[]{"contextMenus","nativeMessaging","activeTab"}))throw new InvalidDataException("Unexpected extension permissions.");
        var background=root.GetProperty("background");InstallationPackage.Fields(background,"service_worker","type");
        if(background.GetProperty("service_worker").GetString()!="service-worker.js"||background.GetProperty("type").GetString()!="module"||root.GetProperty("options_page").GetString()!="options.html")throw new InvalidDataException("Unexpected extension entry point.");
        var action=root.GetProperty("action");InstallationPackage.Fields(action,"default_title");Display(action,"default_title",128);
        var icons=root.GetProperty("icons");InstallationPackage.Fields(icons,"16","48","128");
        if(icons.EnumerateObject().Any(icon=>icon.Value.GetString()!="icon.png"))throw new InvalidDataException("Unexpected extension icon path.");
        var csp=root.GetProperty("content_security_policy");InstallationPackage.Fields(csp,"extension_pages");
        if(csp.GetProperty("extension_pages").GetString()!="script-src 'self'; object-src 'none'")throw new InvalidDataException("Unexpected extension content security policy.");
        static void Display(JsonElement owner,string name,int maximum)
        {
            string? value=owner.GetProperty(name).GetString();
            if(value==null||value.Length<1||value.Length>maximum||value.Any(char.IsControl))throw new InvalidDataException("Invalid extension display text.");
        }
    }
}
