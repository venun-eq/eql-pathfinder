using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// kills-at-cap: a kill is its slain line, not its XP line (docs/specs/kills-at-cap.md, docs/log-formats.md "Kill XP").
/// At 50 the game writes "You gain experience!" with no (x%) once the bar can't move, and nothing at all for a mob that
/// gives no XP; a quest hand-in writes an XP line, with or without (x%), and no slain line.
/// </summary>
public class KillsAtCapTests
{
    private const string AaCap = "You have reached the AA point cap, and cannot gain any further experience until some of your stored AA point pool is used.";

    private static (PathfinderEngine e, TrackerTests.Sink s) Run(params string[] lines)
    {
        var s = new TrackerTests.Sink();
        var e = new PathfinderEngine("Venun", new TrackerOptions(), s);
        foreach (var l in lines) e.ProcessLine(l);
        e.Flush();
        return (e, s);
    }

    private static string L(string hms, string body) => $"[Sun Nov 15 {hms} 2026] {body}";

    /// <summary>A crawl at <paramref name="level"/>: in at 10:00, the lines, out at 11:00.</summary>
    private static string[] Crawl(int level, params string[] lines) =>
    [
        L("09:59:00", $"You have gained a level! Welcome to level {level}!"),
        L("10:00:00", "You have entered Nektropos Castle 4 (Refined)."),
        .. lines,
        L("11:00:00", "You have entered Nektropos Castle."),
    ];

    [Fact]
    public void AKillAtTheCapWithNoXpLineCounts()
    {
        var (_, s) = Run(Crawl(50,
            L("10:01:00", "You have slain a ghoul!"),
            L("10:03:00", "You have slain a ghoul!")));
        var v = Assert.Single(s.Closed);
        Assert.Equal(2, v.Kills);
        Assert.Equal(2, v.KillTimes.Count);
        Assert.Equal(2, s.Kills.Count(k => k.VisitId == v.Id));
        Assert.All(s.Kills, k => { Assert.Equal(50, k.Level); Assert.Equal(0, k.Percent); Assert.Equal("a ghoul", k.Mob); });
    }

    [Fact]
    public void AKillWhoseXpLineHasNoPercentCountsOnceInEitherOrder()
    {
        var (_, s) = Run(Crawl(50,
            L("10:01:00", AaCap),
            L("10:01:00", "You gain experience!"),
            L("10:01:00", "You have slain a ghoul!"),
            L("10:02:00", "You have slain a ghoul!"),   // older clients: the slain line first
            L("10:02:01", "You gain experience!")));
        var v = Assert.Single(s.Closed);
        Assert.Equal(2, v.Kills);
        Assert.Equal(2, s.Kills.Count);
    }

    [Fact]
    public void InTheOpenWorldAnotherPlayersKillCountsOnlyWithPartyXp()
    {
        var (_, s) = Run(
            L("09:59:00", "You have gained a level! Welcome to level 50!"),
            L("10:00:00", "You have entered West Karana."),
            L("10:01:00", "You gain party experience!"),
            L("10:01:00", "A ghoul has been slain by Groupmate!"),
            L("10:03:00", "A ghoul has been slain by Stranger!"),    // no party XP: someone else's kill
            L("10:05:00", "You have slain a ghoul!"));
        Assert.Equal(2, s.Kills.Count);
        Assert.All(s.Kills, k => Assert.Null(k.VisitId));
        Assert.Single(s.Kills, k => k.Party);
    }

    [Fact]
    public void InsideAnInstanceEveryMobSlainCountsOnce()
    {
        // The user (11-18): everyone inside an instance is in your group, so a groupmate's kill counts with or without party
        // XP. A groupmate's death is a slain line too, and isn't a kill.
        var (_, s) = Run(Crawl(50,
            L("10:01:00", "You gain party experience!"),
            L("10:01:00", "A ghoul has been slain by Groupmate!"),
            L("10:03:00", "A ghoul has been slain by Groupmate!"),    // a mob that gave no XP
            L("10:05:00", "Groupmate has been slain by a ghoul!"),
            L("10:06:00", "You have slain a ghoul!")));
        var v = Assert.Single(s.Closed);
        Assert.Equal(3, v.Kills);
        Assert.Equal(3, s.Kills.Count(k => k.VisitId == v.Id));
        Assert.DoesNotContain(s.Kills, k => k.Mob == "Groupmate");
    }

