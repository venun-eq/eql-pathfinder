using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Tracking;

/// <summary>The numbers shown for one visit. Rates use active minutes: time inside minus idle.</summary>
public sealed record VisitStats(
    string Id, DateTime Start, string Zone, string BaseZone, int? Difficulty, string? Trio, string? Race,
    int? LevelStart, int? LevelEnd, string State,
    double Minutes, double IdleMinutes, double ActiveMinutes, double? ResetMinutes,
    double KillLevels, double RewardLevels, bool RewardSizing, bool RewardEstimated, double? LevelsPerHour,
    double T5, double MoteT5, double FragmentT5, double ChestT5, double HighTierT5, double? T5PerHour, double? MotesOnlyPerHour,
    int Motes, int Fragments, string Tiers,
    int Kills, double? KillsPerMinute, double? MedianKillSeconds, int Deaths,
    long Damage, double? Dps, double FightDps, IReadOnlyList<KeyValuePair<string, long>> TopAbilities,
    bool Completed, bool Refunded, DateTime End)
{
    public double LevelsGained => KillLevels + RewardLevels;

    /// <summary>No rate, and not a raid or a cap visit: what "Hide visits too short to judge" hides (the page reads this, it does not recompute it).</summary>
    public bool TooShort { get; init; }
    /// <summary>The loadout slot the visit was played on, when the game's files named one; History's trio tooltip says "loadout 6" from it.</summary>
    public int? Slot { get; init; }

    /// <summary>How the trio is known: "who", "spells", "partial", "carried" or "none" (see <see cref="Analysis.TrioCall"/>).</summary>
    public string? TrioSource { get; init; }

    /// <summary>Leveling crawls, History: kill speed against your usual, "pace", "slow", "fast" or "none" (<see cref="Analysis.RunPace"/>).</summary>
    public string? Pace { get; init; }
    /// <summary>This crawl's kills a minute over your usual there (1.0 = the same).</summary>
    public double? KillSpeed { get; init; }
    /// <summary>"normal", "low" or "high": XP per kill against what the place gives at that level, at that XP penalty.</summary>
    public string? XpVerdict { get; init; }
    /// <summary>"fading" or "slower" when another place was clearly better at that level (<see cref="Analysis.PlaceCheck"/>).</summary>
    public string? PlaceVerdict { get; init; }
    public string? BetterPlace { get; init; }

    /// <summary>A raid instance (" - Solo", " - Group" in its name): judged by its boss, not a pace.</summary>
    public bool Raid => BaseZone.EndsWith(" - Solo", StringComparison.Ordinal) || BaseZone.EndsWith(" - Group", StringComparison.Ordinal);
    /// <summary>Kills whose XP the log sizes (a kill whose XP line had no percent is left out): where XP per kill divides.</summary>
    public int XpKills { get; init; }
    /// <summary>This visit's kill XP per kill (<see cref="Rates.XpPerKill"/>): what the overlay shows.</summary>
    public double? XpPerKill => Rates.XpPerKill(KillLevels, XpKills);
    /// <summary>The kills' share of Levels/h, as the equation reads it: kills a minute x 60 x XP per kill / 100. Null unless every kill's XP is known and both rates exist.</summary>
    public double? KillLevelsPerHour => LevelsPerHour != null && XpKills == Kills && KillsPerMinute is { } kpm && XpPerKill is { } xpk ? kpm * 60 * xpk / 100 : null;
    public bool AtCap { get; init; }
    /// <summary>The boss slain in a raid, from the log.</summary>
    public string? Boss { get; init; }
    /// <summary>"raid", "grouped", "farming" or "leveling": how this visit is judged (<see cref="Analysis.VisitKinds"/>); null when not judged.</summary>
    public string? Kind { get; init; }
    /// <summary>Farming and grouped: "more", "usual", "less", "none" or "early" against your usual T5-eq/h of the same kind here (<see cref="Analysis.FarmPace"/>).</summary>
    public string? FarmVerdict { get; init; }
    /// <summary>How far above (+) or below (-) your usual T5-eq/h, as a fraction.</summary>
    public double? FarmPct { get; init; }
    public double? FarmUsual { get; init; }
    public int FarmRuns { get; init; }
    /// <summary>What the usual is of: "crawls you finished, solo", "instances you didn't finish" or "grouped".</summary>
    public string? FarmBasis { get; init; }
    /// <summary>The levels that usual is made of ("levels 38–42"), below the cap; null at the cap.</summary>
    public string? FarmLevels { get; init; }
    /// <summary>The band that verdict used (a visit's normal swing of T5-eq an hour here).</summary>
    public double? FarmBand { get; init; }
    /// <summary>The whole stop this visit is in (consecutive visits at one place judged by motes), judged as one against the same usual; null for a leveling or raid visit.</summary>
    public Analysis.FarmPace? StopFarm { get; init; }
    /// <summary>The stretch of History this visit is in (<see cref="Analysis.Stretches"/>).</summary>
    public int Stretch { get; init; }
    /// <summary>The clock a visit took, zone-in to zone-out (to now while inside): Active + Idle + After + Outside, so the parts add up to it.</summary>
    public double WholeMinutes { get; init; }
    /// <summary>Inside after the crawl completed with no kill to count (looting, walking out); 0 for a raid, which never completes.</summary>
    public double AfterMinutes { get; init; }
    /// <summary>Out of the instance mid-visit (a death, an errand): the visit's pauses.</summary>
    public double OutsideMinutes { get; init; }
    /// <summary>When the visit zoned out; null while it is still inside.</summary>
    public DateTime? ZonedOut { get; init; }
    /// <summary>Kill speed over the whole stop: consecutive crawls at one place and difficulty in one stretch (<see cref="Analysis.KillSpeeds"/>).</summary>
    public Analysis.KillPace? StopPace { get; init; }

    public static VisitStats From(Visit v, DateTime now, TrackerOptions o, bool isCurrent = false)
    {
        double secs = v.CrawlSeconds(now);
        double min = secs / 60;
        double idle = v.IdleSeconds(o.IdleThresholdSeconds, now) / 60;
        double active = ActiveTime.Minutes(v, o.IdleThresholdSeconds, now);
        var clock = Clock(v, now, min, idle, active);
        double fv = o.FragmentValue;
        // One rule for every rate (Rates): a finished crawl or 8 kills, else no rate. Never 0, never a 1-minute floor.
        double? Hourly(double x) => Rates.PerHour(x, active, v.Completed, v.Kills);
        double t5 = v.Loot.T5(fv);
        string state = v.Completed ? "finished"
            : v.Closed ? "left"
            : v.SegmentStart != null ? "in progress"
            : "paused";
        var top = v.Damage.ByAbility.OrderByDescending(x => x.Value).Take(6).ToList();
        var s = new VisitStats(
            v.Id, v.Start, v.Zone, v.BaseZone, v.Difficulty, v.Trio, v.Race, v.LevelStart, v.LevelEnd, state,
            min, idle, active, v.ResetMinutes,
            v.KillPercent / 100, v.RewardPercent / 100, v.RewardSizing, v.Rewards.Any(r => r.Estimated || r.Unknown),
            Hourly(v.LevelsGained),
            t5, v.Loot.MoteT5, v.Loot.FragmentBaseT5 * fv, v.Loot.ChestT5(fv), v.Loot.HighTierT5(fv),
            Hourly(t5), Hourly(v.Loot.MoteT5),
            v.Loot.MoteCount, v.Loot.FragmentCount, FormatTiers(v.Loot),
            v.Kills, Rates.PerMinute(v.Kills, active, v.Completed, v.Kills), v.MedianKillSeconds, v.Deaths,
            v.Damage.Total, Rates.PerSecond(v.Damage.Total, active, v.Completed, v.Kills),
            v.Damage.FightSeconds > 0 ? v.Damage.Total / v.Damage.FightSeconds : 0, top,
            v.Completed, v.Refunded, v.EndTime)
        {
            Boss = v.BossKilled,
            XpKills = v.XpKills,
            WholeMinutes = clock.Whole,
            AfterMinutes = clock.After,
            OutsideMinutes = clock.Outside,
            ZonedOut = clock.ZonedOut,
            AtCap = v.LevelStart >= o.LevelCap,
        };
        // Controls "Hide visits too short to judge": true when below rate threshold (except raids, judged by boss).
        return s with { TooShort = Rates.Early(v.Completed, v.Kills) && !s.Raid, Slot = v.Slot };
    }

    /// <summary>
    /// The visit's whole clock and where it went: inside (active and idle), outside on its pauses, and what is left after the crawl
    /// completed. After is the remainder floored at 0 and the whole is the sum of the four, so the parts always add up to it.
    /// </summary>
    private static (double Whole, double After, double Outside, DateTime? ZonedOut) Clock(Visit v, DateTime now, double inside, double idle, double active)
    {
        bool open = !v.Closed && v.ExitedAt == null;
        var end = open ? now : v.ExitedAt ?? v.EndTime;
        double outside = v.Pauses.Sum(p => Math.Max(0, (Min(p.To, end) - Max(p.From, v.Start)).TotalMinutes));
        double after = Math.Max(0, (end - v.Start).TotalMinutes - outside - inside);
        return (active + idle + after + outside, after, outside, open ? null : end);
    }
    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    /// <summary>"Gr 1  Maj 6  frag Maj 4" — highest tier first.</summary>
    public static string FormatTiers(LootTally t)
    {
        var parts = new List<string>();
        for (int i = 10; i >= 1; i--) if (t.Motes[i] > 0) parts.Add($"{MoteTiers.Abbrev((MoteTier)i)} {t.Motes[i]}");
        for (int i = 10; i >= 1; i--) if (t.Fragments[i] > 0) parts.Add($"frag {MoteTiers.Abbrev((MoteTier)i)} {t.Fragments[i]}");
        return string.Join("  ", parts);
    }
}

