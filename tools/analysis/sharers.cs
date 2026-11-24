#:project ../../src/EqlPathfinder.Engine/EqlPathfinder.Engine.csproj
#:property PublishAot=false
// Sharers: each player who shares against everyone else (the community numbers built without them), from the shared units:
// how they play, where they're behind the others, how well Ask's blend (Ask.PoolVisits) predicts their visits with their
// own runs left out of the community, and how much of their leveling the others' numbers cover (what a new player gets).
// Players are letters, most crawls first; nothing here names anyone. Results: docs/ask-backtest.md.
// Usage: dotnet run tools/analysis/sharers.cs -- <folder of D1 pages>
//   each page: npx cf d1 query <db> --sql "SELECT u.id, u.player, u.kind, hex(u.facts) AS facts FROM units u ... ORDER BY u.id"
//   with the community job's filter (Community/CommunityJob.cs), saved as JSON. Keep the pages out of the repo.
using System.Text.Json;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Community;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

var crawls = new List<PlayerCrawl>();
var stretches = new List<PlayerStretch>();
foreach (var f in Directory.GetFiles(args[0], "*.json").Order())
{
    using var doc = JsonDocument.Parse(File.ReadAllText(f));
    var root = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement[0] : doc.RootElement;
    foreach (var r in root.GetProperty("results").EnumerateArray())
    {
        var player = r.GetProperty("player").GetString()!;
        var facts = CommunityJob.Unhex(r.GetProperty("facts").GetString()!);
        if (r.GetProperty("kind").GetString() == "stretch") { if (CommunityBuilder.ReadStretch(facts) is { } s) stretches.Add(new(player, s)); }
        else if (CommunityBuilder.ReadCrawl(facts) is { } c) crawls.Add(new(player, c));
    }
}
var now = DateTime.Now;
var rules = new CommunityRules();
var patch = RouteAnalysis.FarmPatch;
var players = crawls.Select(c => c.Player).Concat(stretches.Select(s => s.Player)).Distinct()
    .OrderByDescending(p => crawls.Count(c => c.Player == p)).ToList();
string Name(string p) => ((char)('A' + players.IndexOf(p))).ToString();
Console.WriteLine($"{crawls.Count} crawls and instance visits, {stretches.Count} open-world stretches, {players.Count} players");

List<LevelingSegment> Segs(string p)
{
    var own = crawls.Where(c => c.Player == p).Select(c => c.Crawl).ToList();
    return own.Select(c => CommunityBuilder.Visit(c)).OfType<LevelingSegment>()
        .Concat(stretches.Where(s => s.Player == p).Select(s => CommunityBuilder.Hunt(s.Stretch, own)).OfType<LevelingSegment>())
        .Where(v => v.ActiveMinutes >= 3).OrderBy(v => v.Start).ToList();
}
CommunityView Others(string p) => CommunityData.Parse(JsonSerializer.Serialize(CommunityBuilder.Build(
    crawls.Where(c => c.Player != p), stretches.Where(s => s.Player != p), rules, now), CommunityBuilder.Json))!.For()!;
double Med(IEnumerable<double> xs) { var s = xs.Order().ToList(); return s.Count == 0 ? double.NaN : s[s.Count / 2]; }
string Pct(double logErr) => double.IsNaN(logErr) ? "-" : $"{(Math.Exp(logErr) - 1) * 100:+0;-0}%";
string Miss(IEnumerable<double> errs) { var e = errs.ToList(); return e.Count == 0 ? "-" : $"{(Math.Exp(Med(e.Select(Math.Abs))) - 1) * 100:0}% (n {e.Count}, bias {Pct(Med(e))})"; }
bool Leveling(LevelingSegment v) => v.LevelStart is >= 1 and < 50 && v.Levels > 0.05;

