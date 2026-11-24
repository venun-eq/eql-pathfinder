using EqlPathfinder.Engine.Parsing;

namespace EqlPathfinder.Engine.IO;

/// <summary>
/// A character arriving in the world (<c>Received MSG_EQ_ADDPLAYER, Player = Venun, zone = New Sebilis Expedition</c>) on
/// the server the client last logged in to, and when that zone finished loading (<c>Initialization complete.</c>).
/// </summary>
public sealed record WorldEntry(DateTime At, string Player, string? Server, string Zone, DateTime? Loaded = null);

/// <summary>A swap or class change dbg.txt showed for one character.</summary>
public sealed record ClientEvent(DateTime At, string Player, string? Server);

/// <summary>What the running client's dbg.txt says: when it started, and each character's first arrival in the world.</summary>
public sealed record ClientSession(DateTime Started, IReadOnlyList<WorldEntry> Entries)
{
    /// <summary>This character's first arrival in the world this run, on this server.</summary>
    public WorldEntry? EntryOf(string player, string server) =>
        Entries.FirstOrDefault(e => e.Player.Equals(player, StringComparison.OrdinalIgnoreCase) &&
                                    server.Equals(e.Server, StringComparison.OrdinalIgnoreCase));

    /// <summary>Loadout swaps this run: "Resetting UI." then an arrival in the same zone (docs/log-formats.md, "Which loadout is on").</summary>
    public IReadOnlyList<ClientEvent> Swaps { get; init; } = [];
    /// <summary>Classes changed inside a loadout this run: an arrival in the same zone with no reset and no zoning lines before it.</summary>
    public IReadOnlyList<ClientEvent> ClassChanges { get; init; } = [];

    /// <summary>When this character's classes changed inside a loadout this run.</summary>
    public IReadOnlyList<DateTime> ClassChangesOf(string player, string server) => Of(ClassChanges, player, server);

    /// <summary>When this character swapped loadouts this run (the arrival after the reset).</summary>
    public IReadOnlyList<DateTime> SwapsOf(string player, string server) => Of(Swaps, player, server);

    private static List<DateTime> Of(IReadOnlyList<ClientEvent> events, string player, string server) =>
        events.Where(e => e.Player.Equals(player, StringComparison.OrdinalIgnoreCase) && server.Equals(e.Server, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.At).ToList();
}

/// <summary>
/// The client's own log, Logs\dbg.txt. EverQuest rewrites it each time it starts and writes to it from the first second,
/// while eqlog_*.txt gets nothing until a character enters the world. It says who logged in, on which server, and when
/// they were in: the only time a quiet eqlog means the log is off. Formats: docs/log-formats.md.
/// </summary>
public sealed class ClientDebugLog(string path)
{
    public const string FileName = "dbg.txt";
    private const string EntryMark = "Received MSG_EQ_ADDPLAYER, Player = ";
    private const string ZoneMark = ", zone = ";
    private const string ServerMark = "WorldRPServer message: server name ";
    private const string LoadedMark = "Initialization complete.";
    private const string ResetMark = "Resetting UI.";
    /// <summary>
    /// Lines logged before an arrival that is neither a loadout swap nor a class change (zoning, or a death that
    /// respawns in the same zone without zoning lines).
    /// </summary>
    private static readonly string[] ZoningMarks = ["Waiting to connect to zone", "Zone Connect --", "Starting char select.", "I died."];

    public string Path { get; } = path;

    private long _offset, _length = -1;
    private DateTime _written;
    private ClientSession? _session;
    private readonly List<WorldEntry> _entries = [];
    private string? _server;
    private int _loading = -1;   // the entry in _entries whose zone is loading
    private readonly List<ClientEvent> _swaps = [], _changes = [];
    private bool _reset, _zoning;   // a reset, or a zoning or death line, since the last arrival
    private DateTime? _entryAt;   // when a character first arrived in the world

