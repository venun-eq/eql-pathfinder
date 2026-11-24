using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>A visit is judged by what was played (docs/specs/judge-by-play.md): motes per hour for farming, grouped and unfinished play, levels per hour for leveling.</summary>
public class JudgeByPlayTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 18, 0, 0);
    private static readonly int[] Default = [3, 4];
    private static int _n;

    private static LevelingSegment Seg(string place = "Kedge Keep D4", int level = 40, double minutes = 15, double t5 = 10, bool grouped = false,
        bool completed = true, bool ended = true, bool raid = false, double levels = 0.2, string? boss = null, int kills = 60)
    {
        int d = int.Parse(place[^1..]);
        return new(place, "Kedge Keep", d, T0.AddHours(++_n), level, minutes, levels, t5, 0, 0, kills, 0, 0, 2, "crawl", grouped ? 1 : 0)
        { VisitId = $"v{_n}", KillLevels = levels, Completed = completed, Refunded = completed, Ended = ended, Raid = raid, Boss = boss };
    }

    [Fact]
    public void ADifficultyYouFarmIsFarmingAndTheRestIsLeveling()
    {
        Assert.Equal("farming", VisitKinds.Of(Seg("Kedge Keep D4"), Default, 50));
        Assert.Equal("farming", VisitKinds.Of(Seg("Kedge Keep D3"), Default, 50));
        Assert.Equal("leveling", VisitKinds.Of(Seg("Kedge Keep D1"), Default, 50));
        Assert.Equal("leveling", VisitKinds.Of(Seg("Kedge Keep D0"), Default, 50));
        Assert.Equal("leveling", VisitKinds.Of(Seg("Kedge Keep D4"), [], 50));
        Assert.Equal("farming", VisitKinds.Of(Seg("Kedge Keep D1"), [1], 50));
    }

    [Fact]
    public void GroupedAnInstanceNeverFinishedAndPlayAtTheCapAreJudgedByMotes()
    {
        Assert.Equal("grouped", VisitKinds.Of(Seg("Kedge Keep D1", grouped: true), Default, 50));
        Assert.Equal("grouped", VisitKinds.Of(Seg("Kedge Keep D4", grouped: true), Default, 50));
        Assert.Equal("farming", VisitKinds.Of(Seg("Kedge Keep D1", completed: false, ended: true), Default, 50));
        Assert.Equal("farming", VisitKinds.Of(Seg("Kedge Keep D0", level: 50), Default, 50));
    }

    [Fact]
    public void AnInstanceStillGoingIsLevelingUntilItEndsWithoutCompleted()
    {
        Assert.Equal("leveling", VisitKinds.Of(Seg("Kedge Keep D1", completed: false, ended: false), Default, 50));
    }

    [Fact]
    public void ARaidIsARaidWhateverItsDifficultyOrParty()
    {
        Assert.Equal("raid", VisitKinds.Of(Seg("Nagafen's Lair - Solo D4", raid: true, grouped: true, completed: false), Default, 50));
    }

    [Fact]
    public void FarmingIsJudgedByTheT5PerHourOfYourSameKindAtThatPlace()
    {
        // Your usual: 10 T5-eq in 15 minutes = 40 an hour, five times.
        var usual = Enumerable.Range(0, 5).Select(_ => Seg()).ToList();
        var more = FarmPace.For(Seg(t5: 13.1), usual, Default, 50);
        Assert.Equal("more", more.Verdict);
        Assert.Equal(0.31, more.Pct!.Value, 2);
        Assert.Equal(40, more.Usual!.Value, 3);
        Assert.Equal("less", FarmPace.For(Seg(t5: 7.6), usual, Default, 50).Verdict);
        Assert.Equal("usual", FarmPace.For(Seg(t5: 10.5), usual, Default, 50).Verdict);
    }

    [Fact]
    public void ABandComesFromYourOwnVisitsNotAFixedPercent()
    {
        var steady = Enumerable.Range(0, 6).Select(i => Seg(t5: 10 + i % 2 * 0.1)).ToList();
        var swingy = Enumerable.Range(0, 6).Select(i => Seg(t5: i % 2 == 0 ? 5 : 20)).ToList();
        Assert.True(FarmPace.For(Seg(), swingy, Default, 50).Band > FarmPace.For(Seg(), steady, Default, 50).Band);
    }

    [Fact]
    public void YourUsualIsOnlyVisitsOfTheSameKindSoloAndGroupedAndFinishedAndNotDontMix()
    {
        var grouped = Enumerable.Range(0, 4).Select(_ => Seg(grouped: true)).ToList();
        Assert.Equal("none", FarmPace.For(Seg(), grouped, Default, 50).Verdict);
        var unfinished = Enumerable.Range(0, 4).Select(_ => Seg(completed: false)).ToList();
        Assert.Equal("none", FarmPace.For(Seg(), unfinished, Default, 50).Verdict);
        Assert.Equal("grouped, levels 38–42", FarmPace.For(Seg(grouped: true), grouped, Default, 50).Basis);
        Assert.Equal("crawls you finished, solo, levels 38–42", FarmPace.For(Seg(), Enumerable.Range(0, 4).Select(_ => Seg()), Default, 50).Basis);
        Assert.Equal("instances you didn't finish, levels 38–42", FarmPace.For(Seg(completed: false), unfinished, Default, 50).Basis);
    }

    /// <summary>
    /// The user, 11-19, in a D4 Unrest crawl at 20: "no visit like this one at levels 18-22", with ten finished ones in the log.
    /// A crawl still going hasn't completed yet; it's one you're finishing, not one you left.
    /// </summary>
    [Fact]
    public void ACrawlStillGoingIsComparedWithTheCrawlsYouFinished()
    {
        var finished = Enumerable.Range(0, 5).Select(_ => Seg()).ToList();
        var live = FarmPace.For(Seg(t5: 10.5, completed: false, ended: false), finished, Default, 50);
        Assert.Equal("usual", live.Verdict);
        Assert.Equal(5, live.Runs);
        Assert.Equal("crawls you finished, solo, levels 38–42", live.Basis);
        // Once it ends without "completed" it was left: judged against the instances you didn't finish, as before.
        Assert.Equal("none", FarmPace.For(Seg(completed: false, ended: true), finished, Default, 50).Verdict);
    }

    /// <summary>The user, 11-18: a visit's usual is made of earlier visits only, so its verdict is fixed once it's over.</summary>
    [Fact]
    public void AFarmingVisitIsJudgedOnlyAgainstTheVisitsBeforeIt()
    {
        // 40 T5-eq an hour five times, then this visit at 52 (+30%), then five at 80 an hour that hadn't happened yet.
        var earlier = Enumerable.Range(0, 5).Select(_ => Seg()).ToList();
        var run = Seg(t5: 13);
        var later = Enumerable.Range(0, 5).Select(_ => Seg(t5: 20)).ToList();
        var f = FarmPace.For(run, [.. earlier, run, .. later], Default, 50);
        Assert.Equal("more", f.Verdict);
        Assert.Equal(40, f.Usual!.Value, 3);
        Assert.Equal(5, f.Runs);
        // The first visits at a place had nothing to be judged by.
        Assert.Equal("none", FarmPace.For(earlier[2], [.. earlier, run, .. later], Default, 50).Verdict);
        Assert.Equal(3, FarmPace.For(earlier[3], [.. earlier, run, .. later], Default, 50).Runs);
    }

    [Fact]
    public void FewerThanThreeVisitsOrTooLittleOfThisOneGivesNoVerdict()
    {
        // The usual is made before the visit it judges: a usual is earlier visits.
        List<LevelingSegment> two = [Seg(), Seg()], four = Enumerable.Range(0, 4).Select(_ => Seg()).ToList();
        Assert.Equal("none", FarmPace.For(Seg(), two, Default, 50).Verdict);
        // Too little is the rate rule (a finished crawl or 8 kills), not minutes: 3 kills, unfinished.
        var unfinished = Enumerable.Range(0, 4).Select(_ => Seg(completed: false)).ToList();
        Assert.Equal("early", FarmPace.For(Seg(completed: false, kills: 3), unfinished, Default, 50).Verdict);
        Assert.NotEqual("early", FarmPace.For(Seg(minutes: 3), four, Default, 50).Verdict);   // a finished crawl has a rate however short
    }

    /// <summary>Report #9: a D4 row lost to D0 because the place check compared levels an hour with any place at that level.</summary>
    [Fact]
    public void ThePlaceCheckComparesOnlyPlacesAtTheSameDifficulty()
    {
        var segs = new List<LevelingSegment>();
        for (int i = 0; i < 3; i++) { segs.Add(Seg("Befallen D0", 10, 10, levels: 0.5)); segs.Add(Seg("Befallen D1", 10, 10, levels: 0.2)); segs.Add(Seg("Kedge Keep D1", 10, 10, levels: 0.4)); }
        var d = AskData.From(segs, DateTime.MinValue, T0.AddDays(5));
        bool Open(string p, int l) => true;
        var check = PlaceCheck.At(d, "Befallen D1", 10, 1, Open);
        Assert.NotNull(check);
        Assert.Equal("Kedge Keep D1", check!.Better);   // never Befallen D0, which is faster but isn't the same difficulty
    }

    [Fact]
    public void AFarmedDifficultyHoldingOverHalfTheHoursMakesARelevelFarmed()
    {
        Assert.True(DifficultyMix.Farmed([(4, 300.0), (1, 100.0)], 450, Default));
        Assert.False(DifficultyMix.Farmed([(1, 300.0), (4, 100.0)], 450, Default));
        // Mixed: D3 and D4 each under half, together over half.
        Assert.True(DifficultyMix.Farmed([(3, 150.0), (4, 150.0), (1, 100.0), (0, 80.0)], 480, Default));
        Assert.False(DifficultyMix.Farmed([(4, 300.0)], 450, []));
    }
}

