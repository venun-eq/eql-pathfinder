using EqlPathfinder.Engine.Analysis;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The relevel race (docs/specs/journey-race-in-engine.md): the finished relevels re-figured to one XP requirement, the
/// middle half, the splits, the best of each, and how an open relevel stands. Worked by hand on made-up relevels.
/// </summary>
public class JourneyRaceTests
{
    private static readonly double Xp0 = Planner.XpMultiplier(0), Xp1 = Planner.XpMultiplier(1), Xp2 = Planner.XpMultiplier(2);

    /// <summary>A relevel from level 1: a straight line through (0 h, 1) and (hours, levelNow), or the curve given.</summary>
    private static Journey Relevel(string trio, int rank, double hours, int diff, string end, bool done = true, int levelNow = 50, params (double Hours, double Level)[] curve)
    {
        var points = curve.Length > 0 ? curve.Select(p => new CurvePoint(p.Hours, p.Level)).ToList() : [new CurvePoint(0, 1), new CurvePoint(hours, levelNow)];
        var leg = new JourneyLeg("Befallen", diff, 1, levelNow, 3, hours * 60, levelNow - 1, 100, "crawl");
        return new Journey(trio, trio, "Human", DateTime.Parse("2026-11-01"), DateTime.Parse("2026-11-01"), DateTime.Parse(end), 1, levelNow, done,
            hours, hours + 2, 3, 500, 0, 100, levelNow - 1, rank, [leg], null, null, null, points);
    }

    private static List<Journey> Three() =>
    [
        Relevel("AAA", 0, 10, 4, "2026-11-03"),    // 10 h at xp 1.0
        Relevel("BBB", 1, 12, 4, "2026-11-04"),    // 12 h at xp 1.1
        Relevel("CCC", 2, 14, 1, "2026-11-05"),    // 14 h at xp 1.2, mostly D1
    ];

    [Fact]
    public void RelevelsAtDifferentXpAreRefiguredToTheTargetAndSortedBestFirst()
    {
        var race = JourneyRace.All(Three(), 50, Xp2);
        Assert.Equal(3, race.Rows.Count);
        // AAA: 10 h x (1.2 / 1.0) = 12 h; BBB: 12 x (1.2 / 1.1) = 13.09 h; CCC: 14 h as played.
        Assert.Equal([0, 1, 2], race.Rows.Select(r => r.Journey));
        Assert.Equal(Xp2 / Xp0, race.Rows[0].F, 9);
        Assert.Equal(10 * Xp2 / Xp0, race.Rows[0].Hours, 9);
        Assert.Equal(12 * Xp2 / Xp1, race.Rows[1].Hours, 9);
        Assert.Equal(1, race.Rows[2].F);
        Assert.Equal(14, race.Rows[2].Hours, 9);
        Assert.Equal(10 * Xp2 / Xp0, race.BestCurve![1].Hours, 9);
        Assert.Equal(50, race.BestCurve[1].Level);
        Assert.True(race.Refigured);
        Assert.True(race.MixedRanks);
        Assert.Equal(12 * Xp2 / Xp1, race.TypicalHours!.Value, 9);
        Assert.Equal(Xp2, race.Target);
    }

    [Fact]
    public void WithNoTargetTheyCompareAtTheXpTheLatestOneNeeded()
    {
        var race = JourneyRace.All(Three(), 50, null);
        Assert.Equal(Xp2, race.Target);   // CCC ended last, at rank 2
        Assert.Equal(14, race.Rows.Single(r => r.Journey == 2).Hours, 9);
    }

    [Fact]
    public void OnlyRelevelsOfTheKindCompare_AndNoKindIsNothingToRace()
    {
        var d4 = JourneyRace.OfKind(Three(), 50, Xp1, "D4");
        Assert.Equal([0, 1], d4.Rows.Select(r => r.Journey).Order());
        var d1 = JourneyRace.OfKind(Three(), 50, Xp1, "D1");
        Assert.Equal([2], d1.Rows.Select(r => r.Journey));
        Assert.Empty(JourneyRace.OfKind(Three(), 50, Xp1, "mixed").Rows);
        Assert.Empty(JourneyRace.OfKind(Three(), 50, Xp1, null).Rows);
        Assert.Empty(JourneyRace.OfKind(Three(), 50, Xp1, null).Band.Med);
    }

