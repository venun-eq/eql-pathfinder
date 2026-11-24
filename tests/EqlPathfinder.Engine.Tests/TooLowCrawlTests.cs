using EqlPathfinder.Engine.Analysis;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// A finished crawl with no refund ("opponents were too low-level", docs/specs/too-low-crawls.md) has a gear-only chest.
/// It stays out of the farming usual, but is still judged itself.
/// </summary>
public class TooLowCrawlTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 18, 0, 0);
    private static readonly int[] Farmed = [3, 4];

    private static LevelingSegment Crawl(int n, double t5, bool refunded) =>
        new("The Estate of Unrest D4", "The Estate of Unrest", 4, T0.AddHours(n), 50, 15, 0, t5, 0, 0, 60, 0, 0, 2, "crawl", 0)
        { VisitId = $"v{n}", Completed = true, Refunded = refunded, Ended = true };

    private static List<LevelingSegment> Good() => [Crawl(0, 6, true), Crawl(1, 10, true), Crawl(2, 14, true)];

    [Fact]
    public void ATooLowCrawlIsLeftOutOfTheUsual()
    {
        var withTooLow = Good().Append(Crawl(3, 0, false)).ToList();
        var r = FarmPace.For(Crawl(10, 10, true), withTooLow, Farmed, 50);
        Assert.Equal(3, r.Runs);
        Assert.Equal(40, r.Usual!.Value, 6);

        var two = Good().Take(2).Append(Crawl(3, 0, false)).ToList();
        Assert.Equal("none", FarmPace.For(Crawl(10, 10, true), two, Farmed, 50).Verdict);
    }

    [Fact]
    public void ATooLowCrawlIsStillJudged()
    {
        var r = FarmPace.For(Crawl(10, 0, false), Good(), Farmed, 50);
        Assert.Equal("less", r.Verdict);
        Assert.Equal(-1, r.Pct!.Value, 6);
    }

    [Fact]
    public void ARefundedCrawlStaysIn()
    {
        var withRefunded = Good().Append(Crawl(3, 0, true)).ToList();
        Assert.Equal(4, FarmPace.For(Crawl(10, 10, true), withRefunded, Farmed, 50).Runs);
    }
}
