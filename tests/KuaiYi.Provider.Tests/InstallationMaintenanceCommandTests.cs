using System.Text;
using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class InstallationMaintenanceCommandTests
{
    private static readonly byte[] Pins=Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"kind\":\"KuaiYiInstallerSourcePins\",\"availability\":\"Pinned\",\"generationSha256\":\""+new string('a',64)+"\",\"bootstrapSha256\":\""+new string('b',64)+"\"}");
    [Theory][InlineData("--install",0)][InlineData("--repair",1)][InlineData("--uninstall",2)]
    public void OnlyExactSingleCommands(string value,int expected)
    {Assert.Equal((InstallationPublicCommand)expected,InstallationCommandLine.Parse([value]));Assert.Null(InstallationCommandLine.Parse([value,"--force"]));Assert.Null(InstallationCommandLine.Parse([value,"C:/other-app"]));Assert.Null(InstallationCommandLine.Parse([value,"--delete-user-data"]));}
    [Theory][InlineData("--INSTALL")][InlineData("--remove")][InlineData("")][InlineData("--repair ")]
    public void RejectUnknownCommand(string value)=>Assert.Null(InstallationCommandLine.Parse([value]));
    [Theory][InlineData(1)][InlineData(0)]
    public async Task UnavailablePinsRefuseBeforeAnyPathOrPlatformOwner(int value)
    {
        int paths=0,owners=0;var bytes=Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"kind\":\"KuaiYiInstallerSourcePins\",\"availability\":\"Unavailable\",\"generationSha256\":null,\"bootstrapSha256\":null}");
        var e=new InstallationMaintenanceEntry(bytes,(InstallationMaintenanceIntent)value,(_,_)=>{owners++;throw new Exception();},()=>{paths++;throw new Exception();});
        var r=await e.Start();Assert.Equal(InstallationMaintenanceStatus.Refused,r.Status);Assert.True(r.CleanupConfirmed);Assert.Null(r.Observation);Assert.Equal(0,paths);Assert.Equal(0,owners);
    }
    [Fact]public async Task RepairNeverFallsBackToInstalledOrAdjacentSources()
    {
        int sources=0,owners=0;var e=new InstallationMaintenanceEntry(Pins,InstallationMaintenanceIntent.Repair,(_,_)=>{owners++;throw new Exception();},()=>{sources++;throw new InvalidDataException("PINNED_SOURCE_ABSENT");});
        var r=await e.Start();Assert.Equal(InstallationMaintenanceStatus.Refused,r.Status);Assert.Equal(1,sources);Assert.Equal(0,owners);Assert.Single(r.Errors);Assert.True(r.CleanupConfirmed);
    }
    [Fact]public async Task RepeatedCommandObservationJoinsOriginalWorkWithoutRepairReplay()
    {
        int calls=0;var e=new InstallationMaintenanceEntry(Pins,InstallationMaintenanceIntent.Uninstall,(_,_)=>{Interlocked.Increment(ref calls);throw new InvalidDataException("FOREIGN_OWNER");},()=>throw new Exception("Uninstall must not read repair payload"));
        var a=e.Start();Assert.Same(a,e.Start());var r=await a;Assert.Equal(1,calls);Assert.Equal(InstallationMaintenanceStatus.Refused,r.Status);Assert.Null(r.Observation);Assert.True(r.CleanupConfirmed);Assert.Empty(await e.SettleOriginalsAsync());Assert.Same(a,e.Start());Assert.Equal(1,calls);
    }
    [Fact]public async Task CancellationBeforeAdmissionCannotCreatePlatformOwners()
    {
        using var stop=new CancellationTokenSource();stop.Cancel();int calls=0;var e=new InstallationMaintenanceEntry(Pins,InstallationMaintenanceIntent.Uninstall,(_,_)=>{calls++;throw new Exception();},()=>throw new Exception(),stop.Token);
        var r=await e.Start();Assert.Equal(InstallationMaintenanceStatus.Cancelled,r.Status);Assert.Equal(0,calls);Assert.True(r.CleanupConfirmed);
    }
    [Theory][InlineData(1)][InlineData(3)]
    public async Task RepairRejectsDevelopmentOrInstalledCatalogWithoutAdoptingFiles(int schema)
    {
        string root=Path.Combine(Path.GetTempPath(),"kuaiyi-maintenance-contract-"+Guid.NewGuid().ToString("N")),g=Path.Combine(root,"generation");Directory.CreateDirectory(g);
        string content=Path.Combine(g,"owned-public-input.txt"),catalog=Path.Combine(g,"package.json");byte[] bytes="PUBLIC REGENERABLE INPUT"u8.ToArray();File.WriteAllBytes(content,bytes);
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        var fields=new Dictionary<string,object>{{"schemaVersion",schema},{"product","KuaiYi"},{"version","1.0"},{"files",new[]{new{path="owned-public-input.txt",bytes=bytes.Length,sha256=hash}}}};
        if(schema==3)fields["sourceCatalogSha256"]=new string('c',64);
        byte[] document=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(fields);File.WriteAllBytes(catalog,document);
        byte[] pins=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,kind="KuaiYiInstallerSourcePins",availability="Pinned",generationSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(document)),bootstrapSha256=new string('b',64)});
        bool clean=false;int owners=0;
        try
        {
            var entry=new InstallationMaintenanceEntry(pins,InstallationMaintenanceIntent.Repair,(_,_)=>{owners++;throw new Exception();},()=>root);var r=await entry.Start();
            Assert.Equal(InstallationMaintenanceStatus.Refused,r.Status);Assert.True(r.CleanupConfirmed);Assert.Equal(0,owners);Assert.Equal(bytes,File.ReadAllBytes(content));Assert.Equal(document,File.ReadAllBytes(catalog));Assert.Null(r.Observation);clean=true;
        }
        finally{if(clean){File.Delete(content);File.Delete(catalog);Directory.Delete(g);Directory.Delete(root);}} // Exact two synthetic files only, retained on failure.
    }

}
