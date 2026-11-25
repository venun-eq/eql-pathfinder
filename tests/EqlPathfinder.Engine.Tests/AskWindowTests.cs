using System.Text.Json;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Community;
using EqlPathfinder.Engine.Sharing;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// One per-level table read through a ±1-level window for every Ask answer (docs/ask-backtest.md), never 10-level bands:
/// a band compared one place's play at 21–25 with another's at 26–30.
/// </summary>
public class AskWindowTests
{
    private static readonly DateTime Patch = new(2026, 11, 10, 12, 0, 0), After = Patch.AddDays(2);

    private static LevelingSegment V(string zone, int level, double minutes, double levels, double t5 = 0, int deaths = 0, double dps = 200) =>
        new($"{zone} D4", zone, 4, After, level, minutes, levels, t5, 0, 0, 40, deaths, dps);

    [Fact]
    public void ALevelIsReadWithTheOnesBesideIt()
    {
        // Level 20: 2 visits at 10 minutes; level 21: 1 visit at 16. At 20: (2×2×10 + 1×1×16) / (4 + 1) = 11.2.
        var d = AskData.From([V("Najena", 20, 10, 1), V("Najena", 20, 10, 1), V("Najena", 21, 16, 1)], Patch, After);
        var p = d.Places.Single();
        var w20 = p.Window(20)!;
        Assert.Equal(11.2, w20.Minutes, 3);
        Assert.True(w20.Exact);
        Assert.Equal(2, w20.Visits);
        // Level 22 was never played: it stands in from 21 alone, and says so.
        var w22 = p.Window(22)!;
        Assert.Equal(16, w22.Minutes, 3);
        Assert.False(w22.Exact);
        Assert.Null(p.Window(23));   // two levels away is too far
    }

    [Fact]
    public void AMoteFarmIsOnlyOfferedWherePlayedNearThatLevel()
    {
        // Kedge played from 33 up, Mistmoore up to 32: at 31 only Mistmoore, at 40 only Kedge (the bands offered both).
        var d = AskData.From([
            V("Castle of Mistmoore", 30, 90, 3, t5: 75), V("Castle of Mistmoore", 30, 90, 3, t5: 75),
            V("Kedge Keep", 33, 120, 4, t5: 72), V("Kedge Keep", 36, 120, 4, t5: 72), V("Kedge Keep", 40, 30, 1, t5: 16), V("Kedge Keep", 40, 30, 1, t5: 16),
        ], Patch, After);
        Assert.Equal(["Castle of Mistmoore D4"], Ask.Farms(d, 31, sincePatch: false).Select(b => b.Place));
        Assert.Equal(["Kedge Keep D4"], Ask.Farms(d, 40, sincePatch: false).Select(b => b.Place));
        var kedge = Ask.Farms(d, 40, sincePatch: false).Single();
        Assert.Equal("39-41", kedge.Band);
        Assert.Equal(32.8, kedge.T5PerHour, 1);   // 40: 32 an hour × 2 visits × 2 (nearness), 39: 36 × 1 × 1 → 164 / 5
    }

    [Fact]
    public void RowsAroundALevelCarryEveryRate()
    {
        var d = AskData.From([V("Najena", 20, 30, 1, t5: 10, deaths: 1, dps: 300), V("Najena", 20, 30, 1, t5: 10, deaths: 1, dps: 100)], Patch, After);
        var row = d.Near(20, sincePatch: false).Single();
        Assert.Equal(2, row.LevelsPerHour, 2);
        Assert.Equal(20, row.T5PerHour, 2);
        Assert.Equal(2, row.DeathsPerHour, 2);
        Assert.Equal(200, row.Dps);   // time-weighted across the visits
        Assert.Equal(1, row.Hours, 2);
        Assert.True(Ask.Enough(d, row));
        Assert.Equal(["Najena D4"], d.Near(20, sincePatch: true).Select(r => r.Place));
    }

    [Fact]
    public void TheSharedFileCarriesEveryRatePerLevelAndOlderFilesStillRead()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0);
        SharedCrawl Crawl(int deaths) => new("c" + Guid.NewGuid().ToString("N")[..8], "2026-11-14", 1, "crawl", "Befallen", 4, "WIZ/ENC/CLR", true, "who",
            "Ogre", 0, 10, 11, 1800, true, false, null,
            Enumerable.Range(0, 20).Select(i => new CompactRow($"[{i},1.2,0,8,10]")).ToList(), [], [], Enumerable.Repeat(600, deaths).ToList(),
            new SharedDamage(360000, 1800, 0, new Dictionary<string, long>(), new Dictionary<string, long> { ["Fireball"] = 360000 }),
            new Dictionary<string, int>(), new SharedLoot(new Dictionary<int, int> { [5] = 4 }, new Dictionary<int, int>(), new Dictionary<int, int>(), new Dictionary<int, int>()), End: 0);
        var crawls = Enumerable.Range(0, 3).Select(_ => new PlayerCrawl("a", Crawl(0)))
            .Concat(Enumerable.Range(0, 3).Select(_ => new PlayerCrawl("b", Crawl(1)))).ToList();
        var file = CommunityBuilder.Build(crawls, new CommunityRules(MinPlayers: 2, MinCrawls: 5), now);
        var place = Assert.Single(file.Places);
        Assert.NotNull(place.DeathsPerHour);
        Assert.Equal(1, place.DeathsPerHour![9], 2);   // a 0 an hour, b 2 an hour, equal weight

        var json = JsonSerializer.Serialize(file, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var pool = CommunityData.Parse(json)!.For(null, null)!;
        Assert.True(pool.Ask.Published);
        var row = pool.Ask.Near(10, sincePatch: false).Single();
        Assert.Equal(1, row.DeathsPerHour, 2);
        Assert.Equal(2, pool.PlayersNear("Befallen D4", 10));
        Assert.Single(Ask.Farms(pool.Ask, 10, sincePatch: false));   // published numbers need no further minimum

        // A file from before 11-16 has no extras: minutes and motes still answer.
        var old = file with
        {
            Places = file.Places.Select(p => p with
            {
                Hours = null,
                HighTierPerHour = null,
                FragmentsPerHour = null,
                DeathsPerHour = null,
                KillsPerMinute = null,
                Dps = null
            }).ToList()
        };
        var oldPool = CommunityData.Parse(JsonSerializer.Serialize(old, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }))!.For(null, null)!;
        var oldRow = oldPool.Ask.Near(10, sincePatch: false).Single();
        Assert.Equal(row.LevelsPerHour, oldRow.LevelsPerHour, 2);
        Assert.Equal(0, oldRow.DeathsPerHour);
    }
}
