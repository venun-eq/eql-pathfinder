using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

public class TrackerServiceTests
{
    /// <summary>
    /// The Reward Chest's loot lands a second after "You have completed the Dungeon Crawl", while you're still inside. The
    /// overlay's "done" card has to count it then, not when you zone out: on 11-17 a Nagafen's Lair D4 crawl read
    /// 4 T5-eq/h (one mote) with 4 Major fragments from the chest already looted.
    /// </summary>
    [Fact]
    public async Task ChestLootAfterCompletionReachesTheDoneCard()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-chest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        var t0 = DateTime.Now.AddMinutes(-20);
        string L(double minutes, string text) =>
            $"[{t0.AddMinutes(minutes).ToString("ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture)}] {text}";
        var lines = new List<string>
        {
            L(0, "You have entered Nagafen's Lair."),
            L(0.2, "[50 PAL/MNK/SHM] Venun (Ogre) ZONE: Nagafen's Lair"),
            L(1, "You have entered Nagafen's Lair 4 (Refined)."),
        };
        for (int m = 2; m <= 15; m++) lines.Add(L(m, "You have slain a lava guardian!"));
        lines.Add(L(15.5, "You looted a Mote of Major Potential from an imp protector's corpse and stored it in your currency"));
        lines.Add(L(17, "You have completed the Dungeon Crawl and earned reward loot!"));
        File.WriteAllLines(log, lines);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions()) { ModeSetting = "Farming" };
            await svc.ImportHistoryAsync();
            svc.Start();

            var before = await svc.Query(s => s.BuildSnapshot());
            Assert.Equal(1, before.Next?.Last?.T5);

            File.AppendAllLines(log, [L(17.02, "You looted 4 Fragment of Major Potential from Reward Chest and stored it in your currency")]);
            LiveSnapshot after = before;
            for (int i = 0; i < 100 && after.Current?.Fragments != 4; i++)
            {
                await Task.Delay(100);
                after = await svc.Query(s => s.BuildSnapshot());
            }
            Assert.Equal(4, after.Current?.Fragments);
            Assert.Equal(after.Current!.T5, after.Next?.Last?.T5);
            Assert.Equal(after.Current.T5PerHour, after.Next!.Last!.T5PerHour);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    /// <summary>A mob finished off after the crawl completes, while you're still inside: its kill and XP reach the "done" card too.</summary>
    [Fact]
    public async Task KillsAfterCompletionReachTheDoneCard()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-late-kill-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        var t0 = DateTime.Now.AddMinutes(-20);
        string L(double minutes, string text) =>
            $"[{t0.AddMinutes(minutes).ToString("ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture)}] {text}";
        var lines = new List<string>
        {
            L(0, "You have entered Nagafen's Lair."),
            L(0.2, "[30 PAL/MNK/SHM] Venun (Ogre) ZONE: Nagafen's Lair"),
            L(1, "You have entered Nagafen's Lair 2 (Refined)."),
        };
        for (int m = 2; m <= 15; m++)
        {
            lines.Add(L(m, "You gain experience! (2.000%)"));
            lines.Add(L(m, "You have slain a lava guardian!"));
        }
        lines.Add(L(17, "You have completed the Dungeon Crawl and earned reward loot!"));
        File.WriteAllLines(log, lines);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions()) { ModeSetting = "Leveling" };
            await svc.ImportHistoryAsync();
            svc.Start();

            var before = await svc.Query(s => s.BuildSnapshot());
            Assert.NotNull(before.Next?.Last);
            int kills = before.Current!.Kills;
            Assert.Equal(kills, before.Next!.Last!.Kills);

            File.AppendAllLines(log,
            [
                L(17.2, "You gain experience! (5.000%)"),
                L(17.2, "You have slain a lava guardian!"),
                L(17.4, "[30 PAL/MNK/SHM] Venun (Ogre) ZONE: Nagafen's Lair 2 (Refined)"),
            ]);
            LiveSnapshot after = before;
            for (int i = 0; i < 100 && after.Current?.Kills == kills; i++)
            {
                await Task.Delay(100);
                after = await svc.Query(s => s.BuildSnapshot());
            }
            Assert.Equal(kills + 1, after.Current?.Kills);
            Assert.Equal(after.Current!.Kills, after.Next?.Last?.Kills);
            Assert.Equal(after.Current.LevelsGained, after.Next!.Last!.LevelsGained);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// Queries asked before the loop runs take turns, during a history read or not: they share caches that aren't safe
    /// across threads. Until 11-17 the ones asked during a read all ran together when it ended, and two filling the
    /// community numbers' cache at once broke Home's refresh.
    /// </summary>
    [Fact]
    public async Task QueriesBeforeTheLoopRunOneAtATime()
    {
        var fixture = TestLog.BefallenUnrest;
        var dir = Path.Combine(Path.GetTempPath(), "pf-queries-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, TestLog.LiveName);
        File.Copy(fixture, log);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, TestLog.Logs(log), new TrackerOptions());
            int inside = 0, most = 0;
            int Probe(TrackerService s)
            {
                int n = Interlocked.Increment(ref inside);
                for (int m = Volatile.Read(ref most); n > m; m = Volatile.Read(ref most))
                    if (Interlocked.CompareExchange(ref most, n, m) == m) break;
                Thread.Sleep(40);
                int crawls = s.History(10).Count;
                Interlocked.Decrement(ref inside);
                return crawls;
            }

            // Asked during a read: they wait for it, then read the whole history, one at a time.
            var read = svc.ImportHistoryAsync();
            var during = Enumerable.Range(0, 4).Select(_ => svc.Query(Probe)).ToList();
            await read;
            Assert.All(await Task.WhenAll(during), n => Assert.True(n > 0));

            // Asked from several threads with no read going: still one at a time.
            using var go = new ManualResetEventSlim();
            var callers = Enumerable.Range(0, 4).Select(_ => Task.Run(() => { go.Wait(); return svc.Query(Probe); })).ToList();
            go.Set();
            await Task.WhenAll(callers);

            Assert.Equal(1, most);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// A stop is judged whole (docs/specs/judge-by-play.md): consecutive farmed visits at one place and difficulty share one
    /// StopFarm, judged from their summed T5-eq against the usual, not by the first visit; another place, another difficulty
    /// or a leveling visit starts a new stop; a raid is a raid and has none.
    /// </summary>
    [Fact]
    public async Task ConsecutiveFarmedVisitsAtOnePlaceAreJudgedAsOneStop()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-stop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        var t0 = DateTime.Now.AddHours(-14);
        var lines = new List<string>();
        int slot = 0;
        string L(double minutes, string text) =>
            $"[{t0.AddMinutes(minutes).ToString("ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture)}] {text}";
        // One visit: 14 minutes of kills, some motes, and "completed". Difficulty 3 and 4 are farmed; Befallen D1 is leveling.
        void Visit(string zone, int diff, int motes, string? entered = null)
        {
            double m = slot++ * 30;
            lines.Add(L(m, $"You have entered {zone}."));
            lines.Add(L(m + 0.2, $"[20 PAL/MNK/SHM] Venun (Ogre) ZONE: {zone}"));
            lines.Add(L(m + 1, entered ?? $"You have entered {zone} {diff} (Refined)."));
            for (int k = 2; k <= 15; k++) lines.Add(L(m + k, "You have slain a lava guardian!"));
            for (int k = 0; k < motes; k++) lines.Add(L(m + 15.5, "You looted a Mote of Major Potential from an imp protector's corpse and stored it in your currency"));
            lines.Add(L(m + 17, "You have completed the Dungeon Crawl and earned reward loot!"));
            lines.Add(L(m + 17, "You earned a refund of your instance charge."));
        }
        const string Naga = "Nagafen's Lair", Bef = "Befallen";
        Visit(Bef, 1, 0); Visit(Naga, 4, 10); Visit(Bef, 1, 0); Visit(Naga, 4, 10); Visit(Bef, 1, 0); Visit(Naga, 4, 10); Visit(Bef, 1, 0);
        Visit(Naga, 4, 20); Visit(Naga, 4, 10); Visit(Naga, 4, 3);     // the stop: 33 against a usual of 30 for these 45 minutes
        Visit(Naga, 3, 10);                                            // another difficulty
        Visit(Bef, 3, 10);                                             // another place
        Visit(Bef, 1, 0);                                              // leveling
        Visit(Naga, 4, 10);                                            // again, after a leveling visit
        Visit(Naga, 4, 0, entered: "You have entered Nagafen's Lair - Solo 4 (Refined).");   // a raid
        lines.Add(L(slot * 30, "You have entered Befallen."));
        File.WriteAllLines(log, lines);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions());
            await svc.ImportHistoryAsync();
            var rows = svc.History(100).AsEnumerable().Reverse().ToList();   // oldest first

            var stop = rows.Skip(7).Take(3).ToList();
            Assert.All(stop, r => { Assert.Equal(Naga, r.BaseZone); Assert.NotNull(r.StopFarm); });
            Assert.Same(stop[0].StopFarm, stop[1].StopFarm);
            Assert.Same(stop[0].StopFarm, stop[2].StopFarm);
            Assert.Equal("more", stop[0].FarmVerdict);                 // its first visit alone is well above the usual
            Assert.Equal("usual", stop[0].StopFarm!.Verdict);          // the whole stop is not
            Assert.NotSame(stop[0].StopFarm, rows[5].StopFarm);       // a leveling visit before it
            Assert.NotSame(stop[2].StopFarm, rows[10].StopFarm);      // another difficulty after it
            Assert.NotSame(rows[10].StopFarm, rows[11].StopFarm);     // another place
            Assert.Null(rows[12].StopFarm);                            // leveling
            Assert.NotSame(stop[0].StopFarm, rows[13].StopFarm);      // the same place and difficulty again, after leveling

            var raid = rows[14];
            Assert.Equal(VisitKinds.Raid, raid.Kind);
            Assert.Null(raid.StopFarm);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// The analysis reads the clock it is given (docs/specs/one-clock.md): "Last" on the between-crawls card shows only
    /// within 6 hours of the visit's end, so the same set time must give the same card, whatever today's date is.
    /// </summary>
    [Fact]
    public async Task TheAnalysisReadsTheClockItIsGiven()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-clock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        var t0 = new DateTime(2026, 1, 10, 12, 0, 0);
        string L(double minutes, string text) =>
            $"[{t0.AddMinutes(minutes).ToString("ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture)}] {text}";
        var lines = new List<string>
        {
            L(0, "You have entered Nagafen's Lair."),
            L(0.2, "[50 PAL/MNK/SHM] Venun (Ogre) ZONE: Nagafen's Lair"),
            L(1, "You have entered Nagafen's Lair 4 (Refined)."),
        };
        for (int m = 2; m <= 15; m++) lines.Add(L(m, "You have slain a lava guardian!"));
        lines.Add(L(15.5, "You looted a Mote of Major Potential from an imp protector's corpse and stored it in your currency"));
        lines.Add(L(17, "You have completed the Dungeon Crawl and earned reward loot!"));
        File.WriteAllLines(log, lines);
        try
        {
            async Task<double?> LastT5(DateTime now)
            {
                using var store = new PathfinderStore(Path.Combine(dir, $"t{now.Ticks}.db"));
                using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions(), new SetClock(now)) { ModeSetting = "Farming" };
                await svc.ImportHistoryAsync();
                svc.Start();
                return (await svc.Query(s => s.BuildSnapshot())).Next?.Last?.T5;
            }

            Assert.Equal(1, await LastT5(t0.AddMinutes(18 + 60)));
            Assert.Equal(1, await LastT5(t0.AddMinutes(18 + 60)));
            Assert.Null(await LastT5(t0.AddMinutes(18 + 600)));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task TheLiveStepHoldsALoginSwapUntilTheWelcomeLine()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-login-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        File.WriteAllLines(log,
        [
            "[Sun Nov 22 18:00:30 2026] [50 DRU/BRD/NEC] Venun (Iksar) ZONE: Befallen",
            "[Sun Nov 22 19:00:30 2026] [47 PAL/WIZ/BER] Venun (Erudite) ZONE: Befallen",
        ]);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            store.AddLoadoutEvent("Venun_oggok", new DateTime(2026, 11, 22, 18, 0, 0), "swap", 4);
            store.AddLoadoutEvent("Venun_oggok", new DateTime(2026, 11, 22, 19, 0, 0), "swap", 3);
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions(), new SetClock(new DateTime(2026, 11, 24, 17, 45, 40)));
            await svc.ImportHistoryAsync();
            store.AddLoadoutEvent("Venun_oggok", new DateTime(2026, 11, 24, 17, 45, 32), "swap", 4);
            svc.Start();
            int? Slot(TrackerService x) => ((PathfinderEngine)typeof(TrackerService).GetField("_engine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(x)!).State.Loadout?.Slot;
            await svc.Query(Slot);   // the first can run before the first Step
            Assert.Equal(3, await svc.Query(Slot));   // no Welcome line read yet: the swap isn't taken
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    private sealed class SetClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(now, TimeZoneInfo.Local.GetUtcOffset(now)).ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
    }
}
