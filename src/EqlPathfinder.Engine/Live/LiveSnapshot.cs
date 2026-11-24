using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Live;

/// <summary>The suggestion shown between crawls.</summary>
/// <summary>A place to go next: a zone and difficulty with your medians there; FromLevel when it's for the next level band.</summary>
public sealed record NextOption(string Label, double LevelsPerHour, double T5PerHour, double? Cost, int Crawls, int? FromLevel, bool Again,
    double HighTierPerHour = 0);

/// <summary>Between crawls: the crawl that just ended, how it ranked, and where to go next.</summary>
public sealed record NextStep
{
    public VisitStats? Last { get; init; }
    /// <summary>Leveling: the last crawl's kill speed and XP per kill like for like, and whether another place was clearly better.</summary>
    public RunPace? LastPace { get; init; }
    public PlaceCheck? LastPlace { get; init; }
    /// <summary>A faster place that the same check found not clearly faster (the overlay says why nothing is flagged); null when there was none.</summary>
    public string? LastNotClear { get; init; }
    /// <summary>How the last visit was judged (<see cref="VisitKinds"/>), its T5-eq an hour verdict when by motes, and a raid's boss.</summary>
    public string? LastKind { get; init; }
    public FarmPace? LastFarm { get; init; }
    public string? LastBoss { get; init; }
    public int? Place { get; init; }
    public int? Of { get; init; }
    public IReadOnlyList<NextOption> Options { get; init; } = [];
}

/// <summary>The active relevel, for the overlay header.</summary>
public sealed record JourneyView(string Trio, string? Race, int Level, int Cap, double ActiveHours, double? EtaHours, string? EtaNote);

/// <summary>What the overlay shows. Immutable; built on the tracker thread.</summary>
public sealed record LiveSnapshot(
    string Character, int? Level, string? Trio, string? Race, string? Zone,
    DateTime? LogTime, bool LogQuiet, bool Importing, double ImportPercent,
    string Mode, VisitStats? Current, double? CostPerLevel, PaceCheck? Pace, double? LevelPercent, double? NextLevelMinutes,
    NextStep? Next, JourneyView? Journey,
    SessionStats Session, IReadOnlyList<HighTierDrop> SessionHighTier, IReadOnlyList<VisitStats> Recent,
    IReadOnlyList<HighTierDrop> HighTier, double LootedT5, double LootedHighTierT5, double FragmentValue,
    LoadoutOn? Loadout = null)
{
    /// <summary>The loadout on is one the app hasn't met, or its classes changed: <see cref="Pace"/> and the done card's verdicts are left empty, as another loadout's usual would judge it.</summary>
    public bool NotKnown => LoadoutOn.IsNotKnown(Loadout);
}
