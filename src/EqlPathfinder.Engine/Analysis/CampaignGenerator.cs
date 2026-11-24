using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Analysis;

public static partial class Campaign
{
    /// <summary>
    /// Generates the cheapest loadouts to reach plan goals (fewest runs, lowest XP, classes before races for Completionist;
    /// preferences only break ties). Pinned loadouts stay intact and completed runs (<paramref name="done"/>) are preserved;
    /// new loadouts follow them:
    /// <list type="number">
    /// <item>Classes first, one run at a time: the three classes furthest from their goal, so every class reaches 1, then
    /// 2, then 3 (Completionist as soon as it can come) in the fewest runs. Classes you want first ride along from the
    /// start whenever that costs no run. Each run takes a race still short of its goal, so it earns a race rank too.</item>
    /// <item>Then each race still short, in one loadout, with the three lowest-ranked classes: the least XP to level.</item>
    /// <item>Between equal classes, the lower rank (less XP), then <paramref name="quality"/> (what you prefer).</item>
    /// </list>
    /// </summary>
    public static List<CampaignLoadout> Generate(CampaignPlan plan, IReadOnlyDictionary<string, int> classRanks,
        IReadOnlyDictionary<string, int> raceRanks, IReadOnlyList<int>? done = null, Func<IReadOnlyList<string>, double>? quality = null,
        IReadOnlyList<CampaignOffPlan>? off = null)
    {
        var builder = new PlanBuilder(plan, classRanks, raceRanks, done, quality, off);
        return builder.Build();
    }

    private sealed class PlanBuilder
    {
        private readonly CampaignPlan _plan;
        private readonly IReadOnlyDictionary<string, int> _classRanks;
        private readonly IReadOnlyDictionary<string, int> _raceRanks;
        private readonly Func<IReadOnlyList<string>, double>? _quality;
        private readonly Dictionary<string, double> _qualityCache = new();
        private readonly Dictionary<string, int> _pc;
        private readonly Dictionary<string, int> _pr;
        private readonly Dictionary<string, int> _dc;
        private readonly Dictionary<string, int> _dr;
        private readonly HashSet<string> _locked;
        private readonly bool _ranked;
        private readonly List<CampaignWant> _rankedClasses;
        private readonly List<CampaignWant> _rankedRaces;
        private readonly List<CampaignLoadout> _result = [];
        private readonly List<int> _skip = [];
        private readonly int _kept;
        private readonly ComboPlacement _combos;

        public PlanBuilder(CampaignPlan plan, IReadOnlyDictionary<string, int> classRanks, IReadOnlyDictionary<string, int> raceRanks,
            IReadOnlyList<int>? done, Func<IReadOnlyList<string>, double>? quality, IReadOnlyList<CampaignOffPlan>? off)
        {
            _plan = plan;
            _classRanks = classRanks;
            _raceRanks = raceRanks;
            _quality = quality;
            _pc = Abbrevs.ToDictionary(c => c, c => classRanks.GetValueOrDefault(c));
            _pr = ClassNames.Races.ToDictionary(r => r, r => raceRanks.GetValueOrDefault(r));
            SeedKeptLoadouts(done);
            _kept = _result.Count;
            _dc = Abbrevs.ToDictionary(c => c, c => Math.Max(0, plan.Classes.GetValueOrDefault(c) - _pc[c]));
            _dr = ClassNames.Races.ToDictionary(r => r, r => Math.Max(0, plan.Races.GetValueOrDefault(r) - _pr[r]));
            _locked = plan.Locked.ToHashSet(StringComparer.OrdinalIgnoreCase);
            _ranked = plan.Ranked;
            _rankedClasses = (plan.ClassOrder ?? []).Where(w => _dc.ContainsKey(w.Key)).ToList();
            _rankedRaces = (plan.RaceOrder ?? []).Where(w => _dr.ContainsKey(w.Key)).ToList();
            _combos = ComboPlacement.From(plan.Combos ?? [], _ranked, _rankedClasses, _pc, _dc, _result, off);
        }

