using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>The verdicts as pure code (docs/specs/verdicts-out.md): hand-built visits in, the verdict a player reads out.</summary>
public class VerdictsTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 18, 0, 0);
    private static readonly TrackerOptions Options = new();
    private static readonly int[] Farmed = [3, 4];
    private static int _n;

    private static LevelingSegment Seg(string place, int level = 40, double minutes = 15, double t5 = 10, bool grouped = false, bool earlier = true) =>
        new(place, "Kedge Keep", int.Parse(place[^1..]), earlier ? T0.AddHours(-1000 + ++_n) : T0.AddHours(++_n), level, minutes, 0.2, t5, 0, 0, 60, 0, 0, 2, "crawl",
            grouped ? 1 : 0, null)
        { VisitId = $"v{++_n}", KillLevels = 0.2, Completed = true, Refunded = true, Ended = true };

    private static Verdict Judge(LevelingSegment run, List<LevelingSegment> here) =>
        Verdicts.Judge(run, here, here, Farmed, Options);

    private static Visit Farm(string id, double minutes, string zone = "Kedge Keep", int difficulty = 4, double motes = 20)
    {
        var v = new Visit
        {
            Id = id,
            Zone = zone,
            BaseZone = zone,
            Difficulty = difficulty,
            Start = T0,
            ActiveSeconds = minutes * 60,
            LevelStart = 50,
            LevelEnd = 50,
            Kills = 40,
            Completed = true,
            Closed = true,
            ExitedAt = T0.AddMinutes(minutes),
        };
        v.Loot.Add(LootKind.Mote, MoteTier.Major, (int)motes, false);
        for (int i = 0; i < 40; i++) v.KillTimes.Add(T0.AddSeconds(30 + i * 5));
        return v;
    }

    [Fact]
    public void ALevelingRunWithNoEarlierVisitsHasNoUsual()
    {
        var run = Seg("Kedge Keep D1", earlier: false);
        var v = Judge(run, [run]);
        Assert.Equal(VisitKinds.Leveling, v.Kind);
        Assert.Equal("none", v.Pace!.Verdict);
        Assert.Null(v.Farm);
    }

    [Fact]
    public void AFarmedDifficultyIsJudgedByTheRateAgainstItsUsual()
    {
        var earlier = Enumerable.Range(0, 3).Select(_ => Seg("Kedge Keep D4", t5: 10)).ToList();
        var run = Seg("Kedge Keep D4", t5: 30, earlier: false);   // three times the usual
        var v = Judge(run, [.. earlier, run]);
        Assert.Equal(VisitKinds.Farming, v.Kind);
        Assert.Null(v.Pace);
        Assert.Equal("more", v.Farm!.Verdict);
    }

    [Fact]
    public void AGroupedRunIsJudgedByMotesToo()
    {
        var run = Seg("Kedge Keep D1", grouped: true, earlier: false);
        var v = Judge(run, [run]);
        Assert.Equal(VisitKinds.Grouped, v.Kind);
        Assert.Null(v.Pace);
        Assert.NotNull(v.Farm);
    }

    [Fact]
    public void TheCommunityViewIsNotAskedWhenTheVisitHasAUsualOfItsOwn()
    {
        var earlier = Enumerable.Range(0, 3).Select(_ => Seg("Kedge Keep D1", t5: 10)).ToList();
        var run = Seg("Kedge Keep D1", t5: 10, earlier: false);
        var v = Verdicts.Judge(run, [.. earlier, run], [.. earlier, run], Farmed, Options,
            _ => throw new InvalidOperationException("asked"));
        Assert.NotEqual("none", v.Pace!.Verdict);
    }

    [Fact]
    public void ARaidRowIsMarkedARaidWhateverElseWasJudged()
    {
        var st = VisitStats.From(Farm("r", 10, "Some Raid - Solo"), T0.AddHours(1), Options);
        var row = Verdicts.Apply(st, new Verdict(VisitKinds.Farming, null, null, FarmPace.ForStop([(10, 5, 5, 0.2, 3, "basis")])));
        Assert.Equal(VisitKinds.Raid, row.Kind);
        Assert.Null(row.FarmVerdict);
    }

    [Fact]
    public void ThreeVisitsInARowAtOnePlaceAreOneStopAndAnotherPlaceIsAnother()
    {
        // Newest first, as History sends them: two more at Kedge, then one at another zone, then three at Kedge (the stop of three).
        var rows = new List<VisitStats>();
        foreach (var (id, zone) in new[] { ("a", "Kedge Keep"), ("b", "Kedge Keep"), ("c", "Other Zone"), ("d", "Kedge Keep"), ("e", "Kedge Keep"), ("f", "Kedge Keep") })
            rows.Add(Verdicts.Apply(VisitStats.From(Farm(id, 10, zone), T0.AddHours(1), Options),
                new Verdict(VisitKinds.Farming, null, null, FarmPace.ForStop([(10, 5, null, null, 0, null)]))));
        Verdicts.AssignStops(rows);
        Assert.All(rows, r => Assert.NotNull(r.StopFarm));
        Assert.NotNull(rows[0].StopFarm);
        Assert.Same(rows[0].StopFarm, rows[1].StopFarm);
        Assert.NotSame(rows[1].StopFarm, rows[2].StopFarm);
        Assert.NotSame(rows[2].StopFarm, rows[3].StopFarm);
        Assert.Same(rows[3].StopFarm, rows[5].StopFarm);
    }

    // ---- a past visit's place words are as of its start (docs/specs/verdicts-as-of-visit.md)

    private static LevelingSegment On(string place, double day, double minutes) => Seg(place, minutes: minutes) with { Start = T0.AddDays(day) };

    private static List<LevelingSegment> Visits(string place, double firstDay, double minutes, int n = 3) =>
        Enumerable.Range(0, n).Select(i => On(place, firstDay + i, minutes)).ToList();

    [Fact]
    public void APastVisitsPlaceWordsDoNotChangeWithTodaysDateOrLaterPlay()
    {
        var earlier = Visits("Befallen D1", 0, 5).Concat(Visits("Kedge Keep D1", 3, 15)).ToList();
        var run = On("Kedge Keep D1", 10, 15);
        var then = Judge(run, [.. earlier, run]);
        Assert.Equal("slower", then.Place!.Kind);
        // A month and a half later, the slower place has been played faster than the rival: the old visit's words stay.
        var later = Visits("Kedge Keep D1", 50, 2, 6).Concat(Visits("Befallen D1", 52, 30, 2)).ToList();
        var now = Judge(run, [.. earlier, run, .. later]);
        Assert.Equal(then.Place.Kind, now.Place!.Kind);
        Assert.Equal(then.Place.Better, now.Place.Better);
        Assert.Equal(then.Place.LevelsPerHour, now.Place.LevelsPerHour);
        Assert.Equal(then.Place.BetterLevelsPerHour, now.Place.BetterLevelsPerHour);
    }

    [Fact]
    public void AVisitPlayedBeforeAFasterPlaceWasKnownHasNoPlaceWordsAndALaterOneDoes()
    {
        var early = On("Kedge Keep D1", 1, 15);
        var fast = Visits("Befallen D1", 5, 5);
        var late = On("Kedge Keep D1", 20, 15);
        List<LevelingSegment> all = [On("Kedge Keep D1", 0, 15), early, .. fast, late];
        Assert.Null(Judge(early, all).Place);
        Assert.Equal("slower", Judge(late, all).Place!.Kind);
    }

    [Fact]
    public void APlacesFirstVisitNearALevelHasNoPlaceWordsEvenWhenItsOwnRunWasSlow()
    {
        var run = On("Kedge Keep D1", 10, 60);
        Assert.Null(Judge(run, [.. Visits("Befallen D1", 0, 5), run]).Place);
    }

    [Fact]
    public void AFasterPlaceThatIsNotClearlyFasterIsReportedAsSuchAndNothingElse()
    {
        var earlier = Visits("Befallen D1", 0, 14).Concat(Visits("Kedge Keep D1", 3, 15)).ToList();   // 7% faster: inside the swing
        var run = On("Kedge Keep D1", 10, 15);
        var v = Judge(run, [.. earlier, run]);
        Assert.Null(v.Place);
        Assert.Equal("Befallen D1", v.NotClear);
        Assert.Null(Judge(run, [.. Visits("Kedge Keep D1", 3, 15), run]).NotClear);   // nothing faster
    }
}
