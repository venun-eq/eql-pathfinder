using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Live;

namespace EqlPathfinder.Engine.Tests;

/// <summary>The overlay's NEXT options as pure code (docs/specs/next-ranking.md): Ask's data in, the places a player is offered out.</summary>
public class NextRankingTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 18, 0, 0);
    private static int _n;

    // One crawl at a level: minutes active for 0.2 levels, t5 looted. Two visits make a place offerable.
    private static LevelingSegment Seg(string place, int level, double minutes, double t5, bool sincePatch = false) =>
        new(place, "Kedge Keep", int.Parse(place[^1..]), sincePatch ? T0.AddHours(++_n) : T0.AddHours(-1000 + ++_n), level, minutes, 0.2, t5, 0, 0, 60, 0, 0, 2, "crawl",
            0, null)
        { VisitId = $"v{++_n}", KillLevels = 0.2, Completed = true, Refunded = true, Ended = true };

    private static IEnumerable<LevelingSegment> Visits(string place, int level, double minutes, double t5, int n = 2, bool sincePatch = false) =>
        Enumerable.Range(0, n).Select(_ => Seg(place, level, minutes, t5, sincePatch));

    private static AskData Data(params IEnumerable<LevelingSegment>[] places) => AskData.From(places.SelectMany(p => p), T0, onlyWithRate: true);

    [Fact]
    public void ByLevelingSpeedTheFastestPlaceIsFirstAndTwoAreOffered()
    {
        var d = Data(Visits("Kedge Keep D1", 40, 10, 0), Visits("Kedge Keep D2", 40, 30, 0), Visits("Kedge Keep D3", 40, 60, 0));
        var o = NextRanking.Leveling(d, 40, 50, motes: false, farmRate: 100, xp: 1, lastLabel: "");
        Assert.Equal(["Kedge Keep D1", "Kedge Keep D2"], o.Select(x => x.Label));
        Assert.True(o[0].LevelsPerHour > o[1].LevelsPerHour);
        Assert.All(o, x => Assert.Null(x.FromLevel));
    }

    [Fact]
    public void ByMotesThePlacesThatCostLeastComeFirstEvenWhenSlower()
    {
        // Three places: D1 quick but loots nothing, D3 slower and loots some, D2 slowest and loots more than the farm rate.
        // Ranked by cost D2 < D3 < D1; ranked by speed the second place would be D1. The route's first place may be moved
        // to the front, so the second slot is what shows the sort.
        var d = Data(Visits("Kedge Keep D1", 40, 10, 0), Visits("Kedge Keep D3", 40, 20, 60), Visits("Kedge Keep D2", 40, 30, 120));
        var o = NextRanking.Leveling(d, 40, 50, motes: true, farmRate: 100, xp: 1, lastLabel: "");
        Assert.Equal(["Kedge Keep D2", "Kedge Keep D3"], o.Take(2).Select(x => x.Label));
        Assert.True(o[0].Cost < o[1].Cost);
    }

    [Fact]
    public void ANullCostIsNeverOfferedAheadOfAPricedPlaceByMotes()
    {
        // D2 is so slow that leveling there is too slow to price (cost null): it is last, whatever its loot, so the two
        // offered are D1 and D3. A null that sorted first would take the second slot.
        var d = Data(Visits("Kedge Keep D1", 40, 10, 0), Visits("Kedge Keep D3", 40, 20, 0), Visits("Kedge Keep D2", 40, 5000, 500));
        var o = NextRanking.Leveling(d, 40, 50, motes: true, farmRate: 100, xp: 1, lastLabel: "");
        Assert.Equal(["Kedge Keep D1", "Kedge Keep D3"], o.Take(2).Select(x => x.Label));
        Assert.All(o.Take(2), x => Assert.NotNull(x.Cost));
    }

    [Fact]
    public void TheRouteAskWouldTakeStartsTheOptionsEvenWhenAnotherPlaceIsFaster()
    {
        // A (D1) is fastest at 40 but never played at 41, so a route through it is thin and pays a switch; B (D2) is a little
        // slower and played at both levels. Ask's route starts at B, and so does the list, though the sort alone puts A first.
        var d = Data(Visits("Kedge Keep D1", 40, 10, 0), Visits("Kedge Keep D2", 40, 10.5, 0), Visits("Kedge Keep D2", 41, 10.5, 0));
        var o = NextRanking.Leveling(d, 40, 50, motes: false, farmRate: 100, xp: 1, lastLabel: "");
        Assert.Equal("Kedge Keep D2", o[0].Label);
        Assert.Equal("Kedge Keep D1", o[1].Label);
    }

    [Fact]
    public void LevelsAnHourAreForTheXpYourTrioNeeds()
    {
        var d = Data(Visits("Kedge Keep D1", 40, 10, 0));
        var one = NextRanking.Leveling(d, 40, 50, false, 100, xp: 1, lastLabel: "").Single();
        var two = NextRanking.Leveling(d, 40, 50, false, 100, xp: 2, lastLabel: "").Single();
        Assert.Equal(one.LevelsPerHour / 2, two.LevelsPerHour, 6);
    }

    [Fact]
    public void APlaceWithOneVisitIsNotOfferedAndTheLastPlaceIsMarkedAgain()
    {
        var d = Data(Visits("Kedge Keep D1", 40, 10, 0), Visits("Kedge Keep D2", 40, 5, 0, n: 1));
        var o = NextRanking.Leveling(d, 40, 50, false, 100, 1, lastLabel: "Kedge Keep D1");
        var only = Assert.Single(o);
        Assert.Equal("Kedge Keep D1", only.Label);
        Assert.True(only.Again);
    }

    [Fact]
    public void ACrawlsBehindANumberAreCountedAsTheVisits()
    {
        var d = Data(Visits("Kedge Keep D1", 40, 10, 0, n: 4));
        Assert.Equal(4, NextRanking.Leveling(d, 40, 50, false, 100, 1, "").Single().Crawls);
    }

    [Fact]
    public void APlaceClosedAtTheLevelIsNotOffered()
    {
        // Plane of Hate opens at 46: played at 46, it isn't offered to a 45 that sees it through the window.
        var d = Data(Visits("Kedge Keep D1", 45, 30, 0), Visits("Plane of Hate D1", 46, 5, 0));
        var at45 = NextRanking.Leveling(d, 45, 50, false, 100, 1, "");
        Assert.Equal(["Kedge Keep D1"], at45.Where(x => x.FromLevel == null).Select(x => x.Label));   // the gate opens within 2 levels: only as "from 46"
        Assert.Equal(46, at45.Single(x => x.Label == "Plane of Hate D1").FromLevel);
        Assert.Contains(NextRanking.Leveling(d, 46, 50, false, 100, 1, ""), x => x.Label == "Plane of Hate D1" && x.FromLevel == null);
    }

    [Fact]
    public void NothingIsOfferedWhereNoPlaceWasPlayed()
    {
        Assert.Empty(NextRanking.Leveling(Data(Visits("Kedge Keep D1", 10, 10, 0)), 40, 50, false, 100, 1, ""));
    }

    [Fact]
    public void FarmingOffersTheBestTwoSincethePatchWithThreeCrawls()
    {
        var d = Data(Visits("Kedge Keep D4", 50, 60, 200, n: 3, sincePatch: true), Visits("Kedge Keep D3", 50, 60, 100, n: 3, sincePatch: true),
            Visits("Kedge Keep D2", 50, 60, 400, n: 2, sincePatch: true), Visits("Kedge Keep D1", 50, 60, 50, n: 3, sincePatch: true));
        var o = NextRanking.Farms(d, 50, 1, lastLabel: "Kedge Keep D3");
        Assert.Equal(["Kedge Keep D4", "Kedge Keep D3"], o.Select(x => x.Label));   // D2 has too few crawls
        Assert.All(o, x => Assert.Null(x.Cost));
        Assert.True(o[1].Again);
        Assert.False(o[0].Again);
    }

    [Fact]
    public void FarmingWithNoneSincePatchOffersTheBestFromBefore()
    {
        var d = Data(Visits("Kedge Keep D4", 50, 60, 200, n: 3), Visits("Kedge Keep D3", 50, 60, 100, n: 3));
        Assert.Equal(["Kedge Keep D4", "Kedge Keep D3"], NextRanking.Farms(d, 50, 1, "").Select(x => x.Label));
    }
}
