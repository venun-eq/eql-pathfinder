using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>
/// "Am I going at a proper speed?" for one run, like for like (docs/pace.md). Levels an hour is kill speed × XP per kill:
/// kill speed is the player's part, compared with their usual at the same place, level and solo or group; XP per kill is
/// the place's part, compared with what that place gives at this level, at this run's XP penalty. Stored facts stay raw;
/// only the comparison normalizes.
/// </summary>
/// <param name="Verdict">"pace", "slow", "fast", "none" (fewer than <see cref="MinRuns"/> runs to compare with) or "early" (too little of this run yet).</param>
/// <param name="KillSpeed">This run's kills a minute over the usual (1.0 = the same).</param>
/// <param name="Runs">The runs behind the usual.</param>
/// <param name="Band">How far from 1 still counts as on pace: one run's normal swing.</param>
/// <param name="XpPerKill">This run's kill XP per kill, % of a level, as it happened (a crawl's completion XP not counted).</param>
/// <param name="ExpectedXpPerKill">What this place gives a kill at this run's level, at this run's XP penalty.</param>
/// <param name="Xp">"normal", "low" or "high" against <paramref name="ExpectedXpPerKill"/>; null without one.</param>
/// <param name="ExpectedLevelsPerHour">Usual kill speed × expected XP per kill: levels an hour from kills, before completion XP.</param>
public sealed record RunPace(string Verdict, double? KillSpeed, double? UsualKillsPerMinute, int Runs, double Band,
    double? XpPerKill, double? ExpectedXpPerKill, string? Xp, double? ExpectedLevelsPerHour)
{
    /// <summary>
    /// With no usual of your own: what players who share do here, around this level, solo or group the same, at this run's XP
    /// penalty. Context only, never a verdict (their trios and gear differ), and only with <see cref="MinPlayers"/> behind it.
    /// </summary>
    public OthersHere? Others { get; init; }

    /// <summary>The community file shows a place and level once this many players share it.</summary>
    public const int MinPlayers = 3;
    /// <summary>Minimum earlier runs near the level required before issuing a verdict.</summary>
    public const int MinRuns = 3;

    /// <summary>
    /// Progress note shown while a run has no usual (e.g. "No usual here yet: 2 of 3 earlier runs within 2 levels of 20").
    /// No trailing period; callers append context.
    /// </summary>
    public static string NoUsualNote(int runs, int? level) =>
        $"No usual here yet: {runs} of {MinRuns} earlier runs" + (level is { } l ? $" within {FarmPace.LevelWindow} levels of {l}" : "");

    /// <summary>
    /// Normal kill-speed variance band around a usual (~1 SD is ±14%); wider for 3–4 runs where the median is less certain.
    /// </summary>
    public const double WideBand = 0.18, NarrowBand = 0.15;

    /// <summary>XP per kill swings less (±8% a run), so a gap past this is real.</summary>
    public const double XpBand = 0.15;

    /// <summary>
    /// Default log-slope for how XP per kill falls per level (−8% per level) when a place's runs span too few levels to fit its own.
    /// </summary>
    public static readonly double TypicalSlope = Math.Log(0.92);

    /// <summary>True when a run has too little play to judge yet, matching <see cref="Rates"/> (no time minimum).</summary>
    public static bool Early(LevelingSegment run) => Rates.Early(run.Completed, run.Kills);

    /// <summary>
    /// <paramref name="run"/> against <paramref name="history"/>: earlier runs at the same place (difficulty included) and solo or
    /// group the same. Only runs that started before it, so a verdict is fixed once the run is over (docs/pace.md). Kill speed's usual is the median of those within 2 levels; XP per kill's comes from all of them, each
    /// re-figured for this run's XP penalty and read at this run's level along the place's slope.
    /// </summary>
    public static RunPace For(LevelingSegment run, IEnumerable<LevelingSegment> history) => Judge(run, history) with { XpMultiplier = run.XpAt(BarOf(run)) };

    /// <summary>The bar the run ended on; a run at the cap of its own day was farming, and reads the penalty of the bar below (the levels it needed).</summary>
    private static int BarOf(LevelingSegment run)
    {
        int bar = (run.LevelStart ?? 1) + (int)Math.Floor(run.Levels), cap = run.Cap ?? Tracking.Expansions.All[0].LevelCap;
        return bar >= cap ? cap - 1 : bar;
    }

    /// <summary>The XP this run's levels needed (1.2 = "+20% XP"), for the views to say so beside the numbers.</summary>
    public double XpMultiplier { get; init; } = 1;

    /// <summary>The kills a minute the verdict judged (its kills over its played minutes): the Kill speed bar's value and the lines that quote it. Null without a verdict.</summary>
    public double? KillsPerMinute => KillSpeed * UsualKillsPerMinute;

    /// <summary>The Kill speed bar's colour: the verdict's own (slow is red, on pace and fast green); grey when no verdict judges it.</summary>
    public Tone KillSpeedTone => Verdict switch { "slow" => Tone.Warn, "pace" or "fast" => Tone.Good, _ => Tone.None };

    /// <summary>The XP per kill bar's colour: "low" is red, "normal" and "high" green; grey with no XP verdict or no usual to judge by.</summary>
    public Tone XpTone => Verdict is "early" or "none" ? Tone.None : Xp switch { "low" => Tone.Warn, "normal" or "high" => Tone.Good, _ => Tone.None };

    /// <summary>The same runs' usual T5-eq an hour: the overlay's T5-eq/h tick, so every tick on its card has one source.</summary>
    public double? UsualT5PerHour { get; init; }

    private static RunPace Judge(LevelingSegment run, IEnumerable<LevelingSegment> history)
    {
        var same = history.Where(h => h.Type != "open world" && h.Place == run.Place && h.Grouped == run.Grouped && h.LevelStart != null
            && Rates.Has(h.Completed, h.Kills) && h.PlayedMinutes > 0 && (run.VisitId == null || h.VisitId != run.VisitId) && h.Start < run.Start).ToList();

        // Kill XP over kills with known XP: no-XP mobs count as 0; XP lines without a percent are excluded.
        double? xpk = run.XpKills >= Rates.MinKills ? Rates.XpPerKill(run.KillLevels, run.XpKills) : null;
        double? expectedXp = ExpectedXp(same, run);
        string? xp = xpk is { } a && expectedXp is { } e ? (a < e * (1 - XpBand) ? "low" : a > e * (1 + XpBand) ? "high" : "normal") : null;

        var near = same.Where(h => run.LevelStart is { } l && Math.Abs(h.LevelStart!.Value - l) <= FarmPace.LevelWindow).ToList();
        double band = near.Count >= 5 ? NarrowBand : WideBand;
        double? usual = near.Count >= MinRuns ? RouteAnalysis.Median(near.Select(h => h.Kills / h.PlayedMinutes)) : null;
        double? lvh = usual is { } u && expectedXp is { } x ? u * x * 60 / 100 : null;
        double? usualT5 = usual != null ? RouteAnalysis.Median(near.Select(h => Rates.PerHour(h.T5, h.PlayedMinutes))) : null;
        if (usual == null) return new RunPace("none", null, null, near.Count, band, xpk, expectedXp, xp, lvh);
        if (Early(run)) return new RunPace("early", null, usual, near.Count, band, xpk, expectedXp, xp, lvh) { UsualT5PerHour = usualT5 };

        double speed = run.Kills / run.PlayedMinutes / usual.Value;
        string verdict = speed < 1 - band ? "slow" : speed > 1 + band ? "fast" : "pace";
        return new RunPace(verdict, speed, usual, near.Count, band, xpk, expectedXp, xp, lvh) { UsualT5PerHour = usualT5 };
    }

    /// <summary>
    /// What this place gives a kill at <paramref name="run"/>'s level and XP penalty: each earlier run's kill XP per kill at
    /// rank 0, moved along the place's slope (its own when its runs span 3+ levels, else the typical one) to this run's level,
    /// and averaged over runs within <see cref="FarmPace.LevelWindow"/> levels (or all runs if fewer than <see cref="MinRuns"/>
    /// are nearby, avoiding global-fit overshoot at the low end). Null with fewer than <see cref="MinRuns"/> runs or more
    /// than 2 levels outside observed range.
    /// </summary>
    public static double? ExpectedXp(IReadOnlyList<LevelingSegment> same, LevelingSegment run)
    {
        if (run.LevelStart is not { } start) return null;
        var pts = same.Where(h => h.KillLevels > 0 && h.XpKills > 0).Select(h => (Start: h.LevelStart!.Value, X: h.LevelStart!.Value + h.Levels / 2,
            Y: Math.Log(Rates.XpPerKill(h.KillLevels, h.XpKills)!.Value * h.XpMultiplier))).ToList();
        if (pts.Count < MinRuns) return null;
        double at = start + Math.Max(0, run.Levels) / 2;
        if (at < pts.Min(p => p.X) - 2 || at > pts.Max(p => p.X) + 2) return null;
        double slope = TypicalSlope;
        if (pts.Count >= 4 && pts.Max(p => p.X) - pts.Min(p => p.X) >= 3 && Fit.Slope(pts.Select(p => (p.X, p.Y)).ToList()) is { } fit)
            slope = Math.Clamp(fit, Math.Log(0.8), Math.Log(1.05));
        var near = pts.Where(p => Math.Abs(p.Start - start) <= FarmPace.LevelWindow).ToList();
        return Math.Exp((near.Count >= MinRuns ? near : pts).Average(p => p.Y + slope * (at - p.X))) / run.XpMultiplier;
    }
}

