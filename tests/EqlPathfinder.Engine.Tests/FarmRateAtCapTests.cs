using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>The farm rate counts crawls at the cap at the player's farming difficulties, from the patch's one instant (docs/specs/farm-rate-at-cap.md).</summary>
public class FarmRateAtCapTests
{
    private static int _n;

    private static Visit Crawl(DateTime start, int difficulty, int motes = 20, int minutes = 20)
    {
        var v = new Visit
        {
            Id = "v" + ++_n,
            Zone = "Kedge Keep",
            BaseZone = "Kedge Keep",
            Difficulty = difficulty,
            Start = start,
            ActiveSeconds = minutes * 60,
            LevelStart = 50,
            LevelEnd = 50,
            Kills = minutes,
            Completed = true,
            CompletedAt = start.AddMinutes(minutes),
            Closed = true,
            ExitedAt = start.AddMinutes(minutes),
        };
        v.Loot.Add(LootKind.Mote, MoteTier.Major, motes, false);
        for (int i = 1; i <= minutes; i++) v.KillTimes.Add(start.AddMinutes(i));
        return v;
    }

    private static readonly DateTime After = RouteAnalysis.FarmPatch.AddHours(2);

    [Fact]
    public void ThePatchIsOneInstantSeenOnEachPlayersClock()
    {
        var est = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var sydney = TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
        Assert.Equal(new DateTime(2026, 11, 10, 14, 50, 0), RouteAnalysis.FarmPatchIn(est));
        Assert.Equal(new DateTime(2026, 11, 11, 6, 50, 0), RouteAnalysis.FarmPatchIn(sydney));
        Assert.Equal(RouteAnalysis.FarmPatchIn(TimeZoneInfo.Local), RouteAnalysis.FarmPatch);
    }

    [Fact]
    public void TheSameCrawlsFallOnOppositeSidesOfThePatchOnASydneyClockAndAnEasternOne()
    {
        var est = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var sydney = TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
        // 11-11 03:00 on the wall clock is before the patch in Sydney (patch 06:50 on 11-11) and after it in Eastern (patch 14:50 on 11-10).
        var visits = Enumerable.Range(0, 3).Select(i => Crawl(new DateTime(2026, 11, 11, 3, 0, 0).AddHours(i), 4, 30)).ToList();
        var o = new TrackerOptions();
        var inSydney = RouteAnalysis.FarmRates(visits, o, sydney);
        var inEastern = RouteAnalysis.FarmRates(visits, o, est);
        Assert.Equal((0, 3), (inSydney.Crawls, inSydney.PreCrawls));
        Assert.Equal((3, 0), (inEastern.Crawls, inEastern.PreCrawls));
    }

    [Fact]
    public void ACrawlTenMinutesBeforeThePatchIsBeforeAndTenMinutesAfterIsAfter()
    {
        var p = RouteAnalysis.FarmPatch;
        var visits = new List<Visit>();
        for (int i = 0; i < 3; i++) { visits.Add(Crawl(p.AddMinutes(-60 - i * 30), 4, 10)); visits.Add(Crawl(p.AddMinutes(10 + i * 30), 4, 30)); }
        var info = RouteAnalysis.FarmRates(visits, new TrackerOptions());
        Assert.Equal((3, 3), (info.Crawls, info.PreCrawls));
    }

    [Fact]
    public void ADifficultyZeroCrawlAtTheCapIsNotFarmingAndAFourIs()
    {
        var visits = new List<Visit> { Crawl(After, 0, 1), Crawl(After.AddHours(1), 0, 1), Crawl(After.AddHours(2), 4, 60), Crawl(After.AddHours(3), 4, 60), Crawl(After.AddHours(4), 4, 60) };
        var info = RouteAnalysis.FarmRates(visits, new TrackerOptions());
        Assert.Equal(3, info.Crawls);
        Assert.Equal(2, info.NotFarmed);
        Assert.Equal([3, 4], info.Difficulties);
        Assert.True(info.Usual > 50);
    }

    [Fact]
    public void APlayerWhoFarmsDifficultyTwoHasTheirTwosCountedAndNoFours()
    {
        var o = new TrackerOptions { FarmDifficulties = [2] };
        var visits = new List<Visit> { Crawl(After, 2), Crawl(After.AddHours(1), 2), Crawl(After.AddHours(2), 2), Crawl(After.AddHours(3), 4) };
        var info = RouteAnalysis.FarmRates(visits, o);
        Assert.Equal((3, 1), (info.Crawls, info.NotFarmed));
        Assert.Equal([2], info.Difficulties);
    }

    [Fact]
    public void CrawlsAtOtherDifficultiesAreCountedForTheNoneAtAFarmedDifficultyLine()
    {
        var info = RouteAnalysis.FarmRates([Crawl(After, 1), Crawl(After.AddHours(1), 2)], new TrackerOptions());
        var source = RouteAnalysis.FarmSource(info, new TrackerOptions());
        Assert.Equal(("default", 0, 0, 2), (source.Kind, source.Crawls, source.Unrated, source.NotFarmed));
    }
}
