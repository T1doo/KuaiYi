using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.Windows;

namespace KuaiYi.GrowthHarness;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if(args.Length==2&&args[0]=="--ci-actual-overlay")return ActualOverlayProbe.Run(args[1],"2400",boundedCi:true);
        if(args.Length is 2 or 3&&args[0]=="--actual-overlay")return ActualOverlayProbe.Run(args[1],args.Length==3?args[2]:"240");
        string output = args.Length == 1 ? Path.GetFullPath(args[0]) : Path.GetFullPath("artifacts/growth-probe.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var application = new Application();
        application.Run(new ProbeWindow(output));
        return 0;
    }
}

/// <summary>Explicit P0 visual experiment with public fixture text; no capture or provider connection.</summary>
internal sealed class ProbeWindow : Window
{
    private const string Fixture = "快译 P0 浮窗探针：这里只输出公开测试文本，不读取任何外部内容，也不连接模型。\n\n" +
        "按钮保持在原来的屏幕位置，面板沿可用方向展开。长段落用于观察固定文本宽度、流式高度增长、滚动和屏幕边缘。";
    private readonly Canvas canvas = new() { ClipToBounds = true };
    private readonly TextBlock text = new() { TextWrapping = TextWrapping.Wrap, FontSize = 14, Foreground = Brushes.DarkSlateGray };
    private readonly ScrollViewer scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly Button anchorButton = new() { Width = 48, Height = 48, Content = "译", FontSize = 18, Background = Brushes.Firebrick, Foreground = Brushes.White, ToolTip = "P0 探针：单击启停公开测试流；右键打开操作菜单" };
    private readonly DispatcherTimer streamTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly string evidencePath;
    private readonly Queue<string> frames = new();
    private int discardedFrames;
    private readonly StringBuilder buffer = new();
    private readonly nint initialForeground;
    private nint hwnd;
    private PixelRect anchor, current;
    private LayoutPlan? plan, previous;
    private GrowthController? growth;
    private double transitionStart, lastFrame;
    private bool on, expanded, opening, collapsing, rendering, reading, moving, suppressClick;
    private PixelPoint dragStart;
    private PixelRect dragAnchor;
    private int fixtureOffset;
    private string PublicStream => string.Join("\n\n", Enumerable.Repeat(Fixture, 12));
    private bool ReduceMotion => !SystemParameters.ClientAreaAnimation;

