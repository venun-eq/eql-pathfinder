using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Analysis;

public static partial class Campaign
{
    private static bool Future(CampaignRun r) => r.State is "next" or "planned";

    /// <summary>The combos' classes a Must takes past their goals: the goal tile's line (the goal is never written).</summary>
    private static IReadOnlyList<CampaignPastGoal>? PastGoalsOf(CampaignPlan plan, IReadOnlyList<CampaignGoal> goals)
    {
        var list = new List<CampaignPastGoal>();
        var combos = plan.Combos ?? [];
        for (int i = 0; i < combos.Count; i++)
        {
            if (combos[i].Apart || !combos[i].IsHeld()) continue;
            var past = combos[i].Classes.Where(c => goals.Any(g => g.Kind == "class" && g.Key == c && g.Target > 0 && Math.Min(g.AtEnd, ClassNames.MaxPassiveRank) > g.Target)).ToList();
            if (past.Count > 0) list.Add(new CampaignPastGoal(i, past));
        }
        return list.Count == 0 ? null : list;
    }

    /// <summary>How many runs a held combo needs from its classes' rank-earning runs (a Never needs none).</summary>
    private static int Need(CampaignCombo c, IReadOnlyDictionary<string, int> classRanks)
    {
        int earns = c.Classes.Max(k => Math.Max(0, ClassNames.MaxPassiveRank - classRanks.GetValueOrDefault(k)));
        return c.Apart ? 0 : c.Classes.Count == 3 ? Math.Min(c.Times, earns) : c.EveryRun ? earns : Math.Min(1, earns);
    }

    /// <summary>
    /// The one test of "held": which Musts, Nevers and Unlock-now trios <paramref name="v"/> (the plan played out) breaks, and in which run
    /// (null: in none in particular, the combo has too few runs). A played run is history and never breaks one. Prefers are never broken.
    /// </summary>
    public static List<(int Combo, int? Run)> Broken(CampaignPlan plan, CampaignView v)
    {
        var broken = new List<(int, int?)>();
        if (plan.Loadouts.Count == 0) return broken;   // no runs yet (the start page): a combo written first has nothing to break
        var combos = plan.Combos ?? [];
        var future = v.Runs.Where(Future).ToList();
        for (int i = 0; i < combos.Count; i++)
        {
            var c = combos[i];
            if (!c.IsHeld()) continue;
            bool Has(CampaignRun r) => c.Classes.All(r.Classes.Contains);
            if (ComboPlacement.IsHeldRunRule(c))
            {
                var bad = future.FirstOrDefault(r => ComboPlacement.BreaksHeldRunRule(c, r.Classes, k => r.Gains.Any(g => g.Kind == "class" && g.Key == k)));
                if (bad != null) broken.Add((i, bad.N));
            }
            else if (c.Classes.Count == 2)
            {
                if (!v.Runs.Any(Has) && c.Classes.Any(k => v.ClassRanks.GetValueOrDefault(k) < ClassNames.MaxPassiveRank))
                    broken.Add((i, null));
            }
            else
            {
                int played = v.Runs.Count(r => !Future(r) && Has(r));
                int required = Math.Max(0, Math.Min(c.Times - played, c.Classes.Max(k => ClassNames.MaxPassiveRank - v.ClassRanks.GetValueOrDefault(k))));
                var held = future.Where(Has).ToList();
                if (held.Count < required) broken.Add((i, future.FirstOrDefault(r => !Has(r) && c.Classes.Any(r.Classes.Contains))?.N));
                else if (c.UnlockNow)
                {
                    var gap = held.Take(Math.Max(1, required)).Skip(1).Select((r, k) => (r, k)).FirstOrDefault(x => x.r.N != held[x.k].N + 1).r;
                    if (gap != null) broken.Add((i, gap.N));
                }
            }
        }
        return broken;
    }