/// <summary>Players who share, at a place around a level: their kills a minute, and levels an hour at the XP penalty asked for.</summary>
public sealed record OthersHere(int Players, double KillsPerMinute, double LevelsPerHour)
{
    public static OthersHere? At(Community.CommunityView? view, string place, int level, double xpMultiplier)
    {
        if (view == null || view.PlayersNear(place, level) is var n && n < RunPace.MinPlayers) return null;
        var w = view.Ask.Places.FirstOrDefault(p => p.Place == place)?.Window(level);
        return w is { Minutes: > 0, KillsPerMinute: > 0 } ? new OthersHere(n, w.KillsPerMinute, 60 / (w.Minutes * xpMultiplier)) : null;
    }
}

/// <summary>
/// Is another place clearly better at this level? "fading" when this place was the best a few levels ago and has been passed;
/// "slower" when it wasn't. Only past one run's normal swing in levels an hour (±13%), shrunk by how many runs each side has.
/// </summary>
/// <param name="LevelsPerHour">This place at the level, at the XP penalty asked for.</param>
/// <param name="BetterLevelsPerHour">The better place at the level, at the same penalty.</param>
public sealed record PlaceCheck(string Kind, string Place, double LevelsPerHour, string Better, double BetterLevelsPerHour)
{
    /// <summary>Typical per-run log standard deviation in levels per hour (~0.13).</summary>
    public const double RunSwing = 0.13;

