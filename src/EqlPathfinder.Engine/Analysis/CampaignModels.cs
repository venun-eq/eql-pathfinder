using System.Text.Json.Serialization;
using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>One loadout in a campaign: a race and three classes, reincarnated <see cref="Times"/> times.</summary>
public sealed class CampaignLoadout
{
    public string Race { get; set; } = "";
    public List<string> Classes { get; set; } = [];
    /// <summary>How many times you reincarnate it: 1 to 3 (a race's passive has 3 ranks).</summary>
    public int Times { get; set; } = 3;
    /// <summary>Kept as it is when the plan is rebuilt.</summary>
    public bool Pinned { get; set; }
    /// <summary>Runs you ticked off yourself: reincarnations while the log was off.</summary>
    public int Marked { get; set; }

    public CampaignLoadout Copy() => new() { Race = Race, Classes = [.. Classes], Times = Times, Pinned = Pinned, Marked = Marked };
}

/// <summary>
/// A player-defined combo rule preserved across rebuilds: two or three classes together or apart.
/// Must, Never, and Unlock-now trios are enforced above the cheapest path; Prefer is kept only where free.
/// </summary>
public sealed class CampaignCombo
{
    public List<string> Classes { get; set; } = [];
    /// <summary>Apart (a Never): no run has two of them. Held like a Must.</summary>
    public bool Apart { get; set; }
    /// <summary>A pair: every run that earns their ranks (true) or at least once (false).</summary>
    public bool EveryRun { get; set; } = true;
    /// <summary>A trio: how many runs of it, 1 to 3.</summary>
    public int Times { get; set; } = 3;
    /// <summary>Held above the cheapest path, at its price. False: a Prefer, kept only where free.</summary>
    public bool Must { get; set; }
    /// <summary>A trio's runs back to back. Held like a Must.</summary>
    public bool UnlockNow { get; set; }

    /// <summary>Held above the cheapest path (a Must, a Never or an Unlock-now trio), not just kept where free.</summary>
    public bool IsHeld() => Apart || Must || UnlockNow;
    public bool Same(CampaignCombo o) => Apart == o.Apart && Classes.Count == o.Classes.Count && Classes.All(o.Classes.Contains);
    public CampaignCombo Copy() => new() { Classes = [.. Classes], Apart = Apart, EveryRun = EveryRun, Times = Times, Must = Must, UnlockNow = UnlockNow };
}

/// <summary>What stands in a combo's way: one of the plan's combos (its index) or a pinned loadout (its index).</summary>
/// <param name="Class">For two Musts: the class both need runs of, and <paramref name="Runs"/> how many it has left to earn its ranks.</param>
public sealed record CampaignClash(int? Combo, int? Loadout, string? Class = null, int? Runs = null);

/// <summary>A run whose XP to level a combo changes (<see cref="CampaignComboPrice.XpRuns"/>).</summary>
public sealed record CampaignRunXp(int N, double With, double Without);

/// <summary>When something lands with a combo and without it: What is "completionist:2" or "classes". Only what moves.</summary>
public sealed record CampaignLanding(string What, int? With, int? Without);

/// <summary>What a combo does to the plan, against the plan without it (the form's "What it does", the row's price line).</summary>
/// <param name="Runs">The plan's run numbers it's held in.</param>
/// <param name="Kept">Of <paramref name="Of"/> runs it asks for, how many it's held in.</param>
/// <param name="AsMust">For a Prefer: what it would cost as a Must. Null for a Must or a Never.</param>
/// <param name="PastGoals">Classes it takes past their goals.</param>
/// <param name="Other">A trio's price in the other placement (Spread out and Unlock now), itself with no Other or Clash.</param>
/// <param name="XpRuns">The runs whose XP to level differs with the combo.</param>
/// <param name="Lands">When Completionist's ranks and the class goals land, only those that move.</param>
/// <param name="PastKept">Of the <paramref name="PastOf"/> runs after its classes are 3/3, how many it sits in.</param>
/// <param name="AllPast">Every class of it is 3/3 already: it can only be kept where free.</param>
/// <param name="Same">The index of an equal combo already in the plan.</param>
/// <param name="After">The Unlock-now trio this one follows.</param>
/// <param name="Full">The plan has <see cref="CampaignPlan.MaxCombos"/> combos already.</param>
/// <param name="Free">No more runs, no more XP to level in all, and no class or Completionist rank later (XP moved between runs is free).</param>
/// <param name="KeptRuns">The runs to go it's held in that count toward <paramref name="Kept"/> (<paramref name="Runs"/> has every run it sits in).</param>
/// <param name="RunsWith">The runs to go with the combo, and <paramref name="RunsWithout"/> without it.</param>
/// <param name="AsMustXp">For a Prefer: the runs whose XP to level differs as a Must (With) against the plan now (Without).</param>
public sealed record CampaignComboPrice(CampaignDelta Delta, IReadOnlyList<int> Runs, int Kept, int Of, CampaignDelta? AsMust,
    IReadOnlyList<string> PastGoals, CampaignClash? Clash, CampaignComboPrice? Other = null, IReadOnlyList<CampaignRunXp>? XpRuns = null,
    IReadOnlyList<CampaignLanding>? Lands = null, int PastKept = 0, int PastOf = 0, bool AllPast = false, int? Same = null, int? After = null,
    bool Full = false, bool Free = false, int RunsWith = 0, int RunsWithout = 0, IReadOnlyList<int>? KeptRuns = null, IReadOnlyList<CampaignRunXp>? AsMustXp = null);

