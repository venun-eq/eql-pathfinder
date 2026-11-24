using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// One rule for every rate (docs/specs/one-rate.md, the user, 11-18): a visit has a rate when it is a finished crawl or has
/// at least 8 kills, with no time minimum. Under it the rate is null ("—"), never 0 and never a 1-minute floor.
/// </summary>
public class RateRuleTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 18, 0, 0);
    private static readonly TrackerOptions Options = new();

    private static Visit Crawl(double minutes, int kills, bool finished = false, long damage = 0, int t5Motes = 0)
    {
        var v = new Visit
        {
            Id = "v",
            Zone = "Kedge Keep",
            BaseZone = "Kedge Keep",
            Difficulty = 4,
            Start = T0,
            ActiveSeconds = minutes * 60,
            Kills = kills,
            KillPercent = kills * 2,
            Completed = finished,
            Closed = true,
            ExitedAt = T0.AddMinutes(minutes),
        };
        v.Damage.Total = damage;
        v.Loot.Add(LootKind.Mote, MoteTier.Major, t5Motes, false);
        for (int i = 0; i < kills; i++) v.KillTimes.Add(T0.AddSeconds(30 + i * 5));
        return v;
    }

    private static double Value(object? x) => Convert.ToDouble(x);

    [Fact]
    public void ATwoMinuteInAndOutWithOneKillHasNoRateAtAll()
    {
        var s = VisitStats.From(Crawl(2, 1, t5Motes: 1, damage: 5000), T0.AddMinutes(2), Options);
        Assert.Null(s.LevelsPerHour);
        Assert.Null(s.T5PerHour);
        Assert.Null(s.MotesOnlyPerHour);
        Assert.Null(s.KillsPerMinute);
        Assert.Null(s.Dps);
    }

    [Fact]
    public void AFinishedCrawlHasRatesHoweverShort()
    {
        var s = VisitStats.From(Crawl(4.2, 3, finished: true, t5Motes: 10), T0.AddMinutes(5), Options);
        Assert.NotNull(s.T5PerHour);
        Assert.Equal(s.T5 * 60 / s.ActiveMinutes, Value(s.T5PerHour), 6);
    }

    [Fact]
    public void EightKillsMakeARateWithNoTimeMinimumAndSevenDoNot()
    {
        Assert.NotNull(VisitStats.From(Crawl(1.5, 8), T0.AddMinutes(2), Options).KillsPerMinute);
        Assert.Null(VisitStats.From(Crawl(30, 7), T0.AddMinutes(30), Options).KillsPerMinute);
    }

    [Fact]
    public void ARateOverTheRuleThatIsZeroStaysZero()
    {
        var s = VisitStats.From(Crawl(10, 8, finished: true), T0.AddMinutes(10), Options);
        Assert.Equal(0.0, Value(s.T5PerHour));
    }

    [Fact]
    public void DpsDividesByActiveTimeLikeEveryOtherRate()
    {
        var v = Crawl(20, 8, damage: 600000);
        v.KillTimes.Add(T0.AddMinutes(19));   // a gap of 18 minutes: most of it is idle
        var s = VisitStats.From(v, T0.AddMinutes(20), Options);
        Assert.True(s.ActiveMinutes < s.Minutes - 5, "the visit has idle time");
        Assert.Equal(600000 / (s.ActiveMinutes * 60), Value(s.Dps), 3);
    }

    [Fact]
    public void ASessionHasARateOnlyWhenItHoldsAFinishedCrawlOrEnoughKills()
    {
        var junk = Crawl(2, 1, t5Motes: 1);
        var none = SessionStats.From([junk], T0.AddMinutes(2), Options);
        Assert.True(none.Early);
        Assert.Null(none.T5PerHourCrawl);
        Assert.Null(none.LevelsPerHourCrawl);
        var done = Crawl(3, 2, finished: true, t5Motes: 4);
        var some = SessionStats.From([junk, done], T0.AddMinutes(5), Options);
        Assert.False(some.Early);
        Assert.NotNull(some.T5PerHourCrawl);
    }

    [Fact]
    public void TooLittleToJudgeIsTheSameRuleAsHavingARate()
    {
        LevelingSegment Seg(double minutes, int kills, bool finished) =>
            new("Kedge Keep D4", "Kedge Keep", 4, T0, 40, minutes, 0.5, 0, 0, 0, kills, 0, 0) { Completed = finished };
        Assert.False(RunPace.Early(Seg(1.5, 8, false)));
        Assert.False(RunPace.Early(Seg(4, 2, true)));
        Assert.True(RunPace.Early(Seg(30, 7, false)));
    }
}

