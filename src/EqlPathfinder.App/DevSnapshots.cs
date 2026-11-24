using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Pages;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;
using Microsoft.Web.WebView2.Core;

namespace EqlPathfinder.App;

/// <summary>
/// Developer check: EQLPathfinder.exe --snapshot &lt;outDir&gt; [db] renders the overlay and setup window
/// off-screen to PNGs and writes web/preview.html with real data inlined. No window is shown.
/// </summary>
internal static class DevSnapshots
{
    /// <summary>The same crawl cut after its 5th kill and not finished: every rate null, the verdicts "early", as the engine sends it.</summary>
    private static System.Text.Json.Nodes.JsonNode? NewPlannerView(TrackerService svc, EqlPathfinder.Engine.Analysis.CampaignPlan plan, EqlPathfinder.Engine.Analysis.PlannerInput input) =>
        JsonSerializer.SerializeToNode(svc.ReincarnationData(plan, svc.History(5000), input), PageJson.Options)!["campaign"];

    private static LiveSnapshot NoRateYet(LiveSnapshot s) => s.Current == null ? s : s with
    {
        Current = s.Current with { Kills = 5, Completed = false, LevelsPerHour = null, T5PerHour = null, KillsPerMinute = null, Dps = null },
        // Both verdicts early: whichever kind the preview is, nothing is judged yet.
        Pace = s.Pace == null ? null : s.Pace with
        {
            Farm = s.Pace.Farm == null ? null : s.Pace.Farm with { Verdict = "early", T5PerHour = null, Usual = s.Pace.Farm.Usual ?? 40 },
            Run = s.Pace.Run == null ? null : s.Pace.Run with { Verdict = "early" },
        },
    };

    /// <summary>A leveling crawl that just ended with its completion XP sized: the overlay's done card, its equation line and the rate with the reward.</summary>
    private static LiveSnapshot NotKnown(LiveSnapshot s, int? level) => s with
    {
        Loadout = new LoadoutOn(6, "new"),
        Level = level,
        Trio = null,
        Race = null,
        LevelPercent = null,
        NextLevelMinutes = null,
        Journey = null,
    };
    private static LiveSnapshot DoneLeveling(LiveSnapshot s) => s.Current == null ? s : s with
    {
        Current = null,
        Next = new NextStep
        {
            Last = s.Current with
            {
                State = "finished",
                Completed = true,
                Kills = 60,
                XpKills = 60,
                KillLevels = 0.68,
                RewardLevels = 0.22,
                RewardSizing = false,
                ActiveMinutes = 20,
                KillsPerMinute = 3.0,
                LevelsPerHour = 2.7
            },
            LastPace = s.Pace?.Run,
            LastKind = EqlPathfinder.Engine.Analysis.VisitKinds.Leveling
        },
    };

    /// <summary>The leveling crawl run slow with a low XP per kill (overlay-metric-tones): Kill speed and XP per kill red, the note quoting the bar's number.</summary>
    private static LiveSnapshot SlowLeveling(LiveSnapshot s) => s.Current == null || s.Pace == null ? s : s with
    {
        // Whatever the last visit on this PC was, shown as a leveling crawl 24% under its usual with a low XP per kill (the engine's own numbers: 3.1 of 4.1 kills a minute).
        Current = s.Current with { Completed = false, Kills = 60, LevelsPerHour = 2.1, KillsPerMinute = 3.1, T5PerHour = 41 },
        Pace = s.Pace with
        {
            Kind = EqlPathfinder.Engine.Analysis.VisitKinds.Leveling,
            Farm = null,
            Run = new EqlPathfinder.Engine.Analysis.RunPace("slow", 0.76, 4.1, 5, 0.15, 0.6, 0.9, "low", 2.2) { XpMultiplier = 1.2, UsualT5PerHour = 45 }
        },
    };

    /// <summary>The leveling crawl on pace with a normal XP per kill: green Kill speed and XP per kill, grey Levels/h and T5-eq/h (the real database has no usual to show it).</summary>
    private static LiveSnapshot PaceLeveling(LiveSnapshot s) => s.Current == null || s.Pace == null ? s : s with
    {
        Current = s.Current with { Completed = false, Kills = 60, LevelsPerHour = 2.6, KillsPerMinute = 3.9, T5PerHour = 41 },
        Pace = s.Pace with
        {
            Kind = EqlPathfinder.Engine.Analysis.VisitKinds.Leveling,
            Farm = null,
            Run = new EqlPathfinder.Engine.Analysis.RunPace("pace", 0.95, 4.1, 5, 0.15, 0.9, 0.9, "normal", 2.7) { XpMultiplier = 1.2, UsualT5PerHour = 45 }
        },
    };

    /// <summary>A farming crawl 30% under its usual: the T5-eq/h bar red, Kills/min grey.</summary>
    private static LiveSnapshot FarmingLess(LiveSnapshot s) => s.Current == null || s.Pace?.Farm == null ? s : s with
    {
        Current = s.Current with { Completed = false, Kills = 60, T5PerHour = (s.Pace.Farm.Usual ?? 40) * 0.7 },
        Pace = s.Pace with { Farm = s.Pace.Farm with { Verdict = "less", Pct = -0.3, Usual = s.Pace.Farm.Usual ?? 40, T5PerHour = (s.Pace.Farm.Usual ?? 40) * 0.7 } },
    };

    /// <summary>
    /// A D4 crawl below the cap with no usual yet, 1 of 3 earlier visits (overlay-levels-wording): judged by T5-eq/h, with Levels/h
    /// under it, grey and no tick, and Compact showing both.
    /// </summary>
    private static LiveSnapshot MotesLeveling(LiveSnapshot s)
    {
        if (s.Current == null || s.Pace == null) return s;
        string levels = EqlPathfinder.Engine.Analysis.FarmPace.LevelsOf(s.Current.LevelStart ?? 20);
        return s with
        {
            Current = s.Current with { Difficulty = 4, Completed = false, Kills = 55, LevelsPerHour = 0.4, KillsPerMinute = 3.1, T5PerHour = 42 },
            Pace = s.Pace with
            {
                Kind = EqlPathfinder.Engine.Analysis.VisitKinds.Farming,
                Run = null,
                Place = null,
                Farm = new EqlPathfinder.Engine.Analysis.FarmPace("none", null, null, 1, EqlPathfinder.Engine.Analysis.FarmPace.DefaultBand, 42, $"crawls you finished, solo, {levels}") { Levels = levels }
            },
        };
    }

    /// <summary>The same crawl just ended, 30% over a usual of 4 visits: the done card's Levels/h row under T5-eq/h.</summary>
    private static LiveSnapshot DoneMotesLeveling(LiveSnapshot s)
    {
        var m = MotesLeveling(s);
        if (m.Current == null || m.Pace?.Farm == null) return m;
        return m with
        {
            Current = null,
            Next = new NextStep
            {
                Last = m.Current with { State = "finished", Completed = true, LevelsPerHour = 1.1, T5PerHour = 55 },
                LastFarm = m.Pace.Farm with { Verdict = "more", Pct = 0.31, Usual = 42, Runs = 4, T5PerHour = 55 },
                LastKind = EqlPathfinder.Engine.Analysis.VisitKinds.Farming
            },
        };
    }

    /// <summary>A leveling crawl with 2 of the 3 earlier runs a usual needs: the note counts them.</summary>
    private static LiveSnapshot LevelingNoUsual(LiveSnapshot s) => s.Current == null || s.Pace == null ? s : s with
    {
        Current = s.Current with { Completed = false, Kills = 40, LevelsPerHour = 2.4, KillsPerMinute = 3.6, T5PerHour = 30 },
        Pace = s.Pace with
        {
            Kind = EqlPathfinder.Engine.Analysis.VisitKinds.Leveling,
            Farm = null,
            Place = null,
            Run = new EqlPathfinder.Engine.Analysis.RunPace("none", null, null, 2, 0.18, 0.6, null, null, null) { XpMultiplier = 1.2 }
        },
    };

    /// <summary>A farming visit that just ended, a fifth under its usual: the done card's T5-eq/h bar takes the verdict's colour.</summary>
    private static LiveSnapshot DoneFarming(LiveSnapshot s) => s.Current == null || s.Pace?.Farm == null ? s : s with
    {
        Current = null,
        Next = new NextStep
        {
            Last = s.Current with { State = "finished", Completed = true, T5PerHour = (s.Pace.Farm.Usual ?? 40) * 0.7 },
            LastFarm = s.Pace.Farm with { Verdict = "less", Pct = -0.3, Usual = s.Pace.Farm.Usual ?? 40, T5PerHour = (s.Pace.Farm.Usual ?? 40) * 0.7 },
            LastKind = EqlPathfinder.Engine.Analysis.VisitKinds.Farming
        },
    };

    /// <summary>The logs the snapshots read: EQLPATHFINDER_LOGS (a folder, such as renamed demo logs for the website) or the game's own.</summary>
    private static string LogsDir => Environment.GetEnvironmentVariable("EQLPATHFINDER_LOGS") is { Length: > 0 } d ? d : LogDiscovery.FindLogsDir()!;

    /// <summary>
    /// The app as a render needs it: its resources, and something to hand <see cref="MainWindow"/> for the title bar.
    /// Never starts the real app. WPF queues OnStartup when an Application is constructed and the first message pump runs,
    /// so using a plain App inside --snapshot would touch the live settings.json and pathfinder.db regardless of CLI args.
    /// Only Program.Main constructs the live App.
    /// </summary>
    private sealed class RenderOnlyApp : App
    {
        protected override void OnStartup(StartupEventArgs e) { }
    }