/// <summary>
/// One combo's row on the card. State: "held", "partial" (a Prefer kept in some runs), "notKept", "hand" (a hand edit broke it) or "pin"
/// (a pinned loadout did); <paramref name="Run"/> is where it isn't held and <paramref name="Loadout"/> the pin. <paramref name="PlayedBroke"/>:
/// played runs that had one of a pair's classes without the other (history, never a clash).
/// </summary>
public sealed record CampaignComboRow(int Index, CampaignComboPrice Price, string State, int? Run, int? Loadout, IReadOnlyList<int> PlayedBroke);

/// <summary>A combo that takes classes past their goals: the goal tile's line.</summary>
public sealed record CampaignPastGoal(int Combo, IReadOnlyList<string> Classes);

/// <summary>A class or race ranked in "What you want first". Key: a class abbreviation or a race name; Target 1 to 3; UnlockNow, else Spread out.</summary>
public sealed class CampaignWant
{
    public string Key { get; set; } = "";
    public int Target { get; set; }
    public bool UnlockNow { get; set; }
    public CampaignWant Copy() => new() { Key = Key, Target = Target, UnlockNow = UnlockNow };
}

/// <summary>A ranked row's words. Kind "class" or "race"; Line: its price line or done line, null for none; Clash: the clash found on a click.</summary>
public sealed record CampaignFirstRow(string Kind, string Key, int Target, bool UnlockNow, string? Line, string? Clash = null, int? ClashLoadout = null);

/// <summary>What "What you want first" draws, in the engine's words.</summary>
public sealed record CampaignFirstLane(IReadOnlyList<CampaignFirstRow> Classes, IReadOnlyList<CampaignFirstRow> Races,
    IReadOnlyList<string>? EveryRace = null);

/// <summary>
/// A character's reincarnation campaign: the passive ranks you want (0 to 3 for each class and race) and the loadouts that
/// get there. Kept in settings for each character. The log's reincarnations from <see cref="Since"/> on tick its runs off.
/// </summary>
public sealed class CampaignPlan
{
    public const int MaxFirst = 3, MaxLoadouts = 64, MaxCombos = 32;

