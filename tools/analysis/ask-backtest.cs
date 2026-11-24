#:project ../../src/EqlPathfinder.Engine/EqlPathfinder.Engine.csproj
#:property PublishAot=false
// Backtest: predict each leveling visit from the visits before it (Ask's own tables, 30-day fading, as of that visit),
// by today's 10-level bands, by its exact levels, by windows of levels around them, and by the place's average.
// Usage: dotnet run tools/analysis/ask-backtest.cs -- <copy of pathfinder.db> <out.csv>   (results: docs/ask-backtest.md)
using System.Globalization;
using System.Reflection;
using System.Text;
using EqlPathfinder.Engine.Analysis;
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
var askSegments = typeof(TrackerService).GetMethod("AskSegments", BindingFlags.NonPublic | BindingFlags.Instance)!;
var segs = await svc.Query(s => (((List<LevelingSegment>, int, int, int))askSegments.Invoke(s, null)!).Item1);
var patch = RouteAnalysis.FarmPatch;

var all = segs.Where(v => v.ActiveMinutes >= 3).OrderBy(v => v.Start).ToList();
var targets = all.Where(v => v.LevelStart is >= 1 and < 50 && v.Levels >= 0.3 && v.ActiveMinutes >= 5 && ZoneGates.Open(v.Place, v.LevelStart.Value)).ToList();
Console.WriteLine($"segments {all.Count}, leveling targets {targets.Count} ({targets.Count(t => t.Type == "crawl")} crawls), " +
    $"{all.Min(v => v.Start):yyyy-MM-dd} to {all.Max(v => v.Start):yyyy-MM-dd}");

string[] methods = ["band", "engine", "no drift", "exact", "win1", "win2", "win3", "trend3", "place", "win2>band", "win1>win2"];
var rows = new List<(LevelingSegment T, double Min, double T5, bool PlayedHere, Dictionary<string, (double Min, double T5)> P)>();
foreach (var t in targets)
{
    var train = all.Where(v => v.Start < t.Start).ToList();
    if (train.Count < 30) continue;
    var d = AskData.From(train, patch, t.Start, drift: false);   // the tables as they were before pace drift (11-16)
    var dd = AskData.From(train, patch, t.Start);                // what Ask ships: older visits scaled to the pace then
    var pd = dd.Places.FirstOrDefault(x => x.Place == t.Place);
    var p = d.Places.FirstOrDefault(x => x.Place == t.Place);
    int l0 = t.LevelStart!.Value;
    var cover = new List<(int L, double F)>();
    double left = t.Levels;
    for (int L = l0; left > 1e-9 && L < 50; L++) { double f = Math.Min(1, left); cover.Add((L, f)); left -= f; }

    var preds = new Dictionary<string, (double Min, double T5)>();
    var band = d.Bands.FirstOrDefault(b => b.Band == AskData.BandOf(l0) && b.Place == t.Place && !b.SincePatch);
    if (band is { LevelsPerHour: > 0 }) preds["band"] = (60 / band.LevelsPerHour, band.T5PerHour);
    if (p != null)
    {
        Add("engine", L => (pd ?? p).Window(L) is { } ew ? (ew.Minutes, ew.T5PerHour) : null);   // what Ask ships (AskData.WindowLevels)
        Add("no drift", L => p.Window(L) is { } nw ? (nw.Minutes, nw.T5PerHour) : null);
        Add("exact", L => Window(p, L, 0));
        Add("win1", L => Window(p, L, 1));
        Add("win2", L => Window(p, L, 2));
        Add("win3", L => Window(p, L, 3));
        Add("trend3", L => Trend(p, L, 3));
        Add("place", L => Window(p, L, 49));
    }
    if (preds.TryGetValue("win1", out var w1)) preds["win1>win2"] = w1;
    else if (preds.TryGetValue("win2", out var w2b)) preds["win1>win2"] = w2b;
    if (preds.TryGetValue("win2", out var w2)) preds["win2>band"] = w2;
    else if (preds.TryGetValue("band", out var bd)) preds["win2>band"] = bd;
    bool played = p != null && cover.Any(c => p.At(c.L) != null && p.CrawlsAt(c.L) > 0);
    rows.Add((t, t.ActiveMinutes / t.Levels / t.XpMultiplier, t.T5 / t.ActiveMinutes * 60, played, preds));

    void Add(string name, Func<int, (double Min, double T5)?> cell)
    {
        double sf = 0, sm = 0, st = 0;
        foreach (var (L, f) in cover)
            if (cell(L) is { } c) { sf += f; sm += f * c.Min; st += f * c.T5; }
        if (sf > 0) preds[name] = (sm / sf, st / sf);
    }
}