    public static void Run(string[] args)
    {
        var outDir = args[1];
        var db = args.Length > 2 ? args[2] : AppSettings.DatabasePath;
        Directory.CreateDirectory(outDir);
        var app = new RenderOnlyApp();
        app.InitializeComponent();

        var settings = new AppSettings();
        var options = new TrackerOptions();
        using var store = new PathfinderStore(db);
        var logs = LogDiscovery.FindCharacters(LogsDir)[0];
        using var svc = new TrackerService(store, logs, options);
        if (svc.NeedsRebuild || !svc.HasHistory) svc.ImportHistoryAsync().GetAwaiter().GetResult();
        svc.WaitForGameData(TimeSpan.FromSeconds(30));
        // With the live community file, as a beta copy has it (a developer check, so reading it over the network is fine).
        try
        {
            using var http = new System.Net.Http.HttpClient();
            svc.Community = EqlPathfinder.Engine.Community.CommunityData.Parse(
                http.GetStringAsync(EqlPathfinder.Engine.Sharing.ShareProtocol.DefaultCommunity).GetAwaiter().GetResult());
        }
        catch (Exception ex) { Console.Error.WriteLine("community file: " + ex.Message); }
        var snap = svc.BuildSnapshot();

        var shots = new (string name, string size, LiveSnapshot s)[]
        {
            ("now-compact", "Compact", snap),
            ("now-detailed", "Detailed", snap),
            ("leveling-compact", "Compact", svc.PreviewAsCurrent(false)),
            ("leveling-standard", "Standard", svc.PreviewAsCurrent(false)),
            ("farming-standard", "Standard", svc.PreviewAsCurrent(true)),
            // EQLPATHFINDER_PREVIEW_BEFORE: the leveling overlay during an earlier crawl (the website's shot), e.g. "2026-11-16 22:30".
            ("leveling-at", "Standard", svc.PreviewAsCurrent(false,
                DateTime.TryParse(Environment.GetEnvironmentVariable("EQLPATHFINDER_PREVIEW_BEFORE"), System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var at) ? at : null)),
            // Under Rates.MinKills an unfinished crawl has no rate yet, so the overlay shows "—" and the note.
            ("early", "Standard", NoRateYet(svc.PreviewAsCurrent(false))),
            ("early-farming", "Standard", NoRateYet(svc.PreviewAsCurrent(true))),
            // The overlay after a leveling crawl ends, with its completion XP sized: the equation line and the rate with the reward (xp-at-cap).
            ("done-leveling", "Detailed", DoneLeveling(svc.PreviewAsCurrent(false))),
            ("leveling-slow", "Standard", SlowLeveling(svc.PreviewAsCurrent(false))),
            ("leveling-pace", "Standard", PaceLeveling(svc.PreviewAsCurrent(false))),
            ("leveling-pace-compact", "Compact", PaceLeveling(svc.PreviewAsCurrent(false))),
            ("farming-less", "Standard", FarmingLess(svc.PreviewAsCurrent(true))),
            ("done-farming", "Detailed", DoneFarming(svc.PreviewAsCurrent(true))),
            // Leveling through a farmed difficulty below the cap (overlay-levels-wording): Levels/h stays under T5-eq/h; the no-usual notes count "N of 3".
            ("motes-leveling-standard", "Standard", MotesLeveling(svc.PreviewAsCurrent(false))),
            ("motes-leveling-compact", "Compact", MotesLeveling(svc.PreviewAsCurrent(false))),
            ("early-motes-leveling", "Standard", NoRateYet(MotesLeveling(svc.PreviewAsCurrent(false)))),
            ("done-motes-leveling", "Detailed", DoneMotesLeveling(svc.PreviewAsCurrent(false))),
            ("leveling-no-usual", "Standard", LevelingNoUsual(svc.PreviewAsCurrent(false))),
            // A loadout the app hasn't met (current-loadout): "loadout not known", no ticks, no verdict; between crawls with no level, one line.
            ("not-known", "Standard", NotKnown(svc.PreviewAsCurrent(false), null)),
            ("not-known-nolevel", "Standard", NotKnown(DoneLeveling(svc.PreviewAsCurrent(false)), null)),
            ("logoff-compact", "Compact", snap),
            ("logoff-standard", "Standard", snap),
        };
        foreach (var (name, size, s) in shots)
        {
            settings.Overlay(OverlayKinds.Crawl).Options["size"] = size;
            var ov = new OverlayWindow(settings) { LogOff = name.StartsWith("logoff") };
            ov.Update(s);
            if (name == "now-detailed")
            {
                // A Reward Chest with two drops above T5 arriving in the same second.
                ov.ShowDrop(new HighTierDrop { Kind = LootKind.Mote, Tier = MoteTier.Greater, Count = 1, Source = "Reward Chest", Time = DateTime.Now }, 1.6);
                ov.ShowDrop(new HighTierDrop { Kind = LootKind.Mote, Tier = MoteTier.Grand, Count = 1, Source = "Reward Chest", Time = DateTime.Now }, 1.6);
            }
            if (name == "now-compact")
                ov.ShowDrop(new HighTierDrop { Kind = LootKind.Fragment, Tier = MoteTier.Grand, Count = 1, Source = "Reward Chest", Time = DateTime.Now }, 1.6);
            Render(ov, Path.Combine(outDir, $"overlay-{name}.png"), "#3A4048");
        }

        // The tray icon loads from the same resource at startup; make sure it decodes.
        using (var s = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/pathfinder.ico"))!.Stream)
        using (var ico = new System.Drawing.Icon(s, new System.Drawing.Size(16, 16)))
        using (var bmp = ico.ToBitmap())
            bmp.Save(Path.Combine(outDir, "tray16.png"), System.Drawing.Imaging.ImageFormat.Png);

        RenderSplash(Path.Combine(outDir, "splash.png"));

        // The main window's title bar, filled in as it would be while playing.
        var mw = new MainWindow(app);
        mw.WhoText.Text = logs.Name;
        mw.ServerText.Text = " · " + char.ToUpperInvariant(logs.Server[0]) + logs.Server[1..];
        mw.WhoChip.Visibility = Visibility.Visible;
        mw.WhoChipText.Text = $"{snap.Level} · {snap.Trio} · {snap.Race}";
        mw.StatusDot.Fill = mw.StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xD1, 0x8F));
        mw.StatusText.Text = "Tracking · EverQuest is running";
        mw.UpdateBtn.Visibility = Visibility.Visible;
        mw.UpdateText.Text = "Update 0.4.1 ready · Restart";
        var grid = (Grid)((Border)mw.Content).Child;
        var bar = (Border)grid.Children[0];
        grid.Children.Remove(bar);
        RenderElement(bar, 1280, Path.Combine(outDir, "titlebar.png"));
        // Unmet loadout: the chip says so, and its tooltip explains what to do (written beside the render).
        var (notKnownChip, notKnownTip) = MainWindow.ChipOf(snap with { Level = null, Trio = null, Race = null, Loadout = new LoadoutOn(6, "new") });
        mw.WhoChipText.Text = notKnownChip;
        RenderElement(bar, 1280, Path.Combine(outDir, "titlebar-not-known.png"));
        File.WriteAllText(Path.Combine(outDir, "titlebar-not-known.txt"), notKnownChip + Environment.NewLine + notKnownTip);

