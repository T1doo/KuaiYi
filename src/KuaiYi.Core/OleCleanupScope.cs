using System.Runtime.ExceptionServices;

namespace KuaiYi.Core;

public sealed class OleCleanupException(IEnumerable<Exception> errors) : AggregateException("Native cleanup is not confirmed.", errors);

/// <summary>Apartment-affine cleanup. A failed attempt is never retried or reported as completed.</summary>
public sealed class OleCleanupLease(Action release) : IDisposable
{
    private readonly int thread = Environment.CurrentManagedThreadId;
    private bool attempted;
    private Exception? failure;
    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != thread) throw new InvalidOperationException("Cleanup crossed apartments.");
        if (attempted) { if (failure is not null) throw new AggregateException("Previous cleanup failed.", failure); return; }
        attempted = true;
        try { release(); }
        catch (Exception error) { failure = error; throw; }
    }
}

public sealed class OleCleanupScope : IDisposable
{
    private readonly int thread = Environment.CurrentManagedThreadId;
    private readonly List<OleCleanupLease> resources = [];
    private bool attempted;
    private OleCleanupException? failure;
    public bool CleanupCompleted { get; private set; }
    public T Run<T>(Func<T> work)
    {
        T result = default!; Exception? workError = null;
        try { result = work(); } catch (Exception error) { workError = error; }
        try { Dispose(); }
        catch (OleCleanupException cleanup)
        { if (workError is not null) throw new OleCleanupException(cleanup.InnerExceptions.Append(workError)); throw; }
        if (workError is not null) ExceptionDispatchInfo.Capture(workError).Throw();
        return result;
    }
    public OleCleanupLease Own(Action release)
    {
        if (attempted || Environment.CurrentManagedThreadId != thread) throw new InvalidOperationException("Invalid cleanup ownership.");
        var lease = new OleCleanupLease(release); resources.Add(lease); return lease;
    }
    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != thread) throw new InvalidOperationException("Cleanup scope crossed apartments.");
        if (attempted) { if (failure is not null) throw failure; return; }
        attempted = true; List<Exception> errors = [];
        for (int i = resources.Count - 1; i >= 0; i--) { try { resources[i].Dispose(); } catch (Exception error) { errors.Add(error); } }
        if (errors.Count > 0) { failure = new(errors); throw failure; }
        CleanupCompleted = true;
    }
}