/// <summary>A run of visits to the same zone and difficulty with short gaps: "tonight's Kedge D4".</summary>
public sealed record SessionStats(
    string? Zone, int? Difficulty, int Visits, double T5, double MoteT5, double CrawlMinutes, double WallMinutes,
    double Levels, double? T5PerHourCrawl, double? T5PerHourWall, double? LevelsPerHourCrawl, double? LevelsPerHourWall,
    int Kills, int HighTierDrops, bool Finished = false)
{
    /// <summary>True when the session has visits but not yet a completed crawl or enough kills for a rate (<see cref="Rates"/>).</summary>
    public bool Early => Visits > 0 && Rates.Early(Finished, Kills);

    /// <summary><see cref="Rates.MinKills"/>, for the page to say when the rates start.</summary>
    public int RateFromKills => Rates.MinKills;

    public static SessionStats From(IReadOnlyList<Visit> visits, DateTime now, TrackerOptions o)
    {
        if (visits.Count == 0) return new(null, null, 0, 0, 0, 0, 0, 0, null, null, null, null, 0, 0);
        double t5 = 0, mt5 = 0, crawl = 0, lv = 0;
        int kills = 0, hi = 0;
        bool finished = false;
        DateTime first = visits.Min(v => v.Start), last = DateTime.MinValue;
        foreach (var v in visits)
        {
            t5 += v.Loot.T5(o.FragmentValue);
            mt5 += v.Loot.MoteT5;
            crawl += ActiveTime.Minutes(v, o.IdleThresholdSeconds, now);
            finished |= v.Completed;
            lv += v.LevelsGained;
            kills += v.Kills;
            hi += v.HighTier.Sum(h => h.Count);
            var end = !v.Closed && v.SegmentStart != null ? now : v.EndTime;
            if (end > last) last = end;
        }
        double wall = Math.Max(0, (last - first).TotalMinutes);
        // Both rates follow the one rule over the session (Rates); the wall-clock one divides by time between crawls too.
        double? Rate(double x, double min) => Rates.PerHour(x, min, finished, kills);
        var z = visits[^1];
        return new(z.BaseZone, z.Difficulty, visits.Count, t5, mt5, crawl, wall, lv,
            Rate(t5, crawl), Rate(t5, wall), Rate(lv, crawl), Rate(lv, wall), kills, hi, finished);
    }

    /// <summary>Visits ending the list that share its zone and difficulty, each starting within the gap of the previous end.</summary>
    public static List<Visit> CurrentSession(IReadOnlyList<Visit> chronological, double gapMinutes = 30)
    {
        var result = new List<Visit>();
        for (int i = chronological.Count - 1; i >= 0; i--)
        {
            var v = chronological[i];
            if (result.Count > 0)
            {
                var next = result[0];
                if (v.BaseZone != next.BaseZone || v.Difficulty != next.Difficulty) break;
                if ((next.Start - v.EndTime).TotalMinutes > gapMinutes) break;
            }
            result.Insert(0, v);
        }
        return result;
    }
}
