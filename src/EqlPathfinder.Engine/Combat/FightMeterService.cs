using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Parsing;

namespace EqlPathfinder.Engine.Combat;

/// <summary>
/// The fight meter: its own reader of the same logs, apart from the crawl tracker, so each can change without the
/// other. Reads history once (for "your usual"), then follows the live log and publishes a <see cref="MeterFrame"/>
/// whenever a fight changes.
/// </summary>
public sealed class FightMeterService : IDisposable
{
    /// <summary>
    /// Increment when fight parsing rules change so history is re-read.
    /// 4: D0 instances under plain zone names. 5: Frenzy target parsing &amp; named melee skill rows.
    /// </summary>
    public const int CurrentVersion = 6;

    private readonly CharacterLogs _logs;
    private readonly FightStore _store;
    private FightCheckpoint _checkpoint;
    private FightTracker _tracker;
    private LineParser _parser;
    private readonly List<Fight> _closed = [];
    private readonly List<Fight> _session = [];
    private DateTime? _skipThrough;
    private (string Key, int At, Usual? Value) _usual = ("", 0, null);
    private int _closedCount;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private volatile bool _gameRunning = true;
    private double? _importPercent;
    private Analysis.TrioTimeline? _timeline, _pendingTimeline;

    /// <summary>
    /// The app's trio over time (its /who and spell-checked trios): new fights take their trio from it, and stored fights
    /// are labelled again with it, so "your usual" uses the same trio as every other page. Any thread; applied on the meter's.
    /// </summary>
    public void SetTrioTimeline(Analysis.TrioTimeline timeline) => Volatile.Write(ref _pendingTimeline, timeline);

    private void ApplyTimeline(Analysis.TrioTimeline timeline)
    {
        _timeline = timeline;
        _tracker.Context.Timeline = timeline;
        if (!NeedsImport) _store.Relabel(_logs.Key, timeline);
        _usual = ("", 0, null);
        Publish();
    }

    /// <summary>The trio playing now, as the meter labels fights.</summary>
    private string? TrioNow => _tracker.Context.TrioAt(_tracker.Context.LastLine ?? DateTime.Now);

    public FightMeterService(CharacterLogs logs, string storePath)
    {
        _logs = logs;
        _store = new FightStore(storePath);
        _checkpoint = _store.LoadCheckpoint(logs.Key) ?? new FightCheckpoint();
        (_tracker, _parser) = NewTracker(_checkpoint.Context);
        if (!NeedsImport) _session.AddRange(_store.Session(logs.Key, _checkpoint.Context.Session));
    }

    /// <summary>History hasn't been read with this version's rules yet.</summary>
    public bool NeedsImport => _checkpoint.Version < CurrentVersion;
    public bool GameRunning { get => _gameRunning; set => _gameRunning = value; }
    public MeterFrame? Frame { get; private set; }
    public event Action<MeterFrame>? FrameChanged;
    public event Action<Exception>? Error;
    public FightStore Store => _store;

    private double _linger = FightTracker.DefaultLingerSeconds;
    /// <summary>How long a mob that hasn't died keeps its fight open (<see cref="FightTracker.LingerSeconds"/>). Applies from now on.</summary>
    public double LingerSeconds
    {
        get => _linger;
        set { _linger = Math.Clamp(value, FightTracker.MinLingerSeconds, FightTracker.MaxLingerSeconds); _tracker.LingerSeconds = _linger; }
    }

    private (FightTracker, LineParser) NewTracker(FightContext context)
    {
        context.Timeline = _timeline;
        return (new FightTracker(_logs.Name, context, OnClosed) { LingerSeconds = _linger }, new LineParser(_logs.Name, context.Pets, context.Charm));
    }

    private void OnClosed(Fight f)
    {
        _closed.Add(f);
        _closedCount++;
        if (_session.Count > 0 && _session[^1].Session != f.Session) _session.Clear();
        if (f.Session == _tracker.Context.Session) _session.Add(f);
    }

    private void ProcessLine(string raw)
    {
        if (!LogLine.TryParse(raw, out var line)) return;
        if (_skipThrough is { } skip)
        {
            if (line.Time <= skip) return;
            _skipThrough = null;
        }
        _tracker.OnLine(line.Time);
        var e = _parser.Parse(line) ?? CombatParser.Parse(line);
        if (e != null) _tracker.Apply(e);
    }

    /// <summary>Reads every log from the start. Fights are saved as they finish; the old ones are replaced.</summary>
    public void Import(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        _importPercent = 0;
        try
        {
            _store.Reset(_logs.Key);
            _checkpoint = new FightCheckpoint();
            (_tracker, _parser) = NewTracker(_checkpoint.Context);
            _closed.Clear();
            _session.Clear();
            var files = _logs.Files.Count > 0 ? _logs.Files.ToList() : [_logs.LivePath];
            long total = Math.Max(1, files.Sum(SafeLength)), done = 0;
            foreach (var file in files)
            {
                long before = done;
                _skipThrough = _tracker.Context.LastLine;
                var p = new Inline(pos =>
                {
                    _importPercent = 100.0 * (before + pos) / total;
                    progress?.Report(_importPercent.Value);
                    if (_closed.Count >= 2000) Commit(null);
                    if (Frame == null || (int)_importPercent.Value != (int)(Frame.ImportPercent ?? -1)) Publish();
                });
                _checkpoint.Offsets[file] = LogReader.ReadFrom(file, 0, ProcessLine, p, ct);
                done += SafeLength(file);
            }
            _tracker.CloseAll();
            _checkpoint.Version = CurrentVersion;
            Commit(_checkpoint);
            _session.Clear();
            if (_timeline != null) _store.Relabel(_logs.Key, _timeline);
            _session.AddRange(_store.Session(_logs.Key, _tracker.Context.Session));
        }
        finally { _importPercent = null; }
        Publish();
    }

