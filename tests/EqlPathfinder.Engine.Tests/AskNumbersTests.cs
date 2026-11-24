using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Storage;

namespace EqlPathfinder.Engine.Tests;

/// <summary>Ask's cost per level and how levels/h changes a level at a place come from the engine (docs/specs/engine-owns-numbers.md).</summary>
public class AskNumbersTests
{
    private static readonly DateTime T0 = new(2026, 11, 10, 8, 0, 0);

    /// <summary>A leveling run at Befallen D4 from <paramref name="level"/>: <paramref name="lph"/> levels an hour over <paramref name="minutes"/>.</summary>
    private static LevelingSegment Run(int level, double lph, double minutes = 30, string trio = "WAR/CLR/ROG", int rank = 0, int kills = 60) =>
        new("Befallen D4", "Befallen", 4, T0.AddDays(level), level, minutes, lph * minutes / 60, 0, 0, 0, kills, 0, 0, rank, "crawl", 0, trio);

    [Fact]
    public void CostPerLevelIsTheMotesYouGiveUpForEachLevel()
    {
        Assert.Equal(25.0, RouteAnalysis.CostPerLevel(100, 0, 4)!.Value, 9);
        Assert.Equal(-5.0, RouteAnalysis.CostPerLevel(100, 120, 4)!.Value, 9);
        Assert.Null(RouteAnalysis.CostPerLevel(100, 20, 0.04));   // too slow to price, here and in Ask and Where next alike
        Assert.Null(RouteAnalysis.CostPerLevel(100, 20, 0));
    }

    [Fact]
    public void LevelsPerHourFallingAboutTenPercentALevelIsReadAsMinusTen()
    {
        var runs = Enumerable.Range(0, 5).Select(i => Run(10 + 2 * i, Math.Pow(0.9, 2 * i))).ToList();
        Assert.Equal(-10, Ask.Falls(runs, "WAR/CLR/ROG")["Befallen D4"]!.Value, 0);
    }

    [Fact]
    public void NoFallUnderThreeRunsOrThreeLevelsApart()
    {
        Assert.Null(Ask.Falls([Run(10, 10), Run(11, 9)], null)["Befallen D4"]);
        Assert.Null(Ask.Falls([Run(10, 10), Run(11, 9), Run(12, 8)], null)["Befallen D4"]);   // 2 levels apart
    }

    [Fact]
    public void YourOwnTrioDecidesTheFallOnceItHasThreeRuns()
    {
        var own = Enumerable.Range(0, 3).Select(i => Run(10 + 2 * i, Math.Pow(0.95, 2 * i))).ToList();
        var others = Enumerable.Range(0, 3).Select(i => Run(11 + 2 * i, Math.Pow(0.8, 2 * i), trio: "RNG/DRU/BRD")).ToList();
        Assert.Equal(-5, Ask.Falls(own.Concat(others).ToList(), "WAR/CLR/ROG")["Befallen D4"]!.Value, 0);
    }

    [Fact]
    public void RunsUnderFiveMinutesDontCount()
    {
        var runs = Enumerable.Range(0, 5).Select(i => Run(10 + 2 * i, 10 * Math.Pow(0.9, 2 * i), minutes: 4)).ToList();
        Assert.Null(Ask.Falls(runs, null)["Befallen D4"]);
    }
}

/// <summary>Ask's answer hands the farm rate and the trio to <c>Cost</c> and <c>Falls</c> (the wiring, not just the helpers).</summary>
public class AskPlacesWiringTests
{
    private static readonly DateTime T0 = new(2026, 11, 20, 8, 0, 0);

    private static LevelingSegment Run(int level, double lph, string trio) =>
        new("Befallen D4", "Befallen", 4, T0.AddDays(level), level, 30, lph / 2, 0, 0, 0, 60, 0, 0, 0, "crawl", 0, trio);

    [Fact]
    public void PlacesCarryTheCostAtYourFarmRateAndTheFallOfYourTrio()
    {
        // Your trio's runs fall 5% a level, another trio's 20%: the fall is yours.
        var runs = Enumerable.Range(0, 3).Select(i => Run(10 + 2 * i, 10 * Math.Pow(0.95, 2 * i), "WAR/CLR/ROG"))
            .Concat(Enumerable.Range(0, 3).Select(i => Run(11 + 2 * i, 10 * Math.Pow(0.8, 2 * i), "RNG/DRU/BRD"))).ToList();
        var options = new EqlPathfinder.Engine.Tracking.TrackerOptions();
        var a = EqlPathfinder.Engine.Pages.AskPage.Build(new AskQuery("places", null, null, null, 20, "all", null),
            new EqlPathfinder.Engine.Pages.AskInputs(runs, 0, 0, options, 20, "WAR/CLR/ROG", 0, null, T0.AddDays(30), 0, 0, 120));
        Assert.InRange(a.Places!.Falls["Befallen D4"]!.Value, -7, -4);   // about 5, the midpoint of a run being its level plus half its gain
        var row = a.Places.Levels.SelectMany(l => l.Rows).First(r => r.Place == "Befallen D4");
        Assert.Equal(RouteAnalysis.CostPerLevel(120, row.T5PerHour, row.LevelsPerHour)!.Value, row.Cost!.Value, 0);   // the row shows levels/h to 2 places; the cost uses them whole
        Assert.NotNull(row.Cost);
    }
}

public class FarmRateValueTests
{
    [Fact]
    public void TheFarmRateIsWorkedOutFromTheSettingEveryTimeNothingNeedsToRunFirst()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eql-farmrate-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
            File.WriteAllText(log, "");
            var o = new EqlPathfinder.Engine.Tracking.TrackerOptions { FarmRateMode = "manual", FarmRateManual = 120 };
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), o);
            Assert.Equal(120, svc.FarmRateNow());
            o.FarmRateManual = 90;
            Assert.Equal(90, svc.FarmRateNow());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void WhereNextAndAskPriceTooSlowALevelTheSameWay()
    {
        // Ask's old copy priced anything above 0 levels an hour; the one function leaves out 0.05 and under everywhere.
        Assert.Null(RouteAnalysis.CostPerLevel(100, 20, 0.05));
        Assert.Equal(1600.0, RouteAnalysis.CostPerLevel(100, 20, 0.05001)!.Value, 0);
    }
}
