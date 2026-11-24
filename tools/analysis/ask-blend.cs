#:project ../../src/EqlPathfinder.Engine/EqlPathfinder.Engine.csproj
#:property PublishAot=false
// Ask blend check: how far your minutes a level are from the community's at the same place and level, and which weight
// for their number (Ask.PoolVisits) predicts your next visit best. Results and decision: docs/ask-backtest.md.
// Usage: dotnet run tools/analysis/ask-blend.cs -- <copy of pathfinder.db> <copy of community-v1.json>
using System.Reflection;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Community;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;

var dir = LogDiscovery.FindLogsDir()!;
var who = LogDiscovery.FindCharacters(dir)[0];
using var store = new PathfinderStore(args[0]);
using var svc = new TrackerService(store, who, new TrackerOptions());
if (svc.NeedsRebuild || !svc.HasHistory) await svc.ImportHistoryAsync();
var ready = new TaskCompletionSource<bool>();
svc.SnapshotReady += _ => ready.TrySetResult(true);
svc.Start();
await ready.Task;
svc.WaitForGameData(TimeSpan.FromSeconds(60));
var m = typeof(TrackerService).GetMethod("AskSegments", BindingFlags.NonPublic | BindingFlags.Instance)!;
var segs = await svc.Query(s => (((List<LevelingSegment>, int, int, int))m.Invoke(s, null)!).Item1);
var pool = CommunityData.Parse(File.ReadAllText(args[1]))!;
Console.WriteLine($"community: {pool.File.Players} players, {pool.File.Crawls} crawls, generated {pool.File.Generated:yyyy-MM-dd HH:mm}, rules min players {pool.File.Rules.MinPlayers} min crawls {pool.File.Rules.MinCrawls}");
var c = pool.For("all", "either")!;
var mine = AskData.From(segs, RouteAnalysis.FarmPatch, DateTime.Now);

// 1. Same place, same level: mine against theirs.
var pairs = new List<(string P, int L, int N, double Me, double Them, int Players, double KMe, double KThem)>();
foreach (var p in mine.Places)
{
    var q = c.Ask.Places.FirstOrDefault(x => x.Place == p.Place);
    if (q == null) continue;
    for (int L = 1; L < 50; L++)
        if (p.Window(L) is { } a && q.Window(L) is { } b && a.Exact && b.Exact)
            pairs.Add((p.Place, L, a.Visits, a.Minutes, b.Minutes, c.PlayersAt(p.Place, L, L + 1), a.KillsPerMinute, b.KillsPerMinute));
}
double Med(IEnumerable<double> xs) { var s = xs.Order().ToList(); return s.Count == 0 ? double.NaN : s[s.Count / 2]; }
double Sd(IEnumerable<double> xs) { var s = xs.ToList(); if (s.Count < 2) return double.NaN; double mu = s.Average(); return Math.Sqrt(s.Sum(x => (x - mu) * (x - mu)) / (s.Count - 1)); }
var lr = pairs.Select(x => Math.Log(x.Me / x.Them)).ToList();
Console.WriteLine($"\nplace-levels both have: {pairs.Count} at {pairs.Select(x => x.P).Distinct().Count()} places");
Console.WriteLine($"mine / theirs: median {Math.Exp(Med(lr)):0.00}, log SD {Sd(lr):0.00} (≈ ±{(Math.Exp(Sd(lr)) - 1) * 100:0}%)");
foreach (var g in pairs.GroupBy(x => x.N >= 4 ? 4 : x.N).OrderBy(g => g.Key))
    Console.WriteLine($"  my visits {(g.Key >= 4 ? "4+" : g.Key.ToString())}: {g.Count()} pairs, median {Math.Exp(Med(g.Select(x => Math.Log(x.Me / x.Them)))):0.00}, log SD {Sd(g.Select(x => Math.Log(x.Me / x.Them))):0.00}");
Console.WriteLine("\nby place (levels both, my visits max, mine/theirs, kills a minute me/them):");
foreach (var g in pairs.GroupBy(x => x.P).OrderByDescending(g => g.Count()).Take(25))
    Console.WriteLine($"  {g.Key,-38} L{g.Min(x => x.L)}-{g.Max(x => x.L)} n{g.Max(x => x.N),-3} {Math.Exp(Med(g.Select(x => Math.Log(x.Me / x.Them)))):0.00}  kpm {Med(g.Select(x => x.KMe)):0.0}/{Med(g.Select(x => x.KThem)):0.0}  players {g.Min(x => x.Players)}");

// 2. Predict each of my visits from my visits before it, blended with the community (now; it includes my own shares).
var all = segs.Where(v => v.ActiveMinutes >= 3).OrderBy(v => v.Start).ToList();
var targets = all.Where(v => v.LevelStart is >= 1 and < 49 && v.Levels >= 0.3 && v.ActiveMinutes >= 5 && ZoneGates.Open(v.Place, v.LevelStart.Value)).ToList();
double[] ks = [0, 0.25, 0.5, 1, 2, 4, 1e9];   // 0 = mine only (community only where I have nothing), 1e9 = community only
var err = ks.ToDictionary(k => k, _ => new List<double>());
var errNoMine = new List<double>(); int both = 0;
foreach (var t in targets)
{
    var train = all.Where(v => v.Start < t.Start).ToList();
    if (train.Count < 30) continue;
    var d = AskData.From(train, RouteAnalysis.FarmPatch, t.Start);
    int L = t.LevelStart!.Value;
    double actual = t.ActiveMinutes / t.Levels / t.XpMultiplier;   // rank 0, like the tables
    var a = d.Places.FirstOrDefault(p => p.Place == t.Place)?.Window(L);
    var b = c.Ask.Places.FirstOrDefault(p => p.Place == t.Place)?.Window(L);
    if (b == null) continue;
    if (a == null) { errNoMine.Add(Math.Log(b.Minutes / actual)); continue; }
    both++;
    foreach (var k in ks)
    {
        double z = k >= 1e9 ? 0 : a.Visits / (a.Visits + k);
        err[k].Add(Math.Log((z * a.Minutes + (1 - z) * b.Minutes) / actual));
    }
}
Console.WriteLine($"\npredicting {both} of my visits that have both my earlier visits and community numbers at the place:");
foreach (var k in ks)
    Console.WriteLine($"  K={(k >= 1e9 ? "community only" : k == 0 ? "mine only" : k.ToString())}: typical miss {(Math.Exp(Med(err[k].Select(Math.Abs))) - 1) * 100:0}%, bias {(Math.Exp(Med(err[k])) - 1) * 100:+0;-0}%");
Console.WriteLine($"visits with community numbers but none of mine before: {errNoMine.Count}, community misses {(Math.Exp(Med(errNoMine.Select(Math.Abs))) - 1) * 100:0}%, bias {(Math.Exp(Med(errNoMine)) - 1) * 100:+0;-0}%");
