using System.IO;
using System.Net.Http;
using EqlPathfinder.Engine.Sharing;
using EqlPathfinder.Engine.Usage;

namespace EqlPathfinder.App;

/// <summary>
/// Anonymous usage counts (docs/usage.md): off until the player says yes (the tour's Usage step; copies from before it keep their setting), switched in Settings → Character and data → "Send anonymous usage
/// counts". Counts pages, Ask questions, features, minutes running, imports and errors through <see cref="UsageMetrics"/>,
/// keeps them per day in usage.json, and sends each day's summary to the community server (one row per day, replaced as
/// it grows): 2 minutes after start, every hour while anything changed, and on closing. A random id (its own, not the
/// sharing key or the voter id) is the only link between days.
/// Switching it off forgets what's kept here and asks the server to delete what this copy sent.
/// <para>
/// It listens from the moment settings load (<see cref="Listen"/>), so a first run's setup steps count too (in memory only until the answer), and starts its
/// clock and sending once the app knows its version (<see cref="Begin"/>). A setup closed before the end is kept and sent
/// with the next start.
/// </para>
/// </summary>
internal sealed class UsageService : IDisposable
{
    private static readonly TimeSpan FirstSend = TimeSpan.FromMinutes(2), Every = TimeSpan.FromHours(1);
    private static string FilePath => Path.Combine(AppSettings.DataDir, "usage.json");

    private readonly App _app;
    private readonly AppSettings _settings;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private Task _forgetting = Task.CompletedTask;
    private string _version = "development";
    private readonly UsageSwitch _switch;
    private Timer? _minute, _send, _forget;
    private int _ticks;

    public UsageService(App app, AppSettings settings)
    {
        _app = app;
        _settings = settings;
        _switch = new UsageSwitch(settings, FilePath, Metrics, NewId);
    }

    public static UsageMetrics Metrics => UsageMetrics.Default;

    /// <summary>
    /// Only real copies send: a development build or a developer's isolated copy (EQLPATHFINDER_DATA) counts and shows the
    /// preview but sends nothing, unless EQLPATHFINDER_USAGE=1 asks it to (testing the server).
    /// </summary>
    private bool Sends => Environment.GetEnvironmentVariable("EQLPATHFINDER_USAGE") == "1"
        || (AppSettings.IsolatedDataDir == null && _version != "development");

    private Uri Api => new(Environment.GetEnvironmentVariable("EQLPATHFINDER_API") is { Length: > 0 } e ? e
        : _settings.ShareApi is { Length: > 0 } s ? s : ShareProtocol.DefaultApi);

    /// <summary>Where this copy stands: asked (the tour's Usage step is unanswered), on or off.</summary>
    public UsageStart Start => _switch.Start;

    /// <summary>The Usage step's state for the window: which answer is chosen (none on Ask) and whether No's line deletes.</summary>
    public UsageStep Step() => _switch.Step();

    /// <summary>Starts counting. Right after settings load, before any setup: held in memory while the copy hasn't answered.</summary>
    public void Listen() => _switch.Listen();

    private static string NewId() => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    /// <summary>
    /// The app is up and knows its version: note the start (and a first run, or an update since last time), then count
    /// minutes and send. <paramref name="firstRun"/>: this launch set the copy up.
    /// </summary>
    public void Begin(string version, bool firstRun)
    {
        _version = version;
        if (_switch.Begin(version, firstRun)) StartTimers();
        // A delete that didn't land is asked again, even by an Off copy: 2.5 minutes after the start (30 s after the send timer, so the two never take the gate together), then every hour.
        if (Sends) _forget ??= new Timer(_ => { if (_switch.ForgetPending) _forgetting = ForgetAsync(); }, null, FirstSend + TimeSpan.FromSeconds(30), Every);
    }

