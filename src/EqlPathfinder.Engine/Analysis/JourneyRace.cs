namespace EqlPathfinder.Engine.Analysis;

/// <summary>One finished relevel in a race: <see cref="Journey"/> is its index in the journeys the race was made from.</summary>
/// <param name="F">The target XP over the relevel's own: what its hours and curve were multiplied by.</param>
/// <param name="Hours">Its active hours re-figured for the target XP.</param>
/// <param name="SplitMinutes">Minutes each stretch between <see cref="Race.SplitLevels"/> took, re-figured; null where it never reached the level.</param>
public sealed record RaceRow(int Journey, double F, double Hours, IReadOnlyList<double?> SplitMinutes);

/// <summary>The middle half (25th to 75th percentile) and the median of the hours each level took, every half level.</summary>
public sealed record RaceBand(IReadOnlyList<CurvePoint> Lo, IReadOnlyList<CurvePoint> Hi, IReadOnlyList<CurvePoint> Med);

/// <summary>One split level for the open relevel: its hours there (reached: its curve; later: its projection), the best's, and the difference.</summary>
public sealed record RaceSplit(int Level, double? You, bool Projected, double? Best, double? Delta);

/// <summary>
/// The open relevel against the race. <see cref="Level"/> is the level it is at (the later of the loadout's and the curve's
/// last point). <see cref="Ahead"/> and <see cref="BestAt"/> are at <see cref="Level"/> against its active hours; <see cref="Gap"/> is the
/// chart's gap, at the curve's last point (<see cref="CurveLevel"/>, <see cref="CurveHours"/>).
/// </summary>
public sealed record RaceNow(int Journey, double Level, double? BestAt, double? TypicalAt, double? BestOwn, double? Ahead,
    double CurveLevel, double CurveHours, double? CurveBestAt, double? Gap, IReadOnlyList<double?> SplitMinutes, IReadOnlyList<RaceSplit> Splits);

/// <summary>
/// The finished relevels raced at one XP requirement. <see cref="Rows"/> are best first; <see cref="BestCurve"/> the best's curve
/// re-figured; <see cref="TypicalHours"/> the median of the re-figured hours, <see cref="TypicalPlayedHours"/> of the hours as played.
/// <see cref="BestOfEach"/> is the fastest minutes in each split column, <see cref="BestOfEachHours"/> their sum (null if a column is empty).
/// </summary>
public sealed record Race(double Target, IReadOnlyList<RaceRow> Rows, IReadOnlyList<CurvePoint>? BestCurve, RaceBand Band, double? TypicalHours,
    double? TypicalPlayedHours, double MaxHours, bool MixedRanks, bool Refigured, IReadOnlyList<int> SplitLevels, IReadOnlyList<double?> BestOfEach,
    double? BestOfEachHours, double? BestOfEachUnderBest, RaceNow? Now);

/// <summary>The between-relevels card: the next run's XP, what Ask's route costs at it, and the race to beat at it.</summary>
public sealed record NextRace(double Xp, double Scale, double? RouteMinutes, double? RouteT5, IReadOnlyList<double> LegMinutes, Race Race);

/// <summary>Every race a page draws, from the journeys: Relevels' (all kinds), Home's (the open relevel's kind), the next run's, and your own.</summary>
public sealed record RaceSet(Race All, Race? Now, NextRace? Next, Race Own);

/// <summary>
/// Relevel race comparison: scales each finished relevel's hours and curve to the target XP multiplier
/// (docs/pace.md), computes the interquartile range, splits, and current standing. Pages render only.
/// </summary>
public static class JourneyRace
{
    /// <summary>The levels the splits are taken at; 46 is where the planes open.</summary>
    public static readonly IReadOnlyList<int> Splits = [10, 20, 30, 40, 46, 50];

    /// <summary>The XP requirement at a rank (the last rank's holds beyond).</summary>
    public static double XpOf(int rank) => Planner.XpByRank[Math.Clamp(rank, 0, Planner.XpByRank.Count - 1)];

    /// <summary>Every finished relevel, whatever its kind. A null <paramref name="target"/> compares at the XP the latest one needed.</summary>
    public static Race All(IReadOnlyList<Journey> journeys, int cap, double? target, int? activeIndex = null) => Make(journeys, cap, target, null, true, activeIndex);

