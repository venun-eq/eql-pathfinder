using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>xp-at-cap: at 50 the bar fills but no level follows, and XP per kill is one number (docs/specs/xp-at-cap.md).</summary>
public class XpAtCapTests
{
    private static (PathfinderEngine e, TrackerTests.Sink s) Run(params string[] lines)
    {
        var s = new TrackerTests.Sink();
        var e = new PathfinderEngine("Venun", new TrackerOptions(), s);
        foreach (var l in lines) e.ProcessLine(l);
        e.Flush();
        return (e, s);
    }

    private static string L(string hms, string body) => $"[Sun Nov 15 {hms} 2026] {body}";

    private static string[] Crawl(int level, params string[] lines) =>
    [
        L("09:59:00", $"You have gained a level! Welcome to level {level}!"),
        L("10:00:00", "You have entered Nektropos Castle 4 (Refined)."),
        .. lines,
        L("11:00:00", "You have entered Nektropos Castle."),
    ];

    private static string[] Kill(string hms, string xp) =>
        [L(hms, $"You gain experience! ({xp}%)"), L(hms, "You have slain a ghoul!")];

    private static string[] Kills(int n, string xp) =>
        [.. Enumerable.Range(1, n).SelectMany(i => Kill($"10:{i:00}:00", xp))];

    [Fact]
    public void XpLinesAtTheCapAreNotLevels()
    {
        var (_, s) = Run(Crawl(50, Kills(8, "2.000")));
        var v = Assert.Single(s.Closed);
        var st = VisitStats.From(v, v.EndTime, new TrackerOptions());
        Assert.Equal(8, v.Kills);
        Assert.Equal(0, st.LevelsGained, 6);
        Assert.Equal(0.0, st.LevelsPerHour!.Value, 6);
        Assert.Equal(16, v.CapPercent, 6);   // the fact stays, as the log says it
        Assert.All(s.Kills, k => Assert.Equal(2, k.Percent, 6));
    }

    [Fact]
    public void KillLevelsPerHourIsTheEquationTheOverlayDraws()
    {
        var (_, s) = Run(Crawl(40, Kills(8, "1.500")));
        var v = Assert.Single(s.Closed);
        var st = VisitStats.From(v, v.EndTime, new TrackerOptions());
        Assert.Equal(st.KillsPerMinute!.Value * 60 * st.XpPerKill!.Value / 100, st.KillLevelsPerHour!.Value, 9);
        Assert.Equal(1.5, st.XpPerKill.Value, 6);
    }

    [Fact]
    public void KillLevelsPerHourIsNullWhenOneKillsXpIsUnknown()
    {
        var (_, s) = Run(Crawl(40, [.. Kills(7, "1.500"), L("10:08:00", "You gain experience!"), L("10:08:00", "You have slain a ghoul!")]));
        var v = Assert.Single(s.Closed);
        var st = VisitStats.From(v, v.EndTime, new TrackerOptions());
        Assert.Equal(8, st.Kills);
        Assert.Equal(7, st.XpKills);
        Assert.Null(st.KillLevelsPerHour);
    }

    [Fact]
    public void AVisitThatDingsTheCapCountsOnlyTheKillsBeforeTheDing()
    {
        var (_, s) = Run(Crawl(49, [
            .. Kill("10:01:00", "40.000"), .. Kill("10:02:00", "60.000"),
            L("10:02:00", "You have gained a level! Welcome to level 50!"),
            .. Kill("10:03:00", "3.000"), .. Kill("10:04:00", "3.000")]));
        var v = Assert.Single(s.Closed);
        var st = VisitStats.From(v, v.EndTime, new TrackerOptions());
        Assert.Equal(4, v.Kills);
        Assert.Equal(1.0, st.KillLevels, 6);
        Assert.Equal(6, v.CapPercent, 6);
    }

    [Fact]
    public void CapKillsAreNotInXpPerKillsDivisor()
    {
        var (_, s) = Run(Crawl(49, [
            .. Kill("10:01:00", "50.000"), .. Kill("10:02:00", "50.000"),
            L("10:02:00", "You have gained a level! Welcome to level 50!"),
            .. Kill("10:03:00", "3.000"), .. Kill("10:04:00", "3.000")]));
        var v = Assert.Single(s.Closed);
        var st = VisitStats.From(v, v.EndTime, new TrackerOptions());
        Assert.Equal(2, v.XpKills);
        Assert.Equal(50.0, st.XpPerKill!.Value, 6);
    }

    [Fact]
    public void XpPerKillIsOneFunction()
    {
        Assert.Equal(10.0 / 12, Rates.XpPerKill(0.10, 12)!.Value, 6);
        Assert.Equal(0.0, Rates.XpPerKill(0, 12)!.Value, 6);   // only no-XP mobs killed: a known 0, not unknown (docs/engineering.md Modeling 11)
        Assert.Null(Rates.XpPerKill(0.10, 0));
    }

    [Fact]
    public void KillsAtTheCapWithNoPercentAreNotXpKills()
    {
        // 6 kills with no XP line at all and 4 whose XP line has no percent, all at 50: none is an XP kill, so no XP per kill.
        var (_, s) = Run(Crawl(50, [
            .. Enumerable.Range(1, 6).Select(i => L($"10:{i:00}:00", "You have slain a ghoul!")),
            .. Enumerable.Range(7, 4).SelectMany(i => new[] { L($"10:{i:00}:00", "You gain experience!"), L($"10:{i:00}:00", "You have slain a ghoul!") })]));
        var v = Assert.Single(s.Closed);
        var st = VisitStats.From(v, v.EndTime, new TrackerOptions());
        Assert.Equal(10, st.Kills);
        Assert.Equal(0, st.XpKills);
        Assert.Null(st.XpPerKill);
        Assert.Null(st.KillLevelsPerHour);
    }

    [Fact]
    public void TheDingKillIsTheOneWhoseXpLineWasReadBelowTheCap()
    {
        // The XP line comes before the ding and its slain line after it: the kill's XP filled the bar, so it counts, at level 49.
        var (_, s) = Run(Crawl(49, [
            .. Kill("10:01:00", "40.000"),
            L("10:02:00", "You gain experience! (30.000%)"),
            L("10:02:00", "You have gained a level! Welcome to level 50!"),
            L("10:02:00", "You have slain a ghoul!"),
            .. Kill("10:03:00", "3.000")]));
        var v = Assert.Single(s.Closed);
        var st = VisitStats.From(v, v.EndTime, new TrackerOptions());
        Assert.Equal(3, v.Kills);
        Assert.Equal(2, v.XpKills);
        Assert.Equal(0.7, st.KillLevels, 6);
        Assert.Equal(3, v.CapPercent, 6);
        Assert.Equal([49, 49, 50], s.Kills.Select(k => k.Level).ToArray());   // a kill's level is the level its XP line was read at
    }
}
