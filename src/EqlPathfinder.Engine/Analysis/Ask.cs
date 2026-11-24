using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>A question from the Ask page; anything left null starts from where the player is.</summary>
/// <param name="Q">"level" (from A to B), "farm" (mote farm at a level), "diff" (which difficulty) or "dmg" (is my damage normal).</param>
/// <param name="Goal">"fast" or "motes" (count motes at the farm rate).</param>
/// <param name="Scope">"patch" (since the 11-10 patch) or "all".</param>
/// <param name="Diff">A <see cref="DifficultyCompare.Key"/>.</param>
/// <param name="Where">"all" (default), "instances" (crawls and other instances) or "open" (the open world).</param>
/// <param name="With">"either" (default), "solo" or "group".</param>
public sealed record AskQuery(string? Q, int? From, int? To, string? Goal, int? At, string? Scope, string? Diff, string? Where = null, string? With = null);

/// <summary>Loot in T5-eq, apart: motes, and fragments counted as 1× a same-tier mote; all of it, and above T5.</summary>
public sealed record LootParts(double MoteT5, double MoteHighTierT5, double FragmentT5, double FragmentHighTierT5);

/// <summary>
/// One stretch of leveling time from any source, tagged with what enabled it (spec "Questions the pool answers"): a crawl,
/// another instance, or an open-world hunt; solo or grouped; the trio and its reincarnation rank. Every XP source counts,
/// crawls are one of them.
/// </summary>
/// <param name="Type">"crawl" (an instance with a difficulty), "instance" (one without) or "open world".</param>
/// <param name="Rank">
/// The trio's reincarnation rank then (its most-reincarnated class): each rank adds 10% to the XP a level needs, up to 30
/// ranks (4×), so times are compared at rank 0 (<see cref="Planner.XpMultiplier"/>).
/// </param>
/// <param name="PartyShare">The share of its kills that gave party experience.</param>
public sealed record LevelingSegment(string Place, string Zone, int? Difficulty, DateTime Start, int? LevelStart, double ActiveMinutes,
    double Levels, double T5, double HighTierT5, int Fragments, int Kills, int Deaths, double Dps, int Rank = 0,
    string Type = "crawl", double PartyShare = 0, string? Trio = null)
{
    /// <summary>The visit's real active minutes when <see cref="ActiveMinutes"/> was scaled to today's pace (<see cref="PaceDrift"/>).</summary>
    public double? UnscaledMinutes { get; init; }

    /// <summary>The time actually played (evidence counts this, not the scaled time).</summary>
    public double PlayedMinutes => UnscaledMinutes ?? ActiveMinutes;

    /// <summary>Kills whose XP the log sizes: XP per kill divides by this, not <see cref="Kills"/> (a kill whose XP line had no percent is unknown, not 0).</summary>
    public int XpKills { get => _xpKills ?? Kills; init => _xpKills = value; }
    private readonly int? _xpKills;

    /// <summary>The visit it came from (null for an open-world hunt).</summary>
    public string? VisitId { get; init; }

    /// <summary>The levels from kills alone: <see cref="Levels"/> without a crawl's completion XP, which lands at the end.</summary>
    public double KillLevels { get; init; }

    /// <summary>
    /// The community job only: the loot in parts (motes, and fragments at 1×), so it can publish them apart and each app
    /// apply its own fragment value. <see cref="T5"/> and <see cref="HighTierT5"/> hold them combined.
    /// </summary>
    public LootParts? Loot { get; init; }

    /// <summary>The crawl ended with "completed".</summary>
    public bool Completed { get; init; }

    /// <summary>The visit is over: finished or left for good, not going or paused.</summary>
    public bool Ended { get; init; }

    /// <summary>The crawl's "You earned a refund" line was in the log.</summary>
    public bool Refunded { get; init; }

    /// <summary>A raid instance (" - Solo", " - Group").</summary>
    public bool Raid { get; init; }

    /// <summary>The boss slain in a raid, when the log showed one.</summary>
    public string? Boss { get; init; }

    public static LevelingSegment From(VisitStats v, int rank = 0, double partyShare = 0) => new(PlaceName(v.BaseZone, v.Difficulty), ZoneName(v.BaseZone),
        v.Difficulty, v.Start, v.LevelStart, v.ActiveMinutes, v.LevelsGained, v.T5, v.HighTierT5, v.Fragments, v.Kills, v.Deaths, v.Dps ?? 0, rank,
        v.Difficulty != null ? "crawl" : "instance", partyShare, v.Trio)
    { XpKills = v.XpKills, VisitId = v.Id, KillLevels = v.KillLevels, Completed = v.Completed, Refunded = v.Refunded, Ended = v.State is "finished" or "left", Raid = v.Raid, Boss = v.Boss };

    /// <summary>An open-world hunt (no damage is kept outside instances, so its damage per second is unknown).</summary>
    public static LevelingSegment OpenWorld(FieldStretch f, int rank, string? trio, double t5, double highTierT5, int fragments, int deaths) =>
        new(OpenPlace(f.Zone ?? "Unknown zone"), ZoneName(f.Zone ?? "Unknown zone"), null, f.Start, f.Level, f.Minutes, f.Levels, t5, highTierT5,
            fragments, f.Kills, deaths, 0, rank, "open world", f.Kills > 0 ? f.PartyKills / (double)f.Kills : 0, trio)
        { KillLevels = f.Levels, XpKills = f.XpKills ?? f.Kills };

    public static string ZoneName(string baseZone) => baseZone.StartsWith("The ", StringComparison.Ordinal) ? baseZone[4..] : baseZone;
    public static string PlaceName(string baseZone, int? difficulty) => ZoneName(baseZone) + (difficulty is { } d ? $" D{d}" : "");
    public static string OpenPlace(string zone) => ZoneName(zone) + " · open world";

    /// <summary>The XP a level needed then, against rank 0.</summary>
    public double XpMultiplier => Planner.XpMultiplier(Rank);

    /// <summary>Played in a group: most of its kills gave party experience.</summary>
    public bool Grouped => PartyShare >= 0.5;

    /// <summary>The segments a question's Where and With ask for.</summary>
    public static IEnumerable<LevelingSegment> Filter(IEnumerable<LevelingSegment> all, string? where, string? with) => all.Where(s =>
        (where is not ("instances" or "open") || (where == "open") == (s.Type == "open world")) &&
        (with is not ("solo" or "group") || (with == "group") == s.Grouped));
}

