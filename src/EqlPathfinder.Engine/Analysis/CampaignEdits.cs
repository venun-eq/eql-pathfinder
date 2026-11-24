using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Analysis;

public static partial class Campaign
{
    /// <summary>Plan cost tuple for comparing candidate edits: missed goals, remaining runs, XP, class completion, and preference score.</summary>
    private sealed record Priced(int Missed, int Left, double Xp, int ClassesAt, double Prefer, CampaignView View, double Kept = 0, IReadOnlySet<int>? Broken = null)
    {
        /// <summary>Negative when this plan is the cheaper one.</summary>
        public int CompareTo(Priced o) =>
            Missed != o.Missed ? Missed.CompareTo(o.Missed)
            : Left != o.Left ? Left.CompareTo(o.Left)
            : Math.Abs(Xp - o.Xp) > 1e-6 ? Xp.CompareTo(o.Xp)
            : ClassesAt != o.ClassesAt ? ClassesAt.CompareTo(o.ClassesAt)
            : Math.Abs(Kept - o.Kept) > 1e-6 ? -Kept.CompareTo(o.Kept)
            : Math.Abs(Prefer - o.Prefer) > 1e-6 ? -Prefer.CompareTo(o.Prefer) : 0;

        /// <summary>Whether this plan holds every Must and Never that <paramref name="now"/> does (a change may never break one it kept).</summary>
        public bool KeepsWhatHeld(Priced now) => Broken == null || Broken.IsSubsetOf(now.Broken ?? new HashSet<int>());
    }

    private static Priced Price(CampaignPlan plan, IReadOnlyDictionary<string, int> classRanks, IReadOnlyDictionary<string, int> raceRanks,
        IReadOnlyList<ReincarnationEvent> events, Func<IReadOnlyList<string>, double>? quality, IReadOnlyList<Relevel>? relevels = null)
    {
        var v = Evaluate(plan, classRanks, raceRanks, events, relevels);
        double prefer = quality == null ? 0 : v.Runs.Where(r => r.State is "next" or "planned").Sum(r => quality(r.Classes));
        var combos = plan.Combos ?? [];
        double kept = combos.Where(c => !c.Apart).Sum(c => v.Runs.Count(r => r.State is "next" or "planned" && c.Classes.All(r.Classes.Contains)));
        IReadOnlySet<int>? broken = combos.Count == 0 ? null : Broken(plan, v).Select(b => b.Combo).ToHashSet();
        return new Priced(v.Goals.Sum(g => Math.Max(0, g.Target - g.AtEnd)), v.Left, v.TotalXp, v.ClassesDoneAt ?? 999, prefer, v, kept, broken);
    }

    private static CampaignDelta DeltaOf(Priced now, Priced then, IEnumerable<(IReadOnlyList<string> Before, IReadOnlyList<string> After)> trios,
        IReadOnlyCollection<string>? carries = null)
    {
        var before = now.View.Goals.ToDictionary(g => g.Kind + ":" + g.Key, g => g.AtEnd);
        var fixes = then.View.Goals.Where(g => g.AtEnd >= g.Target && before.GetValueOrDefault(g.Kind + ":" + g.Key) < g.Target).Select(g => g.Kind + ":" + g.Key).ToList();
        var breaks = then.View.Goals.Where(g => g.AtEnd < g.Target && g.AtEnd < before.GetValueOrDefault(g.Kind + ":" + g.Key, g.Target)).Select(g => g.Kind + ":" + g.Key).ToList();
        Func<string, bool> isCarry = carries == null ? ClassNames.Carries.ContainsKey : carries.Contains;   // the old planner: BST among them
        var lost = new List<string>();
        var gained = new List<string>();
        foreach (var (b, a) in trios)
        {
            bool hb = b.Any(ClassNames.IsHealer), ha = a.Any(ClassNames.IsHealer), cb = b.Any(isCarry), ca = a.Any(isCarry);
            if (hb && !ha) lost.Add("heals"); if (!hb && ha) gained.Add("heals");
            if (cb && !ca) lost.Add("a carry"); if (!cb && ca) gained.Add("a carry");
        }
        int sooner = now.View.ClassesDoneAt is { } x && then.View.ClassesDoneAt is { } y ? x - y : 0;
        return new CampaignDelta(then.Left - now.Left, Math.Round(then.Xp - now.Xp, 2), sooner, fixes, breaks, lost.Distinct().ToList(), gained.Distinct().ToList());
    }

    private static CampaignPlan Copy(CampaignPlan p) => new()
    {
        Classes = new(p.Classes, StringComparer.OrdinalIgnoreCase),
        Races = new(p.Races, StringComparer.OrdinalIgnoreCase),
        Locked = [.. p.Locked],
        First = [.. p.First],
        Loadouts = p.Loadouts.Select(l => l.Copy()).ToList(),
        Since = p.Since,
        Prefer = [.. p.Prefer],
        Combos = p.Combos?.Select(c => c.Copy()).ToList(),
        ClassOrder = p.ClassOrder?.Select(w => w.Copy()).ToList(),
        RaceOrder = p.RaceOrder?.Select(w => w.Copy()).ToList(),
    };

