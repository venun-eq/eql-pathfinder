using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// A scrubbed excerpt of a real log (MAG/NEC/BST Erudite, two hours of play) checked against
/// numbers verified by hand at the time. If a change moves these, it changed how crawls are measured.
/// The character is a stand-in and the clock was moved to start at 12:00:00 (FixtureTests), every line by the
/// same amount: the starts below are on the moved clock, and nothing that was measured differs.
/// </summary>
public class GoldenTests
{
    private sealed class Sink : ITrackerSink
    {
        public List<Visit> Closed { get; } = new();
        public List<KillRecord> Kills { get; } = new();
        public void OnVisitClosed(Visit v) => Closed.Add(v);
        public void OnKill(KillRecord k) => Kills.Add(k);
    }

    private static (PathfinderEngine e, Sink s) Load()
    {
        var path = TestLog.BefallenUnrest;
        var s = new Sink();
        var e = new PathfinderEngine(TestLog.Character, new TrackerOptions(), s);
        e.ReadFile(path);
        e.Flush();
        return (e, s);
    }

    [Theory]
    // start,        zone,               D, kill levels, reward levels, kills (slain lines: 0.10.14 counted only XP lines), T5 (frag 1.6), chest T5
    [InlineData("12:06", "Befallen", 1, 9.22, 0.70, 134, 1.8, 0.9)]
    [InlineData("12:41", "Befallen", 4, 2.97, 0.45, 77, 12.4, 6.9)]
    [InlineData("13:07", "Befallen", 4, 2.20, 0.38, 67, 10.9, 8.4)]
    [InlineData("13:34", "Befallen", 4, 1.49, 0.26, 72, 11.8, 9.8)]
    public void CrawlsMatchHandCheckedNumbers(string start, string zone, int d, double killLv, double rewardLv, int kills, double t5, double chest)
    {
        var (_, s) = Load();
        var v = s.Closed.Single(x => x.Start.ToString("HH:mm") == start);
        Assert.Equal(zone, v.BaseZone);
        Assert.Equal(d, v.Difficulty);
        Assert.Equal(killLv, v.KillPercent / 100, 2);
        Assert.Equal(rewardLv, v.RewardPercent / 100, 2);
        Assert.Equal(kills, v.Kills);
        Assert.Equal(t5, v.Loot.T5(1.6), 1);
        Assert.Equal(chest, v.Loot.ChestT5(1.6), 1);
        Assert.True(v.Completed);
        Assert.Equal("NEC/MAG/BST", v.Trio);
        Assert.Equal("Erudite", v.Race);
    }

    [Fact]
    public void EveryKillGetsAMobName()
    {
        var (_, s) = Load();
        Assert.True(s.Kills.Count > 400);
        Assert.All(s.Kills, k => Assert.NotNull(k.Mob));
    }

    [Fact]
    public void LevelsLineUp()
    {
        var (e, s) = Load();
        Assert.Equal(3, s.Closed[0].LevelStart);
        Assert.Equal(13, s.Closed[0].LevelEnd);
        Assert.Equal(23, e.State.Level);
    }
}
