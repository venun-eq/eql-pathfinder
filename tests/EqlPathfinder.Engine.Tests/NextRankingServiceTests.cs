using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

public class NextRankingServiceTests
{
    /// <summary>
    /// Between crawls the overlay offers where to go from your history (docs/specs/next-ranking.md). Moving that ranking into
    /// plain code must not move what a player is offered: four Befallen D1 crawls at level 20, the last just done, give the
    /// same place, the same rates and the same "again", through the whole service.
    /// </summary>
    [Theory]
    [InlineData(0, 30)]
    [InlineData(12, 0)]
    public async Task BetweenCrawlsOffersWhatItOfferedBefore(int hour, int minute)
    {
        var next = await NextAfterCrawls(hoursAgo: 3, now: new DateTime(2026, 11, 19, hour, minute, 0));
        Assert.NotNull(next?.Last);
        var o = Assert.Single(next!.Options);
        Assert.Equal("Befallen D1", o.Label);
        Assert.True(o.Again);
        Assert.Equal("0.960|0.000|52.08|3|", $"{o.LevelsPerHour:0.000}|{o.T5PerHour:0.000}|{o.Cost:0.00}|{o.Crawls}|{o.FromLevel}");
    }

    /// <summary>Crawls long past (over 6 hours) leave no "last crawl", but the history still has places to offer: the card shows with options only.</summary>
    [Theory]
    [InlineData(0, 30)]
    [InlineData(12, 0)]
    public async Task OldCrawlsStillOfferPlacesWithNoLastCrawl(int hour, int minute)
    {
        var next = await NextAfterCrawls(hoursAgo: 10, now: new DateTime(2026, 11, 19, hour, minute, 0));
        Assert.NotNull(next);
        Assert.Null(next!.Last);
        Assert.NotEmpty(next.Options);
    }

    private static async Task<NextStep?> NextAfterCrawls(int hoursAgo, DateTime now)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-next-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        var t0 = now.AddHours(-hoursAgo);
        string L(double minutes, string text) =>
            $"[{t0.AddMinutes(minutes).ToString("ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture)}] {text}";
        var lines = new List<string>();
        void Crawl(int slot, string zone, int diff, int kills)
        {
            double m = slot * 30;
            lines.Add(L(m, $"You have entered {zone}."));
            lines.Add(L(m + 0.2, $"[20 PAL/MNK/SHM] Venun (Ogre) ZONE: {zone}"));
            lines.Add(L(m + 1, $"You have entered {zone} {diff} (Refined)."));
            for (int k = 0; k < kills; k++) { lines.Add(L(m + 2 + k * 0.5, "You gain experience! (1.000%)")); lines.Add(L(m + 2 + k * 0.5, "You have slain a lava guardian!")); }
            lines.Add(L(m + 17, "You have completed the Dungeon Crawl and earned reward loot!"));
            lines.Add(L(m + 17, "You earned a refund of your instance charge."));
        }
        Crawl(0, "Befallen", 1, 20); Crawl(1, "Befallen", 1, 24); Crawl(2, "Befallen", 1, 28); Crawl(3, "Befallen", 1, 30);
        File.WriteAllLines(log, lines);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions(), new FixedClock(now)) { ModeSetting = "Leveling" };
            await svc.ImportHistoryAsync();
            svc.Start();
            return (await svc.Query(s => s.BuildSnapshot())).Next;
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// The crawl just ended is judged from its stats (the live path) and its History row from Ask's segments: the overlay's
    /// place words must equal the row's, and a place verdict leaves no "not clearly faster" line (docs/specs/verdicts-as-of-visit.md).
    /// </summary>
    [Fact]
    public async Task TheCrawlJustEndedAndItsHistoryRowAreJudgedByTheSameCheck()
    {
        var now = new DateTime(2026, 11, 19, 12, 0, 0);
        var dir = Path.Combine(Path.GetTempPath(), "pf-same-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        var t0 = now.AddHours(-5);
        string L(double minutes, string text) =>
            $"[{t0.AddMinutes(minutes).ToString("ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture)}] {text}";
        var lines = new List<string>();
        void Crawl(int slot, string zone, int kills)
        {
            double m = slot * 30;
            lines.Add(L(m, $"You have entered {zone}."));
            lines.Add(L(m + 0.2, $"[20 PAL/MNK/SHM] Venun (Ogre) ZONE: {zone}"));
            lines.Add(L(m + 1, $"You have entered {zone} 1 (Refined)."));
            for (int k = 0; k < kills; k++) { lines.Add(L(m + 2 + k * 0.5, "You gain experience! (1.000%)")); lines.Add(L(m + 2 + k * 0.5, "You have slain a lava guardian!")); }
            lines.Add(L(m + 17, "You have completed the Dungeon Crawl and earned reward loot!"));
            lines.Add(L(m + 17, "You earned a refund of your instance charge."));
        }
        for (int i = 0; i < 3; i++) Crawl(i, "Nagafen's Lair", 12);
        for (int i = 3; i < 7; i++) Crawl(i, "Befallen", 30);
        Crawl(7, "Nagafen's Lair", 12);
        lines.Add(L(7 * 30 + 18, "You have entered Oggok.")); lines.Add(L(7 * 30 + 18.2, "[20 PAL/MNK/SHM] Venun (Ogre) ZONE: Oggok"));   // leaves it: the row is closed, the overlay still judges the crawl just ended
        File.WriteAllLines(log, lines);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions(), new FixedClock(now)) { ModeSetting = "Leveling" };
            await svc.ImportHistoryAsync();
            svc.Start();
            var next = (await svc.Query(s => s.BuildSnapshot())).Next;
            var hist = svc.History(50);
            var row = hist.First(r => r.BaseZone == "Nagafen's Lair" && r.Start == hist.Max(h => h.Start));
            Assert.NotNull(next?.LastPlace);   // a real verdict, so the two sides are not both empty
            Assert.Equal(row.PlaceVerdict, next!.LastPlace!.Kind);
            Assert.Equal(row.BetterPlace, next.LastPlace.Better);
            Assert.Null(next.LastNotClear);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    /// <summary>The service's clock, fixed: the crawls' times and the service's "now" come from one instant, so the time of day the suite runs at can't move a crawl across midnight.</summary>
    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(now, TimeZoneInfo.Local.GetUtcOffset(now)).ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
    }
}