    /// <summary>The loadouts rebuilt around <paramref name="combos"/>, from the plan's pins and its done runs.</summary>
    private static CampaignPlan Rebuilt(CampaignPlan plan, List<CampaignCombo> combos, IReadOnlyDictionary<string, int> classRanks,
        IReadOnlyDictionary<string, int> raceRanks, IReadOnlyList<ReincarnationEvent> events, Func<IReadOnlyList<string>, double>? quality)
    {
        var p = Copy(plan);
        p.Combos = combos.Count == 0 ? null : [.. combos.Select(c => c.Copy())];
        var (done, off) = DoneCounts(plan, events);
        p.Loadouts = Generate(p, classRanks, raceRanks, done, quality, off);
        return p;
    }

    /// <summary>
    /// The runs a rebuild with the plan's Musts, Nevers and Unlock-now trios gives (the plan's own cheapest path, when they hold);
    /// null when there are none, so today's floor stands. Always at least today's floor: that is what a Must costs.
    /// </summary>
    public static int? ComboFloor(CampaignPlan plan, IReadOnlyDictionary<string, int> classRanks, IReadOnlyDictionary<string, int> raceRanks,
        IReadOnlyList<ReincarnationEvent> events, Func<IReadOnlyList<string>, double>? quality, IReadOnlyList<Relevel>? relevels = null)
    {
        var held = (plan.Combos ?? []).Where(c => c.IsHeld() && !c.Apart).ToList();
        if (held.Count == 0) return null;
        var p = Rebuilt(plan, plan.Combos!, classRanks, raceRanks, events, quality);
        return Evaluate(p, classRanks, raceRanks, events, relevels).Left;
    }

    /// <summary>
    /// What <paramref name="combo"/> does to the plan: a rebuild with it against a rebuild without it, from the plan's pins and done runs,
    /// and what it clashes with. <paramref name="index"/>: the combo being changed, left out of the plan first.
    /// </summary>
    public static CampaignComboPrice ComboPrice(CampaignPlan plan, CampaignCombo combo, IReadOnlyDictionary<string, int> classRanks,
        IReadOnlyDictionary<string, int> raceRanks, IReadOnlyList<ReincarnationEvent> events, Func<IReadOnlyList<string>, double>? quality, int? index = null, IReadOnlyList<Relevel>? relevels = null)
        => ComboPriceOf(plan, combo, classRanks, raceRanks, events, quality, index, true, null, relevels);

    private static CampaignComboPrice ComboPriceOf(CampaignPlan plan, CampaignCombo combo, IReadOnlyDictionary<string, int> classRanks,
        IReadOnlyDictionary<string, int> raceRanks, IReadOnlyList<ReincarnationEvent> events, Func<IReadOnlyList<string>, double>? quality, int? index, bool full, Priced? nowIn = null, IReadOnlyList<Relevel>? relevels = null)
    {
        var all = plan.Combos ?? [];
        var others = all.Select((c, i) => (C: c, I: i)).Where(x => x.I != index).ToList();
        var list = others.Select(x => x.C).ToList();
        var now = nowIn ?? Price(Rebuilt(plan, list, classRanks, raceRanks, events, quality), classRanks, raceRanks, events, quality, relevels);
        var then = PriceWithCombo(plan, list, combo, classRanks, raceRanks, events, quality, relevels);
        var v1 = then.View;
        var v0 = now.View;
        var delta = DeltaOf(now, then, []);

        bool held = combo.IsHeld();
        var fut = v1.Runs.Where(Future).ToList();
        bool Sits(CampaignRun r) => !combo.Apart && combo.Classes.All(r.Classes.Contains);
        bool Earns(CampaignRun r) => r.Gains.Any(g => g.Kind == "class" && combo.Classes.Contains(g.Key));
        var runs = fut.Where(Sits).Select(r => r.N).ToList();
        var (kept, of) = KeptOf(combo, v1);

        var past = fut.Where(r => !Earns(r)).ToList();
        var pastGoals = held && !combo.Apart
            ? combo.Classes.Where(c => v1.Goals.Any(g => g.Kind == "class" && g.Key == c && g.Target > 0 && Math.Min(g.AtEnd, ClassNames.MaxPassiveRank) > g.Target)).ToList() : [];

        var (asMust, asMustXp) = !held && full ? PriceAsMust(plan, list, combo, now, classRanks, raceRanks, events, quality, relevels) : (null, null);
        CampaignComboPrice? other = null;
        if (full && !combo.Apart && combo.Classes.Count == 3)
        {
            var o = combo.Copy(); o.UnlockNow = !combo.UnlockNow; o.Must = combo.Must || o.UnlockNow;
            other = ComboPriceOf(plan, o, classRanks, raceRanks, events, quality, index, false, now, relevels);
        }

        // XP to level and landings that move: runs whose bar differs, Completionist's ranks, and the class goals.
        var xp = DiffRunXp(fut, v0.Runs.Where(Future));
        var lands = Landings(v1, v0);
        bool free = delta.Runs <= 0 && delta.Xp <= 0 && !LandsLater(lands);
        int? same = others.Where(x => x.C.Same(combo)).Select(x => (int?)x.I).FirstOrDefault();
        int? after = combo.UnlockNow ? others.Where(x => x.C.UnlockNow && !x.C.Classes.Any(combo.Classes.Contains)).Select(x => (int?)x.I).LastOrDefault() : null;
        CampaignClash? clash = full ? ClashOf(plan, others, combo, classRanks, events) : null;
        var keptRuns = combo.Apart ? [] : combo.Classes.Count == 2 && combo.EveryRun ? fut.Where(Earns).Where(Sits).Select(r => r.N).ToList() : runs.Take(kept).ToList();
        return new CampaignComboPrice(delta, runs, kept, of, asMust, pastGoals, clash, other, xp, lands,
            combo.Apart ? 0 : past.Count(Sits), combo.Apart ? 0 : past.Count, combo.Classes.All(c => classRanks.GetValueOrDefault(c) >= ClassNames.MaxPassiveRank),
            same, after, others.Count >= CampaignPlan.MaxCombos, free, then.Left, now.Left, keptRuns, asMustXp);
    }

