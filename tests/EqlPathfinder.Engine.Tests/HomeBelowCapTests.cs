using System.Text.Json;
using System.Text.Json.Nodes;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Pages;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>Home while a player is mid-relevel below the cap (docs/specs/home-below-cap-state.md).</summary>
public class HomeBelowCapTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0);

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(now, TimeZoneInfo.Local.GetUtcOffset(now)).ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
    }

    private static string Stamp(DateTime t) => t.ToString("ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>One relevel played from 20 to <paramref name="to"/> in one crawl, the loadout on now, under the given cap.</summary>
    private static async Task<JsonObject> HomeAt(int to, int cap)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-hbc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        var t0 = Now.AddHours(-3);
        string L(double m, string text) => $"[{Stamp(t0.AddMinutes(m))}] {text}";
        var lines = new List<string>();
        foreach (var c in new[] { "Warrior", "Cleric", "Ranger" }) lines.Add(L(0, $"You have gained the ability \"Class Reincarnation: {c}\" at a cost of 0 ability points."));
        lines.Add(L(1, "[20 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"));
        lines.Add(L(2, "You have entered Befallen 1 (Refined)."));
        for (int lv = 21; lv <= to; lv++)
        {
            for (int k = 0; k < 4; k++) { lines.Add(L(3 + (lv - 21) * 4 + k, "You gain experience! (20.000%)")); lines.Add(L(3 + (lv - 21) * 4 + k, "You have slain a lava guardian!")); }
            lines.Add(L(3 + (lv - 21) * 4 + 3.5, $"You have gained a level! Welcome to level {lv}!"));
        }
        lines.Add(L(100, "You have completed the Dungeon Crawl and earned reward loot!"));
        lines.Add(L(110, $"[{to} WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"));
        File.WriteAllLines(log, lines);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions { Expansions = [new("Classic", DateTime.MinValue, cap)] }, new FixedClock(Now));
            await svc.ImportHistoryAsync();
            svc.Start();
            return await svc.Query(s => JsonSerializer.SerializeToNode(s.ReincarnationData(s.RebuildCampaign(new CampaignPlan(), new CampaignRoles([], false))), PageJson.Options)!.AsObject());
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact]
    public async Task A_relevel_at_34_of_50_reads_34_on_the_pace_line_and_the_splits_and_Home_is_on_the_relevel()
    {
        var d = await HomeAt(34, 50);
        Assert.Equal("relevel", d["homeNow"]!["mode"]!.GetValue<string>());
        var now = d["journeyRaces"]!["now"]!["now"]!;
        Assert.Equal(34, now["level"]!.GetValue<double>(), 0.5);
        var splits = now["splits"]!.AsArray();
        Assert.Contains(splits, s => s!["level"]!.GetValue<int>() == 30);
        Assert.Contains(splits, s => s!["level"]!.GetValue<int>() == 40);
        Assert.All(splits.Where(s => s!["level"]!.GetValue<int>() <= 30), s => Assert.False(s!["projected"]!.GetValue<bool>()));
        Assert.All(splits.Where(s => s!["level"]!.GetValue<int>() >= 40), s => Assert.True(s!["projected"]!.GetValue<bool>()));
    }

    [Fact]
    public async Task Home_is_on_the_relevel_not_Farming_below_a_cap_of_55_at_50()
    {
        var d = await HomeAt(50, 55);
        Assert.Equal("relevel", d["homeNow"]!["mode"]!.GetValue<string>());
        Assert.Equal(50, d["journeyRaces"]!["now"]!["now"]!["level"]!.GetValue<double>(), 0.5);
    }

    private static Visit V(string zone, int diff, DateTime start, int? levelStart) => new()
    {
        Id = start.Ticks.ToString(),
        BaseZone = zone,
        Zone = zone,
        Difficulty = diff,
        Start = start,
        Closed = true,
        Completed = true,
        CompletedAt = start.AddMinutes(10),
        ExitedAt = start.AddMinutes(10),
        LevelStart = levelStart,
        ActiveSeconds = 600,
    };

    /// <summary>A crawl started at 32 in Befallen, then two started at 50 in the same place; the snapshot's session under the given cap.</summary>
    private static async Task<int> SessionVisitsAt50(int cap)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-hbc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        var t0 = Now.AddHours(-3);
        string L(double m, string text) => $"[{Stamp(t0.AddMinutes(m))}] {text}";
        var lines = new List<string>();
        foreach (var c in new[] { "Warrior", "Cleric", "Ranger" }) lines.Add(L(0, $"You have gained the ability \"Class Reincarnation: {c}\" at a cost of 0 ability points."));
        void Crawl(double m, int level)
        {
            lines.Add(L(m, $"[{level} WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"));
            lines.Add(L(m + 1, "You have entered Befallen 1 (Refined)."));
            for (int k = 0; k < 4; k++) { lines.Add(L(m + 2 + k, "You gain experience! (20.000%)")); lines.Add(L(m + 2 + k, "You have slain a lava guardian!")); }
            lines.Add(L(m + 8, "You have completed the Dungeon Crawl and earned reward loot!"));
            lines.Add(L(m + 9, "You have entered Plane of Knowledge."));
        }
        Crawl(1, 32); Crawl(20, 50); Crawl(40, 50);
        File.WriteAllLines(log, lines);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions { Expansions = [new("Classic", DateTime.MinValue, cap)] }, new FixedClock(Now));
            await svc.ImportHistoryAsync();
            svc.Start();
            return await svc.Query(s => s.BuildSnapshot().Session.Visits);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact]
    public async Task The_farming_session_at_the_cap_leaves_out_a_relevel_crawl_in_the_same_place()
    {
        Assert.Equal(2, await SessionVisitsAt50(50));
        Assert.Equal(3, await SessionVisitsAt50(55));
    }
}