        public List<CampaignLoadout> Build()
        {
            BuildClassRuns();
            BuildRaceRuns();
            var skip = _result.Select((_, i) => i < _skip.Count ? _skip[i] : 0).ToArray();
            Improve(_result, _kept, skip, _plan, _classRanks, _raceRanks, QualityOf, Prio);
            return _result;
        }

        private void SeedKeptLoadouts(IReadOnlyList<int>? done)
        {
            for (int i = 0; i < _plan.Loadouts.Count; i++)
            {
                var l = _plan.Loadouts[i];
                int d = Math.Min(l.Times, done != null && i < done.Count ? done[i] : 0);
                if (l.Pinned)
                {
                    _result.Add(l.Copy());
                    _skip.Add(d);
                    foreach (var c in l.Classes) _pc[c] += l.Times - d;
                    _pr[l.Race] += l.Times - d;
                }
                else if (d > 0)
                {
                    var c = l.Copy();
                    c.Times = d;
                    c.Marked = Math.Min(c.Marked, d);
                    _result.Add(c);
                    _skip.Add(d);
                }
            }
        }

        private void BuildClassRuns()
        {
            int step = 0;
            while ((_dc.Values.Any(v => v > 0) || _combos.HasPendingSlots) && _result.Count < CampaignPlan.MaxLoadouts)
            {
                bool hasGoals = _dc.Values.Any(v => v > 0);
                var due = _combos.TakeDueSlot(step++, hasGoals);
                if (due == null)
                {
                    var t = NextClassRun();
                    AddRuns(RaceForClassRun(t), t, 1);
                    continue;
                }
                var forced = due.Goal
                    ? due.Classes.Where(c => _dc[c] > 0).ToList()
                    : due.Classes.Where(c => _pc[c] < ClassNames.MaxPassiveRank).ToList();
                if (forced.Count > 0)
                {
                    var t = Greedy(due.Goal ? forced : [.. due.Classes]);
                    AddRuns(RaceForClassRun(t), t, 1);
                }
            }
        }

        private void BuildRaceRuns()
        {
            var raceOrder = (_ranked
                ? ClassNames.Races.Where(r => _dr[r] > 0)
                    .OrderBy(r => _locked.Contains(r))
                    .ThenBy(r => _rankedRaces.Any(w => w.Key == r && w.UnlockNow) ? 0 : 1)
                    .ThenBy(RaceRankOrder)
                    .ThenBy(r => RaceRankOrder(r) == int.MaxValue ? _raceRanks.GetValueOrDefault(r) : 0)
                    .ThenBy(r => RaceIndex[r])
                : ClassNames.Races.Where(r => _dr[r] > 0)
                    .OrderBy(r => _locked.Contains(r))
                    .ThenByDescending(r => Prio("race:" + r))
                    .ThenBy(r => RaceIndex[r])).ToList();

            foreach (var race in raceOrder)
            {
                if (_result.Count >= CampaignPlan.MaxLoadouts) break;
                var byRank = Abbrevs.OrderBy(c => _pc[c]).ToList();
                int third = _pc[byRank[2]];
                var trio = Best(byRank.Where(c => _pc[c] < third).ToList(), byRank.Where(c => _pc[c] == third).ToList());
                AddRuns(race, trio, Math.Min(ClassNames.MaxPassiveRank, _dr[race]));
            }
        }

        private int Prio(string key)
        {
            if (!_ranked) { int i = _plan.First.IndexOf(key); return i < 0 ? 0 : _plan.First.Count - i; }
            if (!key.StartsWith("class:")) return 0;
            int k = _rankedClasses.FindIndex(w => w.Key == key[6..]);
            return k < 0 ? 0 : _rankedClasses.Count - k;
        }

        private int RaceRankOrder(string race)
        {
            int i = _rankedRaces.FindIndex(w => w.Key == race);
            return i < 0 ? int.MaxValue : i;
        }

        private double QualityOf(string[] trio)
        {
            if (_quality == null) return 0;
            var k = ClassNames.TrioKey(trio);
            return _qualityCache.TryGetValue(k, out var v) ? v : _qualityCache[k] = _quality(trio);
        }

        private int FewestAfter(string[] trio) =>
            FewestClassRuns(Abbrevs.Select(c => _dc[c] - (_dc[c] > 0 && trio.Contains(c) ? 1 : 0)));