    /// <summary>A crawl at 20: ten kills of 1%, two that gave no XP line, three whose XP line has no (x%), finished.</summary>
    private static string[] MixedKills()
    {
        var lines = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            lines.Add(L($"10:{1 + i:00}:00", "You gain experience! (1.000%)"));
            lines.Add(L($"10:{1 + i:00}:00", "You have slain a ghoul!"));
        }
        lines.Add(L("10:21:00", "You have slain a rat!"));
        lines.Add(L("10:22:00", "You have slain a rat!"));
        for (int i = 0; i < 3; i++)
        {
            lines.Add(L($"10:{31 + i:00}:00", "You gain experience!"));
            lines.Add(L($"10:{31 + i:00}:00", "You have slain a ghoul!"));
        }
        lines.Add(L("10:40:00", "You have completed the Dungeon Crawl and earned reward loot!"));   // a crawl left unfinished stays open to go back in
        return Crawl(20, [.. lines]);
    }

    [Fact]
    public void XpPerKillCountsAKillThatGaveNoXpAsZeroAndLeavesOutOneWhoseXpIsntInTheLog()
    {
        var o = new TrackerOptions();
        var (_, s) = Run(MixedKills());
        var v = Assert.Single(s.Closed);
        Assert.Equal(15, v.Kills);
        var run = Analysis.LevelingSegment.From(VisitStats.From(v, v.EndTime, o));
        Assert.Equal(15, run.Kills);
        // 10% of a level over the 12 kills whose XP is known: the 2 that gave none are 0s, the 3 unknown are left out.
        Assert.Equal(10.0 / 12, Analysis.RunPace.For(run, []).XpPerKill!.Value, 3);
    }

    [Fact]
    public void SharedKillRowsSayZeroForNoXpAndNullForXpNotInTheLog()
    {
        var (e, s) = Run(MixedKills());
        var p = Sharing.ShareBuilder.Build("0.7.1", "oggok", "Venun_oggok", new byte[16], s.Closed, [], s.Kills, [], e.State, new TrackerOptions());
        var xp = Assert.Single(p.Crawls).Kills.Select(r => System.Text.Json.Nodes.JsonNode.Parse(r.Json)!.AsArray()[1]).ToList();
        Assert.Equal(15, xp.Count);
        Assert.Equal(10, xp.Count(x => x is not null && (double)x == 1));
        Assert.Equal(2, xp.Count(x => x is not null && (double)x == 0));
        Assert.Equal(3, xp.Count(x => x is null));
    }

    [Fact]
    public void TheCommunityJobLeavesKillsWithUnknownXpOutOfXpPerKill()
    {
        // Kill rows are [t, xp, party, secs, level]: ten of 1%, five that gave none, five whose XP the log didn't give.
        var rows = Enumerable.Range(0, 20).Select(i => new Sharing.CompactRow($"[{i * 60},{(i < 10 ? "1" : i < 15 ? "0" : "null")},0,8,20]")).ToList();
        var c = new Sharing.SharedCrawl("c1", "2026-11-14", 1, "crawl", "Befallen", 4, "WIZ/ENC/CLR", true, "who", "Ogre", 0, 20, 20,
            30 * 60, true, false, null, rows, [], [], [],
            new Sharing.SharedDamage(1000, 900, 0, new Dictionary<string, long>(), new Dictionary<string, long> { ["Fireball"] = 1000 }),
            new Dictionary<string, int>(),
            new Sharing.SharedLoot(new Dictionary<int, int> { [5] = 4 }, new Dictionary<int, int>(), new Dictionary<int, int>(), new Dictionary<int, int>()),
            new Sharing.CrawlDerived(0.1, 0.1, 0, 30, 0));
        var run = Community.CommunityBuilder.Visit(c, out var why);
        Assert.True(run != null, why);
        Assert.Equal(20, run!.Kills);
        Assert.Equal(10.0 / 15, Analysis.RunPace.For(run, []).XpPerKill!.Value, 3);
    }

    [Fact]
    public void TwoKillsInOneSecondCountTwiceWithOneXpLine()
    {
        var (_, s) = Run(Crawl(50,
            L("10:01:00", AaCap),
            L("10:01:00", "You gain experience!"),
            L("10:01:00", "You have slain a ghoul!"),
            L("10:01:00", "You have slain a ghoul!"),    // an add that gave no XP
            L("10:04:00", "You have slain a ghoul!")));
        var v = Assert.Single(s.Closed);
        Assert.Equal(3, v.Kills);
        Assert.Equal(3, s.Kills.Count);
    }

    [Fact]
    public void AnXpLineWithNoSlainLineIsAQuestRewardNotAKill()
    {
        var (e, s) = Run(Crawl(20,
            L("10:01:00", "You gain experience! (2.500%)"),
            L("10:03:00", "You gain experience!"),
            L("10:05:00", "You gain experience! (3.000%)"),
            L("10:05:00", "You have slain a ghoul!")));
        var v = Assert.Single(s.Closed);
        Assert.Equal(1, v.Kills);
        var k = Assert.Single(s.Kills);
        Assert.Equal(3.0, k.Percent, 3);
        Assert.Equal(5.5, e.State.KillPercentInLevel, 3);   // the reward still fills the bar: a crawl's completion XP is sized from it
    }

    [Fact]
    public void IdleAtTheCapIsMeasuredBetweenKills()
    {
        var o = new TrackerOptions();
        var (_, s) = Run(Crawl(50,
            L("10:01:00", "You have slain a ghoul!"),
            L("10:02:00", "You have slain a ghoul!"),
            L("10:03:00", "You have slain a ghoul!"),
            L("10:23:00", "You have slain a ghoul!"),   // 20 minutes away from the keyboard (30 silent minutes would end the visit)
            L("10:24:00", "You have slain a ghoul!")));
        var v = Assert.Single(s.Closed);
        Assert.True(v.IdleSeconds(o.IdleThresholdSeconds, v.EndTime) >= 15 * 60,   // the 20-minute gap less the 3-minute threshold (the 36 silent minutes after it end the visit at its last kill)
            "the time between kills at the cap counts as idle, as it does below the cap");
    }

    [Fact]
    public void NoPageSaysKillsAtTheCapArentCounted()
    {
        // The overlay is the window's own code, and Crawls has no page test yet (docs/quality.md): this protects the removal
        // of one-rate's at-cap note and tip, "No rate: kills at level 50 aren't counted yet.", now that those kills count.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src", "EqlPathfinder.App"))) dir = dir.Parent;
        Assert.NotNull(dir);
        foreach (var file in new[] { "OverlayWindow.xaml.cs", Path.Combine("web", "js", "crawls.js") })
        {
            var text = File.ReadAllText(Path.Combine(dir!.FullName, "src", "EqlPathfinder.App", file));
            Assert.True(text.Length > 1000, $"{file} wasn't read");
            Assert.DoesNotContain("aren't counted yet", text);
        }
    }

    [Fact]
    public void AVisitBelowTheCapWhereNoKillMovedTheBarIsLeftOutOfRelevelsAndSharing()
    {
        // The stopgap until swap-zones (docs/specs/kills-at-cap.md): a visit filed at 30 whose kills all had XP lines with no (x%)
        // was played on a loadout at the cap, swapped in between a relevel's dings. History keeps it; leveling runs and sharing don't.
        var o = new TrackerOptions();
        var swapped = Crawl(30,
            L("10:01:00", "You gain experience!"), L("10:01:00", "You have slain a ghoul!"),
            L("10:02:00", "You gain experience!"), L("10:02:00", "You have slain a ghoul!"));
        var grey = Crawl(30, L("10:01:00", "You have slain a rat!"), L("10:02:00", "You have slain a rat!"));   // no XP lines at all: not touched
        var (e, s) = Run(swapped);
        var v = Assert.Single(s.Closed);                       // still a visit, so History shows it
        Assert.Equal(2, v.Kills);
        Assert.True(v.PlayedOnACapLoadout(o.LevelCap));
        Assert.Empty(Analysis.PlayFacts.From(s.Closed, e.State).Segments([], [], [], o).Segments);
        var p = Sharing.ShareBuilder.Build("0.7.1", "oggok", "Venun_oggok", new byte[16], s.Closed, [], s.Kills, [], e.State, o);
        Assert.Empty(p.Crawls);

        var (e2, s2) = Run(grey);
        Assert.False(Assert.Single(s2.Closed).PlayedOnACapLoadout(o.LevelCap));
        Assert.Single(Analysis.PlayFacts.From(s2.Closed, e2.State).Segments([], [], [], o).Segments);
        Assert.Single(Sharing.ShareBuilder.Build("0.7.1", "oggok", "Venun_oggok", new byte[16], s2.Closed, [], s2.Kills, [], e2.State, o).Crawls);
    }

    [Fact]
    public void AnOpenWorldHuntWhoseKillsAllHadNoPercentIsLeftOutOfRelevelsAndSharingLikeAVisit()
    {
        // The same stopgap rule as Visit.PlayedOnACapLoadout, outside an instance: at 30, every kill moves the bar, so XP lines
        // with no (x%) mean the loadout played was at the cap.
        var o = new TrackerOptions();
        var swapped = new[] { L("09:59:00", "You have gained a level! Welcome to level 30!"),
            L("10:01:00", "You have slain a ghoul!"), L("10:01:00", "You gain experience!"),
            L("10:02:00", "You have slain a ghoul!"), L("10:02:00", "You gain experience!") };
        var (e, s) = Run(swapped);
        Assert.Equal(2, s.Kills.Count);
        Assert.Empty(Analysis.FieldHunts.From(s.Kills, o.LevelCap));
        Assert.Empty(Analysis.PlayFacts.From(s.Closed, e.State).Segments(s.Kills, [], [], o).Segments);
        Assert.Empty(Sharing.ShareBuilder.Build("0.7.1", "oggok", "Venun_oggok", new byte[16], s.Closed, [], s.Kills, [], e.State, o).Stretches);

        var grey = new[] { L("09:59:00", "You have gained a level! Welcome to level 30!"),
            L("10:01:00", "You have slain a rat!"), L("10:02:00", "You have slain a rat!") };
        var (_, s2) = Run(grey);
        Assert.Single(Analysis.FieldHunts.From(s2.Kills, o.LevelCap));   // no XP lines at all: not touched
    }

    [Fact]
    public void AHandInThatStopsBeingAKillDoesntChangeTheVisitsLevels()
    {
        var (_, s) = Run(Crawl(20,
            L("10:01:00", "You gain experience! (2.000%)"),
            L("10:05:00", "You gain experience! (3.000%)"),
            L("10:05:00", "You have slain a ghoul!")));
        var v = Assert.Single(s.Closed);
        Assert.Equal(0.05, v.LevelsGained, 6);   // the reward's 2% is still in the visit's levels, as a reward
        Assert.Equal(1, v.Kills);
    }
}
