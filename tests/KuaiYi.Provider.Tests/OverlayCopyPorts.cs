namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    // Only the OS clipboard call is replaced; the linked App command and label run unchanged.
    internal readonly List<string> ClipboardWrites=[];
    internal Action<string>? ClipboardWriteForTest;
    private void WriteResultClipboard(string text)
    {ClipboardWrites.Add(text);ClipboardWriteForTest?.Invoke(text);}
    internal void CopyResultForTest()=>CopyResult();
    internal void SelectCopyVersionForTest(Guid id)=>SelectResultVersion(id);
    internal void ExitCopyForTest()=>exiting=true;
    internal void AddCopyToolForTest()=>tools.Add(new(){Tag="复制",Content="复制"});
    internal bool CopyToolEnabled=>tools.Single(tool=>Equals(tool.Tag,"复制")).IsEnabled;
}