    /// <summary>A gap smaller than this isn't worth moving for, however certain.</summary>
    public static readonly double MinGap = Math.Log(1.1);

    /// <param name="open">Whether a place is open at a level (planes need 46).</param>
    public static PlaceCheck? At(AskData d, string place, int level, double xpMultiplier, Func<string, int, bool> open) =>
        Of(d, place, level, xpMultiplier, open).Check;

    /// <summary>
    /// The check, and when another place is faster but not clearly (inside the noise, or under 10%) that place's name, so a page
    /// can say why nothing is flagged from the same check that flags (the overlay's "isn't clearly faster").
    /// </summary>
    public static (PlaceCheck? Check, string? NotClear) Of(AskData d, string place, int level, double xpMultiplier, Func<string, int, bool> open)
    {
        var mine = d.Places.FirstOrDefault(p => p.Place == place);
        if (mine?.Window(level) is not { Visits: >= 1 } me) return (null, null);
        var best = Best(d, place, level, open);
        if (best is not { } b) return (null, null);
        if (!Clear(me, b.W)) return (null, b.W.Minutes < me.Minutes ? b.Place : null);
        // Fading: this place was the best a few levels back, on 2+ runs (merely "not clearly beaten" is too easy on thin data).
        bool fading = mine.Window(level - 3) is { Visits: >= 2 } then && (Best(d, place, level - 3, open) is not { } b3 || then.Minutes <= b3.W.Minutes);
        return (new PlaceCheck(fading ? "fading" : "slower", place, 60 / (me.Minutes * xpMultiplier), b.Place, 60 / (b.W.Minutes * xpMultiplier)), null);
    }

