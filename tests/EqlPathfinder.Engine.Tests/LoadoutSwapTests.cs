using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// A crawl's levels belong to the loadout that played it (report #8, app 0.10.8): a swap seen only through /who, or a
/// reincarnation inside the crawl, gave a crawl the other loadout's level, and the race chart drew a level-32 relevel
/// reaching 50.
/// </summary>
public class LoadoutSwapTests
{
    private static (PathfinderEngine e, TrackerTests.Sink s) Run(params string[] lines)
    {
        var s = new TrackerTests.Sink();
        var e = new PathfinderEngine("Venun", new TrackerOptions(), s);
        foreach (var l in lines) e.ProcessLine(l);
        e.Flush();
        return (e, s);
    }

    private static string L(string hms, string body) => $"[Wed Nov 18 {hms} 2026] {body}";

    private static string Reinc(string kind, string name) => $"You have gained the ability \"{kind} Reincarnation: {name}\" at a cost of 0 ability points.";

    private const string Relevel = "[32 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen";
    private const string Main = "[50 PAL/MNK/SHM] Venun (Froglok) ZONE: Befallen";

    /// <summary>
    /// Report #8 as the log gave it: a relevel at 31 dings to 32 in one crawl, /who shows it, then /who shows the main at 50
    /// (swapped in with no level line), the player swaps back without a /who, enters a crawl, and /who inside shows 32.
    /// </summary>
    private static (PathfinderEngine e, TrackerTests.Sink s) SwapBeforeEntering() => Run(
        L("12:00:00", Reinc("Class", "Warrior")),
        L("12:00:00", Reinc("Class", "Cleric")),
        L("12:00:00", Reinc("Class", "Ranger")),
        L("12:01:00", "[31 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"),
        L("12:02:00", "You have entered Befallen 4 (Refined)."),
        L("12:05:00", "You gain experience! (50.000%)"),
        L("12:05:00", "You have slain a ghoul!"),
        L("12:10:00", "You gain experience! (50.000%)"),
        L("12:10:00", "You have slain a ghoul!"),
        L("12:10:00", "You have gained a level! Welcome to level 32!"),
        L("12:11:00", "You have completed the Dungeon Crawl and earned reward loot!"),
        L("12:12:00", "You have entered Befallen."),
        L("12:13:00", Relevel),
        L("12:14:00", Main),
        L("12:20:00", "You have entered Kedge Keep 4 (Refined)."),
        L("12:21:00", "[32 WAR/CLR/RNG] Venun (Ogre) ZONE: Kedge Keep"),
        L("12:25:00", "You gain experience! (60.000%)"),
        L("12:25:00", "You have slain a seahorse!"),
        L("12:30:00", "You gain experience! (40.000%)"),
        L("12:30:00", "You have slain a seahorse!"),
        L("12:30:00", "You have gained a level! Welcome to level 33!"),
        L("12:35:00", "You gain experience! (70.000%)"),
        L("12:35:00", "You have slain a seahorse!"),
        L("12:40:00", "You gain experience! (30.000%)"),
        L("12:40:00", "You have slain a seahorse!"),
        L("12:40:00", "You have gained a level! Welcome to level 34!"),
        L("12:44:00", "You have completed the Dungeon Crawl and earned reward loot!"),
        L("12:45:00", "You have entered Dagnor's Cauldron."));

    [Fact]
    public void ASwapSeenOnlyThroughWhoBeforeACrawlStartsItAtThePlayedLoadoutsLevel()
    {
        var (_, s) = SwapBeforeEntering();
        var kedge = Assert.Single(s.Closed, v => v.BaseZone == "Kedge Keep");
        Assert.Equal(("WAR/CLR/RNG", 32, 34), (kedge.Trio, kedge.LevelStart, kedge.LevelEnd));
    }

    [Fact]
    public void TheRaceChartDoesNotReachTheCapForARelevelAt34()
    {
        var (e, s) = SwapBeforeEntering();
        var j = Assert.Single(Journeys.Build(s.Closed, e.State, new TrackerOptions()));
        Assert.Equal((31, 34, false), (j.LevelFrom, j.LevelNow, j.Done));
        Assert.Equal(34, j.Curve.Max(p => p.Level));
    }

    [Fact]
    public void ASwapUpToTheCapSeenInsideACrawlIsNotALeveledCrawl()
    {
        // The real log's other shape: a level-9 loadout /who'd, the main swapped in without a /who, and a solo task done at 50.
        var (_, s) = Run(
            L("12:00:00", "[9 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"),
            L("12:05:00", "You have entered Befallen 4 (Refined)."),
            L("12:06:00", Main),
            L("12:07:00", "You have slain a ghoul!"),
            L("12:20:00", "You have entered Befallen."),
            L("13:30:00", "You have entered Befallen."));   // an hour on: a visit with a kill waits to be resumed, then closes
        var v = Assert.Single(s.Closed);
        Assert.Equal(("PAL/MNK/SHM", 50, 50), (v.Trio, v.LevelStart, v.LevelEnd));
    }