/// <summary>
/// Minutes per level at each level 1–49 in one place at rank 0 (null = never leveled there), the T5-eq per hour looted
/// while leveling there, how many visits covered each level, and when it was last played at each level. Recent visits
/// weigh more (<see cref="AskData.HalfLifeDays"/>).
/// </summary>
public sealed record PlaceLevels(string Place, double?[] Minutes, double[] T5PerHour, int[] Crawls, DateTime?[] Last)
{
    public double? At(int level) => level is >= 1 and <= 49 ? Minutes[level - 1] : null;
    public double T5At(int level) => level is >= 1 and <= 49 ? T5PerHour[level - 1] : 0;
    public int CrawlsAt(int level) => level is >= 1 and <= 49 ? Crawls[level - 1] : 0;
    public DateTime? LastAt(int level) => level is >= 1 and <= 49 ? Last[level - 1] : null;

    /// <summary>Per-level extra rates when present in the source (local logs or schema 2+ community files).</summary>
    public LevelExtras? Extras { get; init; }

    /// <summary>
    /// This place around a level: the levels within <paramref name="k"/> of it, nearer ones and better-covered ones counting
    /// more ((k + 1 − distance) × visits). Every Ask answer reads places this way (docs/ask-backtest.md: ±1 predicted a
    /// visit within 17% and ranked two places right 94% of the time, against 10-level bands' 19% and 86%). Null when none of
    /// those levels was played here. Zone gates are the caller's: this only knows the numbers.
    /// </summary>
    public LevelWindow? Window(int level, int k = AskData.WindowLevels)
    {
        double w = 0, m = 0, t5 = 0, hi = 0, fr = 0, de = 0, ki = 0, dw = 0, dps = 0, hours = 0;
        int visits = 0;
        DateTime? last = null;
        for (int j = Math.Max(1, level - k); j <= Math.Min(49, level + k); j++)
        {
            if (At(j) is not { } a || CrawlsAt(j) <= 0) continue;
            double wj = (k + 1 - Math.Abs(j - level)) * CrawlsAt(j);
            w += wj; m += wj * a; t5 += wj * T5At(j);
            visits = Math.Max(visits, CrawlsAt(j));
            if (LastAt(j) is { } l && (last == null || l > last)) last = l;
            if (Extras is not { } x) continue;
            hours += x.Hours[j - 1];
            hi += wj * x.HighTierPerHour[j - 1]; fr += wj * x.FragmentsPerHour[j - 1];
            de += wj * x.DeathsPerHour[j - 1]; ki += wj * x.KillsPerMinute[j - 1];
            if (x.Dps[j - 1] > 0) { dw += wj; dps += wj * x.Dps[j - 1]; }
        }
        if (w <= 0) return null;
        return new LevelWindow(m / w, t5 / w, visits, At(level) != null && CrawlsAt(level) > 0, Extras == null ? null : hours,
            hi / w, fr / w, de / w, ki / w, dw > 0 ? dps / dw : 0, last);
    }
}

/// <summary>Rates per level beyond minutes and motes (arrays of 49, level 1 first); <see cref="Hours"/> are plain totals.</summary>
public sealed record LevelExtras(double[] Hours, double[] HighTierPerHour, double[] FragmentsPerHour, double[] DeathsPerHour,
    double[] KillsPerMinute, double[] Dps);

/// <summary>
/// One place around one level (<see cref="PlaceLevels.Window"/>): minutes a level at rank 0 and the rates beside them.
/// <see cref="Visits"/> is the most visits behind any one of its levels; <see cref="Exact"/>, whether the level itself was
/// played; <see cref="Hours"/> is null when the source has no per-level extras.
/// </summary>
public sealed record LevelWindow(double Minutes, double T5PerHour, int Visits, bool Exact, double? Hours, double HighTierPerHour,
    double FragmentsPerHour, double DeathsPerHour, double KillsPerMinute, double Dps, DateTime? Last);

/// <summary>
/// Rates for one place in one level band (1-10, 11-20, 21-30, 31-40, 41-49, 50), all time or since the 11-10 patch.
/// Levels per hour are at rank 0; recent visits weigh more. <see cref="Crawls"/> and <see cref="Hours"/> are plain totals.
/// </summary>
public sealed record BandStat(string Band, string Place, bool SincePatch, int Crawls, double Hours, double LevelsPerHour, double T5PerHour,
    double HighTierPerHour, double FragmentsPerHour, double DeathsPerHour, double KillsPerMinute, double Dps, DateTime Last)
{
    /// <summary>For a row around one level (<see cref="AskData.Near"/>): that level itself was played, not only the ones beside it.</summary>
    public bool Exact { get; init; } = true;
}