    private static string TrioText(CampaignLoadout l) => $"{l.Race} · {string.Join(" / ", l.Classes)}";

    /// <summary>
    /// When the plan misses a goal: the cheapest ways to reach it (a class or race swapped in, a loadout run once more, the
    /// runs it lacks added, the rest rebuilt). When it reaches every goal with runs to spare: the cheapest ways to drop
    /// them. Only loadouts with no run played and no pin change. Up to three, cheapest first.
    /// </summary>
    public static List<CampaignChange> Fixes(CampaignPlan plan, IReadOnlyDictionary<string, int> classRanks, IReadOnlyDictionary<string, int> raceRanks,
        IReadOnlyList<ReincarnationEvent> events, Func<IReadOnlyList<string>, double>? quality, IReadOnlyCollection<string>? carries = null)
    {
        var now = Price(plan, classRanks, raceRanks, events, quality);
        var v = now.View;
        var shortOf = v.Goals.Where(g => g.AtEnd < g.Target).ToList();
        bool spare = shortOf.Count == 0 && v.Left > (ComboFloor(plan, classRanks, raceRanks, events, quality) ?? v.Floor.Runs);
        if (shortOf.Count == 0 && !spare) return [];
        var ls = plan.Loadouts;
        bool Free(int i) => !ls[i].Pinned && v.Loadouts[i].Done == 0;
        var tried = new List<(string Label, CampaignPlan P, List<(IReadOnlyList<string>, IReadOnlyList<string>)> Trios)>();
        void Try(string label, Action<List<CampaignLoadout>> change, params int[] changed)
        {
            var p = Copy(plan);
            var before = changed.Select(i => (IReadOnlyList<string>)[.. p.Loadouts[i].Classes]).ToList();
            change(p.Loadouts);
            var trios = changed.Select((i, k) => (before[k], (IReadOnlyList<string>)(i < p.Loadouts.Count ? [.. p.Loadouts[i].Classes] : before[k]))).ToList();
            tried.Add((label, p, trios));
        }
        var done = v.Loadouts.Select(r => r.Done).ToList();
        var (_, off) = DoneCounts(plan, events);   // a trio played outside the plan is not seated again

        if (!spare)
        {
            foreach (var g in shortOf)
                for (int i = 0; i < ls.Count; i++)
                {
                    if (!Free(i)) continue;
                    var l = ls[i];
                    int ii = i;
                    if (g.Kind == "class")
                    {
                        if (l.Classes.Contains(g.Key)) { if (l.Times < ClassNames.MaxPassiveRank) Try($"Run {TrioText(l)} {l.Times + 1} times", x => x[ii].Times++, ii); }
                        else for (int k = 0; k < 3; k++) { int kk = k; Try($"{TrioText(l)}: {l.Classes[k]} → {g.Key}", x => x[ii].Classes[kk] = g.Key, ii); }
                    }
                    else if (l.Race == g.Key) { if (l.Times < ClassNames.MaxPassiveRank) Try($"Run {TrioText(l)} {l.Times + 1} times", x => x[ii].Times++, ii); }
                    else Try($"{TrioText(l)}: {l.Race} → {g.Key}", x => x[ii].Race = g.Key, ii);
                }
            // Everything as it stands, plus the runs it lacks.
            var keep = Copy(plan);
            foreach (var l in keep.Loadouts) l.Pinned = true;
            var added = Generate(keep, classRanks, raceRanks, done, quality, off).Skip(ls.Count).ToList();
            if (added.Count > 0)
            {
                int n = added.Sum(l => l.Times);
                Try($"Add {(n == 1 ? "the run" : $"the {n} runs")} it lacks", x => x.AddRange(added.Select(l => l.Copy())));
            }
        }
        else
            for (int i = 0; i < ls.Count; i++)
            {
                if (!Free(i)) continue;
                var l = ls[i];
                int ii = i;
                Try($"Drop {TrioText(l)}", x => x.RemoveAt(ii));
                if (l.Times > 1) Try($"Run {TrioText(l)} {(l.Times == 2 ? "once" : "twice")}", x => x[ii].Times--, ii);
            }
        Try("Rebuild the rest", x => { var r = Generate(new CampaignPlan { Classes = plan.Classes, Races = plan.Races, Locked = plan.Locked, First = plan.First, Loadouts = [.. x], Prefer = plan.Prefer, Combos = plan.Combos, ClassOrder = plan.ClassOrder, RaceOrder = plan.RaceOrder }, classRanks, raceRanks, done, quality, off); x.Clear(); x.AddRange(r); });

        var priced = tried.Select(t => (t.Label, t.P, t.Trios, Pr: Price(t.P, classRanks, raceRanks, events, quality))).ToList();
        var good = priced.Where(t => t.Pr.KeepsWhatHeld(now) && (spare ? t.Pr.Missed == 0 && t.Pr.Left < now.Left : t.Pr.Missed < now.Missed && DeltaOf(now, t.Pr, []).Breaks.Count == 0))
            .OrderBy(t => t.Pr, Comparer<Priced>.Create((a, b) => a.CompareTo(b))).ToList();
        var seen = new HashSet<string>();
        return good.Where(t => seen.Add(t.Label)).Take(3)
            .Select(t => new CampaignChange(t.Label, DeltaOf(now, t.Pr, t.Trios, carries), t.P.Loadouts)).ToList();
    }

