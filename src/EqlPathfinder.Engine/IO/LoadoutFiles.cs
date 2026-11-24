namespace EqlPathfinder.Engine.IO;

/// <summary>
/// The game saves a settings file per loadout slot, <c>UI_&lt;Name&gt;_&lt;server&gt;_LO&lt;n&gt;.ini</c> and
/// <c>&lt;Name&gt;_&lt;server&gt;_LO&lt;n&gt;.ini</c>, on every swap: the slot on is the one written last (docs/log-formats.md,
/// "Which loadout is on"). The files say which slot is on, never what its classes are.
/// </summary>
public static class LoadoutFiles
{
    /// <summary>Every name the game saves a slot under for this character, 18 of them.</summary>
    public static IEnumerable<string> NamesOf(string player, string server) =>
        Enumerable.Range(1, 9).SelectMany(n => new[] { $"UI_{player}_{server}_LO{n}.ini", $"{player}_{server}_LO{n}.ini" });

    /// <summary>
    /// The tracked character's slot on, and when its file was last written, from the game folder's file names and write
    /// times. Only files written at or after <paramref name="since"/> (the client's start) count. Null when none does.
    /// </summary>
    public static (int Slot, DateTime Written)? SlotOn(IEnumerable<(string Name, DateTime Written)> files, string player,
        string server, DateTime? since = null)
    {
        var known = NamesOf(player, server).Select((name, i) => (name, slot: i / 2 + 1)).ToDictionary(x => x.name, x => x.slot, StringComparer.OrdinalIgnoreCase);
        (int Slot, DateTime Written)? best = null;
        foreach (var (name, written) in files)
        {
            if (!known.TryGetValue(name, out int slot) || written < since) continue;
            if (best is not { } b || written >= b.Written) best = (slot, written);
        }
        return best;
    }
}