    /// <summary>The rank you want for each class's passive, by abbreviation. Missing = 0 (not a goal).</summary>
    public Dictionary<string, int> Classes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>The rank you want for each race's passive, by race name.</summary>
    public Dictionary<string, int> Races { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Races you haven't unlocked yet (max faction with their city factions): planned last, and flagged.</summary>
    public List<string> Locked { get; set; } = [];
    /// <summary>Passives to earn first, most wanted first: "class:MAG" or "race:Iksar".</summary>
    public List<string> First { get; set; } = [];
    public List<CampaignLoadout> Loadouts { get; set; } = [];
    /// <summary>When the loadouts were first made: reincarnations the log sees from then on count toward their runs.</summary>
    public DateTime? Since { get; set; }
    /// <summary>
    /// What to prefer between loadouts that cost the same runs and XP, first most (<see cref="Campaign.Preferences"/>):
    /// "carry", "heals". Empty: no preference.
    /// </summary>
    public List<string> Prefer { get; set; } = [.. Campaign.DefaultPrefer];
    /// <summary>Player-defined class combos in insertion order (null on legacy plans).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<CampaignCombo>? Combos { get; set; }
    /// <summary>Ranked priority classes, most wanted first. Non-null <see cref="ClassOrder"/> or <see cref="RaceOrder"/> marks <see cref="Ranked"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<CampaignWant>? ClassOrder { get; set; }
    /// <summary>The new planner's ranked races, most wanted first: only the ones the player ranked (a goal alone doesn't rank a race). Null on the old plan.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<CampaignWant>? RaceOrder { get; set; }
    /// <summary>Whether the planner reads the ranked lists (the new plan) rather than <see cref="First"/>.</summary>
    [JsonIgnore]
    public bool Ranked => ClassOrder != null || RaceOrder != null;

    /// <summary>A copy with only real classes and races, targets 0 to 3, and whole loadouts (settings.json may be hand-edited).</summary>
    public CampaignPlan Normalized()
    {
        static int ClampRank(int v) => Math.Clamp(v, 0, ClassNames.MaxPassiveRank);
        var classes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in Classes ?? new())
            if (ClassNames.Info(k) is { } c && ClampRank(v) > 0) classes[c.Abbrev] = ClampRank(v);
        var races = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in Races ?? new())
            if (Campaign.RaceName(k) is { } r && ClampRank(v) > 0) races[r] = ClampRank(v);
        var loadouts = new List<CampaignLoadout>();
        foreach (var l in Loadouts ?? [])
        {
            if (l == null || Campaign.RaceName(l.Race) is not { } race) continue;
            var cls = (l.Classes ?? []).Select(c => ClassNames.Info(c)?.Abbrev).OfType<string>().Distinct().ToList();
            if (cls.Count != 3) continue;
            int times = Math.Clamp(l.Times, 1, ClassNames.MaxPassiveRank);
            loadouts.Add(new CampaignLoadout { Race = race, Classes = cls, Times = times, Pinned = l.Pinned, Marked = Math.Clamp(l.Marked, 0, times) });
            if (loadouts.Count == MaxLoadouts) break;
        }
        var locked = (Locked ?? []).Select(Campaign.RaceName).OfType<string>().Distinct().ToList();
        List<CampaignWant>? classOrder = null, raceOrder = null;
        if (Ranked)
        {
            // The goals are the truth for a target; an entry whose goal is 0 is unranked, and a goal alone ranks nothing (a race's goal is set on its tile, as a class's).
            classOrder = [];
            foreach (var w in ClassOrder ?? [])
                if (w != null && ClassNames.Info(w.Key)?.Abbrev is { } c && classes.TryGetValue(c, out var ct) && !classOrder.Any(x => x.Key == c))
                    classOrder.Add(new CampaignWant { Key = c, Target = ct, UnlockNow = w.UnlockNow });
            raceOrder = [];
            foreach (var w in RaceOrder ?? [])
                if (w != null && Campaign.RaceName(w.Key) is { } r && races.TryGetValue(r, out var rt) && !raceOrder.Any(x => x.Key == r))
                    raceOrder.Add(new CampaignWant { Key = r, Target = rt, UnlockNow = w.UnlockNow });
        }
        return new CampaignPlan
        {
            ClassOrder = classOrder,
            RaceOrder = raceOrder,
            Classes = classes,
            Races = races,
            Locked = locked,
            First = (First ?? []).Select(Campaign.GoalKey).OfType<string>().Distinct().Take(MaxFirst).ToList(),
            Loadouts = loadouts,
            Since = loadouts.Count > 0 ? Since : null,
            Prefer = (Prefer ?? [.. Campaign.DefaultPrefer]).Where(p => Campaign.Preferences.Any(x => x.Key == p)).Distinct().ToList(),
            Combos = CleanCombos(Combos),
        };
    }

    /// <summary>Combos as settings.json may have them: real classes once, two or three of them, a trio 1 to 3 times, no repeat, at most <see cref="MaxCombos"/>; Unlock now is a Must; null when none.</summary>
    private static List<CampaignCombo>? CleanCombos(IEnumerable<CampaignCombo>? list)
    {
        var kept = new List<CampaignCombo>();
        foreach (var c in list ?? [])
        {
            var cls = (c?.Classes ?? []).Select(x => ClassNames.Info(x)?.Abbrev).OfType<string>().Distinct().ToList();
            if (c == null || cls.Count is < 2 or > 3) continue;
            bool unlock = !c.Apart && cls.Count == 3 && c.UnlockNow;
            var clean = new CampaignCombo { Classes = cls, Apart = c.Apart, EveryRun = c.EveryRun, Times = Math.Clamp(c.Times, 1, ClassNames.MaxPassiveRank), UnlockNow = unlock, Must = c.Must || unlock };
            if (kept.Any(k => k.Same(clean))) continue;
            kept.Add(clean);
            if (kept.Count == MaxCombos) break;
        }
        return kept.Count == 0 ? null : kept;
    }
}

