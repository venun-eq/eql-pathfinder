using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>
/// What every visit was, worked out once: the trio that played it (/who, or carried and checked against the spells cast),
/// the reincarnation rank it played at, whether the DDD rule leaves it out, and how much of it was grouped. Relevels, the
/// Plan, Ask, Route, History and sharing all read these instead of each working them out, so a rule about what a visit
/// was changes in one place.
/// </summary>
public sealed class PlayFacts
{
    private static readonly TrioCall NoTrio = new(null, null, "none", []);
    private readonly Roster _roster;
    private readonly IReadOnlyDictionary<string, (int Kills, int Party)> _party;
    private List<(DateTime Start, string Trio)>? _timeline;

    private readonly List<(DateTime Start, DateTime End)> _bonus;

    private PlayFacts(IReadOnlyList<Visit> visits, Roster roster, IReadOnlyDictionary<string, int>? spellClasses,
        IReadOnlyDictionary<string, (int Kills, int Party)> party, IEnumerable<BonusXpSpan>? bonus = null)
    {
        Visits = visits.OrderBy(v => v.Start).ToList();
        _bonus = (bonus ?? []).Select(b => (b.Start, b.End)).ToList();
        _roster = roster;
        SpellClasses = spellClasses;
        _party = party;
        Calls = TrioInference.Attribute(Visits, spellClasses);
        Pairs = Calls.ToDictionary(x => x.Key, x => (x.Value.Trio, x.Value.Race));
    }

    /// <param name="visits">Every visit (the live one too); they're kept in start order.</param>
    /// <param name="spellClasses">The game's spell → classes table, or null without it (trios are then the plain attribution).</param>
    /// <param name="party">Kills and party-experience kills per visit, or null when unknown (nothing grouped).</param>
    public static PlayFacts From(IReadOnlyList<Visit> visits, EngineState state, IReadOnlyDictionary<string, int>? spellClasses = null,
        IReadOnlyDictionary<string, (int Kills, int Party)>? party = null) =>
        new(visits, state.Roster, spellClasses, party ?? new Dictionary<string, (int, int)>(), state.BonusXp);

    /// <summary>
    /// Whether play from <paramref name="start"/> to <paramref name="end"/> touched a bonus-XP event: its levels came faster
    /// than the place gives, so Ask and the pace checks leave it out (like for like), while History shows it as it was.
    /// </summary>
    public bool DuringBonus(DateTime start, DateTime end) => _bonus.Any(b => start <= b.End && end >= b.Start);

    public IReadOnlyList<Visit> Visits { get; }
    public IReadOnlyDictionary<string, int>? SpellClasses { get; }

    /// <summary>The trio behind each visit and how it's known.</summary>
    public IReadOnlyDictionary<string, TrioCall> Calls { get; }

    /// <summary><see cref="Calls"/> as (trio, race).</summary>
    public IReadOnlyDictionary<string, (string? Trio, string? Race)> Pairs { get; }

    public TrioCall Trio(string visitId) => Calls.GetValueOrDefault(visitId) ?? NoTrio;

    /// <summary>Kills and how many of them gave party experience.</summary>
    public (int Kills, int Party) Party(string visitId) => _party.GetValueOrDefault(visitId);

    /// <summary>The share of a visit's kills that gave party experience (0 when it had none).</summary>
    public double PartyShare(string visitId) => Party(visitId) is var (k, p) && k > 0 ? p / (double)k : 0;

    /// <summary>The user's DDD rule: before the 11-10 nerf, and DDD did most of the damage.</summary>
    public static bool LeftOutForDdd(Visit v) => RouteAnalysis.PreNerfDdd(v);

    /// <summary>
    /// The XP rank a trio played at, at <paramref name="time"/>: its most-reincarnated class then (later reincarnations
    /// don't count). 0 for an unknown trio.
    /// </summary>
    public int RankAt(string? trio, DateTime time) => (trio?.Split('/') ?? [])
        .Select(c => _roster.Classes.GetValueOrDefault(c) is { } i ? Journeys.RankAt(i, time) : 0).DefaultIfEmpty(0).Max();

    /// <summary>The count of that trio at each bar (<see cref="RankAt(string?, DateTime)"/> at the levels below the first cap): the reincarnations made after reaching a level, for the levels past it.</summary>
    public Func<int, int> RankByLevel(string? trio, DateTime time, TrackerOptions o) { var cs = trio?.Split('/'); return L => _roster.TrioRankAt(cs, L, o.BaseCap, time); }