    [Fact]
    public void ASwapSeenThroughWhoMidCrawlEndsTheCrawlAtThePlayedLoadoutsLevel()
    {
        var (_, s) = Run(
            L("12:00:00", Relevel),
            L("12:02:00", "You have entered Befallen 4 (Refined)."),
            L("12:03:00", "[32 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"),
            L("12:05:00", "You gain experience! (60.000%)"),
            L("12:05:00", "You have slain a ghoul!"),
            L("12:10:00", "You gain experience! (40.000%)"),
            L("12:10:00", "You have slain a ghoul!"),
            L("12:10:00", "You have gained a level! Welcome to level 33!"),
            L("12:11:00", "You have completed the Dungeon Crawl and earned reward loot!"),
            L("12:12:00", Main),
            L("12:13:00", "You have entered Befallen."));
        var v = Assert.Single(s.Closed, v => v.Kills > 0);
        Assert.Equal(("WAR/CLR/RNG", 32, 33), (v.Trio, v.LevelStart, v.LevelEnd));
        Assert.All(s.Closed, c => Assert.True(c.LevelStart <= c.LevelEnd, $"{c.Trio} {c.LevelStart}-{c.LevelEnd}"));
    }

    [Fact]
    public void ACrawlLeftUnfinishedIsClosedAtItsOwnLoadoutsLevel()
    {
        // Left without completing, the crawl waits to resume; /who then shows the main at 50, and entering the next
        // instance closes the crawl. It ended at 32, the level its loadout reached.
        var (_, s) = Run(
            L("12:00:00", "[31 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"),
            L("12:02:00", "You have entered Befallen 4 (Refined)."),
            L("12:03:00", "[31 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"),
            L("12:05:00", "You gain experience! (100.000%)"),
            L("12:05:00", "You have slain a ghoul!"),
            L("12:05:00", "You have gained a level! Welcome to level 32!"),
            L("12:06:00", "You have entered Befallen."),
            L("12:07:00", Main),
            L("12:10:00", "You have entered Kedge Keep 4 (Refined)."));
        var v = Assert.Single(s.Closed);
        Assert.Equal(("WAR/CLR/RNG", 31, 32), (v.Trio, v.LevelStart, v.LevelEnd));
    }

    [Fact]
    public void AReincarnationInsideACrawlKeepsTheCrawlAtTheLevelItWasPlayed()
    {
        var (_, s) = Run(
            L("12:00:00", Main),
            L("12:02:00", "You have entered Befallen 4 (Refined)."),
            L("12:03:00", Main),
            L("12:05:00", "You have slain a ghoul!"),
            L("12:10:00", "You have completed the Dungeon Crawl and earned reward loot!"),
            L("12:11:00", Reinc("Race", "Froglok")),
            L("12:11:00", Reinc("Class", "Paladin")),
            L("12:13:00", "You have entered Befallen."));
        var v = Assert.Single(s.Closed);
        Assert.Equal(("PAL/MNK/SHM", 50, 50), (v.Trio, v.LevelStart, v.LevelEnd));
    }

    [Fact]
    public void AWhoForTheSameLoadoutInsideACrawlKeepsWhereItStarted()
    {
        // Guards the fix: /who after a ding in the crawl is the same loadout, so the crawl still started at 20.
        var (_, s) = Run(
            L("12:00:00", "[20 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"),
            L("12:02:00", "You have entered Befallen 4 (Refined)."),
            L("12:03:00", "[20 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"),   // confirms the crawl
            L("12:05:00", "You gain experience! (100.000%)"),
            L("12:05:00", "You have slain a ghoul!"),
            L("12:05:00", "You have gained a level! Welcome to level 21!"),
            L("12:06:00", "[21 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"),
            L("12:08:00", "You gain experience! (100.000%)"),
            L("12:08:00", "You have slain a ghoul!"),
            L("12:08:00", "You have gained a level! Welcome to level 22!"),
            L("12:09:00", "You have completed the Dungeon Crawl and earned reward loot!"),
            L("12:10:00", "You have entered Befallen."));
        var v = Assert.Single(s.Closed);
        // The ding after the /who still counts: a /who of the same loadout and level must not freeze the crawl.
        Assert.Equal(("WAR/CLR/RNG", 20, 22), (v.Trio, v.LevelStart, v.LevelEnd));
    }

    [Fact]
    public void AWhoForAnotherTrioAtTheSameLevelFreezesTheCrawl()
    {
        // Same level, different loadout: only the trio tells them apart, and the later ding is not this crawl's.
        var (_, s) = Run(
            L("12:00:00", Relevel),
            L("12:02:00", "You have entered Befallen 4 (Refined)."),
            L("12:03:00", Relevel),
            L("12:05:00", "You gain experience! (60.000%)"),
            L("12:05:00", "You have slain a ghoul!"),
            L("12:06:00", "[32 PAL/MNK/SHM] Venun (Froglok) ZONE: Befallen"),
            L("12:10:00", "You have gained a level! Welcome to level 33!"),
            L("12:11:00", "You have completed the Dungeon Crawl and earned reward loot!"),
            L("12:13:00", "You have entered Befallen."));
        var v = Assert.Single(s.Closed, v => v.Kills > 0);
        Assert.Equal(("WAR/CLR/RNG", 32, 32), (v.Trio, v.LevelStart, v.LevelEnd));
    }

    [Fact]
    public void ADingThatJumpsLevelsInsideAConfirmedCrawlFreezesItAtTheLevelItWasPlayed()
    {
        var (_, s) = Run(
            L("12:00:00", Relevel),
            L("12:02:00", "You have entered Befallen 4 (Refined)."),
            L("12:03:00", "[32 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"),
            L("12:05:00", "You gain experience! (60.000%)"),
            L("12:05:00", "You have slain a ghoul!"),
            L("12:10:00", "You have gained a level! Welcome to level 50!"),
            L("12:11:00", "You have completed the Dungeon Crawl and earned reward loot!"),
            L("12:13:00", "You have entered Befallen."));
        var v = Assert.Single(s.Closed, v => v.Kills > 0);
        Assert.Equal(("WAR/CLR/RNG", 32, 32), (v.Trio, v.LevelStart, v.LevelEnd));
    }
}
