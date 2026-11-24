using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.App;

/// <summary>
/// The in-game meter. Each row is this crawl as a bar with a white tick for your usual in the same place at a
/// similar level, so a short bar says why you're slow without a sentence to read. Compact: the main rows and level;
/// Standard adds kills and XP per kill and a one-line why; Detailed adds the session and the last crawls.
/// Between crawls it shows how the last one ranked and where to go next.
/// </summary>
public partial class OverlayWindow : Window
{
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _bannerTimer;
    private LiveSnapshot? _last;

    /// <summary>EverQuest is running but its log isn't being written: say so instead of waiting silently.</summary>
    public bool LogOff { get; set; }

    private static readonly Brush Text = Frozen("#ECE7DA"), Muted = Frozen("#A9B1BA"), Dim = Frozen("#8D959E"),
        Gold = Frozen("#F0C860"), GoldSoft = Frozen("#8CF0C860"), Good = Frozen("#CC8FD18F"), GoodText = Frozen("#8FD18F"),
        Amber = Frozen("#D9B86A"), Warn = Frozen("#E8907E"), WarnText = Frozen("#E8A596"), Neutral = Frozen("#7D8894"), XpBlue = Frozen("#A9C4E8");

    public OverlayWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        Chrome = new OverlayChrome(this, settings, OverlayKinds.Crawl, Root);
        ApplySize();
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) { CycleSize(); return; }
            Chrome.Drag();
        };
        MouseRightButtonUp += (_, _) => BuildMenu().IsOpen = true;
        _bannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(25) };
        _bannerTimer.Tick += (_, _) => { DropBanner.Visibility = Visibility.Collapsed; _bannerTimer.Stop(); };
    }

    /// <summary>Place, lock, background, menu and arranging: what every overlay shares.</summary>
    internal OverlayChrome Chrome { get; }

    // ------------------------------------------------------------------ settings

    /// <summary>Compact, Standard or Detailed.</summary>
    private string Size => Chrome.Prefs.Option("size", "Compact");

    private void CycleSize() => ((App)Application.Current).SetOverlayOption(OverlayKinds.Crawl, "size", Size == "Compact" ? "Standard" : "Compact");

    internal void ApplySize()
    {
        DetailedPanel.Visibility = Size == "Detailed" ? Visibility.Visible : Visibility.Collapsed;
        if (_last != null) Update(_last);
    }

    private ContextMenu BuildMenu()
    {
        var app = (App)Application.Current;
        var size = new MenuItem { Header = "Size" };
        foreach (var s in new[] { "Compact", "Standard", "Detailed" })
            size.Items.Add(OverlayChrome.Item(s, () => app.SetOverlayOption(OverlayKinds.Crawl, "size", s), Size == s));
        var mode = new MenuItem { Header = "Mode" };
        string current = Chrome.Prefs.Option("mode", "Auto");
        foreach (var (s, label) in new[] { ("Auto", "Auto (leveling below the cap, farming at it)"), ("Leveling", "Leveling"), ("Farming", "Farming") })
            mode.Items.Add(OverlayChrome.Item(label, () => app.SetOverlayOption(OverlayKinds.Crawl, "mode", s), current == s));
        return Chrome.Menu(size, mode);
    }

    // ------------------------------------------------------------------ drawing

    private readonly List<(HighTierDrop Drop, double T5)> _drops = new();

    /// <summary>
    /// Calls out a drop above T5. Drops that arrive while the banner is up (a Reward Chest often gives several at once)
    /// join it, best first, instead of replacing it.
    /// </summary>
    public void ShowDrop(HighTierDrop d, double fragValue)
    {
        if (DropBanner.Visibility != Visibility.Visible) _drops.Clear();
        _drops.Add((d, d.BaseT5 * (d.Kind == LootKind.Fragment ? fragValue : 1)));
        var all = _drops.OrderByDescending(x => x.T5).ToList();
        // The banner only says what dropped and what it's worth.
        static string Name(HighTierDrop x) => $"{(x.Kind == LootKind.Fragment ? "Fragment" : "Mote")} of {x.Tier} Potential" + (x.Count > 1 ? $" ×{x.Count}" : "");
        if (all.Count == 1)
        {
            DropTitle.Text = "★ " + Name(d);
            DropDetail.Text = $"{all[0].T5.ToString("0.#", CI)} T5-eq · {d.Source}";
        }
        else
        {
            DropTitle.Text = $"★ {all.Count} drops above T5 · {all.Sum(x => x.T5).ToString("0.#", CI)} T5-eq";
            DropDetail.Text = string.Join("\n", all.Select(x => $"{Name(x.Drop)} · {x.T5.ToString("0.#", CI)} T5-eq · {x.Drop.Source}"));
        }
        DropBanner.Visibility = Visibility.Visible;
        _bannerTimer.Stop();
        _bannerTimer.Start();
    }

    public void Update(LiveSnapshot s)
    {
        _last = s;
        bool farming = s.Mode == "farming";
        bool compact = Size == "Compact";
        Rows.Children.Clear();
        Note.Text = "";
        Note.Foreground = Dim;
        Footer.Visibility = Visibility.Visible;
        FootBar.Visibility = Visibility.Visible;

        var c = s.Current;
        if (LogOff && !s.Importing)
        {
            DrawMessage("Your log is off", "", "", s, farming, compact);
            Note.Text = "In EverQuest, type /log on. It stays on, and Pathfinder picks it up right away.";
            Note.Foreground = WarnText;
            // The level and ETA are from before the log went quiet: don't show them as current.
            Footer.Visibility = Visibility.Collapsed;
            FootBar.Visibility = Visibility.Collapsed;
        }
        else if (s.Importing) DrawMessage("Reading your logs", $"{s.ImportPercent:0}%", "Pathfinder reads your history once, then follows the log live.", s, farming, compact);
        else if (c != null && c.State != "finished") DrawCrawl(s, c, farming, compact);
        else if (s.Next is { Last: not null } next) DrawDone(s, next, farming, compact);
        else if (c != null) DrawCrawl(s, c, farming, compact);
        else DrawMessage(s.Zone is { } z ? z : "Waiting for your first zone line", LogStatus(s),
            "Measuring starts when you zone into a crawl.", s, farming, compact);

        // From the inlines, not Text: a note built of coloured runs (RunNote's no-usual one) leaves Text empty, and was never shown.
        Note.Visibility = Note.Inlines.OfType<Run>().Any(r => r.Text.Length > 0) ? Visibility.Visible : Visibility.Collapsed;
        if (DetailedPanel.Visibility == Visibility.Visible) DrawDetailed(s, farming);
    }

    private void DrawMessage(string head, string right, string note, LiveSnapshot s, bool farming, bool compact)
    {
        HeadZone.Text = head;
        Right((right, Muted));
        Note.Text = compact ? "" : note;
        if (farming || s.Level == null && !s.NotKnown) Footer.Visibility = Visibility.Collapsed;
        else LevelFooter(s, compact);
    }

    private void DrawCrawl(LiveSnapshot s, VisitStats c, bool farming, bool compact)
    {
        HeadZone.Text = Zone(c.BaseZone, c.Difficulty);
        // On an unmet loadout no verdict judges the crawl: another loadout's usual would mislead.
        var unmet = s.NotKnown;
        var p = s.Pace;   // the engine leaves it null on a loadout not known
        // What was played decides the rows: farming and grouped by T5-eq/h, leveling by levels/h (docs/pace.md).
        bool byMotes = farming || VisitKinds.ByMotes(p?.Kind ?? VisitKinds.Leveling);
        var run = byMotes ? null : p?.Run;
        var fm = p?.Farm;
        // The XP penalty beside the place, as players say it ("+20% XP to level": a bigger bar, not less XP a kill): the ticks below are for this trio's XP.
        if (run is { XpMultiplier: > 1.001 } && !compact)
            HeadZone.Inlines.Add(new Run($"  +{(run.XpMultiplier - 1) * 100:0}% XP to level") { Foreground = XpBlue, FontSize = 11, FontWeight = FontWeights.Normal });
        // One judge for every tick, bar colour, and word on the card: RunPace for leveling, FarmPace for farming/grouped.
        // and grouped play, each with its own band. A raid, or a visit whose level isn't known, has no judge: values and no ticks.
        bool raid = p?.Kind == VisitKinds.Raid;
        string verdict = raid ? "" : fm?.Verdict ?? run?.Verdict ?? "";
        bool none = verdict == "none";
        bool usual = verdict is not ("" or "none");   // a usual to tick against

        // The rows, what is being played first. Compact shows only that one.
        // The engine assigns each row's tone from its verdict; the overlay only draws it. A raid has no judge.
        var (killTone, xpTone, t5Tone) = raid ? (Tone.None, Tone.None, Tone.None) : (run?.KillSpeedTone ?? Tone.None, run?.XpTone ?? Tone.None, fm?.Tone ?? Tone.None);
        double? Tick(double? x) => usual ? x : null;
        var lv = new Metric("Levels/h", c.LevelsPerHour, Tick(run?.ExpectedLevelsPerHour), "0.0", null);
        var t5 = new Metric("T5-eq/h", fm != null && !raid ? fm.T5PerHour : c.T5PerHour, Tick(fm?.Usual ?? run?.UsualT5PerHour), "0", Gold, t5Tone);
        var kills = run != null
            ? new Metric("Kill speed", raid ? c.KillsPerMinute : run.KillsPerMinute ?? c.KillsPerMinute, Tick(run.UsualKillsPerMinute), "0.0", null, killTone)
            : new Metric("Kills/min", c.KillsPerMinute, Tick(fm?.UsualKillsPerMinute), "0.0", null);
        var xp = new Metric("XP per kill", run != null && !raid ? run.XpPerKill : c.XpPerKill, Tick(run?.ExpectedXpPerKill), "0.00'%'", null, xpTone);
        // Below the cap a crawl judged by T5-eq/h still levels you: show Levels/h underneath with no tick or colour.
        bool levelsToo = byMotes && !farming && !raid && (fm == null || fm.Levels != null);
        var rows = raid ? new[] { t5, kills } : levelsToo ? new[] { t5, lv, kills } : byMotes ? new[] { t5, kills } : new[] { lv, t5, kills, xp };

        // The verdict in a word, on the lead row.
        (string, Brush) word;
        if (c.State == "paused") word = ("outside · can resume", Warn);
        else if (unmet) word = ("type /who", Dim);
        else if (fm != null && !raid) word = fm.Verdict switch
        {
            "more" => ($"{fm.Pct!.Value * 100:0}% more", GoodText),
            "less" => ($"{-fm.Pct!.Value * 100:0}% less", Warn),
            "usual" => ("usual", GoodText),
            "early" => ("warming up", Dim),
            _ => ("no usual yet", Dim),
        };
        else if (run != null && !raid) word = run.Verdict switch
        {
            "pace" => ("on pace", GoodText),
            "slow" => ($"{(1 - run.KillSpeed!.Value) * 100:0}% slow", Warn),
            "fast" => ($"{(run.KillSpeed!.Value - 1) * 100:0}% fast", GoodText),
            "early" => ("warming up", Dim),
            _ => ("no usual yet", Dim),
        };
        else word = ("", Dim);
        Right((Dur(c.Minutes), Muted), (word.Item1, word.Item2));

        // Rows with nothing on either side (no rate yet, no usual) are left out, all but the lead and a Levels/h kept while leveling.
        var shown = rows.Where((m, i) => i == 0 || levelsToo && m == lv || m.Now is > 0 || m.Usual is > 0);
        foreach (var m in compact ? rows.Take(levelsToo ? 2 : 1) : shown) Row(m.Label, m.Now, m.Usual, m.Format, m.Value, m.Tone, dashed: none || unmet);
        if (farming)
        {
            var drops = s.SessionHighTier.Where(d => d.Time >= c.Start).ToList();
            FootBar.Visibility = Visibility.Collapsed;
            FootLeft.Inlines.Clear();
            if (drops.Count > 0)
            {
                FootLeft.Inlines.Add(new Run($"★ {drops.Sum(d => d.Count)} ") { Foreground = Gold });
                FootLeft.Inlines.Add(new Run("above T5: " + string.Join(", ", drops.Take(3).Select(d => (d.Kind == LootKind.Fragment ? "Fragment of " : "") + d.Tier)))
                { Foreground = Muted, FontWeight = FontWeights.Normal });
            }
            else FootLeft.Inlines.Add(new Run("nothing above T5 yet") { Foreground = Dim, FontWeight = FontWeights.Normal });
            FootRight.Text = c.ChestT5 > 0 ? $"chest {c.ChestT5:0.#}" : "";
        }
        else LevelFooter(s, compact);

        // One line of why, only when it helps.
        if (unmet) { Note.Text = compact ? "" : UnmetNote(s); Note.Foreground = Dim; }
        else if (raid || p == null) Note.Text = "";
        else if (run != null) RunNote(run, p.Place, c, s.Level, compact);
        else if (fm != null)
        {
            Note.Text = compact ? "" : Rates.Early(c.Completed, c.Kills) ? NoRateNote(c, true)
                : fm.Verdict is "early" or "none" ? FarmPace.NoUsualNote(fm.Runs, fm.Levels)
                : $"T5-eq/h against your usual here: {fm.Basis}, {fm.Runs} visits.";
            Note.Foreground = Dim;
        }
    }
    private void DrawDone(LiveSnapshot s, NextStep next, bool farming, bool compact)
    {
        var last = next.Last!;
        HeadZone.Inlines.Clear();
        HeadZone.Inlines.Add(new Run("✓ ") { Foreground = GoodText });
        HeadZone.Inlines.Add(new Run($"{Zone(last.BaseZone, last.Difficulty)} done"));
        bool byMotes = farming || VisitKinds.ByMotes(next.LastKind ?? VisitKinds.Leveling);
        var lp = byMotes ? null : next.LastPace;
        Right(byMotes ? ($"{last.T5:0.#} T5-eq", Gold) : ($"+{last.LevelsGained:0.00} levels" + (lp != null ? $" · {last.ActiveMinutes:0} min" : ""), Text));
        string at = last.LevelStart is { } l0 ? $" at {l0}" : "";
        TextBlock Line(string text, Brush brush, double size = 11) => new()
        {
            Text = text,
            FontSize = size,
            Foreground = brush,
            Margin = new Thickness(0, 2, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        if (!compact)
        {
            double? rate = byMotes ? next.LastFarm?.T5PerHour ?? last.T5PerHour : last.LevelsPerHour;
            // Leveling: split kill levels/h as kills/min * 60 * XP/kill / 100 when every kill's XP is known (docs/pace.md).
            if (lp != null && last.KillLevelsPerHour is { } klh && last.XpPerKill is { } perKill && last.KillsPerMinute is { } kpm)
            {
                Rows.Children.Add(Line($"{klh.ToString("0.0", CI)} levels/h from kills = {kpm.ToString("0.0", CI)} kills/min × 60 × {perKill.ToString("0.00", CI)}% of a level", Text, 12.5));
                if (last.RewardLevels > 0 && rate is { } whole) Rows.Children.Add(Line($"{whole.ToString("0.0", CI)} levels/h with the crawl's completion XP", Dim, 10.5));
            }
            else Row(byMotes ? "T5-eq/h" : "Levels/h", rate, lp != null ? null : next.LastFarm?.Usual, byMotes ? "0" : "0.0", byMotes ? Gold : null, byMotes && lp == null ? next.LastFarm?.Tone ?? Tone.None : Tone.None, s.NotKnown);
            // Below the cap, show the crawl's Levels/h under the T5-eq/h that judged it with no tick or colour.
            if (byMotes && !farming && next.LastKind != VisitKinds.Raid && (next.LastFarm == null || next.LastFarm.Levels != null))
                Row("Levels/h", last.LevelsPerHour, null, "0.0", dashed: s.NotKnown);
            if (next.LastKind == VisitKinds.Raid) Rows.Children.Add(Line(next.LastBoss != null ? $"{next.LastBoss} killed" : "No boss kill", next.LastBoss != null ? GoodText : Dim));
            else if (next.LastFarm is { Verdict: "more" or "less" or "usual" } lf)
                Rows.Children.Add(Line(lf.Verdict == "usual" ? $"Usual T5-eq/h: {(lf.T5PerHour ?? 0).ToString("0", CI)} against {lf.Usual!.Value.ToString("0", CI)}{at}" : $"{Math.Abs(lf.Pct!.Value) * 100:0}% {(lf.Verdict == "more" ? "more" : "less")} T5-eq/h than your usual{(last.AtCap ? at : "")} ({lf.Basis})", lf.Verdict == "less" ? WarnText : GoodText));
            if (lp != null)
            {
                string Kpm(double? x) => (x ?? 0).ToString("0.0", CI);
                var (text, brush) = lp.Verdict switch
                {
                    "slow" => ($"{(1 - lp.KillSpeed!.Value) * 100:0}% slow: {Kpm(lp.KillsPerMinute)} kills a minute, your usual {Kpm(lp.UsualKillsPerMinute)}", WarnText),
                    "fast" => ($"{(lp.KillSpeed!.Value - 1) * 100:0}% fast: {Kpm(lp.KillsPerMinute)} kills a minute, your usual {Kpm(lp.UsualKillsPerMinute)}", GoodText),
                    "pace" => ("On pace" + lp.Xp switch { "normal" => $" · XP per kill normal here{at}", "low" => $" · XP per kill low for here{at}", _ => "" }, GoodText),
                    // A usual, but too little of this crawl to judge (left before the rate rule's kills): never "3 of 3".
                    "early" => ($"No verdict: the rate starts at {Rates.MinKills} kills, and this crawl had {last.Kills}", Dim),
                    _ => (RunPace.NoUsualNote(lp.Runs, last.LevelStart) + (lp.Others is { } o
                        ? $" · players here {o.KillsPerMinute.ToString("0.0", CI)} kills a minute, {o.LevelsPerHour.ToString("0.0", CI)} levels/h" : ""), Dim),
                };
                Rows.Children.Add(Line(text, brush));
                if (next.LastPlace is { } pl)
                    Rows.Children.Add(Line($"{(pl.Kind == "fading" ? "Fading · " : "")}{pl.Better} levels faster: {pl.BetterLevelsPerHour.ToString("0.0", CI)} levels/h{at}", Amber));
            }
            else if (next.Place is { } place && next.Of is { } of)
                Rows.Children.Add(new TextBlock
                {
                    Text = $"#{place} of your {of} {Zone(last.BaseZone, last.Difficulty)} visits like this one",
                    FontSize = 11,
                    Foreground = Dim,
                    Margin = new Thickness(0, 2, 0, 0),
                });
        }
        if (s.NotKnown && s.Level == null) Rows.Children.Add(Line("Type /who in game for where to go next.", Dim));
        else if (next.Options.Count > 0)
        {
            // Ranked by what you are doing: T5-eq an hour when farming, levels an hour when leveling.
            var (unit, format) = byMotes ? ("T5-eq/h", "0") : ("Lv/h", "0.0");
            double Val(NextOption o) => unit == "T5-eq/h" ? o.T5PerHour : o.LevelsPerHour;
            // Leveling numbers are for this trio's XP: say so, as players say it.
            string xpNote = !byMotes && unit == "Lv/h" && !s.NotKnown && lp is { XpMultiplier: > 1.001 } ? $" at +{(lp.XpMultiplier - 1) * 100:0}% XP to level" : "";
            Rows.Children.Add(new TextBlock
            {
                Text = $"NEXT · estimated {unit}{xpNote}",
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Muted,
                Margin = new Thickness(0, compact ? 4 : 7, 0, 1),
                TextWrapping = TextWrapping.Wrap,   // a forecast from Ask's tables, not a verdict
            });
            double max = next.Options.Max(Val);
            int i = 0;
            foreach (var o in next.Options.Take(compact ? 1 : 3))
            {
                double v = Val(o);
                string label = o.FromLevel is { } from ? $"{o.Label} {from}+" : o.Label + (o.Again ? " again" : "");
                AddRow(label, v.ToString(format, CI), i == 0 ? Text : Muted, max > 0 ? v / max * 0.8 : 0, double.NaN,
                    i == 0 ? Gold : GoldSoft, 128);
                i++;
            }
            // The crawl's own place check (as of its start) found a faster place that was not clearly faster: say why nothing is flagged.
            string lastLabel = Zone(last.BaseZone, last.Difficulty);
            if (!compact && lp != null && next.LastPlace == null && next.LastNotClear is { } notClear)
                Rows.Children.Add(Line($"{notClear} isn't clearly faster than {lastLabel}{at} (within a run's normal swing), so no Fading.", Dim));
        }
        if (farming || s.Level == null && !s.NotKnown) Footer.Visibility = Visibility.Collapsed;
        else LevelFooter(s, compact);
    }

    private static string UnmetNote(LiveSnapshot s) => s.Loadout!.State == "classes"
        ? $"You changed a class on loadout {s.Loadout.Slot}. Type /who once and your usual comes back."
        : $"Pathfinder hasn't met loadout {s.Loadout.Slot} yet. Type /who once and your usual comes back.";

    private void LevelFooter(LiveSnapshot s, bool compact)
    {
        FootLeft.Inlines.Clear();
        if (s.Level == null && s.NotKnown)
        {
            FootLeft.Inlines.Add(new Run("—"));
            FootBar.Set(0, double.NaN, Gold);
            FootRight.Text = "level after a /who";
            return;
        }
        FootLeft.Inlines.Add(new Run(s.Level?.ToString() ?? "—"));
        double pct = s.LevelPercent ?? 0;
        FootBar.Set(pct / 100, double.NaN, Gold);
        var parts = new List<string>();
        if (s.NextLevelMinutes is { } m && m < 600) parts.Add($"next {m:0}m");
        else if (s.LevelPercent == null) parts.Add("% after the next ding");
        if (!compact && s.Journey is { EtaHours: { } eta }) parts.Add($"≈{eta:0.0} h to {s.Journey.Cap}");
        FootRight.Text = string.Join(" · ", parts);
    }

    private void DrawDetailed(LiveSnapshot s, bool farming)
    {
        var ses = s.Session;
        if (ses.Visits > 0)
        {
            SesTitle.Text = $"SESSION · {ses.Visits} CRAWL{(ses.Visits > 1 ? "S" : "")} · {Zone(ses.Zone ?? "", ses.Difficulty).ToUpperInvariant()}";
            SesLine.Text = ses.Early
                ? (farming ? $"{ses.T5:0.#} T5-eq so far" : $"{ses.Kills} kills so far") + $" · the rate starts at {ses.RateFromKills} kills, or when a crawl is finished"
                : farming
                ? $"{ses.T5PerHourCrawl:0} T5-eq/h in crawls · {ses.T5PerHourWall:0} with resets · {ses.T5:0.#} total"
                : $"{ses.LevelsPerHourCrawl:0.0} levels/h in crawls · {ses.LevelsPerHourWall:0.0} with resets · {ses.Kills} kills";
        }
        else { SesTitle.Text = "SESSION"; SesLine.Text = "No crawls yet."; }
        var hi = s.SessionHighTier.Take(4).Select(d => $"{(d.Kind == LootKind.Fragment ? "Fragment of " : "")}{d.Tier}{(d.Count > 1 ? $" ×{d.Count}" : "")}").ToList();
        HighLine.Text = hi.Count > 0 ? "★ above T5: " + string.Join(", ", hi) : "";
        HighLine.Visibility = hi.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        RecentRows.Children.Clear();
        var recent = s.Recent.Take(5).ToList();
        double max = recent.Count > 0 ? recent.Max(v => (farming ? v.T5PerHour : v.LevelsPerHour) ?? 0) : 0;
        foreach (var v in recent)
        {
            double? x = farming ? v.T5PerHour : v.LevelsPerHour;   // null: no rate yet, "—" and no bar
            RecentRows.Children.Add(MakeRow($"{v.Start:HH:mm} {Zone(v.BaseZone, v.Difficulty)}", x is { } xv ? xv.ToString(farming ? "0" : "0.0", CI) : "—", x == null ? Dim : Muted,
                max > 0 && x != null ? x.Value / max * 0.8 : 0, double.NaN, Neutral, 140, 11.5));
        }
        if (recent.Count == 0) RecentRows.Children.Add(new TextBlock { Text = "Finished crawls appear here.", FontSize = 11.5, Foreground = Dim });
    }

    // ------------------------------------------------------------------ pieces

    private sealed record Metric(string Label, double? Now, double? Usual, string Format, Brush? Value, Tone Tone = Tone.None);

    /// <summary>The leveling note (docs/pace.md): why it's slow, a low XP per kill, a clearly better place, or what the tick is.</summary>
    private void RunNote(RunPace r, PlaceCheck? place, VisitStats c, int? level, bool compact)
    {
        string at = level is { } l ? $" at {l}" : "";
        if (r.Verdict == "early") { Note.Text = compact ? "" : Rates.Early(c.Completed, c.Kills) ? NoRateNote(c, false) : ""; return; }
        if (r.Verdict == "slow")
        {
            Note.Text = $"Slow kills: {(r.KillsPerMinute ?? 0).ToString("0.0", CI)} a minute, your usual here is {r.UsualKillsPerMinute!.Value.ToString("0.0", CI)}."
                + r.Xp switch { "low" => " XP per kill is low too: mobs may be conning low.", "normal" => " XP per kill is normal, so it's the killing.", _ => "" };
            Note.Foreground = WarnText;
            return;
        }
        if (r.Xp == "low")
        {
            Note.Text = $"Less XP per kill than this place gives{at}: mobs may be conning low, or a group is splitting it.";
            Note.Foreground = WarnText;
            return;
        }
        string? placeText = place == null ? null : $"{(place.Kind == "fading" ? "Fading · " : "")}{place.Better} levels faster: "
            + $"{place.BetterLevelsPerHour.ToString("0.0", CI)} levels/h{at}, here {place.LevelsPerHour.ToString("0.0", CI)}.";
        if (r.Verdict == "none" && !compact)
        {
            // No usual of your own, and maybe a clearly faster place: both say something, so both show.
            Note.Inlines.Clear();
            Note.Inlines.Add(new Run(RunPace.NoUsualNote(r.Runs, c.LevelStart ?? level) + "." + (r.Others is { } o
                ? $" Players here: {o.KillsPerMinute.ToString("0.0", CI)} kills a minute, {o.LevelsPerHour.ToString("0.0", CI)} levels/h ({o.Players} players)." : ""))
            { Foreground = Dim });
            if (placeText != null) Note.Inlines.Add(new Run(" " + placeText) { Foreground = Amber });
            return;
        }
        if (placeText != null)
        {
            Note.Text = placeText;
            Note.Foreground = Amber;
            return;
        }
        if (compact) { Note.Text = ""; return; }
        Note.Text = c.Difficulty != null && !c.Completed && r.ExpectedLevelsPerHour is { } e && c.LevelsPerHour is { } clph && clph < e
            ? "Completion XP lands at the end of the crawl." : $"tick = your usual here{at}, from {r.Runs} runs";
    }

    /// <summary>The note while the crawl has no rate: when it starts and what's in so far (the number comes from <see cref="Rates.MinKills"/>).</summary>
    private static string NoRateNote(VisitStats c, bool farming) =>
        $"The rate starts at {Rates.MinKills} kills: " + (farming ? $"{c.T5.ToString("0.#", CI)} T5-eq so far." : $"{c.Kills} so far.");

    /// <summary>A meter row: the bar is this crawl, scaled so your usual sits at 80% when you match it; its colour is the verdict's tone, grey with none.</summary>
    private void Row(string label, double? now, double? usual, string format, Brush? valueBrush = null, Tone tone = Tone.None, bool dashed = false)
    {
        double top = Math.Max(now ?? 0, usual ?? now ?? 0) * 1.25;
        double fill = top > 0 ? (now ?? 0) / top : 0, tick = usual is > 0 && top > 0 ? usual.Value / top : double.NaN;
        Brush bar = tone switch { Tone.Good => Good, Tone.Warn => Warn, _ => Neutral }, value = valueBrush ?? (tone == Tone.Warn ? Warn : Text);
        // No rate yet (Rates): "—" in Dim, no bar, the usual's tick stays.
        if (now == null) value = Dim;
        AddRow(label, now?.ToString(format, CI) ?? "—", value, fill, tick, bar, 74, dashed);
    }

    private void AddRow(string label, string value, Brush valueBrush, double fill, double tick, Brush bar, double labelWidth, bool dashed = false) =>
        Rows.Children.Add(MakeRow(label, value, valueBrush, fill, tick, bar, labelWidth, 12.5, dashed));

    private static Grid MakeRow(string label, string value, Brush valueBrush, double fill, double tick, Brush bar, double labelWidth, double size, bool dashed = false)
    {
        var g = new Grid { Height = 22 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        var l = new TextBlock { Text = label, Foreground = Muted, FontSize = size, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var b = new MeterBar { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Dashed = dashed };
        b.Set(fill, tick, bar);
        var v = new TextBlock { Text = value, Foreground = valueBrush, FontSize = size, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(b, 1);
        Grid.SetColumn(v, 2);
        g.Children.Add(l);
        g.Children.Add(b);
        g.Children.Add(v);
        return g;
    }

    private void Right(params (string text, Brush brush)[] parts)
    {
        HeadRight.Inlines.Clear();
        foreach (var (text, brush) in parts.Where(p => p.text.Length > 0))
        {
            if (HeadRight.Inlines.Count > 0) HeadRight.Inlines.Add(new Run(" · ") { Foreground = Dim });
            HeadRight.Inlines.Add(new Run(text) { Foreground = brush });
        }
    }

    private static string LogStatus(LiveSnapshot s) =>
        s.LogTime is not { } lt ? "waiting for the log" : s.LogQuiet ? $"log quiet since {lt:HH:mm}" : $"● {lt:HH:mm}";

    private static string Zone(string baseZone, int? d) =>
        (baseZone.StartsWith("The ", StringComparison.Ordinal) ? baseZone[4..] : baseZone) + (d is { } x ? $" D{x}" : "");

    private static string Dur(double minutes)
    {
        var t = TimeSpan.FromMinutes(Math.Max(0, minutes));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }

    private static Brush Frozen(string hex)
    {
        var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        b.Freeze();
        return b;
    }
}