/// <summary>The review's fixes (judge-by-play): a stop judged whole, Home's kind picked in the engine, raid bosses kept, the typical of a kind.</summary>
public class JudgeByPlayReviewTests
{
    private static (double, double, double?, double?, int, string?) V(double minutes, double perHour) => (minutes, perHour * minutes / 60, 40, 0.25, 3, "crawls you finished, solo");

    [Fact]
    public void AStopIsJudgedWholeNotByItsFirstVisit()
    {
        // 24% less, usual, 54% more against a usual of 40: the stop's 44 an hour is 10% over, which is usual.
        var stop = FarmPace.ForStop([V(15, 30.4), V(15, 40), V(15, 61.6)]);
        Assert.Equal("usual", stop.Verdict);
        Assert.Equal(0.10, stop.Pct!.Value, 2);
        Assert.Equal("more", FarmPace.ForStop([V(15, 30.4), V(15, 61.6), V(15, 70)]).Verdict);
    }

    [Fact]
    public void AStopWithNoUsualOrTooLittleTimeSaysSo()
    {
        Assert.Equal("none", FarmPace.ForStop([(15, 10, null, null, 0, "grouped")]).Verdict);
        Assert.Equal("early", FarmPace.ForStop([V(3, 90)], early: true).Verdict);
    }