    public ProbeWindow(string output)
    {
        evidencePath = output;
        initialForeground = NativeDesktop.GetForegroundWindow();
        Title = "KuaiYi P0 Growth Harness"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; Background = Brushes.White;
        Width = Height = 48; Content = canvas;
        scroll.Content = text; canvas.Children.Add(scroll); canvas.Children.Add(anchorButton);
        scroll.Visibility = Visibility.Collapsed;
        anchorButton.Click += (_, _) => { if (suppressClick) { suppressClick = false; return; } on = !on; anchorButton.Background = on ? Brushes.ForestGreen : Brushes.Firebrick; if (on) StartFixture(); else { streamTimer.Stop(); Collapse(); } };
        anchorButton.PreviewMouseLeftButtonDown += BeginDrag;
        anchorButton.PreviewMouseMove += Drag;
        anchorButton.PreviewMouseLeftButtonUp += (_, _) => { anchorButton.ReleaseMouseCapture(); moving = false; };
        var menu = new ContextMenu();
        void Item(string title, Action action) { var item = new MenuItem { Header = title }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        Item("收起（不关闭测试流）", Collapse);
        Item("重开内存中的公开结果", () => { if (buffer.Length > 0) Open(); });
        Item("重新生成公开测试流", () => { on = true; anchorButton.Background = Brushes.ForestGreen; StartFixture(); });
        Item("主动阅读 / 返回非激活", ToggleReading);
        Item("退出探针", Close);
        anchorButton.ContextMenu = menu;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && reading) { ToggleReading(); e.Handled = true; } };
        streamTimer.Tick += (_, _) => AppendFixture();
        SourceInitialized += (_, _) => InitializeNative();
        Closed += (_, _) =>
        {
            streamTimer.Stop(); StopRendering();
            File.WriteAllLines(evidencePath, new[] { "milliseconds,stage,dpi,left,top,right,bottom,anchorContained,workContained,nativeMatches,foregroundUnchanged,actualAnchorContained,actualWorkContained" }.Concat(frames), new UTF8Encoding(false));
            File.WriteAllText(evidencePath + ".summary.json", System.Text.Json.JsonSerializer.Serialize(new { retainedFrames = frames.Count, discardedFrames, desktopAcceptance = "NotEvaluated", source = "public-fixture-only" }));
        };
    }

    private void InitializeNative()
    {
        hwnd = new WindowInteropHelper(this).Handle;
        NativeDesktop.SetOverlayStyle(hwnd, false);
        NativeDesktop.GetCursorPos(out var point);
        var monitor = NativeDesktop.MonitorAt(new PixelPoint(point.X, point.Y));
        anchor = NativeDesktop.ClampAnchor(new(point.X, point.Y, point.X + 48, point.Y + 48), monitor);
        Apply(anchor, "compact", monitor);
        HwndSource.FromHwnd(hwnd)?.AddHook((nint window, int message, nint wparam, nint lparam, ref bool handled) =>
        {
            if (message is 0x02E0 or 0x007E) // DPI/display change invalidates the frozen geometry.
                Dispatcher.InvokeAsync(() => { streamTimer.Stop(); StopRendering(); expanded = opening = collapsing = false; scroll.Visibility = Visibility.Collapsed;
                    anchor = NativeDesktop.ClampAnchor(anchor, NativeDesktop.MonitorAt(anchor.Center)); Apply(anchor, "display-reset", NativeDesktop.MonitorAt(anchor.Center)); });
            return 0;
        });
    }

    private void StartFixture() { streamTimer.Stop(); buffer.Clear(); text.Text = ""; fixtureOffset = 0; Open(); }
    private void Open()
    {
        if (hwnd == 0) return;
        streamTimer.Stop(); StopRendering();
        var monitor = NativeDesktop.MonitorAt(anchor.Center);
        plan = OverlayLayoutPlanner.Plan(anchor, monitor, new(420, 144), previous: previous); previous = plan;
        growth = new(plan); text.Width = Math.Max(1, plan.Dpi.Dips(plan.Target.Width) - 32);
        Measure(); growth.Tick(TimeSpan.Zero, true);
        expanded = opening = true; collapsing = false; scroll.Visibility = Visibility.Visible;
        transitionStart = lastFrame = clock.Elapsed.TotalMilliseconds; StartRendering();
    }
    private void Collapse()
    {
        if (!expanded || plan is null) return;
        // Freeze from the last displayed rectangle, including a partially opened frame.
        plan = plan with { Target = current }; opening = false; collapsing = true;
        transitionStart = lastFrame = clock.Elapsed.TotalMilliseconds; StartRendering();
    }
    private void AppendFixture()
    {
        if (!on || fixtureOffset >= PublicStream.Length) { streamTimer.Stop(); return; }
        int count = Math.Min(12, PublicStream.Length - fixtureOffset);
        buffer.Append(PublicStream.AsSpan(fixtureOffset, count)); fixtureOffset += count; text.Text = buffer.ToString();
        if (expanded && !collapsing) { Measure(); StartRendering(); }
    }
    private void Measure()
    {
        if (plan is null || growth is null) return;
        text.Measure(new Size(text.Width, double.PositiveInfinity));
        growth.GrowToMeasuredHeight(plan.Dpi.CeilingPixels(48 + 24 + Math.Max(72, text.DesiredSize.Height)));
    }
    private void StartRendering() { if (rendering) return; lastFrame = clock.Elapsed.TotalMilliseconds; CompositionTarget.Rendering += Render; rendering = true; }
    private void StopRendering() { if (!rendering) return; CompositionTarget.Rendering -= Render; rendering = false; }
    private void Render(object? sender, EventArgs args)
    {
        if (plan is null || growth is null) { StopRendering(); return; }
        double now = clock.Elapsed.TotalMilliseconds;
        if (opening || collapsing)
        {
            double progress = ReduceMotion ? 1 : Math.Clamp((now - transitionStart) / (collapsing ? 180 : 220), 0, 1);
            double ease = 1 - Math.Pow(1 - progress, 3);
            Apply(OverlayLayoutPlanner.Frame(plan, collapsing ? 1 - ease : ease), collapsing ? "collapse" : "open", new(plan.MonitorId, plan.SafeWorkArea.Inset(-plan.Dpi.CeilingPixels(12)), plan.Dpi));
            if (progress == 1)
            {
                if (collapsing) { expanded = collapsing = false; scroll.Visibility = Visibility.Collapsed; StopRendering(); }
                else { opening = false; if (on && fixtureOffset < PublicStream.Length) streamTimer.Start(); }
            }
        }
        else
        {
            Apply(growth.Tick(TimeSpan.FromMilliseconds(Math.Max(0, now - lastFrame)), ReduceMotion), "grow", new(plan.MonitorId, plan.SafeWorkArea.Inset(-plan.Dpi.CeilingPixels(12)), plan.Dpi));
            if (growth.IsSettled) StopRendering();
        }
        lastFrame = now;
    }
    private void Apply(PixelRect rectangle, string stage, MonitorGeometry monitor)
    {
        current = rectangle;
        NativeDesktop.ApplyBounds(hwnd, rectangle, monitor.Dpi.CeilingPixels(stage == "compact" ? 24 : 14));
        Canvas.SetLeft(anchorButton, monitor.Dpi.Dips(anchor.Left - rectangle.Left)); Canvas.SetTop(anchorButton, monitor.Dpi.Dips(anchor.Top - rectangle.Top));
        if (plan is not null)
        {
            bool down = plan.Direction is GrowthDirection.RightDown or GrowthDirection.LeftDown or GrowthDirection.BalancedDown;
            int contentTop = down ? anchor.Bottom + plan.Dpi.CeilingPixels(12) : rectangle.Top + plan.Dpi.CeilingPixels(12);
            int contentBottom = down ? rectangle.Bottom - plan.Dpi.CeilingPixels(12) : anchor.Top - plan.Dpi.CeilingPixels(12);
            Canvas.SetLeft(scroll, monitor.Dpi.Dips(plan.Target.Left + plan.Dpi.CeilingPixels(12) - rectangle.Left));
            Canvas.SetTop(scroll, monitor.Dpi.Dips(contentTop - rectangle.Top)); scroll.Width = Math.Max(1, monitor.Dpi.Dips(plan.Target.Width) - 24);
            scroll.Height = Math.Max(0, monitor.Dpi.Dips(contentBottom - contentTop));
        }
        bool nativeMatches = NativeDesktop.GetWindowRect(hwnd, out var actual) && actual.ToPixels() == rectangle;
        PixelRect work = monitor.WorkArea.Inset(monitor.Dpi.CeilingPixels(12));
        frames.Enqueue(FormattableString.Invariant($"{clock.Elapsed.TotalMilliseconds:F3},{stage},{monitor.Dpi.Factor:F3},{rectangle.Left},{rectangle.Top},{rectangle.Right},{rectangle.Bottom},{rectangle.Contains(anchor)},{work.Contains(rectangle)},{nativeMatches},{NativeDesktop.GetForegroundWindow() == initialForeground},{actual.ToPixels().Contains(anchor)},{work.Contains(actual.ToPixels())}"));
        // Disk writes happen on close so they do not alter the animation timing. Diagnostics remain bounded.
        if (frames.Count > 100_000) { frames.Dequeue(); discardedFrames++; }
    }
    private void BeginDrag(object sender, MouseButtonEventArgs args)
    {
        if (!NativeDesktop.GetCursorPos(out var point)) return;
        dragStart = new(point.X, point.Y); dragAnchor = anchor; moving = suppressClick = false; anchorButton.CaptureMouse();
    }
    private void Drag(object sender, MouseEventArgs args)
    {
        if (!anchorButton.IsMouseCaptured || args.LeftButton != MouseButtonState.Pressed || !NativeDesktop.GetCursorPos(out var point)) return;
        int dx = point.X - dragStart.X, dy = point.Y - dragStart.Y;
        if (!moving && Math.Abs(dx) + Math.Abs(dy) < 5) return;
        moving = suppressClick = true; streamTimer.Stop(); StopRendering(); expanded = opening = collapsing = false; scroll.Visibility = Visibility.Collapsed;
        var moved = new PixelRect(dragAnchor.Left + dx, dragAnchor.Top + dy, dragAnchor.Right + dx, dragAnchor.Bottom + dy);
        var monitor = NativeDesktop.MonitorAt(moved.Center); anchor = NativeDesktop.ClampAnchor(moved, monitor); Apply(anchor, "drag", monitor);
    }
    private void ToggleReading()
    {
        reading = !reading; NativeDesktop.SetOverlayStyle(hwnd, reading);
        if (reading) { Activate(); scroll.Focus(); }
    }
}
