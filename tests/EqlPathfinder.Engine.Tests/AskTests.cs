using EqlPathfinder.Engine.Analysis;

namespace EqlPathfinder.Engine.Tests;

public class AskTests
{
    private static readonly DateTime Patch = new(2026, 11, 10, 12, 0, 0);
    private static readonly DateTime After = Patch.AddDays(2), Before = Patch.AddDays(-2);

    private static LevelingSegment V(string zone, int d, int level, double minutes, double levels, double t5 = 0, DateTime? start = null,
        int kills = 40, int deaths = 0, double dps = 200, int fragments = 0, int rank = 0) =>
        new($"{zone} D{d}", zone, d, start ?? After, level, minutes, levels, t5, 0, fragments, kills, deaths, dps, rank);

    [Fact]
    public void OlderCrawlsFadeWithAThirtyDayHalfLife()
    {
        // 60 days old counts a quarter: (0.25 × 16 + 1 × 8) / 1.25 = 9.6 minutes a level.
        var d = AskData.From([V("Estate of Unrest", 4, 20, 16, 1, start: After.AddDays(-60)), V("Estate of Unrest", 4, 20, 8, 1)], Patch, After);
        var p = d.Places.Single();
        Assert.Equal(9.6, p.At(20));
        Assert.Equal(2, p.CrawlsAt(20));
        Assert.Equal(After, p.LastAt(20));
        // A place with only old crawls keeps its own numbers: weights only matter between crawls.
        var old = AskData.From([V("Najena", 4, 22, 30, 1, start: After.AddDays(-90))], Patch, After);
        Assert.Equal(30, old.Places.Single().At(22));
    }

    [Fact]
    public void TimesCompareAtRankZeroAndAnswerForTheTriosRank()
    {
        // Rank 1 needs 10% more XP: 11 minutes a level then is 10 at rank 0, and 12 for a rank-2 trio.
        var d = AskData.From([V("Estate of Unrest", 4, 20, 22, 2, rank: 1), V("Estate of Unrest", 4, 20, 22, 2, rank: 1)], Patch, After);
        Assert.Equal(10, d.Places.Single().At(20));
        var r = Ask.Chain(d, 20, 22, countMotes: false, farmRate: 50, xpMultiplier: Planner.XpMultiplier(2));
        Assert.Equal(24, r.Minutes, 1);
        Assert.Equal(After, Assert.Single(r.Legs).LastRun);
        var band = d.Bands.Single(b => !b.SincePatch);
        Assert.Equal(6, band.LevelsPerHour, 2);   // 2 levels in 22 minutes at rank 1 = 6 an hour at rank 0
    }

    /// <summary>Unrest D4 at 8 min a level for 20–22, Mistmoore D4 at 10 for 23–26, Old Guk D4 at 16 for 20–26 (rich in motes).</summary>
    private static AskData Data(params LevelingSegment[] extra) => AskData.From(new[]
    {
        V("Estate of Unrest", 4, 20, 24, 3), V("Estate of Unrest", 4, 20, 24, 3),
        V("Castle of Mistmoore", 4, 23, 40, 4), V("Castle of Mistmoore", 4, 23, 40, 4),
        V("Ruins of Old Guk", 4, 20, 112, 7, t5: 112), V("Ruins of Old Guk", 4, 20, 112, 7, t5: 112),
    }.Concat(extra), Patch);

    /// <summary>The players who share: each place at <c>Minutes</c> a level from <c>From</c> up to <c>To</c>, 6 visits a level.</summary>
    private static AskData Pool(params (string Place, int From, int To, double Minutes, double Kills)[] places) => new()
    {
        Places = places.Select(x =>
        {
            var m = new double?[49]; var t5 = new double[49]; var n = new int[49]; var last = new DateTime?[49];
            var kills = new double[49]; var zero = new double[49];
            for (int L = x.From; L < x.To; L++) (m[L - 1], n[L - 1], last[L - 1], kills[L - 1]) = (x.Minutes, 6, After, x.Kills);
            return new PlaceLevels(x.Place, m, t5, n, last) { Extras = new LevelExtras(zero, zero, zero, zero, kills, zero) };
        }).ToList(),
        Bands = [],
        Difficulties = [],
        Published = true,
    };

