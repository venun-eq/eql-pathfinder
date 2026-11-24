using System.Text.Json;
using EqlPathfinder.Engine.Community;
using EqlPathfinder.Engine.Sharing;

namespace EqlPathfinder.Engine.Tests;

/// <summary>Schema 2 of the community file: open-world hunting joins the tables, and solo and grouped play get their own.</summary>
public class CommunitySplitTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 12, 0, 0);
    private static readonly CommunityRules Rules = new(MinPlayers: 2, MinCrawls: 5);

    // 20 kills spread over the time, with the levels' XP between them: the job works levels and time out from these rows.
    private static List<CompactRow> Kills(int level, bool grouped, double minutes = 30, double levels = 1) =>
        Enumerable.Range(0, 20).Select(i => new CompactRow(FormattableString.Invariant($"[{Math.Round(i * (minutes - 1) * 60 / 19)},{levels * 5},{(grouped ? 1 : 0)},8,{level}]"))).ToList();

    private static SharedCrawl Crawl(string zone, int level, bool grouped, string day = "2026-11-14", int seq = 1, string trio = "WIZ/ENC/CLR",
        double minutes = 30, double levels = 1) =>
        new("c" + Guid.NewGuid().ToString("N")[..8], day, seq, "crawl", zone, 4, trio, true, "who", "Ogre", 0, level, level + (int)levels,
            minutes * 60, true, false, null, Kills(level, grouped, minutes, levels), [], [], [],
            new SharedDamage(1000, minutes * 30, 0, new Dictionary<string, long>(), new Dictionary<string, long> { ["Fireball"] = 1000 }),
            new Dictionary<string, int>(), new SharedLoot(new Dictionary<int, int> { [5] = 4 }, new Dictionary<int, int>(), new Dictionary<int, int>(), new Dictionary<int, int>()), new CrawlDerived(levels, levels, 0, minutes, 0));

    private static SharedStretch Stretch(string zone, int level, string day = "2026-11-14", int seq = 2, double minutes = 30, double levels = 1.5,
        bool grouped = false) => new("s" + Guid.NewGuid().ToString("N")[..8], day, seq, zone, Kills(level, grouped, minutes, levels), new StretchDerived(minutes, levels));

    private static (List<PlayerCrawl>, List<PlayerStretch>) TwoPlayers(Func<string, IEnumerable<SharedCrawl>> crawls, Func<string, IEnumerable<SharedStretch>> stretches) =>
        (new[] { "p1", "p2" }.SelectMany(p => crawls(p).Select(c => new PlayerCrawl(p, c))).ToList(),
         new[] { "p1", "p2" }.SelectMany(p => stretches(p).Select(s => new PlayerStretch(p, s))).ToList());

    [Fact]
    public void OpenWorldHuntingBecomesItsOwnPlace()
    {
        var (crawls, stretches) = TwoPlayers(_ => [Crawl("Befallen", 20, false)],
            _ => Enumerable.Range(0, 3).Select(_ => Stretch("The Ruins of Old Guk", 20)));
        var f = CommunityBuilder.Build(crawls, stretches, Rules, Now);
        Assert.Equal(2, f.Schema);
        Assert.Equal(6, f.Stretches);
        var open = Assert.Single(f.Places, p => p.Place == "Ruins of Old Guk · open world");
        Assert.InRange(open.Minutes[19]!.Value, 19, 21);   // 30 minutes for 1.5 levels: about 20 a level

        var c = CommunityData.Parse(JsonSerializer.Serialize(f, CommunityBuilder.Json))!;
        Assert.Equal(["Ruins of Old Guk · open world"], c.For("open")!.Ask.Places.Select(p => p.Place));
        Assert.DoesNotContain(c.For("instances")!.Ask.Places, p => p.Place.EndsWith(" · open world"));
    }

    [Fact]
    public void AStretchTakesItsTrioAndRankFromTheNearestCrawlAndKeepsTheDddRule()
    {
        var bard = Crawl("Befallen", 20, false, day: "2026-11-09", seq: 5, trio: "BRD/DRU/RNG");
        Assert.Null(CommunityBuilder.Hunt(Stretch("The Ruins of Old Guk", 20, day: "2026-11-09", seq: 6), [bard]));   // pre-nerf, on a bard
        Assert.Null(CommunityBuilder.Hunt(Stretch("The Ruins of Old Guk", 20, day: "2026-11-09", seq: 6), []));       // pre-nerf, unknown trio
        var wiz = Crawl("Befallen", 20, false, day: "2026-11-14", seq: 5, trio: "WIZ/ENC/CLR");
        var hunt = CommunityBuilder.Hunt(Stretch("The Ruins of Old Guk", 21, day: "2026-11-14", seq: 6, grouped: true), [wiz])!;
        Assert.Equal(("WIZ/ENC/CLR", 21, "open world", true), (hunt.Trio, hunt.LevelStart, hunt.Type, hunt.Grouped));
        // A hunt during a bonus-XP event is left out, as a crawl is.
        Assert.Null(CommunityBuilder.Hunt(Stretch("The Ruins of Old Guk", 21, day: "2026-11-14", seq: 6) with { Bonus = true }, [wiz]));
    }

    [Fact]
    public void AStretchWithItsOwnTrioUsesItAndTheDddRuleStillApplies()
    {
        // 0.9.30+: the app's trio and rank for the hunt, even with no shared crawl that day (the old way dropped these).
        var own = Stretch("The Ruins of Old Guk", 21, day: "2026-11-14", seq: 6) with { Trio = "WIZ/ENC/CLR", Rank = 2 };
        var hunt = CommunityBuilder.Hunt(own, [])!;
        Assert.Equal(("WIZ/ENC/CLR", 2), (hunt.Trio, hunt.Rank));
        // Its own trio wins over a crawl nearby.
        var other = Crawl("Befallen", 20, false, day: "2026-11-14", seq: 5, trio: "PAL/MNK/ENC");
        Assert.Equal("WIZ/ENC/CLR", CommunityBuilder.Hunt(own, [other])!.Trio);
        // Before the nerf, a hunt the app put on a bard trio is still left out.
        Assert.Null(CommunityBuilder.Hunt(Stretch("The Ruins of Old Guk", 20, day: "2026-11-09", seq: 6) with { Trio = "BRD/DRU/RNG", Rank = 1 }, []));
    }

    [Fact]
    public void SoloAndGroupedPlayGetTheirOwnTables()
    {
        var (crawls, stretches) = TwoPlayers(_ => Enumerable.Range(0, 3).Select(_ => Crawl("Befallen", 10, grouped: true))
            .Concat(Enumerable.Range(0, 3).Select(_ => Crawl("The Estate of Unrest", 22, grouped: false))), _ => []);
        var f = CommunityBuilder.Build(crawls, stretches, Rules, Now);
        Assert.Equal(["Befallen D4"], f.Group!.Places.Select(p => p.Place));
        Assert.Equal(["Estate of Unrest D4"], f.Solo!.Places.Select(p => p.Place));
        Assert.Equal(2, f.Places.Count);   // all play holds both

        var c = CommunityData.Parse(JsonSerializer.Serialize(f, CommunityBuilder.Json))!;
        Assert.Equal(["Befallen D4"], c.For(with: "group")!.Ask.Places.Select(p => p.Place));
        // A schema 1 file can't answer either: the page says what it lacks instead.
        var old = CommunityData.Parse(JsonSerializer.Serialize(f with { Schema = 1, Solo = null, Group = null }, CommunityBuilder.Json))!;
        Assert.Null(old.For(with: "solo"));
        Assert.Null(old.For("open"));
        Assert.NotNull(old.For());
    }
}
