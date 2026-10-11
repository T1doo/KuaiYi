namespace KuaiYi.Core;

public readonly record struct PixelPoint(int X, int Y);
public readonly record struct DipPoint(double X, double Y);
public readonly record struct DipRect(double Left, double Top, double Width, double Height);
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => checked(Right - Left);
    public int Height => checked(Bottom - Top);
    public PixelPoint Center => new(Left + Width / 2, Top + Height / 2);
    public bool Contains(PixelRect other) => other.Left >= Left && other.Top >= Top && other.Right <= Right && other.Bottom <= Bottom;
    public long OverlapArea(PixelRect other) => (long)Math.Max(0, Math.Min(Right, other.Right) - Math.Max(Left, other.Left)) *
        Math.Max(0, Math.Min(Bottom, other.Bottom) - Math.Max(Top, other.Top));
    public PixelRect Inset(int pixels) => new(checked(Left + pixels), checked(Top + pixels), checked(Right - pixels), checked(Bottom - pixels));
}

public readonly record struct DpiScale
{
    public double Factor { get; }
    public DpiScale(double factor)
    {
        if (!double.IsFinite(factor) || factor is < 0.5 or > 8) throw new ArgumentOutOfRangeException(nameof(factor));
        Factor = factor;
    }
    public int CeilingPixels(double dips) => checked((int)Math.Ceiling(dips * Factor));
    public double Dips(int pixels) => pixels / Factor;
}

public sealed record MonitorGeometry(string Id, PixelRect WorkArea, DpiScale Dpi);
public enum GrowthDirection { RightDown, LeftDown, RightUp, LeftUp, BalancedDown, BalancedUp }
public sealed record LayoutPreferences(double WidthDip = 420, double DesiredHeightDip = 640);
public sealed record LayoutPlan(string MonitorId, PixelRect SafeWorkArea, PixelRect Anchor, PixelRect Target,
    GrowthDirection Direction, DpiScale Dpi, int MaximumHeightPixels);

