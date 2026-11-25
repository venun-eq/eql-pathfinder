using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>How fast a relevel went against your other relevels in the same zones, difficulties and level bands.</summary>
/// <param name="Pace">×1.00 is your usual; null when it shares too little with the others.</param>
/// <param name="Kind">"solo": levels per hour on solo crawls; "kills": kills per hour (its crawls were mostly grouped).</param>
public sealed record TrioPace(string Trio, string TrioKey, double? Pace, string Kind, double SharedHours, double GroupedShare,
    int Deaths, bool Done, double ActiveHours, int Rank, int Number);

/// <summary>A stretch of a finished relevel that took longer than your usual hours for those levels.</summary>
public sealed record RelevelDetour(string Name, double Hours, string Source);

public sealed record RelevelPaceResult(IReadOnlyList<TrioPace> Paces, IReadOnlyList<RelevelDetour> Detours);

/// <summary>
/// Facts about the relevels you played, compared like for like: each relevel's pace against your others in the same
/// places, and stretches slower than usual. Does not predict leveling hours for unplayed trios because
/// synergy depends too heavily on the full composition; per-place speed lives in Ask.
/// </summary>
public static class RelevelPaces
{
    private const double SoloBelow = 0.25, GroupedFrom = 0.5;
    /// <summary>How much evidence the default (×1.00 pace) is worth, in hours of crawls.</summary>
    private const double PriorHours = 2;

    private sealed record Row(int J, (string Zone, int? Diff, int Band) Cell, double Minutes, double Lph, double Kph, double Party, Visit V);

    public static RelevelPaceResult Build(IReadOnlyList<Visit> visits, IReadOnlyList<Journey> journeys, EngineState s, TrackerOptions o,
        IReadOnlyDictionary<string, (int Kills, int Party)> party) =>
        Build(PlayFacts.From(visits, s, null, party), journeys, o);

    public static RelevelPaceResult Build(PlayFacts play, IReadOnlyList<Journey> journeys, TrackerOptions o)
    {
        // each crawl is below the cap of its own day
        var who = play.Pairs;
        var ordered = journeys.OrderBy(j => j.Start).ToList();

        // Each relevel's crawls (closed, at least 5 active minutes, below the cap).
        var rows = new List<Row>();
        var crawlsOf = new List<List<Visit>>();
        for (int ji = 0; ji < ordered.Count; ji++)
        {
            var j = ordered[ji];
            var mine = new List<Visit>();
            foreach (var v in play.Visits)
            {
                if (!v.Closed || v.Difficulty == null || v.LevelStart is not { } ls || ls >= o.CapOn(v)) continue;
                if (v.Start < j.Start || v.Start > j.End) continue;
                if (who.GetValueOrDefault(v.Id).Trio is not { } t || ClassNames.TrioKey(t) != j.TrioKey) continue;
                mine.Add(v);
                var st = VisitStats.From(v, v.EndTime, o);
                if (st.ActiveMinutes < Samples.MinFitMinutes || st.LevelsPerHour is not { } stLvh) continue;
                var (k, p) = play.Party(v.Id);
                // Levels per hour at XP ×1.0, so relevels at different ranks compare.
                rows.Add(new Row(ji, (v.BaseZone, v.Difficulty, (ls - 1) / 5), st.ActiveMinutes, stLvh * j.XpAt(ls),
                    Rates.PerHour(v.Kills, st.ActiveMinutes) ?? 0, k > 0 ? (double)p / k : 0, v));
            }
            crawlsOf.Add(mine);
        }

        var soloIdx = LikeForLike(rows.Where(r => r.Party < SoloBelow), r => r.Lph);
        var killIdx = LikeForLike(rows, r => r.Kph);
        var paces = new List<TrioPace>();
        for (int ji = 0; ji < ordered.Count; ji++)
        {
            var j = ordered[ji];
            var mine = rows.Where(r => r.J == ji).ToList();
            double mins = mine.Sum(r => r.Minutes);
            double grouped = mins > 0 ? mine.Where(r => r.Party >= GroupedFrom).Sum(r => r.Minutes) / mins : 0;
            (double? pace, string kind, double hours) = soloIdx.TryGetValue(ji, out var a) && a.Hours >= 1 ? (a.Index, "solo", a.Hours)
                : killIdx.TryGetValue(ji, out var b) && b.Hours >= 0.5 ? (b.Index, "kills", b.Hours) : ((double?)null, "none", 0.0);
            // A little overlap says little: pull toward ×1.00 as if 2 more hours had run at your usual pace.
            if (pace is { } raw) pace = 1 + (raw - 1) * hours / (hours + PriorHours);
            paces.Add(new TrioPace(j.Trio, j.TrioKey, pace is { } pv ? Math.Round(pv, 2, MidpointRounding.AwayFromZero) : null, kind, Math.Round(hours, 2),
                Math.Round(grouped, 2), j.Deaths, j.Done, j.ActiveHours, j.Rank, ji + 1));
        }
        return new RelevelPaceResult(paces, Detours(ordered, paces, crawlsOf, o));
    }

