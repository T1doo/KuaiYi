namespace KuaiYi.App;
// The shared production menu formatter, not a WPF visibility or keyboard fixture.
internal sealed partial class ShellWindow
{
    internal string RetryAdviceMenuForTest(Guid id)=>VersionMenuLabel(overlayResults.Versions.First(v=>v.Identity.VersionId==id));
}