    /// <summary>
    /// Compare only places of the same difficulty (and open world only with open world), since higher difficulties
    /// are played for motes rather than pure leveling speed.
    /// </summary>
    public static bool Alike(string a, string b) => DifficultyOf(a) == DifficultyOf(b);

    private static string DifficultyOf(string place) =>
        place.EndsWith("· open world", StringComparison.Ordinal) ? "open" : System.Text.RegularExpressions.Regex.Match(place, @" D(\d)$") is { Success: true } m ? "D" + m.Groups[1].Value : "other";

    private static (string Place, LevelWindow W)? Best(AskData d, string place, int level, Func<string, int, bool> open) =>
        d.Places.Where(p => p.Place != place && Alike(p.Place, place) && open(p.Place, level) && p.Window(level) is { Visits: >= 2 })
            .Select(p => (p.Place, W: p.Window(level)!)).OrderBy(x => x.W.Minutes).Cast<(string, LevelWindow)?>().FirstOrDefault();

    /// <summary>The other place is faster by more than the noise of both (2 standard errors) and by at least 10%.</summary>
    private static bool Clear(LevelWindow me, LevelWindow other)
    {
        double gap = Math.Log(me.Minutes / other.Minutes);
        double se = RunSwing * Math.Sqrt(1.0 / me.Visits + 1.0 / other.Visits);
        return gap > Math.Max(2 * se, MinGap);
    }
}

/// <summary>
/// A farming or grouped visit against your usual T5-eq an hour: the same kind at the same place and difficulty (crawls you
/// finished or instances you didn't, solo or grouped). Its band is the normal swing of your own visits there, not kill speed's
/// ±15% by assumption (docs/pace.md).
/// </summary>
/// <param name="Verdict">"more", "usual", "less", "none" (fewer than <see cref="RunPace.MinRuns"/> visits to compare with) or "early".</param>
/// <param name="Pct">This visit's T5-eq an hour over (+) or under (−) the usual, as a fraction.</param>
public sealed record FarmPace(string Verdict, double? Pct, double? Usual, int Runs, double Band, double? T5PerHour, string Basis)
{
    /// <summary>The band before there is enough of your own play to measure one, and the least and most it may be.</summary>
    public const double DefaultBand = 0.25, MinBand = 0.15, MaxBand = 0.6;

    /// <summary>The T5-eq/h bar's colour: the verdict's own (less is red, more and usual green); grey when none judges it.</summary>
    public Tone Tone => Verdict switch { "less" => Tone.Warn, "more" or "usual" => Tone.Good, _ => Tone.None };

    /// <summary>The same visits' usual kills a minute (the overlay's Kills/min tick); null at the cap, where no kills are counted.</summary>
    public double? UsualKillsPerMinute { get; init; }

    /// <summary>The levels the usual is made of ("levels 38–42"), below the cap only; null at the cap. The screens draw this, they don't parse <see cref="Basis"/>.</summary>
    public string? Levels { get; init; }

    /// <summary>This visit's place among the visits its usual is made of (1 = best of them and this one), when it has a rate.</summary>
    public int? Rank { get; init; }

    private static double Rate(LevelingSegment s) => Rates.PerHour(s.T5, s.PlayedMinutes) ?? 0;

