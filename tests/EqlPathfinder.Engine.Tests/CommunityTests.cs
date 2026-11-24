using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using EqlPathfinder.Engine.Community;
using EqlPathfinder.Engine.Sharing;

namespace EqlPathfinder.Engine.Tests;

public class CommunityTests
{
    private static SharedCrawl Crawl(string day = "2026-11-14", string zone = "Befallen", int? diff = 4, int level = 10, double levels = 1,
        double minutes = 30, Dictionary<int, int>? motes = null, Dictionary<int, int>? frags = null, Dictionary<string, long>? damage = null)
    {
        var dmg = damage ?? new Dictionary<string, long> { ["Fireball"] = 1000 };
        return new SharedCrawl("c" + Guid.NewGuid().ToString("N")[..8], day, 1, "crawl", zone, diff, "WIZ/ENC/CLR", true, "who", "Ogre", 0, level,
            level + (int)levels, minutes * 60, true, false, null,
            Enumerable.Range(0, 20).Select(i => new CompactRow(FormattableString.Invariant($"[{i * 60},{levels * 5},{(i < 5 ? 1 : 0)},8,{level}]"))).ToList(), [], [], [],
            new SharedDamage(dmg.Values.Sum(), minutes * 30, 0, new Dictionary<string, long>(), dmg), new Dictionary<string, int>(),
            new SharedLoot(motes ?? new() { [5] = 4 }, frags ?? new(), new Dictionary<int, int>(), new Dictionary<int, int>()),
            new CrawlDerived(levels, levels, 0, minutes, 0));
    }

    [Fact]
    public void NoPlayerOutweighsTheCap()
    {
        Assert.Equal([3.0, 3.0], CommunityBuilder.CappedWeights([30, 3], 0.2)); // two players: equal shares
        var w = CommunityBuilder.CappedWeights([100, 1, 1, 1, 1, 1], 0.2);
        Assert.Equal(0.2, w[0] / w.Sum(), 6);
        Assert.Equal(1, w[1]);
        Assert.Equal([5.0, 4.0, 3.0, 2.0, 1.0, 1.0], CommunityBuilder.CappedWeights([5, 4, 3, 2, 1, 1], 0.5)); // no one over half
    }

    [Fact]
    public void PreNerfDddRunsAreLeftOut()
    {
        var ddd = new Dictionary<string, long> { ["Denon's Desperate Dirge Rk. II"] = 900, ["Melee"] = 100 };
        Assert.Null(CommunityBuilder.Visit(Crawl(day: "2026-11-02", damage: ddd)));
        Assert.Null(CommunityBuilder.Visit(Crawl(day: "2026-11-10", damage: ddd)));   // the patch day counts as before
        Assert.NotNull(CommunityBuilder.Visit(Crawl(day: "2026-11-11", damage: ddd))); // DDD after the nerf counts
        Assert.NotNull(CommunityBuilder.Visit(Crawl(day: "2026-11-02")));
    }

    [Fact]
    public void PlayDuringABonusXpEventIsLeftOut()
    {
        // The bonus makes a place look faster than it gives: the app marks it, and the job leaves it out.
        Assert.NotNull(CommunityBuilder.Visit(Crawl()));
        Assert.Null(CommunityBuilder.Visit(Crawl() with { Bonus = true }, out var why));
        Assert.Equal("during a bonus-XP event", why);
    }

    [Fact]
    public void FragmentsCountAtSpellValue()
    {
        var v = CommunityBuilder.Visit(Crawl(motes: new() { [5] = 4, [6] = 1 }, frags: new() { [5] = 3, [8] = 1 }))!;
        Assert.Equal(4 + 2 + 3 * 2 + 8 * 2, v.T5, 6);
        Assert.Equal(2 + 8 * 2, v.HighTierT5, 6);
        Assert.Equal(4, v.Fragments);
        Assert.Equal(0.25, v.PartyShare, 6); // 5 of 20 kills gave party experience
        Assert.Equal("crawl", v.Type);
    }