// A level's numbers from the levels around it, nearer ones and better-covered ones counting more.
static (double Min, double T5)? Window(PlaceLevels p, int l, int k)
{
    double w = 0, m = 0, t5 = 0;
    for (int j = Math.Max(1, l - k); j <= Math.Min(49, l + k); j++)
        if (p.At(j) is { } a && p.CrawlsAt(j) > 0)
        {
            double wj = (k + 1 - Math.Abs(j - l)) * p.CrawlsAt(j);
            w += wj; m += wj * a; t5 += wj * p.T5At(j);
        }
    return w > 0 ? (m / w, t5 / w) : null;
}

// The same window with a straight-line trend across it (minutes a level rise with level), so one-sided windows at a
// place's first or last levels aren't pulled toward the levels on the other side.
static (double Min, double T5)? Trend(PlaceLevels p, int l, int k)
{
    var pts = new List<(double X, double M, double T, double W)>();
    for (int j = Math.Max(1, l - k); j <= Math.Min(49, l + k); j++)
        if (p.At(j) is { } a && p.CrawlsAt(j) > 0) pts.Add((j - l, a, p.T5At(j), (k + 1 - Math.Abs(j - l)) * p.CrawlsAt(j)));
    if (pts.Count == 0) return null;
    double sw = pts.Sum(q => q.W), mx = pts.Sum(q => q.W * q.X) / sw;
    double sxx = pts.Sum(q => q.W * (q.X - mx) * (q.X - mx));
    double Fit(Func<(double X, double M, double T, double W), double> y)
    {
        double my = pts.Sum(q => q.W * y(q)) / sw;
        if (pts.Count < 3 || sxx < 1e-9) return my;
        double b = pts.Sum(q => q.W * (q.X - mx) * (y(q) - my)) / sxx;
        return my + b * (0 - mx);
    }
    double mm = Fit(q => q.M), tt = Fit(q => q.T);
    return (Math.Max(0.5, mm), Math.Max(0, tt));
}

var inv = CultureInfo.InvariantCulture;
void Report(string title, IEnumerable<(LevelingSegment T, double Min, double T5, bool PlayedHere, Dictionary<string, (double Min, double T5)> P)> set, string[] ms)
{
    var list = set.ToList();
    var common = list.Where(r => ms.All(m => r.P.ContainsKey(m))).ToList();
    Console.WriteLine($"\n== {title}: {list.Count} visits, {common.Count} that every method below can answer");
    Console.WriteLine("   method      answers   minutes/level: typical miss  bias   |  motes/h: typical miss (T5-eq/h)");
    foreach (var m in ms)
    {
        var own = list.Where(r => r.P.ContainsKey(m)).ToList();
        var e = common.Select(r => Math.Log(r.P[m].Min / r.Min)).ToList();
        var a = common.Select(r => Math.Abs(r.P[m].T5 - r.T5)).ToList();
        Console.WriteLine($"   {m,-10} {own.Count,4} ({100.0 * own.Count / Math.Max(1, list.Count),3:0}%)   " +
            $"{Pct(Med(e.Select(Math.Abs))),6}   {Signed(Med(e)),6}   |  {Med(a),5:0.0}");
    }
}
static double Med(IEnumerable<double> xs) { var s = xs.Order().ToList(); return s.Count == 0 ? double.NaN : s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2; }
static string Pct(double logErr) => double.IsNaN(logErr) ? "  -" : $"{(Math.Exp(logErr) - 1) * 100:0}%";
static string Signed(double logErr) => double.IsNaN(logErr) ? "  -" : $"{(Math.Exp(logErr) - 1) * 100:+0;-0}%";

Report("Every leveling visit", rows, methods);
Report("Crawls only", rows.Where(r => r.T.Type == "crawl"), methods);
Report("Levels already played at that place (both can answer)", rows.Where(r => r.PlayedHere), ["band", "exact", "win1", "win2", "win3", "trend3"]);
Report("Levels never played at that place before (where the band guesses)", rows.Where(r => !r.PlayedHere), ["band", "win1", "win2", "win3", "trend3", "place"]);
Report("Since the 11-10 patch", rows.Where(r => r.T.Start >= patch), methods);

// How fast minutes a level rise with level inside one place (pooled slope of log minutes on level, by place).
{
    var byPlace = rows.Where(r => r.T.Type == "crawl").GroupBy(r => r.T.Place).Where(g => g.Select(r => r.T.LevelStart).Distinct().Count() >= 3).ToList();
    double sxy = 0, sxx = 0;
    foreach (var g in byPlace)
    {
        double mx = g.Average(r => (double)r.T.LevelStart!), my = g.Average(r => Math.Log(r.Min));
        sxy += g.Sum(r => (r.T.LevelStart!.Value - mx) * (Math.Log(r.Min) - my));
        sxx += g.Sum(r => Math.Pow(r.T.LevelStart!.Value - mx, 2));
    }
    double slope = sxy / sxx;
    Console.WriteLine($"\n== Inside one place, minutes a level rise {(Math.Exp(slope) - 1) * 100:0.0}% per level " +
        $"({byPlace.Count} places with 3+ starting levels): 5 levels apart is {(Math.Exp(5 * slope) - 1) * 100:0}% slower");
}