    /// <summary>What the usual is made of, in words. Below the cap it says which levels (the usual is within <see cref="LevelWindow"/> of the visit's), at the cap it doesn't: every level there is 50.</summary>
    public static string BasisOf(LevelingSegment run, int levelCap) => BasisOf(run, (_, _) => levelCap);

    public static string BasisOf(LevelingSegment run, Tracking.TrackerOptions o) => BasisOf(run, o.CapOn);

    internal static string BasisOf(LevelingSegment run, Func<DateTime, int?, int> capOn)
    {
        string kind = run.Grouped ? "grouped" : Finished(run) ? "crawls you finished, solo" : "instances you didn't finish";
        return run.LevelStart is { } l && l < capOn(run.Start, l) ? $"{kind}, {LevelsOf(l)}" : kind;
    }

    /// <summary>
    /// True for completed crawls and in-progress crawls (compared against finished crawls until abandoned).
    /// </summary>
    public static bool Finished(LevelingSegment run) => run.Completed || run.Type == "crawl" && !run.Ended;

    /// <summary>Level window (+/- levels) around a visit for selecting comparable farming runs.</summary>
    public const int LevelWindow = 2;

    public static string LevelsOf(int level) => $"levels {Math.Max(1, level - LevelWindow)}–{level + LevelWindow}";

    /// <summary>The note while a visit has no usual: "No usual here yet: 1 of 3 earlier visits like this one at levels 18–22." (<paramref name="levels"/> is <see cref="Levels"/>, null at the cap).</summary>
    public static string NoUsualNote(int runs, string? levels) =>
        $"No usual here yet: {runs} of {RunPace.MinRuns} earlier visits like this one{(levels != null ? " at " + levels : "")}.";

    /// <summary>The earlier visits a farming usual is made of: this place, solo or group, finished or not like this one (<see cref="Finished"/>), within <see cref="LevelWindow"/> levels, any trio, each with a rate.</summary>
    public static List<LevelingSegment> Peers(LevelingSegment run, IEnumerable<LevelingSegment> history, IReadOnlyCollection<int> farmed, int levelCap) => Peers(run, history, farmed, (_, _) => levelCap);

    public static List<LevelingSegment> Peers(LevelingSegment run, IEnumerable<LevelingSegment> history, IReadOnlyCollection<int> farmed, Tracking.TrackerOptions o) => Peers(run, history, farmed, o.CapOn);

    internal static List<LevelingSegment> Peers(LevelingSegment run, IEnumerable<LevelingSegment> history, IReadOnlyCollection<int> farmed, Func<DateTime, int?, int> capOn)
    {
        string kind = VisitKinds.Of(run, farmed, capOn);
        bool finished = Finished(run);
        // Finished crawls without a refund were too low-level (gear-only chest); exclude from farming usuals.
        return run.LevelStart is not { } level ? [] : history.Where(h => h.Place == run.Place && h.Grouped == run.Grouped && h.Completed == finished && (!h.Completed || h.Refunded)
            && h.LevelStart is { } hl && Math.Abs(hl - level) <= LevelWindow && Rates.Has(h.Completed, h.Kills) && h.PlayedMinutes > 0
            && (run.VisitId == null || h.VisitId != run.VisitId) && h.Start < run.Start && VisitKinds.ByMotes(VisitKinds.Of(h, farmed, capOn)) == VisitKinds.ByMotes(kind)).ToList();
    }

    public static FarmPace For(LevelingSegment run, IEnumerable<LevelingSegment> history, IReadOnlyCollection<int> farmed, int levelCap) => For(run, history, farmed, (_, _) => levelCap);

    public static FarmPace For(LevelingSegment run, IEnumerable<LevelingSegment> history, IReadOnlyCollection<int> farmed, Tracking.TrackerOptions o) => For(run, history, farmed, o.CapOn);