/// <summary>One difficulty's rates (levels per hour at rank 0; recent visits weigh more).</summary>
public sealed record DifficultyColumn(int Difficulty, int Crawls, double LevelsPerHour, double T5PerHour, double DeathsPerHour, double KillsPerMinute, DateTime Last);

/// <summary>One zone's difficulties side by side in a five-level band (levels <see cref="From"/>–<see cref="To"/>).</summary>
public sealed record DifficultyCompare(string Zone, int From, int To, IReadOnlyList<DifficultyColumn> Columns)
{
    public string Key => $"{Zone}|{From}";
}

/// <summary>
/// What the Ask page answers from (spec "Questions the pool answers"): per place and level, per place and level band, and
/// difficulties side by side. Built from one player's logs today; the community file will have the same shape.
/// </summary>
public sealed class AskData
{
    public required IReadOnlyList<PlaceLevels> Places { get; init; }
    /// <summary>The same, from visits since the 11-10 patch only (empty for a community file, which keeps all time).</summary>
    public IReadOnlyList<PlaceLevels> PlacesSincePatch { get; init; } = [];
    public required IReadOnlyList<BandStat> Bands { get; init; }
    public required IReadOnlyList<DifficultyCompare> Difficulties { get; init; }
    public int Visits { get; init; }
    /// <summary>Every number already has enough players and visits behind it (a community file): no further minimum applies.</summary>
    public bool Published { get; init; }
    /// <summary>How much faster the player has become, when the visits were scaled to today's pace; else null.</summary>
    public PaceDrift? Drift { get; init; }
    /// <summary>Visits left out of the places and difficulties for having no rate (<c>onlyWithRate</c>).</summary>
    public int NoRate { get; init; }
    /// <summary>Those visits, with the level each started at, when and its type: a page counts the ones in its own level and scope (the farm answer's Left out).</summary>
    public IReadOnlyList<(int Level, DateTime Start, string Type)> UnratedAt { get; init; } = [];

    public static readonly string[] BandNames = ["1-10", "11-20", "21-30", "31-40", "41-49", "50"];

    public static string BandOf(int level) => level <= 10 ? "1-10" : level <= 20 ? "11-20" : level <= 30 ? "21-30" : level <= 40 ? "31-40" : level <= 49 ? "41-49" : "50";

    /// <summary>
    /// Exponential decay half-life in days for weighting older visits. A 30-day half-life balances
    /// recency bias against sample coverage (see docs/ask-backtest.md).
    /// </summary>
    public const double HalfLifeDays = 30;

    /// <param name="visits">Closed visits; ones under <see cref="Samples.MinActiveMinutes"/> active minutes are left out.</param>
    /// <param name="patch">Rates "since the patch" start here (fragments arrived 11-10).</param>
    /// <param name="now">Ages are measured from here (default: the newest visit).</param>
    /// <param name="minDifficulties">
    /// Difficulty comparisons need this many difficulties played in the zone and band. The community file uses 1 per
    /// player, so one player's D2 and another's D4 can be set side by side.
    /// </param>
    /// <param name="drift">Scale each visit's time to the player's pace now (<see cref="PaceDrift"/>).</param>
    public static AskData From(IEnumerable<LevelingSegment> visits, DateTime patch, DateTime? now = null, double halfLifeDays = HalfLifeDays,
        int minDifficulties = 2, bool drift = true, bool onlyWithRate = false)
    {
        // Visits below a zone's gate happened before it existed: they'd offer a place nobody can enter at that level now.
        var usable = Usable(visits);
        var at = now ?? (usable.Count > 0 ? usable.Max(v => v.Start) : DateTime.Now);
        double W(LevelingSegment v) => Math.Pow(0.5, Math.Max(0, (at - v.Start).TotalDays) / halfLifeDays);
        var paceDrift = drift ? PaceDrift.From(usable.Where(v => v.Start <= at)) : null;
        usable = Scaled(usable, paceDrift, at);

        // Rank only visits that have a valid rate (Rates.Has); track unrated visits separately so the UI can report exclusions.
        var ranked = onlyWithRate ? usable.Where(v => Rates.Has(v.Completed, v.Kills)).ToList() : usable;
        var unrated = onlyWithRate ? usable.Where(v => !Rates.Has(v.Completed, v.Kills)).ToList() : [];
        var places = Levels(ranked, W);
        var placesSincePatch = Levels(ranked.Where(v => v.Start >= patch), W);

        var placed = ranked.Where(v => v.LevelStart != null).ToList();   // every source: crawls, other instances, the open world
        var bands = new List<BandStat>();
        foreach (var since in new[] { false, true })
            bands.AddRange(placed.Where(v => !since || v.Start >= patch)
                .GroupBy(v => (Band: BandOf(v.LevelStart!.Value), v.Place))
                .Select(g => Stat(g.Key.Band, g.Key.Place, since, g.ToList(), W)));

        var diffs = ranked.Where(v => v.LevelStart != null).Where(v => v.Difficulty != null && v.LevelStart < 50)
            .GroupBy(v => (v.Zone, From: (v.LevelStart!.Value - 1) / 5 * 5 + 1))
            .Select(g => new DifficultyCompare(g.Key.Zone, g.Key.From, Math.Min(50, g.Key.From + 4),
                g.GroupBy(v => v.Difficulty!.Value).OrderBy(x => x.Key).Select(x =>
                {
                    double h = Math.Max(1.0 / 3600, x.Sum(v => W(v) * v.ActiveMinutes) / 60);
                    return new DifficultyColumn(x.Key, x.Count(), R(x.Sum(v => W(v) * v.Levels * v.XpMultiplier) / h), R(x.Sum(v => W(v) * v.T5) / h),
                        R(x.Sum(v => W(v) * v.Deaths) / h), R(x.Sum(v => W(v) * v.Kills) / (h * 60)), x.Max(v => v.Start));
                }).Where(c => c.LevelsPerHour > 0).ToList()))
            .Where(c => c.Columns.Count >= minDifficulties)
            .OrderBy(c => c.From).ThenBy(c => c.Zone, StringComparer.Ordinal).ToList();

        return new AskData
        {
            Places = places,
            PlacesSincePatch = placesSincePatch,
            Bands = bands,
            Difficulties = diffs,
            Visits = ranked.Count,
            Drift = paceDrift,
            NoRate = unrated.Count,
            UnratedAt = unrated.Where(v => v.LevelStart != null).Select(v => (v.LevelStart!.Value, v.Start, v.Type)).ToList()
        };
    }

