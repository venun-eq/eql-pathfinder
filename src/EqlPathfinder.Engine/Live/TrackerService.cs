using System.Collections.Concurrent;
using System.Diagnostics;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Community;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Parsing;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;
using EqlPathfinder.Engine.Usage;

namespace EqlPathfinder.Engine.Live;

/// <summary>
/// Follows one character's log: imports history once, then tails the live file, saves records and a
/// checkpoint every few seconds, and publishes snapshots. All engine work happens on one thread.
/// </summary>
public sealed partial class TrackerService : IDisposable
{
    private readonly PathfinderStore _store;
    private readonly CharacterLogs _logs;
    private readonly TrackerOptions _options;
    private readonly object _storeLock = new();
    private readonly object _historyLock = new();
    private readonly PathfinderStore.Batch _batch = new();
    private readonly List<Visit> _history = new();
    private readonly List<HighTierDrop> _highTier = new();
    /// <summary>The game's files that say which loadout is on; null when there's no folder to watch.</summary>
    private readonly LoadoutWatch? _watch;
    private bool _loadoutRowsChecked;
    private bool _watchFailed;
    private PathfinderEngine _engine;
    private Checkpoint _checkpoint;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private Task? _importTask;
    private bool _live;
    private bool _dirty;
    private DateTime _lastCommit = DateTime.MinValue;
    private volatile bool _importing;
    /// <summary>The last history read threw or was cancelled: what it kept is part of the play (cleared when a read starts).</summary>
    private volatile bool _importFailed;
    private double _importPercent;
    /// <summary>Raised whenever the log moves the tracker (new lines read), so a cache keyed on it can never serve what a visit was before.</summary>
    private int _revision;
    // Kills and party kills per crawl, and kills outside instances (open-world hunting). Guarded by _historyLock.
    private readonly Dictionary<string, (int Kills, int Party)> _party = new();
    private readonly List<KillRecord> _fieldKills = new();
    private readonly Task<Dictionary<string, int>> _spellClasses;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _wake = new(0);
    private readonly ConcurrentQueue<Action> _queries = new();
    /// <summary>One at a time: a query before the loop runs, or a step of the loop.</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _gameRunning = true;
    private volatile bool _publishNow = true;
    private DateTime _lastPublish = DateTime.MinValue;
    private CommunityData? _community;
    private (CommunityData? File, List<LevelingSegment>? Segments, DateTime? From, CommunityData? Yours) _forYou;

    /// <summary>The date every analysis is measured from: Ask's weights, the pace drift, "last crawl" freshness. Timers use the system clock.</summary>
    private DateTime Now => _time.GetLocalNow().DateTime;
    /// <summary>The level cap today, on the player's own clock (docs/specs/kunark-cap.md): it changes at midnight with no new log line, so <see cref="SettingsKey"/> carries it.</summary>
    private int CapNow => _options.CapOn(Now);

    public TrackerService(PathfinderStore store, CharacterLogs logs, TrackerOptions options, TimeProvider? time = null)
    {
        _store = store;
        _logs = logs;
        _options = options;
        _time = time ?? TimeProvider.System;
        _checkpoint = store.LoadCheckpoint(logs.Key) ?? new Checkpoint();
        _engine = NewEngine(_checkpoint.State.Character.Length > 0 ? _checkpoint.State : null);
        // An older checkpoint has no reincarnation times yet; it keeps its counts until the rebuild re-reads them.
        if (!NeedsRebuild) _engine.State.Roster.Apply(options.EnteredCounts);
        _history.AddRange(store.RecentVisits(logs.Key, 100_000));
        _highTier.AddRange(store.HighTierDrops(logs.Key, 25));
        if (Path.GetDirectoryName(logs.LivePath) is { Length: > 0 } logDir && Path.GetDirectoryName(logDir) is { Length: > 0 } gameDir)
            _watch = new LoadoutWatch(gameDir, logDir, logs.Name, logs.Server);
        foreach (var (id, x) in store.PartyKillsByVisit(logs.Key)) _party[id] = x;
        _fieldKills.AddRange(store.OpenWorldKills(logs.Key));
        var install = GameData.InstallDirFromLogs(Path.GetDirectoryName(logs.LivePath)!);
        Passives = install != null ? GameData.LoadReincarnationPassives(install) : new();
        // 38 MB: read in the background; the Plan names what each class did once it's there.
        _spellClasses = install != null ? Task.Run(() => GameData.LoadSpellClasses(install)) : Task.FromResult(new Dictionary<string, int>());
    }

