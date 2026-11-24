using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>
/// Engine-computed labels for "What you want first": each ranked row's price line, the race header,
/// and the fill line. A row's price is computed by rebuilding the plan with the alternate timing; nothing is stored.
/// </summary>
public static partial class Campaign
{
    /// <summary>Every ranked row's line, the race header and the Don't care fill line.</summary>
    public static CampaignFirstLane FirstLane(CampaignPlan plan, CampaignView v, IReadOnlyDictionary<string, int> classRanks,
        IReadOnlyDictionary<string, int> raceRanks, IReadOnlyList<ReincarnationEvent> events, Func<IReadOnlyList<string>, double>? quality,
        IReadOnlyList<Relevel>? relevels = null)
    {
        var cur = Price(Rebuilt(plan, plan.Combos ?? [], classRanks, raceRanks, events, quality), classRanks, raceRanks, events, quality, relevels);
        var classes = (plan.ClassOrder ?? []).Where(w => w.Target > 0)
            .Select(w => WithClash(plan, v, "class", w, FirstRow(plan, v, cur, "class", w, classRanks, raceRanks, events, quality, relevels), classRanks, raceRanks, events)).ToList();
        var races = (plan.RaceOrder ?? []).Where(w => w.Target > 0)
            .Select(w => WithClash(plan, v, "race", w, FirstRow(plan, v, cur, "race", w, classRanks, raceRanks, events, quality, relevels), classRanks, raceRanks, events)).ToList();
        return new CampaignFirstLane(classes, races, EveryRace(plan.RaceOrder, plan.Locked, raceRanks));
    }

    /// <summary>A click on a row's timing, priced before it's set: <paramref name="want"/> in place of the row's own, as the row would read, with its clash.</summary>
    public static CampaignFirstRow? FirstPrice(CampaignPlan plan, string kind, CampaignWant want, IReadOnlyDictionary<string, int> classRanks,
        IReadOnlyDictionary<string, int> raceRanks, IReadOnlyList<ReincarnationEvent> events, Func<IReadOnlyList<string>, double>? quality)
    {
        bool isClass = kind == "class";
        var p = Copy(plan);
        var list = isClass ? p.ClassOrder ??= [] : p.RaceOrder ??= [];
        var at = list.FindIndex(w => string.Equals(w.Key, want.Key, StringComparison.OrdinalIgnoreCase));
        var w2 = want.Copy();
        if (at >= 0) list[at] = w2; else list.Add(w2);
        (isClass ? p.Classes : p.Races)[w2.Key] = w2.Target;
        var cur = Price(Rebuilt(p, p.Combos ?? [], classRanks, raceRanks, events, quality), classRanks, raceRanks, events, quality);
        var row = FirstRow(p, cur.View, cur, kind, w2, classRanks, raceRanks, events, quality, null);
        if (!want.UnlockNow) return row;
        var (text, at2) = ClashOf(plan, isClass, want.Key, Evaluate(plan, classRanks, raceRanks, events), events) ?? default;
        return text == null ? row : row with { Clash = text, ClashLoadout = at2 };
    }

    /// <summary>A Spread out row that is not done or locked, with the clash Unlock now would have with the pinned loadouts.</summary>
    private static CampaignFirstRow WithClash(CampaignPlan plan, CampaignView v, string kind, CampaignWant w, CampaignFirstRow row,
        IReadOnlyDictionary<string, int> classRanks, IReadOnlyDictionary<string, int> raceRanks, IReadOnlyList<ReincarnationEvent> events)
    {
        bool isClass = kind == "class";
        if (w.UnlockNow || (isClass ? classRanks : raceRanks).GetValueOrDefault(w.Key) >= w.Target || (!isClass && plan.Locked.Contains(w.Key, StringComparer.OrdinalIgnoreCase))) return row;
        var (text, at) = ClashOf(plan, isClass, w.Key, v, events) ?? default;
        return text == null ? row : row with { Clash = text, ClashLoadout = at };
    }

    /// <summary>A pinned loadout with runs to go that seats <paramref name="key"/>, with a pinned run without it after: its ranks can't come back to back. Null when none.</summary>
    private static (string Text, int Loadout)? ClashOf(CampaignPlan plan, bool isClass, string key, CampaignView view, IReadOnlyList<ReincarnationEvent> events)
    {
        var (done, _) = DoneCounts(plan, events);
        var pins = plan.Loadouts.Select((l, i) => (L: l, I: i)).Where(x => x.L.Pinned && x.L.Times - done[x.I] > 0).ToList();
        bool Seats(CampaignLoadout l) => isClass ? l.Classes.Contains(key, StringComparer.OrdinalIgnoreCase) : string.Equals(l.Race, key, StringComparison.OrdinalIgnoreCase);
        var clash = pins.FirstOrDefault(x => Seats(x.L) && pins.Any(y => y.I > x.I && !Seats(y.L)));
        if (clash.L == null) return null;
        int run = view.Runs.Where(r => Future(r) && r.Loadout == clash.I).Select(r => r.N + view.Before).DefaultIfEmpty(0).Min();
        string seats = isClass ? $"it seats {key}" : $"it plays {key}";
        return ($"Clashes with your pinned loadout {clash.I + 1} ({TrioText(clash.L)}): {seats}{(run > 0 ? $" in run {run}" : "")}.", clash.I);
    }