    /// <summary>The visits Ask can use, one rule for <see cref="From"/> and <see cref="PlacesAsOf"/>: <see cref="Samples.MinActiveMinutes"/>, and not below a zone's gate.</summary>
    private static List<LevelingSegment> Usable(IEnumerable<LevelingSegment> visits) => visits.Where(IsUsable).ToList();

    private static bool IsUsable(LevelingSegment v) => v.ActiveMinutes >= Samples.MinActiveMinutes && (v.LevelStart is not { } ls || ZoneGates.Open(v.Place, ls));

    /// <summary>Whether a visit is in a count beside an Ask number: usable, and with a rate (<see cref="Rates.Has"/>). What the places, bands and sources count.</summary>
    public static bool Counts(LevelingSegment v) => IsUsable(v) && Rates.Has(v.Completed, v.Kills);

    /// <summary>Each visit's time scaled to the player's pace at <paramref name="at"/>; as it was when there is no drift.</summary>
    private static List<LevelingSegment> Scaled(IEnumerable<LevelingSegment> visits, PaceDrift? drift, DateTime at) =>
        drift == null ? visits.ToList() : visits.Select(v => v with { ActiveMinutes = v.ActiveMinutes * drift.Scale(v.Start, at), UnscaledMinutes = v.PlayedMinutes }).ToList();

    /// <summary>
    /// Place tables as of <paramref name="start"/>: only earlier visits, age-weighted from then and scaled by
    /// the pace drift at that time. Filtered by <paramref name="keep"/> so historical place checks stay cheap.
    /// </summary>
    public static AskData PlacesAsOf(IEnumerable<LevelingSegment> visits, DateTime start, Func<string, bool> keep)
    {
        var usable = Usable(visits.Where(v => v.Start < start));
        var paceDrift = PaceDrift.From(usable);
        double W(LevelingSegment v) => Math.Pow(0.5, Math.Max(0, (start - v.Start).TotalDays) / HalfLifeDays);
        var ranked = Scaled(usable.Where(v => keep(v.Place) && Rates.Has(v.Completed, v.Kills)), paceDrift, start);
        return new AskData { Places = Levels(ranked, W), Bands = [], Difficulties = [], Visits = usable.Count, Drift = paceDrift };
    }

