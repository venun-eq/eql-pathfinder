using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Pages;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>One "usual" for each kind of play, on every screen (docs/specs/one-usual.md).</summary>
public class OneUsualTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 18, 0, 0);
    private static readonly int[] Default = [3, 4];
    private static int _n;

    private static LevelingSegment Seg(string place = "Kedge Keep D4", int level = 40, double minutes = 15, double t5 = 10, bool grouped = false,
        bool completed = true, int kills = 60, double levels = 0.2, string? trio = null, bool earlier = true) =>
        new(place, "Kedge Keep", int.Parse(place[^1..]), earlier ? T0.AddHours(-1000 + ++_n) : T0.AddHours(++_n), level, minutes, levels, t5, 0, 0, kills, 0, 0, 2, "crawl", grouped ? 1 : 0, trio)
        { VisitId = $"v{_n}", KillLevels = levels, Completed = completed, Refunded = completed, Ended = true };

    private static List<LevelingSegment> Many(int n, Func<LevelingSegment> make) => Enumerable.Range(0, n).Select(_ => make()).ToList();

    [Fact]
    public void AnEarlierVisitCountsTowardALevelingUsualOnlyWithARate()
    {
        // 4 minutes and 20 kills, not finished: a rate under the one rule, though under the old 6-minute floor.
        var short20 = Many(3, () => Seg("Kedge Keep D1", minutes: 4, kills: 20, completed: false));
        Assert.Equal(3, RunPace.For(Seg("Kedge Keep D1", earlier: false), short20).Runs);
        // 20 minutes and 7 kills, not finished: no rate, however long.
        var sevenKills = Many(3, () => Seg("Kedge Keep D1", minutes: 20, kills: 7, completed: false));
        Assert.Equal("none", RunPace.For(Seg("Kedge Keep D1", earlier: false), sevenKills).Verdict);
        // A finished crawl has one at any kills.
        var finished = Many(3, () => Seg("Kedge Keep D1", minutes: 5, kills: 3, completed: true));
        Assert.Equal(3, RunPace.For(Seg("Kedge Keep D1", earlier: false), finished).Runs);
    }

    [Fact]
    public void AnEarlierVisitCountsTowardAFarmingUsualOnlyWithARate()
    {
        Assert.Equal(3, FarmPace.For(Seg(earlier: false), Many(3, () => Seg(minutes: 4)), Default, 50).Runs);   // under the old 6 minutes
        Assert.Equal("none", FarmPace.For(Seg(completed: false, earlier: false), Many(3, () => Seg(completed: false, kills: 7, minutes: 30)), Default, 50).Verdict);
    }

    [Fact]
    public void FarmUsualIsWithinTwoLevelsAnyTrio()
    {
        var near = new[] { 38, 39, 41 }.Select(l => Seg(level: l, trio: "Bard/Cleric/Wizard", t5: 20)).ToList();
        var far = Many(4, () => Seg(level: 30, t5: 90, trio: "Bard/Cleric/Wizard"));
        var f = FarmPace.For(Seg(level: 40, trio: "Druid/Monk/Rogue", earlier: false), [.. near, .. far], Default, 50);
        Assert.Equal(3, f.Runs);
        Assert.Equal(80, f.Usual!.Value, 3);   // 20 T5 in 15 minutes
    }

    [Fact]
    public void FarmUsualAtTheCapIsUnchanged()
    {
        var f = FarmPace.For(Seg(level: 50, earlier: false), Many(4, () => Seg(level: 50, t5: 20)), Default, 50);
        Assert.Equal(4, f.Runs);
        Assert.Equal("crawls you finished, solo", f.Basis);
    }

    [Fact]
    public void FarmLevelsComeFromTheEngineBelowTheCapAndAreNullAtIt()
    {
        Assert.Equal("levels 38–42", FarmPace.For(Seg(level: 40, earlier: false), [], Default, 50).Levels);
        Assert.Equal("levels 1–3", FarmPace.For(Seg(level: 1, grouped: true, earlier: false), [], Default, 50).Levels);
        Assert.Null(FarmPace.For(Seg(level: 50, earlier: false), [], Default, 50).Levels);
    }

    [Fact]
    public void FarmBasisNamesTheLevelsBelowTheCap()
    {
        Assert.Equal("crawls you finished, solo, levels 38–42", FarmPace.For(Seg(level: 40, earlier: false), [], Default, 50).Basis);
        Assert.Equal("grouped, levels 1–3", FarmPace.For(Seg(level: 1, grouped: true, earlier: false), [], Default, 50).Basis);
    }

    [Fact]
    public void TheVerdictCarriesEveryTickTheOverlayDraws()
    {
        var run = Seg("Kedge Keep D1", earlier: false);
        var p = RunPace.For(run, Many(4, () => Seg("Kedge Keep D1", t5: 15)));
        Assert.Equal(60, p.UsualT5PerHour!.Value, 3);     // the same runs' T5-eq/h, not another baseline
        var f = FarmPace.For(Seg(earlier: false, t5: 30), Many(4, () => Seg(t5: 10, kills: 45)), Default, 50);
        Assert.Equal(3, f.UsualKillsPerMinute!.Value, 3);
        Assert.Equal(1, f.Rank);                          // 120 against four visits at 40
        Assert.Equal(4, f.Runs);
    }

    [Fact]
    public void AskLeavesOutVisitsWithNoRate()
    {
        // Three completed visits and two instances with 5 kills that never finished.
        var rated = Many(3, () => Seg("Kedge Keep D1", level: 20, levels: 1.0, kills: 60));
        var unrated = Many(2, () => Seg("Kedge Keep D1", level: 22, levels: 1.0, kills: 5, completed: false));
        var d = AskData.From([.. rated, .. unrated], RouteAnalysis.FarmPatch, T0, onlyWithRate: true);
        Assert.NotNull(d.Places.Single().At(20));
        Assert.Null(d.Places.Single().At(22));
        Assert.Equal(2, d.NoRate);
        // The community's half and Home's farm list read as before.
        var all = AskData.From([.. rated, .. unrated], RouteAnalysis.FarmPatch, T0);
        Assert.NotNull(all.Places.Single().At(22));
        Assert.Equal(0, all.NoRate);
    }

    [Fact]
    public void ADifficultyWithOnlyUnratedVisitsGoes()
    {
        var d1 = Many(3, () => Seg("Kedge Keep D1", level: 20, levels: 1.0));
        var d2 = Many(3, () => Seg("Kedge Keep D2", level: 20, levels: 1.0, kills: 5, completed: false));
        Assert.Equal(2, AskData.From([.. d1, .. d2], RouteAnalysis.FarmPatch, T0).Difficulties.Single().Columns.Count);
        Assert.Empty(AskData.From([.. d1, .. d2], RouteAnalysis.FarmPatch, T0, onlyWithRate: true).Difficulties);
    }

    [Fact]
    public void TheAnswerCountsTheVisitsItLeftOut()
    {
        var segs = Many(3, () => Seg("Kedge Keep D1", level: 20, levels: 1.0)).Concat(Many(2, () => Seg("Kedge Keep D1", level: 22, levels: 1.0, kills: 5, completed: false))).ToList();
        var a = AskPage.Build(new AskQuery("level", 20, 25, null, null, "all", null),
            new AskInputs(segs, 0, 0, new TrackerOptions(), 20, null, 2, null, T0));
        Assert.Equal(2, a.NoRateLeftOut);
    }
}

