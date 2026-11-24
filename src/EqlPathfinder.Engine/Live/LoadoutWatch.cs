using EqlPathfinder.Engine.IO;

namespace EqlPathfinder.Engine.Live;

/// <summary>A loadout swap ("swap", to <c>Slot</c>) or a class change ("classes", slot 0) the game's files showed, and when.</summary>
public sealed record LoadoutSighting(DateTime Time, string Kind, int Slot);

/// <summary>
/// Watches the game's files for which loadout the tracked character is on (docs/log-formats.md, "Which loadout is on"):
/// the slot files in the game folder, and dbg.txt in its Logs folder for when a swap happened and a class changed. Polled
/// from the tracker's loop, so nothing here touches the engine; any I/O error is "nothing seen" and the app works as it did.
/// </summary>
public sealed class LoadoutWatch(string gameDir, string logDir, string player, string server)
{
    /// <summary>A swap's writes settle within this many seconds: the slot left is saved about 1 s before the one swapped to.</summary>
    public const double SettleSeconds = 2;

    private readonly ClientDebugLog _dbg = new(Path.Combine(logDir, ClientDebugLog.FileName));
    private int? _emitted;
    private readonly HashSet<DateTime> _changes = [];

    /// <summary>What's new since the last call. <paramref name="now"/> is the local clock, as the files' times are.</summary>
    public List<LoadoutSighting> Poll(DateTime now)
    {
        var seen = new List<LoadoutSighting>();
        try
        {
            var files = new List<(string, DateTime)>();
            foreach (var name in LoadoutFiles.NamesOf(player, server))
            {
                var info = new FileInfo(Path.Combine(gameDir, name));
                if (info.Exists) files.Add((name, info.LastWriteTime));
            }
            var session = _dbg.Read();
            if (LoadoutFiles.SlotOn(files, player, server, session?.Started) is not { } on) return seen;
            if ((now - files.Where(f => f.Item2 >= (session?.Started ?? DateTime.MinValue)).Max(f => f.Item2)).TotalSeconds < SettleSeconds) return seen;
            var at = SwapTime(session, on.Written);
            if (_emitted != on.Slot)
            {
                _emitted = on.Slot;
                seen.Add(new LoadoutSighting(at, "swap", on.Slot));
            }
            // A class change counts for the slot on, so only one after its swap; earlier ones belonged to a slot since left.
            foreach (var t in session?.ClassChangesOf(player, server) ?? [])
                if (t > at && _changes.Add(t)) seen.Add(new LoadoutSighting(t, "classes", 0));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return seen;
    }

    /// <summary>
    /// When the slot on was swapped to: the last swap this run's dbg.txt shows at or before the slot file's write; else the
    /// character's first arrival this run (they entered the world on it); else the file's write, when dbg.txt is missing or
    /// from another run. A /who typed after this time names the slot.
    /// </summary>
    public DateTime SwapTime(ClientSession? session, DateTime written)
    {
        if (session == null) return written;
        var swaps = session.SwapsOf(player, server).Where(t => t <= written).ToList();
        if (swaps.Count > 0) return swaps.Max();
        return session.EntryOf(player, server)?.At is { } entered && entered <= written ? entered : written;
    }
}