        private string[] NextClassRun()
        {
            int fewest = FewestClassRuns(_dc.Values);
            // The classes you want first ride along whenever that keeps the plan at its fewest runs.
            var first = (_ranked ? _rankedClasses.Where(w => !w.UnlockNow).Select(w => w.Key) : _plan.First.Where(k => k.StartsWith("class:")).Select(k => k[6..]))
                .Where(c => _dc.GetValueOrDefault(c) > 0).Take(3).ToList();
            for (int n = first.Count; n > 0; n--)
            {
                var t = Greedy(first.Take(n).ToList());
                if (FewestAfter(t) == fewest - 1) return t;
            }
            return Greedy([]);
        }

        private string[] Greedy(List<string> forced)
        {
            var rest = Abbrevs.Where(c => !forced.Contains(c)).OrderByDescending(c => _dc[c]).ThenBy(c => _pc[c]).ToList();
            int k = 3 - forced.Count;
            if (k == 0) return Abbrevs.Where(forced.Contains).ToArray();
            int third = _dc[rest[k - 1]];
            return Best([.. forced, .. rest.Where(c => _dc[c] > third)], rest.Where(c => _dc[c] == third).ToList());
        }

        /// <summary>
        /// Three classes: all of <paramref name="must"/>, the rest from <paramref name="tied"/>. Lowest rank reached first,
        /// then combos kept, then preferences, then lowest sum of ranks — never breaking a held combo when an allowed trio exists.
        /// </summary>
        private string[] Best(List<string> must, List<string> tied, bool anyTrio = false)
        {
            int k = 3 - must.Count;
            string[]? best = null;
            (int Top, double K, double Q, int Sum) bestKey = default;
            bool check = _combos.HasConstraints && !anyTrio;
            void TryCandidate(string[] t)
            {
                if (check && !_combos.Allowed(t, _pc)) return;
                var key = (t.Max(c => _pc[c] + 1), _combos.KeptScore(t, _result), QualityOf(t), t.Sum(c => _pc[c]));
                if (best == null || key.Item1 < bestKey.Top || key.Item1 == bestKey.Top && (key.Item2 > bestKey.K + 1e-9
                    || Math.Abs(key.Item2 - bestKey.K) <= 1e-9 && (key.Item3 > bestKey.Q + 1e-9
                    || Math.Abs(key.Item3 - bestKey.Q) <= 1e-9 && key.Item4 < bestKey.Sum))) { best = t; bestKey = key; }
            }
            if (k == 0) TryCandidate([.. must]);
            else if (k == 1) foreach (var a in tied) TryCandidate([.. must, a]);
            else if (k == 2) { for (int a = 0; a < tied.Count; a++) for (int b = a + 1; b < tied.Count; b++) TryCandidate([.. must, tied[a], tied[b]]); }
            else for (int a = 0; a < tied.Count; a++) for (int b = a + 1; b < tied.Count; b++) for (int c = b + 1; c < tied.Count; c++) TryCandidate([tied[a], tied[b], tied[c]]);
            if (best == null && check) return AnyAllowed(must) ?? AnyAllowed([]) ?? Best(must, tied, true);
            return Abbrevs.Where(best!.Contains).ToArray();
        }

        private string[]? AnyAllowed(List<string> must)
        {
            string[]? best = null;
            (int Fewest, int Top, double K, double Q, int Sum) bestKey = default;
            for (int a = 0; a < Abbrevs.Count; a++)
                for (int b = a + 1; b < Abbrevs.Count; b++)
                    for (int c = b + 1; c < Abbrevs.Count; c++)
                    {
                        string[] t = [Abbrevs[a], Abbrevs[b], Abbrevs[c]];
                        if (!must.All(t.Contains) || !_combos.Allowed(t, _pc)) continue;
                        var key = (FewestAfter(t), t.Max(x => _pc[x] + 1), _combos.KeptScore(t, _result), QualityOf(t), t.Sum(x => _pc[x]));
                        bool better = best == null || key.Item1 != bestKey.Fewest ? best == null || key.Item1 < bestKey.Fewest
                            : key.Item2 != bestKey.Top ? key.Item2 < bestKey.Top
                            : Math.Abs(key.Item3 - bestKey.K) > 1e-9 ? key.Item3 > bestKey.K
                            : Math.Abs(key.Item4 - bestKey.Q) > 1e-9 ? key.Item4 > bestKey.Q : key.Item5 < bestKey.Sum;
                        if (better) { best = t; bestKey = key; }
                    }
            return best;
        }