    /// <summary>
    /// Each leveling visit's active minutes (at rank 0), loot, deaths, kills and damage, spread evenly over the levels it
    /// covered, weighted by age; then each place's rates level by level.
    /// </summary>
    private static List<PlaceLevels> Levels(IEnumerable<LevelingSegment> visits, Func<LevelingSegment, double> weight)
    {
        var cells = new Dictionary<string, Cells>();
        foreach (var v in visits.Where(v => v.LevelStart is >= 1 and < 50 && v.Levels > 0.05))
        {
            if (!cells.TryGetValue(v.Place, out var c)) cells[v.Place] = c = new Cells();
            double perLevel = v.ActiveMinutes / v.Levels, left = v.Levels, w = weight(v);
            for (int L = v.LevelStart!.Value; left > 1e-9 && L < 50; L++)
            {
                double part = Math.Min(1, left), share = w * part / v.Levels;
                c.Min0[L] += w * part * perLevel / v.XpMultiplier;
                c.MinA[L] += w * part * perLevel;
                c.Lv[L] += w * part;
                c.LvRaw[L] += part;
                c.RawMin[L] += part * v.PlayedMinutes / v.Levels;
                c.T5[L] += share * v.T5;
                c.High[L] += share * v.HighTierT5;
                c.Frag[L] += share * v.Fragments;
                c.Deaths[L] += share * v.Deaths;
                c.Kills[L] += share * v.Kills;
                if (v.Dps > 0) { c.DpsNum[L] += share * v.ActiveMinutes * v.Dps; c.DpsDen[L] += share * v.ActiveMinutes; }
                c.N[L]++;
                if (c.Last[L] is not { } last || v.Start > last) c.Last[L] = v.Start;
                left -= part;
            }
        }
        return cells.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv =>
        {
            var c = kv.Value;
            double[] Per(Func<int, double> f) => Enumerable.Range(1, 49).Select(f).ToArray();
            double PerHour(double[] x, int L) => c.MinA[L] > 0 ? Math.Round(x[L] / c.MinA[L] * 60, 2) : 0;
            return new PlaceLevels(kv.Key,
                Enumerable.Range(1, 49).Select(L => c.LvRaw[L] >= 0.3 && c.Lv[L] > 0 ? Math.Round(c.Min0[L] / c.Lv[L], 1) : (double?)null).ToArray(),
                Per(L => c.MinA[L] > 0 ? Math.Round(c.T5[L] / c.MinA[L] * 60, 1) : 0),
                Enumerable.Range(1, 49).Select(L => c.N[L]).ToArray(),
                Enumerable.Range(1, 49).Select(L => c.Last[L]).ToArray())
            {
                Extras = new LevelExtras(Per(L => Math.Round(c.RawMin[L] / 60, 3)), Per(L => PerHour(c.High, L)), Per(L => PerHour(c.Frag, L)),
                    Per(L => PerHour(c.Deaths, L)), Per(L => c.MinA[L] > 0 ? Math.Round(c.Kills[L] / c.MinA[L], 2) : 0),
                    Per(L => c.DpsDen[L] > 0 ? Math.Round(c.DpsNum[L] / c.DpsDen[L]) : 0)),
            };
        }).Where(p => p.Minutes.Any(m => m != null)).ToList();
    }

    /// <summary>Running sums for one place, indexed by level (0 unused).</summary>
    private sealed class Cells
    {
        public readonly double[] Min0 = new double[50], MinA = new double[50], Lv = new double[50], LvRaw = new double[50], RawMin = new double[50],
            T5 = new double[50], High = new double[50], Frag = new double[50], Deaths = new double[50], Kills = new double[50],
            DpsNum = new double[50], DpsDen = new double[50];
        public readonly int[] N = new int[50];
        public readonly DateTime?[] Last = new DateTime?[50];
    }

    /// <summary>Levels either side that a level's numbers draw on (<see cref="PlaceLevels.Window"/>; chosen by docs/ask-backtest.md).</summary>
    public const int WindowLevels = 1;

    /// <summary>The levels a window around <paramref name="level"/> spans, for labels ("39-41").</summary>
    public static string WindowOf(int level) => $"{Math.Max(1, level - WindowLevels)}-{Math.Min(49, level + WindowLevels)}";

    /// <summary>
    /// Every place open at a level (zone gates) with play within <see cref="WindowLevels"/> of it, as rows like a band's:
    /// what Mote farm, "Is my damage normal" and Every place rank below the cap. Rates are at rank 0; Visits is the most
    /// visits behind any one of the levels. A source without extras (an older community file) gives minutes and motes only.
    /// </summary>
    public List<BandStat> Near(int level, bool sincePatch) =>
        (sincePatch ? PlacesSincePatch : Places).Where(p => ZoneGates.Open(p.Place, level))
            .Select(p => (p, W: p.Window(level))).Where(x => x.W != null).Select(x =>
            {
                var w = x.W!;
                return new BandStat(WindowOf(level), x.p.Place, sincePatch, w.Visits, Math.Round(w.Hours ?? 0, 2), Math.Round(60 / w.Minutes, 2),
                    Math.Round(w.T5PerHour, 2), Math.Round(w.HighTierPerHour, 2), Math.Round(w.FragmentsPerHour, 2), Math.Round(w.DeathsPerHour, 2),
                    Math.Round(w.KillsPerMinute, 2), Math.Round(w.Dps), w.Last ?? DateTime.MinValue)
                { Exact = w.Exact };
            }).ToList();

    private static BandStat Stat(string band, string place, bool since, List<LevelingSegment> g, Func<LevelingSegment, double> w)
    {
        double h = Math.Max(1.0 / 3600, g.Sum(v => w(v) * v.ActiveMinutes) / 60);
        double Rate(Func<LevelingSegment, double> f) => R(g.Sum(v => w(v) * f(v)) / h);
        var dps = g.Select(v => v.Dps).Where(x => x > 0).Order().ToList();
        return new BandStat(band, place, since, g.Count, R(g.Sum(v => v.PlayedMinutes) / 60), Rate(v => v.Levels * v.XpMultiplier), Rate(v => v.T5),
            Rate(v => v.HighTierT5), Rate(v => v.Fragments), Rate(v => v.Deaths), R(g.Sum(v => w(v) * v.Kills) / (h * 60)),
            dps.Count > 0 ? Math.Round(dps[dps.Count / 2]) : 0, g.Max(v => v.Start));
    }

    private static double R(double x) => Math.Round(x, 2);
}