    /// <summary>
    /// For one loadout: the three cheapest changes that keep every goal (a class swapped in, a class traded with another
    /// loadout, a cheaper race), and every race and class for each slot with what it would do.
    /// </summary>
    public static CampaignOptions? Options(CampaignPlan plan, IReadOnlyDictionary<string, int> classRanks, IReadOnlyDictionary<string, int> raceRanks,
        IReadOnlyList<ReincarnationEvent> events, Func<IReadOnlyList<string>, double>? quality, int index,
        IReadOnlyCollection<string>? carries = null)
    {
        if (index < 0 || index >= plan.Loadouts.Count) return null;
        var now = Price(plan, classRanks, raceRanks, events, quality);
        var v = now.View;
        var ls = plan.Loadouts;
        var l = ls[index];
        (CampaignPlan P, Priced Pr, CampaignDelta D) Changed(Action<List<CampaignLoadout>> change, params int[] at)
        {
            var p = Copy(plan);
            var before = at.Select(i => (IReadOnlyList<string>)[.. p.Loadouts[i].Classes]).ToList();
            change(p.Loadouts);
            var pr = Price(p, classRanks, raceRanks, events, quality);
            return (p, pr, DeltaOf(now, pr, at.Select((i, k) => (before[k], (IReadOnlyList<string>)[.. p.Loadouts[i].Classes])), carries));
        }

        var swaps = new List<(string Label, CampaignPlan P, Priced Pr, CampaignDelta D)>();

        // Every race and class for each slot, the cheapest one that keeps every goal marked best. A class that keeps every
        // goal is a swap too.
        List<CampaignChoice> Slot(IEnumerable<string> values, string current, Func<string, bool> inTrio, Action<List<CampaignLoadout>, string> set, Func<string, string>? swapLabel)
        {
            var rows = new List<(string Value, string State, (CampaignPlan P, Priced Pr, CampaignDelta D)? C)>();
            foreach (var x in values)
            {
                if (x == current) rows.Add((x, "current", null));
                else if (inTrio(x)) rows.Add((x, "trio", null));
                else rows.Add((x, "", Changed(p => set(p, x), index)));
            }
            string? best = null;
            Priced? bestPr = null;
            foreach (var r in rows)
            {
                if (r.C is not { } c || c.D.Breaks.Count > 0 || !c.Pr.KeepsWhatHeld(now)) continue;
                if (swapLabel != null) swaps.Add((swapLabel(r.Value), c.P, c.Pr, c.D));
                if (c.Pr.CompareTo(now) < 0 && (bestPr == null || c.Pr.CompareTo(bestPr) < 0)) { bestPr = c.Pr; best = r.Value; }
            }
            return rows.Select(r => new CampaignChoice(r.Value, r.Value == best ? "best" : r.State, r.C?.D)).ToList();
        }
        var races = Slot(ClassNames.Races, l.Race, _ => false, (x, r) => x[index].Race = r, null);
        var classes = Enumerable.Range(0, 3).Select(k => (IReadOnlyList<CampaignChoice>)Slot(Abbrevs, l.Classes[k], c => l.Classes.Contains(c),
            (x, c) => x[index].Classes[k] = c, c => $"{l.Classes[k]} → {c}")).ToList();
        for (int j = 0; j < ls.Count; j++)
        {
            if (j == index || ls[j].Pinned || v.Loadouts[j].Done > 0) continue;
            for (int a = 0; a < 3; a++)
                for (int b = 0; b < 3; b++)
                {
                    string x = l.Classes[a], y = ls[j].Classes[b];
                    if (l.Classes.Contains(y) || ls[j].Classes.Contains(x)) continue;
                    int aa = a, bb = b, jj = j;
                    var r = Changed(p => { p[index].Classes[aa] = y; p[jj].Classes[bb] = x; }, index, j);
                    if (r.D.Breaks.Count == 0 && r.Pr.KeepsWhatHeld(now)) swaps.Add(($"{x} ↔ {y} with {TrioText(ls[j])}", r.P, r.Pr, r.D));
                }
        }
        foreach (var rc in races.Where(x => x.Delta != null && x.Delta.Breaks.Count == 0))
        {
            var r = Changed(x => x[index].Race = rc.Value, index);
            if (r.Pr.CompareTo(now) < 0) swaps.Add(($"{l.Race} → {rc.Value}", r.P, r.Pr, r.D));
        }
        var seen = new HashSet<string>();
        var top = swaps.OrderBy(s => s.Pr, Comparer<Priced>.Create((a, b) => a.CompareTo(b))).Where(s => seen.Add(s.Label)).Take(3)
            .Select(s => new CampaignChange(s.Label, s.D, s.P.Loadouts)).ToList();
        return new CampaignOptions(index, top, races, classes);
    }
}