foreach (var p in players)
{
    var segs = Segs(p);
    var lev = segs.Where(Leveling).ToList();
    double hours = lev.Sum(v => v.PlayedMinutes) / 60;
    Console.WriteLine($"\n==== {Name(p)} ====");
    Console.WriteLine($"{segs.Count} visits and hunts, {segs.Min(v => v.Start):yyyy-MM-dd} to {segs.Max(v => v.Start):yyyy-MM-dd}; leveling {hours:0} h over {lev.Sum(v => v.Levels):0} levels");
    string Kind(LevelingSegment v) => v.Type == "open world" ? "open world" : v.Difficulty is { } d ? $"D{d}" : "other instance";
    Console.WriteLine("leveling time by kind: " + string.Join(", ", lev.GroupBy(Kind).OrderByDescending(g => g.Sum(v => v.PlayedMinutes))
        .Select(g => $"{g.Key} {g.Sum(v => v.PlayedMinutes) / 60 / hours * 100:0}%")));
    Console.WriteLine($"grouped: {lev.Where(v => v.Grouped).Sum(v => v.PlayedMinutes) / 60 / hours * 100:0}% of leveling time; " +
        $"ranks {lev.Min(v => v.Rank)}-{lev.Max(v => v.Rank)}; {lev.Select(v => v.Trio).Distinct().Count()} trios; " +
        $"places {lev.Select(v => v.Place).Distinct().Count()}");
    Console.WriteLine("most leveled: " + string.Join(", ", lev.GroupBy(v => v.Place).OrderByDescending(g => g.Sum(v => v.PlayedMinutes)).Take(6)
        .Select(g => $"{g.Key} L{g.Min(v => v.LevelStart)}-{g.Max(v => v.LevelStart)} ({g.Sum(v => v.PlayedMinutes) / 60:0} h)")));

    var others = Others(p);
    var mine = AskData.From(segs, patch, now);

    // How much of their leveling the others' numbers cover: the share of leveling minutes at a place and level the others publish.
    double covered = lev.Sum(v => others.Ask.Places.FirstOrDefault(x => x.Place == v.Place)?.Window(v.LevelStart!.Value) != null ? v.PlayedMinutes : 0);
    Console.WriteLine($"others publish {others.Ask.Places.Count} places; they cover {covered / 60 / hours * 100:0}% of {Name(p)}'s leveling time (same place, ±1 level)");

    // Them against the others, place by place.
    var vs = Ask.Against(mine, others.Ask, 1, 50);
    if (vs.Count > 0)
    {
        int levels = vs.Sum(x => x.Levels);
        double all = Math.Exp(vs.Sum(x => x.Levels * Math.Log(x.Mine / x.Theirs)) / levels) - 1;
        Console.WriteLine($"against the others at {vs.Count} places ({levels} levels): {all * 100:+0;-0}% time a level overall");
        foreach (var x in vs)
        {
            string split = x.KillsMine > 0 && x.KillsTheirs > 0
                ? $"kills/min {x.KillsMine:0.0} vs {x.KillsTheirs:0.0}, XP a kill {(x.Theirs * x.KillsTheirs / (x.Mine * x.KillsMine) - 1) * 100:+0;-0}%" : "";
            Console.WriteLine($"  {x.Place,-40} L{x.From}-{x.To - 1} n{x.Visits,-3} {x.Mine,5:0.0} vs {x.Theirs,5:0.0} min/lv  {x.Gap * 100,4:+0;-0}%  {split}");
        }
    }
    else Console.WriteLine("against the others: no place both have played with 2+ of their visits");

    // Predict each of their visits from their earlier ones, blended with the others (their own runs left out).
    double[] ks = [0, 0.5, 1, 2, 1e9];
    var err = ks.ToDictionary(k => k, _ => new List<double>());
    var onlyMine = new List<double>(); var cold = new List<double>(); var early = new List<double>();
    int neither = 0, n = 0;
    foreach (var t in lev.Where(v => v.Levels >= 0.3 && v.ActiveMinutes >= 5 && ZoneGates.Open(v.Place, v.LevelStart!.Value)))
    {
        var prior = segs.Where(v => v.Start < t.Start).ToList();
        int L = t.LevelStart!.Value;
        double actual = t.ActiveMinutes / t.Levels / t.XpMultiplier;
        var b = others.Ask.Places.FirstOrDefault(x => x.Place == t.Place)?.Window(L);
        if (prior.Count < 10) { if (b != null) early.Add(Math.Log(b.Minutes / actual)); continue; }
        n++;
        var a = AskData.From(prior, patch, t.Start).Places.FirstOrDefault(x => x.Place == t.Place)?.Window(L);
        if (a == null && b == null) { neither++; continue; }
        if (a == null) { cold.Add(Math.Log(b!.Minutes / actual)); continue; }
        if (b == null) { onlyMine.Add(Math.Log(a.Minutes / actual)); continue; }
        foreach (var k in ks)
        {
            double z = k >= 1e9 ? 0 : a.Visits / (a.Visits + k);
            err[k].Add(Math.Log((z * a.Minutes + (1 - z) * b.Minutes) / actual));
        }
    }
    Console.WriteLine($"predicting {n} of their visits (10+ before): both have the place {err[0].Count}, only theirs {onlyMine.Count}, only the others' {cold.Count}, neither {neither}");
    foreach (var k in ks)
        Console.WriteLine($"  both, others as {(k >= 1e9 ? "everything" : k == 0 ? "nothing" : k + " visit" + (k == 1 ? "" : "s"))}: miss {Miss(err[k])}");
    Console.WriteLine($"  only their own: miss {Miss(onlyMine)}");
    Console.WriteLine($"  a place they hadn't played, from the others alone: miss {Miss(cold)}");
    Console.WriteLine($"  their first 10 visits, from the others alone: miss {Miss(early)}");

    // What Ask's route 1-50 says for them: their own logs, blended, and the others alone.
    var own = Ask.Chain(mine, 1, 50, false, 50);
    var blend = Ask.Chain(mine, 1, 50, false, 50, pool: others.Ask);
    var theirs = Ask.Chain(others.Ask, 1, 50, false, 50);
    string Route(Ask.Route r) => $"{r.Minutes / 60:0.0} h, {r.Legs.Count} legs, gaps {r.Gaps.Count}: " +
        string.Join(" → ", r.Legs.Select(l => $"{l.Place} {l.From}-{l.To}{(l.MineShare <= 0 ? "*" : "")}"));
    Console.WriteLine($"route 1-50, theirs alone: {Route(own)}");
    Console.WriteLine($"route 1-50, blended (* new to them): {Route(blend)}");
    Console.WriteLine($"route 1-50, the others alone: {Route(theirs)}");
}