        // Plan → Campaign as a new player's "Everything 3/3" would build it from these ranks; the same plan one loadout
        // short (its fixes), and what Swap… and the picker show for its first loadout (the window's campaignOptions answer).
        var roles = new EqlPathfinder.Engine.Analysis.CampaignRoles(EqlPathfinder.Engine.Model.ClassRoles.DefaultCarries, true);
        var demoPlan = svc.RebuildCampaign(EqlPathfinder.Engine.Analysis.Campaign.Preset("all"), roles);
        var shortPlan = EqlPathfinder.Engine.Analysis.Campaign.Preset("all");
        shortPlan.Loadouts = demoPlan.Loadouts.Take(demoPlan.Loadouts.Count - 1).Select(l => l.Copy()).ToList();
        // Your combos: made-up combos on the demo plan, rebuilt by the engine, and the engine's answers for the form.
        EqlPathfinder.Engine.Analysis.CampaignCombo Combo(bool must, bool apart, bool every, int times, bool unlock, params string[] k) => new() { Classes = [.. k], Apart = apart, EveryRun = every, Times = times, Must = must, UnlockNow = unlock };
        // The combos shown are the first of these the engine prices as free (or, for the Prefer, as kept in some runs), so the shots show what the card says on any roster.
        var basePlan = EqlPathfinder.Engine.Analysis.Campaign.Preset("all");
        basePlan.Classes["SHD"] = 2;
        EqlPathfinder.Engine.Analysis.CampaignCombo? First(IEnumerable<string[]> options, Func<EqlPathfinder.Engine.Analysis.CampaignCombo> make, Func<EqlPathfinder.Engine.Analysis.CampaignComboPrice, bool> ok, string[]? not = null)
        {
            foreach (var k in options)
            {
                if (not != null && k.Intersect(not).Any()) continue;
                var c = make(); c.Classes = [.. k];
                var pr = svc.ComboPrice(basePlan, c, null, roles);
                if (pr != null && ok(pr)) return c;
            }
            return null;
        }
        bool Free(EqlPathfinder.Engine.Analysis.CampaignComboPrice p) => p.Free && (p.XpRuns?.Count ?? 0) == 0 && p.Clash == null && !p.AllPast && p.Kept == p.Of;
        string[][] Pairs = new[] { "PAL MNK", "WAR CLR", "RNG DRU", "BRD ENC", "ROG WIZ", "NEC MAG", "BST SHM", "CLR WIZ", "DRU NEC", "MNK ROG", "WAR RNG", "PAL BRD", "MAG ENC", "RNG BST" }.Select(x => x.Split(' ')).ToArray();
        string[][] Trios = new[] { "SHD ROG SHM", "WAR CLR RNG", "PAL MNK BRD", "DRU NEC WIZ", "MAG ENC BST", "CLR SHD WIZ", "RNG DRU ENC" }.Select(x => x.Split(' ')).ToArray();
        var cTrio = First(Trios, () => Combo(true, false, true, 3, true, "-"), Free) ?? Combo(true, false, true, 3, true, "SHD", "ROG", "SHM");
        var cPair = First(Pairs, () => Combo(true, false, true, 3, false, "-"), Free, cTrio.Classes.ToArray()) ?? Combo(true, false, true, 3, false, "PAL", "MNK");
        var cPref = First(Pairs, () => Combo(false, false, true, 3, false, "-"), p => p.Kept > 0 && p.Kept < p.Of && p.AsMust != null, cTrio.Classes.Concat(cPair.Classes).ToArray()) ?? Combo(false, false, true, 3, false, "BST", "MAG");
        var cNever = First(Pairs.Reverse(), () => Combo(false, true, true, 3, false, "-"), Free, cTrio.Classes.Concat(cPair.Classes).ToArray()) ?? Combo(false, true, true, 3, false, "WAR", "BER");
        var comboPlan = EqlPathfinder.Engine.Analysis.Campaign.Preset("all");
        comboPlan.Classes["SHD"] = 2;
        comboPlan.Combos = [cTrio, cPair, cPref, cNever];
        comboPlan = svc.RebuildCampaign(comboPlan, roles);
        // A made-up plan whose only goal is WAR 3/3: a Must trio without WAR then costs runs, on any roster (the first one the engine prices as costing a run).
        EqlPathfinder.Engine.Analysis.CampaignPlan WarOnly() => new() { Classes = new(StringComparer.OrdinalIgnoreCase) { ["WAR"] = 3 } };
        var costCombo = Combo(true, false, true, 3, false, "CLR", "BST", "MNK");
        foreach (var k in new[] { "CLR BST MNK", "SHD ROG SHM", "PAL MNK BRD", "DRU NEC WIZ", "MAG ENC BST" })
        {
            var c = Combo(true, false, true, 3, false, k.Split(' '));
            var pr = svc.ComboPrice(WarOnly(), c, null, roles);
            if (pr != null && pr.Delta.Runs > 0 && pr.Clash == null) { costCombo = c; break; }
        }
        var costPlan = WarOnly();
        costPlan.Combos = [costCombo];
        costPlan = svc.RebuildCampaign(costPlan, roles);
        // A trio sharing one class with the Must pair, in a plan that needs that class's runs for the pair: a real clash.
        var clashOthers = new[] { "WAR", "CLR", "RNG", "DRU", "BRD", "WIZ", "NEC", "MAG" }.Where(k => !cTrio.Classes.Concat(cPair.Classes).Contains(k)).Take(2).ToArray();
        var brokenPlan = comboPlan.Normalized();
        var palRun = brokenPlan.Loadouts.First(l => l.Classes.Contains(cPair.Classes[0]) && l.Classes.Contains(cPair.Classes[1]) && !l.Pinned);
        palRun.Classes[palRun.Classes.IndexOf(cPair.Classes[0])] = new[] { "WIZ", "NEC", "BRD" }.First(x => !palRun.Classes.Contains(x));
        var startPlan = EqlPathfinder.Engine.Analysis.Campaign.Preset("classes"); startPlan.Combos = [Combo(true, false, true, 3, false, "SHD", "ROG", "SHM")];   // no runs yet: the start page, with a combo already written
        // What you want first: two ranked classes and three ranked races on a classes 3/3 plan; nothing ranked; and a Spread out class whose Unlock now clashes with two pinned loadouts.
        EqlPathfinder.Engine.Analysis.CampaignWant W(string k, int t, bool unlock = false) => new() { Key = k, Target = t, UnlockNow = unlock };
        var firstPlan = EqlPathfinder.Engine.Analysis.Campaign.Preset("classes");
        firstPlan.Combos = [Combo(true, false, true, 3, false, "SHD", "ROG", "SHM")];
        firstPlan.ClassOrder = [W("ROG", 3, true), W("BER", 3)];
        firstPlan.RaceOrder = [W("Troll", 3, true), W("Iksar", 3), W("Half Elf", 2)];
        foreach (var w in firstPlan.RaceOrder) firstPlan.Races[w.Key] = w.Target;
        firstPlan.Locked = ["Froglok"];   // a locked race among the Don't care chips
        firstPlan = svc.RebuildCampaign(firstPlan, roles);
        // Everything 3/3 as the click builds it: every race a goal, none ranked.
        var allFirst = EqlPathfinder.Engine.Analysis.Campaign.Preset("classes");
        allFirst.Locked = ["Froglok"];
        allFirst.RaceOrder = [];
        foreach (var r in EqlPathfinder.Engine.Model.ClassNames.Races) allFirst.Races[r] = 3;
        allFirst = svc.RebuildCampaign(allFirst, roles);
        // No plan yet: the card under What you want, with a class and two races ranked and no runs to price.
        var startFirst = EqlPathfinder.Engine.Analysis.Campaign.Preset("classes");
        startFirst.Loadouts = []; startFirst.ClassOrder = [W("ROG", 3)]; startFirst.RaceOrder = [W("Troll", 3), W("Iksar", 3)];
        foreach (var w in startFirst.RaceOrder) startFirst.Races[w.Key] = w.Target;
        var emptyFirst = EqlPathfinder.Engine.Analysis.Campaign.Preset("classes");
        emptyFirst.ClassOrder = []; emptyFirst.RaceOrder = [];
        emptyFirst = svc.RebuildCampaign(emptyFirst, roles);
        // A ranked race that is still locked (its line, Unlock now off), and a race row at its goal (patched below: the demo has played no reincarnation).
        var lockedFirst = EqlPathfinder.Engine.Analysis.Campaign.Preset("classes");
        lockedFirst.ClassOrder = [W("ROG", 3)]; lockedFirst.RaceOrder = [W("Troll", 3), W("Froglok", 3), W("Iksar", 3)];
        foreach (var w in lockedFirst.RaceOrder) lockedFirst.Races[w.Key] = w.Target;
        lockedFirst.Locked = ["Froglok"];
        lockedFirst = svc.RebuildCampaign(lockedFirst, roles);
        var clashFirst = EqlPathfinder.Engine.Analysis.Campaign.Preset("classes");
        clashFirst.ClassOrder = [W("ROG", 3)]; clashFirst.RaceOrder = [W("Troll", 3, true), W("Iksar", 3, true)];
        foreach (var w in clashFirst.RaceOrder) clashFirst.Races[w.Key] = w.Target;
        clashFirst = svc.RebuildCampaign(clashFirst, roles);
        foreach (var (l, n) in clashFirst.Loadouts.Select((l, n) => (l, n)).ToList()) l.Pinned = n < 1 || (n < 8 && !l.Classes.Contains("ROG")) || l.Classes.Contains("ROG") && n < 5;
        clashFirst = svc.RebuildCampaign(clashFirst, roles);
        var campDemo = JsonSerializer.Serialize(new
        {
            combos = NewPlannerView(svc, comboPlan, new(roles, false)),
            combosStart = NewPlannerView(svc, startPlan, new(roles, false)),
            combosCost = NewPlannerView(svc, costPlan, new(roles, false)),
            combosBroken = NewPlannerView(svc, brokenPlan, new(roles, false)),
            first = NewPlannerView(svc, firstPlan, new(roles, false)),
            firstAll = NewPlannerView(svc, allFirst, new(roles, false)),
            firstOnce = NewPlannerView(svc, allFirst, new(roles, false) { RaceListEmptied = EqlPathfinder.Engine.Analysis.PlannerSwitch.RaceListEmptiedLine }),
            startFirst = NewPlannerView(svc, startFirst, new(roles, false)),
            firstEmpty = NewPlannerView(svc, emptyFirst, new(roles, false)),
            firstClash = NewPlannerView(svc, clashFirst, new(roles, false)),
            firstLocked = NewPlannerView(svc, lockedFirst, new(roles, false)),
            priceAdd = svc.ComboPrice(demoPlan, Combo(false, false, true, 3, false, "PAL", "BER"), null, roles),
            priceTry = svc.ComboPrice(demoPlan, Combo(false, false, false, 3, false, "SHD", "ROG", "SHM"), null, roles),
            clashPicks = new[] { cPair.Classes[0], clashOthers[0], clashOthers[1] },
            priceClash = svc.ComboPrice(comboPlan, Combo(true, false, true, 3, false, cPair.Classes[0], clashOthers[0], clashOthers[1]), null, roles),
            options = svc.CampaignOptions(demoPlan, 0, roles),
            shortView = svc.CampaignData(shortPlan, svc.Journeys(), roles),
            // The planner for everyone: a plan, and "Campaign has a new planner" over a plan and over the start page (an old plan is stored).
            plan = NewPlannerView(svc, demoPlan, new(new(EqlPathfinder.Engine.Model.ClassRoles.DefaultCarries, true), false)),
            newPlan = NewPlannerView(svc, demoPlan, new(new(EqlPathfinder.Engine.Model.ClassRoles.DefaultCarries, true), true)),
            newEmpty = NewPlannerView(svc, new EqlPathfinder.Engine.Analysis.CampaignPlan(), new(new(EqlPathfinder.Engine.Model.ClassRoles.DefaultCarries, true), true)),
        }, PageJson.Options);
        // Builds page data via the same engine builder and settings mapping as MainWindow.Refresh,
        // using new-player tour defaults with stand-ins for live-app-only fields.
        var player = new AppSettings();
        WelcomeWindow.MarkTourFinished(player);
        var pageSettings = App.SettingsForPage(player, overlaysLocked: false, arranging: false, version: "0.4.0", pendingUpdate: null,
            updatesEnabled: true, gameRunning: true, onBetaBuild: false, campaignAnswered: 0) with
        { LogsDir = LogsDir, Character = logs.Key };
        var json = JsonSerializer.Serialize(PageData.Build(svc, snap, demoPlan, pageSettings), PageJson.Options);
        // Loot's rows as the window answers the page's "loot" (MainWindow.SendLoot): an answer, not part of the page data.
        var lootMsg = JsonSerializer.Serialize(new { kind = "loot", rows = svc.AllLootCompact() }, PageJson.Options);
        var web = Path.Combine(AppContext.BaseDirectory, "web");
        var html = File.ReadAllText(Path.Combine(web, "index.html"));
        // The Sharing preview as it would come back from the window (a fixed salt: the preview is only rendered here).
        var shareDemo = MainWindow.SharePreviewMessage(svc.SharePayload("0.8.0", new byte[16]));
        // The Ask page's answers as the window would send them: the default question, a farm at 50 and at 46, a difficulty.
        string AskMsg(EqlPathfinder.Engine.Analysis.AskQuery q) => JsonSerializer.Serialize(svc.AskPage(q), PageJson.Options);
        var askDemo = "{" + string.Join(",", new (string, EqlPathfinder.Engine.Analysis.AskQuery)[]
        {
            ("level", new(null, null, null, null, null, null, null)), ("range", new("level", 20, 30, "fast", null, null, null)),
            ("motes", new("level", 46, 50, "motes", null, null, null)), ("farm50", new("farm", null, null, null, 50, "patch", null)),
            ("farm46", new("farm", null, null, null, 46, "patch", null)), ("diff", new("diff", null, null, null, 44, null, null)),
            ("dmg", new("dmg", null, null, null, 46, null, null)), ("pool", new("level", 11, 34, "fast", null, null, null)),
            ("open", new("level", 11, 34, "fast", null, null, null, "open")), ("dmg20", new("dmg", null, null, null, 20, null, null)),
            ("places", new("places", null, null, null, null, "all", null)), ("places-patch", new("places", null, null, null, null, "patch", null)),
            ("farm31", new("farm", null, null, null, 31, "all", null)), ("farm40", new("farm", null, null, null, 40, "all", null)),
            // Two contrast cases: a local-only zone and a community-only zone.
            ("diff-mine", new("diff", null, null, null, 44, null, "Lair of the Splitpaw|31")), ("diff-pool", new("diff", null, null, null, 44, null, "Plane of Hate|46")),
        }.Select(x => $"\"{x.Item1}\":{AskMsg(x.Item2)}")) + "}";
        // The Upgrades page as the window would answer it: the real inventory export, a demo bank, an item +5 to +10.
        var demoBank = new EqlPathfinder.Engine.Upgrades.MoteBank { EnteredAt = DateTime.Now.AddHours(-2), Vtps = 2 };
        (demoBank.Motes[5], demoBank.Motes[6], demoBank.Motes[8]) = (429, 4, 2);
        (demoBank.Fragments[5], demoBank.Fragments[6], demoBank.Fragments[7], demoBank.Fragments[8], demoBank.Fragments[9], demoBank.Fragments[10]) = (40, 6, 2, 2, 1, 1);
        string UpgradeDemo(object request) => JsonSerializer.Serialize(UpgradePage.Build(logs, demoBank, svc.BankEventsAfter(demoBank.EnteredAt.Value),
            JsonSerializer.SerializeToElement(request)), PageJson.Options);
        var upgradeDemo = UpgradeDemo(new { target = "gear", tier = 5, progress = 0, goal = 10, vtps = 0 });
        // Three copies, +5 (worn, 12 points in), +5 and +3, merged on the way to +9 with a VTP.
        var upgradeCopies = UpgradeDemo(new
        {
            target = "gear",
            tier = 5,
            progress = 12,
            goal = 9,
            vtps = 1,
            copies = new object[] { new { tier = 5, progress = 0 }, new { tier = 3, progress = 0 } }
        });
        // preview.html is also opened in Chrome (the app-screenshots skill), where no window is behind the page. There, and
        // only there, a stand-in goes in before core.js looks for the window: the engine's, the one text the page tests
        // run in headless Edge on every check. In WebView2 the real chrome.webview is there and it does nothing.
        const string coreScript = "<script src=\"js/core.js\"></script>";
        if (!html.Contains(coreScript)) throw new InvalidOperationException("index.html no longer loads js/core.js the way the preview expects");
        html = html.Replace(coreScript, "<script>\n" + WindowStandIn.Script + "\n</script>\n" + coreScript);
        // The window's messages, inlined: the page data, then Loot's rows. The off-screen WebView2 below posts them itself,
        // so here they go only to the stand-in (it alone keeps what's posted) and never arrive twice. Nothing is caught: a
        // listener that throws shows in the console, as it would in the app.
        html = html.Replace("</body>", $"<script>window.shareDemo = {shareDemo}; window.askDemo = {askDemo}; window.upgradeDemo = {upgradeDemo}; " +
            $"window.upgradeCopies = {upgradeCopies}; window.campDemo = {campDemo}; window.previewMessages = [{json}, {lootMsg}]; " +
            "if (window.chrome.webview.posted) for (const m of window.previewMessages) window.chrome.webview.dispatchEvent(new MessageEvent(\"message\", { data: m }));</script></body>");
        File.WriteAllText(Path.Combine(outDir, "preview.html"), html);
        Directory.CreateDirectory(Path.Combine(outDir, "fonts"));
        foreach (var f in Directory.GetFiles(Path.Combine(web, "fonts"))) File.Copy(f, Path.Combine(outDir, "fonts", Path.GetFileName(f)), true);
        File.Copy(Path.Combine(web, "welcome.html"), Path.Combine(outDir, "welcome.html"), true);
        // The page's stylesheet and one script per page (web/js), beside preview.html as in the app.
        File.Copy(Path.Combine(web, "app.css"), Path.Combine(outDir, "app.css"), true);
        Directory.CreateDirectory(Path.Combine(outDir, "js"));
        foreach (var f in Directory.GetFiles(Path.Combine(web, "js"))) File.Copy(f, Path.Combine(outDir, "js", Path.GetFileName(f)), true);