/// <summary>Pure planner. The caller must first put the anchor in a feasible safe work area.</summary>
public static class OverlayLayoutPlanner
{
    public static LayoutPlan Plan(PixelRect anchor, MonitorGeometry monitor, LayoutPreferences preferences,
        PixelRect? source = null, LayoutPlan? previous = null)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        ArgumentNullException.ThrowIfNull(preferences);
        if (monitor.Dpi.Factor == 0 || !double.IsFinite(preferences.WidthDip) || !double.IsFinite(preferences.DesiredHeightDip) ||
            preferences.WidthDip <= 0 || preferences.DesiredHeightDip <= 0) throw new ArgumentException("Invalid DPI/preferences.");
        PixelRect work = monitor.WorkArea.Inset(monitor.Dpi.CeilingPixels(12));
        if (anchor.Width <= 0 || anchor.Height <= 0 || work.Width <= 0 || work.Height <= 0 || !work.Contains(anchor))
            throw new ArgumentException("Anchor must be contained by a feasible safe work area.", nameof(anchor));
        int preferredWidth = Math.Max(anchor.Width, monitor.Dpi.CeilingPixels(Math.Clamp(preferences.WidthDip, 320, 600)));
        int totalHeightLimit = Math.Max(anchor.Height, Math.Min(monitor.Dpi.CeilingPixels(640), (int)Math.Floor(work.Height * 0.7)));
        int requestedHeight = Math.Max(anchor.Height, monitor.Dpi.CeilingPixels(preferences.DesiredHeightDip));
        List<LayoutPlan> candidates = [];
        foreach (GrowthDirection direction in Enum.GetValues<GrowthDirection>())
        {
            bool down = direction is GrowthDirection.RightDown or GrowthDirection.LeftDown or GrowthDirection.BalancedDown;
            bool right = direction is GrowthDirection.RightDown or GrowthDirection.RightUp;
            bool balanced = direction is GrowthDirection.BalancedDown or GrowthDirection.BalancedUp;
            int widthCapacity = balanced ? work.Width : right ? work.Right - anchor.Left : anchor.Right - work.Left;
            int heightCapacity = down ? work.Bottom - anchor.Top : anchor.Bottom - work.Top;
            int width = Math.Min(preferredWidth, widthCapacity);
            int maximumHeight = Math.Min(totalHeightLimit, heightCapacity);
            int height = Math.Min(requestedHeight, maximumHeight);
            // Balanced is a fallback only when neither side reaches the regular minimum width.
            if (balanced && (work.Right - anchor.Left >= monitor.Dpi.CeilingPixels(320) || anchor.Right - work.Left >= monitor.Dpi.CeilingPixels(320))) continue;
            int left = balanced ? Math.Clamp(anchor.Center.X - width / 2, work.Left, work.Right - width) : right ? anchor.Left : anchor.Right - width;
            int top = down ? anchor.Top : anchor.Bottom - height;
            PixelRect target = new(left, top, left + width, top + height);
            if (target.Contains(anchor) && work.Contains(target)) candidates.Add(new(monitor.Id, work, anchor, target, direction, monitor.Dpi, maximumHeight));
        }
        int minimumWidth = monitor.Dpi.CeilingPixels(320);
        int minimumHeight = anchor.Height + monitor.Dpi.CeilingPixels(96);
        bool Readable(LayoutPlan p) => p.Target.Width >= minimumWidth && p.Target.Height >= minimumHeight;
        var usable = candidates.Where(Readable).ToArray();
        IEnumerable<LayoutPlan> pool = usable.Length > 0 ? usable : candidates;
        LayoutPlan best = pool.OrderByDescending(p => (long)p.Target.Width * Math.Max(0, p.Target.Height - anchor.Height))
            .ThenBy(p => source is { } selected ? p.Target.OverlapArea(selected) : 0)
            .ThenByDescending(p => previous?.Direction == p.Direction)
            .ThenBy(p => (int)p.Direction).First();
        // Movement hysteresis applies only to unchanged monitor/work area and a still-readable previous direction.
        if (previous is not null && previous.MonitorId == monitor.Id && previous.SafeWorkArea == work)
        {
            LayoutPlan? old = candidates.FirstOrDefault(p => p.Direction == previous.Direction && Readable(p));
            int threshold = monitor.Dpi.CeilingPixels(32);
            if (old is not null && best.Target.Width - old.Target.Width < threshold && best.Target.Height - old.Target.Height < threshold) return old;
        }
        return best;
    }

    /// <summary>Interpolate approved outer edges; never round origin and size independently.</summary>
    public static PixelRect Frame(LayoutPlan plan, double progress)
    {
        if (!double.IsFinite(progress) || progress is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(progress));
        int Edge(int seed, int target) => checked(seed + (int)Math.Round((target - (double)seed) * progress, MidpointRounding.AwayFromZero));
        return new(Edge(plan.Anchor.Left, plan.Target.Left), Edge(plan.Anchor.Top, plan.Target.Top),
            Edge(plan.Anchor.Right, plan.Target.Right), Edge(plan.Anchor.Bottom, plan.Target.Bottom));
    }
}

/// <summary>Frame-driven, frozen-direction height follower. Text is measured at the target width by the UI.</summary>
public sealed class GrowthController
{
    private readonly LayoutPlan plan;
    private double height;
    private int targetHeight;
    public GrowthController(LayoutPlan plan)
    {
        this.plan = plan;
        height = plan.Anchor.Height;
        targetHeight = plan.Anchor.Height;
    }
    public bool IsSettled => Math.Abs(height - targetHeight) < plan.Dpi.Factor * 0.5;
    public void GrowToMeasuredHeight(int measuredHeightPixels) => targetHeight = Math.Max(targetHeight,
        Math.Clamp(measuredHeightPixels, plan.Anchor.Height, plan.MaximumHeightPixels));
    public PixelRect Tick(TimeSpan elapsed, bool reduceMotion = false)
    {
        if (elapsed < TimeSpan.Zero || !double.IsFinite(elapsed.TotalMilliseconds)) throw new ArgumentOutOfRangeException(nameof(elapsed));
        double alpha = reduceMotion ? 1 : 1 - Math.Exp(-elapsed.TotalMilliseconds / 70);
        height += (targetHeight - height) * alpha;
        if (IsSettled) height = targetHeight;
        int current = Math.Clamp((int)Math.Round(height), plan.Anchor.Height, plan.MaximumHeightPixels);
        bool down = plan.Direction is GrowthDirection.RightDown or GrowthDirection.LeftDown or GrowthDirection.BalancedDown;
        int top = down ? plan.Anchor.Top : plan.Anchor.Bottom - current;
        return new(plan.Target.Left, top, plan.Target.Right, top + current);
    }
}
