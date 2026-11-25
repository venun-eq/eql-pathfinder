using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>One zone and difficulty as a leveling or farming choice, from your own crawls.</summary>
public sealed record RouteOption(
    string BaseZone, int? Difficulty, int Crawls, double LevelsPerHour, double T5PerHour, double KillsPerMinute,
    double? Cost, IReadOnlyList<string> Trios, int LevelLow, int LevelHigh, double HighTierPerHour = 0)
{
    public string Label => (BaseZone.StartsWith("The ") ? BaseZone[4..] : BaseZone) + (Difficulty is { } d ? $" D{d}" : "");
}

public sealed record LevelBand(int Low, int High)
{
    public string Label => Low == High ? $"{Low}" : $"{Low}–{High}";
}

/// <summary>
/// In-progress visit evaluation using the same judges as History: <see cref="Run"/> for leveling, <see cref="Farm"/>
/// for farming and grouped play, each with its usual, band, and overlay ticks.
/// </summary>
/// <param name="Kind">How the visit is judged (<see cref="VisitKinds"/>).</param>
/// <param name="Run">Leveling: kill speed and XP per kill like for like (<see cref="RunPace"/>).</param>
/// <param name="Place">Leveling: another place clearly better at this level, or null.</param>
/// <param name="Farm">A farming or grouped visit's T5-eq an hour against your usual (<see cref="FarmPace"/>).</param>
/// <param name="Boss">The boss slain in a raid so far.</param>
public sealed record PaceCheck(string? Kind, RunPace? Run, PlaceCheck? Place, FarmPace? Farm, string? Boss);

/// <summary>
/// Your farming at the cap, before and after the 11-10 patch (reincarnation arrived and Bard DDD was nerfed, so earlier
/// rates don't compare). Only crawls at the player's farming difficulties count (Difficulties, the setting when it was worked out);
/// NotFarmed is the crawls at the cap since the patch at any other difficulty. Crawls counts the crawls with a rate (the ones the middle is taken from); Unrated the crawls played without one. Best is your best zone's median among zones with at least three crawls.
/// </summary>
public sealed record FarmRateInfo(
    DateTime Patch, int Crawls, double? Usual, double? Best, string? BestZone, int BestCrawls,
    int PreCrawls, double? PreUsual, double? PreBest, string? PreBestZone, int Unrated = 0,
    IReadOnlyList<int>? Difficulties = null, int NotFarmed = 0);

/// <summary>The farm rate in use and where it comes from (<see cref="RouteAnalysis.FarmSource"/>).</summary>
/// <param name="Kind">best (your best farm: <paramref name="Zone"/>), usual (the middle of your farming), set (the rate you
/// set in Settings) or default (the app's, with no farming of your own and no rate set).</param>
/// <param name="Crawls">Farming crawls at the cap since the patch behind it: the best zone's for best, all of them otherwise.</param>
/// <param name="Fallback">Best or usual was picked, but there isn't enough farming since the patch yet.</param>
/// <param name="Unrated">Farming crawls since the patch with no rate yet (8 kills, or finished), so a page can say "none has a rate yet".</param>
/// <param name="Needs">Crawls with a rate the logs need before they set the rate (<see cref="Samples.MinFarmCrawls"/>).</param>
public sealed record FarmRateSource(string Kind, double Rate, string? Zone, int Crawls, bool Fallback, int Unrated = 0, int Needs = Samples.MinFarmCrawls,
    IReadOnlyList<int>? Difficulties = null, int NotFarmed = 0);

public static class RouteAnalysis
{
    public static IReadOnlyList<LevelBand> Bands(int cap) =>
        [new(1, 10), new(11, 19), new(20, 25), new(26, 33), new(34, 39), new(40, 45), new(46, cap - 1)];

    public static LevelBand BandFor(int level, int cap) =>
        Bands(cap).FirstOrDefault(b => level >= b.Low && level <= b.High) ?? new LevelBand(cap, cap);

