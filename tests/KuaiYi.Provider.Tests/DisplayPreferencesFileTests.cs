using System.Text;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class DisplayPreferencesFileTests
{
    private static readonly DisplayPreferences Value=new(19,600,ThemePreference.Light,MotionPreference.Reduce);
    [Fact]public async Task DirectoryReplacementCannotDeleteAnUnrelatedSameNamedFileDuringCleanup()
    {
        if(!OperatingSystem.IsLinux())return;using var root=new DisplayPreferencesFlowTests.TemporaryDirectory();string directory=Path.Combine(root.Path,"chosen"),moved=Path.Combine(root.Path,"moved"),other=Path.Combine(root.Path,"other");Directory.CreateDirectory(directory);Directory.CreateDirectory(other);string target=Path.Combine(directory,"display.json");string? unrelated=null;
        using var transfer=new DisplayPreferencesTransfer();var preview=transfer.PrepareExport(Value);
        var result=await transfer.ExportAsync(preview,async(bytes,ct)=>
        {
            var prepared=await DisplayPreferencesFiles.PrepareNewAsync(target,bytes,ct);string name=Path.GetFileName(Assert.Single(Directory.GetFiles(directory)));Directory.Move(directory,moved);Directory.CreateSymbolicLink(directory,other);unrelated=Path.Combine(other,name);await File.WriteAllTextAsync(unrelated,"PUBLIC_UNRELATED_KEEP");return prepared;
        });
        Assert.NotEqual(DisplayPreferencesExportStatus.Published,result.Status);Assert.True(File.Exists(unrelated));Assert.Equal("PUBLIC_UNRELATED_KEEP",await File.ReadAllTextAsync(unrelated!));Assert.Single(Directory.GetFiles(moved));Assert.False(File.Exists(Path.Combine(other,"display.json")));
    }
    [Theory][InlineData("file")][InlineData("directory")]
    public async Task ExistingSymbolicLinksAreRejectedBeforeReadingOrPreparingAFile(string kind)
    {
        if(!OperatingSystem.IsLinux())return;using var root=new DisplayPreferencesFlowTests.TemporaryDirectory();string real=Path.Combine(root.Path,"real");Directory.CreateDirectory(real);string input=Path.Combine(real,"display.json");await File.WriteAllBytesAsync(input,DisplayPreferencesDocument.Write(Value));string link=Path.Combine(root.Path,"link");
        if(kind=="file")File.CreateSymbolicLink(link,input);else Directory.CreateSymbolicLink(link,real);string selected=kind=="file"?link:Path.Combine(link,"display.json");await Assert.ThrowsAnyAsync<Exception>(()=>DisplayPreferencesFiles.ReadAsync(selected));
        if(kind=="directory")await Assert.ThrowsAnyAsync<Exception>(()=>DisplayPreferencesFiles.PrepareNewAsync(Path.Combine(link,"out.json"),DisplayPreferencesDocument.Write(Value)));
        Assert.Single(Directory.GetFiles(real));Assert.Equal(Value,await DisplayPreferencesFiles.ReadAsync(input));
    }
    [Fact]public async Task OrdinaryDirectoryReplacementAlsoRetainsAnUnrelatedFileAndReportsUnconfirmedCleanup()
    {
        if(!OperatingSystem.IsLinux())return;using var root=new DisplayPreferencesFlowTests.TemporaryDirectory();string chosen=Path.Combine(root.Path,"chosen"),moved=Path.Combine(root.Path,"moved");Directory.CreateDirectory(chosen);string? unrelated=null;using var transfer=new DisplayPreferencesTransfer();var preview=transfer.PrepareExport(Value);
        var result=await transfer.ExportAsync(preview,async(bytes,ct)=>{var prepared=await DisplayPreferencesFiles.PrepareNewAsync(Path.Combine(chosen,"output.json"),bytes,ct);string name=Path.GetFileName(Assert.Single(Directory.GetFiles(chosen)));Directory.Move(chosen,moved);Directory.CreateDirectory(chosen);unrelated=Path.Combine(chosen,name);await File.WriteAllTextAsync(unrelated,"PUBLIC_UNRELATED_KEEP");return prepared;});
        Assert.Equal(DisplayPreferencesExportStatus.Failed,result.Status);Assert.Equal(DisplayPreferencesIssue.Cleanup,result.Issue);Assert.Equal("PUBLIC_UNRELATED_KEEP",await File.ReadAllTextAsync(unrelated!));Assert.Single(Directory.GetFiles(moved));Assert.Single(Directory.GetFiles(chosen));
    }
    [Fact]public async Task TargetCollisionPreservesExistingFileAndOnlyCleansItsOwnedTemporary()
    {
        using var root=new DisplayPreferencesFlowTests.TemporaryDirectory();string target=Path.Combine(root.Path,"existing.json"),unknown=Path.Combine(root.Path,"unknown.lock");await File.WriteAllTextAsync(target,"PUBLIC_EXISTING_KEEP");await File.WriteAllTextAsync(unknown,"PUBLIC_UNKNOWN_KEEP");using var transfer=new DisplayPreferencesTransfer();var preview=transfer.PrepareExport(Value);
        var result=await transfer.ExportAsync(preview,(bytes,ct)=>DisplayPreferencesFiles.PrepareNewAsync(target,bytes,ct));Assert.Equal(DisplayPreferencesExportStatus.Failed,result.Status);Assert.Equal("PUBLIC_EXISTING_KEEP",await File.ReadAllTextAsync(target));Assert.Equal("PUBLIC_UNKNOWN_KEEP",await File.ReadAllTextAsync(unknown));Assert.Equal(2,Directory.GetFiles(root.Path).Length);Assert.Same(preview,transfer.Preview);
    }
    [Fact]public async Task CancelledNoncooperativePreparationCannotPublishAndCleansOnlyItsOwnStage()
    {
        using var root=new DisplayPreferencesFlowTests.TemporaryDirectory();string target=Path.Combine(root.Path,"display.json");var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);using var transfer=new DisplayPreferencesTransfer();var preview=transfer.PrepareExport(Value);
        var task=transfer.ExportAsync(preview,async(bytes,ct)=>{var prepared=await DisplayPreferencesFiles.PrepareNewAsync(target,bytes,ct);ready.SetResult();await release.Task;return prepared;});await ready.Task.WaitAsync(TimeSpan.FromSeconds(6));Assert.False(File.Exists(target));Assert.Single(Directory.GetFiles(root.Path));transfer.Cancel();release.SetResult();Assert.Equal(DisplayPreferencesExportStatus.Cancelled,(await task).Status);Assert.Empty(Directory.GetFiles(root.Path));
    }
    [Fact]public async Task LateCancellationAfterPublicationKeepsTheActualPublishedOutcomeAndNewPreview()
    {
        using var root=new DisplayPreferencesFlowTests.TemporaryDirectory();string target=Path.Combine(root.Path,"display.json");using var transfer=new DisplayPreferencesTransfer();using var cancellation=new CancellationTokenSource();var preview=transfer.PrepareExport(Value);DisplayPreferencesPreview? newer=null;
        var result=await transfer.ExportAsync(preview,async(bytes,ct)=>new Wrapped(await DisplayPreferencesFiles.PrepareNewAsync(target,bytes,ct),()=>{cancellation.Cancel();newer=transfer.PrepareExport(Value with{FontSizeDip=21});}),cancellation.Token);
        Assert.Equal(DisplayPreferencesExportStatus.Published,result.Status);Assert.Equal(Value,await DisplayPreferencesFiles.ReadAsync(target));Assert.Same(newer,transfer.Preview);Assert.Single(Directory.GetFiles(root.Path));
    }
    [Fact]public async Task DuplicateExportIsBusyAndCleanupFailureNeverLiesAboutACompletedPublication()
    {
        using var transfer=new DisplayPreferencesTransfer();var preview=transfer.PrepareExport(Value);var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource<IPreparedDisplayPreferencesExport>(TaskCreationOptions.RunContinuationsAsynchronously);int prepares=0;
        var first=transfer.ExportAsync(preview,(_,_)=>{prepares++;ready.SetResult();return release.Task;});await ready.Task;var second=await transfer.ExportAsync(preview,(_,_)=>throw new InvalidOperationException("Must not prepare twice."));Assert.Equal(DisplayPreferencesExportStatus.Busy,second.Status);Assert.Equal(1,prepares);var prepared=new CleanupFailure();release.SetResult(prepared);var result=await first;Assert.True(prepared.Published);Assert.Equal(DisplayPreferencesExportStatus.PublishedCleanupUnconfirmed,result.Status);Assert.Equal(DisplayPreferencesIssue.Cleanup,result.Issue);
    }
    private sealed class Wrapped(IPreparedDisplayPreferencesExport inner,Action after):IPreparedDisplayPreferencesExport
    {public void Publish(){inner.Publish();after();}public void Dispose()=>inner.Dispose();}
    private sealed class CleanupFailure:IPreparedDisplayPreferencesExport
    {public bool Published;public void Publish()=>Published=true;public void Dispose()=>throw new IOException("PUBLIC cleanup failure");}
}