    [Fact]
    public void SchemaTwoUnitsStillRead()
    {
        // Before schema 3 a crawl had no trioSource or casts.
        var json = """{"id":"x","day":"2026-11-14","seq":1,"kind":"crawl","zone":"Befallen","difficulty":4,"trio":"WIZ","trioConfirmed":true,"rank":0,"levelStart":10,"levelEnd":11,"activeSeconds":1800,"completed":true,"refunded":false,"kills":[],"rewards":[],"dings":[],"deaths":[],"damage":{"total":100,"fightSeconds":60,"taken":0,"bySource":{},"byAbility":{}},"loot":{"motes":{"5":2},"fragments":{},"chestMotes":{},"chestFragments":{}},"derived":{"levels":1,"killLevels":1,"rewardLevels":0,"activeMinutes":30,"idleMinutes":0}}""";
        var c = CommunityBuilder.ReadCrawl(json)!;
        Assert.Null(c.TrioSource);
        Assert.Equal(2, CommunityBuilder.Visit(c)!.T5, 6);
    }

    [Fact]
    public void ImpossibleCrawlsAreLeftOut()
    {
        Assert.Null(CommunityBuilder.Visit(Crawl(levels: 40, minutes: 10)));
        Assert.Null(CommunityBuilder.Visit(Crawl(minutes: 900)));
    }

    [Fact]
    public void CrawlsWhoseLevelSpanCantHappenAreLeftOut()
    {
        // Apps before the loadout-swap fix shared crawls with another loadout's level (report #8): 14 units went down
        // (50 -> 1 at a reincarnation, 50 -> 34 after a /who swap) and 22 reached 50 with no kills, all banded by their start.
        Assert.Null(CommunityBuilder.Visit(Crawl(level: 50, levels: 0) with { LevelEnd = 1 }, out var down));
        Assert.Equal("impossible time or levels", down);
        Assert.Null(CommunityBuilder.Visit(Crawl(level: 50, levels: 0) with { LevelEnd = 34 }));
        Assert.Null(CommunityBuilder.Visit(Crawl(level: 33, levels: 0) with { LevelEnd = 50, Kills = [] }, out var noKills));
        Assert.Equal("impossible time or levels", noKills);
        // Under a level of XP can't rise two levels, even with kills; two levels at a full level of XP can.
        Assert.Null(CommunityBuilder.Visit(Crawl(level: 25, levels: 0.95) with { LevelEnd = 32 }, out var few));
        Assert.Equal("impossible time or levels", few);
        Assert.NotNull(CommunityBuilder.Visit(Crawl(level: 25, levels: 1.0) with { LevelEnd = 27 }));
        // Real crawls stay: the ding to the cap with kills, farming at the cap with or without kills, a level with no change.
        Assert.NotNull(CommunityBuilder.Visit(Crawl(level: 49, levels: 1)));
        Assert.NotNull(CommunityBuilder.Visit(Crawl(level: 50, levels: 0)));
        Assert.NotNull(CommunityBuilder.Visit(Crawl(level: 50, levels: 0) with { Kills = [] }));
        Assert.NotNull(CommunityBuilder.Visit(Crawl(level: 33, levels: 0) with { Kills = [] }));
    }

