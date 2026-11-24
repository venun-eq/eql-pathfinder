namespace EqlPathfinder.Engine.Tracking;

public sealed class TrackerOptions
{
    /// <summary>
    /// Fragment value in same-tier motes (default 3.56 for +9 gear, 2.0 for spells; see <see cref="Model.Potential.FragmentValue"/>
    /// and docs/motes-and-fragments.md).
    /// </summary>
    public double FragmentValue { get; set; } = Model.Potential.RecommendedFragmentValue;
    /// <summary>Re-entering the same unfinished instance within this window continues the visit.</summary>
    public double ResumeMinutes { get; set; } = 10;
    /// <summary>The difficulties you farm for motes (Settings → How you play): visits there are judged by T5-eq an hour, the rest as leveling.</summary>
    public IReadOnlyCollection<int> FarmDifficulties { get; set; } = Analysis.VisitKinds.DefaultFarmed;
    /// <summary>
    /// Gaps longer than this (zone-in to first kill, between kills, last kill to end) count as idle beyond the threshold.
    /// Default 3 minutes filters real breaks while keeping normal medding, buffing, and travel active.
    /// </summary>
    public double IdleThresholdSeconds { get; set; } = 180;
    /// <summary>No log lines for this long while inside an instance ends the visit (game closed, server restart).</summary>
    public double SilentLogMinutes { get; set; } = 30;
    /// <summary>Hits closer together than this belong to one fight.</summary>
    public double FightGapSeconds { get; set; } = 10;
    /// <summary>A gap since the previous instance longer than this is a break, not a reset.</summary>
    public double ResetCapMinutes { get; set; } = 30;
    /// <summary>best: your best farming zone since the 11-10 patch; usual: the median of all that farming; manual: FarmRateManual.</summary>
    public string FarmRateMode { get; set; } = "best";
    /// <summary>The rate you typed; also the fallback until the logs have three farming crawls since the patch.</summary>
    public double FarmRateManual { get; set; } = DefaultFarmRate;
    /// <summary>The farm rate before you set one or farm enough (T5-eq/h). Pages call it the default when it's in use.</summary>
    public const double DefaultFarmRate = 50;
    /// <summary>What you optimize for: balanced (levels against motes), speed (levels/h), motes (T5-eq/h) or rare (above-T5 drops).</summary>
    public string Goal { get; set; } = "balanced";
    /// <summary>Level cap (50 now; Kunark raises it to 55).</summary>
    public int LevelCap { get; set; } = 50;
    /// <summary>Your priorities for the next trio (the app's Plan page).</summary>
    public Analysis.PlanPrefs Plan { get; set; } = Analysis.PlanPrefs.Default;
    /// <summary>
    /// Reincarnation counts you entered for the tracked character, by <see cref="Roster.Key"/>: what the log can't know
    /// (reincarnations from before logging was on). Reincarnations the log sees after each entry add to it.
    /// </summary>
    public Dictionary<string, EnteredCount> EnteredCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>"I've reincarnated this Count times so far", said at AsOf.</summary>
public sealed record EnteredCount(int Count, DateTime AsOf);

/// <summary>One class or race: how many times it has reincarnated and where it is now.</summary>
public sealed class Incarnation
{
    public string Name { get; set; } = "";
    /// <summary>Reincarnations so far: the log's, or your entered count plus the log's since you entered it.</summary>
    public int Rank { get; set; }
    /// <summary>When the log showed each reincarnation.</summary>
    public List<DateTime> Seen { get; set; } = new();
    /// <summary>
    /// The rank the log last stated, and when: a second or later reincarnation says it ("You have improved Class
    /// Reincarnation: Shadow Knight 2"). 0 until such a line; a first ("gained the ability") states none.
    /// </summary>
    public int LoggedRank { get; set; }
    public DateTime? LoggedAt { get; set; }
    public DateTime? Reincarnated { get; set; }
    /// <summary>Highest level seen since its last reincarnation (null until a ding or /who with it active).</summary>
    public int? Level { get; set; }
    public DateTime? ReachedCap { get; set; }
    public DateTime? LastActive { get; set; }
}

public sealed class Roster
{
    /// <summary>By class abbreviation (WAR, CLR, ...).</summary>
    public Dictionary<string, Incarnation> Classes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>By race name.</summary>
    public Dictionary<string, Incarnation> Races { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Incarnation Class(string abbrev) =>
        Classes.TryGetValue(abbrev, out var i) ? i : Classes[abbrev] = new Incarnation { Name = abbrev };

    public Incarnation Race(string name) =>
        Races.TryGetValue(name, out var i) ? i : Races[name] = new Incarnation { Name = name };

    /// <summary>"class:WAR" or "race:Ogre": how entered counts are keyed.</summary>
    public static string Key(bool race, string name) => (race ? "race:" : "class:") + name;

    /// <summary>
    /// The rank: what the log last stated outright when it's newer than your entry (or there's no entry); otherwise the
    /// count you entered plus what the log saw after you entered it; without either, what the log saw.
    /// </summary>
    public static int Effective(Incarnation i, EnteredCount? entered)
    {
        if (entered == null) return Math.Max(i.LoggedRank, i.Seen.Count);
        if (i.LoggedAt is { } at && at > entered.AsOf) return i.LoggedRank;
        return Math.Max(0, entered.Count) + i.Seen.Count(t => t > entered.AsOf);
    }

    /// <summary>Sets every rank from the log and your entered counts. Safe to repeat.</summary>
    public void Apply(IReadOnlyDictionary<string, EnteredCount>? entered)
    {
        if (entered != null)
            foreach (var key in entered.Keys)
            {
                if (key.StartsWith("race:", StringComparison.OrdinalIgnoreCase)) Race(key[5..]);
                else if (key.StartsWith("class:", StringComparison.OrdinalIgnoreCase)) Class(key[6..]);
            }
        foreach (var (name, i) in Classes) i.Rank = Effective(i, entered?.GetValueOrDefault(Key(false, name)));
        foreach (var (name, i) in Races) i.Rank = Effective(i, entered?.GetValueOrDefault(Key(true, name)));
    }
}

/// <summary>
/// Everything the tracker needs to continue where it stopped. Serialized into the checkpoint so a
/// restart never re-reads the whole log.
/// </summary>
public sealed class EngineState
{
    /// <summary>
    /// Bump whenever the engine starts tracking something new from lines it has already read
    /// (2 = reincarnation roster, 3 = when each reincarnation happened, 4 = spells cast per visit, 5 = charmed pets,
    /// 6 = D0 instances, which enter without a difficulty in their name, 7 = second and later reincarnations, "You have
    /// improved Class Reincarnation: X 2", 8 = time outside the instance mid-visit, so idle time can leave it out, 9 = XP
    /// "with a bonus", which the parser missed, and the bonus-XP events it marks). A
    /// checkpoint from an older version is rebuilt from the logs.
    /// </summary>
    public const int CurrentVersion = 16;   // 16 = a /who and the loadout state it fills (the slot on, each slot met), read from logs (current-loadout); 15 = a way back into a D0 instance goes through its open world, and 1-point hits are read (visit-boundaries); 14 = a kill at the cap is never an XP kill, and a kill's level is its XP line's (numbers-check-cap); 13 = XP lines at the cap are not levels (xp-at-cap); 12 = a kill is its slain line, not its XP line (kills-at-cap); 11 = a raid's boss kill is kept (judge-by-play); 10 = a crawl's levels belong to the loadout that played it (report #8)
    public int Version { get; set; } = CurrentVersion;
    public string Character { get; set; } = "";
    /// <summary>The first line in the logs: nothing before it can be known.</summary>
    public DateTime? FirstTime { get; set; }
    public DateTime? LastTime { get; set; }