/// <summary>The answers: the quickest chain of places between two levels, and the rankings behind the other questions.</summary>
public static class Ask
{
    /// <summary>
    /// How levels/h changes a level at each place, in %, from the runs there (each re-figured to rank 0 for its XP penalty): from
    /// your own trio's runs once it has 3, else everyone's. Runs under 5 minutes or without kills don't count. Null under 3 runs or
    /// when they span under 3 levels. The same fit as the relevel line (<see cref="Fit"/>); the page draws it.
    /// </summary>
    public static Dictionary<string, double?> Falls(IReadOnlyList<LevelingSegment> bars, string? trio) =>
        bars.GroupBy(b => b.Place).ToDictionary(g => g.Key, g =>
        {
            var runs = g.Where(b => Math.Round(b.PlayedMinutes, 1) >= 5 && b.Kills > 0).ToList();
            var mine = string.IsNullOrEmpty(trio) ? [] : runs.Where(b => b.Trio == trio).ToList();
            var all = mine.Count >= 3 ? mine : runs;
            var pts = all.Select(b => (X: (double)b.LevelStart!.Value + Math.Round(b.Levels, 2) / 2,
                    Y: Math.Log((b.ActiveMinutes > 0 ? Math.Round(b.Levels * 60 / b.ActiveMinutes, 2) : 0) * b.XpMultiplier)))
                .Where(q => double.IsFinite(q.Y)).ToList();
            if (pts.Count < 3 || pts.Max(q => q.X) - pts.Min(q => q.X) < 3 || Fit.Slope(pts) is not { } slope) return (double?)null;
            return (Math.Exp(slope) - 1) * 100;
        });

    /// <summary>Minutes added for each change of place (zoning, travel, a new instance).</summary>
    public const double SwitchMinutes = 6;
    /// <summary>Minutes added per level where a place rests on a single visit, so one lucky crawl doesn't win alone.</summary>
    public const double ThinPenalty = 2;

    /// <param name="LastRun">The latest visit behind any of the leg's levels.</param>
    /// <param name="Mine">Your minutes for the leg alone (null unless your logs have every one of its levels).</param>
    /// <param name="Theirs">The players who share, alone (null without their numbers at every one of its levels).</param>
    /// <param name="MineShare">How much of <see cref="Minutes"/> is yours: 1 from your logs alone, 0 from theirs alone.</param>
    public sealed record Leg(string Place, int From, int To, double Minutes, int MaxCrawls, double T5PerHour, DateTime? LastRun,
        double? Mine = null, double? Theirs = null, double MineShare = 1)
    {
        public int Levels => To - From;
    }

    /// <summary>
    /// Prior weight (in equivalent local visits) for community data when blending with local history:
    /// 1 local visit gets 50% weight, 4 local visits get 80%, and unplayed places use community data alone (docs/ask-backtest.md).
    /// </summary>
    public const double PoolVisits = 1;

    /// <summary>Your part of a place's time when both have it: your visits against <see cref="PoolVisits"/>.</summary>
    public static double MineShare(int visits) => visits / (visits + PoolVisits);

    /// <summary>One level of a chain: the place, its minutes (yours and theirs together), and each side's own.</summary>
    private sealed record Step(int L, string P, double M, int N, double T5, DateTime? Last, double? Me, double? Them, double Z, double Cost);

    /// <param name="Gaps">Levels no place in the logs covers.</param>
    public sealed record Route(IReadOnlyList<Leg> Legs, double Minutes, double T5, IReadOnlyList<int> Gaps);

    public sealed record Alternative(string Place, double Minutes, bool Whole, int From, int To);

    /// <summary>
    /// The best place at each level from <paramref name="from"/> to <paramref name="to"/>, as one chain: a place change costs
    /// <see cref="SwitchMinutes"/>. Counting motes, a place gets credit for the farming time its motes save at
    /// <paramref name="farmRate"/> T5-eq/h (minutes × (1 − its T5-eq/h ÷ farm rate)), the same trade the Goal setting makes.
    /// Times are at rank 0 in the data and scaled here by <paramref name="xpMultiplier"/>, the XP the levels need for the
    /// trio being leveled. With <paramref name="pool"/> (the players who share), places only they have played join, and a
    /// place both have is read as yours and theirs together (<see cref="PoolVisits"/>).
    /// </summary>
    public static Route Chain(AskData d, int from, int to, bool countMotes, double farmRate, double xpMultiplier = 1, AskData? pool = null)
    {
        farmRate = Math.Max(1, farmRate);
        var mine = d.Places.GroupBy(p => p.Place).ToDictionary(g => g.Key, g => g.First());
        var theirs = pool?.Places.GroupBy(p => p.Place).ToDictionary(g => g.Key, g => g.First()) ?? [];
        var names = mine.Keys.Concat(theirs.Keys.Where(k => !mine.ContainsKey(k))).ToList();
        var states = new Dictionary<string, (double Cost, List<Step> Path)>();
        var gaps = new List<int>();
        for (int L = from; L < to; L++)
        {
            // Each place around this level (±1, nearer and better-covered levels counting more): a place played at the next
            // level but not this one still stands in, counted like a thin level, so one missing level doesn't decide the
            // whole route. (Relevels usually zone in at 2–4, so a single crawl that happened to start at level 1 forced every
            // 1–20 route to start there.) A number the players who share publish already rests on enough visits: not thin.
            var cands = names.Where(name => ZoneGates.Open(name, L)).Select(name =>
            {
                var p = mine.GetValueOrDefault(name);
                LevelWindow? a = p?.Window(L), b = theirs.GetValueOrDefault(name)?.Window(L);
                if (a == null && b == null) return null;
                double z = a == null ? 0 : b == null ? 1 : MineShare(a.Visits);
                double m = (z * (a?.Minutes ?? 0) + (1 - z) * (b?.Minutes ?? 0)) * xpMultiplier;
                double t5 = z * (a?.T5PerHour ?? 0) + (1 - z) * (b?.T5PerHour ?? 0);
                bool thin = b == null && (!a!.Exact || p!.CrawlsAt(L) < 2);
                double cost = (countMotes ? m * (1 - t5 / farmRate) : m) + (thin ? ThinPenalty : 0);
                return new Step(L, name, m, a is { Exact: true } ? p!.CrawlsAt(L) : 0, t5, a?.Last, a?.Minutes * xpMultiplier, b?.Minutes * xpMultiplier, z, cost);
            }).OfType<Step>().ToList();
            if (cands.Count == 0) { gaps.Add(L); continue; }
            var next = new Dictionary<string, (double Cost, List<Step> Path)>();
            foreach (var c in cands)
            {
                double best = states.Count == 0 ? 0 : double.PositiveInfinity;
                List<Step> path = [];
                foreach (var (p, s) in states)
                {
                    double cost = s.Cost + (p == c.P ? 0 : SwitchMinutes);
                    if (cost < best) { best = cost; path = s.Path; }
                }
                next[c.P] = (best + c.Cost, [.. path, c]);
            }
            states = next;
        }

        var legs = new List<(string P, int From, int To, double Min, int N, double T5Min, DateTime? Last, double? Me, double? Them, double ZMin)>();
        if (states.Count > 0)
            foreach (var s in states.Values.MinBy(s => s.Cost).Path)
            {
                if (legs.Count > 0 && legs[^1].P == s.P && legs[^1].To == s.L)
                {
                    var x = legs[^1];
                    legs[^1] = (s.P, x.From, s.L + 1, x.Min + s.M, Math.Max(x.N, s.N), x.T5Min + s.T5 * s.M, Later(x.Last, s.Last),
                        x.Me + s.Me, x.Them + s.Them, x.ZMin + s.Z * s.M);
                }
                else legs.Add((s.P, s.L, s.L + 1, s.M, s.N, s.T5 * s.M, s.Last, s.Me, s.Them, s.Z * s.M));
            }
        double minutes = legs.Sum(l => l.Min) + Math.Max(0, legs.Count - 1) * SwitchMinutes;
        double t5Total = legs.Sum(l => l.T5Min / 60);
        return new Route(legs.Select(l => new Leg(l.P, l.From, l.To, Math.Round(l.Min, 1), l.N, l.Min > 0 ? Math.Round(l.T5Min / l.Min, 1) : 0, l.Last,
                l.Me is { } me ? Math.Round(me, 1) : null, l.Them is { } them ? Math.Round(them, 1) : null, l.Min > 0 ? Math.Round(l.ZMin / l.Min, 2) : 1)).ToList(),
            Math.Round(minutes, 1), Math.Round(t5Total, 1), gaps);

        static DateTime? Later(DateTime? a, DateTime? b) => a is { } x && b is { } y ? (x > y ? x : y) : a ?? b;
    }