    [Fact]
    public void YourVisitsCountMoreThanThePlayersTheMoreYouHave()
    {
        // You: 8 minutes a level at Unrest over 4 visits. The players who share: 12. Theirs counts as one visit: 80% yours.
        var four = AskData.From(Enumerable.Repeat(V("Estate of Unrest", 4, 20, 24, 3), 4), Patch);
        var pool = Pool(("Estate of Unrest D4", 20, 23, 12, 2));
        var leg = Assert.Single(Ask.Chain(four, 20, 22, countMotes: false, farmRate: 50, pool: pool).Legs);
        Assert.Equal(2 * (0.8 * 8 + 0.2 * 12), leg.Minutes, 6);
        Assert.Equal((16.0, 24.0, 0.8), (leg.Mine!.Value, leg.Theirs!.Value, leg.MineShare));
        // One visit: half yours. Without their numbers: all yours, as before.
        var one = AskData.From([V("Estate of Unrest", 4, 20, 24, 3)], Patch);
        Assert.Equal(2 * 10, Ask.Chain(one, 20, 22, countMotes: false, farmRate: 50, pool: pool).Minutes, 6);
        var alone = Assert.Single(Ask.Chain(four, 20, 22, countMotes: false, farmRate: 50).Legs);
        Assert.Equal((16.0, (double?)null, 1.0), (alone.Minutes, alone.Theirs, alone.MineShare));
    }

    [Fact]
    public void APlaceOnlyThePlayersHavePlayedJoinsTheRouteAtTheirTime()
    {
        var d = AskData.From(Enumerable.Repeat(V("Estate of Unrest", 4, 20, 30, 3), 4), Patch);   // 10 minutes a level
        Assert.Equal("Estate of Unrest D4", Assert.Single(Ask.Chain(d, 20, 23, countMotes: false, farmRate: 50).Legs).Place);
        var leg = Assert.Single(Ask.Chain(d, 20, 23, countMotes: false, farmRate: 50, pool: Pool(("Najena D4", 20, 23, 6, 2))).Legs);
        Assert.Equal(("Najena D4", 18.0, 0, 0.0), (leg.Place, leg.Minutes, leg.MaxCrawls, leg.MineShare));
        Assert.Null(leg.Mine);
        Assert.Null(leg.LastRun);
    }

    [Fact]
    public void YouAgainstThePlayersSaysWhereYoureSlowestAndWhetherItsKillSpeed()
    {
        // Unrest: 12 minutes a level and 2 kills a minute, theirs 10 and 2.5. Najena: 6 against their 8. Old Guk: one visit.
        var d = AskData.From([
            V("Estate of Unrest", 4, 20, 36, 3, kills: 72), V("Estate of Unrest", 4, 20, 36, 3, kills: 72),
            V("Najena", 4, 20, 18, 3, kills: 54), V("Najena", 4, 20, 18, 3, kills: 54),
            V("Ruins of Old Guk", 4, 20, 60, 3)], Patch);
        var pool = Pool(("Estate of Unrest D4", 20, 23, 10, 2.5), ("Najena D4", 20, 23, 8, 3), ("Ruins of Old Guk D4", 20, 23, 15, 2));
        var v = Ask.Against(d, pool, 20, 23);
        Assert.Equal(["Estate of Unrest D4", "Najena D4"], v.Select(x => x.Place));
        Assert.Equal(new Ask.Versus("Estate of Unrest D4", 20, 23, 3, 12, 10, 2, 2, 2.5), v[0]);
        Assert.Equal(0.2, v[0].Gap, 6);
        Assert.Equal(-0.25, v[1].Gap, 6);
        // At the XP a rank-2 trio needs, both sides' minutes grow alike.
        Assert.Equal((14.4, 12.0), (Ask.Against(d, pool, 20, 23, 1.2)[0].Mine, Ask.Against(d, pool, 20, 23, 1.2)[0].Theirs));
    }

