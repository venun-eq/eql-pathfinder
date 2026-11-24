namespace EqlPathfinder.Engine.Model;

/// <summary>
/// One row of "How Pathfinder ranks the classes". A cell is empty (no rank), "1".."4" (a rank), "~text" (a partial one,
/// shown dashed and worth nothing to the survival check) or "3|from 10" (a rank with a note, e.g. the level it comes at).
/// </summary>
public sealed record ClassRole(string Abbrev, string Tank, string Heals, string Slow, string Crowd, string Gives, string Wants, string Notes);

/// <summary>
/// Class role capabilities (tank, heals, slow, crowd control, haste) and survival check rules for the campaign planner.
/// </summary>
public static class ClassRoles
{
    /// <summary>Pathfinder's list of carries, best first: MAG, NEC, ENC, RNG. BST isn't on it.</summary>
    public static readonly IReadOnlyList<string> DefaultCarries = ["MAG", "NEC", "ENC", "RNG"];

    /// <summary>Tank, heals, slow and crowd control that count for survival. BST's slows, ROG's poison, NEC's undead-only and DRU's animal-only charm don't.</summary>
    private static readonly HashSet<string> Tanks = ["WAR", "SHD", "PAL"], Healers = ["CLR", "DRU", "SHM", "PAL"],
        Slows = ["SHM", "ENC", "BRD"], Crowd = ["ENC", "BRD"], HoldUpAlone = ["ENC", "BRD"];

    /// <summary>Who gives haste (a spell that counts: PAL's at 44 is too late), and the classes that gain a lot from it.</summary>
    public static readonly IReadOnlyList<string> HasteGivers = ["ENC", "BRD", "SHM"], HitWithWeapons = ["RNG", "MNK", "BER", "ROG"];

    public static bool IsTank(string c) => Tanks.Contains(c);
    public static bool IsHealer(string c) => Healers.Contains(c);
    public static bool IsSlow(string c) => Slows.Contains(c);
    public static bool IsCrowdControl(string c) => Crowd.Contains(c);

    /// <summary>A trio holds up with ENC or BRD in it, or with two of a tank, heals, a slow and crowd control.</summary>
    public static bool HoldsUp(IReadOnlyList<string> trio)
    {
        if (trio.Any(HoldUpAlone.Contains)) return true;
        return (trio.Any(Tanks.Contains) ? 1 : 0) + (trio.Any(Healers.Contains) ? 1 : 0) + (trio.Any(Slows.Contains) ? 1 : 0)
            + (trio.Any(Crowd.Contains) ? 1 : 0) >= 2;
    }

    public static readonly IReadOnlyList<ClassRole> Table =
    [
        new("WAR", "1", "", "", "", "", "some", "The strongest tank"),
        new("CLR", "", "1", "", "", "", "", "The strongest healer"),
        new("PAL", "3", "4", "", "", "~at 44: too late", "some", "Tank and heals in one seat"),
        new("RNG", "", "", "", "", "", "a lot", "Bow carry"),
        new("SHD", "2", "", "", "", "", "some", "Heals itself with lifetaps"),
        new("DRU", "", "2", "", "~animals only", "", "", ""),
        new("MNK", "", "", "", "", "", "a lot", "Raises its own haste cap"),
        new("BRD", "", "", "3", "2", "3|from 10", "some", "Area slow from 20, and mez: holds a trio up alone. A weaker haste song"),
        new("ROG", "", "", "~poison: not a slow", "", "", "a lot", ""),
        new("SHM", "", "3", "1", "", "2|from 29", "", "Slow from 5; the strong one, Togor's Insects, at 38"),
        new("NEC", "", "", "~undead only", "~undead only", "", "", ""),
        new("WIZ", "", "", "", "", "", "", ""),
        new("MAG", "", "", "", "", "", "", ""),
        new("ENC", "", "", "2", "1", "1|from 16", "", "Area mez; charm from 11; slow from 9, the strong one, Shiftless Deeds, at 41. Holds a trio up alone"),
        new("BST", "", "", "~weak: not counted", "", "", "some", "Weak slows only below 50 (Drowsy at 20, Languid Pace at 35)"),
        new("BER", "", "", "", "", "", "a lot", "Berserker stance"),
    ];

    /// <summary>What a class kills with, for the carries list.</summary>
    public static string Kills(string c) => c switch
    {
        "MAG" => "pet",
        "NEC" => "pet",
        "ENC" => "charm",
        "RNG" => "bow",
        "BST" => "warder",
        "ROG" => "backstab",
        "MNK" => "melee",
        "BER" => "melee",
        "WIZ" => "nukes",
        "BRD" => "songs",
        "SHD" => "pet",
        "DRU" => "nukes",
        _ => "none",
    };
}
