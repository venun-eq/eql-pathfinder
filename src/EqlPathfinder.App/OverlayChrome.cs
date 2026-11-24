using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace EqlPathfinder.App;

/// <summary>One overlay's place and look. <see cref="Options"/> are that overlay's own choices (see <see cref="OverlayKinds"/>).</summary>
public sealed class OverlayPrefs
{
    public bool Visible { get; set; } = true;
    public double Left { get; set; } = 40;
    public double Top { get; set; } = 220;
    /// <summary>Clicks pass through to the game.</summary>
    public bool Locked { get; set; }
    /// <summary>The panel's background; the text stays solid.</summary>
    public double Opacity { get; set; } = 0.92;
    public Dictionary<string, string> Options { get; set; } = new();

    public string Option(string key, string fallback) => Options.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : fallback;
}

/// <summary>
/// The built-in overlays: a fixed set, each with its display name, purpose,
/// default placement, and options. A new overlay is a new entry here and a window that draws it.
/// </summary>
public static class OverlayKinds
{
    public const string Crawl = "crawl", Meter = "meter";

    /// <param name="Options">Its own choices and their defaults (crawl: size, mode; meter: scope, by, mode, clock, fightEnd).</param>
    public sealed record Kind(string Id, string Name, string About, double Top, IReadOnlyDictionary<string, string> Options);

    public static readonly IReadOnlyList<Kind> All =
    [
        new(Crawl, "Crawl overlay", "This crawl's pace and loot against your usual, and where to go next.", 220,
            new Dictionary<string, string> { ["size"] = "Compact", ["mode"] = "Auto" }),
        new(Meter, "Fight meter", "Damage per second by fighter, spell, mob or fight, against your usual here. Pick the view on the meter itself.", 520,
            new Dictionary<string, string> { ["scope"] = "fight", ["by"] = "fighter", ["mode"] = "damage", ["clock"] = "active", ["fightEnd"] = "8" }),
    ];

    public static Kind Get(string id) => All.First(k => k.Id == id);

    public static OverlayPrefs Defaults(string id)
    {
        var k = All.FirstOrDefault(x => x.Id == id);
        return new OverlayPrefs { Top = k?.Top ?? 220, Options = new Dictionary<string, string>(k?.Options ?? new Dictionary<string, string>()) };
    }
}

/// <summary>
/// What every overlay shares, attached to its window: it keeps its place (back on screen if a monitor went away), moves
/// by dragging, locks so clicks pass through to the game, never takes focus from the game, fades only its background,
/// stays above a borderless game, has the same right-click menu, and while you arrange the overlays shows its name and an
/// outline. Each overlay only draws its own content.
/// </summary>
internal sealed class OverlayChrome
{
    private readonly Window _w;
    private readonly AppSettings _settings;
    private readonly Border _panel;
    private readonly DispatcherTimer _raise;
    private IntPtr _hwnd;
    private NameTag? _tag;
    private Thickness _padding;

    /// <summary>All overlays are being arranged (set by the app): shown, unlocked, outlined and named.</summary>
    public static bool Arranging { get; set; }

    public OverlayChrome(Window w, AppSettings settings, string id, Border panel)
    {
        _w = w;
        _settings = settings;
        _panel = panel;
        Kind = OverlayKinds.Get(id);
        var p = Prefs;
        double l = p.Left, t = p.Top;
        if (l < SystemParameters.VirtualScreenLeft || l > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 80 ||
            t < SystemParameters.VirtualScreenTop || t > SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60)
        { l = 40; t = Kind.Top; }
        w.WindowStartupLocation = WindowStartupLocation.Manual;
        w.Left = l;
        w.Top = t;
        w.Title = "EQL Pathfinder " + Kind.Name.ToLowerInvariant();
        ApplyBackground();
        w.SourceInitialized += (_, _) => { _hwnd = new WindowInteropHelper(w).Handle; ApplyLock(); };
        // Borderless-windowed games can cover topmost windows; re-assert every few seconds.
        _raise = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _raise.Tick += (_, _) => { if (_hwnd != IntPtr.Zero && w.IsVisible) Native.Raise(_hwnd); };
        w.IsVisibleChanged += (_, _) =>
        {
            if (w.IsVisible) _raise.Start(); else _raise.Stop();
            if (w.IsVisible) w.Dispatcher.BeginInvoke(ApplyArranging, DispatcherPriority.Loaded);
        };
    }

    public OverlayKinds.Kind Kind { get; }
    public string Id => Kind.Id;
    public OverlayPrefs Prefs => _settings.Overlay(Id);

    /// <summary>Moves the overlay with the mouse and keeps the new place (call from the window's left-button handler).</summary>
    public void Drag()
    {
        try { _w.DragMove(); } catch (InvalidOperationException) { }
        SavePosition();
    }