    [Fact]
    public void MinutesPerLevelSpreadEachVisitOverTheLevelsItCovered()
    {
        var d = Data();
        var unrest = d.Places.Single(p => p.Place == "Estate of Unrest D4");
        Assert.Equal(8, unrest.At(20));
        Assert.Equal(8, unrest.At(22));
        Assert.Null(unrest.At(23));
        Assert.Equal(2, unrest.CrawlsAt(21));
        Assert.Equal(16, d.Places.Single(p => p.Place == "Ruins of Old Guk D4").At(26));
    }

    [Fact]
    public void TheFastestRouteTakesTheQuickestPlaceAtEachLevelAndPaysForTheChange()
    {
        var r = Ask.Chain(Data(), 20, 27, countMotes: false, farmRate: 50);
        Assert.Equal(["Estate of Unrest D4", "Castle of Mistmoore D4"], r.Legs.Select(l => l.Place));
        Assert.Equal((20, 23), (r.Legs[0].From, r.Legs[0].To));
        Assert.Equal((23, 27), (r.Legs[1].From, r.Legs[1].To));
        Assert.Equal(24 + 40 + Ask.SwitchMinutes, r.Minutes);
        Assert.Empty(r.Gaps);
        var alts = Ask.Alternatives(Data(), 20, 27, r);
        var guk = Assert.Single(alts, a => a.Whole);
        Assert.Equal(("Ruins of Old Guk D4", 112.0), (guk.Place, guk.Minutes));
    }

    [Fact]
    public void CountingMotesCanChooseASlowerPlaceThatOutFarmsYourFarm()
    {
        // Old Guk loots 60 T5-eq/h against a farm rate of 50, so every minute there earns more than farming would.
        var r = Ask.Chain(Data(), 20, 27, countMotes: true, farmRate: 50);
        var leg = Assert.Single(r.Legs);
        Assert.Equal("Ruins of Old Guk D4", leg.Place);
        Assert.Equal(112, r.Minutes);
        Assert.Equal(112, r.T5, 1);
    }

    [Fact]
    public void ASingleLuckyCrawlDoesNotWinOnItsOwn()
    {
        // 7.5 min a level from one visit loses to 8 from two: one-visit levels count 2 minutes slower.
        var r = Ask.Chain(Data(V("Najena", 4, 20, 22.5, 3)), 20, 23, countMotes: false, farmRate: 50);
        Assert.Equal("Estate of Unrest D4", Assert.Single(r.Legs).Place);
    }

    [Fact]
    public void OneCrawlThatStartedAtLevelOneDoesNotDecideTheRoute()
    {
        // D4 was run once from level 1 (slow); D1 covers 2–11 faster but never level 1. Before, every 1–11 route had to
        // start in D4 and then stayed there to save the change of place. Now D1 stands in for level 1 from level 2.
        var d = AskData.From([
            V("Befallen", 4, 1, 16, 4), V("Befallen", 4, 5, 40, 7),
            V("Befallen", 1, 2, 21.6, 9), V("Befallen", 1, 2, 21.6, 9),
        ], Patch, After);
        var r = Ask.Chain(d, 1, 11, countMotes: false, farmRate: 50);
        var leg = Assert.Single(r.Legs);
        Assert.Equal(("Befallen D1", 1, 11), (leg.Place, leg.From, leg.To));
    }

    [Fact]
    public void LevelsNobodyCoveredAreGapsNotGuesses()
    {
        // Mistmoore's crawls end at 26; level 27 borrows 26's time (one level of reach), 28 and 29 stay gaps.
        var r = Ask.Chain(Data(), 25, 30, countMotes: false, farmRate: 50);
        Assert.Equal([28, 29], r.Gaps);
        Assert.Equal(28, r.Legs[^1].To);
    }

