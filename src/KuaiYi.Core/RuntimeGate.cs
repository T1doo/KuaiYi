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
public sealed partial class RuntimeGate
{
    private readonly bool requireProfileBinding;
    public RuntimeGate(bool requireProfileBinding=false){this.requireProfileBinding=requireProfileBinding;}
    private readonly object sync = new();
    private readonly HashSet<EndpointOrigin> consented = [];
    private CancellationTokenSource? activeCancellation;
    private RequestIdentity? active;
    private long epoch;
    private Activation activation;
    private bool localOnly;
    private Guid? explicitCaptureOwner;
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
    public bool HasConsentTo(EndpointOrigin origin){lock(sync)return !string.IsNullOrEmpty(origin.Value)&&consented.Contains(origin);}

    public bool TryEnable(EndpointOrigin origin, bool configurationValid)
    {
        lock (sync)
        {
            if (!configurationValid || !consented.Contains(origin) || suspension.HasFlag(Suspension.SessionLocked)) return false;
            activation = Activation.On;
            localOnly = false;
            if (presentation == Presentation.Hidden) presentation = Presentation.Compact;
            return true;
        }
    }

    /// <summary>Explicit local operation consent. Does not authorize any network endpoint or provider send.</summary>
    public bool TryEnableLocalOperation(bool configurationValid)
    {
        lock (sync)
        {
            if (!configurationValid || suspension.HasFlag(Suspension.SessionLocked)) return false;
            activation = Activation.On;
            localOnly = true;
            if (presentation == Presentation.Hidden) presentation = Presentation.Compact;
            return true;
        }
    }

    /// <summary>Only after an explicit per-operation desktop decision. Epoch prevents a pending prompt overriding Off/lock/replacement.
    /// Local acceptance never adds endpoint consent or starts a provider send.</summary>
    public bool TryStartConfirmedLocalRequest(long expectedEpoch, Guid sessionId, Guid versionId,
        out RequestIdentity identity, out CancellationToken cancellation)
    {
        CancellationTokenSource? previous;
        lock (sync)
        {
            identity = default; cancellation = default;
            if (epoch != expectedEpoch || suspension != Suspension.None || sessionId == Guid.Empty || versionId == Guid.Empty) return false;
            previous = activeCancellation;
            activeCancellation = new();
            activation = Activation.On;
            localOnly = true;
            if (presentation == Presentation.Hidden) presentation = Presentation.Compact;
            identity = new(sessionId, versionId, Guid.NewGuid(), ++epoch);
            active = identity; cancellation = activeCancellation.Token; manuallyCollapsedRequest = null;
            activeProfileBinding = null;
        }
        previous?.Cancel(); previous?.Dispose(); return true;
    }

    /// <summary>Connection cleanup cannot cancel a newer request.</summary>
    public void StopRequest(RequestIdentity identity)
    {
        CancellationTokenSource? cancellation;
        lock (sync)
        {
            if (active != identity) return;
            active = null; cancellation = activeCancellation; activeCancellation = null; epoch++;
            if (localOnly) { activation = Activation.Off; presentation = Presentation.Compact; }
        }
        cancellation?.Cancel(); cancellation?.Dispose();
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

    public bool TryBeginExplicitCapture(out Guid owner,out long captureEpoch,out Activation initial)
    {
        lock(sync)
        {
            owner=default;captureEpoch=epoch;initial=activation;
            if(suspension!=Suspension.None||explicitCaptureOwner!=null)return false;
            owner=Guid.NewGuid();explicitCaptureOwner=owner;suspension|=Suspension.Capture;captureEpoch=++epoch;return true;
        }
    }
    public bool IsExplicitCaptureCurrent(Guid owner,long captureEpoch,Activation initial)
    {lock(sync)return explicitCaptureOwner==owner&&epoch==captureEpoch&&activation==initial&&suspension==Suspension.Capture;}
    public bool TryDisableExplicitCapture(Guid owner,long captureEpoch,Activation initial)
    {
        CancellationTokenSource? cancellation;
        lock(sync)
        {
            if(explicitCaptureOwner!=owner||epoch!=captureEpoch||activation!=initial||suspension!=Suspension.Capture)return false;
            activation=Activation.Off;epoch++;active=null;cancellation=activeCancellation;activeCancellation=null;presentation=Presentation.Compact;
        }
        cancellation?.Cancel();cancellation?.Dispose();return true;
    }
    public void EndExplicitCapture(Guid owner)
    {
        lock(sync)
        {
            if(explicitCaptureOwner!=owner)return;
            explicitCaptureOwner=null;suspension&=~Suspension.Capture;epoch++;
        }
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
            return activation == Activation.On && !localOnly && mode == expectedMode && suspension == Suspension.None;
        }
    }

