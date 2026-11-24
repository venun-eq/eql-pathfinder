using System.Globalization;

namespace EqlPathfinder.Engine.Usage;

/// <summary>The part of settings.json that holds the player's usage answer and this copy's id (AppSettings implements it).</summary>
public interface IUsageSettings
{
    /// <summary>Whether the player has answered the tour's Usage step: false on a new copy, null in a file from before the question.</summary>
    bool? UsageChosen { get; set; }
    bool UsageEnabled { get; set; }
    /// <summary>This copy's random usage id (32 hex), made in <see cref="UsageSwitch.Begin"/> and nowhere else.</summary>
    string? UsageId { get; set; }
    /// <summary>The day this copy started counting (yyyy-mm-dd).</summary>
    string? UsageSince { get; set; }
    /// <summary>The ids whose rows the server must still delete, oldest first: set by a No, cleared when the server answers. Replaced whole, never changed in place. A file without the field reads null.</summary>
    string[] UsageForget { get; set; }
    void Save();
}

/// <summary>What <see cref="UsageSwitch.SetEnabled"/> asks the app to do next.</summary>
/// <param name="Began">Counting began now (a Yes after the app started): start the timers.</param>
/// <param name="Stopped">Counting stopped: stop the timers.</param>
/// <param name="ForgetId">The id just retired, now in <see cref="IUsageSettings.UsageForget"/>: ask the server now. Null when this copy never had an id.</param>
public readonly record struct UsageSwitched(bool Began, bool Stopped, string? ForgetId);

/// <summary>
/// The rules for what usage counts keep and send (docs/specs/usage-choice.md): who owns the recorder, when the id is made,
/// what a Yes or No does to what setup counted. The app's UsageService keeps the timers and the HTTP.
/// </summary>
public sealed class UsageSwitch : IDisposable
{
    private readonly IUsageSettings _s;
    private readonly string? _path;
    private readonly UsageMetrics _metrics;
    private readonly Func<string> _newId;
    private readonly Func<DateTime> _now;
    private string _version = "development";
    private bool _begun;

    /// <param name="newId">Makes an id (32 hex): called in <see cref="Begin"/> only, when there is none.</param>
    public UsageSwitch(IUsageSettings settings, string? path, UsageMetrics metrics, Func<string> newId, Func<DateTime>? now = null)
    {
        _s = settings;
        _path = path;
        _metrics = metrics;
        _newId = newId;
        _now = now ?? (() => DateTime.Now);
    }

    public UsageRecorder? Recorder { get; private set; }

    /// <summary>The app has started and told <see cref="Begin"/> its version.</summary>
    public bool Begun => _begun;

    /// <summary>Where this copy stands: asked (the Usage step is unanswered), on or off.</summary>
    public UsageStart Start => UsageChoice.Read(_s.UsageChosen, _s.UsageEnabled);

    /// <summary>The Usage step's state for the window.</summary>
    public UsageStep Step() => UsageChoice.Step(Start, _s.UsageId is { Length: 32 });

    /// <summary>Starts counting, right after settings load: held in memory while the copy hasn't answered.</summary>
    public void Listen()
    {
        if (Recorder != null) return;
        var start = Start;
        if (start == UsageStart.Off)
        {
            // Switched off under a build that never retried its delete: ask once more.
            if (_s.UsageId is { Length: > 0 } old) Retire(old);
            return;
        }
        Recorder = new UsageRecorder(_path, _metrics, _now, held: start == UsageStart.Ask);
    }

    /// <summary>
    /// The app is up and knows its version. Returns whether counting begins (start the timers). A recorder still held is
    /// dropped as a No, without recording an answer. The id is made here and nowhere else.
    /// </summary>
    public bool Begin(string version, bool firstRun, bool starting = true)
    {
        _version = version;
        _begun = true;
        if (Recorder is not { } r) return false;
        if (r.Held)
        {
            r.Choose(false);
            r.Dispose();
            Recorder = null;
            return false;
        }
        if (_s.UsageId is not { Length: 32 })
        {
            _s.UsageId = _newId();
            _s.UsageSince = _now().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            _s.Save();
        }
        var before = r.StartVersion(version);
        if (starting) _metrics.Life("start");
        if (firstRun) _metrics.Life("firstrun");
        else if (before == null) _metrics.Life("seen");          // a copy from before usage counts, reporting for the first time
        else if (before != version) _metrics.Life("updated");
        return true;
    }

