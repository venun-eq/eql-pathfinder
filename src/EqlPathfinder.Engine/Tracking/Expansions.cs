namespace EqlPathfinder.Engine.Tracking;

/// <summary>
/// What an expansion changes, from its day (docs/engineering.md rule 13, docs/specs/kunark-cap.md). <paramref name="From"/> is a
/// day on the player's own clock, no time zone: compared with a log line's time, a unit's <c>Day</c> and the service's local "now".
/// </summary>
public sealed record Expansion(string Name, DateTime From, int LevelCap);

/// <summary>Every expansion in order. It ships in the release: no setting, nothing fetched. Moving a day or adding an entry raises <c>EngineState.CurrentVersion</c>.</summary>
public static class Expansions
{
    public static readonly IReadOnlyList<Expansion> All =
    [
        new("Classic", DateTime.MinValue, 50),
        new("Kunark", new DateTime(2026, 12, 8), 55),
    ];

    /// <summary>
    /// The cap on the day of <paramref name="t"/>; when <paramref name="level"/> is above it, the cap of the first later entry that holds
    /// the level (a ding past the cap means the next cap has come). A level past every listed cap keeps the day's cap.
    /// </summary>
    public static int CapOn(IReadOnlyList<Expansion> list, DateTime t, int? level = null)
    {
        int cap = list.Count == 0 ? 50 : list[0].LevelCap;
        int at = 0;
        for (int i = 0; i < list.Count; i++) if (list[i].From <= t) { cap = list[i].LevelCap; at = i; }
        if (level is { } l && l > cap)
            for (int i = at + 1; i < list.Count; i++) if (l <= list[i].LevelCap) return list[i].LevelCap;
        return cap;
    }

    /// <summary>The highest cap in the list: for sizing arrays only.</summary>
    public static int MaxCap(IReadOnlyList<Expansion> list) => list.Count == 0 ? 50 : list.Max(e => e.LevelCap);
}
