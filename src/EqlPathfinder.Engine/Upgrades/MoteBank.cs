using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Upgrades;

/// <summary>A mote, fragment or condense offer from the log. Offered = given to Constructed Potential.</summary>
public sealed record BankEvent(DateTime Time, LootKind Kind, MoteTier Tier, int Count, bool Offered);

/// <summary>
/// The motes, fragments and VTPs you have, as you entered them from the game. The log can't see motes going into items or
/// spells, so the counts are yours; after you enter them, loot adds to them and condensing moves motes up a tier.
/// </summary>
public sealed class MoteBank
{
    /// <summary>Index = tier (1..10).</summary>
    public int[] Motes { get; set; } = new int[11];
    public int[] Fragments { get; set; } = new int[11];
    public int Vtps { get; set; }
    /// <summary>When you entered the counts (local time, like the log). Null = never entered.</summary>
    public DateTime? EnteredAt { get; set; }

    /// <summary>What changed since you entered the counts.</summary>
    public sealed record Since(int[] MotesLooted, int[] FragmentsLooted, int[] MotesOffered, int[] MotesMade,
        int[] FragmentsOffered, int[] FragmentsMade);

    /// <summary>The counts now: entered counts, plus loot after them, with condensing applied (2 of a tier make 1 of the next).</summary>
    public (int[] Motes, int[] Fragments, Since Changes) Current(IEnumerable<BankEvent> events)
    {
        var motes = Pad(Motes);
        var frags = Pad(Fragments);
        var looted = new int[11];
        var fragLooted = new int[11];
        var offered = new int[11];
        var made = new int[11];
        var fragOffered = new int[11];
        var fragMade = new int[11];
        if (EnteredAt is { } at)
            foreach (var e in events.Where(e => e.Time > at).OrderBy(e => e.Time))
            {
                int r = (int)e.Tier;
                if (r < 1 || r > 10) continue;
                if (e.Offered)
                {
                    // Both motes and fragments condense 2:1 into the next tier at Constructed Potential.
                    // ("You offered 2 Fragment of Major Potential to Constructed Potential." gave 1 Fragment of Greater).
                    var (have, off, mk) = e.Kind == LootKind.Mote ? (motes, offered, made) : (frags, fragOffered, fragMade);
                    have[r] = Math.Max(0, have[r] - e.Count);
                    off[r] += e.Count;
                    if (r < 10) { have[r + 1] += e.Count / 2; mk[r + 1] += e.Count / 2; }
                }
                else if (e.Kind == LootKind.Mote) { motes[r] += e.Count; looted[r] += e.Count; }
                else { frags[r] += e.Count; fragLooted[r] += e.Count; }
            }
        return (motes, frags, new Since(looted, fragLooted, offered, made, fragOffered, fragMade));
    }

    private static int[] Pad(int[]? a)
    {
        var c = new int[11];
        if (a != null) Array.Copy(a, c, Math.Min(a.Length, 11));
        return c;
    }
}