        private string RaceForClassRun(string[] trio)
        {
            var open = ClassNames.Races.Where(r => !_locked.Contains(r)).ToList();
            if (open.Count == 0) open = [.. ClassNames.Races];
            if (_ranked)
            {
                int top = trio.Max(c => _pc[c] + 1);
                var now = _rankedRaces.FirstOrDefault(w => w.UnlockNow && open.Contains(w.Key) && _dr[w.Key] > 0);
                if (now != null) return now.Key;
                return open.OrderBy(r => Math.Max(_pr[r] + 1, top))
                    .ThenBy(r => _dr[r] > 0 ? 0 : 1)
                    .ThenBy(r => _dr[r] > 0 ? RaceRankOrder(r) : int.MaxValue)
                    .ThenBy(r => _dr[r] > 0 && RaceRankOrder(r) == int.MaxValue ? _raceRanks.GetValueOrDefault(r) : 0)
                    .ThenBy(r => _dr[r] > 0 ? 0 : _pr[r])
                    .ThenBy(r => RaceIndex[r]).First();
            }
            return open.OrderBy(r => _dr[r] > 0 ? 0 : 1)
                .ThenByDescending(r => Prio("race:" + r))
                .ThenByDescending(r => _dr[r])
                .ThenBy(r => _pr[r])
                .ThenBy(r => RaceIndex[r]).First();
        }

        private void AddRuns(string race, string[] trio, int times)
        {
            var last = _result.Count > _kept ? _result[^1] : null;
            if (times == 1 && last != null && last.Race == race && last.Times < ClassNames.MaxPassiveRank && ClassNames.TrioKey(last.Classes) == ClassNames.TrioKey(trio))
                last.Times++;
            else _result.Add(new CampaignLoadout { Race = race, Classes = [.. trio], Times = times });
            foreach (var c in trio) { _pc[c] += times; _dc[c] = Math.Max(0, _dc[c] - times); }
            _pr[race] += times;
            _dr[race] = Math.Max(0, _dr[race] - times);
        }
    }

    /// <summary>
    /// Plan cost comparison order: every class goal met, lowest XP to level, earlier class ranks and Completionist,
    /// combo retention, preferences, then earlier race ranks.
    /// </summary>
    private readonly record struct Cost(bool Met, double Xp, double ClassSoon, double Kept, double Prefer, double RaceSoon)
    {
        public bool BetterThan(Cost o) =>
            Met != o.Met ? Met
            : Math.Abs(Xp - o.Xp) > 1e-9 ? Xp < o.Xp
            : Math.Abs(ClassSoon - o.ClassSoon) > 1e-9 ? ClassSoon > o.ClassSoon
            : Math.Abs(Kept - o.Kept) > 1e-9 ? Kept > o.Kept
            : Math.Abs(Prefer - o.Prefer) > 1e-9 ? Prefer > o.Prefer
            : RaceSoon > o.RaceSoon + 1e-9;
    }

