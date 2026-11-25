using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>A cache in TrackerService serves what a fresh build would, whatever moved since (docs/specs/cache-keys.md).</summary>
public class CacheKeysTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0);

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(now, TimeZoneInfo.Local.GetUtcOffset(now)).ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
    }

    private static string Stamp(DateTime t) => t.ToString("ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The farming verdict follows loot that lands while the minute and the kills stand still.</summary>
    [Fact]
    public async Task PaceFollowsLootThatLandsBetweenKills()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-ck-pace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        var t0 = Now.AddMinutes(-20);
        string L(double m, string text) => $"[{Stamp(t0.AddMinutes(m))}] {text}";
        var lines = new List<string> { L(0, "You have entered Nagafen's Lair."), L(0.2, "[50 PAL/MNK/SHM] Venun (Ogre) ZONE: Nagafen's Lair"), L(1, "You have entered Nagafen's Lair 4 (Refined).") };
        for (int m = 2; m <= 15; m++) lines.Add(L(m, "You have slain a lava guardian!"));
        lines.Add(L(15.5, "You looted a Mote of Major Potential from an imp protector's corpse and stored it in your currency"));
        File.WriteAllLines(log, lines);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions(), new FixedClock(Now)) { ModeSetting = "Farming" };
            await svc.ImportHistoryAsync();
            svc.Start();
            var before = await svc.Query(s => s.BuildSnapshot());
            Assert.NotNull(before.Pace?.Farm);

            File.AppendAllLines(log, [L(15.6, "You looted 4 Fragment of Major Potential from an imp protector's corpse and stored it in your currency")]);
            LiveSnapshot after = before;
            for (int i = 0; i < 100 && after.Current?.Fragments != 4; i++) { await Task.Delay(100); after = await svc.Query(s => s.BuildSnapshot()); }
            Assert.Equal(4, after.Current?.Fragments);
            Assert.Equal(after.Current!.T5PerHour, after.Pace!.Farm!.T5PerHour);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    /// <summary>What a fragment is worth (a setting) moves the farm rate; it keeps no old number.</summary>
    [Fact]
    public async Task FarmRateFollowsWhatAFragmentIsWorth()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-ck-farm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        var t0 = Now.AddHours(-4);
        string L(double m, string text) => $"[{Stamp(t0.AddMinutes(m))}] {text}";
        var lines = new List<string>();
        for (int c = 0; c < 3; c++)
        {
            double m = c * 40;
            lines.Add(L(m, "You have entered Nagafen's Lair."));
            lines.Add(L(m + 0.2, "[50 PAL/MNK/SHM] Venun (Ogre) ZONE: Nagafen's Lair"));
            lines.Add(L(m + 1, "You have entered Nagafen's Lair 4 (Refined)."));
            for (int k = 2; k <= 15; k++) lines.Add(L(m + k, "You have slain a lava guardian!"));
            lines.Add(L(m + 15.5, "You looted 5 Fragment of Major Potential from an imp protector's corpse and stored it in your currency"));
            lines.Add(L(m + 16, "You have completed the Dungeon Crawl and earned reward loot!"));
            lines.Add(L(m + 16.1, "You earned a refund of your instance charge."));
        }
        File.WriteAllLines(log, lines);
        try
        {
            var o = new TrackerOptions { FragmentValue = 3.56 };
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), o, new FixedClock(Now));
            await svc.ImportHistoryAsync();
            svc.Start();
            var farm = await svc.Query(s => s.FarmInfo().Usual);
            Assert.NotNull(farm);
            o.FragmentValue = 2.0;
            Assert.NotEqual(farm, await svc.Query(s => s.FarmInfo().Usual));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    /// <summary>Ask's tables follow what a fragment is worth too.</summary>
    [Fact]
    public async Task AskFollowsWhatAFragmentIsWorth()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-ck-ask-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        var t0 = Now.AddHours(-4);
        string L(double m, string text) => $"[{Stamp(t0.AddMinutes(m))}] {text}";
        var lines = new List<string>();
        for (int c = 0; c < 3; c++)
        {
            double m = c * 30;
            lines.Add(L(m, "You have entered Befallen."));
            lines.Add(L(m + 0.2, "[20 PAL/MNK/SHM] Venun (Ogre) ZONE: Befallen"));
            lines.Add(L(m + 1, "You have entered Befallen 1 (Refined)."));
            for (int k = 0; k < 24; k++) { lines.Add(L(m + 2 + k * 0.5, "You gain experience! (1.000%)")); lines.Add(L(m + 2 + k * 0.5, "You have slain a lava guardian!")); }
            lines.Add(L(m + 15.5, "You looted 5 Fragment of Major Potential from an imp protector's corpse and stored it in your currency"));
            lines.Add(L(m + 17, "You have completed the Dungeon Crawl and earned reward loot!"));
            lines.Add(L(m + 17, "You earned a refund of your instance charge."));
        }
        File.WriteAllLines(log, lines);
        try
        {
            var o = new TrackerOptions { FragmentValue = 3.56 };
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), o, new FixedClock(Now));
            await svc.ImportHistoryAsync();
            svc.Start();
            double Sum(AskData d) => d.Places.SelectMany(p => p.T5PerHour).Sum();
            var before = await svc.Query(s => Sum(s.AskData()));
            Assert.True(before > 0);
            o.FragmentValue = 2.0;
            Assert.NotEqual(before, await svc.Query(s => Sum(s.AskData())));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    /// <summary>The farming verdict follows a setting (what a fragment is worth) while the kills and the minute stand still.</summary>
    [Fact]
    public async Task PaceFollowsWhatAFragmentIsWorth()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-ck-pset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        var t0 = Now.AddMinutes(-20);
        string L(double m, string text) => $"[{Stamp(t0.AddMinutes(m))}] {text}";
        var lines = new List<string> { L(0, "You have entered Nagafen's Lair."), L(0.2, "[50 PAL/MNK/SHM] Venun (Ogre) ZONE: Nagafen's Lair"), L(1, "You have entered Nagafen's Lair 4 (Refined).") };
        for (int m = 2; m <= 15; m++) lines.Add(L(m, "You have slain a lava guardian!"));
        lines.Add(L(15.5, "You looted 5 Fragment of Major Potential from an imp protector's corpse and stored it in your currency"));
        File.WriteAllLines(log, lines);
        try
        {
            var o = new TrackerOptions { FragmentValue = 3.56 };
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), o, new FixedClock(Now)) { ModeSetting = "Farming" };
            await svc.ImportHistoryAsync();
            svc.Start();
            var before = await svc.Query(s => s.BuildSnapshot());
            Assert.NotNull(before.Pace?.Farm);
            Assert.Equal(before.Current!.T5PerHour, before.Pace!.Farm!.T5PerHour);
            o.FragmentValue = 2.0;
            var after = await svc.Query(s => s.BuildSnapshot());
            Assert.NotEqual(before.Current.T5PerHour, after.Current!.T5PerHour);
            Assert.Equal(after.Current.T5PerHour, after.Pace!.Farm!.T5PerHour);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    /// <summary>The between-crawls card (leveling) follows a setting even when the farm rate (part of its key) stays put: the rate is manual.</summary>
    [Fact]
    public async Task NextOptionsFollowWhatAFragmentIsWorth()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-ck-nset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        var t0 = Now.AddHours(-4);
        string L(double m, string text) => $"[{Stamp(t0.AddMinutes(m))}] {text}";
        var lines = new List<string>();
        for (int c = 0; c < 3; c++)
        {
            double m = c * 30;
            lines.Add(L(m, "You have entered Befallen."));
            lines.Add(L(m + 0.2, "[20 PAL/MNK/SHM] Venun (Ogre) ZONE: Befallen"));
            lines.Add(L(m + 1, "You have entered Befallen 1 (Refined)."));
            for (int k = 0; k < 24; k++) { lines.Add(L(m + 2 + k * 0.5, "You gain experience! (1.000%)")); lines.Add(L(m + 2 + k * 0.5, "You have slain a lava guardian!")); }
            lines.Add(L(m + 15.5, "You looted 5 Fragment of Major Potential from an imp protector's corpse and stored it in your currency"));
            lines.Add(L(m + 17, "You have completed the Dungeon Crawl and earned reward loot!"));
            lines.Add(L(m + 17, "You earned a refund of your instance charge."));
        }
        File.WriteAllLines(log, lines);
        try
        {
            var o = new TrackerOptions { FragmentValue = 3.56, FarmRateMode = "manual", FarmRateManual = 5 };
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), o, new FixedClock(Now)) { ModeSetting = "Leveling" };
            await svc.ImportHistoryAsync();
            svc.Start();
            double Sum(LiveSnapshot s) => s.Next!.Options.Sum(x => x.T5PerHour);
            var before = await svc.Query(s => s.BuildSnapshot());
            Assert.NotNull(before.Next);
            Assert.True(Sum(before) > 0);
            o.FragmentValue = 2.0;
            Assert.NotEqual(Sum(before), Sum(await svc.Query(s => s.BuildSnapshot())));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    /// <summary>Journeys follow the level cap (a setting): a relevel at the cap is done, one below it is not.</summary>
    [Fact]
    public async Task JourneysFollowTheLevelCap()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-ck-jcap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        var t0 = Now.AddHours(-2);
        string L(double m, string text) => $"[{Stamp(t0.AddMinutes(m))}] {text}";
        var lines = new List<string>();
        foreach (var c in new[] { "Warrior", "Cleric", "Ranger" })
            lines.Add(L(0, $"You have gained the ability \"Class Reincarnation: {c}\" at a cost of 0 ability points."));
        lines.Add(L(1, "[20 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"));
        lines.Add(L(2, "You have entered Befallen 1 (Refined)."));
        for (int k = 0; k < 24; k++) { lines.Add(L(3 + k * 0.5, "You gain experience! (3.000%)")); lines.Add(L(3 + k * 0.5, "You have slain a lava guardian!")); }
        lines.Add(L(10, "You have gained a level! Welcome to level 21!"));
        lines.Add(L(17, "You have completed the Dungeon Crawl and earned reward loot!"));
        File.WriteAllLines(log, lines);
        try
        {
            var o = new TrackerOptions { Expansions = [new("Classic", DateTime.MinValue, 50)] };
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), o, new FixedClock(Now));
            await svc.ImportHistoryAsync();
            svc.Start();
            var before = await svc.Query(s => s.Journeys());
            Assert.NotEmpty(before);
            Assert.All(before, j => Assert.False(j.Done));
            o.Expansions = [new("Classic", DateTime.MinValue, 21)];
            Assert.Contains(await svc.Query(s => s.Journeys()), j => j.Done);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
