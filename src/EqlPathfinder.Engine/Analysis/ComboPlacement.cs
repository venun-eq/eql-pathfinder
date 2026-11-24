using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>
/// A run of a Must or Unlock-now combo placed on purpose: the classes it seats, the plan's run step it's due at,
/// and how many of the combo's runs remain (shared array across that combo's slots).
/// </summary>
internal sealed record ComboSlot(string[] Classes, int Pos, int[] Rem, bool Unlock = false, bool Goal = false);

/// <summary>
/// Combo constraint rules and slot scheduling shared by <see cref="Campaign.Generate"/>, <c>Improve</c>, and
/// <see cref="Campaign.Broken"/>: Never keeps classes apart, held "every run" pairs seat both whenever either earns a rank,
/// Must/Unlock-now trios and Must-once pairs reserve explicit slots, and Prefer acts only as a free tie-breaker.
/// </summary>
internal sealed class ComboPlacement
{
    private readonly List<CampaignCombo> _nevers;
    private readonly List<CampaignCombo> _everyRun;
    private readonly List<CampaignCombo> _wanted;
    private readonly List<(string[] Classes, int[] Rem)> _pending = [];
    private readonly List<ComboSlot> _slots = [];
    private int[]? _unlocking;

    private ComboPlacement(List<CampaignCombo> nevers, List<CampaignCombo> everyRun, List<CampaignCombo> wanted)
    {
        _nevers = nevers;
        _everyRun = everyRun;
        _wanted = wanted;
    }

    public bool HasConstraints => _nevers.Count > 0 || _everyRun.Count > 0 || _pending.Count > 0;
    public bool HasPendingSlots => _slots.Count > 0;
    public IReadOnlyList<ComboSlot> Slots => _slots;

    public static ComboPlacement From(IReadOnlyList<CampaignCombo> combos, bool ranked, IReadOnlyList<CampaignWant> rankedClasses,
        IReadOnlyDictionary<string, int> pc, IReadOnlyDictionary<string, int> dc, IReadOnlyList<CampaignLoadout> keptLoadouts,
        IReadOnlyList<CampaignOffPlan>? off = null)
    {
        var nevers = combos.Where(c => c.Apart).ToList();
        var everyRun = combos.Where(c => !c.Apart && c.Classes.Count == 2 && c.EveryRun && c.IsHeld()).ToList();
        var wanted = combos.Where(c => !c.Apart).ToList();
        var placement = new ComboPlacement(nevers, everyRun, wanted);
        placement.ScheduleSlots(combos, ranked, rankedClasses, pc, dc, keptLoadouts, off);
        return placement;
    }

    /// <summary>Whether <paramref name="c"/> is a per-run held constraint (a Never, or a held "every run" pair).</summary>
    public static bool IsHeldRunRule(CampaignCombo c) =>
        c.Apart || (c.Classes.Count == 2 && c.EveryRun && c.IsHeld());

    /// <summary>
    /// Whether a run seating <paramref name="classes"/> breaks <paramref name="c"/> when <paramref name="earnsClassRank"/>
    /// reports which classes are still below <see cref="ClassNames.MaxPassiveRank"/> before the run.
    /// </summary>
    public static bool BreaksHeldRunRule(CampaignCombo c, IReadOnlyList<string> classes, Func<string, bool> earnsClassRank)
    {
        if (!IsHeldRunRule(c)) return false;
        int n = c.Classes.Count(classes.Contains);
        return c.Apart ? n >= 2 : n == 1 && earnsClassRank(c.Classes.First(classes.Contains));
    }