    /// <summary>Reads what's new since the last call. Null when there's no dbg.txt (or it can't be read yet).</summary>
    public ClientSession? Read()
    {
        try
        {
            var info = new FileInfo(Path);
            if (!info.Exists) return _session = null;
            if (info.Length == _length && info.LastWriteTimeUtc == _written) return _session;
            if (LogReader.FirstTime(Path) is not { } first) return _session;
            // Rewritten by a new start: shorter than what was read, or a different first line.
            if (_session == null || info.Length < _offset || _session.Started != first)
            {
                _offset = 0;
                _entries.Clear();
                _server = null;
                _loading = -1;
                _swaps.Clear();
                _changes.Clear();
                _reset = _zoning = false;
                _entryAt = null;
                _session = new ClientSession(first, []);
            }
            bool changed = false;
            _offset = LogReader.ReadFrom(Path, _offset, line => changed |= Take(line));
            if (changed) _session = _session with { Entries = _entries.ToArray(), Swaps = _swaps.ToArray(), ClassChanges = _changes.ToArray() };
            _length = info.Length;
            _written = info.LastWriteTimeUtc;
            return _session;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return _session; }
    }

    private bool Take(string line)
    {
        var body = Body(line);
        if (body.StartsWith(ServerMark, StringComparison.Ordinal))
        {
            _server = body[ServerMark.Length..].Trim().ToString();
            return false;
        }
        if (body.StartsWith(ResetMark, StringComparison.Ordinal))
        {
            // World-entry UI resets occur right after arrival; loadout-swap resets precede arrival.
            if (!(LogLine.TryParseTime(line, out var rt) && _entryAt is { } ea && rt >= ea && (rt - ea).TotalSeconds <= 15)) _reset = true;
            return false;
        }
        foreach (var m in ZoningMarks) if (body.StartsWith(m, StringComparison.Ordinal)) { _zoning = true; _reset = false; return false; }   // zoning ends a reset from entering the world (11-19)
        if (ParseEntry(line) is { } e)
        {
            e = e with { Server = _server };
            bool seen = _entries.Any(x => x.Player.Equals(e.Player, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Server, e.Server, StringComparison.OrdinalIgnoreCase));
            bool noted = false;
            if (seen)
            {
                // Same-zone arrival without zoning: preceded by a UI reset = loadout swap; otherwise = class change.
                if (_reset) { _swaps.Add(new ClientEvent(e.At, e.Player, e.Server)); noted = true; }
                else if (!_zoning) { _changes.Add(new ClientEvent(e.At, e.Player, e.Server)); noted = true; }
            }
            _reset = _zoning = false;
            _entryAt = null;
            _loading = _entries.FindIndex(x => x.Player.Equals(e.Player, StringComparison.OrdinalIgnoreCase) &&
                                               string.Equals(x.Server, e.Server, StringComparison.OrdinalIgnoreCase));
            if (_loading >= 0)
            {
                if (_entries[_loading].Loaded != null) _loading = -1;   // a later zone: the first arrival is what counts
                return noted;
            }
            _entries.Add(e);
            _entryAt = e.At;
            _loading = _entries.Count - 1;
            return true;
        }
        if (_loading >= 0 && body.TrimEnd().SequenceEqual(LoadedMark) && LogLine.TryParseTime(line, out var t))
        {
            _entries[_loading] = _entries[_loading] with { Loaded = t };
            _loading = -1;
            return true;
        }
        return false;
    }

    /// <summary>
    /// <c>[Tue Nov 17 17:20:45 2026]00321:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = New Sebilis Expedition</c>,
    /// written on entering the world from character select and again on every zone. The server isn't on the line.
    /// </summary>
    public static WorldEntry? ParseEntry(string line)
    {
        var body = Body(line);
        if (!body.StartsWith(EntryMark, StringComparison.Ordinal) || !LogLine.TryParseTime(line, out var t)) return null;
        var rest = body[EntryMark.Length..].TrimEnd();
        int z = rest.IndexOf(ZoneMark, StringComparison.Ordinal);
        var player = (z < 0 ? rest : rest[..z]).Trim().ToString();
        var zone = z < 0 ? "" : rest[(z + ZoneMark.Length)..].Trim().ToString();
        return player.Length == 0 ? null : new WorldEntry(t, player, null, zone);
    }

    /// <summary>What follows the timestamp and the line counter: <c>[…]00321:</c>.</summary>
    private static ReadOnlySpan<char> Body(string line)
    {
        if (line.Length < LogLine.BodyStart || line[25] != ']') return default;
        int colon = line.IndexOf(':', LogLine.BodyStart - 1);
        return colon < 0 ? default : line.AsSpan(colon + 1);
    }
}
