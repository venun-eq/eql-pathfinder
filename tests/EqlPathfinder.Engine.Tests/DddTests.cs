using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>The user's rule (11-16): crawls from before the DDD nerf where DDD did most of the damage count for nothing.</summary>
public class DddTests
{
    private static Visit Crawl(DateTime start, long ddd, long other)
    {
        var v = new Visit
        {
            Id = "v" + start.Ticks + ddd,
            Zone = "The Plane of Fear 4 (Refined)",
            BaseZone = "The Plane of Fear",
            Difficulty = 4,
            Start = start,
            Closed = true,
            Completed = true,
            CompletedAt = start.AddMinutes(10),
            ActiveSeconds = 600,
            Kills = 100,
            LevelStart = 47
        };
        v.Damage.Total = ddd + other;
        if (ddd > 0) v.Damage.ByAbility["Denon's Desperate Dirge"] = ddd;
        if (other > 0) v.Damage.ByAbility["melee"] = other;
        return v;
    }

    private static readonly DateTime BeforeNerf = RouteAnalysis.FarmPatch.AddDays(-5), AfterNerf = RouteAnalysis.FarmPatch.AddDays(1);

    [Fact]
    public void ADddCrawlFromBeforeTheNerfIsLeftOutOfEveryRate()
    {
        var v = Crawl(BeforeNerf, ddd: 9_000, other: 1_000);
        Assert.Equal(0.9, RouteAnalysis.DddShare(v), 3);
        Assert.True(RouteAnalysis.PreNerfDdd(v));
        Assert.False(RouteAnalysis.Usable(v, 50));
    }

    [Fact]
    public void DddAfterTheNerfCountsLikeAnyOtherSpell()
    {
        var v = Crawl(AfterNerf, ddd: 9_000, other: 1_000);
        Assert.False(RouteAnalysis.PreNerfDdd(v));
        Assert.True(RouteAnalysis.Usable(v, 50));
    }

    [Fact]
    public void ACrawlWhereDddWasASideshowStillCounts()
    {
        var v = Crawl(BeforeNerf, ddd: 3_000, other: 7_000);
        Assert.False(RouteAnalysis.PreNerfDdd(v));
        Assert.True(RouteAnalysis.Usable(v, 50));
        Assert.True(RouteAnalysis.IsDdd("Denon`s Desperate Dirge X"));
        Assert.False(RouteAnalysis.IsDdd("Denon's Disruptive Discord"));
    }
}