    [Fact]
    public void AnOpenRelevelAndOneWithoutACurveAreNotRaced()
    {
        var js = Three();
        js.Add(Relevel("OPEN", 1, 3, 4, "2026-11-06", done: false, levelNow: 30));
        js.Add(Relevel("FLAT", 1, 3, 4, "2026-11-07", curve: [(0, 1)]));
        Assert.Equal([0, 1, 2], JourneyRace.All(js, 50, Xp1).Rows.Select(r => r.Journey).Order());
    }

    /// <summary>Three relevels at one XP, 10, 12 and 14 h, each a straight line from level 1 to 50: half-way up (level 25.5) they were at 5, 6 and 7 h.</summary>
    [Fact]
    public void TheBandIsTheMiddleHalfAndTheMedianOfTheHoursEachLevelTook()
    {
        var js = new List<Journey> { Relevel("A", 1, 10, 4, "2026-11-03"), Relevel("B", 1, 12, 4, "2026-11-04"), Relevel("C", 1, 14, 4, "2026-11-05") };
        var band = JourneyRace.All(js, 50, Xp1).Band;
        Assert.Equal(6, band.Med.Single(p => p.Level == 25.5).Hours, 9);
        Assert.Equal(5.5, band.Lo.Single(p => p.Level == 25.5).Hours, 9);   // 25th percentile of 5, 6, 7
        Assert.Equal(6.5, band.Hi.Single(p => p.Level == 25.5).Hours, 9);
        Assert.Equal(1, band.Med[0].Level);                                   // starts at the first curve level
        Assert.Equal(50, band.Med[^1].Level);
    }

    [Fact]
    public void SplitsAreTheMinutesEachStretchTookAndTheBestOfEachAddsUp()
    {
        // A: 1 to 50 in a straight line over 10 h. B: reaches 10 slowly, then 10 levels an hour. Splits at 10, 20, 30, 40, 46, 50.
        var a = Relevel("A", 1, 10, 4, "2026-11-03");
        var b = Relevel("B", 1, 10, 4, "2026-11-04", curve: [(0, 1), (3, 10), (4, 20), (5, 30), (6, 40), (7, 46), (8, 50)]);
        var race = JourneyRace.All([a, b], 50, Xp1);
        Assert.Equal([10, 20, 30, 40, 46, 50], race.SplitLevels);
        var ra = race.Rows.Single(r => r.Journey == 0).SplitMinutes;
        var rb = race.Rows.Single(r => r.Journey == 1).SplitMinutes;
        Assert.Equal(10.0 * 9 / 49 * 60, ra[0]!.Value, 6);
        Assert.Equal(10.0 * 10 / 49 * 60, ra[1]!.Value, 6);
        Assert.Equal(180, rb[0]!.Value, 9);
        Assert.Equal(60, rb[1]!.Value, 9);
        // Best of each: the lower of the two in every column, and their hours.
        Assert.Equal(Math.Min(ra[0]!.Value, rb[0]!.Value), race.BestOfEach[0]!.Value, 9);
        Assert.Equal(60, race.BestOfEach[1]!.Value, 9);
        Assert.Equal(race.BestOfEach.Sum(v => v!.Value) / 60, race.BestOfEachHours!.Value, 9);
        Assert.Equal(race.Rows[0].Hours - race.BestOfEachHours.Value, race.BestOfEachUnderBest!.Value, 9);
    }