    /// <summary>The trio of the closed visit nearest <paramref name="time"/>, within <paramref name="hours"/>: who was hunting in the open world then.</summary>
    public string? TrioNear(DateTime time, double hours = 6)
    {
        _timeline ??= Visits.Where(v => v.Closed).Select(v => (v.Start, Trio: Trio(v.Id).Trio)).Where(x => x.Trio != null)
            .Select(x => (x.Start, x.Trio!)).ToList();
        var before = _timeline.LastOrDefault(x => x.Start <= time);
        var after = _timeline.FirstOrDefault(x => x.Start > time);
        var pick = before.Trio != null && (after.Trio == null || time - before.Start <= after.Start - time) ? before : after;
        return pick.Trio != null && Math.Abs((pick.Start - time).TotalHours) <= hours ? pick.Trio : null;
    }

    /// <summary>
    /// Every stretch of leveling the Ask page answers from: closed instance visits (crawls and others) and open-world hunts,
    /// each with its trio, its rank then (times compare at rank 0) and how much of it was grouped. Left out under the user's
    /// DDD rule: visits from before the nerf where DDD did most of the damage, and open-world hunts from before it on a bard
    /// trio or an unknown one (damage isn't kept outside instances, so a bard's DDD can't be ruled out there). Also left out:
    /// visits and hunts during a bonus-XP event (<see cref="DuringBonus"/>), counted in BonusLeftOut.
    /// </summary>
    /// <param name="openKills">Kills outside instances.</param>
    /// <param name="openLoot">Loot outside instances.</param>
    /// <param name="deaths">Every death (only those inside a hunt count).</param>
    public (List<LevelingSegment> Segments, int LeftOut, int OpenLeftOut, int BonusLeftOut) Segments(IReadOnlyList<KillRecord> openKills,
        IReadOnlyList<PathfinderStore.LootRow> openLoot, IReadOnlyList<LevelRecord> deaths, TrackerOptions o)
    {
        var closed = Visits.Where(v => v.Closed).ToList();
        var segments = new List<LevelingSegment>();
        int leftOut = 0, bonusLeftOut = 0;
        foreach (var v in closed)
        {
            if (v.PlayedOnACapLoadout(o.CapOn(v))) continue;   // the stopgap until swap-zones: still in History, not a leveling run
            if (LeftOutForDdd(v)) { leftOut++; continue; }
            if (DuringBonus(v.Start, v.EndTime)) { bonusLeftOut++; continue; }
            var trio = Trio(v.Id).Trio;
            segments.Add(LevelingSegment.From(VisitStats.From(v, v.EndTime, o), RankAt(trio, v.Start), PartyShare(v.Id)) with { Trio = trio, RankAtLevel = RankByLevel(trio, v.Start, o), Cap = o.CapOn(v) });
        }

        double fv = o.FragmentValue;
        double Value(PathfinderStore.LootRow l) => Valuation.T5(l.Tier, l.Count, l.Kind == LootKind.Fragment, fv);
        int openLeftOut = 0;
        foreach (var f in FieldHunts.From(openKills, o))
        {
            var trio = TrioNear(f.Start);
            if (f.Start < RouteAnalysis.FarmPatch && (trio == null || trio.Split('/').Contains("BRD"))) { openLeftOut++; continue; }
            if (DuringBonus(f.Start, f.End)) { bonusLeftOut++; continue; }
            var end = f.End.AddMinutes(1);
            var got = openLoot.Where(l => l.Time >= f.Start && l.Time <= end).ToList();
            segments.Add(LevelingSegment.OpenWorld(f, RankAt(trio, f.Start), trio, got.Sum(Value),
                got.Where(l => MoteTiers.IsHighTier(l.Tier)).Sum(Value), got.Where(l => l.Kind == LootKind.Fragment).Sum(l => l.Count),
                deaths.Count(e => e.Kind == "death" && e.Time >= f.Start && e.Time <= end)) with
            { RankAtLevel = RankByLevel(trio, f.Start, o), Cap = o.CapOn(f.Start, f.Level) });
        }
        return (segments, leftOut, openLeftOut, bonusLeftOut);
    }
}
