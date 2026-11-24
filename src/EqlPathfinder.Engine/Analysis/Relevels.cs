using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>
/// One relevel the log saw, played or not. <see cref="Level"/> is where its loadout is (the cap when it's done);
/// <see cref="Journey"/> is its crawls, hours and route, null until you've played it (a reincarnation with no visits
/// after it has no journey); <see cref="JourneyIndex"/> is that journey's place in the list given to <see cref="Relevels.Of"/>.
/// <see cref="Number"/> is its place among every relevel by when it started, finished or not. <see cref="EventAt"/> is the time of the reincarnation the log saw for it (null for a journey with none): a plan run is matched to its relevel by it.
/// </summary>
public sealed record Relevel(string Trio, string TrioKey, string? Race, DateTime Reincarnated, int Level, bool Done, Journey? Journey, int? JourneyIndex, int Number, DateTime? EventAt = null);

public static class Relevels
{
    /// <summary>How far apart a journey and a reincarnation event can be and still be one relevel; Campaign groups bursts by it too.</summary>
    public static readonly TimeSpan PairWindow = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Every relevel, oldest first. They come from the reincarnations the log saw (a race with its three classes in one burst,
    /// <see cref="Campaign.Events"/>), each loadout's level from the roster, never a guess. A journey, when there is one, is the
    /// same relevel and adds its hours and route. A journey with no reincarnation event (an older state) still counts.
    /// </summary>
    public static List<Relevel> Of(Roster roster, IReadOnlyList<Journey> journeys, int cap)
    {
        var used = new HashSet<int>();
        var rows = new List<(string Trio, string Key, string? Race, DateTime At, int Level, bool Done, int? Index, DateTime? Ev)>();
        // A journey is the event nearest it in time, within 300 s; the classes are not compared (a burst of reincarnations
        // minutes apart gives events other classes than the journeys have). The nearest pairs are made first.
        var events = Campaign.Events(roster);
        var pairs = new List<(int E, int J, double Gap)>();
        for (int k = 0; k < events.Count; k++)
            for (int i = 0; i < journeys.Count; i++)
            {
                double gap = (journeys[i].Reincarnated - events[k].At).Duration().TotalSeconds;
                if (gap <= PairWindow.TotalSeconds) pairs.Add((k, i, gap));
            }
        var matched = new Dictionary<int, int>();
        foreach (var p in pairs.OrderBy(p => p.Gap))
            if (!matched.ContainsKey(p.E) && used.Add(p.J)) matched[p.E] = p.J;
        for (int k = 0; k < events.Count; k++)
        {
            var e = events[k];
            if (matched.TryGetValue(k, out int idx))
            {
                var j = journeys[idx];
                rows.Add((j.Trio, j.TrioKey, j.Race ?? e.Race, j.Reincarnated, j.LevelNow, j.Done, idx, e.At));
                continue;
            }
            // Not played: a class reincarnated again since left it at the cap (reincarnating needs it), otherwise the lowest
            // of its classes' levels (a reincarnation sets it to 1).
            var incs = e.Classes.Select(c => roster.Classes.GetValueOrDefault(c)).ToList();
            bool left = incs.Any(i => i != null && i.Seen.Any(t => t > e.At.AddSeconds(30)));
            int level = left ? cap : Math.Min(cap, incs.Select(i => i?.Level ?? 1).DefaultIfEmpty(1).Min());
            rows.Add((e.TrioKey, e.TrioKey, e.Race, e.At, level, level >= cap, null, e.At));
        }
        for (int i = 0; i < journeys.Count; i++)
            if (!used.Contains(i)) rows.Add((journeys[i].Trio, journeys[i].TrioKey, journeys[i].Race, journeys[i].Reincarnated, journeys[i].LevelNow, journeys[i].Done, i, null));
        return rows.OrderBy(r => r.At).ThenBy(r => r.Key, StringComparer.Ordinal)
            .Select((r, n) => new Relevel(r.Trio, r.Key, r.Race, r.At, r.Level, r.Done, r.Index is { } x ? journeys[x] : null, r.Index, n + 1, r.Ev)).ToList();
    }
}