    [Fact]
    public void ARelevelThatNeverReachedASplitHasNoMinutesThereAndNoBestOfEachSum()
    {
        var short1 = Relevel("S", 1, 5, 4, "2026-11-03", curve: [(0, 1), (5, 45)]);
        var race = JourneyRace.All([short1], 50, Xp1);
        Assert.Null(race.Rows[0].SplitMinutes[4]);   // 46
        Assert.Null(race.BestOfEach[4]);
        Assert.Null(race.BestOfEachHours);
    }

    [Fact]
    public void AnOpenRelevelStandsAgainstTheBestAtItsLevel()
    {
        var js = Three();
        var open = Relevel("OPEN", 2, 3, 4, "2026-11-06", done: false, levelNow: 30, curve: [(0, 1), (3, 30)]) with { Projection = [new CurvePoint(3, 30), new CurvePoint(9, 50)] };
        js.Add(open);
        var race = JourneyRace.All(js, 50, Xp2, activeIndex: 3);
        var now = race.Now!;
        Assert.Equal(30, now.Level);
        var bestAt = (10 * Xp2 / Xp0) * 29 / 49;           // AAA re-figured, a straight line to level 30
        Assert.Equal(bestAt, now.BestAt!.Value, 9);
        Assert.Equal(bestAt - 3, now.Ahead!.Value, 9);
        Assert.Equal(bestAt - 3, now.Gap!.Value, 9);
        Assert.Equal(3, now.CurveHours);
        Assert.Equal(10.0 * 29 / 49, now.BestOwn!.Value, 9);   // as played
        // The typical is the median of the three at 30: AAA, BBB (12 x 1.2 / 1.1) and CCC (14): BBB's.
        Assert.Equal(12 * Xp2 / Xp1 * 29 / 49, now.TypicalAt!.Value, 9);
        // Splits: levels reached come from the curve, later ones from the projection.
        var s10 = now.Splits.Single(s => s.Level == 10);
        Assert.False(s10.Projected);
        Assert.Equal(3.0 * 9 / 29, s10.You!.Value, 9);
        var s40 = now.Splits.Single(s => s.Level == 40);
        Assert.True(s40.Projected);
        Assert.Equal(3 + 6.0 * 10 / 20, s40.You!.Value, 9);
        Assert.Equal(s40.You!.Value - s40.Best!.Value, s40.Delta!.Value, 9);
        Assert.Equal(3.0 * 9 / 29 * 60, now.SplitMinutes[0]!.Value, 9);
    }

    /// <summary>Row 13 of docs/debt.md, kept by a faithful move: the curve ran past LevelNow, so the race reads the curve's level.</summary>
    [Fact]
    public void TheOpenRelevelsLevelIsTheCurvesLastWhenItRanPastTheLevelNow()
    {
        var js = Three();
        js.Add(Relevel("OPEN", 2, 3, 4, "2026-11-06", done: false, levelNow: 34, curve: [(0, 1), (3, 36)]));
        Assert.Equal(36, JourneyRace.All(js, 50, Xp2, activeIndex: 3).Now!.Level);
    }

    [Fact]
    public void TheNextRunsRouteIsScaledToItsXp()
    {
        var next = JourneyRace.Next(Three(), 50, currentRank: 1, nextXp: Xp2, kind: "D4", routeMinutes: 500, routeT5: 450, legMinutes: [40, 460]);
        var f = Xp2 / Xp1;
        Assert.Equal(f, next.Scale, 9);
        Assert.Equal(500 * f, next.RouteMinutes!.Value, 9);
        Assert.Equal(450 * f, next.RouteT5!.Value, 9);
        Assert.Equal([40 * f, 460 * f], next.LegMinutes);
        // To beat: the best D4 relevel, re-figured to the next run's XP.
        Assert.Equal(0, next.Race.Rows[0].Journey);
        Assert.Equal(Xp2, next.Race.Target);
        // No next run in the campaign: the XP of the trio's own rank, so the route isn't re-figured.
        Assert.Equal(1, JourneyRace.Next(Three(), 50, 1, null, "D4", 500, 450, [40, 460]).Scale);
    }
}
