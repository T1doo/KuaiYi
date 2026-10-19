using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using KuaiYi.App;
using KuaiYi.Core.Installation;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed class IsolatedSessionLayoutTests
{
    private static void Refused(Action action){var error=Record.Exception(action);Assert.True(error is IOException or InvalidDataException,error?.GetType().Name??"Expected refusal.");}
    private sealed class Rig:IDisposable
    {
        internal readonly string Container=Path.Combine(Path.GetTempPath(),"kuaiyi-isolated-root-tests-"+Guid.NewGuid().ToString("N"));
        internal readonly Guid Run=Guid.NewGuid();
        internal string Root=>Path.Combine(Container,IsolatedSessionLayout.Prefix+Run.ToString("N"));
        internal string Data=>Path.Combine(Root,"user-data");internal string App=>Path.Combine(Root,"application");
        internal string Production=>Path.Combine(Container,"production","KuaiYi");internal string Selected=null!;
        internal async Task Prepare()
        {
            Directory.CreateDirectory(Data);Directory.CreateDirectory(Production);File.WriteAllText(Path.Combine(Production,"PUBLIC_SENTINEL"),"PUBLIC_PRODUCTION_UNTOUCHED");
            byte[] marker=IsolatedSessionLayout.Descriptor(Run);File.WriteAllBytes(Path.Combine(Root,IsolatedSessionLayout.Marker),marker);
            string source=Path.Combine(Container,"public-package");Directory.CreateDirectory(source);File.WriteAllBytes(Path.Combine(source,IsolatedSessionLayout.Marker),marker);
            var files=new[]{new{path=IsolatedSessionLayout.Marker,bytes=marker.Length,sha256=Convert.ToHexString(SHA256.HashData(marker))}};
            byte[] catalog=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",version="public-selector-test",files});File.WriteAllBytes(Path.Combine(source,"package.json"),catalog);
            Selected=(await new InstallationStore(App,Data,()=>true).ApplyAsync(source,Convert.ToHexString(SHA256.HashData(catalog)))).DirectoryPath;
        }
        public void Dispose(){if(Directory.Exists(Container))Directory.Delete(Container,true);}
    }
    [Fact]public async Task ActualStoreLayoutSelectsTheSameDedicatedDataAcrossSessionsAndSettingsWrites()
    {
        using var r=new Rig();await r.Prepare();byte[] pointer=File.ReadAllBytes(Path.Combine(r.App,"current.json"));
        string parent=IsolatedSessionLayout.Select(r.Selected,r.Production),child=IsolatedSessionLayout.Select(r.Selected,r.Production);Assert.Equal(r.Data,parent);Assert.Equal(parent,child);
        var settings=new SettingsStore(Path.Combine(child,"config"));var initial=await settings.LoadAsync();Assert.False(initial.ReadOnly);await settings.SaveAsync(initial,initial.Data);
        Assert.True(File.Exists(Path.Combine(r.Data,"config","settings.json")));Assert.Equal(r.Data,IsolatedSessionLayout.Select(r.Selected,r.Production));
        Assert.Equal(pointer,File.ReadAllBytes(Path.Combine(r.App,"current.json")));Assert.Single(Directory.GetFiles(r.Production));Assert.Equal("PUBLIC_PRODUCTION_UNTOUCHED",File.ReadAllText(Path.Combine(r.Production,"PUBLIC_SENTINEL")));
    }
    [Theory][InlineData("--diagnose")][InlineData("--ocr-probe")][InlineData("--platform-review")][InlineData("--prepare-image")][InlineData("--translation-test-provider")][InlineData("--user-data")]
    public void TestBuildRefusesEveryAlternateEntryBeforeOldCommandDispatch(string arg)
    {Assert.False(IsolatedSessionLayout.AllowsEntry([arg]));Assert.False(IsolatedSessionLayout.AllowsEntry([arg,"PUBLIC"]));Assert.False(IsolatedSessionLayout.AllowsEntry([arg,"PUBLIC","PUBLIC"]));}
    [Fact]public void OnlyNormalOrAnExactNonemptyControlledLaunchIsAccepted()
    {
        Assert.True(IsolatedSessionLayout.AllowsEntry([]));Assert.True(IsolatedSessionLayout.AllowsEntry(["--installation-handshake",Guid.NewGuid().ToString("N")]));
        Assert.False(IsolatedSessionLayout.AllowsEntry(["--installation-handshake",Guid.Empty.ToString("N")]));Assert.False(IsolatedSessionLayout.AllowsEntry(["--installation-handshake",Guid.NewGuid().ToString("D")]));Assert.False(IsolatedSessionLayout.AllowsEntry(["--installation-handshake",Guid.NewGuid().ToString("N"),"PUBLIC"]));
    }
    [Theory][InlineData("PUBLIC.")][InlineData("PUBLIC ")][InlineData("PUBLIC~1")][InlineData("PUBLIC:stream")]
    public void WindowsAliasAndStreamSpellingsAreRefusedBeforeFilesystemAccess(string component)
    {
        string path=Path.Combine(Path.GetTempPath(),component,IsolatedSessionLayout.Prefix+Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidDataException>(()=>IsolatedSessionLayout.LocalRoot(path));Assert.False(Directory.Exists(path));
    }
    [Theory][InlineData("PUBLIC.")][InlineData("PUBLIC ")][InlineData("PUBLIC~1")][InlineData("PUBLIC:stream")]
    public void RawWindowsAliasesCannotDisappearDuringPathNormalization(string component)
    {
        string path=Path.Combine(Path.GetTempPath(),component,"..",IsolatedSessionLayout.Prefix+Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidDataException>(()=>IsolatedSessionLayout.LocalRoot(path));
        Assert.False(Directory.Exists(path));
    }
    [Theory][InlineData("missing")][InlineData("oversize")][InlineData("other-run")][InlineData("path-field")][InlineData("duplicate")][InlineData("malformed")][InlineData("whitespace")]
    public async Task InvalidWorkspaceMetadataNeverFallsBackOrRepairsData(string mode)
    {
        using var r=new Rig();await r.Prepare();string marker=Path.Combine(r.Root,IsolatedSessionLayout.Marker);byte[] descriptor=File.ReadAllBytes(marker);
        switch(mode)
        {
            case "missing":File.Delete(marker);break;
            case "oversize":File.WriteAllBytes(marker,new byte[513]);break;
            case "other-run":File.WriteAllBytes(marker,IsolatedSessionLayout.Descriptor(Guid.NewGuid()));break;
            case "path-field":File.WriteAllText(marker,Encoding.UTF8.GetString(descriptor)[..^1]+",\"path\":\"PUBLIC\"}");break;
            case "duplicate":File.WriteAllText(marker,Encoding.UTF8.GetString(descriptor)[..^1]+",\"schemaVersion\":1}");break;
            case "malformed":File.WriteAllText(marker,"[]");break;
            case "whitespace":File.WriteAllText(marker,Encoding.UTF8.GetString(descriptor)+" ");break;
        }
        Refused(()=>IsolatedSessionLayout.Select(r.Selected,r.Production));Assert.Empty(Directory.GetFileSystemEntries(r.Data));Assert.Single(Directory.GetFiles(r.Production));
    }
    [Theory][InlineData("descriptor")][InlineData("data-missing")][InlineData("root-name")][InlineData("flat")]
    public async Task MissingOrMismatchedLayoutIsRetainedWithoutCreatingAnything(string mode)
    {
        using var r=new Rig();await r.Prepare();string directory=r.Selected;
        if(mode=="descriptor")File.WriteAllBytes(Path.Combine(directory,IsolatedSessionLayout.Marker),IsolatedSessionLayout.Descriptor(Guid.NewGuid()));
        if(mode=="data-missing")Directory.Delete(r.Data);
        if(mode=="root-name"){string moved=r.Root+"-wrong";Directory.Move(r.Root,moved);directory=Path.Combine(moved,"application","versions",Path.GetFileName(r.Selected));}
        if(mode=="flat")directory=r.Container;
        Refused(()=>IsolatedSessionLayout.Select(directory,r.Production));Assert.False(Directory.Exists(Path.Combine(r.Production,"config")));
    }
    [Theory][InlineData("same")][InlineData("below")][InlineData("above")][InlineData("case")]
    public async Task TestWorkspaceCannotOverlapProductionInEitherDirection(string mode)
    {
        using var r=new Rig();await r.Prepare();string production=mode switch{"same"=>r.Root,"below"=>r.Data,"above"=>r.Container,_=>r.Root.ToUpperInvariant()};
        Refused(()=>IsolatedSessionLayout.Select(r.Selected,production));Assert.Empty(Directory.GetFileSystemEntries(r.Data));
    }
    [Theory][InlineData("marker")][InlineData("descriptor")][InlineData("data")][InlineData("config")][InlineData("secrets")][InlineData("ancestor")]
    public async Task LinksAtEverySelectionBoundaryAreRejectedAndTheirTargetsPreserved(string mode)
    {
        using var r=new Rig();await r.Prepare();string directory=r.Selected;string external=Path.Combine(r.Container,"PUBLIC_EXTERNAL");Directory.CreateDirectory(external);File.WriteAllText(Path.Combine(external,"PUBLIC_SENTINEL"),"PUBLIC_UNCHANGED");
        string? link=null;
        try
        {
            if(mode is "marker" or "descriptor")
            {
                link=Path.Combine(mode=="marker"?r.Root:r.Selected,IsolatedSessionLayout.Marker);string target=Path.Combine(external,"descriptor.json");File.Copy(link,target);File.Delete(link);File.CreateSymbolicLink(link,target);
            }
            else if(mode=="ancestor"){link=Path.Combine(r.Container,"PUBLIC_ALIAS");Directory.CreateSymbolicLink(link,r.Root);directory=Path.Combine(link,"application","versions",Path.GetFileName(r.Selected));}
            else{link=mode=="data"?r.Data:Path.Combine(r.Data,mode);if(Directory.Exists(link))Directory.Delete(link);Directory.CreateSymbolicLink(link,external);}
            Refused(()=>IsolatedSessionLayout.Select(directory,r.Production));Assert.Equal("PUBLIC_UNCHANGED",File.ReadAllText(Path.Combine(external,"PUBLIC_SENTINEL")));
        }
        finally{if(link!=null){if(mode is "marker" or "descriptor")File.Delete(link);else Directory.Delete(link);}}
    }
    [Fact]public async Task BadPublishInputCannotCreateAWorkspaceOrBecomeAPseudoBundle()
    {
        using var r=new Rig();await r.Prepare();string source=Path.Combine(r.Container,"bad-publish");Directory.CreateDirectory(source);File.WriteAllText(Path.Combine(source,"KuaiYi.App.exe"),"PUBLIC_NOT_AN_EXECUTABLE");string output=Path.Combine(r.Container,"outputs");
        await Assert.ThrowsAsync<BadImageFormatException>(()=>IsolatedSessionPackage.PrepareAsync(source,output,r.Production));Assert.False(Directory.Exists(output));Assert.Single(Directory.GetFiles(r.Production));
    }
    [Theory][InlineData("same")][InlineData("below")][InlineData("above")]
    public async Task ProductionOverlapIsRefusedBeforePublishTraversalOrOutput(string mode)
    {
        using var r=new Rig();await r.Prepare();string source=mode switch{"same"=>r.Production,"below"=>Path.Combine(r.Production,"public-publish"),_=>Path.Combine(r.Container,"production")};Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source,"package.json"),"PUBLIC_DO_NOT_TRAVERSE");string output=Path.Combine(r.Container,"outputs");
        var error=await Assert.ThrowsAsync<InvalidDataException>(()=>IsolatedSessionPackage.PrepareAsync(source,output,r.Production));Assert.Equal("Session test overlaps production data.",error.Message);
        Assert.False(Directory.Exists(output));Assert.Equal("PUBLIC_PRODUCTION_UNTOUCHED",File.ReadAllText(Path.Combine(r.Production,"PUBLIC_SENTINEL")));
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task SourceAndOutputCannotContainEachOtherBeforePublishTraversal(bool sourceAbove)
    {
        using var r=new Rig();await r.Prepare();string parent=Path.Combine(r.Container,"publish-output"),child=Path.Combine(parent,"nested");Directory.CreateDirectory(child);File.WriteAllText(Path.Combine(parent,"package.json"),"PUBLIC_DO_NOT_TRAVERSE");
        string source=sourceAbove?parent:child,output=sourceAbove?child:parent;
        var error=await Assert.ThrowsAsync<InvalidDataException>(()=>IsolatedSessionPackage.PrepareAsync(source,output,r.Production));Assert.Equal("Session test overlaps production data.",error.Message);
        Assert.Empty(Directory.GetDirectories(output,IsolatedSessionLayout.Prefix+"*"));
    }
    [Theory][InlineData("same")][InlineData("below")][InlineData("above")]
    public async Task BuildWorkspaceRefusesProductionOverlapBeforeAnyDirectoryOrLog(string mode)
    {
        using var r=new Rig();await r.Prepare();string output=mode switch{"same"=>r.Production,"below"=>Path.Combine(r.Production,"builds"),_=>Path.Combine(r.Container,"production")};
        Assert.Throws<InvalidDataException>(()=>IsolatedSessionPackage.CreateBuildWorkspace(output,r.Production));
        Assert.False(Directory.Exists(Path.Combine(r.Production,"builds")));Assert.Single(Directory.GetFileSystemEntries(r.Production));
    }
    [Theory][InlineData("link")][InlineData("file")]
    public async Task BuildWorkspaceRefusesLinksAndFileConflictsWithoutRepair(string mode)
    {
        using var r=new Rig();await r.Prepare();string output=Path.Combine(r.Container,"build-output");
        try
        {
            if(mode=="link")Directory.CreateSymbolicLink(output,r.Production);else File.WriteAllText(output,"PUBLIC_UNKNOWN_FILE");
            Assert.ThrowsAny<IOException>(()=>IsolatedSessionPackage.CreateBuildWorkspace(output,r.Production));Assert.Single(Directory.GetFileSystemEntries(r.Production));
            if(mode=="file")Assert.Equal("PUBLIC_UNKNOWN_FILE",File.ReadAllText(output));
        }
        finally{if(mode=="link")Directory.Delete(output);}
    }
    [Fact]public async Task BuildPreparationUsesNewOwnedWorkspacesAndPreservesUnknownOutput()
    {
        using var r=new Rig();await r.Prepare();string output=Path.Combine(r.Container,"build-output");Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"PUBLIC_UNKNOWN"),"PUBLIC_PRESERVED");
        string first=IsolatedSessionPackage.CreateBuildWorkspace(output,r.Production),second=IsolatedSessionPackage.CreateBuildWorkspace(output,r.Production);
        Assert.NotEqual(first,second);Assert.True(Directory.Exists(first));Assert.Empty(Directory.GetFileSystemEntries(first));Assert.Equal("PUBLIC_PRESERVED",File.ReadAllText(Path.Combine(output,"PUBLIC_UNKNOWN")));Assert.Single(Directory.GetFiles(r.Production));
    }
}