// Ranking: two visits to different places at nearly the same level. Did the method, before each, put them in the order
// they actually came out? (Only pairs whose actual results differ by more than 15%, so the order means something.)
{
    var ts = rows.Where(r => r.T.Type == "crawl").ToList();
    Console.WriteLine("\n== Putting two places in the right order (crawls at most 2 levels apart, results 15%+ apart)");
    Console.WriteLine("   method      minutes/level: pairs  right   |  motes/h: pairs  right");
    foreach (var m in methods)
    {
        int nm = 0, okm = 0, nt = 0, okt = 0;
        for (int i = 0; i < ts.Count; i++)
            for (int j = i + 1; j < ts.Count; j++)
            {
                var (a, b) = (ts[i], ts[j]);
                if (a.T.Place == b.T.Place || Math.Abs(a.T.LevelStart!.Value - b.T.LevelStart!.Value) > 2) continue;
                if (!a.P.TryGetValue(m, out var pa) || !b.P.TryGetValue(m, out var pb)) continue;
                if (Math.Abs(Math.Log(a.Min / b.Min)) > Math.Log(1.15)) { nm++; if (Math.Sign(pa.Min - pb.Min) == Math.Sign(a.Min - b.Min)) okm++; }
                if (Math.Max(a.T5, b.T5) >= 5 && Math.Abs(a.T5 - b.T5) > 0.15 * Math.Max(a.T5, b.T5)) { nt++; if (Math.Sign(pa.T5 - pb.T5) == Math.Sign(a.T5 - b.T5)) okt++; }
            }
        Console.WriteLine($"   {m,-10}          {nm,5}  {100.0 * okm / Math.Max(1, nm),4:0}%   |       {nt,5}  {100.0 * okt / Math.Max(1, nt),4:0}%");
    }
}

// How often a band's row rests on only part of its band (the "Unrest for 21-30" case), as Ask has it today.
{
    var now = AskData.From(all, patch, all.Max(v => v.Start));
    var ranked = now.Bands.Where(b => !b.SincePatch && b.Band != "50" && b.Crawls >= Ask.MinCrawls && b.Hours >= Ask.MinHours).ToList();
    int partial = 0;
    Console.WriteLine("\n== Band rows Ask ranks today, and the levels they were actually played at");
    foreach (var b in ranked.OrderBy(b => Array.IndexOf(AskData.BandNames, b.Band)).ThenBy(b => b.Place))
    {
        var lohi = b.Band.Split('-').Select(int.Parse).ToArray();
        var p = now.Places.FirstOrDefault(x => x.Place == b.Place);
        var played = p == null ? [] : Enumerable.Range(lohi[0], lohi[1] - lohi[0] + 1).Where(L => p.At(L) != null && p.CrawlsAt(L) > 0).ToList();
        bool part = played.Count < (lohi[1] - lohi[0] + 1) / 2.0;
        if (part) partial++;
        if (b.Crawls >= 3) Console.WriteLine($"   {b.Band,-6} {b.Place,-30} {b.Crawls,3} visits  played at {(played.Count == 0 ? "-" : $"{played.Min()}-{played.Max()}"),-6} {(part ? "<- under half its band" : "")}");
    }
    Console.WriteLine($"   {partial} of {ranked.Count} ranked band rows were played at under half of their band's levels");
}

var csv = new StringBuilder("start,place,type,level,levels,minutes,actual_min_per_level,actual_t5h,played_here," + string.Join(",", methods.SelectMany(m => new[] { m + "_min", m + "_t5" })) + "\n");
foreach (var r in rows)
    csv.Append(string.Join(",", [r.T.Start.ToString("s"), "\"" + r.T.Place + "\"", r.T.Type, r.T.LevelStart.ToString(), r.T.Levels.ToString("0.00", inv),
        r.T.ActiveMinutes.ToString("0.0", inv), r.Min.ToString("0.00", inv), r.T5.ToString("0.0", inv), r.PlayedHere ? "1" : "0",
        .. methods.SelectMany(m => r.P.TryGetValue(m, out var v) ? new[] { v.Min.ToString("0.00", inv), v.T5.ToString("0.0", inv) } : ["", ""])])).Append('\n');
File.WriteAllText(args[1], csv.ToString());
Console.WriteLine($"\nwrote {rows.Count} rows to {args[1]}");
