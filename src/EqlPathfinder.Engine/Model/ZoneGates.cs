namespace EqlPathfinder.Engine.Model;

/// <summary>
/// Server-enforced minimum entry levels per zone. Client data files do not store level gates and historical logs
/// can predate a gate's introduction, so gates are maintained explicitly here. Ask filters out gated zones below level.
/// numbers (they can't be repeated).
/// </summary>
public static class ZoneGates
{
    /// <summary>The planes open at 46 (2026-11, and General chat from 10-14 on: "after 46 Planes are quick").</summary>
    private static readonly (string Zone, int Level)[] Gates =
    [
        ("Plane of Hate", 46),
        ("Plane of Fear", 46),
        ("Plane of Sky", 46),
    ];

    /// <summary>
    /// The lowest level that can enter a zone or one of its places ("The Plane of Hate", "Plane of Hate D4",
    /// "Plane of Hate - Solo D4", "Plane of Hate · open world"); 1 for an ungated zone.
    /// </summary>
    public static int MinLevel(string zoneOrPlace)
    {
        var name = zoneOrPlace.StartsWith("The ", StringComparison.Ordinal) ? zoneOrPlace[4..] : zoneOrPlace;
        foreach (var (zone, level) in Gates)
            if (name.StartsWith(zone, StringComparison.OrdinalIgnoreCase)) return level;
        return 1;
    }

    /// <summary>Whether a character at this level can enter the zone or place.</summary>
    public static bool Open(string zoneOrPlace, int level) => level >= MinLevel(zoneOrPlace);
}