    /// <summary>The answer, whoever gives it: the tour's Usage step, Settings' switch or the Home banner.</summary>
    public UsageSwitched SetEnabled(bool on)
    {
        bool answered = _s.UsageChosen == false;
        if (answered) _s.UsageChosen = true;   // before the "already so" return: a first-run No finds the switch already off
        if (_s.UsageEnabled == on)
        {
            if (answered) _s.Save();
            if (!on && Recorder is { Held: true } held)
            {
                held.Choose(false);   // a first-run No: what setup counted is dropped unsent
                held.Dispose();
                Recorder = null;
            }
            return default;
        }
        _s.UsageEnabled = on;
        string? retired = null;
        if (!on && _s.UsageId is { Length: 32 } id)
        {
            retired = id;
            Retire(id, save: false);   // in the same save as the switch: closing mid-request can't lose it
        }
        _s.Save();
        if (on)
        {
            if (Recorder is { Held: true } h) h.Choose(true);   // Yes keeps what setup counted
            else Listen();
            return new(_begun && Begin(_version, firstRun: false, starting: false), false, null);
        }
        Recorder?.Clear();
        Recorder?.Dispose();
        Recorder = null;
        return new(false, true, retired);
    }

    /// <summary>Moves an id from "counting under" to "still to delete"; a Yes later gets a new one in <see cref="Begin"/>.</summary>
    private void Retire(string id, bool save = true)
    {
        _s.UsageForget = [.. _s.UsageForget ?? [], id];
        _s.UsageId = null;
        _s.UsageSince = null;
        if (save) _s.Save();
    }

    /// <summary>
    /// May a send round that started with <paramref name="started"/> still post? Not once the recorder is gone or replaced
    /// (a No came in), so nothing is posted after the delete has gone out.
    /// </summary>
    public bool MayPost(UsageRecorder started) => ReferenceEquals(Recorder, started) && !started.Held;

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// One send round: posts every day the server hasn't got, one POST at a time, and stops before any POST once a No has
    /// come in. Returns the number posted; null when another round or a forget holds the gate; -1 when the copy is off
    /// (or went off). Errors from <paramref name="post"/> pass through to the caller.
    /// </summary>
    public async Task<int?> SendRoundAsync(Func<UsageContext> who, Dictionary<string, string>? state, Func<UsagePayload, Task> post)
    {
        if (Recorder is not { } r) return -1;
        if (!await _gate.WaitAsync(0, CancellationToken.None).ConfigureAwait(false)) return null;
        try
        {
            if (!MayPost(r)) return -1;   // a No came in while this round waited
            if (state != null) r.SetState(state);
            int n = 0;
            foreach (var (payload, hash) in r.Pending(who()))
            {
                if (!MayPost(r)) return -1;   // a No came in: nothing goes after the delete
                await post(payload).ConfigureAwait(false);
                r.MarkSent(payload.Day, hash);
                n++;
            }
            if (MayPost(r)) r.Save();
            return n;
        }
        finally { _gate.Release(); }
    }

    /// <summary>A delete the server answered: the id leaves the list and settings are saved (UI thread).</summary>
    public void Forgotten(string id)
    {
        if (!(_s.UsageForget ?? []).Contains(id)) return;
        _s.UsageForget = [.. _s.UsageForget!.Where(x => x != id)];
        _s.Save();
    }

    private static bool IsId(string? id) => id is { Length: 32 } && id.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    /// <summary>
    /// One attempt at the pending deletes, once a round in flight has finished (its next day sees the No and stops), so the
    /// delete is the last word. Oldest first, one at a time: <paramref name="forgotten"/> clears each id the server answered
    /// (on the thread that saves settings), and the first failure passes through and keeps the rest. An entry the server
    /// would refuse is dropped without a request.
    /// </summary>
    public async Task ForgetAsync(Func<string, Task> delete, Action<string> forgotten)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var id in (_s.UsageForget ?? []).ToArray())
            {
                if (IsId(id)) await delete(id).ConfigureAwait(false);
                forgotten(id);
            }
        }
        finally { _gate.Release(); }
    }

    /// <summary>Is a delete waiting to be asked?</summary>
    public bool ForgetPending => _s.UsageForget is { Length: > 0 };

    public void Dispose()
    {
        Recorder?.Dispose();
        Recorder = null;
    }
}