    /// <summary>The finished relevels of one kind ("D4", "mixed"); with no kind, nothing to race.</summary>
    public static Race OfKind(IReadOnlyList<Journey> journeys, int cap, double? target, string? kind, int? activeIndex = null) =>
        Make(journeys, cap, target, kind, kind != null, activeIndex);

    /// <summary>
    /// The card between relevels: the next run's XP (<paramref name="nextXp"/>, or this trio's own when the campaign has none), Ask's route
    /// scaled to it, and the best of the same kind at it.
    /// </summary>
    public static NextRace Next(IReadOnlyList<Journey> journeys, int cap, int currentRank, double? nextXp, string? kind, double? routeMinutes, double? routeT5, IReadOnlyList<double> legMinutes)
    {
        var xp = nextXp ?? XpOf(currentRank);
        var scale = xp / XpOf(currentRank);
        return new NextRace(xp, scale, routeMinutes * scale, routeT5 * scale, legMinutes.Select(m => m * scale).ToList(), OfKind(journeys, cap, xp, kind));
    }

    /// <summary>What a page draws, given the open relevel (an index into <paramref name="journeys"/>), the kind Home compares, the campaign's next XP and the route.</summary>
    public static RaceSet For(IReadOnlyList<Journey> journeys, int cap, int? activeIndex, string? homeKind, int currentRank, double? nextXp,
        double? routeMinutes, double? routeT5, IReadOnlyList<double> legMinutes)
    {
        var active = activeIndex is { } i ? journeys[i] : null;
        double? target = active != null ? XpOf(active.Rank) : null;
        var kind = homeKind ?? active?.Mostly;
        return new RaceSet(All(journeys, cap, target, activeIndex),
            active != null ? OfKind(journeys, cap, target, kind, activeIndex) : null,
            Next(journeys, cap, currentRank, nextXp, homeKind, routeMinutes, routeT5, legMinutes),
            OfKind(journeys, cap, null, homeKind));
    }

    private static Race Make(IReadOnlyList<Journey> journeys, int cap, double? target, string? kind, bool compare, int? activeIndex)
    {
        var fin = compare
            ? journeys.Select((j, i) => (j, i)).Where(x => x.j.Done && x.j.Curve.Count > 1 && (kind == null || x.j.Mostly == kind)).ToList()
            : [];
        var latest = fin.OrderByDescending(x => x.j.End).Select(x => ((Journey, int)?)x).FirstOrDefault();
        var t = target ?? (latest is { } l ? XpOf(l.Item1.Rank) : 1);
        var made = fin.Select(x =>
        {
            var f = t / XpOf(x.j.Rank);
            return (x.i, x.j, f, hours: x.j.ActiveHours * f, curve: Scale(x.j.Curve, f));
        }).OrderBy(x => x.hours).ToList();
        var marks = Splits.Where(s => s <= cap).ToList();
        var rows = made.Select(x => new RaceRow(x.i, x.f, x.hours, SplitMinutes(x.curve, marks))).ToList();
        var bestOf = marks.Select((_, k) => rows.Select(r => r.SplitMinutes[k]).Where(m => m != null).Select(m => m!.Value).ToList())
            .Select(v => v.Count > 0 ? (double?)v.Min() : null).ToList();
        double? sum = bestOf.All(v => v != null) && bestOf.Count > 0 ? bestOf.Sum(v => v!.Value) / 60 : null;
        var curves = made.Select(x => x.curve).ToList();
        double maxH = Math.Max(1, curves.SelectMany(c => c).Select(p => p.Hours).DefaultIfEmpty(0).Max());
        var best = made.Count > 0 ? made[0] : default;
        var now = activeIndex is { } a ? Standing(journeys, a, made.Select(x => (x.j, x.curve)).ToList(), marks) : null;
        return new Race(t, rows, made.Count > 0 ? best.curve : null, Band(curves, cap), made.Count > 0 ? Median(made.Select(x => (double?)x.hours)) : null,
            made.Count > 0 ? Median(made.Select(x => (double?)x.j.ActiveHours)) : null, maxH,
            journeys.Where(j => j.Done).Select(j => j.Rank).Distinct().Count() > 1, journeys.Where(j => j.Done).Select(j => j.Rank).Distinct().Count() > 1 || made.Any(x => x.f != 1),
            marks, bestOf, sum, sum != null && made.Count > 0 ? made[0].hours - sum : null, now);
    }