    private static string RunsWord(IReadOnlyList<int> ns, bool range)
    {
        if (ns.Count == 1) return $"run {ns[0]}";
        if (range && ns.Zip(ns.Skip(1)).All(p => p.Second == p.First + 1)) return $"runs {ns[0]}–{ns[^1]}";
        return "runs " + string.Join(", ", ns.Take(ns.Count - 1)) + " and " + ns[^1];
    }

    /// <summary>The number (as the player sees it) of the next run still to play.</summary>
    private static int NextRun(CampaignView v) => v.Runs.Where(Future).Select(r => r.N + v.Before).DefaultIfEmpty(0).Min();

    private static string NamesWord(IReadOnlyList<string> names) =>
        names.Count < 2 ? string.Concat(names) : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];

    /// <summary>The runs (numbered as the player sees them) still to play that earn <paramref name="key"/> a rank.</summary>
    private static List<int> RanksRuns(CampaignView v, string kind, string key, int target = ClassNames.MaxPassiveRank) =>
        v.Runs.Where(Future).Where(r => r.Gains.Any(g => g.Kind == kind && g.Rank <= target && string.Equals(g.Key, key, StringComparison.OrdinalIgnoreCase)))
            .Select(r => r.N + v.Before).ToList();

    private static string XpPct(double x) => "+" + Math.Round((x - 1) * 100, MidpointRounding.AwayFromZero).ToString("0") + "%";

    /// <summary>What <paramref name="then"/> costs against <paramref name="now"/>, worded; null when it costs nothing (runs, XP to level, or a later landing).</summary>
    private static string? CostWords(Priced then, Priced now, bool would)
    {
        var parts = new List<string>();
        if (then.Left > now.Left) parts.Add($"+{plural(then.Left - now.Left, "run")}, {then.Left} not {now.Left}");
        var xs = then.View.Runs.Where(Future).Join(now.View.Runs.Where(Future), r => r.N, r => r.N, (a, b) => (a.N, W: a.Xp, B: b.Xp))
            .Where(x => x.W > x.B + 1e-6).ToList();
        if (then.Xp > now.Xp + 1e-6 && xs.Count > 0)
            parts.Add(string.Join("; ", xs.Take(3).Select(x => $"run {x.N + then.View.Before} {(would ? "would need" : "needs")} {XpPct(x.W)} XP to level, not {XpPct(x.B)}"))
                + (xs.Count > 3 ? $"; XP to level changes in {xs.Count - 3} more runs" : ""));
        foreach (var l in Landings(then.View, now.View).Where(l => l.With != null && l.Without != null && l.With > l.Without))
        {
            int b = then.View.Before;
            parts.Add(l.What == "classes" ? $"your classes {(would ? "would finish" : "finish")} at run {l.With + b}, not {l.Without + b}"
                : $"Completionist {l.What.Split(':')[1]} {(would ? "would come" : "comes")} at run {l.With + b}, not {l.Without + b}");
        }
        if (parts.Count == 0) return null;
        var text = string.Join("; ", parts);
        return would ? text : char.ToUpper(text[0]) + text[1..];
    }

    private static CampaignFirstRow FirstRow(CampaignPlan plan, CampaignView v, Priced cur, string kind, CampaignWant w,
        IReadOnlyDictionary<string, int> classRanks, IReadOnlyDictionary<string, int> raceRanks, IReadOnlyList<ReincarnationEvent> events,
        Func<IReadOnlyList<string>, double>? quality, IReadOnlyList<Relevel>? relevels)
    {
        bool isClass = kind == "class";
        string key = w.Key;
        int now = (isClass ? classRanks : raceRanks).GetValueOrDefault(key);
        CampaignFirstRow Row(string? line) => new(kind, key, w.Target, w.UnlockNow, line);
        if (now >= w.Target)
        {
            var at = v.Played?.FirstOrDefault(p => p.Gains.Any(g => g.Kind == kind && string.Equals(g.Key, key, StringComparison.OrdinalIgnoreCase) && g.Rank == w.Target))?.N;
            return Row(at is { } n ? $"{w.Target}/3: done at run {n}." : $"{w.Target}/3 already.");
        }
        if (!isClass && plan.Locked.Contains(key, StringComparer.OrdinalIgnoreCase))
            return Row($"{key} is locked, so it's planned last. Unlock it in the game, then tick it off in More options.");
        var lands = RanksRuns(v, kind, key, w.Target);
        if (lands.Count == 0) return Row(null);

        CampaignPlan Flip(bool unlock)
        {
            var p = Copy(plan);
            var f = (isClass ? p.ClassOrder : p.RaceOrder)!.First(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            f.UnlockNow = unlock;
            if (unlock && f.Target < ClassNames.MaxPassiveRank) { f.Target = ClassNames.MaxPassiveRank; (isClass ? p.Classes : p.Races)[key] = f.Target; }
            return p;
        }
        var other = Flip(!w.UnlockNow);
        var otherPrice = Price(Rebuilt(other, other.Combos ?? [], classRanks, raceRanks, events, quality), classRanks, raceRanks, events, quality, relevels);
        var otherRuns = RanksRuns(otherPrice.View, kind, key, w.UnlockNow ? w.Target : ClassNames.MaxPassiveRank);
        if (!w.UnlockNow)
        {
            var cost = CostWords(otherPrice, cur, true);
            if (cost == null) return Row(null);
            bool raised = w.Target < ClassNames.MaxPassiveRank;
            string play = raised ? $"raise {key}'s goal to 3/3 and play {RunsWord(otherRuns, true)}" : $"play it in {RunsWord(otherRuns, true)}";
            return Row($"It lands in {RunsWord(lands, false)}. Unlock now would {play}{Tail(other, otherPrice.View, kind, key, otherRuns, cost == null)?.TrimEnd('.')}: {cost}.");
        }

        var sb = new System.Text.StringBuilder("Unlock now: " + RunsWord(lands, true));
        var tail = Tail(plan, v, kind, key, lands);
        bool belowThree = w.Target < ClassNames.MaxPassiveRank;
        if (tail != null) sb.Append(tail);
        else sb.Append(lands.Count == 1 ? (lands[0] == NextRun(v) ? ", the next one" : "") : ", back to back").Append(belowThree ? $", to {w.Target}/3" : "").Append('.');
        if (belowThree && isClass) sb.Append($" {key}'s active unlocks at 3/3.");
        if (isClass)
            foreach (var m in (plan.Combos ?? []).Where(c => c.Must && !c.Apart && c.Classes.Count == 3 && c.Classes.Contains(key)).Take(1))
                sb.Append($" {key} is in your Must {string.Join(" / ", m.Classes)}, so that trio moves with it.");
        var costU = CostWords(cur, otherPrice, false);
        var costS = CostWords(otherPrice, cur, true);
        if (costU != null) sb.Append(' ').Append(costU).Append('.');
        if (costS != null) sb.Append($" Spread out would play it in {RunsWord(otherRuns, false)}: {costS}.");
        else if (costU != null) sb.Append($" Spread out costs nothing: {RunsWord(otherRuns, false)}.");
        else sb.Append(" Spread out costs the same.");
        return Row(sb.ToString());
    }

    /// <summary>
    /// Where an Unlock now row stands against the Unlock now rows before it: ", with ROG." (same runs), ", after Troll." (the runs before it),
    /// ", after ROG (your combo Never ROG with BER keeps them apart).", or ", with ROG's runs, so it needs no more XP to level." for a race
    /// riding a class's runs. Null for the first.
    /// </summary>
    private static string? Tail(CampaignPlan plan, CampaignView v, string kind, string key, IReadOnlyList<int> mine, bool saysFree = true)
    {
        bool isClass = kind == "class";
        var list = (isClass ? plan.ClassOrder : plan.RaceOrder) ?? [];
        var earlier = list.TakeWhile(x => !string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)).Where(x => x.UnlockNow && x.Target > 0)
            .Select(x => (x.Key, Runs: RanksRuns(v, kind, x.Key, x.Target))).Where(x => x.Runs.Count > 0).ToList();
        if (mine.Count == 0) return null;
        if (isClass)
        {
            var with = earlier.Where(e => e.Runs.Intersect(mine).Any()).Select(e => e.Key).ToList();
            if (with.Count > 0) return $", with {NamesWord(with)}.";
        }
        var before = earlier.Where(e => e.Runs.Max() < mine.Min()).ToList();
        if (before.Count > 0)
        {
            int end = before.Max(e => e.Runs.Max());
            var names = before.Where(e => e.Runs.Max() == end).Select(e => e.Key).ToList();
            if (!isClass) return $", after {names[^1]}.";
            foreach (var c in plan.Combos ?? [])
                if (c.Apart && c.Classes.Contains(key) && c.Classes.Any(names.Contains))
                    return $", after {NamesWord(names)} (your combo Never {string.Join(" with ", c.Classes)} keeps them apart).";
            return $", after {NamesWord(names)}.";
        }
        if (!isClass)
            foreach (var c in (plan.ClassOrder ?? []).Where(x => x.UnlockNow && x.Target > 0))
                if (mine.All(RanksRuns(v, "class", c.Key, c.Target).Contains)) return saysFree ? $", with {c.Key}'s runs, so it needs no more XP to level." : $", with {c.Key}'s runs.";
        return null;
    }
}