    private static Priced PriceWithCombo(CampaignPlan plan, List<CampaignCombo> list, CampaignCombo combo,
        IReadOnlyDictionary<string, int> classRanks, IReadOnlyDictionary<string, int> raceRanks,
        IReadOnlyList<ReincarnationEvent> events, Func<IReadOnlyList<string>, double>? quality, IReadOnlyList<Relevel>? relevels)
    {
        var rebuilt = Rebuilt(plan, [.. list, combo], classRanks, raceRanks, events, quality);
        return Price(rebuilt, classRanks, raceRanks, events, quality, relevels);
    }

    private static (CampaignDelta? AsMust, List<CampaignRunXp>? AsMustXp) PriceAsMust(CampaignPlan plan, List<CampaignCombo> list,
        CampaignCombo combo, Priced now, IReadOnlyDictionary<string, int> classRanks, IReadOnlyDictionary<string, int> raceRanks,
        IReadOnlyList<ReincarnationEvent> events, Func<IReadOnlyList<string>, double>? quality, IReadOnlyList<Relevel>? relevels)
    {
        var m = combo.Copy();
        m.Must = true;
        var mustPr = PriceWithCombo(plan, list, m, classRanks, raceRanks, events, quality, relevels);
        return (DeltaOf(now, mustPr, []), DiffRunXp(mustPr.View.Runs.Where(Future), now.View.Runs.Where(Future)));
    }

    private static List<CampaignRunXp> DiffRunXp(IEnumerable<CampaignRun> withRuns, IEnumerable<CampaignRun> withoutRuns) =>
        withRuns.Join(withoutRuns, r => r.N, r => r.N, (a, b) => new CampaignRunXp(a.N, a.Xp, b.Xp))
            .Where(x => Math.Abs(x.With - x.Without) > 1e-6).ToList();

    /// <summary>When Completionist's ranks and the class goals land in <paramref name="v1"/> against <paramref name="v0"/>; only what moves.</summary>
    private static List<CampaignLanding> Landings(CampaignView v1, CampaignView v0)
    {
        static int? CompAt(CampaignView v, int rank) => v.Runs.Where(Future).FirstOrDefault(r => r.Gains.Any(g => g.Kind == "completionist" && g.Rank == rank))?.N;
        var lands = new List<CampaignLanding>();
        for (int rank = 1; rank <= ClassNames.MaxPassiveRank; rank++)
            if (CompAt(v1, rank) != CompAt(v0, rank)) lands.Add(new CampaignLanding("completionist:" + rank, CompAt(v1, rank), CompAt(v0, rank)));
        if (v1.ClassesDoneAt != v0.ClassesDoneAt) lands.Add(new CampaignLanding("classes", v1.ClassesDoneAt, v0.ClassesDoneAt));
        return lands;
    }

