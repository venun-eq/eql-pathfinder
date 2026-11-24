using System.Text.RegularExpressions;

namespace EqlPathfinder.Engine.Parsing;

/// <summary>
/// Which zone entries are instances, for the crawl tracker and the fight meter alike. A D0 ("Normal") instance enters under
/// the plain zone name ("You have entered The Estate of Unrest.", where D1 is "… 1 (Awakened)"), so the only sign is the line
/// before it: "Player Venun creating instance The Estate of Unrest 274." or an offer to join "Nagafen's Lair - Group 0
/// (Normal)". Entering a zone of exactly that name within 5 minutes is that instance; anything else is the open world, as
/// the name says. Saved with each tracker's state, so a restart between the two lines still works.
/// </summary>
public sealed partial class InstanceWatch
{
    /// <summary>The instance just created or offered, without its number.</summary>
    public string? Pending { get; set; }
    public DateTime? PendingAt { get; set; }
    /// <summary>Its difficulty: an offer names it; a created instance entered under the plain name is D0.</summary>
    public int PendingDifficulty { get; set; }
    public string? PendingLabel { get; set; }

    public const double Minutes = 5;

    /// <summary>"Player Venun creating instance The Estate of Unrest 274." (its zone: "The Estate of Unrest 274").</summary>
    public void Created(DateTime t, string zone) => Expect(t, InstanceNumber().Replace(zone, ""));

    /// <summary>"Slasher has asked you to join the instance: Nagafen's Lair - Group 0 (Normal)."</summary>
    public void Invited(DateTime t, string zone) => Expect(t, zone);

    private void Expect(DateTime t, string name)
    {
        var m = InstanceName().Match(name);
        Pending = m.Success ? m.Groups["base"].Value : name;
        PendingDifficulty = m.Success ? int.Parse(m.Groups["d"].Value) : 0;
        PendingLabel = m.Success ? m.Groups["label"].Value : "Normal";
        PendingAt = t;
    }

    /// <summary>
    /// The zone entry as an instance when it's the one just created or offered; otherwise as it was. Every entry clears
    /// what was pending (one entry, one instance).
    /// </summary>
    public ZoneEntered Resolve(ZoneEntered z)
    {
        var pending = Pending;
        var at = PendingAt;
        Pending = null;
        PendingAt = null;
        if (z.Difficulty != null || pending == null || at is not { } a) return z;
        if ((z.Time - a).TotalMinutes is < 0 or > Minutes || !string.Equals(z.Zone, pending, StringComparison.Ordinal)) return z;
        return z with { BaseZone = z.Zone, Difficulty = PendingDifficulty, DifficultyLabel = PendingLabel, IsInstance = true };
    }

    /// <summary>A return to a D0 instance after a death or errand: the plain name again, as an instance.</summary>
    public static ZoneEntered AsD0(ZoneEntered z, string baseZone, string? label) =>
        z with { BaseZone = baseZone, Difficulty = 0, DifficultyLabel = label ?? "Normal", IsInstance = true };

    [GeneratedRegex(@" \d+$", RegexOptions.CultureInvariant)]
    private static partial Regex InstanceNumber();

    [GeneratedRegex(@"^(?<base>.+?) (?<d>\d+) \((?<label>[A-Za-z]+)\)$", RegexOptions.CultureInvariant)]
    private static partial Regex InstanceName();
}
