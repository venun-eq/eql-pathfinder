using System.Text.RegularExpressions;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

public class LootLedgerTests
{
    /// <summary>Every mote and fragment line in the fixture lands in the ledger, at every tier (not only above T5).</summary>
    [Fact]
    public async Task LedgerCountsEveryTierAndEveryFragment()
    {
        var fixture = TestLog.BefallenUnrest;
        var dir = Path.Combine(Path.GetTempPath(), "pf-ledger-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, TestLog.LiveName);
        File.Copy(fixture, log);

        // Expected, counted straight from the text.
        var rx = new Regex(@"^\[[^\]]+\] You looted (a|an|\d+) (Mote|Fragment)s? of (?:(\w+) )?Potential from .+ and stored it in your currency$");
        var expected = new Dictionary<(LootKind, MoteTier), long>();
        int fragmentLines = 0;
        foreach (var line in File.ReadLines(log))
        {
            var m = rx.Match(line);
            if (!m.Success) continue;
            var kind = m.Groups[2].Value == "Fragment" ? LootKind.Fragment : LootKind.Mote;
            Assert.True(MoteTiers.TryParse(m.Groups[3].Value, out var tier));
            int n = int.TryParse(m.Groups[1].Value, out var c) ? c : 1;
            expected[(kind, tier)] = expected.GetValueOrDefault((kind, tier)) + n;
            if (kind == LootKind.Fragment) fragmentLines++;
        }
        Assert.Contains(expected.Keys, k => k.Item1 == LootKind.Fragment && k.Item2 < MoteTier.Greater);

        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using (var svc = new TrackerService(store, TestLog.Logs(log), new TrackerOptions()))
                await svc.ImportHistoryAsync();
            var (totals, since) = store.LootTotals(TestLog.Key);
            Assert.NotNull(since);
            foreach (var ((kind, tier), n) in expected)
                Assert.Equal(n, totals.Where(t => t.Kind == kind && t.Tier == tier).Sum(t => t.Count));
            Assert.Equal(expected.Values.Sum(), totals.Sum(t => t.Count));
            Assert.Equal(fragmentLines, store.NotableDrops(TestLog.Key, 10_000).Count(d => d.Kind == LootKind.Fragment));

            // Every pickup, each tied to the crawl it dropped in.
            var all = store.AllLoot(TestLog.Key);
            Assert.Equal(expected.Values.Sum(), all.Sum(x => x.Count));
            Assert.Contains(all, x => x.InInstance && x.Place is { } p && p.StartsWith("Befallen D"));
            Assert.Contains(all, x => x.Chest && x.Kind == LootKind.Fragment);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
