using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Analysis;

public sealed record PlanRole(string Key, string Name, string Short, string Hint);

/// <summary>
/// What you value in a leveling trio, in order. Each place is worth more than the one below it
/// (5, 4, 3, 2, 1, ½), and you choose which classes count for each role, so a Wizard can be your carry.
/// </summary>
public sealed class PlanPrefs
{
    public static readonly IReadOnlyList<PlanRole> Roles =
    [
        new("carry", "Carry", "carry", "Kills while the trio stays safe: a pet, the bow, charm."),
        new("heals", "Heals", "heals", "Keeps the trio alive."),
        new("proven", "Proven by your relevels", "proven",
            "Shares two classes with a trio you finished: counts for it if that trio was faster than your average, against it if slower."),
        new("burst", "Burst damage", "nukes", "Direct damage spells."),
        new("control", "Crowd control", "control", "Slow, mez, charm."),
        new("tank", "Tank", "tank", "Takes the hits."),
    ];

    public static readonly IReadOnlyList<double> Weights = [5, 4, 3, 2, 1, 0.5];

    public List<string> Order { get; set; } = Roles.Select(r => r.Key).ToList();
    public Dictionary<string, List<string>> Members { get; set; } = DefaultMembers();

    public static Dictionary<string, List<string>> DefaultMembers() => new()
    {
        ["carry"] = ["MAG", "NEC", "BST", "RNG", "ENC"],
        ["heals"] = ["CLR", "DRU", "SHM", "PAL"],
        ["burst"] = ["WIZ", "MAG", "DRU", "CLR"],
        ["control"] = ["SHM", "ENC", "BRD", "BST"],
        ["tank"] = ["WAR", "PAL", "SHD"],
    };

    public static PlanPrefs Default { get; } = new PlanPrefs().Normalized();

    public static double Weight(int place) => place < Weights.Count ? Weights[place] : Weights[^1];

    public static PlanRole Role(string key) => Roles.First(r => r.Key == key);

    /// <summary>A copy with every role exactly once and only real class abbreviations (settings.json may be hand-edited).</summary>
    public PlanPrefs Normalized()
    {
        var keys = Roles.Select(r => r.Key).ToList();
        var order = (Order ?? []).Where(keys.Contains).Distinct().ToList();
        order.AddRange(keys.Where(k => !order.Contains(k)));
        var defaults = DefaultMembers();
        var members = new Dictionary<string, List<string>>();
        foreach (var k in keys.Where(k => k != "proven"))
        {
            var list = Members?.GetValueOrDefault(k) ?? defaults[k];
            members[k] = list.Select(ClassNames.Abbrev).Where(a => ClassNames.Info(a) != null).Distinct().ToList();
        }
        return new PlanPrefs { Order = order, Members = members };
    }
}
