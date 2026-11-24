using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

public class GoalTests
{
    private static RouteOption Opt(string zone, double lvh, double t5h, double? cost, double rare, int crawls = 3) =>
        new(zone, 4, crawls, lvh, t5h, 4, cost, [], 20, 25, rare);

    private static readonly RouteOption[] Options =
    [
        Opt("Value", 5, 45, 1.0, 1),     // the balanced pick
        Opt("Fast", 8, 10, 5.0, 0),      // levels quickest, few motes
        Opt("Motes", 3, 60, -3.3, 2),    // most motes
        Opt("Rare", 4, 40, 2.5, 9),      // most drops above T5
    ];

    [Theory]
    [InlineData("balanced", "Motes")]   // lowest cost per level (it out-farms your farm)
    [InlineData("speed", "Fast")]
    [InlineData("motes", "Motes")]
    [InlineData("rare", "Rare")]
    public void EachGoalPicksItsOwnBest(string goal, string expected) =>
        Assert.Equal(expected, RouteAnalysis.Rank(Options, goal, leveling: true)[0].BaseZone);

    [Fact]
    public void FarmingAtTheCapCountsWithoutKills()
    {
        // No XP at the cap, so the log names no kills; 206 Plane of Fear runs were being ignored.
        var farm = new Visit { Id = "f", Difficulty = 4, Closed = true, ActiveSeconds = 1200, LevelStart = 50, Kills = 0 };
        farm.Loot.Add(Model.LootKind.Mote, Model.MoteTier.Major, 10, false);
        Assert.True(RouteAnalysis.Usable(farm, 50));
        var leveling = new Visit { Id = "l", Difficulty = 4, Closed = true, ActiveSeconds = 1200, LevelStart = 30, Kills = 0 };
        leveling.Loot.Add(Model.LootKind.Mote, Model.MoteTier.Major, 10, false);
        Assert.False(RouteAnalysis.Usable(leveling, 50));
    }

    [Fact]
    public void FarmRateComesFromTheLogsWhenThereIsEnoughFarming()
    {
        var o = new TrackerOptions { FarmRateManual = 50 };
        var plenty = new FarmRateInfo(RouteAnalysis.FarmPatch, 12, 32, 41, "Plane of Hate D4", 5, 40, 55, 63, "Sky Island D4");
        var thin = new FarmRateInfo(RouteAnalysis.FarmPatch, 2, 30, null, null, 0, 40, 55, 63, "Sky Island D4");

        o.FarmRateMode = "best";
        Assert.Equal(41, RouteAnalysis.EffectiveFarmRate(plenty, o));
        Assert.Equal(50, RouteAnalysis.EffectiveFarmRate(thin, o));   // two crawls since the patch: not enough, use the typed rate
        o.FarmRateMode = "usual";
        Assert.Equal(32, RouteAnalysis.EffectiveFarmRate(plenty, o));
        o.FarmRateMode = "manual";
        Assert.Equal(50, RouteAnalysis.EffectiveFarmRate(plenty, o));
    }

    // Pages called the fallback "your farm rate of 50" and "the rate you set" when the player had neither farmed nor set
    // one (11-17): the source says which it is, and the rate stays what EffectiveFarmRate picks.
    [Fact]
    public void FarmRateSaysWhereItComesFrom()
    {
        var plenty = new FarmRateInfo(RouteAnalysis.FarmPatch, 12, 32, 41, "Plane of Hate D4", 5, 40, 55, 63, "Sky Island D4");
        var noBest = new FarmRateInfo(RouteAnalysis.FarmPatch, 4, 30, null, null, 0, 0, null, null, null);
        var none = new FarmRateInfo(RouteAnalysis.FarmPatch, 0, null, null, null, 0, 40, 55, 63, "Sky Island D4");
        var thin = new FarmRateInfo(RouteAnalysis.FarmPatch, 2, 30, null, null, 0, 0, null, null, null);

        var o = new TrackerOptions();   // a new player: best farm, nothing typed
        Assert.Equal(new FarmRateSource("best", 41, "Plane of Hate D4", 5, false), RouteAnalysis.FarmSource(plenty, o));
        Assert.Equal(new FarmRateSource("usual", 30, null, 4, false), RouteAnalysis.FarmSource(noBest, o));
        Assert.Equal(new FarmRateSource("default", TrackerOptions.DefaultFarmRate, null, 0, true), RouteAnalysis.FarmSource(none, o));
        Assert.Equal(new FarmRateSource("default", TrackerOptions.DefaultFarmRate, null, 2, true), RouteAnalysis.FarmSource(thin, o));

        o.FarmRateMode = "usual";
        Assert.Equal(new FarmRateSource("usual", 32, null, 12, false), RouteAnalysis.FarmSource(plenty, o));

        o.FarmRateMode = "manual";   // typing a rate switches to manual
        Assert.Equal(new FarmRateSource("set", 50, null, 0, false), RouteAnalysis.FarmSource(plenty, o));
        o.FarmRateManual = 35;
        o.FarmRateMode = "best";     // and switching back keeps it as the fallback: still the rate you set
        Assert.Equal(new FarmRateSource("set", 35, null, 0, true), RouteAnalysis.FarmSource(none, o));
        Assert.Equal(41, RouteAnalysis.FarmSource(plenty, o).Rate);

        // The rate itself is chosen as before 11-17.
        double Before(FarmRateInfo info) => o.FarmRateMode switch
        {
            "manual" => o.FarmRateManual,
            "usual" => info.Crawls >= 3 && info.Usual is { } u ? u : o.FarmRateManual,
            _ => info.Best ?? (info.Crawls >= 3 ? info.Usual : null) ?? o.FarmRateManual,
        };
        foreach (var info in new[] { plenty, noBest, none, thin })
            foreach (var mode in new[] { "best", "usual", "manual" })
            {
                o.FarmRateMode = mode;
                Assert.Equal(Before(info), RouteAnalysis.EffectiveFarmRate(info, o));
            }
    }
}