    private static Journey J(string trio, bool done, double hours, double crawlMinutes, int diff = 4, double t5 = 100, DateTime? start = null)
    {
        var route = crawlMinutes > 0 ? new[] { new JourneyLeg("Befallen", diff, 1, 50, 2, crawlMinutes, 10, t5) } : [new JourneyLeg("Befallen", null, 1, 50, 1, 30, 1, 0, "open world")];
        return new(trio, EqlPathfinder.Engine.Model.ClassNames.TrioKey(trio), "Human", T0, start ?? T0, T0.AddHours(hours), 1, 50, done, hours, hours + 1, 2, 100, 0, t5, 10, 1,
            route, null, null, null, []);
    }
    private static readonly DateTime T0 = new(2026, 11, 1, 12, 0, 0);

    [Fact]
    public void HomesKindIsTheRelevelOnNowWithCrawlsElseTheLastFinishedElseNone()
    {
        var now = J("WAR/CLR/RNG", false, 2, 90, start: T0.AddDays(2));
        var done = J("BRD/DRU/NEC", true, 5, 200, diff: 1, start: T0.AddDays(1));
        Assert.Equal(("now", "WAR/CLR/RNG"), Pick([now, done], "WAR/CLR/RNG"));
        Assert.Equal(("last", "BRD/DRU/NEC"), Pick([done], "WAR/CLR/RNG"));
        Assert.Equal(("last", "BRD/DRU/NEC"), Pick([done], null));
        Assert.Equal((null, null), Pick([], "WAR/CLR/RNG"));
        // Open world alone is no difficulty: not "mixed".
        var field = J("WAR/CLR/RNG", false, 2, 0, start: T0.AddDays(2));
        Assert.Equal((null, null), Pick([field], "WAR/CLR/RNG"));
        Assert.Equal(("last", "BRD/DRU/NEC"), Pick([field, done], "WAR/CLR/RNG"));
    }

