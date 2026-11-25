using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>
/// Kill speed summary and pre-formatted label: <paramref name="Word"/> ("On pace", "24% slow", "21% fast", or null
/// without a usual) and <paramref name="Tone"/> ("good" or "warn"). Computed by the engine; pages render only.
/// </summary>
public sealed record KillPace(double? Speed, int Runs, string? Word, string? Tone);

public static class KillSpeeds
{
    /// <summary>Each visit against its usual, weighted by its active minutes, counting only visits that had a usual.</summary>
    public static KillPace Over(IEnumerable<VisitStats> rows)
    {
        double w = 0, s = 0;
        int runs = 0;
        foreach (var v in rows)
            if (v.KillSpeed is { } k) { runs++; w += v.ActiveMinutes; s += k * v.ActiveMinutes; }
        double? speed = w > 0 ? s / w : null;
        return new KillPace(speed, runs, Word(speed), Tone(speed));
    }

    /// <summary>Outside ±15% (<see cref="RunPace.NarrowBand"/>), one run's normal swing, it's slow or fast.</summary>
    public static string? Word(double? speed) =>
        speed is not { } k ? null
        : k < 1 - RunPace.NarrowBand ? $"{Math.Round((1 - k) * 100, MidpointRounding.AwayFromZero):0}% slow"
        : k > 1 + RunPace.NarrowBand ? $"{Math.Round((k - 1) * 100, MidpointRounding.AwayFromZero):0}% fast"
        : "On pace";

    public static string? Tone(double? speed) => speed is { } k ? (k < 1 - RunPace.NarrowBand ? "warn" : "good") : null;
}

/// <summary>Least-squares fits, one copy for every page.</summary>
public static class Fit
{
    /// <summary>The slope of y on x, or null under two points or with every x the same.</summary>
    public static double? Slope(IReadOnlyList<(double X, double Y)> pts)
    {
        if (pts.Count < 2) return null;
        double mx = pts.Average(p => p.X), my = pts.Average(p => p.Y);
        double sxx = pts.Sum(p => (p.X - mx) * (p.X - mx));
        return sxx > 0 ? pts.Sum(p => (p.X - mx) * (p.Y - my)) / sxx : null;
    }
}

/// <summary>
/// A run of History rows in time order that belong together: one relevel (<paramref name="Relevel"/> is its index among the
/// relevels by start), play at the cap, or leveling outside a relevel. A relevel's crawls with a rate give the old and new
/// levels/h and about what % a level it changed.
/// </summary>
public sealed record Stretch(int Id, int? Relevel, string Kind, KillPace KillPace, int Crawls, double? OldLevelsPerHour, double? NewLevelsPerHour,
    bool Fell, int? PerLevelPercent);

public static class Stretches
{
    /// <summary>A finished relevel's crawls run to five minutes past its end (the last crawl may close after the level-up).</summary>
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gives each row its <see cref="VisitStats.Stretch"/> and <see cref="VisitStats.StopPace"/>, and returns the stretches in time order.
    /// <paramref name="newestFirst"/> is History's order; the rows are replaced in place.
    /// </summary>
    public static List<Stretch> Assign(List<VisitStats> newestFirst, IReadOnlyList<Journey> journeys, int cap)
    {
        var js = journeys.OrderBy(j => j.Start).ToList();
        var groups = new List<(string Key, List<int> At)>();   // indexes into newestFirst, oldest first
        for (int i = newestFirst.Count - 1; i >= 0; i--)
        {
            var v = newestFirst[i];
            string key = RelevelOf(js, v) is var r and >= 0 ? "j" + r : (v.AtCap || (v.LevelStart ?? 0) >= cap) ? "cap" : "lvl";
            if (groups.Count > 0 && groups[^1].Key == key) groups[^1].At.Add(i); else groups.Add((key, [i]));
        }
        for (int id = 0; id < groups.Count; id++)
        {
            var at = groups[id].At;
            // A stop: consecutive crawls here at one place and difficulty. Rows without a difficulty aren't crawls and are skipped,
            // as the Relevels page's legs skip them (race.js journeyVisits), so a stop and a leg are the same visits.
            var run = new List<int>();
            void Close()
            {
                var pace = KillSpeeds.Over(run.Select(i => newestFirst[i]));
                foreach (var i in run) newestFirst[i] = newestFirst[i] with { StopPace = pace };
                run.Clear();
            }
            foreach (var i in at)
            {
                var v = newestFirst[i];
                if (v.Difficulty == null) continue;
                if (run.Count > 0 && (newestFirst[run[0]].BaseZone != v.BaseZone || newestFirst[run[0]].Difficulty != v.Difficulty)) Close();
                run.Add(i);
            }
            if (run.Count > 0) Close();
            foreach (var i in at) newestFirst[i] = newestFirst[i] with { Stretch = id };
        }
        return Describe(newestFirst, journeys, cap);
    }

    private static int RelevelOf(List<Journey> oldestFirst, VisitStats v) =>
        oldestFirst.FindIndex(j => v.Start >= j.Start && (!j.Done || v.Start <= j.End + Grace));

    /// <summary>The stretches of rows that already carry their <see cref="VisitStats.Stretch"/> (from <see cref="Assign"/>), in time order. Reads the ids, never reassigns them.</summary>
    public static List<Stretch> Describe(IReadOnlyList<VisitStats> newestFirst, IReadOnlyList<Journey> journeys, int cap)
    {
        var js = journeys.OrderBy(j => j.Start).ToList();
        var infos = new List<Stretch>();
        foreach (var g in newestFirst.GroupBy(v => v.Stretch).OrderBy(g => g.Key))
        {
            var rows = g.OrderBy(v => v.Start).ToList();
            int rel = RelevelOf(js, rows[0]);
            string kind = rel >= 0 ? "relevel" : (rows[0].AtCap || (rows[0].LevelStart ?? 0) >= cap) ? "cap" : "lvl";
            // A relevel's crawls with a rate: levels/h from the oldest to the newest, and the fit of its log against the level.
            var crawls = rel < 0 ? [] : rows.Where(v => v.Difficulty != null && v.LevelsPerHour > 0 && !v.AtCap && (v.LevelStart ?? 0) < cap).ToList();
            double? old = null, neu = null;
            int? perLevel = null;
            if (crawls.Count >= 2)
            {
                old = crawls.MinBy(v => v.Start)!.LevelsPerHour;
                neu = crawls.MaxBy(v => v.Start)!.LevelsPerHour;
                var pts = crawls.Select(v => ((double)v.LevelStart! + v.LevelsGained / 2, Math.Log(v.LevelsPerHour!.Value))).Where(q => double.IsFinite(q.Item2)).ToList();
                if (Fit.Slope(pts) is { } per && per < 0) perLevel = (int)Math.Round((1 - Math.Exp(per)) * 100, MidpointRounding.AwayFromZero);
            }
            infos.Add(new Stretch(g.Key, rel >= 0 ? rel : null, kind, KillSpeeds.Over(rows), crawls.Count, old, neu, old != null && neu <= old, perLevel));
        }
        return infos;
    }
}