    [Fact]
    public void FarmsSinceThePatchLeaveOutOlderCrawls()
    {
        var d = Data(V("Plane of Hate", 4, 50, 60, 0, t5: 45), V("Plane of Hate", 4, 50, 60, 0, t5: 45),
            V("Plane of Fear", 4, 50, 60, 0, t5: 60, start: Before), V("Plane of Fear", 4, 50, 60, 0, t5: 60, start: Before));
        Assert.Equal(["Plane of Hate D4"], Ask.Farms(d, 50, sincePatch: true).Select(f => f.Place));
        var all = Ask.Farms(d, 50, sincePatch: false);
        Assert.Equal(["Plane of Fear D4", "Plane of Hate D4"], all.Select(f => f.Place));
        Assert.Equal(60, all[0].T5PerHour);
        Assert.Equal(2, all[0].Crawls);
    }

    [Fact]
    public void WhereAndWhoWithNarrowThePlayAndTheOpenWorldIsAPlace()
    {
        var segs = new[]
        {
            V("Befallen", 4, 10, 30, 3) with { PartyShare = 0.1 },
            V("Befallen", 4, 10, 40, 3) with { PartyShare = 0.8 },
            LevelingSegment.OpenWorld(new FieldStretch(After, After.AddMinutes(20), 21, 2, 40, 30, "The Ruins of Old Guk", 10), 0, null, 5, 0, 0, 0),
        };
        Assert.Equal(2, LevelingSegment.Filter(segs, "instances", null).Count());
        var open = Assert.Single(LevelingSegment.Filter(segs, "open", null));
        Assert.Equal(("Ruins of Old Guk · open world", "open world"), (open.Place, open.Type));
        Assert.True(open.Grouped);   // 30 of its 40 kills gave party experience
        Assert.Equal(2, LevelingSegment.Filter(segs, null, "group").Count());
        Assert.Single(LevelingSegment.Filter(segs, "instances", "solo"));
        // A hunt is a place like any other: 21 minutes for 2 levels.
        var d = AskData.From(segs.Where(s => s.Type == "open world"), Patch, After);
        Assert.Equal(10.5, d.Places.Single().At(10));
    }

    [Fact]
    public void OneShortVisitIsNotAFarm()
    {
        // A 6-minute visit at 41 T5-eq/h said nothing about a farm, yet it used to rank first.
        var d = Data(V("Plane of Hate - Solo", 4, 50, 6, 0, t5: 4.1), V("Plane of Hate", 4, 50, 60, 0, t5: 30), V("Plane of Hate", 4, 50, 60, 0, t5: 30));
        Assert.Equal(["Plane of Hate D4"], Ask.Farms(d, 50, sincePatch: true).Select(f => f.Place));
        Assert.Empty(Ask.Farms(Data(V("Plane of Hate - Solo", 4, 50, 6, 0, t5: 4.1)), 50, sincePatch: true));
    }

    [Fact]
    public void AFarmTooSmallToRankStillCounts()
    {
        // One 7-minute crawl at 50 can't rank, yet Home said "You haven't farmed at 50 since the patch" beside "Your usual
        // at 50 since the patch: 8.0, 1 crawl". The count lets the pages say there's too little yet.
        var one = V("Plane of Hate - Solo", 4, 50, 7, 0, t5: 1);
        Assert.Empty(Ask.Farms(Data(one), 50, sincePatch: true));
        Assert.Equal(1, Ask.FarmCrawls(Data(one), 50, sincePatch: true));
        Assert.Equal(0, Ask.FarmCrawls(Data(), 50, sincePatch: true));

        // The farm answer carries it, with what a place needs to rank.
        var a = EqlPathfinder.Engine.Pages.AskPage.Build(new AskQuery("farm", null, null, null, 50, null, null),
            new EqlPathfinder.Engine.Pages.AskInputs([one], 0, 0, new EqlPathfinder.Engine.Tracking.TrackerOptions(), 50, null, 0, null, After));
        Assert.Empty(a.Farm.Rows);
        Assert.Equal((1, Ask.MinCrawls, Ask.MinHours), (a.Farm.Crawls, a.Farm.MinCrawls, a.Farm.MinHours));
    }