    internal static FarmPace For(LevelingSegment run, IEnumerable<LevelingSegment> history, IReadOnlyCollection<int> farmed, Func<DateTime, int?, int> capOn)
    {
        string basis = BasisOf(run, capOn);
        string? levels = run.LevelStart is { } ls && ls < capOn(run.Start, ls) ? LevelsOf(ls) : null;
        double mine = Rate(run);
        double? shown = RunPace.Early(run) ? null : mine;   // no rate under the rule (Rates): the page draws "—"
        var same = Peers(run, history, farmed, capOn);
        if (same.Count < RunPace.MinRuns) return new("none", null, null, same.Count, DefaultBand, shown, basis) { Levels = levels };
        var rates = same.Select(Rate).ToList();
        double usual = RouteAnalysis.Median(rates);
        double band = DefaultBand;
        var measured = rates.Where(r => r > 0).ToList(); // a crawl with no motes counts in the usual, but has no log to measure
        if (measured.Count >= 5)
        {
            var logs = measured.Select(r => Math.Log(r)).ToList();
            double m = logs.Average(), sd = Math.Sqrt(logs.Sum(x => (x - m) * (x - m)) / (logs.Count - 1));
            band = Math.Clamp(Math.Exp(sd) - 1, MinBand, MaxBand);
        }
        double? kpm = same.Any(h => h.Kills > 0) ? RouteAnalysis.Median(same.Select(h => (double)h.Kills / h.PlayedMinutes)) : null;
        if (RunPace.Early(run)) return new("early", null, usual, same.Count, band, shown, basis) { UsualKillsPerMinute = kpm, Levels = levels };
        double pct = usual > 0 ? mine / usual - 1 : 0;
        return new(pct > band ? "more" : pct < -band ? "less" : "usual", pct, usual, same.Count, band, shown, basis) { UsualKillsPerMinute = kpm, Rank = rates.Count(r => r > mine) + 1, Levels = levels };
    }

    /// <summary>
    /// A whole stop (consecutive visits at one place judged by motes): its T5-eq over its active hours, against the usual of its
    /// visits weighted by their minutes, with their band. "none" when no visit had a usual, "early" when the stop as a whole has no rate yet (<see cref="Rates"/>).
    /// </summary>
    public static FarmPace ForStop(IReadOnlyList<Tracking.VisitStats> visits) =>
        ForStop(visits.Select(v => (v.ActiveMinutes, v.T5, v.FarmUsual, v.FarmBand, v.FarmRuns, v.FarmBasis)).ToList(),
            Rates.Early(visits.Any(v => v.Completed), visits.Sum(v => v.Kills))) with
        { Levels = visits.Select(v => v.FarmLevels).FirstOrDefault(l => l != null) };

    /// <summary>The same from each visit's active minutes, T5-eq, usual, band, runs behind the usual and basis.</summary>
    public static FarmPace ForStop(IReadOnlyList<(double ActiveMinutes, double T5, double? Usual, double? Band, int Runs, string? Basis)> visits, bool early = false)
    {
        double minutes = visits.Sum(v => v.ActiveMinutes), t5 = visits.Sum(v => v.T5);
        double mine = Rates.PerHour(t5, minutes) ?? 0;
        double? shown = early ? null : mine;   // the stop's own rate, null under the rule
        string basis = visits.Select(v => v.Basis).FirstOrDefault(b => b != null) ?? "";
        var with = visits.Where(v => v.Usual is > 0 && v.ActiveMinutes > 0).ToList();
        if (with.Count == 0) return new("none", null, null, 0, DefaultBand, shown, basis);
        double w = with.Sum(v => v.ActiveMinutes);
        double usual = with.Sum(v => v.Usual!.Value * v.ActiveMinutes) / w, band = with.Sum(v => (v.Band ?? DefaultBand) * v.ActiveMinutes) / w;
        int runs = with.Min(v => v.Runs);
        if (early) return new("early", null, usual, runs, band, shown, basis);
        double pct = mine / usual - 1;
        return new(pct > band ? "more" : pct < -band ? "less" : "usual", pct, usual, runs, band, shown, basis);
    }
}

/// <summary>Metric bar color derived from its verdict: green, red, or neutral when unjudged.</summary>
public enum Tone { None, Good, Warn }
