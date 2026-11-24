using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EqlPathfinder.Engine.Usage;

/// <summary>One day's usage on this copy, as the app keeps it until the server has it (usage.json).</summary>
public sealed class UsageDay
{
    public string Day { get; set; } = "";
    public Dictionary<string, long> Counts { get; set; } = new();
    public Dictionary<string, UsageStat> Stats { get; set; } = new();
    public List<UsageError> Errors { get; set; } = new();
    /// <summary>What was on at the day's last check (sharing, the beta, overlays…): words, no numbers about play.</summary>
    public Dictionary<string, string> State { get; set; } = new();
    /// <summary>The app version that recorded the day last.</summary>
    public string? App { get; set; }
    /// <summary>The version this copy updated from that day, when it did.</summary>
    public string? UpdatedFrom { get; set; }
    /// <summary>A hash of the last summary of this day the server accepted (null = not sent yet).</summary>
    public string? SentHash { get; set; }
}

public sealed class UsageStat
{
    public long N { get; set; }
    public double Sum { get; set; }
    public double Max { get; set; }
}

public sealed class UsageError
{
    public string Kind { get; set; } = "";
    public string Type { get; set; } = "";
    public string At { get; set; } = "";
    public long N { get; set; }
}

/// <summary>Who's sending, as the summary states it.</summary>
/// <param name="Id">This copy's random usage id (32 hex), not the sharing key's id and not the voter id.</param>
/// <param name="FirstSeen">The day this copy first counted (yyyy-mm-dd).</param>
public sealed record UsageContext(string Id, string Channel, string Os, string? FirstSeen);

/// <summary>One day's summary, exactly as sent (POST /v1/usage, docs/usage.md).</summary>
public sealed record UsagePayload(int V, string Id, string Day, string App, string Channel, string Os, string? FirstSeen, string? UpdatedFrom,
    IReadOnlyDictionary<string, long> Counts, IReadOnlyDictionary<string, UsageStat> Stats, IReadOnlyDictionary<string, string> State,
    IReadOnlyList<UsageError> Errors);

/// <summary>
/// Listens to <see cref="UsageMetrics"/> and adds the measurements up per local day: counters by key, durations and sizes as
/// count / sum / max, errors by (what, type, where). Kept in a file until each day's summary reaches the server; a finished
/// day is dropped once sent, today is sent again when it has changed. Thread-safe: measurements arrive on any thread.
/// </summary>
public sealed class UsageRecorder : IDisposable
{
    public const int SchemaVersion = 1;
    /// <summary>Distinct errors kept per day; more only add to the "error.more" counter.</summary>
    public const int MaxErrors = 20;
    /// <summary>Distinct counter keys per day (the server takes up to 200).</summary>
    public const int MaxCounts = 150;
    /// <summary>Unsent days older than this are dropped rather than sent late.</summary>
    public const int KeepDays = 14;

    private sealed class FileShape
    {
        public string? LastVersion { get; set; }
        public List<UsageDay> Days { get; set; } = new();
    }

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly object _lock = new();
    private readonly string? _path;
    private readonly Func<DateTime> _now;
    private readonly MeterListener _listener;
    private FileShape _file;
    private string? _app;   // null until StartVersion: a day counted before it (a first-run setup) takes the version it is sent with
    private bool _dirty;
    private bool _held;   // the player hasn't answered the tour's Usage step: counted in memory, nothing written or pending
    private bool _off;    // answered No: nothing is counted

    /// <param name="path">The file it keeps days in; null keeps them in memory only (tests).</param>
    /// <param name="metrics">Whose instruments to listen to.</param>
    /// <param name="now">The clock (tests move it); local time.</param>
    /// <param name="held">The player hasn't answered the tour's Usage step yet (docs/specs/usage-choice.md): counts stay
    /// in memory, nothing is written or pending until <see cref="Choose"/>. It starts empty: a usage.json left behind isn't read.</param>
    public UsageRecorder(string? path, UsageMetrics metrics, Func<DateTime>? now = null, bool held = false)
    {
        _path = path;
        _now = now ?? (() => DateTime.Now);
        _held = held;
        _file = held ? new FileShape() : Load(path);
        _listener = new MeterListener
        {
            InstrumentPublished = (inst, l) => { if (ReferenceEquals(inst.Meter, metrics.Meter)) l.EnableMeasurementEvents(inst); },
        };
        _listener.SetMeasurementEventCallback<long>((inst, value, tags, _) => OnMeasurement(inst.Name, value, tags));
        _listener.SetMeasurementEventCallback<double>((inst, value, tags, _) => OnMeasurement(inst.Name, value, tags));
        _listener.Start();
    }

