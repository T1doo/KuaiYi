using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class ClipboardPermissionTests
{
    private sealed class Policy:ISecretDirectoryPolicy{public void EnsurePrivate(string path)=>Directory.CreateDirectory(path);}
    private sealed class NoSecrets:ISecretProtector{public byte[] Protect(ReadOnlySpan<byte> bytes)=>throw new InvalidOperationException("No secret operation expected.");public byte[] Unprotect(ReadOnlySpan<byte> bytes)=>throw new InvalidOperationException("No secret read expected.");}
    private sealed class Fixture:IDisposable
    {
        public string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-clipboard-permission-"+Guid.NewGuid().ToString("N"));public SettingsStore Store;public SecretFileStore Secrets;public SettingsCoordinator Coordinator;
        public Fixture(){Store=new(Path.Combine(Root,"config"));Secrets=new(Path.Combine(Root,"secrets"),new NoSecrets(),new Policy());Coordinator=new(Store,Secrets);}
        public void Dispose(){if(Directory.Exists(Root))Directory.Delete(Root,true);}
    }
    [Fact]public async Task SeparateExplicitPermissionTransactionPublishesAndRevokesFrozenCaptureWithoutEnablingOff()
    {
        using var f=new Fixture();using var initial=await f.Coordinator.OpenDraftAsync();var gate=new RuntimeGate();using var link=new SettingsRuntimeLink(f.Coordinator,gate);link.Initialize(initial.Basis);
        var saved=await f.Coordinator.SetClipboardPermissionAsync(@"C:\Public\public.exe",true);Assert.Equal(Activation.Off,gate.Snapshot.Activation);Assert.True(ClipboardApplicationConsent.Allowed(@"c:\public\PUBLIC.exe",saved.Data.Privacy));Assert.False(saved.Data.Privacy.ClipboardFallbackDefault);
        var origin=new EndpointOrigin(new Uri("https://public.invalid"));gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));Assert.True(gate.TryBeginPassiveCapture(TriggerMode.Selection,out var ticket));var revoked=await f.Coordinator.SetClipboardPermissionAsync(@"c:\public\PUBLIC.exe",false);Assert.False(gate.IsCaptureCurrent(ticket));Assert.Empty(revoked.Data.Privacy.ClipboardAllowedApplications);Assert.Equal(revoked.Version,(await f.Store.LoadAsync()).Version);Assert.Empty(Directory.GetFiles(f.Store.DirectoryPath,"secret-txn-*.json"));
    }
    [Fact]public async Task OrdinaryDraftCannotGrantAndStaleDraftCannotOverwriteImmediatePermission()
    {
        using var f=new Fixture();using var ordinary=await f.Coordinator.OpenDraftAsync();ordinary.Update(s=>s with{Privacy=s.Privacy with{ClipboardAllowedApplications=[@"C:\Public\public.exe"]}});await Assert.ThrowsAsync<SettingsValidationException>(()=>f.Coordinator.SaveAsync(ordinary));Assert.Empty(f.Coordinator.Effective!.Data.Privacy.ClipboardAllowedApplications);
        using var stale=await f.Coordinator.OpenDraftAsync();stale.Update(s=>s with{Overlay=s.Overlay with{FontSizeDip=18}});await f.Coordinator.SetClipboardPermissionAsync(@"C:\Public\public.exe",true);await Assert.ThrowsAsync<SettingsConflictException>(()=>f.Coordinator.SaveAsync(stale));var current=await f.Store.LoadAsync();Assert.Single(current.Data.Privacy.ClipboardAllowedApplications);Assert.Equal(15,current.Data.Overlay.FontSizeDip);
    }
    [Fact]public async Task IndependentImmediateTransactionsSerializeAndPreserveOtherApplicationPermissions()
    {
        using var f=new Fixture();using var initial=await f.Coordinator.OpenDraftAsync();var other=new SettingsCoordinator(new(f.Store.DirectoryPath),f.Secrets);await Task.WhenAll(f.Coordinator.SetClipboardPermissionAsync(@"C:\Public\a.exe",true),other.SetClipboardPermissionAsync(@"C:\Public\b.exe",true));var saved=await f.Store.LoadAsync();Assert.Equal(2,saved.Data.Privacy.ClipboardAllowedApplications.Length);Assert.Contains(@"C:\Public\a.exe",saved.Data.Privacy.ClipboardAllowedApplications);Assert.Contains(@"C:\Public\b.exe",saved.Data.Privacy.ClipboardAllowedApplications);
    }
    [Theory][InlineData("public.exe")][InlineData(@"C:\Public\..\private.exe")][InlineData(@"\\server\share\public.exe")][InlineData(@"C:\Public\public.dll")]
    public async Task AmbiguousOrNonlocalApplicationNeverGrantsPermission(string path)
    {using var f=new Fixture();using var initial=await f.Coordinator.OpenDraftAsync();await Assert.ThrowsAsync<InvalidDataException>(()=>f.Coordinator.SetClipboardPermissionAsync(path,true));Assert.Empty(f.Coordinator.Effective!.Data.Privacy.ClipboardAllowedApplications);Assert.False(File.Exists(f.Store.PrimaryPath));}
    [Theory][InlineData("future")][InlineData("damaged")]
    public async Task UnknownOrDamagedConfigPreservesOriginalAndNeverGrants(string kind)
    {using var f=new Fixture();Directory.CreateDirectory(f.Store.DirectoryPath);byte[] bytes=System.Text.Encoding.UTF8.GetBytes(kind=="future"?"{\"schemaVersion\":99}":"{public damaged");await File.WriteAllBytesAsync(f.Store.PrimaryPath,bytes);var error=await Record.ExceptionAsync(()=>f.Coordinator.SetClipboardPermissionAsync(@"C:\Public\public.exe",true));Assert.True(error is UnknownSettingsSchemaException or SettingsRecoveryRequiredException);Assert.Equal(bytes,await File.ReadAllBytesAsync(f.Store.PrimaryPath));Assert.Empty(Directory.GetFiles(f.Store.DirectoryPath,"secret-txn-*.json"));}
    [Fact]public async Task CommittedObserverFailureRetainsJournalAndReplaysPermissionInvalidation()
    {
        using var f=new Fixture();using var initial=await f.Coordinator.OpenDraftAsync();int notifications=0;bool fail=true;f.Coordinator.Committed+=(settings,_)=>{notifications++;if(fail)throw new IOException("public observer failure");};await Assert.ThrowsAsync<IOException>(()=>f.Coordinator.SetClipboardPermissionAsync(@"C:\Public\public.exe",true));Assert.Single(Directory.GetFiles(f.Store.DirectoryPath,"secret-txn-*.json"));Assert.True(ClipboardApplicationConsent.Allowed(@"C:\Public\public.exe",(await f.Store.LoadAsync()).Data.Privacy));fail=false;using var recovered=await f.Coordinator.OpenDraftAsync();Assert.Equal(2,notifications);Assert.Empty(Directory.GetFiles(f.Store.DirectoryPath,"secret-txn-*.json"));Assert.Single(recovered.Data.Privacy.ClipboardAllowedApplications);
    }
    [Fact]public async Task RevocationNormalizesSlashAndCaseWithoutLeavingEquivalentGrant()
    {using var f=new Fixture();using var initial=await f.Coordinator.OpenDraftAsync();await f.Store.SaveAsync(initial.Basis,initial.Data with{Privacy=new(){ClipboardAllowedApplications=["c:/public/PUBLIC.exe"]}});using var loaded=await f.Coordinator.OpenDraftAsync();Assert.True(ClipboardApplicationConsent.Allowed(@"C:\Public\public.exe",loaded.Data.Privacy));var revoked=await f.Coordinator.SetClipboardPermissionAsync(@"C:\Public\public.exe",false);Assert.Empty(revoked.Data.Privacy.ClipboardAllowedApplications);}
}
