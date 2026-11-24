using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Upgrades;

/// <summary>An item or spell to raise, and what there is to raise it with.</summary>
/// <param name="Progress">Points already in the current tier (the game shows it as "4/8").</param>
/// <param name="Motes">Motes owned, index = tier (1..10).</param>
/// <param name="Fragments">Fragments owned, index = tier.</param>
/// <param name="Vtps">Void-Touched Potentials to spend on it. Each covers one whole tier, so they take the top tiers.</param>
/// <param name="Copies">Your other copies of the item, each at its +N and the points already in that tier. The inventory
/// export has only the +N, so the player types the points.</param>
/// <param name="MergeCopies">True (the default): raise the copies too and merge them in. False: keep them; merging is priced only to compare.</param>
public sealed record UpgradeRequest(UpgradeTarget Target, int FromTier, long Progress, int Goal, int[] Motes, int[] Fragments,
    int Vtps = 0, ItemCopy[]? Copies = null, bool MergeCopies = true);

/// <summary>Another copy of the item: its +N and the points already in that tier.</summary>
public sealed record ItemCopy(int Tier, long Progress = 0);

/// <summary>One batch of motes or fragments of one tier, put in back to back.</summary>
/// <param name="Copy">0 = the item itself; n = the request's nth other copy, which is merged in afterwards.</param>
public sealed record UpgradeStep(LootKind Kind, MoteTier Tier, int Count, int PointsEach, int StartTier, long StartProgress,
    int EndTier, long EndProgress, int Copy = 0);

/// <summary>Merge copy <paramref name="Copy"/> (now at CopyTier and CopyProgress) into the item: it adds all its points, plus 1.</summary>
public sealed record MergeStep(int Copy, int FromCopyTier, int CopyTier, long CopyProgress, int StartTier, long StartProgress,
    int EndTier, long EndProgress);

/// <summary>Offer this many motes (or fragments) of <paramref name="From"/> at Constructed Potential to get half as many of the next tier.</summary>
public sealed record CondenseStep(MoteTier From, int Offered, LootKind Kind = LootKind.Mote);

/// <summary>The cheapest way found, in the order to do it.</summary>
/// <param name="PointsNeeded">Points from where the item is to where the VTPs take over (or the goal).</param>
/// <param name="Condense">Condensing of motes and fragments you have, in order; farmed Majors are condensed on top of this.</param>
/// <param name="FragmentsFromBank">Fragments of each tier taken from the bank, to put in or to condense.</param>
/// <param name="MotesToFarm">Motes of each tier the bank can't supply: farm Majors and condense them.</param>
/// <param name="CostT5">The value spent: motes at their T5-eq, fragments at their best other use.</param>
/// <param name="Overshoot">Points past the goal (or the VTP tier). They carry into the next tier as progress.</param>
/// <param name="CostByVtps">The whole cost with 0, 1, 2 … VTPs, so each VTP's worth here is the drop between neighbours.</param>
/// <param name="Merges">Copies merged in, in order, after the item's own steps (empty when the copies are kept).</param>
/// <param name="CostOtherWay">With copies: the cost the other way (merging them if they're kept, keeping them if merged), same VTPs.</param>
public sealed record UpgradePlan(
    int FromTier, long Progress, int Goal, long PointsNeeded, int VtpsUsed,
    IReadOnlyList<UpgradeStep> Steps, IReadOnlyList<CondenseStep> Condense,
    int[] MotesFromBank, int[] FragmentsFromBank, int[] MotesToFarm, double FarmT5, double CostT5, long Overshoot,
    int EndTier, long EndProgress, IReadOnlyList<double> CostByVtps,
    IReadOnlyList<MergeStep>? Merges = null, double? CostOtherWay = null);

/// <summary>
/// The cheapest way to raise an item or spell. The search covers every mix of owned motes and fragments plus Majors still
/// to farm (condensed to whatever tier fits), applied in tier order, with each tier's cap and the waste of overshooting
/// counted. Majors and the motes condensed from them cost their T5-eq; owned motes below Major are limited to what you
/// have because Majors can't be split down.
/// <para>
/// Copies to merge in are more items to put points into: a merge adds all of a copy's points plus 1, so a point costs
/// the same in any copy, except that a copy at a lower tier still takes cheaper motes. Each mote goes into whichever copy
/// is lowest, so two copies at +5 both go to +7 and merge into +8, instead of one going +5 → +8 on Grand motes.
/// </para>
/// </summary>
public static class UpgradePlanner
{
    public const int MaxTier = 10;