/// <summary>A crawl going on is judged by the same judge as History, not by a baseline of its own (docs/specs/one-usual.md).</summary>
public class OneUsualInProgressTests
{
    [Fact]
    public async Task AVisitInProgressIsJudgedByTheSameJudgeAsHistory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-oneusual-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        var t0 = DateTime.Now.AddHours(-3);
        string L(double minutes, string text) =>
            $"[{t0.AddMinutes(minutes).ToString("ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture)}] {text}";
        var lines = new List<string>();
        // Three D4 visits (not finished, 11 kills each: a rate) at level 30, then one going on at level 40: nothing within 2 levels, so no usual (as History would say).
        foreach (var (start, level, finish) in new[] { (0.0, 30, false), (30.0, 30, false), (60.0, 30, false), (90.0, 40, false) })
        {
            lines.Add(L(start, "You have entered Nagafen's Lair."));
            lines.Add(L(start + 0.2, $"[{level} PAL/MNK/SHM] Venun (Ogre) ZONE: Nagafen's Lair"));
            lines.Add(L(start + 1, "You have entered Nagafen's Lair 4 (Refined)."));
            for (int m = 2; m <= 12; m++) lines.Add(L(start + m, "You have slain a lava guardian!"));
            lines.Add(L(start + 13, "You looted a Mote of Major Potential from an imp protector's corpse and stored it in your currency"));
            if (start < 90) lines.Add(L(start + 16, "You have entered Oggok."));
            if (finish) { lines.Add(L(start + 15, "You have completed the Dungeon Crawl and earned reward loot!")); lines.Add(L(start + 15.1, "You earned a refund of your instance charge.")); }
        }
        File.WriteAllLines(log, lines);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions()) { ModeSetting = "Farming" };
            await svc.ImportHistoryAsync();
            // No Start(): the loop's first Tick ends a visit whose log has been silent for hours (this one ends 1.5 h ago),
            // and whether it ran before the query decided if there was a current visit. Not running, a query only reads.
            var snap = await svc.Query(s => s.BuildSnapshot());
            Assert.Equal("farming", snap.Pace?.Kind);
            Assert.Equal("none", snap.Pace?.Farm?.Verdict);
            Assert.Equal(0, snap.Pace?.Farm?.Runs);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
