namespace KuaiYi.Core;
public enum OverlayTransition{Compact,Opening,Expanded,Closing,Moving}
public static class OverlayDragGeometry
{
    public static (PixelRect Anchor,PixelRect Card) Move(PixelRect anchor,PixelRect card,PixelPoint desired,DpiScale oldDpi,MonitorGeometry monitor)
    {
        if(!card.Contains(anchor))throw new ArgumentException("Card must contain anchor.");
        var work=monitor.WorkArea.Inset(monitor.Dpi.CeilingPixels(12));int size=monitor.Dpi.CeilingPixels(48);
        if(work.Width<size||work.Height<size)throw new ArgumentException("No feasible anchor.");
        (int First,int Second) Extents(int first,int second,int capacity)
        {
            double a=first/oldDpi.Factor*monitor.Dpi.Factor,b=second/oldDpi.Factor*monitor.Dpi.Factor;
            double scale=a+b>capacity?capacity/(a+b):1;
            int left=(int)Math.Round(a*scale),right=Math.Min(capacity-left,(int)Math.Round(b*scale));return(left,right);
        }
        var x=Extents(anchor.Left-card.Left,card.Right-anchor.Right,work.Width-size);
        var y=Extents(anchor.Top-card.Top,card.Bottom-anchor.Bottom,work.Height-size);
        int left=Math.Clamp(desired.X,work.Left+x.First,work.Right-size-x.Second),top=Math.Clamp(desired.Y,work.Top+y.First,work.Bottom-size-y.Second);
        var moved=new PixelRect(left,top,left+size,top+size);
        return(moved,new(left-x.First,top-y.First,left+size+x.Second,top+size+y.Second));
    }
}
/// <summary>Actual production animation geometry; the WPF adapter supplies measured text height and frame time.</summary>
public sealed class OverlayFrames
{
    public PixelRect Anchor{get;private set;}public PixelRect Current{get;private set;}public MonitorGeometry Monitor{get;private set;}
    public LayoutPlan? Plan{get;private set;}public OverlayTransition Transition{get;private set;}
    private PixelRect from,target;private double elapsed,height,movingOpacity,movingTextWidth,fromOpacity;private int measured;private bool wantsExpanded,toolbarPlan;private GrowthDirection? movingDirection;private LayoutPlan? lastPlan;
    public GrowthDirection? ContentDirection=>Plan?.Direction??movingDirection;
    public double TextWidthDip=>Plan is {} p?p.Dpi.Dips(p.Target.Width):movingTextWidth;
    public bool NeedsFrame=>Transition is OverlayTransition.Opening or OverlayTransition.Closing||Transition==OverlayTransition.Expanded&&Current!=target;
    public bool IsExpanded=>wantsExpanded;
    public double ContentOpacity=>Transition switch{OverlayTransition.Compact=>0,OverlayTransition.Moving=>movingOpacity,OverlayTransition.Closing=>fromOpacity*Math.Clamp(1-elapsed/80,0,1),OverlayTransition.Opening=>fromOpacity+(1-fromOpacity)*Math.Clamp(elapsed/80,0,1),_=>1};
    public OverlayFrames(PixelRect anchor,MonitorGeometry monitor){Anchor=Current=anchor;Monitor=monitor;Validate();}
    public void Open(int measuredHeight,LayoutPreferences preferences)
        =>Open(measuredHeight,BodyPlan(preferences));
    public LayoutPlan BodyPlan(LayoutPreferences preferences)=>OverlayLayoutPlanner.Plan(Anchor,Monitor,preferences,previous:toolbarPlan?lastPlan:Plan??lastPlan);
    public void OpenMeasured(LayoutPreferences preferences,Func<LayoutPlan,int> measure)
    {ArgumentNullException.ThrowIfNull(measure);var plan=BodyPlan(preferences);Open(measure(plan),plan);}
    private void Open(int measuredHeight,LayoutPlan plan)
    {
        double opacity=ContentOpacity;wantsExpanded=true;measured=Math.Max(Anchor.Height,measuredHeight);Plan=plan;lastPlan=Plan;toolbarPlan=false;
        target=HeightRect(Math.Clamp(measured,Anchor.Height,Plan.MaximumHeightPixels));Begin(OverlayTransition.Opening,opacity);
    }
    public void OpenToolbar(double widthDip=220)
    {
        wantsExpanded=true;toolbarPlan=true;measured=Anchor.Height;Plan=OverlayLayoutPlanner.Plan(Anchor,Monitor,new(widthDip,144));
        int width=Math.Min(Plan.Target.Width,Math.Max(Anchor.Width,Monitor.Dpi.CeilingPixels(widthDip)));
        bool right=Plan.Direction is GrowthDirection.RightDown or GrowthDirection.RightUp;
        bool balanced=Plan.Direction is GrowthDirection.BalancedDown or GrowthDirection.BalancedUp;
        int left=balanced?Math.Clamp(Anchor.Center.X-width/2,Plan.SafeWorkArea.Left,Plan.SafeWorkArea.Right-width):right?Anchor.Left:Anchor.Right-width;
        Plan=Plan with{Target=new(left,Anchor.Top,left+width,Anchor.Bottom)};target=Plan.Target;Begin(OverlayTransition.Opening);
    }
    public void Grow(int measuredHeight)
    {
        measured=Math.Max(measured,measuredHeight);
        if(!wantsExpanded||Plan==null||Transition==OverlayTransition.Moving)return;
        if(Transition==OverlayTransition.Expanded)target=HeightRect(Math.Clamp(measured,Anchor.Height,Plan.MaximumHeightPixels));
    }
    public void Collapse(){double opacity=ContentOpacity;wantsExpanded=false;target=Anchor;Begin(OverlayTransition.Closing,opacity);}
    public void Compact(){wantsExpanded=false;Plan=null;Current=target=Anchor;Transition=OverlayTransition.Compact;elapsed=0;Validate();}
    public void BeginMove(){movingOpacity=ContentOpacity;movingDirection=Plan?.Direction;movingTextWidth=TextWidthDip;Transition=OverlayTransition.Moving;from=Current;}
    public void Move(PixelPoint desired,MonitorGeometry monitor)
    {
        var moved=OverlayDragGeometry.Move(Anchor,Current,desired,Monitor.Dpi,monitor);Anchor=moved.Anchor;Current=moved.Card;Monitor=monitor;Plan=null;Transition=OverlayTransition.Moving;Validate();
    }
    public void EndMove(LayoutPreferences preferences,int? measuredHeight=null)
    {if(measuredHeight.HasValue)measured=measuredHeight.Value;if(wantsExpanded)Open(measured,preferences);else{target=Anchor;Begin(OverlayTransition.Closing);}}
    public void Reanchor(PixelRect anchor,MonitorGeometry monitor,LayoutPreferences preferences)
    {
        // All old animated edges are discarded before changing coordinate systems.
        Anchor=Current=anchor;Monitor=monitor;Plan=null;Transition=OverlayTransition.Compact;Validate();
        if(wantsExpanded)Open(measured,preferences);
    }
    public PixelRect Tick(TimeSpan delta,bool reduceMotion=false)
    {
        if(delta<TimeSpan.Zero||!double.IsFinite(delta.TotalMilliseconds))throw new ArgumentOutOfRangeException(nameof(delta));
        if(Transition==OverlayTransition.Moving)return Current;
        if(Transition is OverlayTransition.Opening or OverlayTransition.Closing)
        {
            double duration=Transition==OverlayTransition.Opening?220:180;elapsed+=delta.TotalMilliseconds;double t=reduceMotion?1:Math.Clamp(elapsed/duration,0,1);
            double eased=Transition==OverlayTransition.Opening?1-Math.Pow(1-t,3):t*t*(3-2*t);
            int Edge(int first,int last)=>checked(first+(int)Math.Round((last-(double)first)*eased));
            Current=new(Edge(from.Left,target.Left),Edge(from.Top,target.Top),Edge(from.Right,target.Right),Edge(from.Bottom,target.Bottom));
            if(t==1){Current=target;height=Current.Height;Transition=wantsExpanded?OverlayTransition.Expanded:OverlayTransition.Compact;if(wantsExpanded&&Plan!=null)target=HeightRect(Math.Clamp(measured,Anchor.Height,Plan.MaximumHeightPixels));}
        }
        else if(Transition==OverlayTransition.Expanded&&Current!=target)
        {
            height+=(target.Height-height)*(reduceMotion?1:1-Math.Exp(-delta.TotalMilliseconds/70));if(Math.Abs(target.Height-height)<Monitor.Dpi.Factor*.5)height=target.Height;
            Current=HeightRect((int)Math.Round(height));
        }
        Validate();return Current;
    }
    private void Begin(OverlayTransition next,double? opacity=null){fromOpacity=opacity??ContentOpacity;from=Current;elapsed=0;Transition=next;Validate();}
    private PixelRect HeightRect(int h)
    {
        var plan=Plan??throw new InvalidOperationException("Missing plan.");bool down=plan.Direction is GrowthDirection.RightDown or GrowthDirection.LeftDown or GrowthDirection.BalancedDown;
        return new(plan.Target.Left,down?Anchor.Top:Anchor.Bottom-h,plan.Target.Right,down?Anchor.Top+h:Anchor.Bottom);
    }
    private void Validate(){if(!Current.Contains(Anchor)||!Monitor.WorkArea.Inset(Monitor.Dpi.CeilingPixels(12)).Contains(Current))throw new InvalidOperationException("Anchor/card/work-area invariant violated.");}
}