    // Ties go to the plan that puts in more points: points past the goal carry into the next tier as progress.
    private const double LeftoverTieBreak = -1e-9;

    public static double MoteCharge(MoteTier tier) => Potential.SpellT5(LootKind.Mote, tier);

    /// <summary>
    /// Opportunity cost of spending a fragment, in Majors. Because fragments condense 2:1, each fragment's value scales with tier;
    /// that's the best of a spell or an item at its top tier, for this fragment or any it can be condensed into, halved
    /// per condense, as high as the recommendation feeds gear (<see cref="Potential.RecommendedGoal"/>: +9, with VTPs
    /// saved for +9 → +10). From Lesser up that's an Ascendant fragment into a +8 item, about 56.9 Majors ÷ 2^(9 − tier):
    /// 3.56 for a Major fragment, against 2 on a spell. A fragment above that tier (an Infinite) is priced as the two it's
    /// condensed from, so the charge still doubles tier by tier (the plan relies on that: it never draws on the same
    /// fragments twice). Where a plan has no VTP for +9 → +10, fragments are worth more there (4.5 a Major), so they go there.
    /// </summary>
    public static double FragmentCharge(MoteTier tier)
    {
        if ((int)tier > Potential.RecommendedGoal)
            return FragmentCharge((MoteTier)Potential.RecommendedGoal) * (1 << ((int)tier - Potential.RecommendedGoal));
        double best = 0;
        for (int s = (int)tier; s <= Potential.RecommendedGoal; s++)
        {
            var t = (MoteTier)s;
            double use = Math.Max(Potential.GearMajors(LootKind.Fragment, t, Potential.GearMaxTier(t))!.Value, Potential.SpellT5(LootKind.Fragment, t));
            best = Math.Max(best, use / (1L << (s - (int)tier)));
        }
        return best;
    }

    // A fragment spent where it's worth exactly its charge saves farming, so a tie goes to the fragment.
    private const double FragmentTieBreak = 1e-7;

    public static UpgradePlan Plan(UpgradeRequest req)
    {
        int from = Math.Clamp(req.FromTier, 0, MaxTier);
        int goal = Math.Clamp(req.Goal, from, MaxTier);
        long progress = Math.Clamp(req.Progress, 0, Potential.TierCost(from) - 1);
        var motes = Counts(req.Motes);
        var frags = Counts(req.Fragments);
        var types = UnitTypes(req.Target, motes, frags);

        // Where each copy starts, in points from +0: the item itself first, then the other copies.
        var copies = (req.Copies ?? []).Select(c => Math.Clamp(c.Tier, 0, MaxTier)).ToArray();
        long[] alone = [Potential.PointsToReach(from) + progress];
        long[] all = [.. alone, .. (req.Copies ?? []).Select((c, i) =>
            Potential.PointsToReach(copies[i]) + Math.Clamp(c.Progress, 0, Potential.TierCost(copies[i]) - 1))];
        bool merge = req.MergeCopies && copies.Length > 0;
        var lanes = merge ? all : alone;

        int maxVtps = goal - from;
        var costs = new List<double>();
        for (int k = 0; k <= Math.Min(maxVtps, 5); k++) costs.Add(Solve(types, lanes, goal - k).Cost);

        int vtps = Math.Clamp(req.Vtps, 0, maxVtps);
        int stop = goal - vtps;
        var (picks, cost) = Solve(types, lanes, stop);
        double? otherWay = copies.Length > 0 ? Exact(Solve(types, merge ? alone : all, stop).Picks) : null;

        // Application order: tier by tier, motes before fragments (the order the search assumed). Each unit goes into the
        // copy that's lowest now, as the search assumed; the copies' points then merge into the item.
        var at = lanes.Select(c => (Tier: TierAt(c), Progress: c - Potential.PointsToReach(TierAt(c)))).ToArray();
        var batches = Enumerable.Range(0, lanes.Length).Select(_ => new List<UpgradeStep>()).ToArray();
        var needs = new int[MaxTier + 1];
        var fragsUsed = new int[MaxTier + 1];
        foreach (var (type, count) in picks)
        {
            var n = new int[lanes.Length];
            var start = at.ToArray();
            for (int u = 0; u < count; u++)
            {
                int low = 0;
                for (int l = 1; l < lanes.Length; l++) if (Cum(at[l]) < Cum(at[low])) low = l;
                at[low] = Potential.Add(at[low].Tier, at[low].Progress, type.Points);
                n[low]++;
            }
            for (int l = 0; l < lanes.Length; l++)
                if (n[l] > 0) batches[l].Add(new UpgradeStep(type.Kind, type.Tier, n[l], type.Points, start[l].Tier, start[l].Progress, at[l].Tier, at[l].Progress, l));
            if (type.Kind == LootKind.Mote) needs[(int)type.Tier] += count;
            else fragsUsed[(int)type.Tier] += count;
        }

        // The item's own steps, then each copy's, each followed by its merge.
        var steps = new List<UpgradeStep>(batches[0]);
        var merges = new List<MergeStep>();
        var (tier, prog) = at[0];
        for (int l = 1; l < lanes.Length; l++)
        {
            steps.AddRange(batches[l]);
            var (t2, p2) = Potential.Add(tier, prog, Cum(at[l]) + 1);
            merges.Add(new MergeStep(l, copies[l - 1], at[l].Tier, at[l].Progress, tier, prog, t2, p2));
            (tier, prog) = (t2, p2);
        }
        long overshoot = picks.Count > 0 || merges.Count > 0 ? Math.Max(0, Cum((tier, prog)) - Potential.PointsToReach(stop)) : 0;
        if (vtps > 0) tier = Math.Max(tier, stop) + vtps;

        var alloc = Allocate(needs, motes, LootKind.Mote);
        var fragAlloc = Allocate(fragsUsed, frags, LootKind.Fragment);   // never short: a type's count is what the bank can make
        double farm = 0;
        for (int r = 1; r <= MaxTier; r++) farm += alloc.ToFarm[r] * MoteCharge((MoteTier)r);
        return new UpgradePlan(from, progress, goal, Math.Max(0, Potential.PointsNeeded(from, progress, stop)), vtps, steps,
            [.. alloc.Condense, .. fragAlloc.Condense], alloc.FromBank, fragAlloc.FromBank, alloc.ToFarm, farm, Exact(picks), overshoot,
            Math.Min(tier, MaxTier), vtps > 0 ? 0 : prog, costs, merges, otherWay);
    }

