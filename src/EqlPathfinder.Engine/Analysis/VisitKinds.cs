namespace EqlPathfinder.Engine.Analysis;

/// <summary>
/// Classifies how a visit is judged based on what was actually played (never a configured goal):
/// raids by boss kill; grouped play, farmed difficulties, incomplete instances, and cap-level visits
/// by T5-eq an hour; the rest by levels an hour.
/// </summary>
public static class VisitKinds
{
    public const string Raid = "raid", Grouped = "grouped", Farming = "farming", Leveling = "leveling";

    /// <summary>D3 and D4 until the player says otherwise (Settings → How you play).</summary>
    public static readonly IReadOnlyList<int> DefaultFarmed = [3, 4];

    public static string Of(LevelingSegment s, IReadOnlyCollection<int> farmed, int levelCap) => Of(s, farmed, (_, _) => levelCap);

    /// <summary>The same, with the cap of the day the visit was played (<see cref="TrackerOptions.CapOn(DateTime, int?)"/>).</summary>
    public static string Of(LevelingSegment s, IReadOnlyCollection<int> farmed, Tracking.TrackerOptions o) => Of(s, farmed, o.CapOn);

    internal static string Of(LevelingSegment s, IReadOnlyCollection<int> farmed, Func<DateTime, int?, int> capOn)
    {
        if (s.Raid) return Raid;
        if (s.Grouped) return Grouped;
        if (s.LevelStart is { } l && l >= capOn(s.Start, l)) return Farming;
        if (s.Difficulty is { } d && farmed.Contains(d)) return Farming;
        // The log says a crawl ended only by "completed": an instance that ended without it was given up or respawning.
        if (s.Type != "open world" && s.Ended && !s.Completed) return Farming;
        return Leveling;
    }

    /// <summary>Farming and grouped visits are judged by T5-eq an hour.</summary>
    public static bool ByMotes(string kind) => kind is Farming or Grouped;
}