    private static bool LandsLater(IEnumerable<CampaignLanding> lands) =>
        lands.Any(l => l.With == null ? l.Without != null : l.Without != null && l.With > l.Without);

    /// <summary>How many of the runs a combo asks for the plan <paramref name="v"/> holds it in (played runs count for a trio and an at-least-once pair; runs being played don't count for the rest).</summary>
    private static (int Kept, int Of) KeptOf(CampaignCombo c, CampaignView v)
    {
        bool Sits(CampaignRun r) => c.Classes.All(r.Classes.Contains);
        if (c.Apart) return (0, 0);
        if (c.Classes.Count == 3) return (Math.Min(c.Times, v.Runs.Count(Sits)), c.Times);
        if (c.EveryRun)
        {
            var earn = v.Runs.Where(Future).Where(r => r.Gains.Any(g => g.Kind == "class" && c.Classes.Contains(g.Key))).ToList();
            return (earn.Count(Sits), earn.Count);
        }
        return (Math.Min(1, v.Runs.Count(Sits)), 1);
    }

    /// <summary>
    /// What a held combo can't live beside, found when it's added: a held combo that needs the same class's runs, a Never it would
    /// break, or a pinned loadout with runs to go that seats a class it needs. A Prefer never clashes.
    /// </summary>
    private static CampaignClash? ClashOf(CampaignPlan plan, List<(CampaignCombo C, int I)> others, CampaignCombo combo,
        IReadOnlyDictionary<string, int> classRanks, IReadOnlyList<ReincarnationEvent> events)
    {
        if (!combo.IsHeld()) return null;
        int Cap(string k) => Math.Max(0, ClassNames.MaxPassiveRank - classRanks.GetValueOrDefault(k));
        foreach (var (o, i) in others)
        {
            if (!o.IsHeld() || o.Same(combo)) continue;
            if (o.Apart != combo.Apart)
            {
                var apart = o.Apart ? o : combo;
                var tog = o.Apart ? combo : o;
                if (tog.Classes.Count(apart.Classes.Contains) >= 2) return new CampaignClash(i, null);
                continue;
            }
            if (o.Apart) continue;
            if (o.Classes.All(combo.Classes.Contains) || combo.Classes.All(o.Classes.Contains)) continue;   // the bigger one holds the smaller
            if (combo.Classes.Intersect(o.Classes).FirstOrDefault(k => Need(combo, classRanks) + Need(o, classRanks) > Cap(k)) is { } k2)
                return new CampaignClash(i, null, k2, Cap(k2));
        }
        var (done, _) = DoneCounts(plan, events);
        for (int l = 0; l < plan.Loadouts.Count; l++)
        {
            var pin = plan.Loadouts[l];
            int togo = pin.Times - done[l];
            if (!pin.Pinned || togo <= 0 || combo.Classes.All(pin.Classes.Contains)) continue;
            if (combo.Apart ? combo.Classes.Count(pin.Classes.Contains) >= 2
                : combo.Classes.Intersect(pin.Classes).Any(k => togo + Need(combo, classRanks) > Cap(k))) return new CampaignClash(null, l);
        }
        return null;
    }

    /// <summary>
    /// Every combo's price and where it stands in the plan as it is (hand edits and pins included), and the runs a rebuild with the
    /// Musts gives. Nothing here is stored: a price is worked out each time. Null rows with no combos.
    /// </summary>
    public static (IReadOnlyList<CampaignComboRow>? Rows, int? Floor, bool? Cost, string? Why, string? CostWhat) ComboRows(CampaignPlan plan, CampaignView v, IReadOnlyDictionary<string, int> classRanks,
        IReadOnlyDictionary<string, int> raceRanks, IReadOnlyList<ReincarnationEvent> events, Func<IReadOnlyList<string>, double>? quality, IReadOnlyList<Relevel>? relevels = null)
    {
        var combos = plan.Combos ?? [];
        if (combos.Count == 0) return (null, null, null, null, null);
        var broken = Broken(plan, v);
        var rows = new List<CampaignComboRow>(combos.Count);
        for (int i = 0; i < combos.Count; i++)
        {
            var c = combos[i];
            var price = ComboPriceOf(plan, c, classRanks, raceRanks, events, quality, i, true, null, relevels);
            rows.Add(BuildComboRow(plan, v, c, i, price, broken));
        }
        var (cost, why, costWhat) = SummarizeHeldCombos(plan, combos, classRanks, raceRanks, events, quality, relevels);
        return (rows, ComboFloor(plan, classRanks, raceRanks, events, quality, relevels), cost, why, costWhat);
    }