    [Fact]
    public void DifficultiesSideBySideKeepSpeedAndMotesApart()
    {
        var d = Data(V("Kedge Keep", 1, 41, 30, 2, t5: 2, kills: 120), V("Kedge Keep", 1, 42, 30, 2, t5: 2, kills: 120),
            V("Kedge Keep", 4, 41, 60, 2.5, t5: 30, kills: 90), V("Kedge Keep", 4, 43, 60, 2.5, t5: 30, kills: 90));
        var c = Ask.NearestDifficulty(d, 44)!;
        Assert.Equal(("Kedge Keep", 41, 45), (c.Zone, c.From, c.To));
        Assert.Equal([1, 4], c.Columns.Select(x => x.Difficulty));
        Assert.Equal(4, c.Columns[0].LevelsPerHour);
        Assert.Equal(2.5, c.Columns[1].LevelsPerHour);
        Assert.Equal(30, c.Columns[1].T5PerHour);
        Assert.Equal(4, c.Columns[0].KillsPerMinute);
    }

    [Fact]
    public void TheGapAgainstThePlayersSplitsIntoKillSpeedAndXpPerKillInTheEngine()
    {
        // Unrest: 12 minutes a level at 2 kills a minute, theirs 10 at 2.5: 20% slower, all of it kill speed (each kill
        // fills 1 - 25/24 = -4% less, so none). Najena is quicker, so nothing is said. Both places have 3 levels, so the
        // overall gap is the geometric mean of 1.2 and 0.75, less one.
        var d = AskData.From([
            V("Estate of Unrest", 4, 20, 36, 3, kills: 72), V("Estate of Unrest", 4, 20, 36, 3, kills: 72),
            V("Najena", 4, 20, 18, 3, kills: 54), V("Najena", 4, 20, 18, 3, kills: 54)], Patch);
        var pool = Pool(("Estate of Unrest D4", 20, 23, 10, 2.5), ("Najena D4", 20, 23, 8, 3));
        var v = Ask.Against(d, pool, 20, 23);
        Assert.Equal(0.2, v[0].KillGap, 6);
        Assert.Equal(1 - 25.0 / 24, v[0].PerKillGap, 6);
        Assert.Equal("kill", v[0].Why);
        Assert.Equal("", v[1].Why);
        Assert.Equal(Math.Sqrt(1.2 * 0.75) - 1, Ask.VersusOverall(v), 6);
    }

    [Fact]
    public void BothCausesAreNamedWhenEachIsAtLeastFivePercent()
    {
        // 30% slower; kills 20% slower; each kill fills 1 - (10 * 2.5) / (13 * 2) = 3.8% less: kill speed alone. With theirs at
        // 2.5 and ours 2.2 at 13 minutes: kill 12%, per kill 1 - 25/28.6 = 12.6%: both.
        var d = AskData.From([V("Estate of Unrest", 4, 20, 39, 3, kills: 86), V("Estate of Unrest", 4, 20, 39, 3, kills: 86)], Patch);
        var v = Ask.Against(d, Pool(("Estate of Unrest D4", 20, 23, 10, 2.5)), 20, 23)[0];
        Assert.Equal("both", v.Why);
        Assert.Equal(1 - 2.205 / 2.5, v.KillGap, 2);
        var xp = Ask.Against(AskData.From([V("Estate of Unrest", 4, 20, 39, 3, kills: 98), V("Estate of Unrest", 4, 20, 39, 3, kills: 98)], Patch),
            Pool(("Estate of Unrest D4", 20, 23, 10, 2.5)), 20, 23)[0];
        Assert.Equal("xp", xp.Why);   // as fast a killer (2.5 a minute) taking 30% longer: each kill fills 23% less
    }
}
