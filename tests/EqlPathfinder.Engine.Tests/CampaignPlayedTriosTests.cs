using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Tests;

/// <summary>Report 13: "runs played before this plan" showed the classes the log reset together, not the trio leveled.</summary>
public class CampaignPlayedTriosTests
{
    private static readonly DateTime T = new(2026, 11, 11, 11, 58, 15);

    private static Dictionary<string, int> Ranks(IEnumerable<string> keys, IEnumerable<string> had) =>
        keys.ToDictionary(k => k, k => had.Count(h => h == k));

    private static Journey Played(string race, string trio, DateTime at, DateTime? start = null) =>
        new(trio.Replace("/", " / "), ClassNames.TrioKey(trio.Split('/')), race, at, start ?? at, start ?? at, 1, 50, true, 0, 0, 1, 0, 0, 0, 0, 0, [], null, null, null, []);

    private static Relevel Rel(string race, string trio, DateTime ev, int n, DateTime? start = null) =>
        new(trio, ClassNames.TrioKey(trio.Split('/')), race, ev, 50, true, Played(race, trio, ev, start), n - 1, n, ev);

    private static CampaignView Evaluate(List<ReincarnationEvent> events, List<Relevel> relevels)
    {
        var classes = Ranks(ClassNames.All.Select(c => c.Abbrev), events.SelectMany(e => e.Classes));
        var races = Ranks(ClassNames.Races, events.Select(e => e.Race));
        var plan = new CampaignPlan { Since = T.AddDays(5) };
        return Campaign.Evaluate(plan, classes, races, events, relevels);
    }

    private static string[] Shown(CampaignPlayed p) => [.. p.Gains.Select(g => $"{g.Kind}:{g.Key}:{g.Rank}")];

    [Fact]
    public void PlayedRunsShowTheTrioYouLeveled()
    {
        // The log reset ROG/RNG/WAR with Ogre, then BRD/CLR/DRU with Erudite; you played WAR/CLR/ROG and DRU/BRD/RNG.
        var a = T; var b = T.AddSeconds(162);
        var events = new List<ReincarnationEvent> { new(a, "Ogre", ["ROG", "RNG", "WAR"]), new(b, "Erudite", ["BRD", "CLR", "DRU"]) };
        var v = Evaluate(events, [Rel("Erudite", "WAR/CLR/ROG", a, 1), Rel("Ogre", "DRU/BRD/RNG", b, 2)]);
        Assert.Equal(2, v.Played!.Count);
        Assert.Equal(["WAR", "CLR", "ROG"], v.Played[0].Classes);
        Assert.Equal("Erudite", v.Played[0].Race);
        Assert.Equal(["class:WAR:1", "class:CLR:1", "class:ROG:1", "race:Erudite:1"], Shown(v.Played[0]));
        Assert.Equal(["DRU", "BRD", "RNG"], v.Played[1].Classes);
        Assert.Equal("Ogre", v.Played[1].Race);
        Assert.Equal(["class:DRU:1", "class:BRD:1", "class:RNG:1", "race:Ogre:1"], Shown(v.Played[1]));
        // Every class and race ends on the rank the log counts.
        Assert.Equal(6, v.Played.SelectMany(p => p.Gains).Count(g => g.Kind == "class" && g.Rank == 1));
        Assert.Equal(["Erudite", "Ogre"], v.Played.SelectMany(p => p.Gains).Where(g => g.Kind == "race").Select(g => g.Key));
    }

    [Fact]
    public void AReincarnationYouNeverPlayedKeepsTheLogsGrouping()
    {
        var events = new List<ReincarnationEvent> { new(T, "Wood Elf", ["MNK", "SHM", "MAG"]) };
        var v = Evaluate(events, [new Relevel("MAG/MNK/SHM", "MAG/MNK/SHM", "Wood Elf", T, 50, true, null, null, 1, T)]);
        Assert.Equal(["MNK", "SHM", "MAG"], v.Played!.Single().Classes);
        Assert.Equal("Wood Elf", v.Played![0].Race);
        // No relevel at all: the same.
        Assert.Equal(["MNK", "SHM", "MAG"], Evaluate(events, []).Played!.Single().Classes);
    }

