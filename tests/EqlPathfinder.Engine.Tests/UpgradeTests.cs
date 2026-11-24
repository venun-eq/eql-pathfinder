using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

public class UpgradeTests
{
    [Fact]
    public async Task HistoryFromAnOlderEngineIsRebuiltOnce()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-upgrade-" + Guid.NewGuid().ToString("N"));
        var logsDir = Path.Combine(dir, "Logs");
        Directory.CreateDirectory(logsDir);
        var log = Path.Combine(logsDir, "eqlog_Venun_oggok.txt");
        File.WriteAllLines(log,
        [
            "[Wed Nov 11 11:58:15 2026] You have gained the ability \"Class Reincarnation: Warrior\" at a cost of 0 ability points.",
            "[Wed Nov 11 11:58:16 2026] [1 WAR/ROG/CLR] Venun (Ogre) ZONE: Oggok",
        ]);
        var logs = new CharacterLogs("Venun", "oggok", log, [log]);
        try
        {
            using (var store = new PathfinderStore(Path.Combine(dir, "t.db")))
            {
                // What v0.1.0 left behind: history read, but no roster.
                var old = new Checkpoint { State = new EngineState { Character = "Venun", Version = 1 } };
                old.Offsets[log] = new FileInfo(log).Length;
                store.Commit(logs.Key, new PathfinderStore.Batch(), old);

                using var svc = new TrackerService(store, logs, new TrackerOptions());
                Assert.True(svc.HasHistory);
                Assert.True(svc.NeedsRebuild);
                await svc.ImportHistoryAsync();
                Assert.False(svc.NeedsRebuild);

                using var again = new TrackerService(store, logs, new TrackerOptions());
                Assert.False(again.NeedsRebuild);
                var roster = await again.Query(s => s.ReincarnationData());
                Assert.NotNull(roster);
            }
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task HistoryReadBeforeTheLoadoutSwapFixIsReadAgain()
    {
        // Engine 9 stored crawls with the other loadout's levels (report #8): a version-9 checkpoint is read again once.
        var dir = Path.Combine(Path.GetTempPath(), "pf-upgrade-" + Guid.NewGuid().ToString("N"));
        var logsDir = Path.Combine(dir, "Logs");
        Directory.CreateDirectory(logsDir);
        var log = Path.Combine(logsDir, "eqlog_Venun_oggok.txt");
        File.WriteAllLines(log, ["[Wed Nov 18 12:00:00 2026] [32 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"]);
        var logs = new CharacterLogs("Venun", "oggok", log, [log]);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            var old = new Checkpoint { State = new EngineState { Character = "Venun", Version = 9 } };
            old.Offsets[log] = new FileInfo(log).Length;
            store.Commit(logs.Key, new PathfinderStore.Batch(), old);
            using var svc = new TrackerService(store, logs, new TrackerOptions());
            Assert.True(svc.HasHistory);
            Assert.True(svc.NeedsRebuild);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
