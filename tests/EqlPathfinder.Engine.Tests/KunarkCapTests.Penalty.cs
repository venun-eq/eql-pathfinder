using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

// kunark-cap, part B: the XP penalty by level, the hours to go, Ask's levels past 50 (docs/specs/kunark-cap.md, decisions 10-15).
public partial class KunarkCapTests
{
    static string Improved(DateTime t, int rank) => L(t, $"You have improved Class Reincarnation: Paladin {rank} at a cost of 0 ability points.");

    static IEnumerable<string> PaladinAt(DateTime t, int level) =>
    [
        L(t, "You have entered West Karana."),
        L(t.AddSeconds(10), $"[{level} PAL/MNK/SHM] Venun (Ogre) ZONE: West Karana"),
    ];

    [Fact]
    public void TheXpPenaltyCountsOnlyReincarnationsMadeAfterReachingTheLevel()
    {
        // Three reincarnations made at 50 before 12-08: +30% to level 50 (the bars of levels 1 to 49), nothing for the new levels.
        var t = Before.AddHours(12);
        string[] three =
        [
            .. PaladinAt(Before.AddHours(9), 49), .. Ding(Before.AddHours(10), 50),
            L(t, "You have gained the ability \"Class Reincarnation: Paladin\" at a cost of 0 ability points."),
            Improved(t.AddMinutes(1), 2), Improved(t.AddMinutes(2), 3),
        ];
        var (e, _) = Run(new TrackerOptions(), three);
        var pal = e.State.Roster.Classes["PAL"];
        Assert.Equal([50, 50, 50], pal.SeenLevels);
        Assert.Equal((3, 3, 0, 0), (pal.RankAt(1, 50), pal.RankAt(49, 50), pal.RankAt(50, 50), pal.RankAt(54, 50)));

        // Reaching 55 after the launch and reincarnating again: +40% to level 50, +10% for 51 to 55.
        var (e2, _) = Run(new TrackerOptions(),
        [
            .. three,
            .. PaladinAt(After.AddHours(9), 1),
            .. Enumerable.Range(2, 54).SelectMany(l => Ding(After.AddHours(9).AddMinutes(l), l)),
            Improved(After.AddHours(12), 4),
        ]);
        var p2 = e2.State.Roster.Classes["PAL"];
        Assert.Equal([50, 50, 50, 55], p2.SeenLevels);
        Assert.Equal((4, 4, 1, 1, 0), (p2.RankAt(1, 50), p2.RankAt(49, 50), p2.RankAt(50, 50), p2.RankAt(54, 50), p2.RankAt(55, 50)));
        Assert.Equal(1.4, Planner.XpMultiplier(p2.RankAt(49, 50)), 6);
        Assert.Equal(1.1, Planner.XpMultiplier(p2.RankAt(52, 50)), 6);

        // The relevel that started before the fourth: the fourth is not in it.
        Assert.Equal(3, p2.RankAt(49, 50, since: After.AddHours(8)));

        // A trio counts its highest class at each level: A has three old, B has one made after 55.
        var roster = new Roster();
        roster.Classes["A"] = new Incarnation { Name = "A", Rank = 3, Seen = [Before, Before, Before], SeenLevels = [50, 50, 50] };
        roster.Classes["B"] = new Incarnation { Name = "B", Rank = 1, Seen = [After], SeenLevels = [55] };
        Assert.Equal((3, 1, 1), (roster.TrioRankAt(["A", "B"], 49, 50), roster.TrioRankAt(["A", "B"], 50, 50), roster.TrioRankAt(["A", "B"], 54, 50)));

        // A count typed after 12-08 counts to 50 only: it can't say how many came after 55.
        var typed = new Incarnation { Name = "A", Rank = 3, EnteredAsOf = After };
        Assert.Equal((3, 0), (typed.RankAt(49, 50), typed.RankAt(50, 50)));

        // The number: a rank-3 trio's crawl at 52 is in Ask's minutes as played, not divided by 1.3.
        var seg = new LevelingSegment("Befallen D4", "Befallen", 4, After.AddHours(14), 52, 30, 1, 5, 0, 0, 20, 0, 0, 3)
        {
            RankAtLevel = L => roster.TrioRankAt(["A"], L, 50),
        };
        var low = seg with { LevelStart = 20 };
        var d = AskData.From([seg, low], new DateTime(2026, 11, 10), After.AddDays(1), drift: false);
        var place = Assert.Single(d.Places);
        Assert.Equal(30, place.At(52));                        // as played: no penalty past 50
        Assert.Equal(Math.Round(30 / 1.3, 1), place.At(20));   // below 50 it is divided out, as always
    }

