using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using EqlPathfinder.Engine.Combat;

namespace EqlPathfinder.App;

/// <summary>
/// The fight meter overlay: damage per second for this fight, this crawl (or zone) or this session, by fighter, spell,
/// mob or fight, with your usual here as the yardstick. Click a row to open it; drag the header to move.
/// </summary>
public sealed class MeterWindow : Window
{
    private const int MaxRows = 8;
    private static readonly Brush Text = B("#ECE7DA"), Soft = B("#D8DDE2"), Muted = B("#A9B1BA"), Dim = B("#8D959E"), Gold = B("#F0C860"),
        Good = B("#8FD18F"), Amber = B("#E8B04E"), Warn = B("#E8907E"), Track = B("#1B222C"), Seg = B("#252E39"), Rail = B("#0D1116"),
        Line = B("#29323D"), MeFill = B("#5C4C22"), PetFill = B("#3F3720"), OtherFill = B("#22406A"), PlainFill = B("#2E3A46");

    private readonly AppSettings _settings;
    private readonly Border _root;
    private readonly StackPanel _body = new();
    private MeterFrame? _frame;
    private MeterQuery _query;

    public MeterWindow(AppSettings settings)
    {
        _settings = settings;
        var prefs = settings.Overlay(OverlayKinds.Meter);
        _query = new MeterQuery(prefs.Option("scope", "fight"), prefs.Option("by", "fighter"), prefs.Option("mode", "damage"));
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Height;
        Width = 328;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 12;
        Foreground = Text;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        _root = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderBrush = B("#18FFFFFF"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 9, 12, 10),
            Child = _body,
        };
        TextElement.SetForeground(_root, Text);
        Content = _root;
        Chrome = new OverlayChrome(this, settings, OverlayKinds.Meter, _root);

