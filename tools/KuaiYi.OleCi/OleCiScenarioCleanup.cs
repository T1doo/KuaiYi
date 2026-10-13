using System.Runtime.ExceptionServices;

namespace KuaiYi.App;

// CI-only ownership of input sent by this test. Never releases an unowned press.
internal sealed class OleCiOwnedPress(Action down, Action up)
{
    public bool IsHeld { get; private set; }
    public void Down()
    {
        if (IsHeld) throw new InvalidOperationException("Test already owns a press.");
        down(); IsHeld=true;
    }
    public bool Release()
    {
        if (!IsHeld) return false;
        up(); IsHeld=false; return true;
    }
}
internal static class OleCiScenarioCleanup
{
    internal static async Task CompleteAsync(Exception? primary, Action release, Func<ValueTask> cleanup, Action<Exception> observe)
    {
        ExceptionDispatchInfo? first=null;
        void Record(Exception error)
        {
            first??=ExceptionDispatchInfo.Capture(error);
            try { observe(error); }
            catch { /* A diagnostic sink cannot replace the retained primary/cleanup failure. */ }
        }
        try { release(); } catch(Exception error) { Record(error); }
        try { await cleanup(); } catch(Exception error) { Record(error); }
        // The caller's catch/throw is still unwinding through finally. Let it preserve its stack.
        if(primary==null)first?.Throw();
    }
}