    public CharacterLogs Logs => _logs;
    public bool HasHistory => _checkpoint.Offsets.Count > 0;
    /// <summary>History is being read (the first time, or again after an update): pages and facts are still filling in.</summary>
    public bool Importing => _importing;
    /// <summary>History was read by an older engine that didn't track everything this one does.</summary>
    public bool NeedsRebuild => HasHistory && _checkpoint.State.Version < EngineState.CurrentVersion;
    public TrackerOptions Options => _options;
    public IReadOnlyDictionary<string, Passive> Passives { get; }
    /// <summary>"Auto" (leveling below the cap, farming at it), "Leveling" or "Farming".</summary>
    public string ModeSetting { get; set; } = "Auto";

    /// <summary>The numbers built from what players share, every 30 minutes (community/v1.json), set by the app; null without them.</summary>
    public CommunityData? Community
    {
        get => Volatile.Read(ref _community);
        set => Volatile.Write(ref _community, value);
    }

    /// <summary>
    /// Since when this copy's play is in the community numbers (everything: <see cref="DateTime.MinValue"/>), or null when
    /// it isn't (not sharing). Set by the app; read whenever the numbers are.
    /// </summary>
    public Func<DateTime?>? SharedFrom { get; set; }

    public event Action<LiveSnapshot>? SnapshotReady;
    public event Action<HighTierDrop>? HighTierDropped;
    public event Action<Exception>? Error;

    public double FragmentValue
    {
        get => _options.FragmentValue;
        set => _options.FragmentValue = value;
    }

    /// <summary>
    /// Set by the app from the game watcher. While the game is closed the log can't change, so the
    /// loop checks every 5 s instead of twice a second and publishes only when something happens.
    /// </summary>
    public bool GameRunning
    {
        get => _gameRunning;
        set
        {
            if (_gameRunning == value) return;
            _gameRunning = value;
            _publishNow = true;
            Wake();
        }
    }

    private PathfinderEngine NewEngine(EngineState? state) => new(_logs.Name, _options, new Sink(this), state);

    /// <summary>Every setting a cached build reads (what a fragment is worth, the farmed difficulties, the farm rate, the cap, the idle gap). Changing one changes this, so it goes in every key.</summary>
    private string SettingsKey() =>
        $"{_options.FragmentValue}|{string.Join(',', _options.FarmDifficulties.Order())}|{_options.FarmRateMode}|{_options.FarmRateManual}|{CapNow}|{_options.IdleThresholdSeconds}";

    /// <summary>
    /// Community numbers for this player with their own play excluded when sharing, avoiding double-counting in Ask.
    /// Rebuilt when Ask's visits or the community file change.
    /// </summary>
    private CommunityData? CommunityForYou()
    {
        var c = Community;
        if (c == null || SharedFrom?.Invoke() is not { } from) return c;
        var segs = AskSegments().Segments;
        var f = _forYou;
        if (ReferenceEquals(f.File, c) && ReferenceEquals(f.Segments, segs) && f.From == from) return f.Yours;
        var yours = c.LeavingOut(segs, from);
        _forYou = (c, segs, from, yours);
        return yours;
    }

    // ------------------------------------------------------------------ import

    public Task ImportHistoryAsync(IProgress<double>? progress = null, CancellationToken ct = default) =>
        _importTask = ImportCoreAsync(progress, ct);

