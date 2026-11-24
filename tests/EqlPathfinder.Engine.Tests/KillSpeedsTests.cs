using EqlPathfinder.Engine.Analysis;

namespace EqlPathfinder.Engine.Tests;

/// <summary>Kill speed over visits and the one fit (docs/specs/engine-owns-numbers.md): the pages used to work these out themselves.</summary>
public class KillSpeedsTests
{
    [Fact]
    public void KillSpeedIsWeightedByMinutesAndCountsOnlyVisitsWithAUsual()
    {
        var rows = new[] { StretchTests.Row("a", 0, ks: 0.8, active: 30), StretchTests.Row("b", 1, ks: 1.0, active: 60), StretchTests.Row("c", 2, ks: null, active: 500) };
        var p = KillSpeeds.Over(rows);
        Assert.Equal((0.8 * 30 + 60) / 90, p.Speed!.Value, 6);
        Assert.Equal(2, p.Runs);
        Assert.Equal("On pace", p.Word);
        Assert.Equal("good", p.Tone);
    }

    [Fact]
    public void NoVisitWithAUsualIsNoKillSpeedAtAll()
    {
        var p = KillSpeeds.Over([StretchTests.Row("a", 0, ks: null, active: 20)]);
        Assert.Null(p.Speed);
        Assert.Equal(0, p.Runs);
        Assert.Null(p.Word);
        Assert.Null(p.Tone);
    }

    [Theory]
    [InlineData(0.76, "24% slow", "warn")]
    [InlineData(0.85, "On pace", "good")]
    [InlineData(1.15, "On pace", "good")]
    [InlineData(1.21, "21% fast", "good")]
    public void TheWordUsesTheNarrowBand(double speed, string word, string tone)
    {
        Assert.Equal(0.15, RunPace.NarrowBand);
        Assert.Equal(word, KillSpeeds.Word(speed));
        Assert.Equal(tone, KillSpeeds.Tone(speed));
    }

    [Fact]
    public void ASlopeIsTheLeastSquaresFitAndNeedsTwoPointsAtDifferentX()
    {
        Assert.Equal(2.0, Fit.Slope([(0, 1), (1, 3), (2, 5)])!.Value, 9);
        Assert.Equal(0.25, Fit.Slope([(0, 0), (2, 2), (4, 1)])!.Value, 9);
        Assert.Null(Fit.Slope([(1, 1)]));
        Assert.Null(Fit.Slope([(3, 1), (3, 2)]));
    }
}
