namespace KuaiYi.Core;

public enum Activation { Off, On }
public enum TriggerMode { Selection, Drop }
public enum Presentation { Compact, Peek, Expanding, Expanded, Collapsing, Hidden }
[Flags]
public enum Suspension { None = 0, Settings = 1, Capture = 2, Moving = 4, SessionLocked = 8 }

public readonly record struct CaptureTicket(long Epoch, TriggerMode Mode);
public readonly record struct RequestIdentity(Guid SessionId, Guid VersionId, Guid RequestId, long Epoch);

/// <summary>Canonical endpoint origins; userinfo and non-HTTP schemes are rejected before consent.</summary>
public readonly record struct EndpointOrigin
{
    public string Value { get; }
    public EndpointOrigin(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme is not ("https" or "http") || endpoint.UserInfo.Length != 0)
            throw new ArgumentException("Endpoint must be an absolute HTTP(S) URI without userinfo.", nameof(endpoint));
        Value = endpoint.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped).ToLowerInvariant();
    }
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Thread-safe eligibility and late-event gate. Platform acquisition and each provider send must check it.</summary>
public sealed class RuntimeGate
{
    private readonly object sync = new();
    private readonly HashSet<EndpointOrigin> consented = [];
    private CancellationTokenSource? activeCancellation;
    private RequestIdentity? active;
    private long epoch;
    private Activation activation;
    private TriggerMode mode;
    private Suspension suspension;
    private Presentation presentation;
    private Guid? manuallyCollapsedRequest;

    public (Activation Activation, TriggerMode Mode, Suspension Suspension, Presentation Presentation, long Epoch) Snapshot
    {
        get { lock (sync) return (activation, mode, suspension, presentation, epoch); }
    }

    public void ConsentTo(EndpointOrigin origin)
    {
        if (string.IsNullOrEmpty(origin.Value)) throw new ArgumentException("Missing origin.", nameof(origin));
        lock (sync) consented.Add(origin);
    }

    public bool TryEnable(EndpointOrigin origin, bool configurationValid)
    {
        lock (sync)
        {
            if (!configurationValid || !consented.Contains(origin) || suspension.HasFlag(Suspension.SessionLocked)) return false;
            activation = Activation.On;
            if (presentation == Presentation.Hidden) presentation = Presentation.Compact;
            return true;
        }
    }

    public void Disable(bool hide = false)
    {
        CancellationTokenSource? cancellation;
        lock (sync)
        {
            activation = Activation.Off;
            epoch++;
            active = null;
            cancellation = activeCancellation;
            activeCancellation = null;
            presentation = hide ? Presentation.Hidden : Presentation.Compact;
        }
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    public void SetSessionLocked(bool locked)
    {
        lock (sync)
        {
            if (locked) suspension |= Suspension.SessionLocked;
            else suspension &= ~Suspension.SessionLocked;
        }
        // Lock cancels; unlock never restores On.
        if (locked) Disable(hide: true);
        else { lock (sync) presentation = Presentation.Compact; }
    }

    public void SetSuspension(Suspension reason, bool suspended)
    {
        if (reason == Suspension.None || reason.HasFlag(Suspension.SessionLocked))
            throw new ArgumentException("Use SetSessionLocked for session changes.", nameof(reason));
        lock (sync)
        {
            suspension = suspended ? suspension | reason : suspension & ~reason;
            epoch++; // Every change invalidates pending acquisition, while existing request identity remains valid.
        }
    }

    public void SetMode(TriggerMode next)
    {
        if (!Enum.IsDefined(next)) throw new ArgumentOutOfRangeException(nameof(next));
        lock (sync) { if (mode != next) { mode = next; epoch++; } }
    }

    public bool TryBeginPassiveCapture(TriggerMode expectedMode, out CaptureTicket ticket)
    {
        lock (sync)
        {
            ticket = new CaptureTicket(epoch, mode);
            return activation == Activation.On && mode == expectedMode && suspension == Suspension.None;
        }
    }

    public bool IsCaptureCurrent(CaptureTicket ticket)
    {
        lock (sync) return activation == Activation.On && suspension == Suspension.None && ticket.Epoch == epoch && ticket.Mode == mode;
    }

    public bool TryStartRequest(Guid sessionId, Guid versionId, CaptureTicket? passiveTicket,
        out RequestIdentity identity, out CancellationToken cancellation)
    {
        CancellationTokenSource? previous;
        lock (sync)
        {
            identity = default;
            cancellation = default;
            if (sessionId == Guid.Empty || versionId == Guid.Empty || activation != Activation.On ||
                suspension.HasFlag(Suspension.SessionLocked)) return false;
            if (passiveTicket is { } ticket && (ticket.Epoch != epoch || ticket.Mode != mode || suspension != Suspension.None)) return false;
            previous = activeCancellation;
            activeCancellation = new CancellationTokenSource();
            identity = new RequestIdentity(sessionId, versionId, Guid.NewGuid(), ++epoch);
            cancellation = activeCancellation.Token;
            active = identity;
            manuallyCollapsedRequest = null;
        }
        previous?.Cancel();
        previous?.Dispose();
        return true;
    }

    public bool CanApply(RequestIdentity identity)
    {
        lock (sync) return activation == Activation.On && active == identity && !suspension.HasFlag(Suspension.SessionLocked);
    }

    /// <summary>Final destination-thread check and bounded synchronous application, serialized with identity replacement.</summary>
    public bool TryApply(RequestIdentity identity, Action apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        lock (sync)
        {
            if (activation != Activation.On || active != identity || suspension.HasFlag(Suspension.SessionLocked)) return false;
            // Caller is already on its destination thread: do not block, await or dispatch inside this callback.
            apply();
            return true;
        }
    }

    public bool CanSend(RequestIdentity identity, EndpointOrigin actualOrigin)
    {
        lock (sync) return activation == Activation.On && active == identity && consented.Contains(actualOrigin) &&
            !suspension.HasFlag(Suspension.SessionLocked) && activeCancellation is { IsCancellationRequested: false };
    }

    /// <summary>Serialize the final eligibility check with starting an asynchronous send. Never await within this callback.</summary>
    public bool TryBeginSend<T>(RequestIdentity identity, EndpointOrigin actualOrigin, Func<Task<T>> begin, out Task<T>? task)
    {
        ArgumentNullException.ThrowIfNull(begin);
        lock (sync)
        {
            task = null;
            if (activation != Activation.On || active != identity || !consented.Contains(actualOrigin) ||
                suspension.HasFlag(Suspension.SessionLocked) || activeCancellation is not { IsCancellationRequested: false }) return false;
            task = begin();
            return true;
        }
    }

    public void StopGeneration()
    {
        CancellationTokenSource? cancellation;
        lock (sync)
        {
            active = null;
            cancellation = activeCancellation;
            activeCancellation = null;
            epoch++;
        }
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    public void Collapse()
    {
        lock (sync) { presentation = Presentation.Compact; manuallyCollapsedRequest = active?.RequestId; }
    }

    public bool TryRevealDelta(RequestIdentity identity)
    {
        lock (sync)
        {
            if (activation != Activation.On || active != identity || manuallyCollapsedRequest == identity.RequestId) return false;
            presentation = Presentation.Expanding;
            return true;
        }
    }

    public void Reopen(bool hasCachedResult)
    {
        lock (sync) { if (hasCachedResult) presentation = Presentation.Expanded; }
    }
}