    /// <summary>Visits worth learning from: a difficulty instance with real play in it.</summary>
    public static bool Usable(Visit v) => Usable(v, 50);

    /// <summary>
    /// At the cap a farming visit counts with 5 kills, or when something was looted.
    /// </summary>
    public static bool Usable(Visit v, int cap) =>
        v.Difficulty != null && v.Closed && v.CrawlSeconds(v.EndTime) >= 300 &&
        (v.Kills >= 5 || (v.LevelStart >= cap && v.Loot.MoteCount + v.Loot.FragmentCount > 0)) && !PreNerfDdd(v);

    /// <summary>The Bard song the 11-10 patch nerfed (it now lands on the same target only once every 2 ticks), any rank.</summary>
    public static bool IsDdd(string ability) =>
        ability.StartsWith("Denon's Desperate Dirge", StringComparison.OrdinalIgnoreCase) ||
        ability.StartsWith("Denon`s Desperate Dirge", StringComparison.OrdinalIgnoreCase);

    /// <summary>The share of a visit's damage that came from DDD.</summary>
    public static double DddShare(Visit v) =>
        v.Damage.Total <= 0 ? 0 : (double)v.Damage.ByAbility.Where(kv => IsDdd(kv.Key)).Sum(kv => kv.Value) / v.Damage.Total;

    /// <summary>
    /// True for a pre-patch visit where Bard DDD accounted for at least half of total damage.
    /// Excluded from routes, farm rates, and pace baselines because those pre-nerf rates are no longer attainable;
    /// post-patch DDD counts normally.
    /// </summary>
    public static bool PreNerfDdd(Visit v) => v.Start < FarmPatch && DddShare(v) >= 0.5;

    /// <summary>When reincarnation began (XP rules and chests changed with the 11-10 patch); earlier leveling isn't comparable.</summary>
    public static DateTime? EraStart(EngineState s)
    {
        var times = s.Roster.Classes.Values.Concat(s.Roster.Races.Values).Select(i => i.Reincarnated).Where(t => t != null);
        return times.Any() ? times.Min() : null;
    }

    /// <summary>
    /// The Nov 10, 2026 patch (reincarnation launch &amp; Bard DDD nerf; servers returned 2:50 PM EST).
    /// Compared in local wall-clock time because EverQuest logs record local timestamps.
    /// </summary>
    public static DateTime FarmPatch => FarmPatchIn(TimeZoneInfo.Local);

