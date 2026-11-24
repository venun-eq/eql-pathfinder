using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Hand-checked values for the numbers on a visit (docs/specs/golden-numbers.md). Every expected value was worked out from the
/// definitions in docs/pace.md and docs/log-formats.md and the raw log lines (or from a few numbers picked so the arithmetic is
/// done in the head), never by running the engine and copying what it printed. The arithmetic is beside each value.
/// </summary>
public class GoldenNumbersTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 18, 0, 0);
    private static readonly TrackerOptions Options = new();

    // ---------------------------------------------------------------- the four Befallen crawls, from the log's own lines

    private sealed class Sink : ITrackerSink
    {
        public List<Visit> Closed { get; } = new();
        public void OnVisitClosed(Visit v) => Closed.Add(v);
        public void OnKill(KillRecord k) { }
    }

    private static List<Visit> Crawls()
    {
        var s = new Sink();
        var e = new PathfinderEngine(TestLog.Character, new TrackerOptions(), s);
        e.ReadFile(TestLog.BefallenUnrest);
        e.Flush();
        return s.Closed;
    }

    /// <summary>
    /// The fixture's whole History: the four visits above, and a fifth the values above do not hold (The Estate of Unrest, below). The log's instance entries are
    /// 12:06:20 (Befallen 1), 12:41:49, 13:07:53 and 13:34:27 (Befallen 4) and 13:50:04 (Estate of Unrest 4, finished 14:05:51; its numbers are not hand-checked yet). The other entries are outside zones.
    /// </summary>
    [Fact]
    public void TheWholeHistoryIsTheFourHeldCrawlsAndAFifthNotYetHeld()
    {
        var got = Crawls().Where(v => v.Difficulty != null).OrderBy(v => v.Start).Select(v => v.Start.ToString("HH:mm") + " " + v.Zone).ToList();
        Assert.Equal(new[] { "12:06 Befallen 1 (Awakened)", "12:41 Befallen 4 (Refined)", "13:07 Befallen 4 (Refined)", "13:34 Befallen 4 (Refined)", "13:50 The Estate of Unrest 4 (Refined)" }, got);
    }

    private static void Close(double expected, double? actual, double tolerance) =>
        Assert.True(actual is { } a && Math.Abs(a - expected) <= tolerance, $"expected {expected} +/- {tolerance}, got {actual}");

    /// <summary>
    /// The clock, from the log's time stamps (idle threshold 3 minutes; no gap before a kill, between two, or after the last is
    /// longer than that in any of the four, so idle is 0):
    ///   12:06  zone-in 12:06:20, completed 12:37:28 = 1868 s inside = 31.1333 min; zone-out (the next "You have entered") 12:39:37
    ///          = 1997 s whole = 33.2833 min; after = 1997 - 1868 = 129 s = 2.15 min.
    ///   12:41  zone-in 12:41:49, last kill 13:02:24 (a straggler after the 13:01:39 completion) = 1235 s = 20.5833 min; zone-out
    ///          13:02:39 = 1250 s = 20.8333 min whole; after 15 s = 0.25 min.
    ///   13:07  zone-in 13:07:53, completed 13:24:41 = 1008 s = 16.8 min; zone-out 13:24:58 = 1025 s = 17.0833 min; after 17 s.
    ///   13:34  zone-in 13:34:27, last kill 13:48:10 (completed 13:48:03) = 823 s = 13.7167 min; zone-out 13:48:26 = 839 s = 13.9833
    ///          min; after 16 s.
    /// Kills are the slain lines in the visit (an instance counts a groupmate's: 134, 77, 67, 72). A rate is x * 60 / active minutes.
    /// Kill levels are the sum of the "You gain experience! (x%)" lines with a slain line in their second (9.2157, 2.9672, 2.1982,
    /// 1.4882); reward levels are GoldenTests' hand-checked 0.70, 0.45, 0.38, 0.26 at 2 decimals, hence the +/- 0.05 on levels/h.
    /// T5-eq is spell points / 16 (docs/motes-and-fragments.md): a mote of tier r is 2^(r-5), so Major 1, Potential 0.5, Lesser 0.25,
    /// Minor 0.125; a fragment is that tier's mote at the fragment value 1.6. From the looted lines:
    ///   12:06 1 + 0.5 x 1.6 = 1.8; 12:41 6 + 4 x 1.6 = 12.4; 13:07 4.5 + 4 x 1.6 = 10.9; 13:34 7 + 3 x 1.6 = 11.8.
    /// </summary>
    [Theory]
    // start, active min, whole min, after min, kills, kills/min, levels/h, T5-eq, T5-eq/h
    //   kills/min  134/31.1333 = 4.3041;  77/20.5833 = 3.7409;  67/16.8 = 3.9881;  72/13.7167 = 5.2491
    //   levels/h   (9.2157+0.70) x 60/31.1333 = 19.109;  (2.9672+0.45) x 60/20.5833 = 9.961;  (2.1982+0.38) x 60/16.8 = 9.207;
    //              (1.4882+0.26) x 60/13.7167 = 7.648
    //   T5-eq/h    1.8 x 60/31.1333 = 3.469;  12.4 x 60/20.5833 = 36.146;  10.9 x 60/16.8 = 38.929;  11.8 x 60/13.7167 = 51.614
    [InlineData("12:06", 1868 / 60.0, 1997 / 60.0, 129 / 60.0, 134, 4.3041, 19.109, 1.8, 3.469)]
    [InlineData("12:41", 1235 / 60.0, 1250 / 60.0, 15 / 60.0, 77, 3.7409, 9.961, 12.4, 36.146)]
    [InlineData("13:07", 1008 / 60.0, 1025 / 60.0, 17 / 60.0, 67, 3.9881, 9.207, 10.9, 38.929)]
    [InlineData("13:34", 823 / 60.0, 839 / 60.0, 16 / 60.0, 72, 5.2491, 7.648, 11.8, 51.614)]
    public void ACrawlsClockAndRatesAreTheHandWorkedOnes(string start, double active, double whole, double after, int kills, double kpm, double lvh, double t5, double t5h)
    {
        var v = Crawls().Single(x => x.Start.ToString("HH:mm") == start);
        var s = VisitStats.From(v, v.EndTime.AddHours(1), new TrackerOptions { FragmentValue = 1.6 });
        Assert.Equal(active, s.ActiveMinutes, 3);
        Assert.Equal(0, s.IdleMinutes, 6);
        Assert.Equal(active, s.Minutes, 3);
        Assert.Equal(whole, s.WholeMinutes, 3);
        Assert.Equal(after, s.AfterMinutes, 3);
        Assert.Equal(0, s.OutsideMinutes, 6);
        Assert.Equal(s.ActiveMinutes + s.IdleMinutes + s.AfterMinutes + s.OutsideMinutes, s.WholeMinutes, 6);
        Assert.Equal(kills, s.Kills);
        Close(kpm, s.KillsPerMinute, 0.001);
        Close(lvh, s.LevelsPerHour, 0.05);
        Assert.Equal(t5, s.T5, 1);
        Close(t5h, s.T5PerHour, 0.02);
        if (start == "13:34")
        {
            // At the app's own fragment value, 3.56: 7 motes + 3 fragments x 3.56 = 17.68 (docs/motes-and-fragments.md).
            Assert.Equal(17.68, VisitStats.From(v, v.EndTime.AddHours(1), Options).T5, 2);
        }
    }

    // ---------------------------------------------------------------- Kill s (the median), from numbers picked for the arithmetic

    [Fact]
    public void KillSecondsIsTheMiddleOneOfAnOddCountAndTheMeanOfTheMiddleTwoOfAnEvenCount()
    {
        // 10, 20, 30 -> 20 (the middle of three). 10, 20, 30, 50 -> (20 + 30) / 2 = 25.
        var odd = new Visit { KillSeconds = { 30, 10, 20 } };
        var even = new Visit { KillSeconds = { 50, 10, 30, 20 } };
        Assert.Equal(20, odd.MedianKillSeconds);
        Assert.Equal(25, even.MedianKillSeconds);
        Assert.Null(new Visit().MedianKillSeconds);
    }

    // ---------------------------------------------------------------- active and idle time

    [Fact]
    public void IdleIsEveryGapPastThreeMinutesCountedOnlyForWhatIsOver()
    {
        // Kills at 60, 100 and 400 s, the crawl ends at 420 s, threshold 180 s. Gaps: 0->60 = 60 (under), 60->100 = 40 (under),
        // 100->400 = 300 (120 over), 400->420 = 20 (under). Idle = 120 s.
        Assert.Equal(120, ActiveTime.IdleSeconds([60, 100, 400], 420, [], 180));
        // A first pull late: one kill at 500, ends 500. 0->500 = 500, 320 over.
        Assert.Equal(320, ActiveTime.IdleSeconds([500], 500, [], 180));
        // A last gap long: one kill at 60, ends at 400: 0->60 under, 60->400 = 340, 160 over.
        Assert.Equal(160, ActiveTime.IdleSeconds([60], 400, [], 180));
        // A pause (out of the instance) 200..300 s sits inside the 100->400 gap, so that gap counts 300 - 100 = 200: 20 over. Idle = 20 s.
        Assert.Equal(20, ActiveTime.IdleSeconds([60, 100, 400], 420, [(200, 300)], 180));
        // No kills: nothing to measure activity by, so no idle.
        Assert.Equal(0, ActiveTime.IdleSeconds([], 900, [], 180));
    }

    [Fact]
    public void ActiveMinutesAreTimeInsideLessIdle()
    {
        // 10 minutes inside (600 s), kills at 30, 60 and 570 s, completed at 600 s: gaps 30, 30, 510 (330 over), 30. Idle 330 s = 5.5 min.
        // Active = 600 - 330 = 270 s = 4.5 min.
        var v = new Visit { Id = "v", Start = T0, ActiveSeconds = 600, Completed = true, CompletedAt = T0.AddSeconds(600), Closed = true, ExitedAt = T0.AddSeconds(600), Kills = 3 };
        foreach (var s in new[] { 30, 60, 570 }) v.KillTimes.Add(T0.AddSeconds(s));
        Assert.Equal(4.5, ActiveTime.Minutes(v, 180, T0.AddHours(1)), 6);
        var stats = VisitStats.From(v, T0.AddHours(1), Options);
        Assert.Equal(5.5, stats.IdleMinutes, 6);
        Assert.Equal(4.5, stats.ActiveMinutes, 6);
    }

    [Fact]
    public void AnOpenWorldStretchIsFirstKillToLastPlusAMinuteForThePull()
    {
        // 12:00:00 to 12:10:30 = 10.5 min, + 1 = 11.5. A single kill is the minute alone.
        var first = new DateTime(2026, 11, 16, 12, 0, 0);
        Assert.Equal(11.5, ActiveTime.OpenWorldMinutes(first, first.AddMinutes(10.5)), 6);
        Assert.Equal(1, ActiveTime.OpenWorldMinutes(first, first), 6);
    }

    // ---------------------------------------------------------------- cost per level, the farm rate

    [Fact]
    public void ACostPerLevelIsTheFarmRateGivenUpOverTheLevelsGained()
    {
        // Farming gives 50 T5-eq an hour, leveling here gives 20 T5-eq and 10 levels an hour: 30 given up for 10 levels = 3 a level.
        Assert.Equal(3.0, RouteAnalysis.CostPerLevel(50, 20, 10));
        // Leveling that out-earns the farm costs less than nothing: (50 - 80) / 10 = -3.
        Assert.Equal(-3.0, RouteAnalysis.CostPerLevel(50, 80, 10));
        // Too slow to say: 0.05 levels an hour or under has no cost; just over it does: (50 - 20) / 0.06 = 500.
        Assert.Null(RouteAnalysis.CostPerLevel(50, 20, 0.05));
        Assert.Equal(500.0, RouteAnalysis.CostPerLevel(50, 20, 0.06)!.Value, 6);
    }

    /// <summary>A finished crawl at the cap: one kill a minute for <paramref name="minutes"/> (so no idle), <paramref name="motes"/> Majors, T5-eq an hour = motes x 60 / minutes.</summary>
    private static Visit Farm(string id, string zone, int motes, int minutes = 20)
    {
        var v = new Visit
        {
            Id = id,
            Zone = zone,
            BaseZone = zone,
            Difficulty = 4,
            Start = T0,
            ActiveSeconds = minutes * 60,
            LevelStart = 50,
            LevelEnd = 50,
            Kills = minutes,
            Completed = true,
            CompletedAt = T0.AddMinutes(minutes),
            Closed = true,
            ExitedAt = T0.AddMinutes(minutes),
        };
        v.Loot.Add(LootKind.Mote, MoteTier.Major, motes, false);
        for (int i = 1; i <= minutes; i++) v.KillTimes.Add(T0.AddMinutes(i));
        return v;
    }

    [Fact]
    public void TheFarmRateIsTheBestPlacesMedianOrTheUsualOfAllAndNeedsThreeCrawls()
    {
        // 20-minute crawls, so T5-eq an hour = motes x 3. Kedge: 10, 20, 30 motes = 30, 60, 90 (median 60). Nektropos: 3, 4, 5 motes = 9, 12, 15
        // (median 12). All six: 9, 12, 15, 30, 60, 90 -> median (15 + 30) / 2 = 22.5.
        var visits = new List<Visit>
        {
            Farm("a1", "Kedge Keep", 10), Farm("a2", "Kedge Keep", 20), Farm("a3", "Kedge Keep", 30),
            Farm("b1", "Nektropos Castle", 3), Farm("b2", "Nektropos Castle", 4), Farm("b3", "Nektropos Castle", 5),
        };
        var info = RouteAnalysis.FarmRates(visits, Options);
        Assert.Equal(6, info.Crawls);
        Assert.Equal(22.5, info.Usual!.Value, 6);
        Assert.Equal(60, info.Best!.Value, 6);
        Assert.Equal("Kedge Keep D4", info.BestZone);
        Assert.Equal(3, info.BestCrawls);
        // Mode "best" (the default) weighs levels against 60, "usual" against 22.5, "manual" against what was typed.
        Assert.Equal(60, RouteAnalysis.EffectiveFarmRate(info, new TrackerOptions()), 6);
        Assert.Equal(22.5, RouteAnalysis.EffectiveFarmRate(info, new TrackerOptions { FarmRateMode = "usual" }), 6);
        Assert.Equal(35, RouteAnalysis.EffectiveFarmRate(info, new TrackerOptions { FarmRateMode = "manual", FarmRateManual = 35 }), 6);
        // Two crawls are not three: the app's default of 50 stands.
        var few = RouteAnalysis.FarmRates(visits.Take(2), Options);
        Assert.Equal(50, RouteAnalysis.EffectiveFarmRate(few, new TrackerOptions()), 6);
    }

    // ---------------------------------------------------------------- the farming verdict's band

    private static LevelingSegment Seg(string id, double t5PerHour, int daysBefore = 1) =>
        new("Kedge Keep D4", "Kedge Keep", 4, T0.AddDays(-daysBefore), 50, 60, 0, t5PerHour, 0, 0, 30, 0, 0) { Completed = true, Refunded = true, VisitId = id };

    private static FarmPace Judge(double mine, params double[] usual) =>
        FarmPace.For(Seg("now", mine, 0), usual.Select((r, i) => Seg("p" + i, r)).ToList(), [3, 4], 50);

    [Fact]
    public void TheBandIsTheSpreadOfYourOwnVisitsInLogsAndIsClamped()
    {
        // 60-minute segments, so T5-eq an hour is the T5 given. Five earlier visits 80, 100, 100, 100, 125: their logs differ from ln 100 by
        // -0.22314, 0, 0, 0, +0.22314 (80 x 125 = 100^2). Sum of squares 2 x 0.049793 = 0.099586, over n-1 = 4 = 0.0248965, sd 0.157786,
        // band = e^0.157786 - 1 = 0.17091.
        var spread = Judge(100, 80, 100, 100, 100, 125);
        Assert.Equal(0.17091, spread.Band, 4);
        Assert.Equal(100, spread.Usual!.Value, 6);   // the median of the five
        // Identical visits have no spread at all: the floor, 0.15.
        Assert.Equal(0.15, Judge(100, 100, 100, 100, 100, 100).Band, 6);
        // 10, 20, 40, 80, 160: logs one ln 2 apart, sd = ln 2 x sqrt(2.5) = 1.0960, e^1.0960 - 1 = 1.99, past the ceiling, 0.6.
        Assert.Equal(0.6, Judge(40, 10, 20, 40, 80, 160).Band, 6);
        // Four visits are too few to measure one: the default 0.25.
        Assert.Equal(0.25, Judge(100, 80, 100, 100, 125).Band, 6);
    }

    [Fact]
    public void AVisitIsMoreOrLessOnlyPastTheBandAndNoneWithUnderThreeEarlierVisits()
    {
        // Usual 100, band 0.17091: 120 is +20% (more), 110 is +10% (usual), 80 is -20% (less), 90 is -10% (usual).
        var five = new[] { 80.0, 100, 100, 100, 125 };
        Assert.Equal("more", Judge(120, five).Verdict);
        Assert.Equal("usual", Judge(110, five).Verdict);
        Assert.Equal("less", Judge(80, five).Verdict);
        Assert.Equal("usual", Judge(90, five).Verdict);
        Assert.Equal(0.20, Judge(120, five).Pct!.Value, 6);
        Assert.Equal("none", Judge(120, 100, 100).Verdict);
    }
}