    // Level and reward sizing
    public int? Level { get; set; }
    public bool LevelPositionKnown { get; set; }
    public double KillPercentInLevel { get; set; }
    /// <summary>Rewards that landed in the open level: (visit id, reward id).</summary>
    public List<RewardRef> OpenLevelRewards { get; set; } = new();
    public RewardRef? LastReward { get; set; }
    public DateTime? LastRewardTime { get; set; }

    // Character
    public List<string>? Classes { get; set; }
    public string? Race { get; set; }
    public HashSet<string> Pets { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Your charmed pet, if any, as the parser left it (so a restart mid-charm keeps it).</summary>
    public EqlPathfinder.Engine.Parsing.CharmState Charm { get; set; } = new();
    public int Reincarnations { get; set; }
    public Roster Roster { get; set; } = new();

    /// <summary>
    /// When XP came "with a bonus" (a server event): Ask and the pace checks leave play from then out, since a bonus makes
    /// a place look faster than it is (<see cref="Analysis.PlayFacts.DuringBonus"/>). Kept as the log says.
    /// </summary>
    public List<BonusXpSpan> BonusXp { get; set; } = new();

    /// <summary>A bonus-XP line at <paramref name="t"/>: it extends the latest span when it comes within an hour of it.</summary>
    public void NoteBonusXp(DateTime t)
    {
        if (BonusXp.Count > 0 && BonusXp[^1] is { } last && t >= last.Start && t - last.End <= TimeSpan.FromHours(1)) { if (t > last.End) last.End = t; return; }
        BonusXp.Add(new BonusXpSpan { Start = t, End = t });
    }

    // Zones and visits
    public string? Zone { get; set; }
    public Visit? Current { get; set; }
    public DateTime? LastVisitExit { get; set; }
    public bool NewInstanceSinceEntry { get; set; }
    /// <summary>The instance just created or offered: D0 enters under the plain zone name (<see cref="InstanceWatch"/>).</summary>
    public EqlPathfinder.Engine.Parsing.InstanceWatch Instance { get; set; } = new();
    /// <summary>Closed visits whose crawl reward hasn't been sized yet.</summary>
    public Dictionary<string, Visit> AwaitingReward { get; set; } = new();

    // Kill attribution (kills-at-cap): a kill is its slain line. The game writes the XP line first, then the slain line, in the
    // same second (older clients the other way round), so each waits a second for the other. An XP line nothing pairs with is
    // a quest or task reward; a slain line nothing pairs with is a kill that gave no XP.
    public List<PendingKill> PendingXp { get; set; } = new();
    public List<PendingSlain> PendingSlains { get; set; } = new();
    public Dictionary<string, DateTime> Engaged { get; set; } = new(StringComparer.Ordinal);

    // Mote bank: everything looted and offered at Construct Potential since tracking began
    public LootTally Looted { get; set; } = new();
    public LootTally Offered { get; set; } = new();
    public int VoidTouchedReceived { get; set; }

    public string? Trio => Classes is { Count: > 0 } ? string.Join("/", Classes) : null;

    /// <summary>
    /// The loadout slot on and how its trio is known (current-loadout); null while no slot file has named one. Kept in step
    /// with <see cref="Classes"/>, <see cref="Race"/> and <see cref="Level"/>, which are the slot's while it is on.
    /// </summary>
    public LoadoutOn? Loadout { get; set; }
    /// <summary>What each slot's last /who named (and its level since), by slot number. Worked out from the swaps, so a re-read rebuilds it.</summary>
    public Dictionary<int, SlotMemory> Slots { get; set; } = new();
    /// <summary>Each swap and class change, for the fight meter's timeline: from when, which slot, and the trio then (null: not known).</summary>
    public List<LoadoutStint> Stints { get; set; } = new();
    /// <summary>The last /who seen, so a swap read late (an app start reads the log before the files) can take it.</summary>
    public SlotMemory? LastWho { get; set; }
    public DateTime? LastWhoTime { get; set; }
    /// <summary>Highest <c>loadout_events</c> row ID already ingested by the engine (idempotent cursor).</summary>
    public long LoadoutRow { get; set; }
}

/// <summary>What one loadout slot is known to be: the classes and race its last /who named, and its level since.</summary>
public sealed class SlotMemory
{
    public List<string>? Classes { get; set; }
    public string? Race { get; set; }
    public int? Level { get; set; }
    /// <summary>A /who has named this slot.</summary>
    public bool Known { get; set; }
    /// <summary>A class changed in it since that /who.</summary>
    public bool ClassesChanged { get; set; }
    /// <summary>When the /who that last named it was typed: one after a swap away from it was meant for the slot swapped to.</summary>
    public DateTime? WhoAt { get; set; }
}

/// <summary>A stretch on one loadout: from a swap or class change, until the next. A null trio is "not known".</summary>
public sealed record LoadoutStint(DateTime From, int Slot, string? Trio);

/// <summary>
/// The loadout slot on (1 to 9, as the game's loadout window numbers them) and <paramref name="State"/>: "known" (a /who
/// named it, now or before a swap back to it), "new" (no /who has named it) or "classes" (a class changed in it since).
/// </summary>
public sealed record LoadoutOn(int Slot, string State)
{
    /// <summary>The one rule for "a loadout not met, or whose classes changed": nothing is judged by another loadout's usual.</summary>
    public static bool IsNotKnown(LoadoutOn? l) => l is { State: "new" or "classes" };
}

public sealed record RewardRef(string VisitId, string RewardId);

/// <summary>A slain line that counts as a kill and waits a second for its XP line.</summary>
public sealed class PendingSlain
{
    public DateTime Time { get; set; }
    public string Mob { get; set; } = "";
    public double? Seconds { get; set; }
    /// <summary>Another player's kill outside an instance: yours only with party XP in its second.</summary>
    public bool NeedsParty { get; set; }
    public string? VisitId { get; set; }
    public string? Zone { get; set; }
    public int? Difficulty { get; set; }
    public int? Level { get; set; }
}

/// <summary>An XP line waiting a second for a slain line.</summary>
public sealed class PendingKill
{
    public DateTime Time { get; set; }
    public double Percent { get; set; }
    /// <summary>The line had no (x%): the XP isn't sized.</summary>
    public bool Unknown { get; set; }
    public bool Party { get; set; }
    public string? VisitId { get; set; }
    public string? Zone { get; set; }
    public int? Difficulty { get; set; }
    public int? Level { get; set; }
}

/// <summary>A stretch of time the log gave XP "with a bonus": from the first such line to the last, gaps under an hour joined.</summary>
public sealed class BonusXpSpan
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}
