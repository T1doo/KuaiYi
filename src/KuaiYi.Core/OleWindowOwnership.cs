namespace KuaiYi.Core;

/// <summary>One HWND generation: framework handoff, exclusive native registration, then close.</summary>
public sealed class OleWindowOwnership(long hwnd)
{
    private readonly object sync = new();
    private bool attempted, transferFinished, prepared, closed;
    private Lease? current;
    public long Hwnd { get; } = hwnd != 0 ? hwnd : throw new ArgumentOutOfRangeException(nameof(hwnd));

    // Only the creating Window may supply this action, before publishing SourceInitialized.
    public void TransferFrameworkRegistration(Action release)
    {
        lock (sync)
        {
            if (attempted || closed) throw new InvalidOperationException("Window registration handoff is one-shot.");
            attempted = true;
            try { release(); prepared = true; }
            finally { transferFinished = true; }
        }
    }
    private bool Settled => current == null || current.CleanupConfirmed && current.Original.IsCompletedSuccessfully;
    public Lease Reserve(long handle, Task original)
    {
        lock (sync)
        {
            if (!prepared || closed || handle != Hwnd || !Settled || original.IsCompleted)
                throw new InvalidOperationException("Window registration ownership is unavailable.");
            return current = new Lease(this, original);
        }
    }
    public bool TryClose()
    {
        lock (sync)
        {
            // A failed initial handoff has never admitted a custom registrar. WPF may close
            // and clean up its original registration; a reentrant close during handoff may not.
            if (!transferFinished || !Settled) return false;
            closed = true;
            return true;
        }
    }
    public void Invalidate() { lock (sync) closed = true; }
    public sealed class Lease
    {
        private readonly OleWindowOwnership owner;
        internal Task Original { get; }
        internal bool CleanupConfirmed { get; private set; }
        private bool reported;
        internal Lease(OleWindowOwnership owner, Task original) { this.owner = owner; Original = original; }
        public void ConfirmCleanup(bool success)
        {
            lock (owner.sync)
            {
                if (reported) throw new InvalidOperationException("Cleanup result is immutable.");
                reported = true;
                CleanupConfirmed = success;
            }
        }
    }
}