    private static long Cum((int Tier, long Progress) x) => Potential.PointsToReach(x.Tier) + x.Progress;

    /// <summary>The tier an item is at with this many points from +0.</summary>
    private static int TierAt(long points)
    {
        int t = 0;
        while (t < MaxTier && Potential.PointsToReach(t + 1) <= points) t++;
        return t;
    }

    /// <summary>
    /// The lowest copy's position after <paramref name="added"/> points spread lowest-first over copies starting at
    /// <paramref name="starts"/>, rounded down. Units are lumpy, so the real lowest copy is at or below this: a cap checked
    /// here holds.
    /// </summary>
    private static long[] Levels(long[] starts, int n)
    {
        var a = starts.OrderBy(x => x).ToArray();
        var level = new long[n + 1];
        int j = 1;
        long sum = a[0];
        for (int s = 0; s <= n; s++)
        {
            while (j < a.Length && s + sum >= a[j] * j) sum += a[j++];
            level[s] = (s + sum) / j;
        }
        return level;
    }

    private sealed record UnitType(LootKind Kind, MoteTier Tier, int Points, double Cost, int Count, long Cap);

    private static int[] Counts(int[]? a)
    {
        var c = new int[MaxTier + 1];
        if (a != null) for (int i = 1; i <= MaxTier && i < a.Length; i++) c[i] = Math.Max(0, a[i]);
        return c;
    }

    private static List<UnitType> UnitTypes(UpgradeTarget target, int[] motes, int[] frags)
    {
        var list = new List<UnitType>();
        // Fragments of each tier the bank can supply: its own plus all the lower ones condensed up 2:1. The tiers overlap,
        // but charged at their best use only one tier is ever worth spending (the highest the item takes), so a plan
        // doesn't draw on the same fragments twice.
        var fragAvail = new int[MaxTier + 1];
        for (int r = 1; r <= MaxTier; r++) fragAvail[r] = frags[r] + fragAvail[r - 1] / 2;
        for (var t = MoteTier.Infinitesimal; t <= MoteTier.Infinite; t++)
        {
            int r = (int)t;
            // Gear: usable while the item is at most one tier below (cumulative points at most 2^r - 2). Spells: no cap.
            long cap = target == UpgradeTarget.Gear ? (1L << r) - 2 : long.MaxValue;
            int moteCount = t >= MoteTier.Major ? int.MaxValue : motes[r];
            int mp = Potential.Points(LootKind.Mote, t, target), fp = Potential.Points(LootKind.Fragment, t, target);
            if (moteCount > 0) list.Add(new UnitType(LootKind.Mote, t, mp, MoteCharge(t) + mp * LeftoverTieBreak, moteCount, cap));
            if (fragAvail[r] > 0)
                list.Add(new UnitType(LootKind.Fragment, t, fp, FragmentCharge(t) * (1 - FragmentTieBreak) + fp * LeftoverTieBreak, fragAvail[r], cap));
        }
        return list;
    }