    private sealed class Inline(Action<long> f) : IProgress<long> { public void Report(long value) => f(value); }

    private static long SafeLength(string f) { try { return new FileInfo(f).Length; } catch (IOException) { return 0; } }

    private void Commit(FightCheckpoint? checkpoint)
    {
        _store.Save(_logs.Key, _closed, checkpoint);
        _closed.Clear();
    }

    /// <summary>Follows the live log: four times a second while the game runs, every 5 seconds otherwise.</summary>
    public void Start()
    {
        if (_loop != null) return;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _loop = Task.Run(async () =>
        {
            try { if (NeedsImport) Import(null, ct); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { Error?.Invoke(ex); return; }
            Publish();
            while (!ct.IsCancellationRequested)
            {
                try { Step(); }
                catch (Exception ex) { Error?.Invoke(ex); }
                try { await Task.Delay(_gameRunning ? 250 : 5000, ct); } catch (OperationCanceledException) { break; }
            }
            try { _tracker.CloseAll(); _checkpoint.Offsets[_logs.LivePath] = _liveOffset; Commit(_checkpoint); }
            catch (Exception ex) { Error?.Invoke(ex); }
        }, ct);
    }

    private long _liveOffset = -1;

    private void Step()
    {
        if (Interlocked.Exchange(ref _pendingTimeline, null) is { } timeline) ApplyTimeline(timeline);
        var live = _logs.LivePath;
        bool changed = false;
        if (File.Exists(live))
        {
            if (_liveOffset < 0) _liveOffset = _checkpoint.Offsets.GetValueOrDefault(live);
            long len = SafeLength(live);
            if (len < _liveOffset) _liveOffset = 0;
            if (len > _liveOffset)
            {
                int openBefore = _tracker.Open.Count;
                long damageBefore = _tracker.Open.Sum(f => f.Damage + f.Taken + f.Healing);
                _liveOffset = LogReader.ReadFrom(live, _liveOffset, ProcessLine);
                changed = _closed.Count > 0 || _tracker.Open.Count != openBefore || _tracker.Open.Sum(f => f.Damage + f.Taken + f.Healing) != damageBefore;
            }
        }
        if (_tracker.Open.Count > 0)
        {
            // The log clock is this PC's clock, so a fight with no new lines for 10 seconds is over.
            _tracker.Tick(DateTime.Now);
            if (_closed.Count > 0) changed = true;
        }
        // The saved offset only moves while nothing is open, so a restart never loses the start of a fight.
        if (_tracker.Open.Count == 0 && _liveOffset >= 0 && _checkpoint.Offsets.GetValueOrDefault(live) != _liveOffset)
        {
            _checkpoint.Offsets[live] = _liveOffset;
            Commit(_checkpoint);
        }
        else if (_closed.Count > 0) Commit(null);
        if (changed || Frame == null || ContextChanged()) Publish();
    }

    private bool ContextChanged()
    {
        var c = _tracker.Context;
        return Frame is not { } f || f.Zone != c.Zone || f.Difficulty != c.Difficulty || f.Stint != c.Stint || f.Session != c.Session || f.Trio != TrioNow || f.NotKnown != c.NotKnownAt(c.LastLine ?? DateTime.Now);
    }

    private void Publish()
    {
        var c = _tracker.Context;
        if (_session.Count > 0 && _session[^1].Session != c.Session) _session.Clear();
        var trio = TrioNow;
        bool notKnown = _tracker.Context.NotKnownAt(_tracker.Context.LastLine ?? DateTime.Now);
        string key = $"{c.Zone}|{c.Difficulty}|{trio}";
        if (_importPercent == null && (_usual.Key != key || _closedCount - _usual.At >= 25))
            _usual = (key, _closedCount, c.Zone == null ? null : Usual.From(_store.InPlace(_logs.Key, c.Zone, c.Difficulty, trio), c.LastLine ?? DateTime.Now));
        var frame = new MeterFrame(_logs.Name, c.Zone, c.Difficulty, c.InInstance, trio, c.Session, c.Stint, _session.ToList(),
            _tracker.Open.Select(f => f.Clone()).ToList(), _importPercent == null && !notKnown ? _usual.Value : null, _importPercent, notKnown);
        Frame = frame;
        FrameChanged?.Invoke(frame);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _loop?.Wait(3000); } catch (AggregateException) { }
        _store.Dispose();
    }
}
