namespace EqlPathfinder.Engine.Model;

/// <summary>
/// Server-enforced minimum entry levels per zone, maintained explicitly here: client data files do not store level gates.
/// Ask filters out gated zones below level. The planes have always needed 46: a visit read below that is a loadout at the cap
/// the player swapped to with no /who since, so its level is the last one seen (docs/log-formats.md, 11-20), not a time before a rule.
/// </summary>
public static class ZoneGates
{
    /// <summary>The planes have always opened at 46 (General chat from 10-14 on: "after 46 Planes are quick").</summary>
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
