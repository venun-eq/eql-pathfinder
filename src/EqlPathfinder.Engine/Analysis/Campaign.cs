using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>
/// Plans reincarnations toward target passives. Each run is one race and three classes; every passive has 3 ranks;
/// Completionist advances when all classes reach 1, 2, then 3; XP scales with the highest rank in the run
/// (+10% per rank, <see cref="Planner.XpMultiplier"/>). Optimizes fewest runs, lowest XP, and even ranks, using
/// <see cref="Quality"/> only for tie-breaking without guessing trio leveling speed.
/// </summary>
public static partial class Campaign
{
    private static readonly IReadOnlyList<string> Abbrevs = ClassNames.All.Select(c => c.Abbrev).ToList();

    /// <summary>What you can prefer between loadouts that cost the same: Campaign → What you want.</summary>
    public static readonly IReadOnlyList<PlanRole> Preferences =
    [
        new("carry", "A carry", "carry", "MAG, NEC or BST pet, RNG bow, ENC charm"),
        new("heals", "Heals", "heals", "CLR, DRU, SHM or PAL"),
    ];

    public static readonly IReadOnlyList<string> DefaultPrefer = ["carry", "heals"];

    /// <summary>
    /// The tie-breaker between trios that cost the same runs and XP: what you prefer that the trio has, the first worth
    /// 5 and the next 4, and a point off for two pets (only one is up at a time until Kunark's Summoner arte). What a trio
    /// has, never how fast it might level: that isn't knowable for a trio you haven't played.
    /// </summary>
    public static Func<IReadOnlyList<string>, double> Quality(IReadOnlyList<string> prefer) => t =>
    {
        double score = 0;
        for (int i = 0; i < prefer.Count; i++)
        {
            bool has = prefer[i] switch
            {
                "carry" => t.Any(c => ClassNames.Carries.ContainsKey(c)),
                "heals" => t.Any(ClassNames.IsHealer),
                _ => false,
            };
            if (has) score += PlanPrefs.Weight(i);
        }
        if (t.Count(c => c is "MAG" or "NEC" or "BST") >= 2) score -= 1;
        return score;
    };