    [Fact]
    public void AskSizesItsLevelsFromTheListAndAVisitAt50AfterKunarkIsNotFarming()
    {
        Assert.Equal(Expansions.MaxCap(Expansions.All) - 1, AskData.BarCount);
        LevelingSegment At(int level, DateTime day, int cap) =>
            new("Befallen D4", "Befallen", 4, day.AddHours(14), level, 30, 0.2, 5, 0, 0, 20, 0, 0, 0) { Cap = cap };
        var d = AskData.From([At(50, Before, 50), At(50, After, 55), At(55, After.AddDays(1), 55)], new DateTime(2026, 11, 10), After.AddDays(2), drift: false);
        var place = Assert.Single(d.Places);
        Assert.Equal(AskData.BarCount, place.Minutes.Length);
        // The visit at 50 on 12-05 and the one at the cap of 55 are cap play (band "50"); the one at 50 on 12-09 is leveling.
        Assert.Equal(2, Assert.Single(d.Bands, b => b.Band == "50" && !b.SincePatch).Crawls);
    }

    static PathfinderEngine TwoRelevels(DateTime first, DateTime second)
    {
        string[] Batch(DateTime t) =>
        [
            L(t, "You have gained the ability \"Class Reincarnation: Paladin\" at a cost of 0 ability points."),
            L(t, "You have gained the ability \"Class Reincarnation: Monk\" at a cost of 0 ability points."),
            L(t, "You have gained the ability \"Class Reincarnation: Shaman\" at a cost of 0 ability points."),
        ];
        var (e, _) = Run(new TrackerOptions(), [.. Batch(first), .. Batch(second)]);
        return e;
    }

    static Visit CrawlOf(DateTime start, int from, int to)
    {
        var v = V(start, from, to);
        v.KillPercent = (to - from) * 100;
        v.Kills = 40;
        return v;
    }

    static Journey Active(DateTime finishedAt, int finishedTo, DateTime activeAt, int activeTo)
    {
        var e = TwoRelevels(finishedAt.AddDays(-2), finishedAt.AddDays(1));
        var js = Journeys.Build([CrawlOf(finishedAt, 1, finishedTo), CrawlOf(activeAt, 1, activeTo)], e.State, new TrackerOptions());
        return js.Single(j => !j.Done);
    }

    [Fact]
    public void TheEtaStopsAtTheHighestLevelAnyonePlayed()
    {
        // The finished relevel's crawl gained 49 levels in half an hour: 0.5 / 49 h a level.
        double perLevel = 0.5 / 49;
        var finished = new DateTime(2026, 12, 3, 14, 0, 0);
        var before = Active(finished, 50, new DateTime(2026, 12, 6, 14, 0, 0), 32);   // as today
        Assert.Equal(50, before.EtaTo);
        Assert.Equal(18 * perLevel, before.EtaHours!.Value, 4);

        // On 12-09 with relevels finished at 50: levels 32 to 49 have hours, 50 up has none, and the projection ends at 50.
        var after = Active(finished, 50, After.AddHours(14), 32);
        Assert.Equal(50, after.EtaTo);
        Assert.Equal(18 * perLevel, after.EtaHours!.Value, 4);
        Assert.Equal(50, after.Projection![^1].Level);

        // At 50 or above the top there are no hours: nothing is filled in from level 49.
        foreach (int lv in new[] { 50, 51 })
        {
            var past = Active(finished, 50, After.AddHours(14), lv);
            Assert.Null(past.EtaHours);
            Assert.Null(past.EtaTo);
            Assert.Null(past.Projection);
        }

        // A relevel finished at 55 reaches 55.
        var to55 = Active(After.AddDays(1).AddHours(14), 55, After.AddDays(4).AddHours(14), 32);
        Assert.Equal(55, to55.EtaTo);
        Assert.Equal(23 * 0.5 / 54, to55.EtaHours!.Value, 4);
    }
}