    /// <summary>The version that recorded last (from the file): a different one now means this copy was updated.</summary>
    public string? LastVersion
    {
        get { lock (_lock) return _file.LastVersion; }
    }

    /// <summary>The version recording from now on; returns the one before it (null on a copy's first count).</summary>
    public string? StartVersion(string app)
    {
        lock (_lock)
        {
            if (_off) return _file.LastVersion;
            var before = _file.LastVersion;
            _app = app;
            _file.LastVersion = app;
            var d = Today();
            d.App = app;
            if (before != null && before != app) d.UpdatedFrom = before;
            _dirty = true;
            return before;
        }
    }

    /// <summary>What's on right now, kept as today's state.</summary>
    public void SetState(IReadOnlyDictionary<string, string> state)
    {
        lock (_lock)
        {
            if (_off) return;
            var d = Today();
            d.State = state.Take(20).ToDictionary(kv => UsageKeys.Word(kv.Key), kv => kv.Value.Length > 40 ? kv.Value[..40] : kv.Value);
            if (_app != null) d.App = _app;
            _dirty = true;
        }
    }

    private void OnMeasurement(string instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (instrument == "pathfinder.errors")
        {
            string kind = "other", type = "", at = "";
            foreach (var t in tags)
                switch (t.Key)
                {
                    case "kind": kind = t.Value as string ?? kind; break;
                    case "type": type = t.Value as string ?? ""; break;
                    case "at": at = t.Value as string ?? ""; break;
                }
            AddError(kind, type, at, (long)value);
            return;
        }
        var key = UsageKeys.For(instrument, tags);
        if (key == null) return;
        lock (_lock)
        {
            if (_off) return;
            var d = Today();
            if (_app != null) d.App = _app;
            _dirty = true;
            if (instrument.StartsWith("pathfinder.import.", StringComparison.Ordinal))
            {
                var s = d.Stats.TryGetValue(key, out var x) ? x : d.Stats[key] = new UsageStat();
                s.N++;
                s.Sum += value;
                s.Max = Math.Max(s.Max, value);
                return;
            }
            if (!d.Counts.ContainsKey(key) && d.Counts.Count >= MaxCounts) key = "more";
            d.Counts[key] = d.Counts.GetValueOrDefault(key) + (long)value;
        }
    }

    private void AddError(string kind, string type, string at, long n)
    {
        lock (_lock)
        {
            if (_off) return;
            var d = Today();
            if (_app != null) d.App = _app;
            _dirty = true;
            var e = d.Errors.FirstOrDefault(x => x.Kind == kind && x.Type == type && x.At == at);
            if (e != null) e.N += n;
            else if (d.Errors.Count < MaxErrors) d.Errors.Add(new UsageError { Kind = kind, Type = type, At = at, N = n });
            else d.Counts["error.more"] = d.Counts.GetValueOrDefault("error.more") + n;
        }
    }

    private string DayOf(DateTime t) => t.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Today's day, made when the first thing happens on it. Call under the lock.</summary>
    private UsageDay Today()
    {
        var day = DayOf(_now());
        var d = _file.Days.FirstOrDefault(x => x.Day == day);
        if (d != null) return d;
        d = new UsageDay { Day = day, App = _app };   // App may be null until StartVersion
        _file.Days.Add(d);
        return d;
    }

    /// <summary>A copy of every day kept (oldest first).</summary>
    public IReadOnlyList<UsageDay> Days
    {
        get { lock (_lock) return _file.Days.OrderBy(d => d.Day, StringComparer.Ordinal).Select(Copy).ToList(); }
    }

    /// <summary>Today's summary as it would be sent now (Settings → See what's sent).</summary>
    public UsagePayload Preview(UsageContext who)
    {
        lock (_lock) return Payload(Today(), who);
    }

    /// <summary>
    /// The summaries the server hasn't got: finished days not sent, and today when it changed since it was sent. Days older
    /// than <see cref="KeepDays"/> are dropped here, unsent.
    /// </summary>
    public List<(UsagePayload Payload, string Hash)> Pending(UsageContext who)
    {
        lock (_lock)
        {
            if (_held) return new List<(UsagePayload, string)>();
            var oldest = DayOf(_now().AddDays(-KeepDays));
            if (_file.Days.RemoveAll(d => string.CompareOrdinal(d.Day, oldest) < 0) > 0) _dirty = true;
            var list = new List<(UsagePayload, string)>();
            foreach (var d in _file.Days.OrderBy(d => d.Day, StringComparer.Ordinal))
            {
                var p = Payload(d, who);
                var h = Hash(p);
                if (h != d.SentHash) list.Add((p, h));
            }
            return list;
        }
    }

