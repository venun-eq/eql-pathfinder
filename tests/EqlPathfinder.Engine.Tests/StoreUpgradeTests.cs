using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;
using Microsoft.Data.Sqlite;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// kills-at-cap adds the store's first column to a table players already have (kills.xp_unknown). If the ALTER threw,
/// App.xaml.cs would catch it and the app would run with no store, so a database from before it must open, twice.
/// </summary>
public class StoreUpgradeTests
{
    [Fact]
    public async Task ADatabaseFromBeforeXpUnknownOpensTwiceAndKeepsItsRows()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-upgrade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "t.db");
        try
        {
            using (var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = db, Pooling = false }.ToString()))
            {
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE kills (
                        character TEXT NOT NULL, time TEXT NOT NULL, visit_id TEXT, zone TEXT, difficulty INTEGER,
                        level INTEGER, mob TEXT, pct REAL NOT NULL, party INTEGER NOT NULL, secs REAL);
                    INSERT INTO kills VALUES ('Old_oggok', '2026-11-01T10:00:00.0000000', NULL, 'Befallen', NULL, 12, 'a rat', 1.5, 0, NULL);
                    """;
                cmd.ExecuteNonQuery();
            }
            SqliteConnection.ClearAllPools();

            using (var first = new PathfinderStore(db))
            {
                var old = Assert.Single(first.OpenWorldKills("Old_oggok"));
                Assert.False(old.XpUnknown);
                Assert.Equal(1.5, old.Percent);
            }
            using (var second = new PathfinderStore(db))   // the column is there now: no second ALTER
            {
                Assert.False(Assert.Single(second.OpenWorldKills("Old_oggok")).XpUnknown);

                var log = Path.Combine(dir, TestLog.LiveName);
                File.WriteAllLines(log, [
                    "[Sun Nov 15 10:01:00 2026] You gain experience!",
                    "[Sun Nov 15 10:01:00 2026] You have slain a ghoul!",
                ]);
                using (var svc = new TrackerService(second, TestLog.Logs(log), new TrackerOptions()))
                    await svc.ImportHistoryAsync();
                Assert.True(Assert.Single(second.OpenWorldKills(TestLog.Key)).XpUnknown);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