    private static CampaignComboRow BuildComboRow(CampaignPlan plan, CampaignView v, CampaignCombo c, int index,
        CampaignComboPrice price, List<(int Combo, int? Run)> broken)
    {
        // Played runs with one of a pair's classes and not the other: history, said and never fixed.
        var playedBroke = c.Classes.Count == 2 && c.EveryRun && !c.Apart
            ? v.Runs.Where(r => !Future(r) && c.Classes.Count(r.Classes.Contains) == 1).Select(r => r.N).ToList() : [];
        var b = broken.Where(x => x.Combo == index).Select(x => ((int, int?)?)x).FirstOrDefault();
        int storedKept = KeptOf(c, v).Kept;
        bool handEdited = !c.IsHeld() && !c.Apart && storedKept < price.Kept && v.Runs.Any(Future);
        if (b != null || handEdited)
        {
            int? run = b?.Item2 ?? v.Runs.Where(Future).FirstOrDefault(r => !c.Classes.All(r.Classes.Contains) && c.Classes.Any(r.Classes.Contains))?.N;
            var at = run == null ? null : v.Runs.FirstOrDefault(r => r.N == run);
            int? loadout = at != null && at.Loadout >= 0 && at.Loadout < plan.Loadouts.Count && plan.Loadouts[at.Loadout].Pinned ? at.Loadout : null;
            return new CampaignComboRow(index, price, loadout != null ? "pin" : "hand", run, loadout, playedBroke);
        }
        string state = c.IsHeld() || price.Of == 0 || price.Kept >= price.Of ? "held" : price.Kept == 0 ? "notKept" : "partial";
        return new CampaignComboRow(index, price, state, null, null, playedBroke);
    }

    private static (bool? Cost, string? Why, string? CostWhat) SummarizeHeldCombos(CampaignPlan plan, List<CampaignCombo> combos,
        IReadOnlyDictionary<string, int> classRanks, IReadOnlyDictionary<string, int> raceRanks,
        IReadOnlyList<ReincarnationEvent> events, Func<IReadOnlyList<string>, double>? quality, IReadOnlyList<Relevel>? relevels)
    {
        var held = combos.Where(c => c.IsHeld()).ToList();
        if (held.Count == 0) return (null, null, null);
        var with = Price(Rebuilt(plan, held, classRanks, raceRanks, events, quality), classRanks, raceRanks, events, quality, relevels);
        var none = Price(Rebuilt(plan, [], classRanks, raceRanks, events, quality), classRanks, raceRanks, events, quality, relevels);
        var d = DeltaOf(none, with, []);
        var lands = Landings(with.View, none.View).Where(l => LandsLater([l])).ToList();
        bool cost = d.Runs > 0 || d.Xp > 0 || lands.Count > 0;
        var what = new List<string>();
        if (d.Runs > 0) what.Add("more runs");
        if (d.Xp > 0) what.Add("more XP to level");
        if (lands.Any(l => l.What != "classes")) what.Add("a later Completionist rank");
        if (lands.Any(l => l.What == "classes")) what.Add("your classes done later");
        string? costWhat = cost ? string.Join(" and ", what.Count > 2 ? [string.Join(", ", what.Take(what.Count - 1)), what[^1]] : what) : null;
        string why = string.Join(", ", held.Where(c => !c.Apart).Select(c =>
            string.Join(" / ", Abbrevs.Where(c.Classes.Contains)) + (c.Classes.Count == 3 ? " " + c.Times + (c.Times == 1 ? " time" : " times") : "")));
        return (cost, why, costWhat);
    }
}