    [Fact]
    public void ACellNeedsTwoPlayersAndEnoughCrawls()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0);
        var crawls = new List<PlayerCrawl>();
        for (int i = 0; i < 6; i++) crawls.Add(new PlayerCrawl("a", Crawl(level: 10, minutes: 20)));   // player a: 20 min/level
        crawls.Add(new PlayerCrawl("b", Crawl(level: 10, minutes: 40)));                                // player b: 40 min/level
        crawls.Add(new PlayerCrawl("a", Crawl(zone: "Unrest", level: 30, minutes: 25)));                // only a played Unrest

        var file = CommunityBuilder.Build(crawls, new CommunityRules(MinPlayers: 2, MinCrawls: 5), now);
        Assert.Equal(2, file.Players);
        var befallen = Assert.Single(file.Places);
        Assert.Equal("Befallen D4", befallen.Place);
        Assert.Equal(30, befallen.Minutes[9]);          // level 10: equal weight per player, (20 + 40) / 2
        Assert.Equal(2, befallen.Players[9]);
        Assert.Equal(7, befallen.Crawls[9]);
        Assert.Null(befallen.Minutes[10]);              // no one leveled 11 there

        Assert.Empty(CommunityBuilder.Build(crawls, new CommunityRules(MinPlayers: 3), now).Places);
        Assert.Empty(CommunityBuilder.Build(crawls, new CommunityRules(MinPlayers: 2, MinCrawls: 8), now).Places);
    }

    [Fact]
    public void TheJobWorksLevelsAndActiveTimeOutFromTheFacts()
    {
        // The app sent 2 levels and 99 active minutes, but its kills add up to 1 level: the facts win.
        var c = Crawl(levels: 1, minutes: 30) with { Derived = new CrawlDerived(2, 2, 0, 99, 0) };
        Assert.Equal(1, CommunityBuilder.Visit(c)!.Levels, 6);
        Assert.Equal(99, CommunityBuilder.Visit(c)!.ActiveMinutes, 6);   // no pauses or end (an app before schema 4): its minutes stand

        // Schema 4: kills at 0, 60 … 1140 s, out from 300 to 900 s, ending at 1500 s with 1800 s inside. The 600 s outside
        // aren't idle; the last kill to the end (360 s) is 180 over the 3-minute threshold.
        var four = c with { Pauses = [[300, 900]], End = 1500 };
        Assert.Equal((1800 - 180) / 60.0, CommunityBuilder.Visit(four)!.ActiveMinutes, 6);
    }

    [Fact]
    public void ActiveTimeIsTheAppsIdleRule()
    {
        // The same visit through the app's records and through seconds from its start.
        var start = new DateTime(2026, 11, 17, 20, 0, 0);
        var v = new EqlPathfinder.Engine.Tracking.Visit
        {
            Start = start,
            Completed = true,
            CompletedAt = start.AddSeconds(2000),
            ActiveSeconds = 1700,
            KillTimes = [start.AddSeconds(400), start.AddSeconds(450), start.AddSeconds(1500)],
            Pauses = [new(start.AddSeconds(600), start.AddSeconds(900))],
        };
        double app = v.IdleSeconds(180, start.AddHours(1));
        double facts = EqlPathfinder.Engine.Tracking.ActiveTime.IdleSeconds([400, 450, 1500], 2000, [(600, 900)], 180);
        Assert.Equal(app, facts, 6);
        Assert.Equal((400 - 180) + (1050 - 300 - 180) + (500 - 180), facts, 6);   // zone-in gap, the gap across the pause, last kill to the end
    }

    [Fact]
    public void FragmentsArePublishedApartForEachAppToValue()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0);
        var crawls = new List<PlayerCrawl>();
        foreach (var p in new[] { "a", "b" })
            for (int i = 0; i < 3; i++)
                crawls.Add(new PlayerCrawl(p, Crawl(level: 10, minutes: 30, motes: new() { [5] = 10 }, frags: new() { [5] = 2, [9] = 1 })));
        var file = CommunityBuilder.Build(crawls, new CommunityRules(MinPlayers: 2, MinCrawls: 5), now);
        var place = Assert.Single(file.Places);
        // Per crawl: 10 Majors = 10 T5-eq of motes; fragments at 1×: 2 + 16 = 18; above T5: the Ascendant's 16.
        Assert.Equal(20, place.MoteT5PerHour![9], 1);        // over 30 minutes
        Assert.Equal(36, place.FragmentT5PerHour![9], 1);
        Assert.Equal(32, place.FragmentHighTierPerHour![9], 1);
        // Apps before the split read the combined numbers, at 2.0.
        Assert.Equal(place.MoteT5PerHour[9] + 2 * place.FragmentT5PerHour[9], place.T5PerHour[9], 1);
        var band = Assert.Single(file.Bands, b => b.Place == "Befallen D4" && !b.SincePatch);
        Assert.Equal(band.MoteT5PerHour!.Value + 2 * band.FragmentT5PerHour!.Value, band.T5PerHour, 1);

        // An app values fragments its own way: Settings' 4.5 here.
        var data = CommunityData.Parse(JsonSerializer.Serialize(file, CommunityBuilder.Json))!;
        Assert.Equal(20 + 4.5 * 36, data.For(fragmentValue: 4.5)!.Ask.Places.Single().T5PerHour[9], 1);
        Assert.Equal(place.T5PerHour[9], data.For()!.Ask.Places.Single().T5PerHour[9], 1);
        // A file from before the split keeps its combined number, whatever the setting.
        var old = file with { Places = file.Places.Select(p => p with { MoteT5PerHour = null, FragmentT5PerHour = null }).ToList() };
        Assert.Equal(place.T5PerHour[9],
            CommunityData.Parse(JsonSerializer.Serialize(old, CommunityBuilder.Json))!.For(fragmentValue: 4.5)!.Ask.Places.Single().T5PerHour[9], 1);
    }

    [Fact]
    public void FactsReadBackAsTheAppPacksThem()
    {
        // ShareUnits packs a unit as camelCase JSON without nulls, gzipped; D1 hands it back through hex(facts).
        var crawl = Crawl(motes: new() { [5] = 2 }, frags: new() { [7] = 1 });
        var json = JsonSerializer.SerializeToUtf8Bytes(crawl, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true)) gz.Write(json);
        var back = CommunityBuilder.ReadCrawl(CommunityJob.Unhex(Convert.ToHexString(ms.ToArray())))!;

        Assert.Equal(crawl.Day, back.Day);
        Assert.Equal(crawl.Kills.Count, back.Kills.Count);
        Assert.Equal(1, back.Loot.Fragments[7]);
        Assert.Equal(crawl.Derived, back.Derived);
        Assert.Equal(CommunityBuilder.Visit(crawl)!.T5, CommunityBuilder.Visit(back)!.T5);
    }

    [Fact]
    public void TheJobSaysWhyPlayWasLeftOutAndHowMuchItShows()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0);
        var ddd = new Dictionary<string, long> { ["Denon's Desperate Dirge Rk. II"] = 900, ["Melee"] = 100 };
        var crawls = new List<PlayerCrawl>();
        for (int i = 0; i < 5; i++) crawls.Add(new PlayerCrawl("a", Crawl(level: 10)));
        crawls.Add(new PlayerCrawl("b", Crawl(level: 10)));                                  // level 10 has two players: shown
        crawls.Add(new PlayerCrawl("a", Crawl(zone: "Unrest", level: 30)));                  // only a: not shown
        crawls.Add(new PlayerCrawl("b", Crawl(day: "2026-11-02", damage: ddd)));             // left out: DDD
        crawls.Add(new PlayerCrawl("b", Crawl(levels: 40, minutes: 10)));                    // left out: impossible

        var stats = new CommunityStats();
        CommunityBuilder.Build(crawls, [], new CommunityRules(MinPlayers: 2, MinCrawls: 5), now, stats);
        Assert.Equal(1, stats.LeftOut["mostly DDD before the nerf"]);
        Assert.Equal(1, stats.LeftOut["impossible time or levels"]);
        Assert.Equal(1, stats.CellsPublished);
        Assert.Equal(2, stats.CellsWithPlay);
        Assert.Equal(1, stats.CellsOnePlayer);
        Assert.Contains("1 of 2 place-and-level cells shown", stats.ToString());
    }

    [Fact]
    public void MalformedOrHostileSharedUnitsAreLeftOutWithoutCrashing()
    {
        // Empty JSON object: non-nullable properties are null after deserialization.
        var emptyCrawl = CommunityBuilder.ReadCrawl("{}")!;
        Assert.Null(CommunityBuilder.Visit(emptyCrawl, out _));

        var emptyStretch = CommunityBuilder.ReadStretch("{}")!;
        Assert.Null(CommunityBuilder.Hunt(emptyStretch, [], out _));

        // Valid day/zone but null nested collections and objects.
        var sparseCrawl = CommunityBuilder.ReadCrawl("""{"day":"2026-11-14","zone":"Befallen"}""")!;
        Assert.Null(CommunityBuilder.Visit(sparseCrawl, out _));

        // Pauses array containing a null entry must not throw.
        var nullPause = CommunityBuilder.ReadCrawl(
            """{"day":"2026-11-14","zone":"Befallen","difficulty":4,"levelStart":10,"levelEnd":11,"activeSeconds":1800,"completed":true,"kills":[[0,100,0,8,10]],"rewards":[],"deaths":[],"damage":{"total":100,"fightSeconds":60,"taken":0,"bySource":{},"byAbility":{}},"loot":{"motes":{},"fragments":{},"chestMotes":{},"chestFragments":{}},"derived":{"levels":1,"killLevels":1,"rewardLevels":0,"activeMinutes":30,"idleMinutes":0},"pauses":[null],"end":1800}""")!;
        Assert.NotNull(CommunityBuilder.Visit(nullPause));

        // Negative rank would yield XpMultiplier <= 0; out-of-range difficulty/rank/zone must be dropped.
        Assert.Null(CommunityBuilder.Visit(Crawl() with { Rank = -10 }));
        Assert.Null(CommunityBuilder.Visit(Crawl() with { Rank = 1000 }));
        Assert.Null(CommunityBuilder.Visit(Crawl() with { Difficulty = -1 }));
        Assert.Null(CommunityBuilder.Visit(Crawl() with { Difficulty = 25 }));
        Assert.Null(CommunityBuilder.Visit(Crawl() with { Zone = "" }));
        Assert.Null(CommunityBuilder.Visit(Crawl() with { Zone = "   " }));
        Assert.Null(CommunityBuilder.Visit(Crawl() with { Zone = new string('A', 81) }));
        Assert.Null(CommunityBuilder.Visit(Crawl() with { Zone = "Befallen\nInjected" }));
        Assert.Null(CommunityBuilder.Visit(Crawl() with { LevelStart = -5 }));
        Assert.Null(CommunityBuilder.Visit(Crawl() with { LevelStart = 250 }));

        // NaN / Infinity must never slip past range checks into the pool.
        Assert.Null(CommunityBuilder.Visit(Crawl() with { Derived = new CrawlDerived(1, 1, 0, double.NaN, 0) }));
        Assert.Null(CommunityBuilder.Visit(Crawl() with { Derived = new CrawlDerived(1, 1, 0, double.PositiveInfinity, 0) }));
        Assert.Null(CommunityBuilder.Visit(Crawl() with { ActiveSeconds = double.NaN, End = 1800 }));

        // Overflowing fragment counts or DDD damage values must not throw OverflowException.
        Assert.Null(CommunityBuilder.Visit(Crawl(frags: new() { [5] = int.MaxValue, [6] = int.MaxValue })));
        var hugeDdd = new SharedDamage(long.MaxValue, 600, 0, new Dictionary<string, long>(),
            new Dictionary<string, long> { ["Denon's Desperate Dirge"] = long.MaxValue, ["Denon's Desperate Dirge Rk. II"] = long.MaxValue });
        Assert.Null(CommunityBuilder.Visit(Crawl(day: "2026-11-02") with { Damage = hugeDdd }));

        // Hostile open-world stretch: negative rank, control chars, NaN derived.
        var badStretch = new SharedStretch("s1", "2026-11-14", 1, "Befallen\0bad", [], new StretchDerived(30, 1));
        Assert.Null(CommunityBuilder.Hunt(badStretch, []));
        var negRankStretch = new SharedStretch("s2", "2026-11-14", 1, "Befallen", [], new StretchDerived(30, 1), Trio: "WIZ/ENC/CLR", Rank: -5);
        Assert.Null(CommunityBuilder.Hunt(negRankStretch, []));
        var nanStretch = new SharedStretch("s3", "2026-11-14", 1, "Befallen", [], new StretchDerived(double.NaN, 1), Trio: "WIZ/ENC/CLR", Rank: 0);
        Assert.Null(CommunityBuilder.Hunt(nanStretch, []));
    }
}