    private async Task ImportCoreAsync(IProgress<double>? progress, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            _importing = true;
            _importFailed = true;   // until it completes
            var clock = Stopwatch.StartNew();
            try
            {
                lock (_storeLock) _store.Reset(_logs.Key);
                lock (_historyLock) { _history.Clear(); _party.Clear(); _fieldKills.Clear(); }
                _highTier.Clear();
                _checkpoint = new Checkpoint();
                _engine = NewEngine(null);
                QueueLoadoutRows();   // the swaps seen so far, replayed among the lines by their times (Reset leaves them)
                var files = _logs.Files.Count > 0 ? _logs.Files.ToList() : [_logs.LivePath];
                long total = files.Sum(SafeLength), done = 0;
                DateTime? lastEnd = null;
                foreach (var f in files)
                {
                    long fileDone = done;
                    _engine.SkipThrough = lastEnd;
                    var p = new InlineProgress(pos =>
                    {
                        _importPercent = total > 0 ? 100.0 * (fileDone + pos) / total : 100;
                        progress?.Report(_importPercent);
                        if (_batch.Count > 20000) lock (_storeLock) _store.Commit(_logs.Key, _batch, null);
                    });
                    long end = _engine.ReadFile(f, 0, p, ct);
                    _checkpoint.Offsets[f] = end;
                    done += SafeLength(f);
                    lastEnd = _engine.State.LastTime;
                }
                _engine.Flush();
                _engine.State.Roster.Apply(_options.EnteredCounts);
                _journeys = null;
                _revision++;
                SaveCheckpoint();
                _importPercent = 100;
                progress?.Report(100);
                _importFailed = false;
                UsageMetrics.Default.Import(clock.Elapsed.TotalSeconds, total / 1e6);
            }
            finally { _importing = false; _loadoutRowsChecked = false; }   // a swap kept during the read is taken by the next step
        }, ct);
    }

    /// <summary>
    /// Polls game files and persists loadout swaps and class changes immediately (even mid-import), since INI files
    /// and dbg.txt keep no history. Returns true when new events were recorded.
    /// </summary>
    private bool WatchLoadout()
    {
        if (_watch == null) return false;
        bool any = false;
        try
        {
            foreach (var x in _watch.Poll(DateTime.Now))
            {
                lock (_storeLock) _store.AddLoadoutEvent(_logs.Key, x.Time, x.Kind, x.Slot);
                any = true;
            }
        }
        catch (Exception ex)
        {
            // Whatever it is (a store error, say), the log must still be read: tracking goes on without the loadout watch.
            if (!_watchFailed) { _watchFailed = true; Error?.Invoke(ex); }
        }
        return any;
    }

    /// <summary>Hands the engine every stored loadout event it hasn't taken (by row), to be applied by time among the lines.</summary>
    private void QueueLoadoutRows()
    {
        _loadoutRowsChecked = true;
        List<(long Id, DateTime Time, string Kind, int Slot)> rows;
        lock (_storeLock) rows = _store.LoadoutEventsAfter(_logs.Key, _engine.State.LoadoutRow);
        foreach (var r in rows)
            _engine.Queue(r.Id, r.Kind == "swap" ? new LoadoutSwapped(r.Time, r.Slot) : new LoadoutClassesChanged(r.Time));
    }

    private static long SafeLength(string f) { try { return new FileInfo(f).Length; } catch (IOException) { return 0; } }

    // ------------------------------------------------------------------ live

    private void Wake() { if (_wake.CurrentCount == 0) _wake.Release(); }

    public void Start()
    {
        if (_loop != null) return;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _loop = Task.Run(async () =>
        {
            _live = true;
            while (!ct.IsCancellationRequested)
            {
                // A query asked before the loop began may still be running: the state is the loop's once it's done.
                try { _gate.Wait(ct); } catch (OperationCanceledException) { break; }
                try { Step(); }
                catch (Exception ex) { Error?.Invoke(ex); }
                finally { _gate.Release(); }
                try { await _wake.WaitAsync(_gameRunning ? 500 : 5000, ct); } catch (OperationCanceledException) { break; }
            }
            try { SaveCheckpoint(); } catch (Exception ex) { Error?.Invoke(ex); }
        }, ct);
    }

    /// <summary>Runs <paramref name="f"/> on the tracker thread (the engine state isn't thread-safe).</summary>
    public Task<T> Query<T>(Func<TrackerService, T> f)
    {
        if (_loop == null) return BeforeLoop(f);
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queries.Enqueue(() =>
        {
            try { tcs.SetResult(f(this)); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        Wake();
        return tcs.Task;
    }

    /// <summary>
    /// Serializes queries behind any active history import so concurrent callers never race on shared caches.
    /// </summary>
    private async Task<T> BeforeLoop<T>(Func<TrackerService, T> f)
    {
        if (_importTask is { IsCompleted: false } import)
            try { await import.ConfigureAwait(false); } catch (Exception) { /* a failed read: the query reads what there is */ }
        await _gate.WaitAsync().ConfigureAwait(false);
        try { return f(this); }
        finally { _gate.Release(); }
    }

    private void Step()
    {
        while (_queries.TryDequeue(out var q)) q();
        bool sighted = WatchLoadout();
        if (_importing) { Publish(); return; }
        bool changed = false;
        long rowBefore = _engine.State.LoadoutRow;
        if (sighted || !_loadoutRowsChecked) QueueLoadoutRows();
        var live = _logs.LivePath;
        if (File.Exists(live))
        {
            long len = SafeLength(live);
            long off = _checkpoint.Offsets.GetValueOrDefault(live);
            if (len < off) off = 0;
            if (len > off)
            {
                _checkpoint.Offsets[live] = _engine.ReadFile(live, off);
                _revision++;
                _dirty = true;
                changed = true;
            }
        }
        _engine.ApplyQueued(Now);   // a login's swap row waits for its Welcome line; only Flush passes MaxValue
        if (_engine.State.LoadoutRow != rowBefore) { _revision++; _dirty = true; changed = true; }
        _engine.Tracker.Tick(Now);
        if (_dirty && (_batch.Count > 0 || (DateTime.Now - _lastCommit).TotalSeconds > 10)) SaveCheckpoint();

        // Redraw at most once a second while playing (the crawl clock ticks), every few seconds when
        // nothing moves, and only on change while the game is closed.
        double since = (DateTime.Now - _lastPublish).TotalSeconds;
        bool inCrawl = _engine.State.Current is { SegmentStart: not null };
        double every = !_gameRunning ? 300 : inCrawl ? 1 : 3;
        if (_publishNow || since >= (changed ? 1 : every))
        {
            _publishNow = false;
            Publish();
        }
    }

    private void SaveCheckpoint()
    {
        _checkpoint.State = _engine.State;
        _checkpoint.LastTime = _engine.State.LastTime;
        lock (_storeLock) _store.Commit(_logs.Key, _batch, _checkpoint);
        _lastCommit = DateTime.Now;
        _dirty = false;
    }

    private void Publish()
    {
        _lastPublish = DateTime.Now;
        SnapshotReady?.Invoke(BuildSnapshot());
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(5)); } catch (AggregateException) { }
        _cts?.Dispose();
    }

    // ------------------------------------------------------------------ sink

    private sealed class Sink(TrackerService s) : ITrackerSink
    {
        public void OnVisitClosed(Visit v)
        {
            s._batch.Visits.Add(v);
            lock (s._historyLock) s._history.Add(v);
            s._dirty = true;
        }

        public void OnVisitUpdated(Visit v)
        {
            s._batch.Visits.Add(v);
            lock (s._historyLock)
            {
                int i = s._history.FindLastIndex(x => x.Id == v.Id);
                if (i >= 0) s._history[i] = v;
            }
            s._dirty = true;
        }

        public void OnKill(KillRecord k)
        {
            s._batch.Kills.Add(k);
            lock (s._historyLock)
            {
                if (k.VisitId is { } id) { var x = s._party.GetValueOrDefault(id); s._party[id] = (x.Kills + 1, x.Party + (k.Party ? 1 : 0)); }
                else s._fieldKills.Add(k);
            }
            s._dirty = true;
        }

        public void OnLoot(LootRecord l) { s._batch.Loot.Add(l); s._dirty = true; }
        public void OnLevel(LevelRecord l) { s._batch.Levels.Add(l); s._dirty = true; }

        public void OnHighTier(HighTierDrop d)
        {
            s._highTier.Insert(0, d);
            if (s._highTier.Count > 25) s._highTier.RemoveAt(s._highTier.Count - 1);
            // Only call out drops as they happen, not ones found while catching up on an old log.
            if (s._live && !s._importing && (DateTime.Now - d.Time).TotalMinutes < 3) s.HighTierDropped?.Invoke(d);
        }
    }

    private sealed class InlineProgress(Action<long> report) : IProgress<long>
    {
        public void Report(long value) => report(value);
    }
}
