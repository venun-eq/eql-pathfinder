using EqlPathfinder.Engine.Combat;

namespace EqlPathfinder.Engine.Tests;

public class FightMeterLiveTests
{
    private static string Stamp(DateTime t) => t.ToString("'['ddd MMM dd HH:mm:ss yyyy'] '", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The service reads history once, then follows the log as the game appends to it.</summary>
    [Fact]
    public async Task FollowsTheLogAndPublishesFrames()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-meter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        var now = DateTime.Now.AddSeconds(-30);
        File.WriteAllLines(log,
        [
            Stamp(now) + "You have entered Befallen 4 (Refined).",
            Stamp(now.AddSeconds(1)) + "You crush a zombie for 50 points of damage.",
            Stamp(now.AddSeconds(2)) + "You have slain a zombie!",
        ]);
        var logs = new EqlPathfinder.Engine.IO.CharacterLogs("Venun", "oggok", log, [log]);
        try
        {
            using var meter = new FightMeterService(logs, Path.Combine(dir, "fights.db"));
            Assert.True(meter.NeedsImport);
            meter.Start();
            await WaitFor(() => meter.Frame is { ImportPercent: null, Fights.Count: 1 });
            Assert.Equal("Befallen D4", meter.Frame!.Place);

            var t = DateTime.Now;
            File.AppendAllLines(log, [Stamp(t) + "You crush a rat for 70 points of damage.", Stamp(t) + "Hellsfury slashes a rat for 30 points of damage."]);
            await WaitFor(() => meter.Frame is { Open.Count: 1 });
            var view = MeterViews.Build(meter.Frame!, new MeterQuery("fight", "fighter"));
            Assert.Equal("a rat", view.Title);
            Assert.Equal(["Venun", "Hellsfury"], view.Rows.Select(r => r.Label));

            File.AppendAllLines(log, [Stamp(DateTime.Now) + "You have slain a rat!"]);
            await WaitFor(() => meter.Frame is { Open.Count: 0, Fights.Count: 2 });
            Assert.Equal(2, MeterViews.Build(meter.Frame!, new MeterQuery("place", "fight")).Rows.Count);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    private static async Task WaitFor(Func<bool> ok)
    {
        for (int i = 0; i < 100 && !ok(); i++) await Task.Delay(100);
        Assert.True(ok(), "the meter didn't get there in 10 seconds");
    }
}
