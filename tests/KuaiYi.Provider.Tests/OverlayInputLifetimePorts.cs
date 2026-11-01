namespace KuaiYi.App;
// Explicit ports around the production close coordinator, not a native hook/COM substitute.
internal sealed partial class ShellWindow
{
    internal void InputOwnerForTest(Func<Task> cleanup,Func<bool>? ready=null){overlayInputCleanup=cleanup;overlayInputReady=ready;}
}
