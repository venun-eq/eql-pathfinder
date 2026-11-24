using System.Text.RegularExpressions;

namespace EqlPathfinder.Engine.Upgrades;

/// <summary>An upgradable item from the inventory export. Copies with the same name, tier and place are counted together.</summary>
public sealed record GearItem(string Name, int Tier, string Place, int Count);

/// <summary>
/// Reads /outputfile inventory (Name_server-Inventory.txt in the game folder, tab-separated: Location, Name, ID, Count, Slots).
/// Upgradable items carry their tier in the name ("Pyrosilk Robe +9"); in Equipment storage an item without one is +0.
/// The export has no progress within a tier, no motes or fragments, and leaves out gear worn on other loadouts.
/// </summary>
public static partial class InventoryExport
{
    /// <summary>The export's path: the game folder is the parent of the Logs folder.</summary>
    public static string PathFor(string logsDir, string name, string server) =>
        Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(logsDir)) ?? logsDir, $"{name}_{server}-Inventory.txt");

    public static List<GearItem> ReadGear(string path) => ParseGear(File.ReadLines(path));

    public static List<GearItem> ParseGear(IEnumerable<string> lines)
    {
        var counts = new Dictionary<(string, int, string), int>();
        foreach (var line in lines.Skip(1))
        {
            var f = line.Split('\t');
            if (f.Length < 2 || f[1].Length == 0) continue;
            string loc = f[0].Trim();
            if (loc is "Augmentation" or "Personal-Depot") continue;
            var m = TierRx().Match(f[1]);
            string name;
            int tier;
            if (m.Success) (name, tier) = (m.Groups["name"].Value, int.Parse(m.Groups["tier"].Value));
            else if (loc == "Equipment") (name, tier) = (f[1].Trim(), 0);
            else continue;
            var key = (name, tier, Place(loc));
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }
        return counts.Select(kv => new GearItem(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, kv.Value))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenByDescending(x => x.Tier).ToList();
    }

    /// <summary>Where it is, in words: Equipment, Bags, Bank, Hoard, Activated, or Worn for a slot.</summary>
    public static string Place(string location)
    {
        if (location.StartsWith("General", StringComparison.Ordinal)) return "Bags";
        if (location.StartsWith("Bank", StringComparison.Ordinal)) return "Bank";
        if (location.StartsWith("Hoard", StringComparison.Ordinal)) return "Hoard";
        if (location is "Equipment" or "Activated") return location;
        return "Worn";
    }

    [GeneratedRegex(@"^(?<name>.+?) \+(?<tier>\d{1,2})$", RegexOptions.CultureInvariant)]
    private static partial Regex TierRx();
}
