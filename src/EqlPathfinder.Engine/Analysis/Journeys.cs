using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>
/// A stretch of a relevel spent in one place. <see cref="Kind"/>: "crawl" (a Dungeon Crawl instance), "instance" (any
/// other instance: " - Solo", " - Group"), or "open world" (kills outside instances). <see cref="Crawls"/> counts the
/// visits or hunting stretches it joins.
/// </summary>
public sealed record JourneyLeg(string BaseZone, int? Difficulty, int? LevelFrom, int? LevelTo, int Crawls,
    double ActiveMinutes, double Levels, double T5, string Kind = "crawl")
{
    public string Label => (BaseZone.StartsWith("The ") ? BaseZone[4..] : BaseZone) + (Difficulty is { } d ? $" D{d}" : "")
        + (Kind == "open world" ? " (open world)" : "");
    /// <summary>Kills in the leg, and whether a crawl in it was finished: the rule for having a rate (<see cref="Rates"/>).</summary>
    public int Kills { get; init; }
    public bool Finished { get; init; }
    public double? LevelsPerHour => Rates.PerHour(Levels, ActiveMinutes, Finished, Kills);
    public double? T5PerHour => Rates.PerHour(T5, ActiveMinutes, Finished, Kills);
}

/// <summary>A point on a relevel's level-by-active-hours curve.</summary>
public sealed record CurvePoint(double Hours, double Level);

/// <summary>
/// One trio's relevel after reincarnating, everywhere its XP came from: crawls to the cap, other instances
/// (<see cref="InstanceHours"/>), and open-world hunting (<see cref="FieldHours"/>), all inside <see cref="ActiveHours"/>.
/// <see cref="Crawls"/> counts Dungeon Crawls only.
/// </summary>
public sealed record Journey(
    string Trio, string TrioKey, string? Race, DateTime Reincarnated, DateTime Start, DateTime End,
    int LevelFrom, int LevelNow, bool Done, double ActiveHours, double WallHours, int Crawls, int Kills, int Deaths,
    double T5, double Levels, int Rank, IReadOnlyList<JourneyLeg> Route, double? EtaHours, double? Pace, string? EtaNote,
    IReadOnlyList<CurvePoint> Curve, IReadOnlyList<CurvePoint>? Projection = null, double FieldHours = 0, double FieldLevels = 0,
    double InstanceHours = 0, double InstanceLevels = 0)
{
    /// <summary>The count at the bar of a level during this relevel (<see cref="Incarnation.RankAt"/>); null: <see cref="Rank"/> at every level.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Func<int, int>? RankAtLevel { get; init; }
    /// <summary>The level the hours to go reach (<see cref="EtaHours"/>): the highest level a finished relevel reached, where the model of hours ends. Null with no hours.</summary>
    public int? EtaTo { get; init; }
    public double XpAt(int level) => Planner.XpMultiplier(RankAtLevel?.Invoke(level) ?? Rank);

    /// <summary>"D4", or "mixed": what Home compares this relevel with (<see cref="DifficultyMix"/>).</summary>
    public string? Mostly => DifficultyMix.Of(this);

    /// <summary>The level it has reached: the loadout's, or the curve's last point if that ran past it (the race and Home read this one).</summary>
    public double ReachedLevel => Curve.Count > 0 ? Math.Max(LevelNow, Curve[^1].Level) : LevelNow;

    /// <summary>Played for motes: farmed difficulties hold over half its hours (<see cref="DifficultyMix.Farmed(Journey, IReadOnlyCollection{int})"/>).</summary>
    public bool Farmed { get; init; }

    /// <summary>T5-eq per active hour over the whole relevel.</summary>
    public double T5PerHour => ActiveHours > 0 ? T5 / ActiveHours : 0;

    /// <summary>The median T5-eq/h of your other finished relevels of the same kind (<see cref="Mostly"/>); null with fewer than two.</summary>
    public double? TypicalOfKind { get; init; }

    /// <summary>Kill speed over this relevel's crawls against your usual at each place (<see cref="KillSpeeds"/>); set when History is sent.</summary>
    public KillPace? KillPace { get; init; }

    /// <summary>Crawl minutes: the part of the relevel that says what kind it is (open world has no difficulty).</summary>
    public double CrawlMinutes => Route.Where(l => l.Kind == "crawl" && l.Difficulty != null).Sum(l => l.ActiveMinutes);
}

