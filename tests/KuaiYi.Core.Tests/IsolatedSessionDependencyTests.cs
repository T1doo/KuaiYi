using System.Text.Json;
using Xunit;

namespace KuaiYi.Core.Tests;

// Declaration checks use public synthetic files. They do not execute DLLs or prove Windows loading.
public sealed class IsolatedSessionDependencyTests
{
    private sealed class Rig : IDisposable
    {
        internal string Root { get; }=Path.Combine(Path.GetTempPath(),"kuaiyi-session-deps-"+Guid.NewGuid().ToString("N"));
        internal Rig()=>Directory.CreateDirectory(Root);
        internal void File(string name)
        {
            string path=Path.Combine(Root,name);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            System.IO.File.WriteAllText(path,"PUBLIC_SYNTHETIC_DEPENDENCY");
        }
        internal void Declarations(object libraries,string target=".NETCoreApp,Version=v10.0/win-x64")=>
            System.IO.File.WriteAllText(Path.Combine(Root,"KuaiYi.App.deps.json"),JsonSerializer.Serialize(new{runtimeTarget=new{name=target},targets=new Dictionary<string,object>{{target,libraries}}}));
        public void Dispose()=>Directory.Delete(Root,true);
    }

    [Fact] public void RuntimeNativeAndResourceDeclarationsRequireTheirActualFiles()
    {
        using var r=new Rig();r.File("PUBLIC.dll");r.File("PUBLIC_NATIVE.dll");r.File("zh-Hans/PUBLIC.resources.dll");
        r.Declarations(new Dictionary<string,object>{{"PUBLIC/1.0",new{runtime=new Dictionary<string,object>{{"lib/net10.0/PUBLIC.dll",new{}}},native=new Dictionary<string,object>{{"runtimes/win-x64/native/PUBLIC_NATIVE.dll",new{}}},resources=new Dictionary<string,object>{{"lib/net10.0/zh-Hans/PUBLIC.resources.dll",new{locale="zh-Hans"}}}}}});
        Assert.Equal(3,IsolatedSessionPackage.RequireDeclaredDependencies(r.Root));
        System.IO.File.Delete(Path.Combine(r.Root,"zh-Hans/PUBLIC.resources.dll"));
        Assert.Throws<InvalidDataException>(()=>IsolatedSessionPackage.RequireDeclaredDependencies(r.Root));
    }

    [Theory][InlineData("../PUBLIC.dll")][InlineData("lib/../PUBLIC.dll")][InlineData("/PUBLIC.dll")][InlineData("lib//PUBLIC.dll")][InlineData("PUBLIC.dll:stream")]
    public void UnsafeDeclarationsAreRefusedEvenWithAMatchingBasename(string name)
    {
        using var r=new Rig();r.File("PUBLIC.dll");r.Declarations(new Dictionary<string,object>{{"PUBLIC/1.0",new{runtime=new Dictionary<string,object>{{name,new{}}}}}});
        Assert.Throws<InvalidDataException>(()=>IsolatedSessionPackage.RequireDeclaredDependencies(r.Root));
    }

    [Theory][InlineData("../PUBLIC")][InlineData("")][InlineData("PUBLIC/OTHER")][InlineData("PUBLIC:stream")]
    public void UnsafeResourceLocalesAreRefused(string locale)
    {
        using var r=new Rig();r.Declarations(new Dictionary<string,object>{{"PUBLIC/1.0",new{resources=new Dictionary<string,object>{{"PUBLIC.resources.dll",new{locale}}}}}});
        Assert.Throws<InvalidDataException>(()=>IsolatedSessionPackage.RequireDeclaredDependencies(r.Root));
    }

    [Fact] public void ForeignRuntimeTargetsDoNotReplaceRequiredWinX64Files()
    {
        using var r=new Rig();r.File("PUBLIC.dll");r.Declarations(new Dictionary<string,object>{{"PUBLIC/1.0",new{runtime=new Dictionary<string,object>{{"PUBLIC.dll",new{}}},runtimeTargets=new Dictionary<string,object>{{"runtimes/linux-x64/native/PUBLIC_LINUX.so",new{rid="linux-x64",assetType="native"}},{"runtimes/win-x64/native/PUBLIC_WIN.dll",new{rid="win-x64",assetType="native"}}}}}});
        Assert.Throws<InvalidDataException>(()=>IsolatedSessionPackage.RequireDeclaredDependencies(r.Root));r.File("PUBLIC_WIN.dll");
        Assert.Equal(2,IsolatedSessionPackage.RequireDeclaredDependencies(r.Root));
    }

    [Theory][InlineData("empty")][InlineData("wrong-target")][InlineData("missing")][InlineData("empty-file")]
    public void EmptyWrongOrMissingDependencyClosureIsRefused(string mode)
    {
        using var r=new Rig();var runtime=new Dictionary<string,object>();if(mode!="empty")runtime["PUBLIC.dll"]=new{};
        r.Declarations(new Dictionary<string,object>{{"PUBLIC/1.0",new{runtime}}},mode=="wrong-target"?".NETCoreApp,Version=v10.0/linux-x64":".NETCoreApp,Version=v10.0/win-x64");
        if(mode=="empty-file")System.IO.File.WriteAllBytes(Path.Combine(r.Root,"PUBLIC.dll"),[]);
        Assert.Throws<InvalidDataException>(()=>IsolatedSessionPackage.RequireDeclaredDependencies(r.Root));
    }

    [Fact] public void SymlinkDependencyRefusedWithoutChangingItsTarget()
    {
        using var r=new Rig();r.File("PUBLIC_TARGET.dll");r.Declarations(new Dictionary<string,object>{{"PUBLIC/1.0",new{runtime=new Dictionary<string,object>{{"PUBLIC.dll",new{}}}}}});
        System.IO.File.CreateSymbolicLink(Path.Combine(r.Root,"PUBLIC.dll"),Path.Combine(r.Root,"PUBLIC_TARGET.dll"));
        Assert.Throws<IOException>(()=>IsolatedSessionPackage.RequireDeclaredDependencies(r.Root));
        Assert.Equal("PUBLIC_SYNTHETIC_DEPENDENCY",System.IO.File.ReadAllText(Path.Combine(r.Root,"PUBLIC_TARGET.dll")));
    }
}
