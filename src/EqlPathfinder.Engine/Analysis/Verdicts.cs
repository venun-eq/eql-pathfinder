using EqlPathfinder.Engine.Community;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>How a visit is judged (<see cref="VisitKinds"/>): its kind, and the verdict that kind gets.</summary>
public sealed record Verdict(string Kind, RunPace? Pace, PlaceCheck? Place, FarmPace? Farm, string? NotClear = null);

/// <summary>
/// The step that combines the pure numbers into the verdicts players read, with plain inputs and no service: a leveling visit
/// like for like (<see cref="RunPace"/>) and whether another place at its difficulty was clearly better; a farming or grouped
/// one by T5-eq an hour (<see cref="FarmPace"/>); a raid by its boss; and a stop of consecutive farming visits whole.
/// <c>TrackerService</c> keeps the thread, the store and the caches and calls these.
/// </summary>
public static class Verdicts
{
    /// <summary>One visit's segment judged against the segments at its place and (for the place check) Ask's instance visits <paramref name="instances"/>. <paramref name="community"/> gives the shared view for solo or group (true), asked only when the visit has no usual of its own.</summary>
    public static Verdict Judge(LevelingSegment run, IReadOnlyList<LevelingSegment> instances, IReadOnlyList<LevelingSegment> here, IReadOnlyCollection<int> farmed, int cap,
        Func<bool, CommunityView?>? community = null)
    {
        string kind = VisitKinds.Of(run, farmed, cap);
        if (VisitKinds.ByMotes(kind)) return new(kind, null, null, FarmPace.For(run, here.ToList(), farmed, cap));
        int level = run.LevelStart ?? 1;
        var pace = RunPace.For(run, here.Where(h => VisitKinds.Of(h, farmed, cap) == VisitKinds.Leveling).ToList());
        // No usual of your own: what players who share do here, as context (solo against solo, group against group).
        if (pace.Verdict == "none" && community?.Invoke(run.Grouped) is { } view)
            pace = pace with { Others = OthersHere.At(view, run.Place, level, run.XpMultiplier) };
        // The place check is as of this visit's start: the places as the visits before it left them, so it never changes once the visit is over.
        var (place, notClear) = PlaceCheck.Of(AskData.PlacesAsOf(instances, run.Start, p => PlaceCheck.Alike(p, run.Place)), run.Place, level, run.XpMultiplier, ZoneGates.Open);
        return new(kind, pace, place, null, notClear);
    }

    /// <summary>Every closed crawl with a level judged, by visit id (History's).</summary>
    public static Dictionary<string, Verdict> JudgeAll(IEnumerable<LevelingSegment> segments, IReadOnlyList<LevelingSegment> instances,
        IReadOnlyDictionary<string, List<LevelingSegment>> byPlace, IReadOnlyCollection<int> farmed, int cap)
    {
        var map = new Dictionary<string, Verdict>();
        foreach (var run in segments)
            if (run.VisitId != null && run.Type == "crawl" && run.LevelStart != null)
                map[run.VisitId] = Judge(run, instances, byPlace[run.Place], farmed, cap);
        return map;
    }

    /// <summary>A visit's row with its verdict written in: a raid by its boss, otherwise the kind and what the kind carries.</summary>
    public static VisitStats Apply(VisitStats st, Verdict? j)
    {
        if (st.Raid) return st with { Kind = VisitKinds.Raid };
        if (j == null) return st;
        st = st with { Kind = j.Kind };
        if (j.Pace is { } p) st = st with
        {
            Pace = p.Verdict is "early" ? "none" : p.Verdict,
            KillSpeed = p.KillSpeed,
            XpVerdict = p.Xp,
            PlaceVerdict = j.Place?.Kind,
            BetterPlace = j.Place?.Better
        };
        if (j.Farm is { } f) st = st with { FarmVerdict = f.Verdict, FarmPct = f.Pct, FarmUsual = f.Usual, FarmRuns = f.Runs, FarmBasis = f.Basis, FarmLevels = f.Levels, FarmBand = f.Band };
        return st;
    }

    /// <summary>
    /// A stop is judged whole: consecutive visits (in time order) at one place and difficulty that were judged by motes, as one
    /// against the same usual. <paramref name="rows"/> are newest first, as History sends them; each row of a stop gets its <c>StopFarm</c>.
    /// </summary>
    public static void AssignStops(List<VisitStats> rows)
    {
        for (int i = rows.Count - 1; i >= 0;)
        {
            if (!(rows[i].Kind is { } k && VisitKinds.ByMotes(k))) { i--; continue; }
            int j = i;
            while (j - 1 >= 0 && rows[j - 1].Kind is { } k2 && VisitKinds.ByMotes(k2) && rows[j - 1].BaseZone == rows[i].BaseZone && rows[j - 1].Difficulty == rows[i].Difficulty) j--;
            var stop = FarmPace.ForStop(rows.GetRange(j, i - j + 1));
            for (int n = j; n <= i; n++) rows[n] = rows[n] with { StopFarm = stop };
            i = j - 1;
        }
    }
}
