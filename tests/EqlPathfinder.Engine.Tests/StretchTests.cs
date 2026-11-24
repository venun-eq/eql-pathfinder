using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Pages;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>History's stretches and each relevel's and stop's kill pace come from the engine (docs/specs/engine-owns-numbers.md).</summary>
public class StretchTests
{
    private static readonly DateTime T0 = new(2026, 11, 10, 8, 0, 0);

    /// <summary>A crawl <paramref name="hour"/> hours after T0 that gained one level from <paramref name="level"/>.</summary>
    internal static VisitStats Row(string id, double hour, double? ks = null, double active = 30, int level = 10, double? lph = 10, string zone = "Befallen", int diff = 4) =>
        new(id, T0.AddHours(hour), zone + " " + diff, zone, diff, "WAR/CLR/ROG", null, level, level + 1, "finished", active, 0, active, null, 1, 0, false, false, lph,
            0, 0, 0, 0, 0, null, null, 0, 0, "", 60, 2, 5, 0, 0, null, 0, [], true, true, T0.AddHours(hour).AddMinutes(active))
        { KillSpeed = ks };

    private static Journey Relevel(double fromHour, double toHour, bool done = true) => new("WAR/CLR/ROG", "CLR/ROG/WAR", null, T0.AddHours(fromHour), T0.AddHours(fromHour), T0.AddHours(toHour),
        1, 50, done, 4, 5, 3, 180, 0, 0, 30, 0, [], null, null, null, []);

    [Fact]
    public void RowsAreGroupedByWhereTheyFall()
    {
        var rows = new List<VisitStats>
        {
            Row("v5", 9, level: 50, lph: null), Row("v4", 3.5, ks: null, level: 30, lph: 5), Row("v3", 3, ks: 1.0, level: 20, lph: 8),
            Row("v2", 2, ks: 0.8, level: 10, lph: 10, zone: "Kedge Keep"), Row("v1", 0.2, level: 5, lph: 12),
        };
        var stretches = Stretches.Assign(rows, [Relevel(1, 4)], 50);
        Assert.Equal(["lvl", "relevel", "cap"], stretches.Select(s => s.Kind));
        Assert.Equal([2, 1, 1, 1, 0], rows.Select(r => r.Stretch));
        var r = stretches[1];
        Assert.Equal(0, r.Relevel);
        Assert.Equal(3, r.Crawls);
        Assert.Equal(10, r.OldLevelsPerHour);
        Assert.Equal(5, r.NewLevelsPerHour);
        Assert.True(r.Fell);
        Assert.Equal((int)Math.Round((1 - Math.Exp((Math.Log(5) - Math.Log(10)) / 20)) * 100), r.PerLevelPercent);
        Assert.Equal(2, r.KillPace.Runs);
        Assert.Equal((0.8 * 30 + 30) / 60, r.KillPace.Speed!.Value, 6);
        Assert.Null(stretches[2].KillPace.Speed);
        Assert.Null(stretches[2].PerLevelPercent);
    }

    [Fact]
    public void AFinishedRelevelRunsFiveMinutesPastItsEnd()
    {
        var rows = new List<VisitStats> { Row("a", 4.2, level: 20), Row("b", 4.05, level: 20), Row("x", 2, level: 10) };
        Stretches.Assign(rows, [Relevel(1, 4)], 50);
        Assert.Equal(rows[1].Stretch, rows[2].Stretch);      // 4.05 h is inside the five minutes
        Assert.NotEqual(rows[0].Stretch, rows[1].Stretch);   // 4.2 h is outside: leveling outside a relevel
    }

    [Fact]
    public void AStopIsConsecutiveVisitsAtOnePlaceAndDifficultyInOneRelevel()
    {
        var rows = new List<VisitStats>
        {
            Row("v4", 3.5, ks: 1.2, active: 30), Row("v3", 3, ks: 1.0, active: 60), Row("v2", 2, ks: 0.5, active: 30, zone: "Kedge Keep"),
        };
        Stretches.Assign(rows, [Relevel(1, 4)], 50);
        Assert.Equal(rows[0].StopPace, rows[1].StopPace);
        Assert.Equal((1.2 * 30 + 60) / 90, rows[0].StopPace!.Speed!.Value, 6);
        Assert.Equal(0.5, rows[2].StopPace!.Speed);
    }
}