    /// <summary>
    /// Tie-breaker between seatings of equal run count and XP, in priority order: SHM/ENC separation
    /// (when <see cref="CampaignRoles.OneStrongSlow"/>), survival check (when <paramref name="check"/>), haste beside melee, then carries.
    /// their order: a carry in each trio, the best spread out, none paired. Never how fast a trio levels.
    /// </summary>
    public static Func<IReadOnlyList<string>, double> RolesQuality(CampaignRoles roles, bool check)
    {
        var carries = roles.Carries.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => PlanPrefs.Weight(x.i));
        return t =>
        {
            double score = 0;
            if (roles.OneStrongSlow && t.Contains("SHM") && t.Contains("ENC")) score -= 1000;
            if (check && ClassRoles.HoldsUp(t)) score += 100;
            int givers = t.Count(ClassRoles.HasteGivers.Contains);
            if (givers > 0)
            {
                bool weapons = t.Any(ClassRoles.HitWithWeapons.Contains);
                score += weapons ? 20 : -20;
                // The earlier a class gets its haste, the better (ENC 16, BRD 10 at a weaker rate, SHM 29).
                if (weapons) score += t.Contains("ENC") ? 3 : t.Contains("BRD") ? 1 : 2;
                if (givers > 1) score -= 5;
            }
            var mine = t.Where(carries.ContainsKey).Select(c => carries[c]).OrderByDescending(w => w).ToList();
            if (mine.Count > 0) score += mine[0] - 2 * mine.Skip(1).Sum();
            return score;
        };
    }

    /// <summary>What a cache of the tie-break is keyed on: "" for the old planner (<paramref name="roles"/> null), else the carries in order, the toggle and whether trios are checked.</summary>
    public static string TieKey(CampaignRoles? roles, SurvivalCheck check) =>
        roles == null ? "" : "|new|" + string.Join(",", roles.Carries) + "|" + roles.OneStrongSlow + "|" + check.State;

    public static string? RaceName(string? s) =>
        s == null ? null : ClassNames.Races.FirstOrDefault(r => r.Equals(s.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>"class:mag" → "class:MAG", "race:iksar" → "race:Iksar"; null when it names nothing real.</summary>
    public static string? GoalKey(string? k)
    {
        if (k == null) return null;
        if (k.StartsWith("class:", StringComparison.OrdinalIgnoreCase)) return ClassNames.Info(k[6..]) is { } c ? "class:" + c.Abbrev : null;
        if (k.StartsWith("race:", StringComparison.OrdinalIgnoreCase)) return RaceName(k[5..]) is { } r ? "race:" + r : null;
        return null;
    }

    /// <summary>Ready-made goals: "all" (every class and race 3/3) and "classes" (every class 3/3).</summary>
    public static CampaignPlan Preset(string key)
    {
        var p = new CampaignPlan();
        if (key is "all" or "classes") foreach (var c in Abbrevs) p.Classes[c] = ClassNames.MaxPassiveRank;
        if (key is "all") foreach (var r in ClassNames.Races) p.Races[r] = ClassNames.MaxPassiveRank;
        return p;
    }

    /// <summary>
    /// Your reincarnations from the roster's own times: each race the log saw, with the three classes it saw within
    /// 5 minutes of it (the game grants all four together).
    /// </summary>
    public static List<ReincarnationEvent> Events(Roster roster)
    {
        var window = TimeSpan.FromMinutes(5);
        var classTimes = roster.Classes
            .SelectMany(kv => kv.Value.Seen.Select(t => (Class: ClassNames.Abbrev(kv.Key), At: t))).ToList();
        var used = new HashSet<(string, DateTime)>();
        var events = new List<ReincarnationEvent>();
        foreach (var (name, inc) in roster.Races)
        {
            if (RaceName(name) is not { } race) continue;
            foreach (var t in inc.Seen.Order())
            {
                var near = classTimes.Where(x => !used.Contains(x) && (x.At - t).Duration() <= window)
                    .OrderBy(x => (x.At - t).Duration()).DistinctBy(x => x.Class).Take(3).ToList();
                if (near.Count < 3) continue;
                foreach (var x in near) used.Add(x);
                events.Add(new ReincarnationEvent(t, race, near.Select(x => x.Class).ToList()));
            }
        }
        return events.OrderBy(e => e.At).ToList();
    }

    /// <summary>
    /// How many of each loadout's runs are done: reincarnations after <see cref="CampaignPlan.Since"/> with its race and
    /// trio (the first loadout with room takes each), plus the runs you ticked off. The rest are off the plan. One at
    /// Since itself was already in your ranks when the plan was built from them.
    /// </summary>
    public static (int[] Done, List<CampaignOffPlan> Off) DoneCounts(CampaignPlan plan, IReadOnlyList<ReincarnationEvent> events)
    {
        var (done, off, _) = Matched(plan, events);
        return (done, off);
    }

    /// <summary>DoneCounts, and when each loadout's matched reincarnations happened (the ticked-off runs have no time).</summary>
    private static (int[] Done, List<CampaignOffPlan> Off, List<DateTime>[] Times) Matched(CampaignPlan plan, IReadOnlyList<ReincarnationEvent> events)
    {
        var ls = plan.Loadouts;
        var matched = new int[ls.Count];
        var times = ls.Select(_ => new List<DateTime>()).ToArray();
        var off = new List<CampaignOffPlan>();
        if (plan.Since is { } since)
            foreach (var e in events.Where(e => e.At > since))
            {
                int i = -1;
                for (int k = 0; k < ls.Count && i < 0; k++)
                    if (ls[k].Race == e.Race && ClassNames.TrioKey(ls[k].Classes) == e.TrioKey && matched[k] + ls[k].Marked < ls[k].Times) i = k;
                if (i >= 0) { matched[i]++; times[i].Add(e.At); }
                else off.Add(new CampaignOffPlan(e.At, e.Race, e.Classes));
            }
        return (ls.Select((l, i) => Math.Min(l.Times, matched[i] + l.Marked)).ToArray(), off, times);
    }

    /// <summary>
    /// The order you'd play the runs in: (loadout, which of its runs), from the first loadout down. The loadouts are built
    /// in the cheapest order, classes first, so they're played as they stand.
    /// </summary>
    public static List<(int Loadout, int Pass)> Sequence(IReadOnlyList<CampaignLoadout> ls)
    {
        var seq = new List<(int, int)>();
        for (int i = 0; i < ls.Count; i++)
            for (int p = 0; p < ls[i].Times; p++) seq.Add((i, p));
        return seq;
    }

    /// <summary>
    /// The fewest runs that can reach the class goals: a run raises three different classes one rank each, so it's the
    /// larger of what one class still needs and a third of what they all need (rounded up). Taking, run by run, the three
    /// classes that need the most always gets there in that many runs.
    /// </summary>
    public static int FewestClassRuns(IEnumerable<int> needs)
    {
        int max = 0, sum = 0;
        foreach (var n in needs) { max = Math.Max(max, n); sum += n; }
        return Math.Max(max, (sum + 2) / 3);
    }

    /// <summary>The page's round for a run: the lowest class rank its gains raise (1 to 3), or 0 when it raises no class rank to 3 or under.</summary>
    private static int RoundOf(IReadOnlyList<CampaignGain> gains) =>
        gains.Where(g => g.Kind == "class").Select(g => g.Rank).DefaultIfEmpty(0).Min();

    private readonly record struct QueuedRun(int LoadoutIndex, DateTime? At, bool Done, Relevel? Open, ReincarnationEvent? Event)
    {
        public (string Race, IReadOnlyList<string> Classes) Resolve(IReadOnlyList<CampaignLoadout> loadouts)
        {
            var l = LoadoutIndex >= 0 ? loadouts[LoadoutIndex] : null;
            string race = l?.Race ?? Open?.Race ?? Event!.Race;
            IReadOnlyList<string> classes = Open != null ? Open.Trio.Split('/', StringSplitOptions.TrimEntries) : l?.Classes ?? Event!.Classes;
            return (race, classes);
        }
    }

    /// <summary>
    /// The plan played out from your ranks now: which runs are done, and for the rest, the rank each relevels at, its XP to
    /// level and the passive ranks it earns.
    /// </summary>
    public static CampaignView Evaluate(CampaignPlan plan, IReadOnlyDictionary<string, int> classRanks, IReadOnlyDictionary<string, int> raceRanks,
        IReadOnlyList<ReincarnationEvent> events, IReadOnlyList<Relevel>? relevels = null, IReadOnlyList<CampaignPreset>? presets = null)
    {
        var ls = plan.Loadouts;
        var (done, off, times) = Matched(plan, events);
        var pc = Abbrevs.ToDictionary(c => c, c => classRanks.GetValueOrDefault(c));
        var pr = ClassNames.Races.ToDictionary(r => r, r => raceRanks.GetValueOrDefault(r));
        int Completionist() => Math.Min(ClassNames.MaxPassiveRank, pc.Values.Min());
        int compNow = Completionist();

        var seq = BuildRunSequence(plan, done, times, events, relevels, out var openEventTimes);
        int reincarnated = seq.Count(x => x.Done);

        var runs = new List<CampaignRun>(seq.Count);
        double lastXp = 1, totalXp = 0;
        bool next = false;
        bool ClassesMet() => plan.Classes.All(kv => pc.GetValueOrDefault(kv.Key) >= kv.Value);
        int? classesAt = ClassesMet() ? 0 : null;
        for (int n = 0; n < seq.Count; n++)
        {
            if (seq[n].Done)
            {
                runs.Add(EvaluateCompletedRun(ls, seq, n, pc, pr));
                continue;
            }
            var run = EvaluatePlannedRun(ls[seq[n].LoadoutIndex], n + 1, seq[n].LoadoutIndex, next, pc, pr, out double xp);
            lastXp = xp;
            totalXp += xp;
            runs.Add(run);
            next = true;
            if (classesAt == null && ClassesMet()) classesAt = n + 1;
        }

        var locked = plan.Locked.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = ls.Select((l, i) =>
        {
            var w = new List<string>();
            if (locked.Contains(l.Race) && done[i] < l.Times) w.Add($"{l.Race} isn't unlocked yet: max faction with its three city factions first.");
            return new CampaignLoadoutRow(i, done[i], l.Times - done[i], w);
        }).ToList();

        var goals = BuildGoals(plan, classRanks, raceRanks, pc, pr);
        var (played, unseen) = History(plan, classRanks, raceRanks, events, off, openEventTimes, relevels);
        return new CampaignView(plan, rows, runs, reincarnated, runs.Count(r => r.State is "next" or "planned"),
            Math.Round(totalXp, 2), lastXp, goals, compNow, Completionist(), off, presets ?? [],
            Abbrevs.ToDictionary(c => c, c => classRanks.GetValueOrDefault(c)), ClassNames.Races.ToDictionary(r => r, r => raceRanks.GetValueOrDefault(r)),
            Floor(plan, classRanks, raceRanks), classesAt, Played: played, Unseen: unseen, Leveling: runs.Count(r => r.State == "leveling"))
        {
            PastGoals = PastGoalsOf(plan, goals),
        };
    }

    private static List<QueuedRun> BuildRunSequence(CampaignPlan plan, int[] done, List<DateTime>[] times,
        IReadOnlyList<ReincarnationEvent> events, IReadOnlyList<Relevel>? relevels, out HashSet<DateTime> openEventTimes)
    {
        var ls = plan.Loadouts;
        var byEvent = new Dictionary<DateTime, Relevel>();
        foreach (var r in relevels ?? []) if (r.EventAt is { } at) byEvent.TryAdd(at, r);
        var planAts = times.SelectMany(t => t).ToHashSet();
        var seq = new List<QueuedRun>();
        // An off-plan open relevel from after the plan began is numbered by time with the matched runs, not ahead of them.
        var late = new List<QueuedRun>();
        foreach (var e in events.Where(e => !planAts.Contains(e.At)))
            if (byEvent.GetValueOrDefault(e.At) is { Done: false } rel)
            {
                if (plan.Since is { } since && e.At > since) late.Add(new(-1, e.At, true, rel, e));
                else seq.Add(new(-1, e.At, true, rel, e));
            }
        openEventTimes = seq.Concat(late).Select(x => x.At!.Value).ToHashSet();
        for (int i = 0; i < ls.Count; i++)
            for (int m = 0; m < Math.Min(ls[i].Marked, done[i]); m++) seq.Add(new(i, null, true, null, null));
        seq.AddRange(times.SelectMany((t, i) => t.Select(at => new QueuedRun(i, at, true, byEvent.GetValueOrDefault(at) is { Done: false } r ? r : null, null)))
            .Concat(late).OrderBy(x => x.At).ThenBy(x => x.LoadoutIndex));
        for (int i = 0; i < ls.Count; i++)
            for (int p = done[i]; p < ls[i].Times; p++) seq.Add(new(i, null, false, null, null));
        return seq;
    }

    private static CampaignRun EvaluateCompletedRun(IReadOnlyList<CampaignLoadout> ls, List<QueuedRun> seq, int n,
        IReadOnlyDictionary<string, int> pc, IReadOnlyDictionary<string, int> pr)
    {
        var item = seq[n];
        var (race, classes) = item.Resolve(ls);
        int Then(string name, int now) => now - Enumerable.Range(n + 1, seq.Count - n - 1).Count(m =>
        {
            if (!seq[m].Done) return false;
            var (laterRace, laterClasses) = seq[m].Resolve(ls);
            return laterRace == name || laterClasses.Contains(name);
        });
        var gains = new List<CampaignGain>();
        var past = new List<string>();
        foreach (var c in classes)
        {
            int have = Then(c, pc.GetValueOrDefault(c));
            if (have is > 0 and <= ClassNames.MaxPassiveRank) gains.Add(new("class", c, have));
            else if (have > ClassNames.MaxPassiveRank) past.Add(c);
        }
        int raceNow = Then(race, pr.GetValueOrDefault(race));
        if (raceNow is > 0 and <= ClassNames.MaxPassiveRank) gains.Add(new("race", race, raceNow));
        else if (raceNow > ClassNames.MaxPassiveRank) past.Add(race);
        int rank = Math.Max(classes.Max(c => Then(c, pc.GetValueOrDefault(c))), raceNow);
        if (item.Open == null)
            return new CampaignRun(n + 1, item.LoadoutIndex, race, classes, "done", rank, 0, gains, past, Round: RoundOf(gains), At: item.At);
        return new CampaignRun(n + 1, item.LoadoutIndex, race, classes, "leveling", rank, rank > 0 ? Planner.XpMultiplier(rank) : 0, gains, past,
            item.Open.Level, item.Open.Journey?.EtaHours, item.Open.Journey?.Crawls ?? 0, RoundOf(gains), item.At, item.Open.Journey?.EtaTo);
    }

    private static CampaignRun EvaluatePlannedRun(CampaignLoadout l, int number, int loadoutIndex, bool afterFirst,
        Dictionary<string, int> pc, Dictionary<string, int> pr, out double xp)
    {
        var gains = new List<CampaignGain>();
        var past = new List<string>();
        int compBefore = Math.Min(ClassNames.MaxPassiveRank, pc.Values.Min());
        foreach (var c in l.Classes)
        {
            pc[c]++;
            if (pc[c] <= ClassNames.MaxPassiveRank) gains.Add(new("class", c, pc[c])); else past.Add(c);
        }
        pr[l.Race]++;
        if (pr[l.Race] <= ClassNames.MaxPassiveRank) gains.Add(new("race", l.Race, pr[l.Race]));
        else past.Add(l.Race);
        int compAfter = Math.Min(ClassNames.MaxPassiveRank, pc.Values.Min());
        if (compAfter > compBefore) gains.Add(new("completionist", "", compAfter));
        int rank = Math.Max(l.Classes.Max(c => pc[c]), pr[l.Race]);
        xp = Planner.XpMultiplier(rank);
        return new CampaignRun(number, loadoutIndex, l.Race, l.Classes, afterFirst ? "planned" : "next", rank, xp, gains, past, Round: RoundOf(gains));
    }

    private static List<CampaignGoal> BuildGoals(CampaignPlan plan, IReadOnlyDictionary<string, int> classRanks,
        IReadOnlyDictionary<string, int> raceRanks, IReadOnlyDictionary<string, int> pc, IReadOnlyDictionary<string, int> pr)
    {
        var goals = new List<CampaignGoal>();
        foreach (var c in Abbrevs)
        {
            int t = plan.Classes.GetValueOrDefault(c);
            if (t > 0) goals.Add(new("class", c, t, classRanks.GetValueOrDefault(c), pc[c]));
        }
        foreach (var r in ClassNames.Races)
        {
            int t = plan.Races.GetValueOrDefault(r);
            if (t > 0) goals.Add(new("race", r, t, raceRanks.GetValueOrDefault(r), pr[r]));
        }
        return goals;
    }

    /// <summary>
    /// The runs you played that the plan's loadouts don't hold: the log's reincarnations up to the plan's start (all of
    /// them before there's a plan) and the ones since that no loadout had room for, in order, each with what it earned;
    /// and how many runs your ranks count that the log never saw whole (a race rank is one reincarnation: counts you
    /// entered on Roster, or play from before the log). They're history, so the plan only numbers its runs after them.
    /// </summary>
    public static (List<CampaignPlayed> Played, int Unseen) History(CampaignPlan plan, IReadOnlyDictionary<string, int> classRanks,
        IReadOnlyDictionary<string, int> raceRanks, IReadOnlyList<ReincarnationEvent> events, IReadOnlyList<CampaignOffPlan> off, IReadOnlySet<DateTime>? leveling = null, IReadOnlyList<Relevel>? relevels = null)
    {
        // Your ranks before any reincarnation the log saw, then each one played forward to say what it earned.
        var pc = Abbrevs.ToDictionary(c => c, c => classRanks.GetValueOrDefault(c));
        var pr = ClassNames.Races.ToDictionary(r => r, r => raceRanks.GetValueOrDefault(r));
        var (shownAs, order) = PlayedAs(events, relevels);
        foreach (var (race, classes) in shownAs)
        {
            foreach (var c in classes) if (pc.ContainsKey(c)) pc[c] = Math.Max(0, pc[c] - 1);
            if (pr.ContainsKey(race)) pr[race] = Math.Max(0, pr[race] - 1);
        }
        int Comp() => Math.Min(ClassNames.MaxPassiveRank, pc.Values.Min());
        var offAt = off.Select(o => o.At).ToHashSet();
        int unseen = Math.Max(0, raceRanks.Values.Sum() - events.Count - plan.Loadouts.Sum(l => l.Marked));
        var played = new List<CampaignPlayed>();
        foreach (int k in order)
        {
            var e = events[k];
            var (race, classes) = shownAs[k];
            var gains = new List<CampaignGain>();
            int comp = Comp();
            foreach (var c in classes.Where(pc.ContainsKey))
                if (++pc[c] <= ClassNames.MaxPassiveRank) gains.Add(new("class", c, pc[c]));
            if (pr.ContainsKey(race) && ++pr[race] <= ClassNames.MaxPassiveRank) gains.Add(new("race", race, pr[race]));
            if (Comp() > comp) gains.Add(new("completionist", "", Comp()));
            bool before = plan.Since is not { } since || e.At <= since, isOff = !before && offAt.Contains(e.At);
            if ((before || isOff) && leveling?.Contains(e.At) != true) played.Add(new CampaignPlayed(unseen + played.Count + 1, e.At, race, classes, gains, isOff));
        }
        return (played, unseen);
    }

    /// <summary>
    /// The race and classes each reincarnation is shown with, in the events' order: the trio and race you played after it
    /// (its relevel, from /who), since the log's AA lines only say which classes were reset together. Kept to the log's grouping
    /// for a reincarnation with no played relevel, and for all of a burst when its played trios (or races) don't hold the same
    /// classes (races) as the log's groups, so the ranks over all runs stay the roster's.
    /// </summary>
    private static (List<(string Race, IReadOnlyList<string> Classes)> Shown, List<int> Order) PlayedAs(IReadOnlyList<ReincarnationEvent> events, IReadOnlyList<Relevel>? relevels)
    {
        var byEvent = new Dictionary<DateTime, Relevel>();
        foreach (var r in relevels ?? []) if (r.Journey != null && r.EventAt is { } at) byEvent.TryAdd(at, r);
        var shown = events.Select(e => (e.Race, Classes: e.Classes)).ToList();
        var with = new List<(int K, string Race, IReadOnlyList<string> Trio)>();
        for (int k = 0; k < events.Count; k++)
        {
            if (!byEvent.TryGetValue(events[k].At, out var rel)) continue;
            var trio = rel.Trio.Split('/', StringSplitOptions.TrimEntries);
            if (trio.Length == 3 && trio.Distinct().Count() == 3 && trio.All(Abbrevs.Contains)) with.Add((k, rel.Race ?? events[k].Race, trio));
        }
        static string Bag(IEnumerable<string> xs) => string.Join(",", xs.Order(StringComparer.Ordinal));
        var playedTrio = new HashSet<int>();
        // Each burst is checked alone: one that doesn't add up keeps the log's grouping, the others show what was played.
        foreach (var (from, to) in Bursts(events))
        {
            var w = with.Where(x => x.K >= from && x.K < to).ToList();
            if (Bag(w.SelectMany(x => x.Trio)) == Bag(w.SelectMany(x => events[x.K].Classes)))
                foreach (var x in w) { shown[x.K] = (shown[x.K].Race, x.Trio); playedTrio.Add(x.K); }
            if (Bag(w.Select(x => x.Race)) == Bag(w.Select(x => events[x.K].Race)))
                foreach (var x in w) shown[x.K] = (x.Race, shown[x.K].Classes);
        }
        return (shown, PlayOrder(events, with.Where(w => playedTrio.Contains(w.K)).ToDictionary(w => w.K, w => byEvent[events[w.K].At].Journey!.Start)));
    }

    /// <summary>The events in bursts: each reset within <see cref="Relevels.PairWindow"/> of the one before (a lone reset is a burst of one), as [from, to) indexes.</summary>
    private static List<(int From, int To)> Bursts(IReadOnlyList<ReincarnationEvent> events)
    {
        var bursts = new List<(int, int)>();
        for (int i = 0; i < events.Count;)
        {
            int j = i + 1;
            while (j < events.Count && events[j].At - events[j - 1].At <= Relevels.PairWindow) j++;
            bursts.Add((i, j));
            i = j;
        }
        return bursts;
    }

    /// <summary>
    /// The order the played-before rows are numbered in: the events as the log has them, except that inside a burst (resets
    /// within the pairing window of the one before, with no play between) the rows follow when each shown trio's relevel started.
    /// A reset with no played trio sorts by its reset time, so it comes first in its burst.
    /// </summary>
    private static List<int> PlayOrder(IReadOnlyList<ReincarnationEvent> events, IReadOnlyDictionary<int, DateTime> started)
    {
        var order = new List<int>();
        foreach (var (from, to) in Bursts(events))
            order.AddRange(Enumerable.Range(from, to - from).OrderBy(k => started.GetValueOrDefault(k, events[k].At)));
        return order;
    }

    /// <summary>The fewest runs that can reach the plan's goals from these ranks: one race rank a run, three class ranks a run.</summary>
    public static CampaignFloor Floor(CampaignPlan plan, IReadOnlyDictionary<string, int> classRanks, IReadOnlyDictionary<string, int> raceRanks)
    {
        var need = Abbrevs.Select(c => (C: c, N: Math.Max(0, plan.Classes.GetValueOrDefault(c) - classRanks.GetValueOrDefault(c)))).ToList();
        int races = ClassNames.Races.Sum(r => Math.Max(0, plan.Races.GetValueOrDefault(r) - raceRanks.GetValueOrDefault(r)));
        int sum = need.Sum(x => x.N), max = need.Max(x => x.N), third = (sum + 2) / 3, classRuns = Math.Max(max, third);
        int runs = Math.Max(races, classRuns);
        string why = runs == 0 ? ""
            : races >= classRuns ? $"{races} race {(races == 1 ? "rank" : "ranks")} to earn, one a run"
            : third >= max ? (sum == 1 ? "1 class rank to earn" : $"{sum} class ranks to earn, three a run")
            : $"{need.First(x => x.N == max).C} needs {max} {(max == 1 ? "rank" : "ranks")}, one a run";
        return new CampaignFloor(runs, classRuns, why);
    }

    /// <summary>All 16 races in the order Everything 3/3 writes: the ranked ones as they stand, then the fewest ranks now, then the game's order, locked last.</summary>
    public static List<string> EveryRace(IEnumerable<CampaignWant>? ranked, IEnumerable<string> locked, IReadOnlyDictionary<string, int> raceRanks)
    {
        var game = ClassNames.Races.ToList();
        var lock_ = locked.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var first = (ranked ?? []).Where(w => w.Target > 0).Select(w => w.Key).Distinct().ToList();
        return [.. first, .. game.Where(r => !first.Contains(r)).OrderBy(r => lock_.Contains(r)).ThenBy(r => raceRanks.GetValueOrDefault(r)).ThenBy(game.IndexOf)];
    }

    /// <summary>
    /// What "Everything 3/3" and "Every class 3/3" take from your ranks now. On a plan with runs (<paramref name="ranked"/> or <paramref name="current"/>)
    /// the count is the click's: its pinned runs and the runs you played stay, as a rebuild keeps them.
    /// </summary>
    public static List<CampaignPreset> Presets(IReadOnlyDictionary<string, int> classRanks, IReadOnlyDictionary<string, int> raceRanks,
        Func<IReadOnlyList<string>, double>? quality, CampaignPlan? ranked = null, IReadOnlyList<ReincarnationEvent>? events = null,
        IReadOnlyList<Relevel>? relevels = null, CampaignPlan? current = null)
    {
        var list = new List<CampaignPreset>();
        var kept = current ?? ranked;
        events ??= [];
        foreach (var (key, name) in new[] { ("all", "Everything 3/3"), ("classes", "Every class 3/3") })
        {
            // The click rebuilds the plan you have with these goals: its pins, played runs, combos, timings and preferences stay.
            var goals = Preset(key);
            var p = kept != null ? Copy(kept) : goals;
            p.Classes = goals.Classes;
            p.Races = goals.Races;
            p.First = [.. p.First.Where(g => !g.StartsWith("race:", StringComparison.OrdinalIgnoreCase) || key == "all")];
            if (ranked != null)
            {
                // The new planner: a click leaves your ranked classes (and their Unlock now) as they are. Everything 3/3 keeps your ranked races, at 3/3, and ranks no other; Every class 3/3 ranks none.
                p.Locked = [.. ranked.Locked];
                p.ClassOrder = [.. (ranked.ClassOrder ?? []).Select(w => w.Copy())];
                p.RaceOrder = key == "all" ? [.. (ranked.RaceOrder ?? []).Select(w => { var c = w.Copy(); c.Target = ClassNames.MaxPassiveRank; return c; })] : [];
            }
            if (kept is { Loadouts.Count: > 0 })
            {
                var (done, off) = DoneCounts(p, events);
                p.Loadouts = Generate(p, classRanks, raceRanks, done, quality, off);
            }
            else p.Loadouts = Generate(p, classRanks, raceRanks, null, quality);
            var v = Evaluate(p, classRanks, raceRanks, events, relevels);
            list.Add(new CampaignPreset(key, name, v.Left, v.TotalXp, v.LastXp));
        }
        return list;
    }

    private static string plural(int n, string w) => $"{n} {w}{(n == 1 ? "" : "s")}";
}
