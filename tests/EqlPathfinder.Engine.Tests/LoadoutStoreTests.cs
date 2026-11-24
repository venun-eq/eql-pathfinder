using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The swaps the game's files showed are kept in the store, and the crawls read after them take their slot
/// (docs/specs/current-loadout.md, Decisions: "What's stored").
/// </summary>
public class LoadoutStoreTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0);
    private static readonly DateTime Day = new(2026, 11, 18);

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(now, TimeZoneInfo.Local.GetUtcOffset(now)).ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
    }

    private static string L(string hms, string body) => $"[Wed Nov 18 {hms} 2026] {body}";
    private static DateTime T(string hms) => Day + TimeSpan.Parse(hms, System.Globalization.CultureInfo.InvariantCulture);

    private static string[] TwoCrawls() =>
    [
        L("12:00:30", "[10 DRU/BRD/NEC] Venun (Barbarian) ZONE: Befallen"),   // slot 6
        L("12:01:30", "[50 PAL/MNK/SHM] Venun (Froglok) ZONE: Befallen"),     // slot 1
        L("12:03:00", "You have entered Kedge Keep 4 (Refined)."),            // on slot 6
        L("12:05:00", "You gain experience! (2.000%)"), L("12:05:00", "You have slain a seahorse!"),
        L("12:07:00", "You gain experience! (2.000%)"), L("12:07:00", "You have slain a seahorse!"),
        L("12:10:00", "You have completed the Dungeon Crawl and earned reward loot!"),
        L("12:11:00", "You have entered Dagnor's Cauldron."),
        L("12:13:00", "You have entered Kedge Keep 4 (Refined)."),            // on slot 1, no /who
        L("12:15:00", "You gain experience! (0.100%)"), L("12:15:00", "You have slain a seahorse!"),
        L("12:17:00", "You gain experience! (0.100%)"), L("12:17:00", "You have slain a seahorse!"),
        L("12:20:00", "You have completed the Dungeon Crawl and earned reward loot!"),
        L("12:21:00", "You have entered Dagnor's Cauldron."),
    ];

    private static void Seed(PathfinderStore store)
    {
        store.AddLoadoutEvent("Venun_oggok", T("12:00:00"), "swap", 6);
        store.AddLoadoutEvent("Venun_oggok", T("12:01:00"), "swap", 1);
        store.AddLoadoutEvent("Venun_oggok", T("12:02:00"), "swap", 6);
        store.AddLoadoutEvent("Venun_oggok", T("12:12:00"), "swap", 1);
    }

    private static (string dir, string log) Folder(string[] lines)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-lo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Logs"));
        var log = Path.Combine(dir, "Logs", "eqlog_Venun_oggok.txt");
        File.WriteAllLines(log, lines);
        return (dir, log);
    }

    private static void Clean(string dir)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(dir, true); } catch (IOException) { }
    }

    [Fact]
    public void TheEventsSurviveAResetAndARepeatIsKeptOnce()
    {
        var (dir, _) = Folder([]);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            Seed(store);
            store.AddLoadoutEvent("Venun_oggok", T("12:12:00"), "swap", 1);   // the same sighting again
            store.AddLoadoutEvent("Other_oggok", T("12:00:00"), "swap", 3);   // another character's
            store.Reset("Venun_oggok");
            var rows = store.LoadoutEventsAfter("Venun_oggok", 0);
            Assert.Equal([6, 1, 6, 1], rows.Select(r => r.Slot));
            Assert.Equal(T("12:12:00"), rows[3].Time);
            Assert.Single(store.LoadoutEventsAfter("Other_oggok", 0));
            Assert.Single(store.LoadoutEventsAfter("Venun_oggok", rows[2].Id));
        }
        finally { Clean(dir); }
    }

    [Fact]
    public async Task AFreshServiceReReadingTheLogGivesTheCrawlAfterAStoredSwapItsSlot()
    {
        var (dir, log) = Folder(TwoCrawls());
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            Seed(store);
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions(), new FixedClock(Now));
            await svc.ImportHistoryAsync();
            svc.Start();
            var h = (await svc.Query(s => s.History(10))).OrderBy(v => v.Start).ToList();
            Assert.Equal(2, h.Count);
            Assert.Equal(("DRU/BRD/NEC", "Barbarian", 10, 6), (h[0].Trio, h[0].Race, h[0].LevelStart, h[0].Slot));
            Assert.Equal(("PAL/MNK/SHM", "Froglok", 50, 1), (h[1].Trio, h[1].Race, h[1].LevelStart, h[1].Slot));
        }
        finally { Clean(dir); }
    }

    [Fact]
    public async Task ARestartHasTheSlotOnAndEachSlotsWhoBackAndTakesARowStoredAfterTheCheckpointOnce()
    {
        var (dir, log) = Folder(TwoCrawls());
        var db = Path.Combine(dir, "t.db");
        try
        {
            int stints;
            using (var store = new PathfinderStore(db))
            {
                Seed(store);
                using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions(), new FixedClock(Now));
                await svc.ImportHistoryAsync();
                svc.Start();
                stints = await svc.Query(s => s.TrioTimeline().Spans().Count());
                Assert.Equal("PAL/MNK/SHM", await svc.Query(s => s.TrioTimeline().At(Now)?.Trio));
            }
            using (var store = new PathfinderStore(db))
            {
                // No new line and no new row: the checkpoint alone brings the state back.
                using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions(), new FixedClock(Now));
                svc.Start();
                Assert.Equal("PAL/MNK/SHM", await svc.Query(s => s.TrioTimeline().At(Now)?.Trio));
                Assert.Equal(stints, await svc.Query(s => s.TrioTimeline().Spans().Count()));
            }
            using (var store = new PathfinderStore(db))
            {
                store.AddLoadoutEvent("Venun_oggok", T("12:30:00"), "swap", 6);   // seen after the last checkpoint
                for (int i = 0; i < 2; i++)
                {
                    using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions(), new FixedClock(Now));
                    svc.Start();
                    await svc.Query(s => 0);   // the loop's first step takes the stored rows after a first query, as it reads the log tail
                    // Slot 6 is back on with the trio its /who named; a second restart takes the row no second time.
                    Assert.Equal("DRU/BRD/NEC", await svc.Query(s => s.TrioTimeline().At(Now)?.Trio));
                    Assert.Equal(stints + 1, await svc.Query(s => s.TrioTimeline().Spans().Count()));
                }
            }
        }
        finally { Clean(dir); }
    }

    [Fact]
    public async Task ASwapSeenAfterTheImportEndedIsAppliedByTheNextStep()
    {
        var (dir, log) = Folder(TwoCrawls());
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions(), new FixedClock(Now));
            await svc.ImportHistoryAsync();
            Assert.Equal("PAL/MNK/SHM", await svc.Query(s => s.TrioTimeline().At(Now)?.Trio));
            store.AddLoadoutEvent("Venun_oggok", T("12:30:00"), "swap", 3);   // seen while the import ran, or just after
            svc.Start();
            await svc.Query(s => 0);
            Assert.Null(await svc.Query(s => s.TrioTimeline().At(Now)?.Trio));   // slot 3 is on, and not met
            var snap = await svc.Query(s => s.BuildSnapshot());
            Assert.True(snap.NotKnown);
            Assert.Null(snap.Pace);   // nothing is judged by another loadout's usual: the engine says so, the overlay only draws it
        }
        finally { Clean(dir); }
    }

    private static string[] SixKillCrawl(string who = "[50 PAL/MNK/SHM] Venun (Froglok)") =>
    [
        L("12:00:30", who + " ZONE: Befallen"),
        L("12:03:00", "You have entered Kedge Keep 4 (Refined)."),
        .. Enumerable.Range(1, 6).SelectMany(i => new[] { L($"12:{3 + i:00}:00", "You gain experience! (2.000%)"), L($"12:{3 + i:00}:00", "You have slain a seahorse!") }),
        L("12:12:00", "You have completed the Dungeon Crawl and earned reward loot!"),
        L("12:13:00", "You have entered Dagnor's Cauldron."),
    ];

    private static async Task<LiveSnapshot> SnapshotWith(string[] lines, DateTime? swapAt)
    {
        var (dir, log) = Folder(lines);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions(), new FixedClock(T("12:25:00")));
            await svc.ImportHistoryAsync();
            if (swapAt is { } at) store.AddLoadoutEvent("Venun_oggok", at, "swap", 3);   // a slot not met
            svc.Start();
            await svc.Query(s => 0);
            return await svc.Query(s => s.BuildSnapshot());
        }
        finally { Clean(dir); }
    }

    [Fact]
    public async Task ALogEndingInsideACrawlHasNoPaceOnASlotNotMetAndHasOneWithout()
    {
        var lines = TwoCrawls()[..12];   // ends after the second crawl's kills at 12:17, still inside it
        var without = await SnapshotWith(lines, null);
        Assert.False(without.NotKnown);
        Assert.NotNull(without.Pace);
        var with = await SnapshotWith(lines, T("12:16:30"));
        Assert.True(with.NotKnown);
        Assert.Null(with.Pace);
    }

    [Fact]
    public async Task ALogEndingOnTheCompletedLineHasNoVerdictOnASlotNotMetAndHasOneWithout()
    {
        var lines = SixKillCrawl();
        var without = await SnapshotWith(lines, null);
        Assert.NotNull(without.Next?.LastFarm);
        var with = await SnapshotWith(lines, T("12:13:30"));
        Assert.True(with.NotKnown);
        Assert.Null(with.Next?.LastPace);
        Assert.Null(with.Next?.LastFarm);
    }
}
