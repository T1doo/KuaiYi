namespace KuaiYi.Contracts;

public sealed record OleProbeControl(string Command, long Epoch, bool Enabled = false, int X = 0, int Y = 0);
public sealed record OleProbeEvent(Guid Instance, long Epoch, string Phase, string Status, uint Effect = 0,
    bool? AsyncAvailable = null, bool? AsyncMode = null, bool? StartSucceeded = null, int CodeUnits = 0,
    bool? ForegroundUnchanged = null, long Hwnd = 0, int ManagedThreadId = 0, string? Apartment = null, bool CleanupCompleted = false);
