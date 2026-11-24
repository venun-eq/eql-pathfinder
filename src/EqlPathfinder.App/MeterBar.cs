using System.Windows;
using System.Windows.Media;

namespace EqlPathfinder.App;

/// <summary>
/// One overlay meter bar: a rounded track, a fill for this crawl, and an optional white tick for your usual.
/// Drawn directly (no layout children), so a dozen of them cost nothing to redraw every second.
/// </summary>
public sealed class MeterBar : FrameworkElement
{
    private static readonly Brush Track = Frozen(Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF));
    private static readonly Brush TickBrush = Frozen(Color.FromRgb(0xEC, 0xE7, 0xDA));

    /// <summary>0..1 of the track.</summary>
    public double Fill { get; set; }
    /// <summary>0..1 of the track, or NaN for no tick.</summary>
    public double Tick { get; set; } = double.NaN;
    public Brush FillBrush { get; set; } = Frozen(Color.FromRgb(0xF0, 0xC8, 0x60));

    /// <summary>No usual to compare with yet (docs/pace.md): an empty dashed track, nothing filled against it.</summary>
    public bool Dashed { get; set; }

    private static readonly Pen DashPen = FrozenPen();

    public MeterBar() => Height = 9;

    public void Set(double fill, double tick, Brush brush)
    {
        Fill = Math.Clamp(double.IsNaN(fill) ? 0 : fill, 0, 1);
        Tick = double.IsNaN(tick) ? double.NaN : Math.Clamp(tick, 0, 1);
        FillBrush = brush;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size available) =>
        new(double.IsInfinity(available.Width) ? 100 : available.Width, Height);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight, r = h / 2;
        if (w <= 0 || h <= 0) return;
        if (Dashed) { dc.DrawRoundedRectangle(null, DashPen, new Rect(0.5, 0.5, w - 1, h - 1), r, r); return; }
        dc.DrawRoundedRectangle(Track, null, new Rect(0, 0, w, h), r, r);
        double fw = w * Fill;
        if (fw > 0.5) dc.DrawRoundedRectangle(FillBrush, null, new Rect(0, 0, Math.Max(fw, h), h), r, r);
        if (!double.IsNaN(Tick))
        {
            double x = Math.Clamp(w * Tick, 1, w - 1);
            dc.DrawRoundedRectangle(TickBrush, null, new Rect(x - 1, -3, 2, h + 6), 1, 1);
        }
    }

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Pen FrozenPen()
    {
        var p = new Pen(Frozen(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)), 1) { DashStyle = new DashStyle([3, 2], 0) };
        p.Freeze();
        return p;
    }
}