    private void StartTimers()
    {
        _ticks = 0;
        _minute ??= new Timer(_ => Tick(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        _send ??= new Timer(_ => _ = SendAsync(), null, FirstSend, Every);
    }

    /// <summary>The first-run setup was closed before it finished: kept, and sent with the next start.</summary>
    public void SetupQuit()
    {
        Metrics.Life("setupquit");
        _switch.Recorder?.Save();
    }

    /// <summary>The answer, whoever gives it: the tour's Usage step, Settings' switch or the Home banner.</summary>
    public void SetEnabled(bool on)
    {
        var r = _switch.SetEnabled(on);
        if (r.Began) StartTimers();
        if (r.Stopped) Stop();
        if (r.ForgetId != null && Sends) _forgetting = ForgetAsync();
    }


    /// <summary>Today's summary as it would be sent now, pretty-printed (Settings → See what's sent). UI thread.</summary>
    public string PreviewText()
    {
        if (_switch.Recorder is not { } r) return "Usage counts are off: nothing is kept or sent.";
        r.SetState(State());
        var text = UsageRecorder.ToJson(r.Preview(Who()), indented: true);
        return Sends ? text : "This copy is a development build: it counts but never sends.\n\n" + text;
    }

    /// <summary>Writes what's kept to disk now (before a crash takes the app down).</summary>
    public void Save() => _switch.Recorder?.Save();

    private void Tick()
    {
        try
        {
            Metrics.Minute(_app.GameRunning);
            if (++_ticks % 5 == 0)
            {
                var state = _app.Dispatcher.Invoke(State);
                _switch.Recorder?.SetState(state);
                _switch.Recorder?.Save();
            }
        }
        catch (Exception ex) when (ex is TaskCanceledException or InvalidOperationException) { /* shutting down */ }
    }

    /// <summary>What's switched on, as words (UI thread): no numbers about play, nothing about the character.</summary>
    private Dictionary<string, string> State() => new()
    {
        ["sharing"] = _settings.ShareEnabled ? (_settings.SharePaused ? "paused" : "on") : "off",
        ["beta"] = _settings.Beta ? "yes" : "no",
        ["overlays"] = string.Join(",", _settings.Overlays.Where(kv => kv.Value.Visible).Select(kv => UsageKeys.Word(kv.Key)).Order()) is { Length: > 0 } o ? o : "none",
        ["start"] = UsageKeys.Word(_settings.StartWindow),
        ["goal"] = UsageKeys.Word(_settings.Goal),
        ["updates"] = _app.UpdatesEnabled ? "yes" : "no",
    };

    private UsageContext Who() => new(_settings.UsageId ?? "", _settings.Beta ? "beta" : "stable",
        Environment.OSVersion.Version.ToString(), _settings.UsageSince);

    /// <summary>The timer's round (any thread): today's state from the UI thread, then whatever the server hasn't got.</summary>
    private async Task SendAsync()
    {
        if (!Sends || _switch.Recorder == null) return;
        Dictionary<string, string> state;
        try { state = await _app.Dispatcher.InvokeAsync(State); }
        catch (TaskCanceledException) { return; }
        await SendCoreAsync(state, CancellationToken.None);
    }

    /// <summary>Sends every day the server hasn't got (only what changed since it last got it); says how it went.</summary>
    private async Task<string> SendCoreAsync(Dictionary<string, string>? state, CancellationToken ct)
    {
        if (!Sends) return "not sending (development copy)";
        try
        {
            var client = new UsageClient(_http, Api, _version);
            return await _switch.SendRoundAsync(Who, state, p => client.SendAsync(p, ct)) switch
            {
                null => "busy",
                -1 => "off",
                var n => $"sent {n}",
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return "not sent: " + ex.Message;   // offline or the server is busy: the days wait for the next round
        }
        catch (Exception ex)
        {
            AppSettings.LogError("usage send", ex);
            return "failed: " + ex.Message;
        }
    }

    /// <summary>
    /// Asks the server to delete every id still pending. Waits for a send round in flight (its next day sees the No and
    /// stops), so the delete is the last word. A 2xx clears the id (on the UI thread, which owns settings); anything else
    /// keeps it for the next try.
    /// </summary>
    private async Task ForgetAsync()
    {
        try
        {
            await _switch.ForgetAsync(i => new UsageClient(_http, Api, _version).ForgetAsync(i),
                id => _app.Dispatcher.BeginInvoke(() => _switch.Forgotten(id))).ConfigureAwait(false);
            LastForget = "deleted on the server";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ObjectDisposedException)
        {
            LastForget = "not reached: " + ex.Message;   // the server prunes it within the year anyway
        }
    }

    /// <summary>How the last delete on the server went (developer checks).</summary>
    internal string? LastForget { get; private set; }

    /// <summary>Sends what's pending now and says how it went (developer checks).</summary>
    internal Task<string> SendNowAsync() => SendCoreAsync(State(), CancellationToken.None);

    internal int PendingCount() => _switch.Recorder?.Pending(Who()).Count ?? 0;

    private void Stop()
    {
        _minute?.Dispose();
        _send?.Dispose();
        _minute = _send = null;
    }

    /// <summary>
    /// Closing (UI thread): what this session counted goes now rather than at the next start, which can be days away. At
    /// most 3 seconds, so closing never hangs; whatever doesn't make it is kept and goes next time.
    /// </summary>
    public void Dispose()
    {
        Stop();
        _forget?.Dispose();
        _forget = null;
        if (_switch.Begun && _switch.Recorder != null && Sends)
        {
            try
            {
                var state = State();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                Task.Run(() => SendCoreAsync(state, cts.Token)).Wait(TimeSpan.FromSeconds(3.5));
            }
            catch (Exception) { /* closing anyway */ }
        }
        try { _forgetting.Wait(TimeSpan.FromSeconds(3)); }   // a delete queued behind a send still goes before the client closes
        catch (Exception) { /* closing anyway */ }
        _switch.Dispose();
        _http.Dispose();
    }
}