    /// <summary>The server has this day's summary: a finished day is dropped, today keeps counting.</summary>
    public void MarkSent(string day, string hash)
    {
        lock (_lock)
        {
            var d = _file.Days.FirstOrDefault(x => x.Day == day);
            if (d == null) return;
            if (string.CompareOrdinal(day, DayOf(_now())) < 0) _file.Days.Remove(d);
            else d.SentHash = hash;
            _dirty = true;
        }
    }

    /// <summary>
    /// The player's answer on the tour's Usage step (docs/specs/usage-choice.md). Yes keeps what was counted and counts,
    /// keeps and sends from now on; No empties it, deletes the file and counts nothing until a Yes.
    /// </summary>
    public void Choose(bool yes)
    {
        lock (_lock)
        {
            if (yes)
            {
                _held = _off = false;
                _dirty = true;
                return;
            }
            _held = _off = true;
            _file = new FileShape();
            _dirty = false;
            if (_path != null) { try { File.Delete(_path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
    }

    /// <summary>True while the player hasn't answered (or has said No): nothing is kept or sent.</summary>
    public bool Held { get { lock (_lock) return _held; } }

    /// <summary>Forgets everything kept (usage switched off): the file goes too.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _file = new FileShape { LastVersion = _file.LastVersion };
            _dirty = false;
            if (_path != null) { try { File.Delete(_path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
    }

    /// <summary>Writes the file if anything changed since the last save.</summary>
    public void Save()
    {
        if (_path == null) return;
        string text;
        lock (_lock)
        {
            if (_held || !_dirty) return;
            text = JsonSerializer.Serialize(_file, Json);
            _dirty = false;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, text);
            File.Move(tmp, _path, overwrite: true);
        }
        catch (IOException) { lock (_lock) _dirty = true; }
        catch (UnauthorizedAccessException) { lock (_lock) _dirty = true; }
    }

    public void Dispose()
    {
        _listener.Dispose();
        Save();
    }

    private UsagePayload Payload(UsageDay d, UsageContext? who) => new(
        SchemaVersion, who?.Id ?? "", d.Day, d.App ?? _app ?? "development", who?.Channel ?? "stable", who?.Os ?? "", who?.FirstSeen, d.UpdatedFrom,
        new SortedDictionary<string, long>(d.Counts, StringComparer.Ordinal),
        new SortedDictionary<string, UsageStat>(d.Stats.ToDictionary(kv => kv.Key, kv => new UsageStat { N = kv.Value.N, Sum = Math.Round(kv.Value.Sum, 1), Max = Math.Round(kv.Value.Max, 1) }), StringComparer.Ordinal),
        new SortedDictionary<string, string>(d.State, StringComparer.Ordinal),
        d.Errors.Select(e => new UsageError { Kind = e.Kind, Type = e.Type, At = e.At, N = e.N }).ToList());

    public static string ToJson(UsagePayload p, bool indented = false) =>
        JsonSerializer.Serialize(p, indented ? new JsonSerializerOptions(Json) { WriteIndented = true } : Json);

    private static string Hash(UsagePayload p) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ToJson(p))))[..16].ToLowerInvariant();

    private static UsageDay Copy(UsageDay d) => new()
    {
        Day = d.Day,
        App = d.App,
        UpdatedFrom = d.UpdatedFrom,
        SentHash = d.SentHash,
        Counts = new(d.Counts),
        State = new(d.State),
        Stats = d.Stats.ToDictionary(kv => kv.Key, kv => new UsageStat { N = kv.Value.N, Sum = kv.Value.Sum, Max = kv.Value.Max }),
        Errors = d.Errors.Select(e => new UsageError { Kind = e.Kind, Type = e.Type, At = e.At, N = e.N }).ToList(),
    };

    private static FileShape Load(string? path)
    {
        if (path == null || !File.Exists(path)) return new FileShape();
        try { return JsonSerializer.Deserialize<FileShape>(File.ReadAllText(path), Json) ?? new FileShape(); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return new FileShape(); }
    }
}
