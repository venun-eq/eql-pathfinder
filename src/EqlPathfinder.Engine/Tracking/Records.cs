using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Tracking;

/// <summary>Counts of motes and fragments by tier (index = tier number 1..10), with the Reward Chest share.</summary>
public sealed class LootTally
{
    public int[] Motes { get; set; } = new int[11];
    public int[] Fragments { get; set; } = new int[11];
    public int[] ChestMotes { get; set; } = new int[11];
    public int[] ChestFragments { get; set; } = new int[11];

    public void Add(LootKind kind, MoteTier tier, int n, bool chest)
    {
        int i = (int)tier;
        if (kind == LootKind.Fragment) { Fragments[i] += n; if (chest) ChestFragments[i] += n; }
        else { Motes[i] += n; if (chest) ChestMotes[i] += n; }
    }

    public int MoteCount => Motes.Sum();
    public int FragmentCount => Fragments.Sum();

    public double MoteT5 => Sum(Motes);
    /// <summary>Fragments valued as plain same-tier motes, before the fragment multiplier.</summary>
    public double FragmentBaseT5 => Sum(Fragments);
    public double ChestMoteT5 => Sum(ChestMotes);
    public double ChestFragmentBaseT5 => Sum(ChestFragments);

    public double T5(double fragmentValue) => MoteT5 + FragmentBaseT5 * fragmentValue;
    public double ChestT5(double fragmentValue) => ChestMoteT5 + ChestFragmentBaseT5 * fragmentValue;

    /// <summary>T5-eq from drops above T5 only (Greater and up).</summary>
    public double HighTierT5(double fragmentValue)
    {
        double s = 0;
        for (int i = 1; i <= 10; i++)
            if (MoteTiers.IsHighTier((MoteTier)i))
                s += Valuation.T5((MoteTier)i, Motes[i], false, fragmentValue) + Valuation.T5((MoteTier)i, Fragments[i], true, fragmentValue);
        return s;
    }

    private static double Sum(int[] counts)
    {
        double s = 0;
        for (int i = 1; i < counts.Length; i++) s += Valuation.T5((MoteTier)i, counts[i], false, 1);
        return s;
    }

    public void AddFrom(LootTally o)
    {
        for (int i = 0; i < 11; i++)
        {
            Motes[i] += o.Motes[i]; Fragments[i] += o.Fragments[i];
            ChestMotes[i] += o.ChestMotes[i]; ChestFragments[i] += o.ChestFragments[i];
        }
    }
}

/// <summary>A drop above T5, as looted (before any condensing at Constructed Potential).</summary>
public sealed class HighTierDrop
{
    public DateTime Time { get; set; }
    public LootKind Kind { get; set; }
    public MoteTier Tier { get; set; }
    public int Count { get; set; }
    public string Source { get; set; } = "";
    public bool FromChest { get; set; }
    public string? VisitId { get; set; }
    public string? Zone { get; set; }
    public double BaseT5 => Valuation.T5(Tier, Count, false, 1);
}

public sealed class DamageTally
{
    public long Total { get; set; }
    public long Hits { get; set; }
    public long Crits { get; set; }
    public Dictionary<string, long> BySource { get; set; } = new();
    public Dictionary<string, long> ByAbility { get; set; } = new();
    /// <summary>Seconds with damage flowing: consecutive hits under the fight gap are joined.</summary>
    public double FightSeconds { get; set; }
    public DateTime? LastAt { get; set; }
    public long Taken { get; set; }
}

/// <summary>A Dungeon Crawl reward inside a visit. The log gives no amount: it is sized when its level dings.</summary>
public sealed class VisitReward
{
    public string Id { get; set; } = "";
    public DateTime Time { get; set; }
    /// <summary>Percent of a level, summed over every level it touched.</summary>
    public double Share { get; set; }
    /// <summary>Still in the open level: its size is known only at the next ding.</summary>
    public bool Sizing { get; set; } = true;
    /// <summary>Shared a level with another reward, so the split is an even estimate.</summary>
    public bool Estimated { get; set; }
    /// <summary>The level's starting point was unknown (start of log, loadout swap), so the size can't be worked out.</summary>
    public bool Unknown { get; set; }
}

/// <summary>
/// One visit to an instance: from zone-in until the Dungeon Crawl completes (crawls) or you leave
/// (normal instances). Leaving and coming back within the resume window continues the same visit;
/// time outside is not counted.
/// </summary>
public sealed class Visit
{
    public string Id { get; set; } = "";
    public string Zone { get; set; } = "";
    public string BaseZone { get; set; } = "";
    public int? Difficulty { get; set; }
    public string? DifficultyLabel { get; set; }

    public DateTime Start { get; set; }
    public DateTime? SegmentStart { get; set; }
    public DateTime? PausedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>Kills after completion (finishing off stragglers) extend the active time to here.</summary>
    public DateTime? ActiveUntil { get; set; }
    public DateTime? ExitedAt { get; set; }
    public DateTime? LastActivity { get; set; }
    public double ActiveSeconds { get; set; }
    public bool Completed { get; set; }
    public bool Refunded { get; set; }
    public bool Closed { get; set; }
    /// <summary>Minutes between leaving the previous instance and entering this one.</summary>
    public double? ResetMinutes { get; set; }

    public string? Trio { get; set; }
    public string? Race { get; set; }
    /// <summary>A /who during the visit set the trio; before that it's carried over and may be stale.</summary>
    public bool TrioConfirmed { get; set; }
    /// <summary>The loadout slot it was played on, when the game's files named one (current-loadout).</summary>
    public int? Slot { get; set; }
    /// <summary>"slot": the trio is the slot's last /who, not this visit's. "unmet": played on a slot no /who has named, so no trio. Null: as before.</summary>
    public string? TrioFrom { get; set; }
    /// <summary>The trio is a /who's, or the slot's last one: a swap inside the visit ends its levels, and spells don't overwrite it.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool TrioKnown => TrioConfirmed || TrioFrom == "slot";
    public int? LevelStart { get; set; }
    public int? LevelEnd { get; set; }

