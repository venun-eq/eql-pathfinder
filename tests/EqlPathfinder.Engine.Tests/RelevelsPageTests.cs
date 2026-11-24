using System.Text.Json.Nodes;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// History → Relevels (docs/specs/journey-race-in-engine.md): the page draws the race the engine worked out. Three finished relevels
/// (SHD/ROG/BER 7.3 h at rank 2, RNG/DRU/BRD 9.3 h and PAL/MNK/ENC 9.7 h at rank 1) and SHD/SHM/WIZ at 30 after 3 h, rank 1: the
/// hours are re-figured for rank 1's +10% (SHD/ROG/BER: 7.3 x 1.1 / 1.2 = 6.69 h). Each curve is 1 to 30 in 40% of its hours, then to 50.
/// </summary>
public sealed class RelevelsPages : IAsyncLifetime
{
    internal PageRun Routes { get; private set; } = null!;
    internal PageRun Splits { get; private set; } = null!;
    /// <summary>The splits when PAL/MNK/ENC (9.7 h) is the one that was fastest from 46 to 50.</summary>
    internal PageRun FasterAtTheEnd { get; private set; } = null!;

    private static JsonObject Pt(double hours, double level) => new() { ["hours"] = hours, ["level"] = level };

    public async Task InitializeAsync()
    {
        var message = await HomeGoalPages.Leveling("motes", "D4");
        var faster = await HomeGoalPages.Leveling("motes", "D4");
        faster["reinc"]!["journeys"]!.AsArray()[2]!["curve"] = new JsonArray(Pt(0, 1), Pt(3.88, 30), Pt(9.2, 46), Pt(9.7, 50));
        HomeGoalPages.WithRaces(faster);
        var opened = await PageRuns.AtOnce(
            h => h.Run("relevels", PageStep.Send(message), PageStep.Read("routes")),
            h => h.Run("relevels", PageStep.Send(message), PageStep.Click("[data-rel=view][data-v=splits]"), PageStep.Read("splits")),
            h => h.Run("relevels", PageStep.Send(faster), PageStep.Click("[data-rel=view][data-v=splits]"), PageStep.Read("splits")));
        (Routes, Splits, FasterAtTheEnd) = (opened.Runs[0], opened.Runs[1], opened.Runs[2]);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public class RelevelsPageTests(RelevelsPages pages) : IClassFixture<RelevelsPages>
{
    [Fact]
    public void TheRaceHeaderNamesTheBestTheTypicalAndTheLastInHoursRefiguredForTheOpenRelevelsXp()
    {
        var race = pages.Routes.Text("relevelsBody");
        Assert.Contains("3 relevels · best 6.7 h · typical 9.3 h · last 9.7 h · SHD/SHM/WIZ in progress", race);
        Assert.Contains("Hours are re-figured for +10% XP to level (this relevel's)", race);
    }

    [Fact]
    public void EveryRelevelIsARowWithItsRefiguredHoursAndItsHoursAsPlayed()
    {
        var rows = pages.Routes.Text("relevelsBody");
        Assert.Contains("SHD/ROG/BER", rows);
        Assert.Contains("6.7 h", rows);
        Assert.Contains("7.3 h played", rows);   // the rank 2 relevel, as played
        Assert.Contains("9.3 h", rows);
        Assert.Contains("9.7 h", rows);
        Assert.Contains("★ SHD/ROG/BER", rows);  // the best has the star
    }

    /// <summary>Level 10 of SHD/ROG/BER: 9 of 29 levels of the first 2.68 h (7.3 x 0.4 x 1.1 / 1.2) is 0.83 h, 50 min. RNG/DRU/BRD: 9/29 x 3.72 h = 69 min. PAL/MNK/ENC: 9/29 x 3.88 h = 72 min.</summary>
    [Fact]
    public void TheSplitsAreMinutesPerStretchAndBestOfEachIsTheFastestInEveryColumn()
    {
        var splits = pages.Splits.Text("relevelsBody");
        Assert.Matches(@"SHD/ROG/BER[\s\S]*?\b50\b", splits);
        Assert.Contains("69", splits);
        Assert.Contains("72", splits);
        Assert.Contains("Best of each", splits);
        Assert.Contains("Your best stretches add up to", splits);
    }
}

public class RelevelsBestOfEachTests(RelevelsPages pages) : IClassFixture<RelevelsPages>
{
    /// <summary>
    /// SHD/ROG/BER (6.69 h refigured) is fastest in every stretch but the last; PAL/MNK/ENC's curve here is level 46 at 9.2 h, so 46 to 50 takes it 30 min
    /// against 48.18 (SHD/ROG/BER: 0.6 x 6.6917 h x 60 x 4/20) and 66.96. SHD/ROG/BER's stretches in minutes: 160.6 x 9/29 = 49.8, 160.6 x 10/29 = 55.4 twice, 240.9 x 10/20 = 120.5, 240.9 x 6/20 = 72.3.
    /// Best of each: 49.83 + 55.38 + 55.38 + 120.45 + 72.27 + 30 = 383.31 min = 6.39 h, 6.6917 - 6.3886 = 0.30 h under the best.
    /// </summary>
    [Fact]
    public void BestOfEachTakesTheFastestInEachColumnEvenFromAnotherRelevelAndSaysHowFarUnderTheBestItIs()
    {
        var splits = pages.FasterAtTheEnd.Text("relevelsBody");
        Assert.Matches(@"Best of each\s*50\s*55\s*55\s*120\s*72\s*30\s*6\.4 h", splits);
        Assert.Contains("add up to 6.4 h: the relevel you'd have if one run did them all, 0.3 h under your best", splits);
    }
}
