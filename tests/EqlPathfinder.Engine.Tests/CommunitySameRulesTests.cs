using System.Text.Json;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Community;
using EqlPathfinder.Engine.Sharing;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The community job works a shared crawl out by the app's own rules (docs/specs/community-same-rules.md): the same
/// active time, the same damage per second over it, and the same gate for which visits rank a place.
/// </summary>
public class CommunitySameRulesTests
{
    private sealed class Sink : ITrackerSink
    {
        public List<Visit> Closed { get; } = new();
        public List<KillRecord> Kills { get; } = new();
        public List<LevelRecord> Levels { get; } = new();
        public void OnVisitClosed(Visit v) => Closed.Add(v);
        public void OnKill(KillRecord k) => Kills.Add(k);
        public void OnLevel(LevelRecord l) => Levels.Add(l);
    }

    private static string L(string hms, string body) => $"[Wed Nov 11 {hms} 2026] {body}";

    private static readonly DateTime Now = new(2026, 11, 16, 12, 0, 0);

    /// <summary>A shared crawl at Befallen D4 with <paramref name="kills"/> solo kills a minute apart, worth one level in all.</summary>
    private static SharedCrawl Crawl(string player, int level, double minutes, int kills, bool completed, long damage = 60_000) =>
        new("c" + player + Guid.NewGuid().ToString("N")[..8], "2026-11-14", 1, "crawl", "Befallen", 4, "WIZ/ENC/CLR", true, "who", "Ogre", 0, level,
            level + 1, minutes * 60, completed, false, null,
            Enumerable.Range(0, kills).Select(i => new CompactRow(FormattableString.Invariant($"[{i * 60},{100.0 / kills},0,8,{level}]"))).ToList(), [], [], [],
            new SharedDamage(damage, minutes * 30, 0, new Dictionary<string, long>(), new Dictionary<string, long> { ["Fireball"] = damage }),
            new Dictionary<string, int>(),
            new SharedLoot(new Dictionary<int, int> { [5] = 4 }, new Dictionary<int, int>(), new Dictionary<int, int>(), new Dictionary<int, int>()),
            End: 0);

    [Fact]
    public void OneCrawlReadsTheSameThroughTheJobAsInTheApp()
    {
        // A finished crawl with an eight-minute gap between its second and third kills: five minutes of it are idle.
        var o = new TrackerOptions();
        var s = new Sink();
        var e = new PathfinderEngine("Standin", o, s);
        foreach (var l in new[]
        {
            L("12:00:00", "You have gained the ability \"Class Reincarnation: Paladin\" at a cost of 0 ability points."),
            L("12:00:00", "You have gained the ability \"Class Reincarnation: Monk\" at a cost of 0 ability points."),
            L("12:00:00", "You have gained the ability \"Class Reincarnation: Enchanter\" at a cost of 0 ability points."),
            L("12:00:01", "[10 PAL/MNK/ENC] Standin (Iksar) ZONE: Befallen"),
            L("12:00:10", "You have entered Befallen 4 (Refined)."),
            L("12:01:00", "You crush a skeleton for 500 points of damage."),
            L("12:01:00", "You gain experience! (10.000%)"),
            L("12:01:00", "You have slain a skeleton!"),
            L("12:02:00", "You crush a skeleton for 500 points of damage."),
            L("12:02:00", "You gain experience! (10.000%)"),
            L("12:02:00", "You have slain a skeleton!"),
            L("12:02:05", "You looted 3 Mote of Major Potential from a skeleton's corpse and stored it in your currency"),
            L("12:10:00", "You crush a skeleton for 500 points of damage."),
            L("12:10:00", "You gain experience! (10.000%)"),
            L("12:10:00", "You have slain a skeleton!"),
            L("12:11:00", "You have completed the Dungeon Crawl and earned reward loot!"),
            L("12:12:00", "You have entered Befallen."),
            L("12:13:00", "You have entered Befallen 4 (Refined)."),
        }) e.ProcessLine(l);
        e.Flush();

        var visit = s.Closed.First(v => v.Completed);
        var app = LevelingSegment.From(VisitStats.From(visit, visit.Start.AddHours(1), o));
        var shared = Assert.Single(ShareBuilder.Build("0.7.1", "oggok", "Standin_oggok", new byte[16], s.Closed, [], s.Kills, s.Levels, e.State, o).Crawls,
            c => c.Completed);
        var job = CommunityBuilder.Visit(shared, out var why);
        Assert.True(job != null, why);

        Assert.True(app.Dps > 0, "the app saw no damage: the test's log lines are wrong, not the job");
        Assert.True(app.T5 > 0, "the app saw no loot: the test's log lines are wrong, not the job");
        Assert.Equal(app.ActiveMinutes, job!.ActiveMinutes, 3);
        Assert.Equal(app.Levels, job.Levels, 3);
        Assert.Equal(app.Kills, job.Kills);
        Assert.Equal(app.Deaths, job.Deaths);
        Assert.Equal(app.Completed, job.Completed);
        Assert.Equal(app.Dps, job.Dps, 1);
        // The job publishes motes and fragments apart; at the player's own fragment value they add up to the app's T5-eq.
        Assert.Equal(app.T5, job.Loot!.MoteT5 + o.FragmentValue * job.Loot.FragmentT5, 3);
    }

