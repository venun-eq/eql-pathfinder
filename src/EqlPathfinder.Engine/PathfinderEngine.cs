using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Parsing;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine;

/// <summary>Parser + tracker for one character. Feed it raw log lines in order.</summary>
public sealed class PathfinderEngine
{
    public PathfinderEngine(string character, TrackerOptions options, ITrackerSink sink, EngineState? state = null)
    {
        State = state ?? new EngineState { Character = character };
        State.Character = character;
        Options = options;
        Parser = new LineParser(character, State.Pets, State.Charm);
        Tracker = new Tracker(State, options, sink);
    }

    public EngineState State { get; }
    public TrackerOptions Options { get; }
    public LineParser Parser { get; }
    public Tracker Tracker { get; }
    public long Lines { get; private set; }
    public long Events { get; private set; }

    /// <summary>Lines at or before this time are skipped (used when moving on to the next archived file).</summary>
    public DateTime? SkipThrough { get; set; }

    /// <summary>Called with each raw line that produced an event (used by the scrubber).</summary>
    public Action<string, LogEvent>? EventLine { get; set; }

    public void ProcessLine(string raw)
    {
        if (!LogLine.TryParse(raw, out var line)) return;
        if (SkipThrough is { } skip)
        {
            if (line.Time <= skip) return;
            SkipThrough = null;
        }
        Lines++;
        if (line.Body.StartsWith("Welcome to EverQuest Legends!", StringComparison.Ordinal)) Tracker.NoteLogin(line.Time);   // before the queued swap it dates (login-swap)
        ApplyQueued(line.Time);
        _lastLine = line.Time;
        Tracker.OnLine(line.Time);
        var e = Parser.Parse(line);
        if (e == null) return;
        Events++;
        EventLine?.Invoke(raw, e);
        Tracker.Apply(e);
    }

    /// <summary>End of a backfill: emit anything still waiting on a following line.</summary>
    public void Flush()
    {
        ApplyQueued(DateTime.MaxValue);
        Tracker.Flush();
    }

    private readonly List<(long Row, LogEvent Event)> _queued = new();

    /// <summary>
    /// A loadout swap or class change the game's files showed (current-loadout), taken when the log reaches its time: before the
    /// first line at or after it, so a /who in the same second counts as after a swap. <paramref name="row"/> is its
    /// <c>loadout_events</c> id, remembered in the state so a row is never taken twice.
    /// </summary>
    public void Queue(long row, LogEvent e)
    {
        if (row <= State.LoadoutRow) return;
        int at = _queued.FindLastIndex(q => q.Event.Time <= e.Time);
        _queued.Insert(at + 1, (row, e));
    }

    private DateTime? _lastLine;
    private LogEvent? _gapChecked;
    private bool _gapBefore;

    /// <summary>
    /// A swap after a silence in the log, with no login line read yet, waits up to 30 s for one: the game's startup lines
    /// come before "Welcome to EverQuest Legends!", and the swap then counts as the login's guess (login-swap).
    /// </summary>
    private bool MaybeLogin(LoadoutSwapped sw, DateTime until)
    {
        if (_gapChecked != sw) { _gapChecked = sw; _gapBefore = _lastLine is not { } last || last < sw.Time.AddSeconds(-60); }
        return _gapBefore && until < sw.Time.AddSeconds(30) && !(State.LoginAt is { } la && la >= sw.Time.AddMinutes(-2));
    }

    /// <summary>Takes the queued events up to <paramref name="until"/>.</summary>
    public void ApplyQueued(DateTime until)
    {
        while (_queued.Count > 0 && _queued[0].Event.Time <= until)
        {
            var (row, e) = _queued[0];
            if (e is LoadoutSwapped sw && MaybeLogin(sw, until)) break;
            _queued.RemoveAt(0);
            Tracker.Apply(e);
            State.LoadoutRow = Math.Max(State.LoadoutRow, row);
        }
    }

    /// <summary>Reads a whole file (or from an offset) through the engine. Returns the end offset.</summary>
    public long ReadFile(string path, long offset = 0, IProgress<long>? progress = null, CancellationToken ct = default) =>
        LogReader.ReadFrom(path, offset, ProcessLine, progress, ct);
}
