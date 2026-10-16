using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class DisplayPreferencesPersistenceTests
{
    private static readonly DisplayPreferences Imported=new(20,720,ThemePreference.Dark,MotionPreference.Reduce);
    private static async Task Apply(SettingsDraft draft)
    {using var transfer=new DisplayPreferencesTransfer();var preview=(await transfer.ImportAsync(draft.CaptureDisplayPreferences(),_=>Task.FromResult(Imported)))!;Assert.True(transfer.ApplyImport(preview,draft));}
    [Theory][InlineData(false)][InlineData(true)]
    public async Task AppliedPreviewUsesTheExistingSaveOrCancelTransactionWithoutSecretAccess(bool save)
    {
        using var fixture=new Fixture();using(var seed=await fixture.Coordinator.OpenDraftAsync())await fixture.Coordinator.SaveAsync(seed);byte[] original=await File.ReadAllBytesAsync(fixture.Store.PrimaryPath);string version=fixture.Coordinator.Effective!.Version;
        using(var draft=await fixture.Coordinator.OpenDraftAsync())
        {
            await Apply(draft);Assert.Equal(original,await File.ReadAllBytesAsync(fixture.Store.PrimaryPath));Assert.Equal(version,fixture.Coordinator.Effective!.Version);
            if(save)await fixture.Coordinator.SaveAsync(draft);
        }
        using var reopened=await fixture.Coordinator.OpenDraftAsync();Assert.Equal(save?Imported:new(15,420,ThemePreference.FollowSystem,MotionPreference.FollowSystem),reopened.CaptureDisplayPreferences().Value);Assert.Equal(save,version!=reopened.Basis.Version);
        if(!save)Assert.Equal(original,await File.ReadAllBytesAsync(fixture.Store.PrimaryPath));else Assert.Equal(original,await File.ReadAllBytesAsync(fixture.Store.BackupPath));Assert.Equal(0,fixture.Protector.Calls);
        var gate=new RuntimeGate();Assert.Equal(Activation.Off,gate.Snapshot.Activation);Assert.False(gate.TryBeginPassiveCapture(reopened.Data.TriggerMode,out _));Assert.False(gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out _,out _));
    }
    [Fact]public async Task ImportDoesNotBypassIndependentCoordinatorConflictOrOverwriteWinner()
    {
        using var fixture=new Fixture();using(var seed=await fixture.Coordinator.OpenDraftAsync())await fixture.Coordinator.SaveAsync(seed);var other=new SettingsCoordinator(new(fixture.Store.DirectoryPath),fixture.Secrets);using var a=await fixture.Coordinator.OpenDraftAsync();using var b=await other.OpenDraftAsync();await Apply(a);await Apply(b);
        a.Update(s=>s with{Overlay=s.Overlay with{PreferredWidthDip=800}});await fixture.Coordinator.SaveAsync(a);byte[] winner=await File.ReadAllBytesAsync(fixture.Store.PrimaryPath);await Assert.ThrowsAsync<SettingsConflictException>(()=>other.SaveAsync(b));Assert.False(b.IsClosed);Assert.Equal(winner,await File.ReadAllBytesAsync(fixture.Store.PrimaryPath));Assert.Equal(800,(await fixture.Store.LoadAsync()).Data.Overlay.PreferredWidthDip);Assert.Equal(0,fixture.Protector.Calls);
    }
    [Fact]public async Task LinkedImportAndExportLeaveUncapturedPasswordAndOtherControlsUntouched()
    {
        using var root=new DisplayPreferencesFlowTests.TemporaryDirectory();string input=Path.Combine(root.Path,"input.json"),output=Path.Combine(root.Path,"output.json");await File.WriteAllBytesAsync(input,DisplayPreferencesDocument.Write(Imported));using var draft=new SettingsDraft(new(new(),"PUBLIC-basis",SettingsLoadState.Current));var window=new SettingsWindow(draft,null);window.SeedUncapturedPasswordForDisplayTest("PUBLIC_SYNTHETIC_UNCAPTURED_PASSWORD");
        await window.ImportDisplayForTest(input);window.ApplyDisplayForTest();window.PreviewDisplayForTest();Assert.Equal(DisplayPreferencesExportStatus.Published,(await window.ExportDisplayForTest(output))!.Status);Assert.Equal(Imported,await DisplayPreferencesFiles.ReadAsync(output));Assert.Equal(0,window.CaptureCalls);Assert.Equal("PUBLIC_SYNTHETIC_UNCAPTURED_PASSWORD",window.PasswordText);Assert.DoesNotContain("PASSWORD",await File.ReadAllTextAsync(output));window.CloseDisplayForTest();
    }
    private sealed class Fixture:IDisposable
    {
        private readonly DisplayPreferencesFlowTests.TemporaryDirectory root=new();public SettingsStore Store{get;}public SecretFileStore Secrets{get;}public SettingsCoordinator Coordinator{get;}public NeverProtector Protector{get;}=new();
        public Fixture(){Store=new(Path.Combine(root.Path,"config"));Secrets=new(Path.Combine(root.Path,"secrets"),Protector,new DirectoryPolicy());Coordinator=new(Store,Secrets);}
        public void Dispose()=>root.Dispose();
    }
    private sealed class NeverProtector:ISecretProtector
    {public int Calls;public byte[] Protect(ReadOnlySpan<byte> value){Calls++;throw new InvalidOperationException("Display transfer must not access secrets.");}public byte[] Unprotect(ReadOnlySpan<byte> value){Calls++;throw new InvalidOperationException("Display transfer must not access secrets.");}}
    private sealed class DirectoryPolicy:ISecretDirectoryPolicy{public void EnsurePrivate(string path)=>Directory.CreateDirectory(path);}
}