/// <summary>A reincarnation the log saw: a race and the three classes beside it.</summary>
public sealed record ReincarnationEvent(DateTime At, string Race, IReadOnlyList<string> Classes)
{
    public string TrioKey => ClassNames.TrioKey(Classes);
}

/// <summary>A passive rank a run earns. Kind: class, race or completionist (Key empty).</summary>
public sealed record CampaignGain(string Kind, string Key, int Rank);

/// <summary>
/// One reincarnation in the plan, in the order you'd play it. State: done (its loadout is back at the cap, or you ticked it off), leveling
/// (reincarnated, its relevel still open: Level, EtaHours and Crawls are Home's for it), next or planned. Rank and XP are for runs still to play; Past lists the classes and race already at 3 (the run raises
/// them anyway: more XP to level, no passive). A done or leveling run has them too, as they were when it was played (the ranks you have now, less one for each later reincarnated run holding the name). At: when you reincarnated into it, when known.
/// Round is the lowest class rank the run raises to 3 or under (1 to 3: the page's Round 1, 2, 3), or 0 when it raises no class passive (the page's Races). No hours: how long a trio takes isn't knowable before you play it.
/// </summary>
public sealed record CampaignRun(int N, int Loadout, string Race, IReadOnlyList<string> Classes, string State,
    int RankAfter, double Xp, IReadOnlyList<CampaignGain> Gains, IReadOnlyList<string> Past,
    int? Level = null, double? EtaHours = null, int? Crawls = null, int Round = 0, DateTime? At = null);

/// <summary>A goal: the rank you want, the rank now, and the rank once the plan is played.</summary>
public sealed record CampaignGoal(string Kind, string Key, int Target, int Now, int AtEnd);

public sealed record CampaignLoadoutRow(int Index, int Done, int Left, IReadOnlyList<string> Warnings);

/// <summary>A reincarnation since the plan was made that none of its loadouts has room for.</summary>
public sealed record CampaignOffPlan(DateTime At, string Race, IReadOnlyList<string> Classes);

/// <summary>
/// A run you played that the plan's loadouts don't hold: before the plan began, or since with no loadout for it
/// (<see cref="OffPlan"/>). History: nothing in the plan changes it. Gains are what it earned then.
/// </summary>
public sealed record CampaignPlayed(int N, DateTime At, string Race, IReadOnlyList<string> Classes, IReadOnlyList<CampaignGain> Gains, bool OffPlan);

/// <summary>What a ready-made goal costs from where you are: runs, the XP they need together (in relevels at +0%), and the last one's.</summary>
public sealed record CampaignPreset(string Key, string Name, int Runs, double TotalXp, double LastXp);

/// <summary>The fewest runs that can reach your goals from your ranks now, and the arithmetic behind it.</summary>
/// <param name="ClassRuns">The fewest for the class goals alone: they can be met that many runs after the ones done.</param>
/// <param name="Why">"24 class ranks to earn, three a run", "42 race ranks to earn, one a run" or "MAG needs 3 ranks, one a run".</param>
public sealed record CampaignFloor(int Runs, int ClassRuns, string Why);

/// <summary>What a change to the plan does, against the plan as it stands.</summary>
/// <param name="Runs">Runs it adds (negative: runs it saves).</param>
/// <param name="Xp">XP to level it adds over every run, in relevels at +0%: 0.1 is one run's bar 10% bigger.</param>
/// <param name="ClassesSooner">How many runs sooner your class goals are met (negative: later).</param>
/// <param name="Fixes">Goals it reaches that the plan misses ("class:MNK").</param>
/// <param name="Breaks">Goals it leaves short, or shorter than they were.</param>
/// <param name="Lost">What the trios it changes lose: "heals", "a carry"; <paramref name="Gained"/> likewise.</param>
public sealed record CampaignDelta(int Runs, double Xp, int ClassesSooner, IReadOnlyList<string> Fixes, IReadOnlyList<string> Breaks,
    IReadOnlyList<string> Lost, IReadOnlyList<string> Gained);