        CaptureWeb(outDir, "main", 1274, 757, "preview.html", async (core, shot) =>
        {
            // Delivers messages through WebView2 just like MainWindow.Refresh so broken listeners fail visibly.
            core.PostWebMessageAsJson(json);
            core.PostWebMessageAsJson(lootMsg);
            // Wait for the page to have taken both, by name (the page declares them with let, so they aren't on window;
            // typeof, so a core.js that didn't load reads as not taken), five seconds at most, and go on either way: a
            // broken listener must leave shots to look at, never a run that hangs.
            const string hasData = "(typeof data !== 'undefined' && data != null)", hasLoot = "(typeof lootRows !== 'undefined' && lootRows != null)";
            bool arrived = false;
            for (int i = 0; i < 50 && !(arrived = await core.ExecuteScriptAsync($"{hasData} && {hasLoot}") == "true"); i++) await Task.Delay(100);
            if (!arrived)
            {
                // Never blank pages beside an empty main-js-errors.txt: the run says so itself. Whether WebView2 hands a
                // listener's throw to window's error event hasn't been seen, so the thrown error is in that file only
                // if it does; this line is there either way.
                var missing = await core.ExecuteScriptAsync(hasData) == "true" ? "Loot's rows never arrived" : "the page data never arrived";
                await core.ExecuteScriptAsync("(window.__errors = window.__errors || []).push(" +
                    JsonSerializer.Serialize($"--snapshot: {missing}: core.js's listener hadn't taken what was posted after 5 seconds") + ")");
            }
            foreach (var k in new[] { "level", "range", "motes", "farm50", "farm46", "diff", "dmg", "pool", "open", "dmg20", "places", "places-patch", "farm31", "farm40", "diff-mine", "diff-pool" })
            {
                await core.ExecuteScriptAsync($"show('ask'); askAnswer = window.askDemo['{k}']; renderAsk(); document.querySelector('#main').scrollTop = 0");
                await shot("ask-" + k);
            }
            // Every place at level 44 (the planes, open from 46, stay out).
            await core.ExecuteScriptAsync("askAnswer = window.askDemo['places']; placeLevel = 44; renderAsk(); document.querySelector('[data-ask-step=place]').scrollIntoView()");
            await shot("ask-places-41");
            // Help's "What's coming" from the live server, as the window would answer (a developer check: network is fine).
            string roadmap = "null";
            try
            {
                using var http = new System.Net.Http.HttpClient();
                roadmap = System.Text.Json.JsonDocument.Parse(await http.GetStringAsync(EqlPathfinder.Engine.Sharing.ShareProtocol.DefaultApi + "/v1/roadmap"))
                    .RootElement.GetProperty("items").GetRawText();
            }
            catch (Exception ex) { Console.Error.WriteLine("roadmap: " + ex.Message); }
            foreach (var p in new[] { "home", "campaign", "roster", "crawls", "loot", "relevels", "help", "settings" })
            {
                await core.ExecuteScriptAsync($"show('{p}')" + (p == "help" ? $"; roadmapItems = {roadmap}; renderRoadmap();" : ""));
                await shot(p);
            }
            // Home on a loadout the app hasn't met, and one whose classes changed (current-loadout), as the engine would send them, with the open relevels it already has.
            foreach (var (name, script) in new[]
            {
                ("home-newloadout", "m: 'newloadout', s: 'new', lv: null, race: null"),
                ("home-newloadout-ding", "m: 'newloadout', s: 'new', lv: 11, race: null"),
                ("home-classes-changed", "m: 'classes', s: 'classes', lv: 10, race: 'Barbarian'"),
            })
            {
                await core.ExecuteScriptAsync($"window.loSaved ??= JSON.stringify([data.reinc.homeNow, data.snapshot.trio, data.snapshot.race, data.snapshot.level, data.reinc.currentTrio]); {{ const o = {{ {script} }}; const [h, t, rc, l, ct] = JSON.parse(window.loSaved); data.reinc.homeNow = Object.assign(h, {{ mode: o.m, loadout: {{ slot: 6, state: o.s }}, level: o.lv }}); data.snapshot.trio = null; data.reinc.currentTrio = null; data.snapshot.race = o.race; data.snapshot.level = o.lv; show('home'); renderHome(); document.querySelector('#main').scrollTop = 0; }}");
                await shot(name);
            }
            // At the cap, farming, with the classes changed: the farming card says so under its trio line.
            await core.ExecuteScriptAsync("{ const [h, t, rc, l, ct] = JSON.parse(window.loSaved); data.reinc.homeNow = Object.assign(h, { mode: 'farming', loadout: { slot: 6, state: 'classes' }, level: 50 }); data.snapshot.trio = null; data.reinc.currentTrio = null; data.snapshot.race = 'Barbarian'; data.snapshot.level = 50; homePick = null; show('home'); renderHome(); document.querySelector('#main').scrollTop = 0; }");
            await shot("home-farming-classes-changed");
            await core.ExecuteScriptAsync("{ const [h, t, rc, l, ct] = JSON.parse(window.loSaved); data.reinc.homeNow = Object.assign(h, { mode: 'farming', loadout: { slot: 6, state: 'new' }, level: 50 }); data.snapshot.trio = null; data.reinc.currentTrio = null; data.snapshot.race = null; data.snapshot.level = 50; homePick = null; show('home'); renderHome(); document.querySelector('#main').scrollTop = 0; }");
            await shot("home-farming-new-loadout");
            await core.ExecuteScriptAsync("{ const [h, t, rc, l, ct] = JSON.parse(window.loSaved); data.reinc.homeNow = h; data.snapshot.trio = t; data.snapshot.race = rc; data.snapshot.level = l; data.reinc.currentTrio = ct; renderHome(); }");
            // Help scrolled to "Your loadouts and /who" (current-loadout): the top of Help doesn't show it.
            await core.ExecuteScriptAsync("{ show('help'); const m = document.querySelector('#main'), c = [...document.querySelectorAll('#main .card')].find(x => (x.querySelector('.h')?.textContent ?? '').includes('Your loadouts')); if (c) m.scrollTop = c.getBoundingClientRect().top - m.getBoundingClientRect().top + m.scrollTop - 20; }");
            await shot("help-loadouts");
            await core.ExecuteScriptAsync("document.querySelector('#main').scrollTop = 0; show('home');");
            // Crawls with "Hide visits too short to judge" unticked: a visit at the cap under 8 kills reads "too short" (kills-at-cap).
            await core.ExecuteScriptAsync("show('crawls'); document.querySelector('#hideShort').checked = false; renderCrawls();");
            await shot("crawls-all");
            // History at the "At 50" group: Gained and Lv/h empty on visits that start and end at the cap (xp-at-cap).
            await core.ExecuteScriptAsync("show('crawls'); document.querySelector('#hideShort').checked = false; renderCrawls(); [...document.querySelectorAll('#crawlTable tbody tr')].find(r => /At 50/.test(r.textContent))?.scrollIntoView();");
            await shot("crawls-cap");
            await core.ExecuteScriptAsync("document.querySelector('#hideShort').checked = true; renderCrawls();");
            // Home's place-by-place card: each stop judged whole (judge-by-play).
            await core.ExecuteScriptAsync("show('home'); { const m = document.querySelector('#main'), c = document.querySelector('[aria-label=\"This relevel, place by place\"]'); if (c) m.scrollTop = c.offsetTop - 20; }");
            await shot("home-places");
            await core.ExecuteScriptAsync("document.querySelector('#main').scrollTop = 0");
            // Home at the cap the other way round from what auto picked (between relevels ↔ farming), then back to auto.
            await core.ExecuteScriptAsync("{ homePick = homePick ? null : 'between'; show('home'); renderHome(); }");
            await shot("home-flipped");
            await core.ExecuteScriptAsync("homePick = null; renderHome();");
            // Home with no relevel to go by (judge-by-play): the kind is read from play, so with none the line says so. Then the kind back.
            await core.ExecuteScriptAsync("window.kindSaved = JSON.stringify([data.reinc.homeKind, data.reinc.journeys, data.reinc.journeyRaces]); { const e = { target: 1, rows: [], bestCurve: null, band: { lo: [], hi: [], med: [] }, typicalHours: null, typicalPlayedHours: null, maxHours: 1, mixedRanks: false, refigured: false, splitLevels: [], bestOfEach: [], bestOfEachHours: null, bestOfEachUnderBest: null, now: null }; data.reinc.journeyRaces = { all: e, now: null, next: { ...data.reinc.journeyRaces.next, race: e }, own: e }; } data.reinc.homeKind = null; data.reinc.journeys = []; homePick = 'between'; data.reinc.homeNow.mode = 'farming'; show('home'); renderHome(); document.querySelector('#main').scrollTop = 0");
            await shot("home-norelevel");
            await core.ExecuteScriptAsync("{ const [k, j, q] = JSON.parse(window.kindSaved); data.reinc.homeKind = k; data.reinc.journeys = j; data.reinc.journeyRaces = q; homePick = null; renderHome(); }");
            await core.ExecuteScriptAsync("{ homePick = 'between'; renderHome(); document.querySelector('#homeHero [data-info=likeforlike]')?.scrollIntoView({ block: 'center' }); }");
            await Task.Delay(400);
            await core.ExecuteScriptAsync("document.querySelector('#homeHero [data-info=likeforlike]')?.click(); document.querySelector('#homeHero .infotip')");
            await shot("home-goal-info");
            await core.ExecuteScriptAsync("document.querySelector('#infoPop')?.querySelector('button')?.click(); homePick = null; renderHome(); document.querySelector('#main').scrollTop = 0");
            // Home's one-time notice, on purpose: what an update shows a player it has something new to tell (the
            // preview's player took the tour, which told it already). Then the settings as they were.
            await core.ExecuteScriptAsync("window.noticesSaved = data.settings.noticesSeen; data.settings.noticesSeen = []; show('home'); renderHomeNotice(); document.querySelector('#main').scrollTop = 0");
            await shot("home-notice");
            await core.ExecuteScriptAsync("data.settings.noticesSeen = window.noticesSaved; renderHomeNotice();");
            // Relevels: splits, and the routes by hours.
            // Plan → Campaign: its runs further down, then a new player's start (the presets) and the goals editor.
            await core.ExecuteScriptAsync("show('campaign'); document.querySelector('#main').scrollTop = 99999;");
            await shot("campaign-runs");
            // The first two runs reincarnated: leveling (32 and 1 of the cap), then back at the cap (done).
            foreach (var (levelingNow, name) in new[] { (true, "campaign-leveling"), (false, "campaign-leveling-done") })
            {
                await core.ExecuteScriptAsync($"window.campOrig ??= data.reinc.campaign; {{ const c = structuredClone(window.campOrig); c.runs.slice(0, 2).forEach((r, i) => {{ r.state = {(levelingNow ? "'leveling'" : "'done'")}; r.level = i ? 1 : 32; r.etaHours = i ? null : 6.2; r.crawls = i ? 0 : 9; }}); c.runs[2].state = 'next'; c.done = 2; c.left = c.runs.length - 2; c.leveling = {(levelingNow ? 2 : 0)}; c.runs.slice(0, 2).forEach(r => {{ const row = c.loadouts[r.loadout]; if (row) {{ row.done++; row.left--; }} }}); {(levelingNow ? "" : "c.runs = c.runs.slice(2); c.runs.forEach(r => r.n -= 2); c.before += 2; c.done = 0; c.left = c.runs.length; c.runs[0].state = 'next';")} data.reinc.campaign = c; show('campaign'); document.querySelector('#main').scrollTop = 0; renderCampaign(); }}");
                await shot(name);
                if (levelingNow)
                {
                    await core.ExecuteScriptAsync("document.querySelector('.camp-list .rline .chip:not(.gold)')?.scrollIntoView({ block: 'start' }); document.querySelector('#main').scrollTop -= 60;");
                    await shot("campaign-leveling-rows");
                }
            }
            await core.ExecuteScriptAsync("data.reinc.campaign = window.campOrig; renderCampaign();");
            // Swap… on the first loadout with the window's answer, then the picker for its first class, then its ⋯ menu.
            await core.ExecuteScriptAsync("show('campaign'); document.querySelector('#main').scrollTop = 0; campSwap = 0; campOpts = { index: 0, plan: JSON.stringify(campPlan()), options: window.campDemo.options }; renderCampaign();");
            await shot("campaign-swap");
            await core.ExecuteScriptAsync("campOpenPop({ kind: 'pick', i: 0, slot: 0 }, document.querySelector('#campBody [data-cl=\"0\"][data-pick=\"0\"]'));");
            await shot("campaign-picker");
            await core.ExecuteScriptAsync("campClosePop(); campOpenPop({ kind: 'menu', i: 0 }, document.querySelector('#campBody [data-cl=\"0\"][data-act=\"menu\"]'));");
            await shot("campaign-menu");
            await core.ExecuteScriptAsync("campClosePop(); campSwap = null; campOpts = null;");
            // Your runs as a table by round (campaign-run-list): full width with played folded, Swap… and Played open, a folded round, narrow, and waiting.
            var widthBefore = Window!.Width;
            Window!.Width = 1625;
            await core.ExecuteScriptAsync("window.campOrig ??= data.reinc.campaign; { const c = structuredClone(window.campOrig); c.before = 3; c.unseen = 1; const g = (k, r) => ({ kind: 'class', key: k, rank: r }); c.played = [{ n: 2, at: '2026-11-08T12:00:00', race: 'Human', classes: ['PAL', 'MNK', 'SHM'], gains: [g('PAL', 3), g('MNK', 3), g('SHM', 2), { kind: 'race', key: 'Human', rank: 3 }], offPlan: false }, { n: 3, at: '2026-11-09T12:00:00', race: 'Troll', classes: ['SHD', 'ROG', 'BST'], gains: [g('SHD', 2), g('ROG', 1), g('BST', 2)], offPlan: false }]; c.runs.slice(0, 2).forEach((r, i) => { if (i) { r.state = 'leveling'; r.level = 32; r.etaHours = 6.2; r.crawls = 9; } else { r.state = 'done'; r.at = '2026-11-10T12:00:00'; } }); c.runs[2].state = 'next'; c.done = 2; c.left = c.runs.length - 2; c.leveling = 1; c.runs.slice(0, 2).forEach(r => { const row = c.loadouts[r.loadout]; if (row) { row.done++; row.left--; } }); data.reinc.campaign = c; campShowPlayed = false; campFolded.clear(); campSwap = null; campOpts = null; campLocal = null; show('campaign'); renderCampaign(); document.querySelector('.camp-list').scrollIntoView({ block: 'start' }); }");
            await Task.Delay(300);
            await shot("campaign-runs-full");
            await core.ExecuteScriptAsync("campShowPlayed = true; { const i = campView().runs.find(r => r.state === 'next').loadout; campSwap = i; campOpts = { index: i, plan: JSON.stringify(campPlan()), options: window.campDemo.options }; } renderCampaign(); document.querySelector('.camp-list').scrollIntoView({ block: 'start' });");
            await Task.Delay(300);
            await shot("campaign-runs-opened");
            await core.ExecuteScriptAsync("campShowPlayed = false; campSwap = null; campOpts = null; campFolded.add('r2'); renderCampaign(); document.querySelector('.camp-list').scrollIntoView({ block: 'start' });");
            await Task.Delay(300);
            await shot("campaign-runs-folded");
            await core.ExecuteScriptAsync("campFolded.clear(); campLocal = campCopy(campPlan()); renderCampaign(); document.querySelector('.camp-list').scrollIntoView({ block: 'start' });");
            await Task.Delay(300);
            await shot("campaign-runs-busy");
            Window!.Width = 906;
            await core.ExecuteScriptAsync("campLocal = null; renderCampaign(); document.querySelector('.camp-list').scrollIntoView({ block: 'start' });");
            await Task.Delay(300);
            await shot("campaign-runs-narrow");
            await core.ExecuteScriptAsync("data.reinc.campaign = window.campOrig; campShowPlayed = false; campFolded.clear(); renderCampaign(); document.querySelector('#main').scrollTop = 0;");
            Window!.Width = widthBefore;
            // The same plan without its last loadout, so a race falls short: the cheapest fixes.
            await core.ExecuteScriptAsync("window.campSaved = data.reinc.campaign; data.reinc.campaign = window.campDemo.shortView; renderCampaign(); document.querySelector('#campBody .fix')?.scrollIntoView({ block: 'center' });");
            await shot("campaign-fixes");
            await core.ExecuteScriptAsync("data.reinc.campaign = window.campSaved; renderCampaign();");
            await core.ExecuteScriptAsync("campView().plan = { ...campCopy(EMPTY_CAMP), combos: [{ classes: ['SHD', 'ROG', 'SHM'], times: 3, must: true }] }; campView().comboRows = []; show('campaign'); renderCampaign();");
            await shot("campaign-start");
            await core.ExecuteScriptAsync("campLocal = campCopy(EMPTY_CAMP); campLocal.classes = { MAG: 3, NEC: 2 }; campLocal.races = { Iksar: 3 }; campLocal.first = ['class:MAG']; campLocal.locked = ['Kerran']; campMore = true; renderCampaign(); document.querySelector('#main').scrollTop = 99999;");
            await shot("campaign-goals");
            await core.ExecuteScriptAsync("campLocal = null; campMore = false; renderCampaign();");
            // The planner out of beta: the card "Campaign has a new planner" (over the start page, over a plan, narrow), and More options.
            await core.ExecuteScriptAsync("window.addPre = () => { campView().presets ??= [{ key: 'all', name: 'Everything 3/3', runs: 39, totalXp: 62, lastXp: 2 }, { key: 'classes', name: 'Every class 3/3', runs: 7, totalXp: 9, lastXp: 1.4 }]; };");
            async Task Plan(string view, string more = "true") =>
                await core.ExecuteScriptAsync($"data.reinc.campaign = window.campDemo.{view}; campMore = {more}; rolesLocal = null; window.addPre(); show('campaign'); renderCampaign(); document.querySelector('#main').scrollTop = 0;");
            await Plan("newEmpty", "false");
            await shot("campaign-planner-new-empty");
            await Plan("newPlan", "false");
            await shot("campaign-planner-new");
            var wasWide = Window!.Width;
            Window!.Width = 906;
            await Plan("newPlan", "false");
            await shot("campaign-planner-new-narrow");
            Window!.Width = wasWide;
            await Plan("plan");
            await Task.Delay(400);
            await shot("campaign-more");
            // Your combos: the card with its rows, none yet, on the start page, the form, the clash, a price, a break, the menu.
            async Task Combos(string name, string view, string script, string scroll = "document.querySelector('#campBody .combos').scrollIntoView({ block: 'start' });")
            {
                await core.ExecuteScriptAsync($"data.reinc.campaign = structuredClone(window.campDemo.{view}); campMore = false; campLocal = null; rolesLocal = null; comboForm = null; campClosePop(); show('campaign'); {script} renderCampaign(); setTimeout(() => {{ {scroll} }}, 0);");
                await Task.Delay(400);
                await shot(name);
            }
            // The page layout (campaign-page-layout): wide with the side column, a hand-set mix stacked, and the empty plan at both widths.
            Window!.Width = 1800;
            await Plan("plan", "false");
            await shot("campaign-layout-full");
            await Plan("combosStart", "false");
            await shot("campaign-layout-empty");
            Window!.Width = 1000;
            await Plan("shortView", "false");
            await shot("campaign-layout-mix");
            await Plan("combosStart", "false");
            await shot("campaign-layout-empty-narrow");
            // Review fixes: the band at 1,274 px with More options open, the legend, a dearer plan, a real mix, the empty plan's big number narrow.
            Window!.Width = 1274;
            await Plan("combosCost", "true");
            await shot("campaign-layout-1274");
            await core.ExecuteScriptAsync("document.querySelector('.glegend').scrollIntoView({ block: 'end' });");
            await Task.Delay(300);
            await shot("campaign-layout-1274-legend");
            Window!.Width = 1000;
            await Plan("firstLocked", "false");
            await shot("campaign-layout-dearer");
            await core.ExecuteScriptAsync("document.querySelector('.glegend').scrollIntoView({ block: 'end' });");
            await Task.Delay(300);
            await shot("campaign-layout-dearer-legend");
            await Plan("combosStart", "false");
            await core.ExecuteScriptAsync("document.querySelector('.presets').scrollIntoView({ block: 'start' });");
            await Task.Delay(300);
            await shot("campaign-layout-empty-narrow-big");
            // A real mix that lights no ready-made goal (Human is not a goal), scrolled to the tiles' counts and the legend.
            await Plan("shortView", "false");
            await core.ExecuteScriptAsync("delete campView().plan.races['Human']; renderCampaign(); document.querySelector('.glegend').scrollIntoView({ block: 'end' });");
            await Task.Delay(300);
            await shot("campaign-layout-mix-legend");
            Window!.Width = 1274;
            await Combos("campaign-combos", "combos", "", "document.querySelector('#campSide').scrollIntoView({ block: 'start' });");
            await Combos("campaign-combos-empty", "plan", "");
            await Combos("campaign-start-combos", "combosStart", "");
            await Combos("campaign-combo-add", "plan", "comboForm = { index: null, picks: ['PAL', 'BER'], together: true, every: true, times: 3, unlock: false, must: false, price: window.campDemo.priceAdd, last: null, fromPin: null };");
            await Combos("campaign-combo-clash", "combos", "comboForm = { index: null, picks: window.campDemo.clashPicks, together: true, every: true, times: 3, unlock: false, must: true, price: window.campDemo.priceClash, last: null, fromPin: null };");
            await Combos("campaign-combos-cost", "combosCost", "", "document.querySelector('#campBand').scrollIntoView({ block: 'start' });");
            await Combos("campaign-combo-broken", "combosBroken", "");
            await Combos("campaign-combo-menu", "combos", "", "document.querySelector('#campBody .camp-list').scrollIntoView({ block: 'start' }); setTimeout(() => campOpenPop({ kind: 'menu', i: 5 }, document.querySelector('#campBody [data-cl=\"5\"][data-act=\"menu\"]')), 150);");
            // An empty card's example (campaign-combos-example): a trio played twice, a pair from the first run to come, neither, and Add this opened.
            const string playedTwice = "campView().played = [['SHD', 'SHM', 'ROG'], ['ROG', 'SHD', 'SHM']].map((k, i) => ({ n: i + 1, at: '2026-11-0' + (i + 1) + 'T20:00:00', race: 'Iksar', classes: k, gains: [], offPlan: false }));";
            const string noTrio = "campView().played = []; campView().runs = campView().runs.filter(r => r.state !== 'done');";
            await Combos("campaign-combos-try-trio", "plan", playedTwice);
            await Combos("campaign-combos-try-pair", "plan", noTrio);
            await Combos("campaign-combos-try-none", "combosStart", "campView().plan.combos = null; " + noTrio);   // a relevel on now stays: the no-plan page offers no pair from it
            await Combos("campaign-combos-try-open", "plan", playedTwice + " comboForm = { index: null, picks: ['SHD', 'ROG', 'SHM'], together: true, every: false, times: 3, unlock: false, must: false, price: window.campDemo.priceTry, last: null, fromPin: null, fromTry: true };");
            Window!.Width = 1000;
            await Combos("campaign-combos-try-narrow", "plan", playedTwice);
            Window!.Width = 1274;
            // What you want first: the card with its lists, with nothing ranked, a row's menu, an Unlock now with a clash, and narrow.
            async Task FirstShot(string name, string view, string script = "", string scroll = "")
            {
                await core.ExecuteScriptAsync($"data.reinc.campaign = structuredClone(window.campDemo.{view}); campMore = false; campLocal = null; rolesLocal = null; comboForm = null; campFirstClash = null; campFirstFlash = \"\"; campClosePop(); show('campaign'); {script} renderCampaign(); setTimeout(() => {{ document.querySelector('#campFirst').scrollIntoView({{ block: 'start' }}); {scroll} }}, 0);");
                await Task.Delay(500);
                await shot(name);
            }
            await FirstShot("campaign-first", "first");
            await FirstShot("campaign-first-tile", "first", "", "document.querySelector('#campSide').scrollIntoView({ block: 'start' });");
            await FirstShot("campaign-first-empty", "firstEmpty", "", "document.querySelector('#campSide')?.scrollIntoView({ block: 'start' });");
            await FirstShot("campaign-first-all", "firstAll");
            await FirstShot("campaign-first-once", "firstOnce", "", "document.querySelector('#campFirstRaces').scrollIntoView({ block: 'start' });");
            await FirstShot("campaign-start-first", "startFirst", "", "document.querySelector('#campSide').scrollIntoView({ block: 'start' });");
            await FirstShot("campaign-first-menu", "first", "", "setTimeout(() => campOpenPop({ kind: 'first', i: 'race:Iksar' }, document.querySelector('[data-first=\"menu\"][data-fk=\"race:Iksar\"]')), 150);");
            await FirstShot("campaign-first-clash", "firstClash", "campFirstClash = 'class:ROG';");
            await FirstShot("campaign-first-locked", "firstLocked", "", "document.querySelector('#campFirstRaces').scrollIntoView({ block: 'start' });");
            await FirstShot("campaign-first-done", "firstLocked", "{ const x = data.reinc.races.find(r => r.key === 'Troll'); x.rank = 3; campView().firstLane.races.find(r => r.key === 'Troll').line = '3/3: done at run 4.'; }", "document.querySelector('#campFirstRaces').scrollIntoView({ block: 'start' });");
            Window!.Width = 1000;
            await FirstShot("campaign-first-narrow", "first");
            Window!.Width = 1274;
            Window!.Width = 1000;
            await Combos("campaign-combos-narrow", "combos", "comboForm = { index: null, picks: ['PAL', 'BER'], together: true, every: true, times: 3, unlock: false, must: false, price: window.campDemo.priceAdd, last: null, fromPin: null };");
            Window!.Width = 1274;
            await core.ExecuteScriptAsync("campClosePop(); comboForm = null;");
            async Task Roles(string name, string script = "")
            {
                await core.ExecuteScriptAsync("data.reinc.campaign = structuredClone(window.campDemo.plan); rolesLocal = null; " + script + " show('classroles'); renderClassRoles(); document.querySelector('#main').scrollTop = 0;");
                await shot(name);
            }
            await Roles("classroles", "campView().newPlanner.check = { state: 'checked', difficulty: 4, relevels: 1, checks: true };");
            await Roles("classroles-table", "setTimeout(() => document.querySelector('.roles-table').scrollIntoView(), 0);");
            await Roles("classroles-foot", "setTimeout(() => document.querySelector('#main').scrollTop = 99999, 0);");
            await Roles("classroles-all", "document.body.style.zoom = 0.55; setTimeout(() => document.querySelector('.roles-table').scrollIntoView(), 0);");
            await core.ExecuteScriptAsync("document.body.style.zoom = '';");
            // A 1000 px wide window: the media queries see the real width.
            Window!.Width = 1000;
            await Roles("classroles-1000");
            Window!.Width = 1274;
            await Roles("classroles-edited", "rolesLocal = { carries: ['NEC', 'MAG', 'BST'], oneStrongSlow: true }; campView().newPlanner.carriesAreDefault = false;");
            await Roles("classroles-empty", "rolesLocal = { carries: [], oneStrongSlow: true };");
            await Roles("classroles-quiet", "campView().newPlanner.check = { state: 'quiet', difficulty: 0, relevels: 1, checks: false };");
            await Roles("classroles-spread", "campView().newPlanner.check = { state: 'spread', difficulty: null, relevels: 1, checks: true };");
            await Roles("classroles-norelevel", "campView().newPlanner.check = { state: 'norelevel', difficulty: null, relevels: 0, checks: true };");
            await Roles("classroles-nofarm", "campView().newPlanner.check = { state: 'off', difficulty: null, relevels: 1, checks: false }; campView().newPlanner.farmed = [];");
            await Roles("classroles-narrow", "const st = document.createElement('style'); st.textContent = '#page-classroles { max-width: 870px } .roles-grid { grid-template-columns: minmax(0, 1fr) !important }'; document.head.appendChild(st);");
            await core.ExecuteScriptAsync("data.reinc.campaign = window.campOrig ?? window.campSaved; campMore = false; show('campaign'); renderCampaign();");

            await core.ExecuteScriptAsync("relView = 'splits'; show('relevels'); renderRelevels();");
            await shot("relevels-splits");
            await core.ExecuteScriptAsync("relView = 'routes'; relAxis = 'time'; renderRelevels(); document.querySelector('#main').scrollTop = 99999;");
            await shot("relevels-hours");
            await core.ExecuteScriptAsync("relAxis = 'level'; store.set('relView', 'routes'); store.set('relAxis', 'level'); renderRelevels();");
            // Home while sharing is invite-only, after a code that didn't work.
            await core.ExecuteScriptAsync("data.settings.shareIntroSeen = false; data.settings.shareEnabled = false; show('home'); " +
                "shareStatus = { requireEnrollment: true, enabled: false, error: 'That code has expired, or it was already used.' }; renderHomeShare(); document.querySelector('#main').scrollTop = 0");
            await shot("home-share-code");
            await core.ExecuteScriptAsync("shareStatus = null; data.settings.shareIntroSeen = true; renderHomeShare();");
            // Help's "How Pathfinder counts", a screen at a time from its heading down.
            await core.ExecuteScriptAsync("show('help'); const m = document.querySelector('#main'); " +
                "m.scrollTop = document.querySelector('.help.counts').offsetTop - 60;");
            for (int i = 1; i <= 4; i++)
            {
                await shot("help-counts-" + i);
                await core.ExecuteScriptAsync("document.querySelector('#main').scrollTop += document.querySelector('#main').clientHeight - 60;");
            }
            // Upgrades: an item from the export, +5 to +10, with the demo bank.
            await core.ExecuteScriptAsync("upReq = Object.assign({}, UP_DEFAULT, { item: 'Cudgel of the Fool', tier: 5, goal: 10, gear: [{ tier: 5, progress: 0, place: '', on: true }] }); " +
                "show('upgrade'); upg = window.upgradeDemo; upItemsKey = upg.inventory.time + '|' + upItemList().size; upSent = []; renderUpgrade();");
            await shot("upgrade");
            await core.ExecuteScriptAsync("document.querySelector('#main').scrollTop = 99999");
            await shot("upgrade-bottom");
            // Three copies of it, merged: the copies list, the path chart and the steps (upSent: the plan's copies 1 and 2 are rows 1 and 2).
            await core.ExecuteScriptAsync("upReq = Object.assign({}, UP_DEFAULT, { item: 'Cudgel of the Fool', tier: 5, progress: 12, goal: 9, vtps: 1, gear: [" +
                "{ tier: 5, progress: 12, place: 'Worn', on: true }, { tier: 5, progress: 0, place: 'Bags', on: true }, { tier: 3, progress: 0, place: 'Bank', on: true }] }); " +
                "document.querySelector('#main').scrollTop = 0; upg = window.upgradeCopies; upItemsKey = upg.inventory.time + '|' + upItemList().size; upSent = [1, 2]; renderUpgrade();");
            await shot("upgrade-copies");
            await core.ExecuteScriptAsync("document.querySelector('#main').scrollTop = document.querySelector('#upPlan').offsetTop - 20");
            await shot("upgrade-copies-plan");
            await core.ExecuteScriptAsync("document.querySelector('#main').scrollTop = 99999");
            await shot("upgrade-copies-bottom");
            // Settings' usage switch with its text (everything the summary holds).
            await core.ExecuteScriptAsync("show('settings'); renderSettings(); [...document.querySelectorAll('#main *')].find(e => e.children.length == 0 && e.textContent.trim() == 'Send anonymous usage counts').scrollIntoView({ block: 'center' })");
            await shot("settings-usage");
            // The Sharing preview, built.
            await core.ExecuteScriptAsync("data.settings.beta = true; data.settings.onBetaBuild = true; renderSettings(); show('settings'); sharePreview = window.shareDemo; renderShare(); document.querySelector('#main').scrollTop = 99999");
            await shot("settings-share");
            // Sharing switched on, mid-upload, with a link code showing; then off with the server asking for enrollment.
            // The setting goes on with the status, as in the app, so "Your data stays on this PC" above the card agrees
            // with it; and off again with it, for this shot and the ones after (Home's sharing card reads it too).
            await core.ExecuteScriptAsync("data.settings.shareEnabled = true; renderSettings(); show('settings'); sharePreview = null; shareStatus = { enabled: true, paused: false, from: null, busy: true, pending: 340, " +
                "counts: { crawl: 712, instance: 96, stretch: 141, reincarnation: 20, relevel: 5 }, pcs: 2, lastUpload: new Date(Date.now() - 180000).toISOString(), " +
                "requireEnrollment: false, link: { code: 'K7QM-3XPD', expires: new Date(Date.now() + 540000).toISOString() } }; renderShare(); document.querySelector('#main').scrollTop = 99999");
            await shot("settings-share-on");
            await core.ExecuteScriptAsync("data.settings.shareEnabled = false; renderSettings(); show('settings'); shareStatus = { enabled: false, requireEnrollment: true, error: 'Sharing needs an enrollment code from someone who already shares, or a link code from your other PC.' }; " +
                "shareLinkOpen = true; renderShare(); document.querySelector('#main').scrollTop = 99999");
            await shot("settings-share-off");
            await core.ExecuteScriptAsync("shareLinkOpen = false; shareStatus = null;");
            // The website's Home (site/img): as a player sees it once the one-time sharing card is answered.
            await core.ExecuteScriptAsync("data.settings.shareIntroSeen = true; show('home'); renderHome(); renderHomeShare(); document.querySelector('#main').scrollTop = 0");
            await shot("site-home");
            // History → Relevels' lower half: your relevels like-for-like, and where the hours went.
            await core.ExecuteScriptAsync("show('relevels'); document.querySelector('#main').scrollTop = 99999");
            await shot("relevels-bottom");
            // The roster in "set counts" mode.
            await core.ExecuteScriptAsync("show('roster'); document.querySelector('#editCounts').click()");
            await shot("roster-counts");
            // A log that has seen no reincarnation (and no counts set): Home's "Set counts" opens Roster's counts.
            if (await core.ExecuteScriptAsync("editingCounts = false; show('home'); renderHome(); !!document.querySelector('#page-home [data-set-counts]')") == "true")
            {
                await core.ExecuteScriptAsync("document.querySelector('#page-home [data-set-counts]').click()");
                await shot("home-set-counts");
            }
        });

