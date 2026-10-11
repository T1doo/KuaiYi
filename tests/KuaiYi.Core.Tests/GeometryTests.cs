using KuaiYi.Core;
using Xunit;

namespace KuaiYi.Core.Tests;

public sealed class GeometryTests
{
    [Theory]
    [InlineData(12, 12, GrowthDirection.RightDown)]
    [InlineData(1860, 12, GrowthDirection.LeftDown)]
    [InlineData(12, 1020, GrowthDirection.RightUp)]
    [InlineData(1860, 1020, GrowthDirection.LeftUp)]
    public void FourCornersGrowInward(int x, int y, GrowthDirection expected)
    {
        var monitor = new MonitorGeometry("primary", new(0, 0, 1920, 1080), new(1));
        var plan = OverlayLayoutPlanner.Plan(new(x, y, x + 48, y + 48), monitor, new());
        Assert.Equal(expected, plan.Direction);
    }
    [Fact]
    public void BalancedFallbackFitsNarrowWorkAreaWithoutMovingAnchor()
    {
        var monitor = new MonitorGeometry("narrow", new(-600, -400, 0, 500), new(1));
        var plan = OverlayLayoutPlanner.Plan(new(-324, -376, -276, -328), monitor, new());
        Assert.Equal(GrowthDirection.BalancedDown, plan.Direction);
        Assert.Equal(420, plan.Target.Width);
        Assert.True(plan.Target.Contains(plan.Anchor));
    }
    [Fact]
    public void TenThousandFixedSeedTopologiesMaintainEveryFrameInvariant()
    {
        var random = new Random(0x4B5959);
        double[] scales = [1, 1.25, 1.5, 1.75, 2, 2.5];
        for (int i = 0; i < 10_000; i++)
        {
            var dpi = new DpiScale(scales[random.Next(scales.Length)]);
            int width = random.Next(100, 2400) + dpi.CeilingPixels(72);
            int height = random.Next(100, 1600) + dpi.CeilingPixels(72);
            int x = random.Next(-6000, 6000), y = random.Next(-4000, 4000);
            var monitor = new MonitorGeometry("m" + i, new(x, y, x + width, y + height), dpi);
            var work = monitor.WorkArea.Inset(dpi.CeilingPixels(12));
            int size = dpi.CeilingPixels(48);
            int left = random.Next(work.Left, work.Right - size + 1), top = random.Next(work.Top, work.Bottom - size + 1);
            PixelRect anchor = new(left, top, left + size, top + size);
            var plan = OverlayLayoutPlanner.Plan(anchor, monitor, new(random.Next(320, 601), random.Next(48, 900)));
            Assert.Equal(anchor, plan.Anchor);
            for (int frame = 0; frame <= 20; frame++)
            {
                PixelRect rect = OverlayLayoutPlanner.Frame(plan, frame / 20d);
                Assert.True(rect.Contains(anchor), $"seed case {i} frame {frame}: lost anchor");
                Assert.True(work.Contains(rect), $"seed case {i} frame {frame}: overflow");
                Assert.True(rect.Width >= size && rect.Height >= size);
            }
            Assert.Equal(anchor, OverlayLayoutPlanner.Frame(plan, 0));
            Assert.Equal(plan.Target, OverlayLayoutPlanner.Frame(plan, 1));
        }
    }
    [Fact]
    public void StreamHeightIsMonotonicBoundedAndWidthDirectionRemainFixed()
    {
        var plan = OverlayLayoutPlanner.Plan(new(1860, 1020, 1908, 1068), new("m", new(0, 0, 1920, 1080), new(1)), new());
        var controller = new GrowthController(plan);
        int last = 48;
        for (int i = 0; i < 2000; i++)
        {
            controller.GrowToMeasuredHeight(i % 3 == 0 ? i * 10 : 48);
            PixelRect frame = controller.Tick(TimeSpan.FromMilliseconds(16.666));
            Assert.True(frame.Height >= last);
            Assert.True(frame.Height <= plan.MaximumHeightPixels);
            Assert.Equal(plan.Target.Width, frame.Width);
            Assert.Equal(plan.Anchor.Bottom, frame.Bottom);
            Assert.True(frame.Contains(plan.Anchor));
            Assert.True(plan.SafeWorkArea.Contains(frame));
            last = frame.Height;
        }
        Assert.True(controller.IsSettled);
    }
    [Fact]
    public void InvalidAnchorsAndNonfinitePreferencesFailBeforeGeometry()
    {
        var monitor = new MonitorGeometry("m", new(0, 0, 1920, 1080), new(1));
        Assert.Throws<ArgumentException>(() => OverlayLayoutPlanner.Plan(new(0, 0, 48, 48), monitor, new()));
        Assert.Throws<ArgumentException>(() => OverlayLayoutPlanner.Plan(new(12, 12, 60, 60), monitor, new(double.NaN)));
    }
}