public class StretchWiringTests
{
    private static readonly DateTime T0 = new(2026, 11, 10, 8, 0, 0);
    private static VisitStats Row(string id, double hour, double? ks, int level, double lph, string zone = "Befallen", int? diff = 4) =>
        StretchTests.Row(id, hour, ks, 30, level, lph, zone, diff ?? 0) with { Difficulty = diff };

    private static Journey Relevel(double fromHour, double toHour) => new("WAR/CLR/ROG", "CLR/ROG/WAR", null, T0.AddHours(fromHour), T0.AddHours(fromHour), T0.AddHours(toHour),
        1, 50, true, 4, 5, 3, 180, 0, 0, 30, 0, [], null, null, null, []);

    [Fact]
    public void TwoRelevelsBackToBackEachGetTheirOwnStretchAndNumbers()
    {
        var rows = new List<VisitStats>
        {
            Row("d", 6.5, 2.0, 20, 4), Row("c", 6, 2.0, 10, 8),
            Row("b", 2.5, 1.0, 20, 5), Row("a", 2, 1.0, 10, 10),
        };
        var stretches = Stretches.Assign(rows, [Relevel(1, 3), Relevel(5, 7)], 50);
        Assert.Equal(2, stretches.Count);
        Assert.Equal([1, 1, 0, 0], rows.Select(r => r.Stretch));
        Assert.Equal([0, 1], stretches.Select(s => s.Relevel));
        Assert.Equal((10.0, 5.0), (stretches[0].OldLevelsPerHour!.Value, stretches[0].NewLevelsPerHour!.Value));
        Assert.Equal((8.0, 4.0), (stretches[1].OldLevelsPerHour!.Value, stretches[1].NewLevelsPerHour!.Value));
        Assert.Equal(1.0, stretches[0].KillPace.Speed);
        Assert.Equal(2.0, stretches[1].KillPace.Speed);
        // Describe reads the ids on the rows; it doesn't reassign them.
        Assert.Equal(stretches.Select(s => s.Id), Stretches.Describe(rows, [Relevel(1, 3), Relevel(5, 7)], 50).Select(s => s.Id));
    }

    [Fact]
    public void ARowWithNoDifficultyInsideAStopDoesNotBreakIt()
    {
        // The Relevels page's legs skip rows with no difficulty (race.js journeyVisits); the engine's stop does too, so a stop and a leg match.
        var rows = new List<VisitStats>
        {
            Row("c", 3.5, 1.0, 20, 5), Row("open", 3, null, 15, 6, diff: null), Row("a", 2, 1.0, 10, 10),
        };
        Stretches.Assign(rows, [Relevel(1, 4)], 50);
        Assert.Equal(1.0, rows[0].StopPace!.Speed);
        Assert.Equal(rows[0].StopPace, rows[2].StopPace);
        Assert.Equal(2, rows[0].StopPace!.Runs);
    }
}

public class StretchTrackerTests
{
    /// <summary>The tracker's own message: the stretches History's rows point at, the usual's run count, each relevel's and stop's kill pace.</summary>
    [Fact]
    public async Task TheTrackerSendsStretchesJourneysAndUsualRunsFromTheEngine()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-stretch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, TestLog.LiveName);
        File.Copy(TestLog.BefallenUnrest, log);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, TestLog.Logs(log), new TrackerOptions());
            await svc.ImportHistoryAsync();
            var (history, reinc) = await svc.Query(s =>
            {
                var h = s.History(5000);
                return (h, System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(s.ReincarnationData(null, h), PageJson.Options))!);
            });
            Assert.Equal(RunPace.MinRuns, (int)reinc["usualRuns"]!);
            var ids = reinc["stretches"]!.AsArray().Select(x => (int)x!["id"]!).ToList();
            Assert.NotEmpty(history);
            Assert.All(history, r => Assert.Contains(r.Stretch, ids));
            Assert.All(history.Where(r => r.Difficulty != null), r => Assert.NotNull(r.StopPace));
            foreach (var j in reinc["journeys"]!.AsArray()) Assert.NotNull(j!["killPace"]);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