        // The welcome tour, fed what the window would send: this PC's character, live, then its history read.
        var age = File.Exists(logs.LivePath) ? (DateTime.Now - File.GetLastWriteTime(logs.LivePath)).TotalSeconds : (double?)null;
        string Msg(object o) => JsonSerializer.Serialize(o, PageJson.Options);
        var init = Msg(new { kind = "init", mode = "firstrun", settings = new { overlaySize = "Standard" } });
        var chars = Msg(new
        {
            kind = "chars",
            logsDir = LogsDir,
            dirOk = true,
            gameRunning = false,
            selected = logs.Key,
            locked = false,
            characters = new[]
            {
                new { key = logs.Key, name = logs.Name, server = "Oggok", ageSeconds = (double?)Math.Min(age ?? 30, 30), mb = 1350.0, files = 3 },
                new { key = "Alt_oggok", name = "Grimsby", server = "Oggok", ageSeconds = (double?)(86400 * 4), mb = 12.0, files = 1 },
            },
        });
        var imported = Msg(new { kind = "imported", ok = true, character = logs.Name, roster = svc.RosterCounts(), visits = svc.History(100_000).Count });
        // The Usage step's preview is the real recorder's own text: a first run's, held, with the steps reached so far.
        string previewText;
        using (var meter = new System.Diagnostics.Metrics.Meter(EqlPathfinder.Engine.Usage.UsageMetrics.MeterName, "1"))
        {
            var metrics = new EqlPathfinder.Engine.Usage.UsageMetrics(meter);
            using var held = new EqlPathfinder.Engine.Usage.UsageRecorder(null, metrics, held: true);
            metrics.Feature("setup-welcome");
            metrics.Feature("setup-usage");
            previewText = EqlPathfinder.Engine.Usage.UsageRecorder.ToJson(
                held.Preview(new EqlPathfinder.Engine.Usage.UsageContext("", "stable", Environment.OSVersion.Version.ToString(), null)), indented: true);
        }
        var previewMsg = Msg(new { kind = "usagePreview", text = previewText });
        string UsageMsg(bool? selected, bool noDeletes) => Msg(new { kind = "usage", usage = new { selected, noDeletes } });
        CaptureWeb(outDir, "welcome", 1000, 640, "welcome.html", async (core, shot) =>
        {
            await core.ExecuteScriptAsync($"feed({init}); feed({chars});");
            await shot("1-welcome");
            // Usage counts, first run: nothing chosen, then each answer, then what's sent.
            await core.ExecuteScriptAsync("show('usage')");
            await shot("usage");
            await core.ExecuteScriptAsync($"feed({UsageMsg(true, false)})");
            await shot("usage-yes");
            await core.ExecuteScriptAsync($"feed({UsageMsg(false, false)})");
            await shot("usage-no");
            await core.ExecuteScriptAsync($"feed({UsageMsg(true, false)}); feed({previewMsg}); document.querySelector('#seeSent').click(); feed({previewMsg});");
            await Task.Delay(150);
            await shot("usage-sent");
            await core.ExecuteScriptAsync("usageOpen = false; renderUsage();");
            await core.ExecuteScriptAsync("show('log')");
            await shot("2-log");
            await core.ExecuteScriptAsync("feed({kind:'progress', percent: 41, character: 'Kestrel'}); show('reinc')");
            await shot("3-reading");
            await core.ExecuteScriptAsync($"feed({imported}); document.querySelector('[data-answer=yes]').click()");
            await shot("3-reinc");
            await core.ExecuteScriptAsync("show('overlay')");
            await shot("4-overlay");
            await core.ExecuteScriptAsync("show('share')");
            await shot("5b-share");
            // Invite-only sharing: the code box under the choice.
            await core.ExecuteScriptAsync("feed({ kind: 'shareConfig', requireCode: true }); choices.share = true; renderChoices();");
            await shot("5c-share-code");
            await core.ExecuteScriptAsync("shareNeedsCode = false; choices.share = false; renderChoices();");
            await core.ExecuteScriptAsync("show('ready')");
            await shot("6-ready");
            // A friend whose log never saw a reincarnation, after tapping PAL, MNK, ENC and Froglok once.
            await core.ExecuteScriptAsync("""
                [...read.roster.classes, ...read.roster.races].forEach(x => { x.logged = 0; x.rank = 0; x.entered = null; });
                ['PAL', 'MNK', 'ENC'].forEach(k => { const c = read.roster.classes.find(x => x.key === k); c.rank = 1; c.entered = 1; });
                const f = read.roster.races.find(x => x.key === 'Froglok'); f.rank = 1; f.entered = 1;
                reincAnswer = 'yes'; show('reinc');
                """);
            await shot("3-reinc-friend");
            // Nobody's log yet, with the game running: what a friend who never logged sees.
            await core.ExecuteScriptAsync("feed({kind:'chars', logsDir: 'C:\\\\Users\\\\Public\\\\Daybreak Game Company\\\\Installed Games\\\\EverQuest Legends\\\\Logs', dirOk: true, gameRunning: true, selected: null, characters: []}); show('log')");
            await shot("2-log-empty");
            // The tour from Help, usage counts on: Yes chosen, and No deletes.
            await core.ExecuteScriptAsync($"feed({Msg(new { kind = "init", mode = "tour", settings = new { overlaySize = "Standard" }, usage = new { selected = (bool?)true, noDeletes = true } })}); show('usage')");
            await shot("usage-tour");
        });
    }

    /// <summary>A page as WebView2 draws it, from a window parked off-screen; <c>shots</c> drives it and saves PNGs.</summary>
    /// <summary>The window being captured, so a shot can change its width.</summary>
    private static Window? Window;

    private static void CaptureWeb(string outDir, string prefix, int width, int height, string page,
        Func<CoreWebView2, Func<string, Task>, Task> shots)
    {
        var frame = new DispatcherFrame();
        var web = new Microsoft.Web.WebView2.Wpf.WebView2();
        var w = new Window
        {
            Width = width,
            Height = height,
            Left = -20000,
            Top = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Content = web,
        };
        w.Show();
        Window = w;
        async Task Go()
        {
            try
            {
                // Off-screen windows count as hidden to Chromium; keep it painting.
                var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(outDir, "wv2"),
                    new CoreWebView2EnvironmentOptions("--disable-features=CalculateNativeWinOcclusion"));
                await web.EnsureCoreWebView2Async(env);
                var core = web.CoreWebView2;
                core.SetVirtualHostNameToFolderMapping("preview.local", outDir, CoreWebView2HostResourceAccessKind.Allow);
                var loaded = new TaskCompletionSource();
                core.NavigationCompleted += (_, _) => loaded.TrySetResult();
                // Script errors would only show as a blank or half-drawn page: collect them and write them out.
                await core.AddScriptToExecuteOnDocumentCreatedAsync("window.__errors = []; addEventListener('error', e => " +
                    "__errors.push((e.message || 'error') + ' @ ' + String(e.filename || '').split('/').pop() + ':' + e.lineno));");
                core.Navigate("https://preview.local/" + page);
                await loaded.Task;
                await shots(core, async name =>
                {
                    await Task.Delay(700);
                    using var fs = File.Create(Path.Combine(outDir, $"{prefix}-{name}.png"));
                    await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
                });
                var errors = await core.ExecuteScriptAsync("JSON.stringify(window.__errors || [])");
                File.WriteAllText(Path.Combine(outDir, $"{prefix}-js-errors.txt"), errors);
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(outDir, $"{prefix}-error.txt"), ex.ToString()); }
            finally { frame.Continue = false; }
        }
        // WebView2 starts only inside a running message loop.
        w.Dispatcher.BeginInvoke(() => _ = Go());
        Dispatcher.PushFrame(frame);
        w.Close();
    }

    /// <summary>The installer's splash (vpk pack --splashImage, saved as Assets/splash.png): compass, wordmark, what it does.</summary>
    private static void RenderSplash(string path)
    {
        Brush Hex(string h) => (Brush)new BrushConverter().ConvertFromString(h)!;
        var compass = new Viewbox
        {
            Width = 84,
            Height = 84,
            Child = new Canvas
            {
                Width = 24,
                Height = 24,
                Children =
                {
                    Place(new System.Windows.Shapes.Ellipse { Width = 19, Height = 19, Stroke = Hex("#F0C860"), StrokeThickness = 1.4 }, 2.5, 2.5),
                    new System.Windows.Shapes.Path { Data = Geometry.Parse("M12,4.5 L14.3,12 12,19.5 9.7,12 Z"), Fill = Hex("#F0C860") },
                    Place(new System.Windows.Shapes.Ellipse { Width = 2.6, Height = 2.6, Fill = Hex("#141A22") }, 10.7, 10.7),
                },
            },
        };
        var word = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 18, 0, 0) };
        Chrome.FillWordmark(word, 24);
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(compass);
        stack.Children.Add(word);
        stack.Children.Add(new TextBlock
        {
            Text = "Your crawls, measured. Your next move, suggested.",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            Foreground = Hex("#A9B1BA"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Installing…",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            Foreground = Hex("#8D959E"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 22, 0, 0),
        });
        var card = new Border
        {
            Width = 480,
            Height = 280,
            BorderBrush = Hex("#2A333E"),
            BorderThickness = new Thickness(1),
            Child = stack,
            Background = new RadialGradientBrush(Color.FromRgb(0x25, 0x2D, 0x39), Color.FromRgb(0x0D, 0x11, 0x16))
            {
                Center = new Point(0.5, 0.36),
                GradientOrigin = new Point(0.5, 0.36),
                RadiusX = 0.75,
                RadiusY = 0.95,
            },
        };
        card.Measure(new Size(480, 280));
        card.Arrange(new Rect(0, 0, 480, 280));
        card.UpdateLayout();
        // 1.5× so it stays sharp on the usual 125–150% displays.
        var rtb = new RenderTargetBitmap(720, 420, 144, 144, PixelFormats.Pbgra32);
        rtb.Render(card);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        enc.Save(fs);
    }

    private static UIElement Place(UIElement e, double left, double top)
    {
        Canvas.SetLeft(e, left);
        Canvas.SetTop(e, top);
        return e;
    }

    private static void RenderElement(FrameworkElement child, double width, string path)
    {
        var e = new Border { Width = width, Child = child };
        e.Measure(new Size(width, double.PositiveInfinity));
        e.Arrange(new Rect(e.DesiredSize));
        e.UpdateLayout();
        var rtb = new RenderTargetBitmap((int)(e.ActualWidth * 2), (int)(e.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
        rtb.Render(e);
        e.Child = null;   // free to be drawn again
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        enc.Save(fs);
    }

    /// <summary>
    /// Developer check: EQLPathfinder.exe --meter-snapshot &lt;outDir&gt; &lt;fights.db&gt; "&lt;date time&gt;" draws the fight meter's
    /// views for the session that was running then, from a fight database the CLI made (pathfinder fights --db).
    /// </summary>
    public static void RunMeter(string[] args)
    {
        var outDir = args[1];
        Directory.CreateDirectory(outDir);
        var app = new RenderOnlyApp();
        app.InitializeComponent();
        var logs = LogDiscovery.FindCharacters(LogsDir)[0];
        // A store not built yet (demo logs) is read from the logs first, as the app does on its first run.
        using (var meter = new EqlPathfinder.Engine.Combat.FightMeterService(logs, args[2])) if (meter.NeedsImport) meter.Import();
        using var store = new EqlPathfinder.Engine.Combat.FightStore(args[2]);
        var frame = EqlPathfinder.Engine.Combat.MeterViews.LookBack(store, logs.Key, logs.Name, DateTime.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture))
            ?? throw new InvalidOperationException("no fights before that time");
        var biggest = frame.Fights.Where(f => f.Stint == frame.Stint).MaxBy(f => f.Fighters.Count * 1_000_000L + f.Damage)!;
        foreach (var (name, q) in new (string, EqlPathfinder.Engine.Combat.MeterQuery)[]
                 {
                     ("fight-fighter", new("fight", "fighter")),
                     ("fight-opened", new("place", "fight", FightStart: biggest.Start, FightMob: biggest.Mob)),
                     ("place-fighter", new("place", "fighter")),
                     ("place-spell", new("place", "spell")),
                     ("place-mob", new("place", "mob")),
                     ("session-fight", new("session", "fight")),
                     ("place-me-spells", new("place", "fighter", Fighter: frame.Me)),
                     ("place-healing", new("place", "fighter", "healing")),
                     ("place-taken", new("place", "fighter", "taken")),
                 })
        {
            var w = new MeterWindow(new AppSettings());
            w.Show(frame, q);
            Render(w, Path.Combine(outDir, $"meter-{name}.png"), "#3A4048");
        }
        // A loadout the app hasn't met: no usual tick or percent, the footer says why (current-loadout).
        var notKnown = new MeterWindow(new AppSettings());
        notKnown.Show(frame with { NotKnown = true, Usual = null, Trio = null }, new("place", "fighter"));
        Render(notKnown, Path.Combine(outDir, "meter-not-known.png"), "#3A4048");
    }

    private static void Render(Window w, string path, string? backdrop)
    {
        var content = (FrameworkElement)w.Content;
        w.Content = null;
        var wrap = new Border
        {
            Background = (Brush)new BrushConverter().ConvertFromString(backdrop ?? "#141920")!,
            Padding = new Thickness(backdrop != null ? 16 : 0),
            Width = double.IsNaN(w.Width) ? double.NaN : w.Width,
            Child = content,
        };
        wrap.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        wrap.Arrange(new Rect(wrap.DesiredSize));
        wrap.UpdateLayout();
        var rtb = new RenderTargetBitmap((int)(wrap.ActualWidth * 2), (int)(wrap.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
        rtb.Render(wrap);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        enc.Save(fs);
    }
}