    /// <summary>
    /// Counts violations of Never and held "every run" pair rules across the remaining runs of <paramref name="loadouts"/>
    /// (skipping <paramref name="skip"/> completed runs per loadout). Used by <c>Improve</c> so swaps never add violations.
    /// </summary>
    public static int CountBrokenRuns(IReadOnlyList<CampaignCombo> combos, IReadOnlyList<CampaignLoadout> loadouts,
        IReadOnlyList<int> skip, int[] startClassRanks, IReadOnlyDictionary<string, int> classIndex)
    {
        var held = combos.Where(IsHeldRunRule).ToList();
        if (held.Count == 0) return 0;
        var pc = (int[])startClassRanks.Clone();
        int broke = 0;
        for (int i = 0; i < loadouts.Count; i++)
            for (int p = 0; p < loadouts[i].Times; p++)
            {
                if (p < skip[i]) continue;
                var cls = loadouts[i].Classes;
                foreach (var c in held)
                    if (BreaksHeldRunRule(c, cls, k => pc[classIndex[k]] < ClassNames.MaxPassiveRank)) broke++;
                foreach (var k in cls) pc[classIndex[k]]++;
            }
        return broke;
    }

    /// <summary>Whether <paramref name="trio"/> respects all Never rules, held "every run" pairs, and reserved Must trio capacities.</summary>
    public bool Allowed(IReadOnlyList<string> trio, IReadOnlyDictionary<string, int> pc)
    {
        foreach (var n in _nevers)
            if (BreaksHeldRunRule(n, trio, _ => true)) return false;
        foreach (var p in _everyRun)
            if (BreaksHeldRunRule(p, trio, k => pc[k] < ClassNames.MaxPassiveRank)) return false;
        foreach (var g in _pending)
            if (g.Rem[0] > 0 && g.Classes.Any(trio.Contains) && g.Classes.Max(c => ClassNames.MaxPassiveRank - pc[c] - (trio.Contains(c) ? 1 : 0)) < g.Rem[0])
                return false;
        return true;
    }

    /// <summary>How many wanted (non-Apart) combos still needing placement are satisfied by <paramref name="trio"/>.</summary>
    public double KeptScore(IReadOnlyList<string> trio, IReadOnlyList<CampaignLoadout> placed)
    {
        double s = 0;
        foreach (var c in _wanted)
        {
            if (!c.Classes.All(trio.Contains)) continue;
            int count = placed.Where(l => c.Classes.All(l.Classes.Contains)).Sum(l => l.Times);
            if (c.Classes.Count == 3 ? count < c.Times : c.EveryRun || count == 0) s++;
        }
        return s;
    }

    /// <summary>Pops the next scheduled Must / Unlock-now slot due at <paramref name="step"/>, keeping Unlock-now runs back to back.</summary>
    public ComboSlot? TakeDueSlot(int step, bool hasClassGoals)
    {
        var due = _unlocking == null ? null : _slots.Where(s => s.Rem == _unlocking).OrderBy(s => s.Pos).FirstOrDefault();
        due ??= _slots.OrderBy(s => s.Pos).FirstOrDefault(s => s.Pos <= step || !hasClassGoals);
        _unlocking = due is { Unlock: true } && due.Rem[0] > 1 ? due.Rem : null;
        if (due == null) return null;
        _slots.Remove(due);
        due.Rem[0]--;
        return due;
    }