    private static (string?, string?) Pick(IReadOnlyList<Journey> js, string? trio) { var (j, src) = DifficultyMix.HomeKind(js, trio); return (src, j?.Trio); }

    [Fact]
    public void TheTypicalOfAKindIsTheMedianOfYourOtherFinishedRelevelsOfIt()
    {
        var a = J("A/B/C", true, 2, 100, t5: 100);       // 50 an hour
        var b = J("D/E/F", true, 2, 100, t5: 120);       // 60
        var c = J("G/H/I", true, 2, 100, t5: 140);       // 70
        var d1 = J("J/K/L", true, 2, 100, diff: 1, t5: 10);
        var r = Journeys.WithTypicals([a, b, c, d1]);
        Assert.Equal(65, r[0].TypicalOfKind!.Value, 3);   // b and c
        Assert.Equal(60, r[1].TypicalOfKind!.Value, 3);   // a and c
        Assert.Null(r[3].TypicalOfKind);                  // the only D1 one
        Assert.Null(Journeys.WithTypicals([a, b])[0].TypicalOfKind);   // one other is too few
    }

    [Fact]
    public void ARaidBossSlainInsideTheRaidIsKept()
    {
        var s = new TrackerTests.Sink();
        var e = new PathfinderEngine("Venun", new TrackerOptions(), s);
        foreach (var l in new[]
        {
            "[Sat Nov 14 12:00:00 2026] You have entered Nagafen's Lair - Solo 4 (Refined).",
            "[Sat Nov 14 12:05:00 2026] You have slain Lord Nagafen!",
            "[Sat Nov 14 12:30:00 2026] You have entered Nagafen's Lair.",
            "[Sat Nov 14 13:30:00 2026] You have entered Befallen.",   // an hour on: a visit with a kill waits to be resumed, then closes
        }) e.ProcessLine(l);
        e.Flush();
        var v = Assert.Single(s.Closed);
        Assert.Equal("Lord Nagafen", v.BossKilled);
        var st = VisitStats.From(v, v.EndTime, new TrackerOptions());
        Assert.True(st.Raid);
        Assert.Equal("Lord Nagafen", st.Boss);
        Assert.True(EngineState.CurrentVersion >= 11);   // earlier raids are read again to find their boss
    }
}
