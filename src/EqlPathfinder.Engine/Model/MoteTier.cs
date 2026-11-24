namespace EqlPathfinder.Engine.Model;

/// <summary>
/// Mote and fragment tiers. T5 (Major) is the unit: each tier up doubles, each tier down halves.
/// The untiered "Mote of Potential" is tier 4.
/// </summary>
public enum MoteTier
{
    Infinitesimal = 1,
    Minor = 2,
    Lesser = 3,
    Potential = 4,
    Major = 5,
    Greater = 6,
    Superior = 7,
    Grand = 8,
    Ascendant = 9,
    Infinite = 10,
}

public enum LootKind { Mote, Fragment }

public static class MoteTiers
{
    /// <summary>T5-equivalent value of one mote of this tier: 2^(tier - 5).</summary>
    public static double T5Value(MoteTier tier) => Math.Pow(2, (int)tier - 5);

    /// <summary>Tiers above T5 are called out: Greater and up.</summary>
    public static bool IsHighTier(MoteTier tier) => tier > MoteTier.Major;

    public static bool TryParse(string? word, out MoteTier tier)
    {
        // "Mote of Potential" has no tier word.
        if (string.IsNullOrEmpty(word)) { tier = MoteTier.Potential; return true; }
        return Enum.TryParse(word, ignoreCase: false, out tier) && Enum.IsDefined(tier);
    }

    public static string Abbrev(MoteTier tier) => tier switch
    {
        MoteTier.Infinitesimal => "Tiny",
        MoteTier.Minor => "Min",
        MoteTier.Lesser => "Les",
        MoteTier.Potential => "Pot",
        MoteTier.Major => "Maj",
        MoteTier.Greater => "Gr",
        MoteTier.Superior => "Sup",
        MoteTier.Grand => "Grd",
        MoteTier.Ascendant => "Asc",
        MoteTier.Infinite => "Inf",
        _ => tier.ToString(),
    };
}