    public void SavePosition()
    {
        Prefs.Left = _w.Left;
        Prefs.Top = _w.Top;
        _settings.Save();
    }

    /// <summary>Locked, and not being arranged: clicks pass through to the game.</summary>
    public void ApplyLock()
    {
        if (_hwnd != IntPtr.Zero) Native.SetExStyle(_hwnd, Prefs.Locked && !Arranging);
    }

    /// <summary>Only the panel fades; the numbers stay solid so they read over any scene.</summary>
    public void ApplyBackground()
    {
        byte a = (byte)Math.Round(Math.Clamp(Prefs.Opacity, 0.3, 1.0) * 255);
        _panel.Background = new SolidColorBrush(Color.FromArgb(a, 0x0C, 0x10, 0x15));
    }

    /// <summary>While arranging: unlocked, with a gold outline and its name, so you can tell the overlays apart while you place them.</summary>
    public void ApplyArranging()
    {
        ApplyLock();
        var layer = AdornerLayer.GetAdornerLayer(_panel);
        if (Arranging && _tag == null && layer != null)
        {
            // Room above the content for the name, so it covers nothing.
            _padding = _panel.Padding;
            _panel.Padding = new Thickness(_padding.Left, _padding.Top + 20, _padding.Right, _padding.Bottom);
            layer.Add(_tag = new NameTag(_panel, Kind.Name));
        }
        else if (!Arranging && _tag != null)
        {
            layer?.Remove(_tag);
            _tag = null;
            _panel.Padding = _padding;
        }
    }

    /// <summary>The right-click menu every overlay has, with the overlay's own items after "Open".</summary>
    public ContextMenu Menu(params MenuItem[] own)
    {
        var app = (App)Application.Current;
        var m = new ContextMenu();
        m.Items.Add(Item("Open EQL Pathfinder", () => app.OpenMain()));
        foreach (var i in own) m.Items.Add(i);
        var bg = new MenuItem { Header = "Background" };
        foreach (var o in new[] { 1.0, 0.9, 0.8, 0.65, 0.5 })
            bg.Items.Add(Item($"{o:P0}", () => app.SetOverlayOpacity(Id, o), Math.Abs(Prefs.Opacity - o) < 0.03));
        m.Items.Add(bg);
        m.Items.Add(new Separator());
        m.Items.Add(Item($"Lock the {Kind.Name.ToLowerInvariant()} (clicks pass through; unlock from the tray compass)",
            () => app.SetOverlayLocked(Id, !Prefs.Locked), Prefs.Locked));
        m.Items.Add(Arranging ? Item("Done arranging", () => app.SetArranging(false)) : Item("Arrange overlays…", () => app.SetArranging(true)));
        m.Items.Add(Item($"Hide the {Kind.Name.ToLowerInvariant()}", () => app.SetOverlayVisible(Id, false)));
        return m;
    }

    public static MenuItem Item(string text, Action a, bool check = false) => new() { Header = text, IsChecked = check, Command = new Relay(a) };

    /// <summary>A gold outline and the overlay's name in a gold tab at its top-left corner.</summary>
    private sealed class NameTag(UIElement panel, string name) : Adorner(panel)
    {
        private static readonly Brush Gold = Frozen(Color.FromRgb(0xF0, 0xC8, 0x60)), Ink = Frozen(Color.FromRgb(0x11, 0x16, 0x1C));
        private static readonly Pen Outline = FrozenPen();

        protected override void OnRender(DrawingContext dc)
        {
            var size = AdornedElement.RenderSize;
            dc.DrawRoundedRectangle(null, Outline, new Rect(1, 1, Math.Max(0, size.Width - 2), Math.Max(0, size.Height - 2)), 10, 10);
            var text = new FormattedText(name, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 12, Ink,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawRoundedRectangle(Gold, null, new Rect(10, 4, text.Width + 16, text.Height + 4), 4, 4);
            dc.DrawText(text, new Point(18, 6));
        }

        private static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        private static Pen FrozenPen()
        {
            var p = new Pen(Gold, 2) { DashStyle = DashStyles.Dash };
            p.Freeze();
            return p;
        }
    }
}

internal sealed class Relay(Action a) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? p) => true;
    public void Execute(object? p) => a();
}

internal static class Native
{
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

    public static void SetExStyle(IntPtr h, bool clickThrough)
    {
        int ex = GetWindowLong(h, -20);
        ex |= 0x80 | 0x08000000;                      // WS_EX_TOOLWINDOW (not in Alt-Tab) | WS_EX_NOACTIVATE (clicks don't take focus from the game)
        if (clickThrough) ex |= 0x20 | 0x80000;       // WS_EX_TRANSPARENT | WS_EX_LAYERED
        else ex &= ~0x20;
        SetWindowLong(h, -20, ex);
    }

    public static void Raise(IntPtr h) => SetWindowPos(h, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
}