    /// <summary>A pace as players say it (docs/pace.md: never "×1.05"): "5% faster than your usual".</summary>
    public static string PaceWords(double pace)
    {
        int d = (int)Math.Round(Math.Abs(pace - 1) * 100, MidpointRounding.AwayFromZero);
        return d == 0 ? "at your usual pace" : $"{d}% {(pace > 1 ? "faster" : "slower")} than your usual";
    }

    /// <summary>
    /// Pace by relevel against the others in the same cells: each relevel's rate in a cell (time-weighted), over the
    /// median of every relevel's rate there, for cells two or more relevels share; a weighted geometric mean.
    /// </summary>
    private static Dictionary<int, (double Index, double Hours)> LikeForLike(IEnumerable<Row> rows, Func<Row, double> metric)
    {
        var acc = new Dictionary<int, (double W, double Log)>();
        foreach (var cell in rows.GroupBy(r => r.Cell))
        {
            var byJ = cell.GroupBy(r => r.J).Select(x => (J: x.Key, Rate: x.Sum(r => metric(r) * r.Minutes) / x.Sum(r => r.Minutes), Minutes: x.Sum(r => r.Minutes)))
                .Where(x => x.Rate > 0).ToList();
            if (byJ.Count < 2) continue;
            double med = RouteAnalysis.Median(byJ.Select(x => x.Rate));
            foreach (var x in byJ)
            {
                var a = acc.GetValueOrDefault(x.J);
                acc[x.J] = (a.W + x.Minutes, a.Log + x.Minutes * Math.Log(x.Rate / med));
            }
        }
        return acc.ToDictionary(x => x.Key, x => (Math.Exp(x.Value.Log / x.Value.W), x.Value.W / 60));
    }

    /// <summary>
    /// The crawl stretches of your finished relevels that took longest past your usual hours for those levels (the median
    /// over your finished relevels' crawls): where your hours went, place by place.
    /// </summary>
    private static List<RelevelDetour> Detours(List<Journey> ordered, List<TrioPace> paces, List<List<Visit>> crawlsOf, TrackerOptions o)
    {
        var doneIdx = Enumerable.Range(0, ordered.Count).Where(i => ordered[i].Done).ToList();
        if (doneIdx.Count < 2) return [];
        var model = Journeys.HoursPerLevel(doneIdx.SelectMany(i => crawlsOf[i]), o, doneIdx.Max(i => ordered[i].LevelNow));
        var detours = new List<RelevelDetour>();
        foreach (int i in doneIdx)
            foreach (var leg in ordered[i].Route)
            {
                if (leg.Kind != "crawl") continue;
                if (leg.LevelFrom is not { } a || leg.LevelTo is not { } b || b - a < 2) continue;
                double usual = 0;
                bool ok = true;
                for (int L = a; L < b && L < model.Length; L++) { if (double.IsNaN(model[L])) { ok = false; break; } usual += model[L]; }
                if (!ok || usual <= 0) continue;
                double extra = leg.ActiveMinutes / 60 - usual;
                if (extra < 0.25) continue;
                detours.Add(new RelevelDetour($"{leg.Label} at {a}–{b}", Math.Round(extra, 1),
                    $"{ordered[i].Trio}, relevel {paces[i].Number}: {leg.LevelsPerHour:0.0} Lv/h there, against {(b - a) / usual:0.0} usual for those levels"));
            }
        return detours.OrderByDescending(d => d.Hours).Take(3).ToList();
    }
}