public static class Journeys
{
    /// <summary>Each relevel with the median T5-eq/h of your other finished relevels of its kind, when there are two or more.</summary>
    public static List<Journey> WithTypicals(IReadOnlyList<Journey> all) => all.Select(j =>
    {
        var like = all.Where(x => !ReferenceEquals(x, j) && x.Done && x.ActiveHours > 0 && x.Mostly == j.Mostly).Select(x => x.T5PerHour).ToList();
        return like.Count >= 2 ? j with { TypicalOfKind = RouteAnalysis.Median(like) } : j;
    }).ToList();

    /// <summary>
    /// Groups crawls by trio and race after the trio's classes reincarnated. A journey ends at the crawl
    /// that reaches the cap; later crawls with the same trio are farming. Open-world stretches join the
    /// relevel of the trio whose crawls they sit between.
    /// </summary>
    public static List<Journey> Build(IReadOnlyList<Visit> visits, EngineState state, TrackerOptions o, IReadOnlyList<FieldStretch>? field = null,
        IReadOnlyDictionary<string, int>? spellClasses = null) => Build(PlayFacts.From(visits, state, spellClasses), state, o, field);

    /// <inheritdoc cref="Build(IReadOnlyList{Visit}, EngineState, TrackerOptions, IReadOnlyList{FieldStretch}?, IReadOnlyDictionary{string, int}?)"/>
    public static List<Journey> Build(PlayFacts facts, EngineState state, TrackerOptions o, IReadOnlyList<FieldStretch>? field = null)
    {
        var visits = facts.Visits;
        var roster = state.Roster;
        var who = facts.Pairs;
        var built = new List<(Journey j, List<Visit> visits)>();
        var fieldByTrio = FieldByTrio(visits, who, field, o);
        // A relevel starts at the latest reincarnation of its classes before the crawl, so reincarnating one of
        // them again later starts a new relevel and leaves the finished one in place.
        // Crawls, and any other instance you killed something in (a "- Solo" task, a "- Group" instance).
        var byRelevel = visits.Where(v => Counts(v, o) && who.GetValueOrDefault(v.Id).Trio != null)
            .GroupBy(v => ClassNames.TrioKey(who[v.Id].Trio!))
            .SelectMany(g => g.GroupBy(v => StartOf(roster, g.Key.Split('/'), v.Start)).Where(x => x.Key != null)
                .Select(x => (Key: g.Key, Since: x.Key!.Value, Visits: x.ToList())));
        foreach (var g in byRelevel)
        {
            var classes = g.Key.Split('/');
            var incs = classes.Select(c => roster.Classes[c]).ToList();
            var since = g.Since;
            var list = new List<Visit>();
            // Classes that all reached the cap of their day left the relevel then: later visits are play past it.
            DateTime? reached = incs.All(i => i.ReachedCap != null) ? incs.Max(i => i.ReachedCap) : null;
            foreach (var v in g.Visits.OrderBy(v => v.Start))
            {
                if (reached is { } rc0 && v.Start > rc0) break;
                list.Add(v);
                if (v.LevelEnd is { } le && le >= o.CapOn(v.Start, le)) break;   // the cap of that visit's day: done at 50 stays done
            }
            if (list.Count == 0) continue;

            // Race from /who (excluding illusions), falling back to the race reincarnated in the same burst.
            string? race = list.Select(v => who[v.Id].Race).Where(r => r != null).GroupBy(r => r)
                .OrderByDescending(x => x.Count()).Select(x => x.Key).FirstOrDefault();
            var burst = incs.Select(i => LastAt(i, since)).Where(t => t != null).Select(t => t!.Value).ToList();
            race ??= roster.Races.Values
                .Where(r => Times(r).Any(rt => burst.Count(t => Math.Abs((t - rt).TotalSeconds) <= 5) >= 2))
                .Select(r => r.Name).FirstOrDefault();

            var stats = list.Select(v => VisitStats.From(v, v.EndTime, o)).ToList();
            // Hunting up to the next reincarnation of any of these classes.
            var until = incs.SelectMany(Times).Where(t => t > since.AddSeconds(30)).DefaultIfEmpty(DateTime.MaxValue).Min();
            var hunts = fieldByTrio.GetValueOrDefault(g.Key)?.Where(f => f.Start >= since && f.Start < until).ToList() ?? [];
            int from = list.Where(v => v.LevelStart != null).Select(v => v.LevelStart!.Value)
                .Concat(hunts.Where(f => f.Level != null).Select(f => f.Level!.Value)).DefaultIfEmpty(1).Min();
            // Exclude loadouts already at the cap swapped in to raid or farm (not an active relevel).
            if (from >= o.CapOn(list[0].Start, from)) continue;
            // A class that has reincarnated again since left this relevel at the cap of that day (reincarnating needs it); one that
            // reached the cap of its day is there for good, whatever the cap is now.
            int? ClassCap(Incarnation i) => Times(i).Where(t => t > since.AddSeconds(30)).Select(t => (int?)o.CapOn(t)).Max()
                ?? (i.ReachedCap is { } rc ? o.CapOn(rc) : i.Level is { } il && il >= o.CapOn(i.LastActive ?? since, il) ? o.CapOn(i.LastActive ?? since, il) : null);
            int ClassLevel(Incarnation i) => ClassCap(i) ?? i.Level ?? 0;
            int now = Math.Max(list.Where(v => v.LevelEnd != null).Select(v => v.LevelEnd!.Value).DefaultIfEmpty(from).Max(),
                incs.Select(ClassLevel).DefaultIfEmpty(0).Min());
            // Done: a visit ended at or above the cap of its own day, or every class is at the cap of its day. Later play never reopens it.
            int? doneAt = list.Where(v => v.LevelEnd is { } e && e >= o.CapOn(v.Start, e)).Select(v => (int?)o.CapOn(v.Start, v.LevelEnd)).FirstOrDefault();
            if (doneAt == null && incs.All(i => ClassCap(i) != null)) doneAt = incs.Max(i => ClassCap(i));
            bool done = doneAt != null;
            int capNow = doneAt ?? Math.Min(now, o.MaxCap);
            // Use the trio's reincarnation rank at the time of play (highest class rank in the trio, not the race).
            int rank = incs.Max(i => RankAt(i, since));
            var trioDisplay = list.Select(v => who[v.Id].Trio).Last(t => t != null)!;
            double fieldMin = hunts.Sum(f => f.Minutes), fieldLv = hunts.Sum(f => f.Levels);
            double instMin = 0, instLv = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Difficulty == null) { instMin += stats[i].ActiveMinutes; instLv += stats[i].LevelsGained; }
            var start = hunts.Count > 0 && hunts[0].Start < list[0].Start ? hunts[0].Start : list[0].Start;
            var end = hunts.Count > 0 && hunts[^1].End > list[^1].EndTime ? hunts[^1].End : list[^1].EndTime;
            built.Add((new Journey(
                trioDisplay, g.Key, race, since, start, end, from, capNow, done,
                (stats.Sum(s => s.ActiveMinutes) + fieldMin) / 60, (end - start).TotalHours, list.Count(v => v.Difficulty != null),
                list.Sum(v => v.Kills) + hunts.Sum(f => f.Kills), list.Sum(v => v.Deaths), stats.Sum(s => s.T5), stats.Sum(s => s.LevelsGained) + fieldLv, rank,
                Legs(list, stats, hunts), null, null, null, Curve(list, stats, hunts, from, capNow, done, o),
                null, fieldMin / 60, fieldLv, instMin / 60, instLv)
            { RankAtLevel = L => roster.TrioRankAt(classes, L, o.BaseCap, since) }, list));
        }
        // Estimate the rest of the way for trios still leveling, from the finished ones.
        var finishedJourneys = built.Where(b => b.j.Done).ToList();
        var model = HoursPerLevel(finishedJourneys.SelectMany(b => b.visits), o, finishedJourneys.Select(b => b.j.LevelNow).DefaultIfEmpty(0).Max());
        var result = built.Select(b => b.j.Done ? b.j : WithEta(b.j, model, o)).Select(j => j with { Farmed = DifficultyMix.Farmed(j, o.FarmDifficulties) }).ToList();
        result = WithTypicals(result);
        return result.OrderByDescending(j => j.Start).ToList();
    }

    /// <summary>
    /// Effective trio and race per visit. A crawl before your first /who after a loadout change has no trio;
    /// it takes the next confirmed trio if the levels carry on without a gap.
    /// </summary>
    public static Dictionary<string, (string? Trio, string? Race)> Attribute(IReadOnlyList<Visit> visits)
    {
        var result = new Dictionary<string, (string? Trio, string? Race)>();
        var pending = new List<Visit>();
        foreach (var v in visits.OrderBy(v => v.Start))
        {
            // Played on a loadout no /who named: no trio, and the next /who is another loadout's, so it fills nothing back.
            if (v.TrioFrom == "unmet" && !v.TrioConfirmed) { result[v.Id] = (null, null); continue; }
            if (v.Trio == null) { pending.Add(v); continue; }
            result[v.Id] = (v.Trio, v.Race);
            int? expect = v.LevelStart;
            for (int i = pending.Count - 1; i >= 0 && expect != null; i--)
            {
                var p = pending[i];
                if (p.LevelStart is not { } ps || p.LevelEnd is not { } pe || pe > expect || expect - pe > 2 || ps > pe) break;
                result[p.Id] = (v.Trio, v.Race);
                expect = ps;
            }
            pending.Clear();
        }
        foreach (var p in pending) result[p.Id] = (null, null);
        return result;
    }

    /// <summary>Every reincarnation the log saw for a class or race (older states only kept the last).</summary>
    private static IEnumerable<DateTime> Times(Incarnation i) =>
        i.Seen.Count > 0 ? i.Seen : i.Reincarnated is { } r ? [r] : [];

    /// <summary>The class's last reincarnation at or before <paramref name="t"/> (30 s of slack for the burst).</summary>
    private static DateTime? LastAt(Incarnation i, DateTime t)
    {
        DateTime? best = null;
        foreach (var x in Times(i)) if (x <= t.AddSeconds(30) && (best == null || x > best)) best = x;
        return best;
    }

    /// <summary>When the relevel a crawl at <paramref name="t"/> belongs to started: the latest of its classes' reincarnations before it.</summary>
    private static DateTime? StartOf(Roster roster, string[] classes, DateTime t)
    {
        DateTime? start = null;
        foreach (var c in classes)
        {
            if (roster.Classes.GetValueOrDefault(c) is not { } i || LastAt(i, t) is not { } last) return null;
            if (start == null || last > start) start = last;
        }
        return start;
    }

    /// <summary>Rank during the relevel that started at <paramref name="since"/>: today's rank less the reincarnations seen after it.</summary>
    public static int RankAt(Incarnation i, DateTime since) => Math.Max(0, i.Rank - i.Seen.Count(t => t > since.AddSeconds(30)));

    /// <summary>
    /// Open-world stretches by the trio of the nearest crawl in time (within 3 hours). Kills don't name the
    /// loadout, but you rarely swap trios between a crawl and the hunting next to it.
    /// </summary>
    private static Dictionary<string, List<FieldStretch>> FieldByTrio(IReadOnlyList<Visit> visits,
        IReadOnlyDictionary<string, (string? Trio, string? Race)> who, IReadOnlyList<FieldStretch>? field, TrackerOptions o)
    {
        var result = new Dictionary<string, List<FieldStretch>>();
        if (field == null || field.Count == 0) return result;
        var crawls = visits.Where(v => Counts(v, o) && who.GetValueOrDefault(v.Id).Trio != null).OrderBy(v => v.Start).ToList();
        if (crawls.Count == 0) return result;
        var starts = crawls.Select(v => v.Start).ToArray();
        foreach (var f in field.OrderBy(f => f.Start))
        {
            int i = Array.BinarySearch(starts, f.Start);
            if (i < 0) i = ~i - 1;
            Visit? best = null;
            double bestGap = double.MaxValue;
            foreach (int k in new[] { i, i + 1 })
            {
                if (k < 0 || k >= crawls.Count) continue;
                var v = crawls[k];
                double gap = f.End < v.Start ? (v.Start - f.End).TotalMinutes : f.Start > v.EndTime ? (f.Start - v.EndTime).TotalMinutes : 0;
                if (gap < bestGap) { bestGap = gap; best = v; }
            }
            if (best == null || bestGap > 180) continue;
            var key = ClassNames.TrioKey(who[best.Id].Trio!);
            (result.TryGetValue(key, out var l) ? l : result[key] = new()).Add(f);
        }
        return result;
    }

    /// <summary>Level reached after each crawl or open-world stretch against active hours so far (for the relevel race chart).</summary>
    private static List<CurvePoint> Curve(List<Visit> list, List<VisitStats> stats, List<FieldStretch> hunts, int from, int now, bool done, TrackerOptions o)
    {
        var pts = new List<CurvePoint> { new(0, from) };
        double h = 0;
        var segments = list.Select((v, i) => (Time: v.Start, Minutes: stats[i].ActiveMinutes, Level: v.LevelStart, Gained: stats[i].LevelsGained))
            .Concat(hunts.Select(f => (Time: f.Start, f.Minutes, f.Level, Gained: f.Levels)))
            .OrderBy(x => x.Time);
        foreach (var seg in segments)
        {
            h += seg.Minutes / 60;
            if (seg.Level is { } ls)
                pts.Add(new(Math.Round(h, 3), Math.Round(Math.Max(pts[^1].Level, Math.Min(o.CapOn(seg.Time, ls), ls + seg.Gained)), 2)));
        }
        // A level can ding outside an instance (the last one of a relevel often does): the curve reaches it.
        int reached = now;
        if (pts[^1].Level < reached) pts.Add(new(Math.Round(h, 3), reached));
        return pts;
    }

    /// <summary>
    /// The relevel's route in time order: crawls, other instances, and open-world hunting, each its own leg, however
    /// small (a ding between two crawls shows as the open-world leg it happened in). Back-to-back stays in one place join.
    /// </summary>
    private static List<JourneyLeg> Legs(List<Visit> list, List<VisitStats> stats, List<FieldStretch> hunts)
    {
        var parts = list.Select((v, i) => (Time: v.Start, Leg: new JourneyLeg(v.BaseZone, v.Difficulty, v.LevelStart, v.LevelEnd, 1,
                stats[i].ActiveMinutes, stats[i].LevelsGained, stats[i].T5, v.Difficulty != null ? "crawl" : "instance")
        { Kills = v.Kills, Finished = v.Completed }))
            .Concat(hunts.Select(f => (Time: f.Start, Leg: new JourneyLeg(f.Zone ?? "Open world", null, f.Level, f.LevelEnd ?? f.Level, 1,
                f.Minutes, f.Levels, 0, "open world")
            { Kills = f.Kills })))
            .OrderBy(x => x.Time).Select(x => x.Leg);
        var legs = new List<JourneyLeg>();
        foreach (var leg in parts)
        {
            if (legs.Count > 0 && legs[^1] is var p && p.Kind == leg.Kind && p.BaseZone == leg.BaseZone && p.Difficulty == leg.Difficulty)
                legs[^1] = p with
                {
                    LevelTo = leg.LevelTo ?? p.LevelTo,
                    Crawls = p.Crawls + 1,
                    ActiveMinutes = p.ActiveMinutes + leg.ActiveMinutes,
                    Levels = p.Levels + leg.Levels,
                    T5 = p.T5 + leg.T5,
                    Kills = p.Kills + leg.Kills,
                    Finished = p.Finished || leg.Finished,
                };
            else legs.Add(leg);
        }
        return legs;
    }

    /// <summary>A visit that belongs in a relevel: a crawl or instance with kills, excluding cap-loadout play.</summary>
    private static bool Counts(Visit v, TrackerOptions o) => (v.Difficulty != null || v.Kills > 0) && !v.PlayedOnACapLoadout(o.CapOn(v));

    /// <summary>
    /// Median active hours to gain each level, from finished journeys' crawls (index = level). The model reaches the highest level a
    /// finished relevel reached (its top): a level nobody played has no hours (index at the top and above is NaN).
    /// </summary>
    public static double[] HoursPerLevel(IEnumerable<Visit> finishedJourneyVisits, TrackerOptions o, int? reached = null)
    {
        var finished = finishedJourneyVisits.ToList();
        int top = Math.Min(o.MaxCap, reached ?? finished.Select(v => v.LevelEnd ?? 0).DefaultIfEmpty(0).Max());
        var samples = new List<double>[o.MaxCap + 1];
        for (int i = 0; i <= o.MaxCap; i++) samples[i] = new();
        foreach (var v in finished.Where(v => v.LevelStart != null && v.LevelsGained > 0.05))
        {
            var s = VisitStats.From(v, v.EndTime, o);
            if (s.ActiveMinutes < Samples.MinActiveMinutes) continue;
            double hpl = s.ActiveMinutes / 60 / s.LevelsGained;
            // Spread the crawl's rate over the levels it covered.
            int a = v.LevelStart!.Value, b = Math.Max(a, (int)Math.Floor(a + s.LevelsGained - 0.001));
            for (int L = a; L <= Math.Min(b, top - 1); L++) samples[L].Add(hpl);
        }
        var model = new double[o.MaxCap + 1];
        Array.Fill(model, double.NaN);
        for (int L = 1; L < top; L++) model[L] = samples[L].Count > 0 ? RouteAnalysis.Median(samples[L]) : double.NaN;
        // Fill gaps from the nearest known levels (below the top).
        for (int L = 1; L < top; L++)
        {
            if (!double.IsNaN(model[L])) continue;
            int lo = L - 1; while (lo >= 1 && double.IsNaN(model[lo])) lo--;
            int hi = L + 1; while (hi < top && double.IsNaN(model[hi])) hi++;
            model[L] = lo >= 1 && hi < top ? (model[lo] + model[hi]) / 2 : lo >= 1 ? model[lo] : hi < top ? model[hi] : double.NaN;
        }
        return model;
    }

    /// <summary>The level the model's hours reach: the first level with none.</summary>
    private static int TopOf(double[] model) { int t = 1; while (t < model.Length && !double.IsNaN(model[t])) t++; return t; }

    private static Journey WithEta(Journey j, double[] model, TrackerOptions o)
    {
        int top = TopOf(model);
        if (top <= 1 || j.LevelNow >= top || model.Skip(1).Take(top - 1).Any(double.IsNaN)) return j;
        double ModelHours(int a, int b) { double h = 0; for (int L = a; L < b && L < top; L++) h += model[L]; return h; }
        double remaining = ModelHours(j.LevelNow, top);
        double pace = 1;
        string note;
        double soFar = ModelHours(j.LevelFrom, j.LevelNow);
        if (j.ActiveHours >= 1 && soFar > 0.5)
        {
            pace = Math.Clamp(j.ActiveHours / soFar, 0.5, 2.0);
            note = pace >= 1.05 ? $"{(pace - 1) * 100:0}% slower than your finished trios at these levels"
                 : pace <= 0.95 ? $"{(1 - pace) * 100:0}% faster than your finished trios at these levels"
                 : "on your finished trios' pace";
        }
        else note = "from the finished trios' pace";
        // The rest of the way, level by level, at the finished trios' pace adjusted by this one's.
        var projection = new List<CurvePoint> { new(Math.Round(j.ActiveHours, 3), j.LevelNow) };
        double h = j.ActiveHours;
        for (int L = j.LevelNow; L < top; L++)
        {
            h += model[L] * pace;
            projection.Add(new(Math.Round(h, 3), L + 1));
        }
        return j with { EtaHours = remaining * pace, Pace = pace, EtaNote = note, Projection = projection, EtaTo = top };
    }
}
