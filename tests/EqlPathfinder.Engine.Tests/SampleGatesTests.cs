using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Pages;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>One named smallest sample for each purpose, and a count beside a number counts what the number used (docs/specs/sample-gates.md).</summary>
public class SampleGatesTests
{
    private static readonly DateTime T0 = new(2026, 11, 19, 18, 0, 0);
    private static int _n;

    private static Visit Farm(string id, int motes, bool finished, int minutes = 20, int kills = 20)
    {
        var v = new Visit
        {
            Id = id,
            Zone = "Kedge Keep",
            BaseZone = "Kedge Keep",
            Difficulty = 4,
            Start = T0,
            ActiveSeconds = minutes * 60,
            LevelStart = 50,
            LevelEnd = 50,
            Kills = kills,
            Completed = finished,
            CompletedAt = finished ? T0.AddMinutes(minutes) : null,
            Closed = true,
            ExitedAt = T0.AddMinutes(minutes),
        };
        v.Loot.Add(LootKind.Mote, MoteTier.Major, motes, false);
        for (int i = 1; i <= Math.Min(kills, minutes); i++) v.KillTimes.Add(T0.AddMinutes(i));
        return v;
    }

    private static LevelingSegment Seg(int level, bool rated, int minutes = 15, string place = "Kedge Keep D4", string type = "crawl") =>
        new(place, "Kedge Keep", 4, T0.AddHours(-1000 + ++_n), level, minutes, 0.2, 10, 0, 0, rated ? 60 : 5, 0, 0, 2, type, 0, null)
        { VisitId = $"v{_n}", KillLevels = 0.2, Completed = rated, Refunded = rated, Ended = true };

    [Fact]
    public void TheFarmRatesCrawlCountIsTheCrawlsItsMiddleUsed()
    {
        // Three finished crawls, and two unfinished ones with loot and under 8 kills: no rate.
        var visits = new List<Visit> { Farm("a", 10, true), Farm("b", 20, true), Farm("c", 30, true), Farm("d", 90, false, kills: 6), Farm("e", 90, false, kills: 6) };
        var info = RouteAnalysis.FarmRates(visits, new TrackerOptions());
        Assert.Equal(3, info.Crawls);
        Assert.Equal(2, info.Unrated);
        Assert.Equal(60, info.Usual!.Value, 6);
        var source = RouteAnalysis.FarmSource(info, new TrackerOptions { FarmRateMode = "usual" });
        Assert.Equal(("usual", 3), (source.Kind, source.Crawls));
    }

    [Fact]
    public void AZoneNeedsThreeCrawlsWithARateToBeTheBest()
    {
        var visits = new List<Visit> { Farm("a", 10, true), Farm("b", 20, true), Farm("c", 90, false, kills: 6) };
        var info = RouteAnalysis.FarmRates(visits, new TrackerOptions());
        Assert.Null(info.Best);
        Assert.Equal(2, info.Crawls);
        var source = RouteAnalysis.FarmSource(info, new TrackerOptions());
        Assert.Equal(("default", 2, 1, true), (source.Kind, source.Crawls, source.Unrated, source.Fallback));
    }

    [Fact]
    public void CrawlsAllWithoutARateAreCountedApartNotAsNone()
    {
        var info = RouteAnalysis.FarmRates([Farm("a", 90, false, kills: 6), Farm("b", 90, false, kills: 6)], new TrackerOptions());
        Assert.Equal((0, 2), (info.Crawls, info.Unrated));
        Assert.Null(info.Usual);
    }

    [Fact]
    public void TheCapsFarmRowsCountOnlyCrawlsWithARateAndLeftOutCountsTheRest()
    {
        var segs = new List<LevelingSegment> { Seg(50, true), Seg(50, true), Seg(50, false), Seg(50, false), Seg(30, false) };
        var d = AskData.From(segs, RouteAnalysis.FarmPatch, T0, onlyWithRate: true);
        Assert.Equal(2, d.Bands.Single(b => b.Band == "50" && !b.SincePatch).Crawls);
        Assert.Equal(3, d.NoRate);   // the cap counts too
        Assert.Equal(2, d.UnratedAt.Count(u => u.Level >= 50));
        Assert.Equal(2, d.Visits);
    }

    [Fact]
    public void TheFarmAnswerLeftOutCountsCrawlsNotOpenWorldHuntsOrOtherInstances()
    {
        var segs = new List<LevelingSegment> { Seg(50, true), Seg(50, false), Seg(50, false, type: "open world"), Seg(50, false, type: "instance") };
        T(segs, "farm", 50, a => Assert.Equal(1, a.NoRateLeftOut));
    }

    [Fact]
    public void ALevelingAnswerLeftOutCountsOnlyItsOwnLevelRange()
    {
        var segs = new List<LevelingSegment> { Seg(10, true), Seg(10, false), Seg(12, false), Seg(40, false), Seg(41, false), Seg(42, false) };
        AskAnswer Level(int from, int to) => AskPage.Build(new AskQuery("level", from, to, null, null, "all", null), new AskInputs(segs, 0, 0, new TrackerOptions(), 10, null, 2, null, T0));
        Assert.Equal(2, Level(5, 15).NoRateLeftOut);
        Assert.Equal(3, Level(40, 45).NoRateLeftOut);
    }

    [Fact]
    public void TheFarmAnswerLeftOutCountsOnlyItsLevelAndTheLevelingAnswerCountsRatedVisits()
    {
        var segs = new List<LevelingSegment> { Seg(50, true), Seg(50, false), Seg(50, false), Seg(30, false), Seg(30, true) };
        T(segs, "farm", 50, a => { Assert.Equal(2, a.NoRateLeftOut); Assert.Equal(2, a.Sources.Instances); });
    }

    [Fact]
    public void AFarmAnswerWhoseCrawlsAllLackARateSaysSoRatherThanNone()
    {
        var segs = new List<LevelingSegment> { Seg(50, false), Seg(50, false), Seg(50, false), Seg(50, false) };
        T(segs, "farm", 50, a => { Assert.Equal(0, a.Farm.Crawls); Assert.Equal(4, a.Farm.Unrated); });
    }

    private static void T(List<LevelingSegment> segs, string q, int at, Action<AskAnswer> check) =>
        check(AskPage.Build(new AskQuery(q, null, null, null, at, "all", null), new AskInputs(segs, 0, 0, new TrackerOptions(), at, null, 2, null, T0)));
}
