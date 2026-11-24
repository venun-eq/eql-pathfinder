namespace EqlPathfinder.Engine.Tracking;

/// <summary>
/// Single source of truth for rate eligibility and calculation: a visit has a rate once completed or at least
/// <see cref="MinKills"/> kills (no time minimum). Below threshold, rates are null ("—"), never 0.
/// </summary>
public static class Rates
{
    /// <summary>Kills that make a rate for a crawl that isn't finished. Pages read it as <c>rateFromKills</c>.</summary>
    public const int MinKills = 8;

    /// <summary>Whether what happened is enough for a rate: a finished crawl, or <see cref="MinKills"/> kills.</summary>
    public static bool Has(bool finished, int kills) => finished || kills >= MinKills;

    /// <summary>Too little to judge or show a rate yet: the opposite of <see cref="Has"/>.</summary>
    public static bool Early(bool finished, int kills) => !Has(finished, kills);

    /// <summary>The one division: <paramref name="x"/> over active minutes, an hour; null with no time.</summary>
    public static double? PerHour(double x, double activeMinutes) => activeMinutes > 0 ? x * 60 / activeMinutes : null;

    /// <summary>An hour's rate when the rule is met, else null.</summary>
    public static double? PerHour(double x, double activeMinutes, bool finished, int kills) => Has(finished, kills) ? PerHour(x, activeMinutes) : null;

    /// <summary>A minute's rate (kills a minute) when the rule is met, else null.</summary>
    public static double? PerMinute(double x, double activeMinutes, bool finished, int kills) => PerHour(x, activeMinutes, finished, kills) / 60;

    /// <summary>A second's rate (DPS, over active time) when the rule is met, else null.</summary>
    public static double? PerSecond(double x, double activeMinutes, bool finished, int kills) => PerHour(x, activeMinutes, finished, kills) / 3600;

    /// <summary>The one XP per kill, % of a level: kill levels over the kills whose XP the log sizes (completion XP is never in it); 0 for kills of no-XP mobs, null with none to divide.</summary>
    public static double? XpPerKill(double killLevels, int xpKills) => xpKills > 0 ? 100 * killLevels / xpKills : null;
}

/// <summary>
/// Minimum sample thresholds for baselines, tables, and farm rates. A visit counts when it has a rate (<see cref="Rates.Has"/>)
/// and at least <see cref="MinActiveMinutes"/> of active time; a local farm rate requires <see cref="MinFarmCrawls"/> crawls.
/// </summary>
public static class Samples
{
    /// <summary>Active minutes under which a visit is a pass-through, not play to learn a rate from.</summary>
    public const double MinActiveMinutes = 3;

    /// <summary>Crawls with a rate at the cap, since the patch, that make a farm rate (and a best zone's median): fewer is one lucky or unlucky crawl.</summary>
    public const int MinFarmCrawls = 3;

    /// <summary>Active minutes a visit needs to be fitted to a slope (pace drift, relevel paces): under it, the first kills' noise swamps the pace the fit reads. Higher than <see cref="MinActiveMinutes"/> because a fit weighs each visit by its own rate, a table only counts it.</summary>
    public const double MinFitMinutes = 5;
}