    /// <summary>
    /// You against the players who share at one place, over the levels you've both played (each read with the ones beside
    /// it, as the route reads them, and both played at the level itself).
    /// </summary>
    /// <param name="Mine">Your minutes a level there, on average over those levels, at the XP the trio needs; <paramref name="Theirs"/> likewise.</param>
    /// <param name="Visits">The most of your visits behind any one of those levels.</param>
    /// <param name="KillsMine">Kills per minute over those levels; both are 0 when either side lacks per-level extras.</param>
    public sealed record Versus(string Place, int From, int To, int Levels, double Mine, double Theirs, int Visits, double KillsMine, double KillsTheirs)
    {
        /// <summary>How much longer you take a level than they do (negative: quicker).</summary>
        public double Gap => Theirs > 0 ? Mine / Theirs - 1 : 0;

        /// <summary>How much slower you kill than they do, 0 when either side has no kills a minute.</summary>
        public double KillGap => KillsMine > 0 && KillsTheirs > 0 ? 1 - KillsMine / KillsTheirs : 0;

        /// <summary>How much less of a level each of your kills fills, 0 when either side has no kills a minute.</summary>
        public double PerKillGap => KillsMine > 0 && KillsTheirs > 0 && Mine > 0 ? 1 - Theirs * KillsTheirs / (Mine * KillsMine) : 0;

        /// <summary>Why you are slower: "both", "kill" (kill speed), "xp" (XP per kill) or "" (not clearly slower, or no kills to split by).</summary>
        public string Why => Gap < VersusNoise || KillsMine <= 0 || KillsTheirs <= 0 ? ""
            : KillGap >= VersusCause && PerKillGap >= VersusCause ? "both" : KillGap >= VersusCause ? "kill" : PerKillGap >= VersusCause ? "xp" : "";
    }

    /// <summary>A gap under this is noise: about the same time.</summary>
    public const double VersusNoise = 0.15;

    /// <summary>"warn" for a time that much longer than theirs, "good" for that much shorter, else "soft".</summary>
    public static string VersusTone(double gap) => gap >= VersusNoise ? "warn" : gap <= -VersusNoise ? "good" : "soft";
    /// <summary>A part of the gap under this is not named as a cause.</summary>
    public const double VersusCause = 0.05;

    /// <summary>The gap over every place, each weighted by its levels and averaged geometrically (a ratio of times).</summary>
    public static double VersusOverall(IReadOnlyList<Versus> v)
    {
        int levels = v.Sum(x => x.Levels);
        return levels == 0 ? 0 : Math.Exp(v.Sum(x => x.Levels * Math.Log(x.Mine / x.Theirs)) / levels) - 1;
    }