/// <summary>The other copies of the rule: the pace check, a relevel's legs, the box that hides visits, an empty session.</summary>
public class RateRuleEverywhereTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 18, 0, 0);
    private static readonly TrackerOptions Options = new();

    private static Visit Farm(string id, double minutes, int kills, bool finished = true, string zone = "Kedge Keep", int motes = 20)
    {
        var v = new Visit
        {
            Id = id,
            Zone = zone,
            BaseZone = zone,
            Difficulty = 4,
            Start = T0,
            ActiveSeconds = minutes * 60,
            LevelStart = 50,
            LevelEnd = 50,
            Kills = kills,
            Completed = finished,
            Closed = true,
            ExitedAt = T0.AddMinutes(minutes),
        };
        v.Loot.Add(LootKind.Mote, MoteTier.Major, motes, false);
        for (int i = 0; i < kills; i++) v.KillTimes.Add(T0.AddSeconds(30 + i * 5));
        return v;
    }

    /// <summary>The verdict a visit in progress gets: the same judge as History (<see cref="FarmPace"/>), against three earlier visits.</summary>
    private static FarmPace Pace(Visit now)
    {
        var past = Enumerable.Range(0, 3).Select(i => Farm("p" + i, 20, 40, motes: 50)).Select(v => LevelingSegment.From(VisitStats.From(v, v.EndTime, Options)) with { Start = T0.AddDays(-1), Completed = now.Completed, Refunded = now.Completed }).ToList();
        return FarmPace.For(LevelingSegment.From(VisitStats.From(now, now.EndTime, Options)), past, [3, 4], 50);
    }

    [Fact]
    public void AFarmingVisitIsEarlyWithSevenKillsHoweverLongAndNotWithEightHoweverShort()
    {
        Assert.Equal("early", Pace(Farm("a", 30, 7, finished: false)).Verdict);
        Assert.NotEqual("early", Pace(Farm("b", 1.5, 8, finished: false)).Verdict);
        Assert.NotEqual("early", Pace(Farm("c", 4, 2, finished: true)).Verdict);
    }

    [Fact]
    public void ALegHasNoRateUnderSevenKillsAndOneWithEightAcrossMergedLegs()
    {
        var seven = new JourneyLeg("Kedge Keep", 4, 40, 41, 1, 20, 1, 10) { Kills = 7 };
        Assert.Null(seven.LevelsPerHour);
        Assert.Null(seven.T5PerHour);
        var merged = new JourneyLeg("Kedge Keep", 4, 40, 42, 2, 40, 2, 20) { Kills = 8 };
        Assert.Equal(3.0, merged.LevelsPerHour!.Value, 6);
        Assert.Equal(30.0, merged.T5PerHour!.Value, 6);
        Assert.NotNull(new JourneyLeg("Kedge Keep", 4, 40, 41, 1, 3, 0.1, 1) { Kills = 1, Finished = true }.T5PerHour);
    }

    [Fact]
    public void TheBoxHidesNoRateVisitsExceptRaids()
    {
        VisitStats Stats(Visit v) => VisitStats.From(v, v.EndTime, Options);
        var junk = Farm("j", 3, 2, finished: false); junk.LevelStart = 40;
        Assert.True(Stats(junk).TooShort);
        Assert.False(Stats(Farm("f", 3, 2, finished: true)).TooShort);
        Assert.True(Stats(Farm("c", 12, 0, finished: false)).TooShort);   // at the cap too, now its kills count (kills-at-cap)
        var raid = Farm("r", 3, 2, finished: false, zone: "Kedge Keep - Solo"); raid.LevelStart = 40;
        Assert.False(Stats(raid).TooShort);
    }

    [Fact]
    public void ASessionWithNoVisitsHasNoRatesNotZeros()
    {
        var none = SessionStats.From([], T0, Options);
        Assert.Null(none.T5PerHourCrawl);
        Assert.Null(none.T5PerHourWall);
        Assert.Null(none.LevelsPerHourCrawl);
        Assert.Null(none.LevelsPerHourWall);
    }
}

/// <summary>A stop is judged as a whole by the same rule: a finished crawl in it, or 8 kills in all.</summary>
public class StopRateRuleTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 18, 0, 0);

    private static VisitStats Visit(double minutes, int kills, bool finished) =>
        VisitStats.From(new Visit
        {
            Id = "v",
            Zone = "Kedge Keep",
            BaseZone = "Kedge Keep",
            Difficulty = 4,
            Start = T0,
            ActiveSeconds = minutes * 60,
            Kills = kills,
            Completed = finished,
            Closed = true,
            ExitedAt = T0.AddMinutes(minutes)
        }, T0.AddMinutes(minutes), new TrackerOptions())
        with
        { FarmUsual = 40, FarmBand = 0.25, FarmRuns = 3, FarmBasis = "crawls you finished, solo" };

    [Fact]
    public void AStopOfShortUnfinishedVisitsIsEarlyAndOneWithAFinishedCrawlIsJudged()
    {
        Assert.Equal("early", FarmPace.ForStop([Visit(3, 2, false), Visit(4, 3, false)]).Verdict);
        Assert.NotEqual("early", FarmPace.ForStop([Visit(3, 2, false), Visit(4, 3, true)]).Verdict);
        Assert.NotEqual("early", FarmPace.ForStop([Visit(3, 4, false), Visit(4, 4, false)]).Verdict);   // 8 kills between them
    }
}