    public bool IsCaptureCurrent(CaptureTicket ticket)
    {
        lock (sync) return activation == Activation.On && !localOnly && suspension == Suspension.None && ticket.Epoch == epoch && ticket.Mode == mode;
    }

    public bool TryStartRequest(Guid sessionId, Guid versionId, CaptureTicket? passiveTicket,
        out RequestIdentity identity, out CancellationToken cancellation)
        =>TryStartRequestCore(sessionId,versionId,passiveTicket,null,out identity,out cancellation);

    private bool TryStartRequestCore(Guid sessionId,Guid versionId,CaptureTicket? passiveTicket,
        RequestProfileBinding? binding,out RequestIdentity identity,out CancellationToken cancellation)
    {
        CancellationTokenSource? previous;
        lock (sync)
        {
            identity = default;
            cancellation = default;
            if (sessionId == Guid.Empty || versionId == Guid.Empty || activation != Activation.On ||
                suspension.HasFlag(Suspension.SessionLocked)) return false;
            if(requireProfileBinding&&!localOnly&&binding==null)return false;
            if(binding is {} selected&&(localOnly||string.IsNullOrEmpty(selected.ProfileId)||selected.BindingId==Guid.Empty||!profileBindings.TryGetValue(selected.ProfileId,out var currentBinding)||currentBinding!=selected))return false;
            if (passiveTicket is { } ticket && (localOnly || ticket.Epoch != epoch || ticket.Mode != mode || suspension != Suspension.None)) return false;
            previous = activeCancellation;
            activeCancellation = new CancellationTokenSource();
            identity = new RequestIdentity(sessionId, versionId, Guid.NewGuid(), ++epoch);
            cancellation = activeCancellation.Token;
            active = identity;
            activeProfileBinding = binding;
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

    /// <summary>Create a caller-owned cancellation link under the identity lock, before the source can be replaced/disposed.</summary>
    public bool TryLinkRequestCancellation(RequestIdentity identity, CancellationToken caller, out CancellationTokenSource? linked)
    {
        lock (sync)
        {
            linked = null;
            if (activation != Activation.On || active != identity || suspension.HasFlag(Suspension.SessionLocked) ||
                activeCancellation is not { IsCancellationRequested: false }) return false;
            linked = CancellationTokenSource.CreateLinkedTokenSource(activeCancellation.Token, caller);
            return true;
        }
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
        lock (sync) return activation == Activation.On && !localOnly && HasSendBindingLocked() && active == identity && consented.Contains(actualOrigin) &&
            !suspension.HasFlag(Suspension.SessionLocked) && activeCancellation is { IsCancellationRequested: false };
    }

    /// <summary>Serialize the final eligibility check with starting an asynchronous send. Never await within this callback.</summary>
    public bool TryBeginSend<T>(RequestIdentity identity, EndpointOrigin actualOrigin, Func<Task<T>> begin, out Task<T>? task)
    {
        ArgumentNullException.ThrowIfNull(begin);
        lock (sync)
        {
            task = null;
            if (activation != Activation.On || localOnly || !HasSendBindingLocked() || active != identity || !consented.Contains(actualOrigin) ||
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
    public bool TryPeek(bool show)
    {
        lock(sync)
        {if(suspension.HasFlag(Suspension.SessionLocked)||presentation==Presentation.Hidden)return false;if(show&&presentation!=Presentation.Compact||!show&&presentation!=Presentation.Peek)return false;presentation=show?Presentation.Peek:Presentation.Compact;return true;}
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
        =>TryReopen(hasCachedResult);
    public bool TryReopen(bool hasCachedResult)
    {
        lock (sync) { if(!hasCachedResult||presentation==Presentation.Hidden||suspension.HasFlag(Suspension.SessionLocked))return false;presentation = Presentation.Expanded;return true; }
    }
}