    private static RaceNow? Standing(IReadOnlyList<Journey> journeys, int index, List<(Journey j, List<CurvePoint> curve)> cmp, List<int> marks)
    {
        var act = journeys[index];
        if (act.Curve.Count == 0) return null;
        var last = act.Curve[^1];
        double lv = act.ReachedLevel;
        var bestCurve = cmp.Count > 0 ? cmp[0].curve : null;
        double? bestAt = bestCurve != null ? HoursAt(bestCurve, lv) : null;
        double? typAt = cmp.Count > 0 ? Median(cmp.Select(c => HoursAt(c.curve, lv))) : null;
        double? bestOwn = cmp.Count > 0 ? HoursAt(cmp[0].j.Curve, lv) : null;
        double? curveBest = bestCurve != null ? HoursAt(bestCurve, last.Level) : null;
        var splits = marks.Select(L =>
        {
            bool reached = L <= lv;
            double? you = reached ? HoursAt(act.Curve, L) : HoursAt(act.Projection ?? [], L);
            double? b = bestCurve != null ? HoursAt(bestCurve, L) : null;
            return new RaceSplit(L, you, !reached, b, you != null && b != null ? you - b : null);
        }).ToList();
        return new RaceNow(index, lv, bestAt, typAt, bestOwn, bestAt != null ? bestAt - act.ActiveHours : null, last.Level, last.Hours, curveBest,
            curveBest != null ? curveBest - last.Hours : null, SplitMinutes(act.Curve, marks), splits);
    }

    /// <summary>The minutes each stretch took (from the last level reached); a level never reached has none and leaves the clock where it was.</summary>
    private static List<double?> SplitMinutes(IReadOnlyList<CurvePoint> curve, IReadOnlyList<int> marks)
    {
        double prev = 0;
        return marks.Select(L =>
        {
            if (HoursAt(curve, L) is not { } t) return (double?)null;
            var m = (t - prev) * 60;
            prev = t;
            return m;
        }).ToList();
    }

    private static List<CurvePoint> Scale(IReadOnlyList<CurvePoint> c, double f) => f == 1 ? c.ToList() : c.Select(p => new CurvePoint(p.Hours * f, p.Level)).ToList();

    /// <summary>Active hours when the curve first reached level L (interpolated), or null if it never did.</summary>
    internal static double? HoursAt(IReadOnlyList<CurvePoint> c, double L)
    {
        if (c.Count == 0) return null;
        if (c[0].Level >= L) return c[0].Hours;
        for (int i = 1; i < c.Count; i++)
            if (c[i].Level >= L)
            {
                var a = c[i - 1];
                var b = c[i];
                return b.Level == a.Level ? b.Hours : a.Hours + (L - a.Level) / (b.Level - a.Level) * (b.Hours - a.Hours);
            }
        return null;
    }

    private static double Quantile(List<double> sorted, double q)
    {
        var i = (sorted.Count - 1) * q;
        int lo = (int)Math.Floor(i), hi = (int)Math.Ceiling(i);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (i - lo);
    }

    /// <summary>The median of the values that exist (null with none): the 0.5 quantile, interpolated.</summary>
    private static double? Median(IEnumerable<double?> xs)
    {
        var v = xs.Where(x => x != null).Select(x => x!.Value).Order().ToList();
        return v.Count > 0 ? Quantile(v, 0.5) : null;
    }

    /// <summary>The middle half and median of the hours each level took across curves, every half level from where the first starts.</summary>
    private static RaceBand Band(List<List<CurvePoint>> curves, int cap)
    {
        List<CurvePoint> lo = [], hi = [], med = [];
        if (curves.Count < 2) return new RaceBand(lo, hi, med);
        var start = curves.Min(c => c[0].Level);
        for (var L = Math.Ceiling(start * 2) / 2; L <= cap; L += 0.5)
        {
            var hs = curves.Where(c => c[0].Level <= L).Select(c => HoursAt(c, L)).Where(h => h != null).Select(h => h!.Value).Order().ToList();
            if (hs.Count < 2) continue;
            lo.Add(new CurvePoint(Quantile(hs, 0.25), L));
            hi.Add(new CurvePoint(Quantile(hs, 0.75), L));
            med.Add(new CurvePoint(Quantile(hs, 0.5), L));
        }
        return new RaceBand(lo, hi, med);
    }
}
