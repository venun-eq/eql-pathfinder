using EqlPathfinder.Engine.Analysis;

namespace EqlPathfinder.Engine.Tests;

/// <summary>Older visits scaled to the player's pace now (Analysis/PaceDrift.cs; backtest in docs/ask-backtest.md).</summary>
public class PaceDriftTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 12, 0, 0), Patch = new(2026, 11, 10, 12, 0, 0);

    private static LevelingSegment V(string zone, int level, double minutesPerLevel, DateTime start) =>
        new($"{zone} D4", zone, 4, start, level, minutesPerLevel * 2, 2, 0, 0, 0, 40, 0, 200);

    /// <summary>Seven places, each played 60 days ago at twice the minutes it takes now (and once in between).</summary>
    private static List<LevelingSegment> TwiceAsFastNow() =>
        Enumerable.Range(0, 7).SelectMany(i => new[]
        {
            V($"Zone{i}", 10 + i * 5, 20, Now.AddDays(-60)),
            V($"Zone{i}", 10 + i * 5, 10 * Math.Sqrt(2), Now.AddDays(-30)),
            V($"Zone{i}", 10 + i * 5, 10, Now.AddDays(-1)),
        }).ToList();

    [Fact]
    public void ItFindsHowMuchFasterThePlayerHasBecome()
    {
        var d = PaceDrift.From(TwiceAsFastNow())!;
        Assert.Equal(7, d.Cells);
        Assert.Equal(21, d.Visits);
        Assert.Equal(-29.7, d.PerMonthPercent, 1);           // half the time over 59 days (the last visit is a day old)
        Assert.Equal(0.70, d.Scale(Now.AddDays(-30), Now), 2); // a visit 30 days old counts at about 70% of its minutes
        Assert.Equal(1 / 1.5, d.Scale(Now.AddDays(-60), Now), 3); // 60 days old would be half: held to 1.5x (old one-offs mustn't win routes)
        Assert.Equal(1, d.Scale(Now, Now), 6);
    }

    [Fact]
    public void AskScalesOldVisitsButKeepsTheRealHoursAsEvidence()
    {
        var d = AskData.From(TwiceAsFastNow(), Patch, Now);
        Assert.NotNull(d.Drift);
        var zone0 = d.Places.Single(p => p.Place == "Zone0 D4");
        Assert.InRange(zone0.Window(10)!.Minutes, 9.5, 10.5);   // every visit now reads as about 10 minutes a level
        var old = AskData.From(TwiceAsFastNow(), Patch, Now, drift: false);
        Assert.True(old.Places.Single(p => p.Place == "Zone0 D4").Window(10)!.Minutes > 11);
        // Hours played stay as played: 20 + 14.1 + 10 minutes a level, 2 levels each.
        var band = d.Bands.Single(b => b.Place == "Zone0 D4" && !b.SincePatch);
        Assert.Equal((40 + 28.28 + 20) / 60, band.Hours, 2);
    }

    [Fact]
    public void TooLittleHistoryMeansNoDrift()
    {
        Assert.Null(PaceDrift.From(TwiceAsFastNow().Take(9)));    // three places
        Assert.Null(AskData.From(TwiceAsFastNow().Take(9), Patch, Now).Drift);
        // Cells need dates a week apart: the same day twice says nothing about time.
        var sameDay = Enumerable.Range(0, 8).SelectMany(i => new[] { V($"Z{i}", 20, 20, Now.AddHours(-5)), V($"Z{i}", 20, 10, Now) });
        Assert.Null(PaceDrift.From(sameDay));
    }

    [Fact]
    public void AnExtremeTrendIsHeldBack()
    {
        // Ten times faster in 30 days is held to 40% faster a month, and no visit scales past 3x.
        var wild = Enumerable.Range(0, 7).SelectMany(i => new[] { V($"Z{i}", 20, 100, Now.AddDays(-30)), V($"Z{i}", 20, 10, Now) }).ToList();
        wild.AddRange(Enumerable.Range(0, 7).Select(i => V($"Z{i}", 20, 50, Now.AddDays(-15))));
        var d = PaceDrift.From(wild)!;
        Assert.Equal(-40, d.PerMonthPercent, 1);
        Assert.Equal(1 / PaceDrift.MaxScale, d.Scale(Now.AddDays(-400), Now), 6);
    }
}