    /// <summary>
    /// Places between two levels with both local and community data, ordered slowest-vs-community first.
    /// Requires <see cref="MinCrawls"/> local visits. Comparison context only, never a pace verdict.
    /// </summary>
    public static List<Versus> Against(AskData d, AskData pool, int from, int to, double xpMultiplier = 1)
    {
        var theirs = pool.Places.GroupBy(p => p.Place).ToDictionary(g => g.Key, g => g.First());
        var list = new List<Versus>();
        foreach (var p in d.Places)
        {
            if (!theirs.TryGetValue(p.Place, out var q)) continue;
            double me = 0, them = 0, km = 0, kt = 0;
            int n = 0, visits = 0, lo = 0, hi = 0;
            bool kills = true;
            for (int L = from; L < to; L++)
            {
                if (!ZoneGates.Open(p.Place, L) || p.Window(L) is not { Exact: true } a || q.Window(L) is not { Exact: true } b) continue;
                me += a.Minutes; them += b.Minutes;
                km += a.KillsPerMinute * a.Minutes; kt += b.KillsPerMinute * b.Minutes;
                kills &= a.KillsPerMinute > 0 && b.KillsPerMinute > 0;
                visits = Math.Max(visits, a.Visits);
                if (n++ == 0) lo = L;
                hi = L;
            }
            if (n == 0 || visits < MinCrawls) continue;
            list.Add(new Versus(p.Place, lo, hi + 1, n, Math.Round(me / n * xpMultiplier, 1), Math.Round(them / n * xpMultiplier, 1), visits,
                kills ? Math.Round(km / me, 2) : 0, kills ? Math.Round(kt / them, 2) : 0));
        }
        return list.OrderByDescending(v => v.Gap).ThenBy(v => v.Place, StringComparer.Ordinal).ToList();
    }

    /// <summary>Other ways through the same levels: single places that cover them all (quickest first), then the ones that cover the most.</summary>
    public static List<Alternative> Alternatives(AskData d, int from, int to, Route chosen, int take = 3, double xpMultiplier = 1)
    {
        int span = to - from;
        var used = chosen.Legs.Select(l => l.Place).ToHashSet();
        var cover = d.Places.Select(p =>
        {
            var have = Enumerable.Range(from, span).Where(L => ZoneGates.Open(p.Place, L) && p.Window(L) != null).ToList();
            return (p.Place, Have: have, Minutes: have.Sum(L => p.Window(L)!.Minutes * xpMultiplier));
        }).Where(x => x.Have.Count > 0).ToList();
        var whole = cover.Where(x => x.Have.Count == span && !(chosen.Legs.Count == 1 && used.Contains(x.Place)))
            .OrderBy(x => x.Minutes).Take(take)
            .Select(x => new Alternative(x.Place, Math.Round(x.Minutes, 1), true, from, to)).ToList();
        whole.AddRange(cover.Where(x => x.Have.Count < span && !used.Contains(x.Place)).OrderByDescending(x => x.Have.Count)
            .Take(Math.Max(0, take - whole.Count))
            .Select(x => new Alternative(x.Place, Math.Round(x.Minutes, 1), false, x.Have.Min(), x.Have.Max() + 1)));
        return whole;
    }

    /// <summary>A rate needs at least this much behind it before it's ranked: one short visit says little.</summary>
    public const int MinCrawls = 2;
    public const double MinHours = 1.0 / 3;

    /// <summary>Whether a row can be ranked: enough of your own visits, or a community number (published only with enough).</summary>
    public static bool Enough(AskData d, BandStat b) => d.Published || b.Crawls >= MinCrawls && b.Hours >= MinHours;

    /// <summary>
    /// The rows a level is ranked from: every place around it (±1 level, <see cref="AskData.Near"/>) below the cap, the cap's
    /// own rows at it. Never a 10-level band: those compared one place's play at 21–25 with another's at 26–30.
    /// </summary>
    public static IEnumerable<BandStat> RowsAt(AskData d, int level, bool sincePatch) => level < 50
        ? d.Near(level, sincePatch)
        : d.Bands.Where(b => b.Band == AskData.BandOf(level) && b.SincePatch == sincePatch && ZoneGates.Open(b.Place, level));

    /// <summary>Mote farms at a level, most T5-eq per hour first (places that looted almost nothing, or rest on too little, are left out).</summary>
    public static List<BandStat> Farms(AskData d, int level, bool sincePatch) =>
        RowsAt(d, level, sincePatch).Where(b => b.T5PerHour >= 1 && Enough(d, b)).OrderByDescending(b => b.T5PerHour).ToList();

    /// <summary>Total crawls at a level (whether ranked as a farm or not), so views distinguish "too few yet" from "none".</summary>
    public static int FarmCrawls(AskData d, int level, bool sincePatch) => RowsAt(d, level, sincePatch).Sum(b => b.Crawls);

    /// <summary>Your damage per active second in each place at a level, highest first.</summary>
    public static List<BandStat> Damage(AskData d, int level) =>
        RowsAt(d, level, false).Where(b => b.Dps > 0 && b.T5PerHour >= 1 && Enough(d, b)).OrderByDescending(b => b.Dps).ToList();

    /// <summary>The difficulty comparison nearest a level (the player's own, by default).</summary>
    public static DifficultyCompare? NearestDifficulty(AskData d, int level) =>
        d.Difficulties.OrderBy(c => level >= c.From && level <= c.To ? 0 : Math.Min(Math.Abs(level - c.From), Math.Abs(level - c.To)))
            .ThenByDescending(c => c.Columns.Sum(x => x.Crawls)).FirstOrDefault();
}