    [Fact]
    public void TheJobsDamagePerSecondDividesByActiveTime()
    {
        // 30 minutes inside, kills a minute apart from the start, and the last at 19 minutes: 11 minutes to the end,
        // 8 of them past the 3-minute idle threshold. 22 active minutes.
        var c = Crawl("a", 10, 30, 20, completed: true, damage: 66_000) with { End = 1800 };
        var v = CommunityBuilder.Visit(c)!;
        Assert.Equal(22, v.ActiveMinutes, 3);
        Assert.Equal(66_000 / (22 * 60.0), v.Dps, 1);   // 50 a second; time inside would give 36.7

        // An unfinished crawl under eight kills has no rate in the app, so its damage counts nowhere (0, as the app's null).
        var early = Crawl("a", 10, 30, 5, completed: false) with { End = 1800 };
        Assert.Equal(0, CommunityBuilder.Visit(early)!.Dps);
    }

    [Fact]
    public void VisitsWithNoRateAreLeftOutOfThePoolsPlacesAndItsBands()
    {
        // Two players, three unfinished in-and-outs each with five kills: no rate in either player's own Ask.
        var crawls = new List<PlayerCrawl>();
        foreach (var p in new[] { "a", "b" })
            for (int i = 0; i < 3; i++) crawls.Add(new PlayerCrawl(p, Crawl(p, 10, 20, 5, completed: false)));
        var stats = new CommunityStats();
        var file = CommunityBuilder.Build(crawls, [], new CommunityRules(MinPlayers: 2, MinCrawls: 5), Now, stats);
        Assert.Empty(file.Places);
        // The bands leave them out too (docs/specs/sample-gates.md, the user 11-19: the hold from one-usual ended with kills at the cap).
        Assert.DoesNotContain(file.Bands, b => b.Place == "Befallen D4");
        Assert.Equal(6, stats.LeftOut["no rate"]);

        // A finished crawl has a rate whatever its kills: five-kill finished crawls still rank the place.
        var finished = crawls.Select(x => x with { Crawl = x.Crawl with { Completed = true } }).ToList();
        Assert.NotNull(Assert.Single(CommunityBuilder.Build(finished, new CommunityRules(MinPlayers: 2, MinCrawls: 5), Now).Places).Minutes[9]);
    }

    [Fact]
    public void ASharersPlayComesOutByTheRulesItWentInBy()
    {
        // You share three in-and-outs with no rate at level 10; a and b finished crawls there. Read without you, the pool
        // is a's and b's alone: your visits come out by the same gate that put them in (CommunityView.Without).
        var others = new List<PlayerCrawl>();
        foreach (var (p, minutes) in new[] { ("a", 20.0), ("b", 40.0) })
            for (int i = 0; i < 3; i++) others.Add(new PlayerCrawl(p, Crawl(p, 10, minutes, 20, completed: true)));
        var mine = Enumerable.Range(0, 3).Select(_ => Crawl("me", 10, 90, 5, completed: false)).ToList();
        var rules = new CommunityRules(MinPlayers: 2, MinCrawls: 5);

        var withMe = CommunityBuilder.Build(others.Concat(mine.Select(c => new PlayerCrawl("me", c))), rules, Now);
        var withoutMe = CommunityBuilder.Build(others, rules, Now);
        // Your segments as your own app has them: finished or not is known.
        var yours = mine.Select(c => CommunityBuilder.Visit(c)! with { Completed = c.Completed }).ToList();
        var read = CommunityData.Parse(JsonSerializer.Serialize(withMe, CommunityBuilder.Json))!.LeavingOut(yours, DateTime.MinValue);

        var expected = Assert.Single(withoutMe.Places).Minutes[9];
        Assert.NotNull(expected);
        var got = read.For(fragmentValue: 3.56)!.Ask.Places.Single(p => p.Place == "Befallen D4").Minutes[9];
        Assert.True(got is { } g && Math.Abs(g - expected!.Value) < 0.05,
            $"level 10 without you: expected a's and b's {expected} minutes, got {got?.ToString() ?? "nothing"}");
    }

    [Fact]
    public void KillsAtTheCapAddNoXpAndAreNotCountedForXpPerKill()
    {
        // Ten kills at level 49 worth 10% each, ten at 50 worth 10% each: the app counts XP only below the cap.
        var c = Crawl("a", 49, 20, 20, completed: true) with
        {
            Kills = Enumerable.Range(0, 20).Select(i => new CompactRow(FormattableString.Invariant($"[{i * 60},10,0,8,{(i < 10 ? 49 : 50)}]"))).ToList(),
            Rewards = [],
        };
        var (_, killLevels, _) = CommunityBuilder.Recompute(c);
        Assert.Equal(1.0, killLevels, 3);
        var seg = CommunityBuilder.Visit(c)!;
        Assert.Equal(20, seg.Kills);
        Assert.Equal(10, seg.XpKills);
    }
}