    /// <summary>
    /// Bounded knapsack over points put in, one stage per unit type, clamped at the stop tier. With copies to merge, the
    /// points needed are what the merges don't bring, and a unit's cap is checked against the lowest copy.
    /// </summary>
    private static (List<(UnitType Type, int Count)> Picks, double Cost) Solve(List<UnitType> types, long[] lanes, int stop)
    {
        // Each merge brings a copy's points plus 1.
        long have = lanes.Sum() + lanes.Length - 1;
        long pg = Potential.PointsToReach(stop);
        if (pg <= have) return ([], 0);
        int n = (int)(pg - have);
        var level = Levels(lanes, n);

        var dp = new double[n + 1];
        Array.Fill(dp, double.PositiveInfinity);
        dp[0] = 0;
        var back = new List<(int[] From, int[] K)>();
        foreach (var type in types)
        {
            var nd = (double[])dp.Clone();
            var fromIdx = new int[n + 1];
            var ks = new int[n + 1];
            for (int s = 0; s < n; s++)
            {
                if (double.IsPositiveInfinity(dp[s])) continue;
                for (int k = 1; k <= type.Count; k++)
                {
                    // The kth unit starts where k - 1 have gone in, which is short of n (the loop stops at n).
                    if (level[s + (k - 1) * type.Points] > type.Cap) break;
                    int t = (int)Math.Min(s + (long)k * type.Points, n);
                    double c = dp[s] + k * type.Cost;
                    if (c < nd[t] - 1e-12) { nd[t] = c; fromIdx[t] = s; ks[t] = k; }
                    if (t == n) break;
                }
            }
            dp = nd;
            back.Add((fromIdx, ks));
        }

        var picks = new List<(UnitType, int)>();
        int at = n;
        for (int i = types.Count - 1; i >= 0; i--)
        {
            int k = back[i].K[at];
            if (k == 0) continue;
            picks.Add((types[i], k));
            at = back[i].From[at];
        }
        picks.Reverse();
        return (picks, dp[n]);
    }

    private static double Exact(List<(UnitType Type, int Count)> picks) =>
        picks.Sum(x => x.Count * (x.Type.Kind == LootKind.Mote ? MoteCharge(x.Type.Tier) : FragmentCharge(x.Type.Tier)));

    private sealed record Allocation(int[] FromBank, int[] ToFarm, List<CondenseStep> Condense);

    /// <summary>
    /// Covers the planned motes (or fragments) from the bank, lowest tier first: same tier, then lower ones condensed 2:1.
    /// What's left of motes is farmed as Majors and condensed up.
    /// </summary>
    private static Allocation Allocate(int[] needs, int[] owned, LootKind kind)
    {
        var bank = (int[])owned.Clone();
        var fromBank = new int[MaxTier + 1];
        var toFarm = new int[MaxTier + 1];
        var offered = new int[MaxTier + 1];

        int Make(int r, int x)
        {
            if (r - 1 < 1 || x <= 0) return 0;
            int want = 2 * x;
            int direct = Math.Min(want, bank[r - 1]);
            bank[r - 1] -= direct;
            fromBank[r - 1] += direct;
            int have = direct;
            if (have < want) have += Make(r - 1, want - have);
            if (have % 2 == 1)
            {
                // One short of a pair: it goes back to the bank (a bank mote untouched, or a condensed one kept).
                bank[r - 1]++;
                if (direct > 0) fromBank[r - 1]--;
                have--;
            }
            offered[r - 1] += have;
            return have / 2;
        }

        for (int r = 1; r <= MaxTier; r++)
        {
            int need = needs[r];
            if (need == 0) continue;
            int use = Math.Min(need, bank[r]);
            bank[r] -= use;
            fromBank[r] += use;
            need -= use;
            need -= Make(r, need);
            toFarm[r] = need;
        }
        var condense = new List<CondenseStep>();
        for (int r = 1; r < MaxTier; r++) if (offered[r] > 0) condense.Add(new CondenseStep((MoteTier)r, offered[r], kind));
        return new Allocation(fromBank, toFarm, condense);
    }
}
