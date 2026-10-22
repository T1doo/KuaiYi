using System.Text.Json;
using KuaiYi.Core.Installation;
namespace KuaiYi.Core.Tests;
// Actual normal framework-dependent build bytes, no native/runtime fixture or source revision rewriting on the valid path.
internal sealed class BootstrapSource
{
    internal string Root{get;}
    internal string Version="1.0";
    internal BootstrapSource(string root)
    {Root=root;Directory.CreateDirectory(root);foreach(string file in InstallationBootstrapPackage.Files)File.Copy(Path.Combine(MaterializedPayloadSource.Repo,"src/KuaiYi.Launcher/bin/Release/net10.0/win-x64",file),Path.Combine(root,file));Catalog();}
    internal void Catalog()
    {var files=Directory.GetFiles(Root).Where(p=>Path.GetFileName(p)!="package.json").Order(StringComparer.Ordinal).Select(p=>new{path=Path.GetFileName(p),bytes=new FileInfo(p).Length,sha256=InstallationBootstrapPackage.Hash(p)}).ToArray();File.WriteAllBytes(Path.Combine(Root,"package.json"),JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",version=Version,files}));}
    internal string Digest=>InstallationBootstrapPackage.Hash(Path.Combine(Root,"package.json"));
}
