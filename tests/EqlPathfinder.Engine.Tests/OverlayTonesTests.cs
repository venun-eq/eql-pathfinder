using EqlPathfinder.Engine.Analysis;

namespace EqlPathfinder.Engine.Tests;

/// <summary>The overlay's bars say what the verdict says (docs/specs/overlay-metric-tones.md): the engine gives each row its tone, the App draws it.</summary>
public class OverlayTonesTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 18, 0, 0);
    private static readonly int[] Default = [3, 4];
    private static int _n;

    private static LevelingSegment Run(double minutes, int kills, double killLevels, DateTime? start = null, int xpKills = -1) =>
        new("Kedge Keep D0", "Kedge Keep", 0, start ?? T0.AddMinutes(++_n), 43, minutes, 0.5, 0, 0, 0, kills, 0, 0, 2, "crawl", 0)
        { VisitId = $"v{++_n}", KillLevels = killLevels, XpKills = xpKills < 0 ? kills : xpKills };

    /// <summary>Five earlier crawls: 60 kills in 15 minutes (4 a minute), 0.5 of a level from the kills.</summary>
    private static List<LevelingSegment> Usual() =>
        Enumerable.Range(0, 5).Select(i => Run(15, 60, 0.5, T0.AddDays(-1).AddMinutes(++_n))).ToList();

    [Theory]
    [InlineData(41, Tone.Good)]   // 3.42 a minute: 85.4% of the usual, just inside the band (15%)
    [InlineData(40, Tone.Warn)]   // 3.33 a minute: 83.3%, just outside
    [InlineData(55, Tone.Good)]   // 4.58 a minute: fast is green too
    public void KillSpeedTakesTheVerdictsColourAtTheEdgeOfItsBand(int kills, Tone tone)
    {
        var p = RunPace.For(Run(12, kills, 0.5), Usual());
        Assert.Equal(tone, p.KillSpeedTone);
        // The bar's value is the number the verdict judged: its kills over its played minutes.
        Assert.Equal(kills / 12.0, p.KillsPerMinute!.Value, 6);
        // A coloured bar is red exactly when its fill falls short of its tick by more than the band.
        Assert.Equal(p.KillsPerMinute!.Value / p.UsualKillsPerMinute!.Value < 1 - p.Band, tone == Tone.Warn);
    }

    [Theory]
    [InlineData(0.86, Tone.Good)]   // just inside the XP band (15%)
    [InlineData(0.84, Tone.Warn)]   // just outside
    [InlineData(1.30, Tone.Good)]   // high is green too
    public void XpPerKillTakesTheVerdictsColourAtTheEdgeOfItsBand(double ofExpected, Tone tone)
    {
        var usual = Usual();
        double expected = RunPace.For(Run(15, 60, 0.5), usual).ExpectedXpPerKill!.Value;
        var p = RunPace.For(Run(15, 60, expected * ofExpected * 60 / 100), usual);
        Assert.Equal(tone, p.XpTone);
        Assert.Equal(p.XpPerKill!.Value / p.ExpectedXpPerKill!.Value < 1 - RunPace.XpBand, tone == Tone.Warn);
    }

    [Fact]
    public void NoVerdictMeansEveryRowIsGrey()
    {
        // Warming up (under 8 kills) and no usual yet: nothing judges the bars.
        var early = RunPace.For(Run(3, 3, 0.1, xpKills: 3), Usual());
        Assert.Equal("early", early.Verdict);
        Assert.Equal(Tone.None, early.KillSpeedTone);
        Assert.Equal(Tone.None, early.XpTone);
        var none = RunPace.For(Run(12, 36, 0.5), Usual().Take(2));
        Assert.Equal("none", none.Verdict);
        Assert.Equal(Tone.None, none.KillSpeedTone);
        Assert.Equal(Tone.None, none.XpTone);
        Assert.Null(none.KillsPerMinute);
        // Under 8 sized kills there is no XP verdict, though the kill speed has one.
        var unsized = RunPace.For(Run(12, 40, 0.1, xpKills: 5), Usual());
        Assert.Equal(Tone.Warn, unsized.KillSpeedTone);
        Assert.Equal(Tone.None, unsized.XpTone);
    }

    private static LevelingSegment Farm(double t5, DateTime? start = null) =>
        new("Kedge Keep D4", "Kedge Keep", 4, start ?? T0.AddHours(++_n), 40, 15, 0.2, t5, 0, 0, 60, 0, 0, 2, "crawl", 0)
        { VisitId = $"f{_n}", KillLevels = 0.2, Completed = true, Refunded = true, Ended = true };

    [Theory]
    [InlineData(8.6, Tone.Good)]    // 34.4 an hour against a usual of 40: 86%, inside the band
    [InlineData(8.4, Tone.Warn)]    // 84%: outside
    [InlineData(10.0, Tone.Good)]   // the usual
    [InlineData(13.1, Tone.Good)]   // more is green too
    public void FarmingT5TakesTheVerdictsColourAtTheEdgeOfItsBand(double t5, Tone tone)
    {
        var history = Enumerable.Range(0, 5).Select(_ => Farm(10)).ToList();
        var p = FarmPace.For(Farm(t5), history, Default, 50);
        Assert.Equal(tone, p.Tone);
        Assert.Equal(p.T5PerHour!.Value / p.Usual!.Value < 1 - p.Band, tone == Tone.Warn);
    }

    [Fact]
    public void AFarmingVisitWithNoUsualOrTooLittleIsGrey()
    {
        Assert.Equal(Tone.None, FarmPace.For(Farm(10), [], Default, 50).Tone);
        var history = Enumerable.Range(0, 5).Select(_ => Farm(10)).ToList();
        var early = new LevelingSegment("Kedge Keep D4", "Kedge Keep", 4, T0.AddHours(++_n), 40, 3, 0.1, 1, 0, 0, 3, 0, 0, 2, "crawl", 0)
        { VisitId = "fe", KillLevels = 0.1 };
        Assert.Equal(Tone.None, FarmPace.For(early, history, Default, 50).Tone);
    }
}