        MouseLeftButtonDown += (_, e) =>
        {
            if (e.Handled) return;
            Chrome.Drag();
        };
        MouseRightButtonUp += (_, _) => Chrome.Menu().IsOpen = true;
        Render();
    }

    /// <summary>Place, lock, background, menu and arranging: what every overlay shares.</summary>
    internal OverlayChrome Chrome { get; }

    public void Update(MeterFrame frame)
    {
        _frame = frame;
        Render();
    }

    /// <summary>For the developer snapshot: one view, drawn.</summary>
    internal void Show(MeterFrame frame, MeterQuery query)
    {
        _frame = frame;
        _query = query;
        Render();
    }

    private void Set(MeterQuery q)
    {
        _query = q;
        var o = Chrome.Prefs.Options;
        if (o.GetValueOrDefault("scope") != q.Scope || o.GetValueOrDefault("by") != q.By || o.GetValueOrDefault("mode") != q.Mode)
        {
            (o["scope"], o["by"], o["mode"]) = (q.Scope, q.By, q.Mode);
            _settings.Save();
        }
        Render();
    }

    // ------------------------------------------------------------------ drawing

    private void Render()
    {
        _body.Children.Clear();
        var f = _frame;
        if (f == null || f.ImportPercent != null)
        {
            _body.Children.Add(new TextBlock { Text = "Fight meter", FontSize = 13, FontWeight = FontWeights.SemiBold });
            _body.Children.Add(new TextBlock
            {
                Text = f?.ImportPercent is { } p ? $"Reading your past fights… {p:0}%" : "Starting…",
                Foreground = Muted,
                Margin = new Thickness(0, 4, 0, 0),
            });
            return;
        }
        // The clock is a setting (Settings → Overlays, or a click on the other clock's line), read each time it draws.
        var v = MeterViews.Build(f, _query with { Clock = Chrome.Prefs.Option("clock", "active") });
        _body.Children.Add(Header(v));
        _body.Children.Add(Controls(v));
        if (v.Rows.Count == 0)
            _body.Children.Add(new TextBlock { Text = EmptyText(v), Foreground = Dim, Margin = new Thickness(0, 10, 0, 4), TextWrapping = TextWrapping.Wrap });
        else
        {
            if (v.Rows[0].Layout is "table" or "ability") _body.Children.Add(ColumnHeads(v.Rows[0].Layout, v.By));
            foreach (var r in v.Rows.Take(MaxRows)) _body.Children.Add(Row(r, v));
            if (v.Rows.Count > MaxRows)
                _body.Children.Add(new TextBlock { Text = $"+{v.Rows.Count - MaxRows} more", Foreground = Dim, FontSize = 11, Margin = new Thickness(0, 3, 0, 0) });
        }
        // The footer leads with the clock you didn't pick ("Kill time 1:20 · 301 DPS"); a click swaps the two.
        var foot = new TextBlock
        {
            Text = v.Footer,
            Foreground = Muted,
            FontSize = 11,
            Margin = new Thickness(0, 8, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Background = Brushes.Transparent,
        };
        if (v.Footer.Length > 0 && _query.Mode == "damage")
        {
            foot.Cursor = Cursors.Hand;
            foot.ToolTip = v.Clock == "kill"
                ? "The big number is kill time: first swing to the death, pauses included. Click to lead with active time."
                : "The big number is active time: the seconds you were fighting, pauses left out. Click to lead with kill time.";
            foot.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                ((App)Application.Current).SetOverlayOption(OverlayKinds.Meter, "clock", v.Clock == "kill" ? "active" : "kill");
                Render();
            };
        }
        _body.Children.Add(foot);
    }

    private string EmptyText(MeterView v) => _query.Mode switch
    {
        "healing" => "No healing here yet.",
        "taken" => "Nothing has hit you here yet.",
        _ => _query.Scope == "fight" ? "Waiting for a fight." : "No fights here yet.",
    };

    private UIElement Header(MeterView v)
    {
        var dock = new DockPanel();
        var right = new TextBlock { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(10, 0, 0, 0) };
        right.Inlines.Add(new Run(v.PerSecond.ToString("N0")) { FontSize = 16, FontWeight = FontWeights.Bold });
        right.Inlines.Add(new Run(" " + v.Unit) { Foreground = Muted });
        if (v.UsualPct is { } p) right.Inlines.Add(new Run($"  {p:P0}") { Foreground = PctBrush(p), FontWeight = FontWeights.SemiBold });
        DockPanel.SetDock(right, Dock.Right);
        dock.Children.Add(right);
        if (v.CanBack)
        {
            var back = Link("‹ ", () => Set(_query.Fighter != null ? _query with { Fighter = null }
                : _query.FightStart != null ? _query with { FightStart = null, FightMob = null } : _query with { Mob = null }));
            back.FontSize = 15;
            back.ToolTip = "Back";
            DockPanel.SetDock(back, Dock.Left);
            dock.Children.Add(back);
        }
        var title = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Bottom };
        title.Inlines.Add(new Run(v.Title) { FontSize = 13.5, FontWeight = FontWeights.SemiBold });
        title.Inlines.Add(new Run("  " + Clock(v.Seconds)) { Foreground = Muted });
        dock.Children.Add(title);
        return dock;
    }

    private UIElement Controls(MeterView v)
    {
        var dock = new DockPanel { Margin = new Thickness(0, 7, 0, 7) };
        var menus = new StackPanel { Orientation = Orientation.Horizontal };
        if (_query.Mode == "damage" && _query.Fighter == null)
        {
            var options = _query.Scope == "fight" || _query.FightStart != null
                ? new[] { ("fighter", "by fighter"), ("spell", "by spell") }
                : [("fighter", "by fighter"), ("spell", "by spell"), ("mob", "by mob"), ("fight", "by fight")];
            menus.Children.Add(Pick("by " + v.By, options, _query.By, by => Set(_query with { By = by, Mob = null, Fighter = null })));
        }
        menus.Children.Add(Pick(_query.Mode switch { "healing" => "Healing", "taken" => "Taken", _ => "Damage" },
            [("damage", "Damage"), ("healing", "Healing"), ("taken", "Damage taken")], _query.Mode, mode => Set(_query with { Mode = mode, Fighter = null })));
        DockPanel.SetDock(menus, Dock.Right);
        dock.Children.Add(menus);

        var seg = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (key, label) in new[] { ("fight", "Fight"), ("place", v.PlaceLabel), ("session", "Session") })
        {
            bool on = _query.Scope == key && _query.FightStart == null;
            var b = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(7, 2, 7, 3),
                Background = on ? Seg : Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = label, FontSize = 11, Foreground = on ? Text : Muted },
            };
            b.MouseLeftButtonDown += (_, e) => { e.Handled = true; Set(_query with { Scope = key, Fighter = null, Mob = null, FightStart = null, FightMob = null }); };
            seg.Children.Add(b);
        }
        dock.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = Rail,
            BorderBrush = Line,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = seg,
        });
        return dock;
    }

    /// <summary>A small "label ▾" that opens a menu of choices.</summary>
    private static UIElement Pick(string label, (string Key, string Label)[] options, string current, Action<string> choose)
    {
        var t = new TextBlock { Text = label + " ▾", FontSize = 11, Foreground = Muted, Cursor = Cursors.Hand, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        t.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            var m = new ContextMenu { PlacementTarget = t };
            foreach (var (key, text) in options)
                m.Items.Add(new MenuItem { Header = text, IsChecked = key == current, Command = new Relay(() => choose(key)) });
            m.IsOpen = true;
        };
        return t;
    }

    private static TextBlock Link(string text, Action click)
    {
        var t = new TextBlock { Text = text, Foreground = Muted, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Bottom };
        t.MouseLeftButtonDown += (_, e) => { e.Handled = true; click(); };
        return t;
    }

    private static UIElement ColumnHeads(string layout, string by)
    {
        var g = Columns(layout);
        string[] heads = layout == "table" ? [by == "fight" ? "Fight" : "Mob", "Time", "DPS", "Usual"] : ["Spell or ability", "Hits", "Crit", "Max", "DPS"];
        for (int i = 0; i < heads.Length; i++)
            Cell(g, i, new TextBlock { Text = heads[i], Foreground = Dim, FontSize = 11, HorizontalAlignment = i == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right });
        return new Border { BorderBrush = B("#252E38"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 3), Child = g };
    }

    private static Grid Columns(string layout)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        foreach (var w in layout == "table" ? new[] { 40.0, 44, 44 } : [34.0, 36, 40, 40])
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        return g;
    }

    private static void Cell(Grid g, int col, UIElement e)
    {
        Grid.SetColumn(e, col);
        g.Children.Add(e);
    }

    private UIElement Row(MeterRow r, MeterView v)
    {
        FrameworkElement row = r.Layout switch
        {
            "bar" => BarRow(r, v),
            "usual" => UsualRow(r),
            "table" => TableRow(r),
            _ => AbilityRow(r),
        };
        if (r.Open != null)
        {
            row.Cursor = Cursors.Hand;
            row.ToolTip = r.Open switch { "fighter" => "Click for spells", "mob" => "Click for its fights", _ => "Click to open this fight" };
            row.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                Set(r.Open switch
                {
                    "fighter" => _query with { Fighter = r.Key },
                    "mob" => _query with { Mob = r.Key },
                    _ => _query with { FightStart = r.FightStart, FightMob = r.Key, Fighter = null },
                });
            };
        }
        return row;
    }

    private static FrameworkElement BarRow(MeterRow r, MeterView v)
    {
        var fill = new Grid();
        fill.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.001, r.Bar), GridUnitType.Star) });
        fill.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0, 1 - r.Bar), GridUnitType.Star) });
        fill.Children.Add(new Border { Background = r.Kind switch { "me" => MeFill, "pet" => PetFill, "other" when v.Unit == "DPS" && r.Open == "fighter" => OtherFill, _ => PlainFill } });

        var label = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        label.Inlines.Add(new Run(r.Label) { FontWeight = r.Kind == "me" ? FontWeights.SemiBold : FontWeights.Normal });
        if (r.Sub != null) label.Inlines.Add(new Run(" · " + r.Sub) { Foreground = Muted });
        var right = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = Soft };
        right.Inlines.Add(new Run($"{r.Amount:N0} · "));
        right.Inlines.Add(new Run(r.PerSecond.ToString("N0")) { Foreground = Text, FontWeight = FontWeights.Bold });
        right.Inlines.Add(new Run($" · {r.Share:P0}"));
        var text = new DockPanel { Margin = new Thickness(8, 0, 8, 0) };
        DockPanel.SetDock(right, Dock.Right);
        text.Children.Add(right);
        text.Children.Add(label);

        var g = new Grid { Height = 24 };
        g.Children.Add(fill);
        if (r.Tick is { } tick)
        {
            // Your usual, on the bar's own scale: the fill ends past the line when you're ahead of it, short of it when behind.
            var mark = new Grid { ToolTip = r.UsualPct is { } up ? $"The line is your usual here. You're at {up:P0} of it." : "The line is your usual here." };
            mark.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.001, tick), GridUnitType.Star) });
            mark.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0, 1 - tick), GridUnitType.Star) });
            mark.Children.Add(new Border { Width = 2, HorizontalAlignment = HorizontalAlignment.Right, Background = r.UsualPct is { } q ? PctBrush(q) : Text });
            g.Children.Add(mark);
        }
        g.Children.Add(text);
        return new Border { CornerRadius = new CornerRadius(5), Background = Track, ClipToBounds = true, Margin = new Thickness(0, 0, 0, 4), Child = g };
    }

    private static FrameworkElement UsualRow(MeterRow r)
    {
        var top = new DockPanel();
        var right = new TextBlock { Margin = new Thickness(8, 0, 0, 0) };
        right.Inlines.Add(new Run(r.PerSecond.ToString("N0")) { FontWeight = FontWeights.Bold });
        if (r.UsualPct is { } p) right.Inlines.Add(new Run($"  {p:P0}") { Foreground = PctBrush(p) });
        DockPanel.SetDock(right, Dock.Right);
        top.Children.Add(right);
        var label = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        label.Inlines.Add(new Run(r.Label));
        if (r.Sub != null) label.Inlines.Add(new Run(" · " + r.Sub) { Foreground = Muted });
        top.Children.Add(label);
        var bar = new MeterBar { Height = 6, Margin = new Thickness(0, 3, 0, 0) };
        bar.Set(r.Bar, r.UsualPct != null ? 0.8 : double.NaN, r.UsualPct is { } q ? PctBrush(q) : Gold);
        var s = new StackPanel { Margin = new Thickness(0, 0, 0, 7), Background = Brushes.Transparent };
        s.Children.Add(top);
        s.Children.Add(bar);
        return s;
    }

    private static FrameworkElement TableRow(MeterRow r)
    {
        var g = Columns("table");
        var label = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        label.Inlines.Add(new Run(r.Label) { Foreground = r.Live ? Gold : Text, FontWeight = r.Live ? FontWeights.SemiBold : FontWeights.Normal });
        if (r.Count > 0) label.Inlines.Add(new Run($" ×{r.Count}") { Foreground = Dim });
        Cell(g, 0, label);
        Cell(g, 1, Num(Clock(r.Seconds), Text));
        Cell(g, 2, Num(r.PerSecond.ToString("N0"), Text, true));
        Cell(g, 3, Num(r.UsualPct is { } p ? p.ToString("P0") : "", r.UsualPct is { } q ? PctBrush(q) : Dim));
        return new Border { Padding = new Thickness(0, 3, 0, 3), Background = Brushes.Transparent, Child = g };
    }

    private static FrameworkElement AbilityRow(MeterRow r)
    {
        var g = Columns("ability");
        var label = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        label.Inlines.Add(new Run(r.Label));
        label.Inlines.Add(new Run($" {r.Share:P0}") { Foreground = Dim });
        Cell(g, 0, label);
        if (r.Hits > 0)
        {
            Cell(g, 1, Num(r.Hits.ToString("N0"), Text));
            Cell(g, 2, Num(((double)r.Crits / r.Hits).ToString("P0"), Text));
            Cell(g, 3, Num(r.Max.ToString("N0"), Text));
        }
        else Cell(g, 3, Num(r.Amount.ToString("N0"), Soft));
        Cell(g, 4, Num(r.PerSecond.ToString("N0"), r.UsualPct is { } p ? PctBrush(p) : Text, true));
        return new Border { Padding = new Thickness(0, 3, 0, 3), BorderBrush = B("#1F2731"), BorderThickness = new Thickness(0, 0, 0, 1), Child = g };
    }

    private static TextBlock Num(string text, Brush brush, bool bold = false) =>
        new() { Text = text, Foreground = brush, HorizontalAlignment = HorizontalAlignment.Right, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal };

    /// <summary>Green at 90% of your usual and up, amber from 75%, red below.</summary>
    private static Brush PctBrush(double p) => p >= 0.9 ? Good : p >= 0.75 ? Amber : Warn;

    private static string Clock(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Round(seconds));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }

    private static Brush B(string hex)
    {
        var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        b.Freeze();
        return b;
    }
}
