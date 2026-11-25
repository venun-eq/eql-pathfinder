using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Community;
using EqlPathfinder.Engine.Pages;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

// kunark-cap, the reviews' findings: what the cap of the day does to each reading.
public partial class KunarkCapTests
{
    [Fact]
    public void ATrioThatDinged50OutsideACrawlStaysDoneAt50AndLaterCrawlsAreNotItsRelevel()
    {
        var e = Reincarnated(new DateTime(2026, 12, 1, 12, 0, 0));
        foreach (var c in new[] { "PAL", "MNK", "SHM" }) { var i = e.State.Roster.Classes[c]; i.Level = 51; i.ReachedCap = new DateTime(2026, 12, 4, 20, 0, 0); }
        var j = Assert.Single(Journeys.Build([V(new(2026, 12, 3, 14, 0, 0), 1, 45), V(After.AddHours(14), 51, 53)], e.State, new TrackerOptions()));
        Assert.Equal((true, 50, 1), (j.Done, j.LevelNow, j.Crawls));
    }

    static (Roster r, Func<int, int> rank) Rank3At50()
    {
        var r = new Roster();
        r.Classes["A"] = new Incarnation { Name = "A", Rank = 3, Seen = [Before, Before, Before], SeenLevels = [50, 50, 50] };
        return (r, L => r.TrioRankAt(["A"], L, 50));
    }

    [Fact]
    public void ARankThreeTrioAt50ReadsThirtyPercentBeforeAndAfterKunark()
    {
        var (_, rank) = Rank3At50();
        foreach (var now in new[] { Before, After })
        {
            var seg = new LevelingSegment("Befallen D4", "Befallen", 4, now.AddHours(14), 50, 30, 0.2, 5, 0, 0, 20, 0, 0, 3) { RankAtLevel = rank, Trio = "A/B/C" };
            var a = AskPage.Build(new AskQuery("level", null, null, null, null, null, null),
                new AskInputs([seg], 0, 0, new TrackerOptions(), 50, "A/B/C", 3, null, now.AddHours(15), RankAtLevel: rank));
            Assert.Equal(30, a.XpExtra);
        }
    }

    [Fact]
    public void AFarmRunAt50BeforeKunarkKeepsItsXpPenalty()
    {
        var (_, rank) = Rank3At50();
        var seg = new LevelingSegment("Befallen D4", "Befallen", 4, Before.AddHours(14), 50, 30, 0, 5, 0, 0, 20, 0, 0, 3) { RankAtLevel = rank, Cap = 50 };
        Assert.Equal(1.3, RunPace.For(seg, []).XpMultiplier, 6);
        var lvl = seg with { Cap = 55 };
        Assert.Equal(1.0, RunPace.For(lvl, []).XpMultiplier, 6);
    }

    [Fact]
    public void AskAnswersNothingAboveTheBaseCapUntilTheBandsLand()
    {
        var seg = new LevelingSegment("Plane of Hate - Solo", "Plane of Hate", 4, Before.AddHours(14), 50, 60, 0, 5, 0, 0, 20, 0, 0, 0) { Cap = 50 };
        var a = AskPage.Build(new AskQuery("farm", null, null, null, 55, null, null),
            new AskInputs([seg, seg with { Start = Before.AddDays(-1) }, seg with { Start = Before.AddDays(-2) }], 0, 0, new TrackerOptions(), 50, null, 0, null, After));
        Assert.Empty(a.Farm.Rows);
    }
}

public partial class KunarkCapTests
{
    [Fact]
    public void ASharedCrawlCarriesTheCapOfItsDay()
    {
        Assert.Equal(50, CommunityBuilder.Visit(SharedAt50("2026-12-05"))!.Cap);
        Assert.Equal(55, CommunityBuilder.Visit(SharedAt50("2026-12-09"))!.Cap);
    }
}
