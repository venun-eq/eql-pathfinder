using EqlPathfinder.Engine.Analysis;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// A finished crawl whose mobs were below the player's level opens a chest with no motes ("The Dungeon Crawl opponents
/// were too low-level to earn a refund", docs/specs/chestless-crawls.md). Its 0 T5-eq is a fact, and it mustn't stop the
/// band of the place's other visits from being measured.
/// </summary>
public class ChestlessCrawlTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 18, 0, 0);
    private static readonly int[] Farmed = [3, 4];

    // A finished solo crawl at the cap, 15 minutes: T5-eq an hour is t5 × 4. Each one starts an hour after the last,
    // so the history is always earlier than the visit judged, whichever visits make the usual.
    private static LevelingSegment Crawl(int n, double t5) =>
        new("The Estate of Unrest D4", "The Estate of Unrest", 4, T0.AddHours(n), 50, 15, 0, t5, 0, 0, 60, 0, 0, 2, "crawl", 0)
        { VisitId = $"v{n}", Completed = true, Refunded = true, Ended = true };

    [Fact]
    public void AChestWithNoMotesDoesNotStopTheBand()
    {
        double[] withMotes = [6, 10, 14, 8, 12];
        var five = withMotes.Select((t5, i) => Crawl(i, t5)).ToList();
        var fiveAndNone = five.Append(Crawl(5, 0)).ToList();
        var judged = Crawl(10, 10);

        double measured = FarmPace.For(judged, five, Farmed, 50).Band;
        Assert.NotEqual(FarmPace.DefaultBand, measured, 3);
        Assert.Equal(measured, FarmPace.For(judged, fiveAndNone, Farmed, 50).Band, 6);

        // Four with motes and one without: five visits, but only four rates to measure, so the default.
        var fourAndNone = five.Take(4).Append(Crawl(5, 0)).ToList();
        Assert.Equal(FarmPace.DefaultBand, FarmPace.For(judged, fourAndNone, Farmed, 50).Band, 6);
    }
}