/// <summary>A change to the plan, what it does, and the loadouts it leaves.</summary>
public sealed record CampaignChange(string Label, CampaignDelta Delta, IReadOnlyList<CampaignLoadout> Loadouts);

/// <summary>A race or class for one loadout slot. State: "current", "trio" (already in the trio), "best" (the cheapest change) or "".</summary>
public sealed record CampaignChoice(string Value, string State, CampaignDelta? Delta);

/// <summary>What Swap… and the picker show for one loadout: the best changes, and every race and class with what it does.</summary>
public sealed record CampaignOptions(int Index, IReadOnlyList<CampaignChange> Swaps, IReadOnlyList<CampaignChoice> Races,
    IReadOnlyList<IReadOnlyList<CampaignChoice>> Classes);

/// <param name="Floor">The fewest runs from your ranks now: the plan is the cheapest when <see cref="Left"/> matches it.</param>
/// <param name="ClassesDoneAt">The run your class goals are met (0: already; null: the plan misses one).</param>
/// <param name="Fixes">When the plan misses a goal or has runs to spare: the cheapest changes (<see cref="Campaign.Fixes"/>).</param>
/// <param name="Played">Runs you played outside the plan's loadouts, in order (<see cref="Campaign.History"/>).</param>
/// <param name="Unseen">Runs in your ranks the log never saw whole (counts you entered on Roster): before the rest.</param>
/// <param name="Done">Runs reincarnated, leveling or done, among <see cref="Runs"/>; <paramref name="Leveling"/> is how many of them are still below the cap.</param>
public sealed record CampaignView(
    CampaignPlan Plan, IReadOnlyList<CampaignLoadoutRow> Loadouts, IReadOnlyList<CampaignRun> Runs,
    int Done, int Left, double TotalXp, double LastXp,
    IReadOnlyList<CampaignGoal> Goals, int CompletionistNow, int CompletionistAtEnd,
    IReadOnlyList<CampaignOffPlan> OffPlan, IReadOnlyList<CampaignPreset> Presets,
    IReadOnlyDictionary<string, int> ClassRanks, IReadOnlyDictionary<string, int> RaceRanks,
    CampaignFloor Floor, int? ClassesDoneAt, IReadOnlyList<CampaignChange>? Fixes = null,
    IReadOnlyList<CampaignPlayed>? Played = null, int Unseen = 0, int Leveling = 0)
{
    /// <summary>
    /// Runs completed before this plan started: displayed run numbers add this offset to <see cref="CampaignRun.N"/>.
    /// </summary>
    public int Before => (Played?.Count ?? 0) + Unseen;
    /// <summary>The planner's carries, survival check and role table, and whether the card shows.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NewPlannerView? NewPlanner { get; init; }
    /// <summary>Combos that push classes past their target goals, shown on the goal summary tile. Null when none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CampaignPastGoal>? PastGoals { get; init; }
    /// <summary>Each combo's price and state (<see cref="Campaign.ComboRows"/>); null with no combos.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CampaignComboRow>? ComboRows { get; init; }
    /// <summary>The runs a rebuild with the player's Musts and Unlock-now trios gives; set only when there are any (<see cref="Campaign.ComboFloor"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ComboFloor { get; init; }
    /// <summary>Whether the Musts and Unlock-now trios cost anything (runs, XP to level, or class ranks later) against no combos; null with none.</summary>
    public bool? CombosCost { get; init; }
    /// <summary>Why <see cref="ComboFloor"/> is what it is: the Musts it keeps ("WAR / CLR / BST 3 times").</summary>
    public string? ComboWhy { get; init; }
    /// <summary>What the Musts cost, worded ("more XP to level and a later Completionist rank"); null when they cost nothing.</summary>
    public string? ComboCostWhat { get; init; }
    /// <summary>What "What you want first" draws: each ranked row line, the race header and the Don't care line (<see cref="Campaign.FirstLane"/>). Only in the new planner with a ranked plan.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CampaignFirstLane? FirstLane { get; init; }
}