    private void ScheduleSlots(IReadOnlyList<CampaignCombo> combos, bool ranked, IReadOnlyList<CampaignWant> rankedClasses,
        IReadOnlyDictionary<string, int> pc, IReadOnlyDictionary<string, int> dc, IReadOnlyList<CampaignLoadout> keptLoadouts,
        IReadOnlyList<CampaignOffPlan>? off)
    {
        int spreadOver = Math.Max(1, Campaign.FewestClassRuns(dc.Values)), unlockAt = 0;
        var inGroup = new HashSet<CampaignCombo>();
        if (ranked)
        {
            foreach (var (gc, trio) in BuildUnlockGroups(combos, rankedClasses, dc))
            {
                if (trio != null)
                {
                    var cls = trio.Classes.ToArray();
                    int have = CountMatchingRuns(keptLoadouts, off, cls);
                    int runs = Math.Min(trio.Times - have, cls.Max(k => ClassNames.MaxPassiveRank - pc[k]));
                    inGroup.Add(trio);
                    if (runs <= 0) continue;
                    var rem = new[] { runs };
                    _pending.Add((cls, rem));
                    for (int n = 0; n < runs; n++) _slots.Add(new ComboSlot(cls, unlockAt++, rem, true));
                }
                else
                {
                    int runs = gc.Max(c => dc[c]);
                    var rem = new[] { runs };
                    for (int n = 0; n < runs; n++) _slots.Add(new ComboSlot([.. gc], unlockAt++, rem, true, true));
                }
            }
        }
        foreach (var c in combos.Where(c => !c.Apart && c.IsHeld() && !inGroup.Contains(c)))
        {
            var cls = c.Classes.ToArray();
            int have = CountMatchingRuns(keptLoadouts, off, cls);
            int earns = cls.Max(k => ClassNames.MaxPassiveRank - pc[k]);
            int runs = cls.Length == 3 ? Math.Min(c.Times - have, earns) : !c.EveryRun && have == 0 && earns > 0 ? 1 : 0;
            if (runs <= 0) continue;
            var rem = new[] { runs };
            if (cls.Length == 3) _pending.Add((cls, rem));
            for (int n = 0; n < runs; n++)
                _slots.Add(new ComboSlot(cls, c.UnlockNow ? unlockAt++ : n * Math.Max(spreadOver, runs) / runs, rem, c.UnlockNow));
        }
    }

    /// <summary>
    /// Ranked Unlock-now classes in groups of up to three seats: each joins the earliest group with a free seat and no Never
    /// against a class in it (a held "every run" pair brings its partner along). A held Must trio holding one becomes that group's runs.
    /// </summary>
    private List<(List<string> Classes, CampaignCombo? Trio)> BuildUnlockGroups(IReadOnlyList<CampaignCombo> combos,
        IReadOnlyList<CampaignWant> rankedClasses, IReadOnlyDictionary<string, int> dc)
    {
        var groups = new List<(List<string> Classes, CampaignCombo? Trio)>();
        var trios = combos.Where(c => !c.Apart && c.Classes.Count == 3 && c.IsHeld()).ToList();
        foreach (var w in rankedClasses.Where(w => w.UnlockNow && dc[w.Key] > 0))
        {
            var trio = trios.FirstOrDefault(t => t.Classes.Contains(w.Key));
            var partner = _everyRun.Where(p => p.Classes.Contains(w.Key)).Select(p => p.Classes.First(x => x != w.Key)).FirstOrDefault(x => dc[x] > 0);
            var add = new List<string> { w.Key };
            if (partner != null && trio == null) add.Add(partner);
            bool Fits((List<string> Classes, CampaignCombo? Trio) g) =>
                !g.Classes.Contains(w.Key)
                && (trio == null ? g.Trio == null : g.Trio == trio || g.Trio == null && g.Classes.All(trio.Classes.Contains))
                && g.Classes.Concat(add).Distinct().Count() <= 3
                && !_nevers.Any(n => n.Classes.Any(add.Contains) && n.Classes.Any(g.Classes.Contains));
            int at = groups.FindIndex(Fits);
            if (at < 0) { groups.Add(([], null)); at = groups.Count - 1; }
            var grp = groups[at];
            foreach (var a in add) if (!grp.Classes.Contains(a)) grp.Classes.Add(a);
            groups[at] = (grp.Classes, trio ?? grp.Trio);
        }
        return groups;
    }

    private static int CountMatchingRuns(IReadOnlyList<CampaignLoadout> keptLoadouts, IReadOnlyList<CampaignOffPlan>? off, string[] cls) =>
        keptLoadouts.Where(l => cls.All(l.Classes.Contains)).Sum(l => l.Times) + (off?.Count(o => cls.All(o.Classes.Contains)) ?? 0);
}