    public int Kills { get; set; }
    /// <summary>Kills whose XP line had no (x%): they gave XP the log doesn't size, so XP per kill leaves them out (kills-at-cap).</summary>
    public int UnknownXpKills { get; set; }
    /// <summary>Kills whose XP line had a percent: the bar moved for them.</summary>
    public int PercentKills { get; set; }
    /// <summary>The boss slain in a raid instance (a known raid boss only), for the Crawls verdict.</summary>
    public string? BossKilled { get; set; }
    public double KillPercent { get; set; }
    /// <summary>The percent of XP lines read at the level cap: the bar filling with no level to come, kept as the log says it and left out of <see cref="KillPercent"/> (xp-at-cap).</summary>
    public double CapPercent { get; set; }
    /// <summary>Kills whose XP line was read at the cap: left out of XP per kill's divisor, since their percent is not levels.</summary>
    public int CapKills { get; set; }
    public int Deaths { get; set; }
    public List<VisitReward> Rewards { get; set; } = new();
    public List<DateTime> KillTimes { get; set; } = new();
    public List<double> KillSeconds { get; set; } = new();

    public LootTally Loot { get; set; } = new();
    public List<HighTierDrop> HighTier { get; set; } = new();
    public DamageTally Damage { get; set; } = new();
    /// <summary>Spells you began casting here, by name (rank stripped), and how often: which classes were playing.</summary>
    public Dictionary<string, int> Casts { get; set; } = new();

    // ---- derived numbers (computed, not stored state) ----

    public bool IsCrawlish => Difficulty.HasValue;
    public double RewardPercent => Rewards.Sum(r => r.Share);
    /// <summary>Kills whose XP the log sizes: where XP per kill divides.</summary>
    public int XpKills => Kills - UnknownXpKills - CapKills;

    /// <summary>
    /// True when filed below the cap but actually played on a cap-level loadout (XP lines had no percent).
    /// Excluded from relevels and sharing; visits of only no-XP mobs are unaffected.
    /// </summary>
    public bool PlayedOnACapLoadout(int levelCap) => CapLoadout.Played(LevelStart, levelCap, UnknownXpKills, PercentKills);
    public bool RewardSizing => Rewards.Any(r => r.Sizing);
    /// <summary>Levels gained: kill XP plus reward XP sized so far.</summary>
    public double LevelsGained => (KillPercent + RewardPercent) / 100.0;

    public double CrawlSeconds(DateTime now)
    {
        double s = ActiveSeconds;
        if (SegmentStart is { } ss && !Completed) s += Math.Max(0, (now - ss).TotalSeconds);
        return Math.Max(0, s);
    }

    /// <summary>
    /// Idle time beyond the threshold across zone-in to first kill, between kills, and last kill to visit end
    /// (excluding out-of-instance <see cref="Pauses"/> so breaks aren't double-subtracted). Returns 0 when a visit has no kills.
    /// </summary>
    public double IdleSeconds(double thresholdSeconds, DateTime now)
    {
        if (KillTimes.Count == 0) return 0;
        double S(DateTime t) => (t - Start).TotalSeconds;
        return Math.Min(ActiveTime.IdleSeconds(KillTimes.Select(S).ToList(), S(IdleEnd(now)),
            Pauses.Select(p => (S(p.From), S(p.To))).ToList(), thresholdSeconds), CrawlSeconds(now));
    }

    /// <summary>Where the last idle gap ends: completion (or the last straggler), leaving, or now while it's going.</summary>
    public DateTime IdleEnd(DateTime now) =>
        Completed ? ActiveUntil ?? CompletedAt ?? now : SegmentStart != null ? now : PausedAt ?? ExitedAt ?? KillTimes.LastOrDefault(Start);

    /// <summary>When you were out of the instance mid-visit (a death, an errand) before coming back to it.</summary>
    public List<PauseSpan> Pauses { get; set; } = new();

    public double? MedianKillSeconds
    {
        get
        {
            if (KillSeconds.Count == 0) return null;
            var a = KillSeconds.Order().ToArray();
            int n = a.Length;
            return n % 2 == 1 ? a[n / 2] : (a[n / 2 - 1] + a[n / 2]) / 2.0;
        }
    }

    public DateTime EndTime => ActiveUntil ?? CompletedAt ?? ExitedAt ?? PausedAt ?? LastActivity ?? Start;
}

public sealed record PauseSpan(DateTime From, DateTime To);

public sealed record KillRecord(
    DateTime Time, string? VisitId, string? Zone, int? Difficulty, int? Level,
    string? Mob, double Percent, bool Party, double? KillSeconds, bool XpUnknown = false);

public sealed record LootRecord(
    DateTime Time, string? VisitId, string? Zone, LootKind Kind, MoteTier Tier, int Count,
    string Source, bool FromChest, bool Spent);

public sealed record LevelRecord(DateTime Time, string Kind, int? Level, string? Detail);

/// <summary>Shared cap-loadout detection helper for visits and open-world hunts.</summary>
public static class CapLoadout
{
    /// <summary>Below the cap every kill moves the bar, so a run whose kills had XP lines and none with a percent was played on a loadout at the cap.</summary>
    public static bool Played(int? level, int levelCap, int unknownXpKills, int percentKills) => level < levelCap && unknownXpKills > 0 && percentKills == 0;
}