    [Fact]
    public void WhenThePlayedTriosDontAddUpTheLogsGroupingStays()
    {
        // The relevel says WAR/CLR/RNG but the log never reset RNG in this burst: the totals would not match the roster.
        var events = new List<ReincarnationEvent> { new(T, "Ogre", ["ROG", "CLR", "WAR"]) };
        var v = Evaluate(events, [Rel("Ogre", "WAR/CLR/RNG", T, 1)]);
        Assert.Equal(["ROG", "CLR", "WAR"], v.Played!.Single().Classes);
        // Same classes but another race than any the log reset: the race stays too.
        var w = Evaluate(events, [Rel("Kerran", "WAR/CLR/ROG", T, 1)]);
        Assert.Equal(["WAR", "CLR", "ROG"], w.Played!.Single().Classes);
        Assert.Equal("Ogre", w.Played![0].Race);
    }

    [Fact]
    public void ABurstIsNumberedByWhenEachTrioWasPlayed()
    {
        // Three resets seconds apart, played in the order e2, e0, e1 (like the 11-11 burst). A fourth was never played.
        var e0 = new ReincarnationEvent(T, "Human", ["RNG", "DRU", "BRD"]);
        var e1 = new ReincarnationEvent(T.AddSeconds(20), "Ogre", ["NEC", "MAG", "BST"]);
        var e2 = new ReincarnationEvent(T.AddSeconds(40), "Elf", ["PAL", "MNK", "ENC"]);
        var e3 = new ReincarnationEvent(T.AddSeconds(60), "Troll", ["WAR", "CLR", "ROG"]);
        var h = T.AddHours(1);
        var v = Evaluate([e0, e1, e2, e3], [
            Rel("Human", "DRU/BRD/RNG", e0.At, 1, h.AddHours(1)), Rel("Ogre", "BST/MAG/NEC", e1.At, 2, h.AddHours(2)),
            Rel("Elf", "PAL/MNK/ENC", e2.At, 3, h)]);
        // The unplayed reset keeps its reset time, so it comes first in the burst; the played ones follow in play order.
        Assert.Equal([e3.At, e2.At, e0.At, e1.At], v.Played!.Select(p => p.At));
        Assert.Equal([1, 2, 3, 4], v.Played!.Select(p => p.N));
        Assert.Equal(["PAL", "MNK", "ENC"], v.Played![1].Classes);
        Assert.Equal(["DRU", "BRD", "RNG"], v.Played![2].Classes);
        Assert.Equal(["BST", "MAG", "NEC"], v.Played![3].Classes);
        Assert.Equal(12, v.Played!.SelectMany(p => p.Gains).Count(g => g.Kind == "class"));
    }

    [Fact]
    public void ABurstThatDoesntAddUpKeepsTheLogsGroupingAndTheOtherStaysPlayed()
    {
        // Two bursts an hour apart. The first one's relevel says WAR/CLR/RNG for a reset that never had RNG; the second is fine.
        var a = new ReincarnationEvent(T, "Ogre", ["ROG", "CLR", "WAR"]);
        var b = new ReincarnationEvent(T.AddHours(1), "Elf", ["PAL", "MNK", "ENC"]);
        var v = Evaluate([a, b], [Rel("Ogre", "WAR/CLR/RNG", a.At, 1), Rel("Elf", "ENC/MNK/PAL", b.At, 2)]);
        Assert.Equal(["ROG", "CLR", "WAR"], v.Played![0].Classes);
        Assert.Equal(["ENC", "MNK", "PAL"], v.Played![1].Classes);
    }

    [Fact]
    public void ARepeatedClassGetsItsRankOnTheRunPlayedFirst()
    {
        // WAR is reset in both; the second reset's trio was played first, so WAR 1 lands on it and WAR 2 on the other.
        var e0 = new ReincarnationEvent(T, "Human", ["WAR", "CLR", "ROG"]);
        var e1 = new ReincarnationEvent(T.AddSeconds(30), "Ogre", ["WAR", "MNK", "ENC"]);
        var h = T.AddHours(1);
        var v = Evaluate([e0, e1], [Rel("Human", "WAR/CLR/ROG", e0.At, 1, h.AddHours(1)), Rel("Ogre", "WAR/MNK/ENC", e1.At, 2, h)]);
        Assert.Equal([e1.At, e0.At], v.Played!.Select(p => p.At));
        Assert.Contains("class:WAR:1", Shown(v.Played![0]));
        Assert.Contains("class:WAR:2", Shown(v.Played![1]));
    }
}