    /// <summary>The patch's instant (11-10 19:50 UTC) as a wall-clock time in <paramref name="zone"/>.</summary>
    public static DateTime FarmPatchIn(TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(new DateTime(2026, 11, 10, 19, 50, 0, DateTimeKind.Utc), zone);

    /// <summary>Leveling options for a level band, best first by your goal (default: cost per level, lowest first).</summary>
    public static List<RouteOption> LevelingOptions(IEnumerable<Visit> visits, int low, int high, TrackerOptions o, DateTime? since,
        double farmRate, string? trioKey = null)
    {
        var pool = visits.Where(v => Usable(v, o.CapOn(v)) && (since == null || v.Start >= since) &&
                                     v.LevelStart is { } ls && ls >= low && ls <= high && ls < o.CapOn(v) && v.LevelsGained > 0 &&
                                     (trioKey == null || (v.Trio != null && ClassNames.TrioKey(v.Trio) == trioKey)));
        return Rank(Group(pool, o, leveling: true, farmRate), o.Goal, leveling: true);
    }

    /// <summary>Farming options at the cap, best first: T5-eq per hour, or drops above T5 when that's your goal.</summary>
    public static List<RouteOption> FarmingOptions(IEnumerable<Visit> visits, TrackerOptions o, DateTime? since) =>
        Rank(Group(visits.Where(v => Usable(v, o.CapOn(v)) && (since == null || v.Start >= since) && v.LevelStart >= o.CapOn(v)), o, leveling: false, farmRate: 0), // farming rows carry no cost, so no rate
            o.Goal, leveling: false);

    /// <summary>
    /// Orders options by a goal: balanced = cost per level (levels against the motes you'd farm instead), speed = levels per
    /// hour, motes = T5-eq per hour, rare = T5-eq per hour from drops above T5. Ties go to the option with more crawls.
    /// </summary>
    public static List<RouteOption> Rank(IEnumerable<RouteOption> options, string goal, bool leveling)
    {
        var o = goal switch
        {
            "speed" when leveling => options.OrderByDescending(x => x.LevelsPerHour),
            "rare" => options.OrderByDescending(x => x.HighTierPerHour).ThenByDescending(x => x.T5PerHour),
            "motes" => options.OrderByDescending(x => x.T5PerHour),
            _ when leveling => options.OrderBy(x => x.Cost ?? double.MaxValue),
            _ => options.OrderByDescending(x => x.T5PerHour),
        };
        return o.ThenByDescending(x => x.Crawls).ToList();
    }

    private static List<RouteOption> Group(IEnumerable<Visit> pool, TrackerOptions o, bool leveling, double farmRate)
    {
        var list = new List<RouteOption>();
        foreach (var g in pool.GroupBy(v => (v.BaseZone, v.Difficulty)))
        {
            var stats = g.Select(v => VisitStats.From(v, v.EndTime, o)).Where(s => s.ActiveMinutes >= 3).ToList();
            if (stats.Count == 0) continue;
            double lvh = Median(stats.Select(s => s.LevelsPerHour));
            double t5h = Median(stats.Select(s => s.T5PerHour));
            double kpm = Median(stats.Select(s => s.KillsPerMinute));
            double hth = Median(stats.Select(s => Rates.PerHour(s.HighTierT5, s.ActiveMinutes)));
            double? cost = leveling ? CostPerLevel(farmRate, t5h, lvh) : null;
            var trios = g.Where(v => v.Trio != null).Select(v => v.Trio!).Distinct().ToList();
            var levels = g.Where(v => v.LevelStart != null).Select(v => v.LevelStart!.Value).ToList();
            list.Add(new RouteOption(g.Key.BaseZone, g.Key.Difficulty, stats.Count, lvh, t5h, kpm, cost, trios,
                levels.Count > 0 ? levels.Min() : 0, levels.Count > 0 ? levels.Max() : 0, hth));
        }
        return list;
    }

    /// <summary>Your farming rates at the cap from the logs, since the 11-10 patch and before it.</summary>
    public static FarmRateInfo FarmRates(IEnumerable<Visit> visits, TrackerOptions o, TimeZoneInfo? zone = null)
    {
        var atCap = visits.Where(v => Usable(v, o.CapOn(v)) && v.LevelStart >= o.CapOn(v)).ToList();
        var farmed = o.FarmDifficulties.ToHashSet();
        var farm = atCap.Where(v => v.Difficulty is { } d && farmed.Contains(d)).ToList();   // your farming, not every crawl at the cap: a quick D0 run isn't a farm
        (int n, int unrated, double? usual, double? best, string? zone, int bestN) Sum(IEnumerable<Visit> vs)
        {
            var stats = vs.Select(v => (v, s: VisitStats.From(v, v.EndTime, o))).Where(x => x.s.ActiveMinutes >= Samples.MinActiveMinutes).ToList();
            var rated = stats.Where(x => x.s.T5PerHour != null).ToList();   // a crawl with no rate is counted apart, never in the middle or its count
            if (rated.Count == 0) return (0, stats.Count, null, null, null, 0);
            var top = rated.GroupBy(x => (x.v.BaseZone, x.v.Difficulty)).Where(g => g.Count() >= Samples.MinFarmCrawls)
                .Select(g => (label: (g.Key.BaseZone.StartsWith("The ") ? g.Key.BaseZone[4..] : g.Key.BaseZone) + (g.Key.Difficulty is { } d ? $" D{d}" : ""),
                              med: Median(g.Select(x => x.s.T5PerHour)), n: g.Count()))
                .OrderByDescending(z => z.med).FirstOrDefault();
            return (rated.Count, stats.Count - rated.Count, Median(rated.Select(x => x.s.T5PerHour)), top.label != null ? top.med : null, top.label, top.n);
        }
        var patch = FarmPatchIn(zone ?? TimeZoneInfo.Local);
        var post = Sum(farm.Where(v => v.Start >= patch));
        var pre = Sum(farm.Where(v => v.Start < patch));
        int notFarmed = atCap.Where(v => v.Start >= patch && !(v.Difficulty is { } d && farmed.Contains(d))).Count(v => VisitStats.From(v, v.EndTime, o).ActiveMinutes >= Samples.MinActiveMinutes);
        return new FarmRateInfo(patch, post.n, post.usual, post.best, post.zone, post.bestN, pre.n, pre.usual, pre.best, pre.zone, post.unrated, farmed.Order().ToList(), notFarmed);
    }

    /// <summary>The farm rate to weigh levels against: from the logs (needs three farming crawls since the patch), else the one you typed. Worked out here from its source and handed to every reader as an argument; nothing stores it.</summary>
    public static double EffectiveFarmRate(FarmRateInfo info, TrackerOptions o) => FarmSource(info, o).Rate;

    /// <summary>
    /// The farm rate in use and where it comes from, so every page can say so: your best farm or your usual (from your
    /// crawls at the cap since the patch), the rate you set in Settings, or the app's default when you have neither.
    /// Without three farming crawls since the patch, best and usual fall back to the typed rate; that rate is "set" when
    /// it isn't the default (typing one switches to manual, so a typed rate survives switching back).
    /// </summary>
    public static FarmRateSource FarmSource(FarmRateInfo info, TrackerOptions o)
    {
        if (o.FarmRateMode == "manual") return new("set", o.FarmRateManual, null, 0, false, info.Unrated, Samples.MinFarmCrawls, info.Difficulties, info.NotFarmed);
        if (o.FarmRateMode != "usual" && info.Best is { } best) return new("best", best, info.BestZone, info.BestCrawls, false, info.Unrated, Samples.MinFarmCrawls, info.Difficulties, info.NotFarmed);
        if (info.Crawls >= Samples.MinFarmCrawls && info.Usual is { } usual) return new("usual", usual, null, info.Crawls, false, info.Unrated, Samples.MinFarmCrawls, info.Difficulties, info.NotFarmed);
        bool typed = Math.Abs(o.FarmRateManual - TrackerOptions.DefaultFarmRate) > 0.001;
        return new(typed ? "set" : "default", o.FarmRateManual, null, info.Crawls, true, info.Unrated, Samples.MinFarmCrawls, info.Difficulties, info.NotFarmed);
    }

    /// <summary>Levels cost against the motes you would farm instead: (farm rate - T5-eq an hour) over levels an hour; null when leveling is too slow to say. The one formula and the one guard: the overlay, Where next and the Ask page all call it. Levels an hour is already for the XP your trio needs.</summary>
    public static double? CostPerLevel(double farmRate, double t5PerHour, double levelsPerHour) =>
        levelsPerHour > 0.05 ? (farmRate - t5PerHour) / levelsPerHour : null;

    /// <summary>The median of the rates that exist: a visit with no rate (<see cref="Rates"/>) is left out, not counted as 0.</summary>
    public static double Median(IEnumerable<double?> xs) => Median(xs.Where(x => x != null).Select(x => x!.Value));

    public static double Median(IEnumerable<double> xs)
    {
        var a = xs.Where(x => !double.IsNaN(x)).Order().ToArray();
        if (a.Length == 0) return 0;
        return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2;
    }
}