    private static readonly Dictionary<string, int> ClassIndex = Abbrevs.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i);
    private static readonly Dictionary<string, int> RaceIndex = ClassNames.Races.Select((r, i) => (r, i)).ToDictionary(x => x.r, x => x.i);

    /// <summary>
    /// The greedy pass decides one run at a time. This trades a class between two new loadouts whenever the whole plan
    /// costs less (<see cref="Cost"/>): less XP, class ranks sooner, more of what you prefer. A trade must leave every class
    /// goal met, and never moves a class you want first to a later loadout.
    /// </summary>
    private static void Improve(List<CampaignLoadout> ls, int from, int[] skip, CampaignPlan plan,
        IReadOnlyDictionary<string, int> classRanks, IReadOnlyDictionary<string, int> raceRanks,
        Func<string[], double> quality, Func<string, int> prio)
    {
        if (ls.Count - from < 2) return;
        var startC = Abbrevs.Select(c => classRanks.GetValueOrDefault(c)).ToArray();
        var startR = ClassNames.Races.Select(r => raceRanks.GetValueOrDefault(r)).ToArray();
        var goals = plan.Classes.Select(kv => (I: ClassIndex[kv.Key], T: kv.Value)).ToArray();
        var combos = plan.Combos ?? [];
        var wanted = combos.Where(c => !c.Apart).ToList();
        var protectedTrios = combos.Where(c => !c.Apart && c.IsHeld() && c.Classes.Count == 3).ToList();
        var stay = plan.Ranked ? (plan.ClassOrder ?? []).Where(w => w.UnlockNow).Select(w => w.Key).ToHashSet() : [];
        bool Protected(CampaignLoadout l) => protectedTrios.Any(c => c.Classes.All(l.Classes.Contains));

        Cost Measure() => MeasurePlanCost(ls, from, skip, startC, startR, goals, wanted, quality);

        var best = Measure();
        int broke = ComboPlacement.CountBrokenRuns(combos, ls, skip, startC, ClassIndex);
        for (int pass = 0; pass < 12; pass++)
        {
            bool better = false;
            for (int i = from; i < ls.Count; i++)
                for (int j = i + 1; j < ls.Count; j++)
                {
                    if (Protected(ls[i]) || Protected(ls[j])) continue;   // a Must's runs stay as they are
                    for (int a = 0; a < 3; a++)
                        for (int b = 0; b < 3; b++)
                        {
                            string x = ls[i].Classes[a], y = ls[j].Classes[b];
                            if (x == y || ls[i].Classes.Contains(y) || ls[j].Classes.Contains(x) || prio("class:" + x) > prio("class:" + y)) continue;
                            if (stay.Contains(x) || stay.Contains(y)) continue;   // an Unlock now class keeps its back-to-back runs
                            ls[i].Classes[a] = y; ls[j].Classes[b] = x;
                            int nb = broke == 0 && combos.Count == 0 ? 0 : ComboPlacement.CountBrokenRuns(combos, ls, skip, startC, ClassIndex);
                            var v = Measure();
                            if (nb <= broke && v.BetterThan(best)) { best = v; broke = nb; better = true; }
                            else { ls[i].Classes[a] = x; ls[j].Classes[b] = y; }
                        }
                }
            if (!better) break;
        }
    }

    private static Cost MeasurePlanCost(List<CampaignLoadout> ls, int from, int[] skip, int[] startC, int[] startR,
        (int I, int T)[] goals, List<CampaignCombo> wanted, Func<string[], double> quality)
    {
        var pc = (int[])startC.Clone();
        var pr = (int[])startR.Clone();
        int total = 0;
        for (int i = 0; i < ls.Count; i++) total += ls[i].Times - skip[i];
        int n = 0, comp = pc.Min();
        double xp = 0, classSoon = 0, raceSoon = 0, q = 0, kept = 0;
        for (int i = 0; i < ls.Count; i++)
        {
            var l = ls[i];
            var ci = l.Classes.Select(c => ClassIndex[c]).ToArray();
            int ri = RaceIndex[l.Race];
            double lq = i >= from ? quality([.. l.Classes]) : 0;
            for (int p = 0; p < l.Times; p++)
            {
                if (p < skip[i]) continue;
                n++;
                int top = 0;
                foreach (var c in ci)
                {
                    if (++pc[c] <= ClassNames.MaxPassiveRank) classSoon += total - n;
                    top = Math.Max(top, pc[c]);
                }
                if (++pr[ri] <= ClassNames.MaxPassiveRank) raceSoon += total - n;
                top = Math.Max(top, pr[ri]);
                int c2 = pc.Min();
                if (c2 > comp && c2 <= ClassNames.MaxPassiveRank) classSoon += total - n;
                comp = c2;
                xp += Planner.XpMultiplier(top);
                q += lq;
                foreach (var c in wanted) if (c.Classes.All(l.Classes.Contains)) kept++;
            }
        }
        return new Cost(goals.All(g => pc[g.I] >= g.T), xp, classSoon, kept, q, raceSoon);
    }
}
