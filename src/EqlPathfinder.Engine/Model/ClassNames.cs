namespace EqlPathfinder.Engine.Model;

[Flags]
public enum ClassTrait
{
    None = 0,
    Tank = 1, Melee = 2, Ranged = 4, Pet = 8, Charm = 16, Heal = 32, Nuke = 64, Slow = 128, Lifetap = 256, Songs = 512,
}

public sealed record ClassInfo(string Abbrev, string Name, ClassTrait Traits, string Note);

public static class ClassNames
{
    /// <summary>The 16 classes in the game's own order (spells_us.txt class columns).</summary>
    public static readonly IReadOnlyList<ClassInfo> All =
    [
        new("WAR", "Warrior", ClassTrait.Tank | ClassTrait.Melee, "Tank; no ranged, pet or charm carry."),
        new("CLR", "Cleric", ClassTrait.Heal | ClassTrait.Nuke, "Best heals; undead nukes."),
        new("PAL", "Paladin", ClassTrait.Tank | ClassTrait.Melee | ClassTrait.Heal, "Tank with heals; undead nukes."),
        new("RNG", "Ranger", ClassTrait.Ranged | ClassTrait.Melee, "Bow carry (Ranged stance)."),
        new("SHD", "Shadow Knight", ClassTrait.Tank | ClassTrait.Melee | ClassTrait.Lifetap | ClassTrait.Pet, "Tank with lifetaps."),
        new("DRU", "Druid", ClassTrait.Heal | ClassTrait.Nuke | ClassTrait.Charm, "Heals, nukes, DoTs."),
        new("MNK", "Monk", ClassTrait.Melee, "Melee DPS; feign death."),
        new("BRD", "Bard", ClassTrait.Songs | ClassTrait.Charm, "Songs; DDD from 43 (nerfed 11-10)."),
        new("ROG", "Rogue", ClassTrait.Melee, "Backstab DPS."),
        new("SHM", "Shaman", ClassTrait.Heal | ClassTrait.Slow, "Heals, slow, buffs, canni."),
        new("NEC", "Necromancer", ClassTrait.Pet | ClassTrait.Lifetap | ClassTrait.Charm, "Pet, DoTs, lifetaps."),
        new("WIZ", "Wizard", ClassTrait.Nuke, "Burst nukes."),
        new("MAG", "Magician", ClassTrait.Pet | ClassTrait.Nuke, "Strong pet and nukes."),
        new("ENC", "Enchanter", ClassTrait.Charm, "Charm carry, mez, haste."),
        new("BST", "Beastlord", ClassTrait.Pet | ClassTrait.Melee | ClassTrait.Slow, "Pet (warder) and melee."),
        new("BER", "Berserker", ClassTrait.Melee, "Melee DPS."),
    ];

    /// <summary>
    /// The 16 races you can reincarnate, in the game's order (dbstr 26100+): the races a loadout can pick. The string file
    /// also has Drakkin (26145, with no passive text) and a "Race 18" slot; neither can be picked, so neither counts.
    /// </summary>
    public static readonly IReadOnlyList<string> Races =
    [
        "Human", "Barbarian", "Erudite", "Wood Elf", "High Elf", "Dark Elf", "Half Elf", "Dwarf", "Troll", "Ogre",
        "Halfling", "Gnome", "Iksar", "Kerran", "Froglok", "Half Dark Elf",
    ];

    /// <summary>The most ranks a class or race reincarnation passive has, and Completionist's (every class reaching 1, 2, 3).</summary>
    public const int MaxPassiveRank = 3;

    private static readonly Dictionary<string, string> ToAbbrev = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Shadowknight"] = "SHD",
    };

    static ClassNames()
    {
        foreach (var c in All) { ToAbbrev[c.Name] = c.Abbrev; ToAbbrev[c.Abbrev] = c.Abbrev; }
    }

    /// <summary>
    /// Classes that carry a leveling trio on their own: a real pet, the bow, or Enchanter charm.
    /// (SHD's pet, druid animal charm and bard charm are situational.)
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Carries = new Dictionary<string, string>
    {
        ["MAG"] = "MAG pet",
        ["NEC"] = "NEC pet",
        ["BST"] = "BST warder",
        ["RNG"] = "RNG bow",
        ["ENC"] = "ENC charm",
    };

    public static bool IsHealer(string abbrev) => abbrev is "CLR" or "DRU" or "SHM" or "PAL";

    public static string Abbrev(string name) => ToAbbrev.GetValueOrDefault(name.Trim(), name.Trim().ToUpperInvariant());

    public static ClassInfo? Info(string abbrevOrName) => All.FirstOrDefault(c => c.Abbrev == Abbrev(abbrevOrName));

    private static readonly HashSet<string> PlayableRaces = new(Races.Append("Vah Shir"), StringComparer.OrdinalIgnoreCase);

    /// <summary>/who shows an illusion's race ("Ancient Skeleton"); only playable races are real.</summary>
    public static bool IsPlayableRace(string race) => PlayableRaces.Contains(race.Trim());

    /// <summary>Order-independent key for a trio: "NEC/MAG/BST" and "MAG/NEC/BST" are the same loadout.</summary>
    public static string TrioKey(IEnumerable<string> classes) => string.Join("/", classes.Select(Abbrev).Order(StringComparer.Ordinal));

    public static string TrioKey(string trio) => TrioKey(trio.Split('/', StringSplitOptions.RemoveEmptyEntries));
}
