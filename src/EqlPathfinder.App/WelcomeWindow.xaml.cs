using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Pages;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;
using Microsoft.Web.WebView2.Core;

namespace EqlPathfinder.App;

/// <summary>FirstRun: install, pick a character, read its history. Change: the same without the welcome. Tour: the running copy, nothing re-read.</summary>
public enum WelcomeMode { FirstRun, Change, Tour }

/// <summary>
/// The welcome tour (web/welcome.html): what Pathfinder does, turning the log on (with a live check), reincarnations from
/// before the log, and the overlay. In FirstRun and Change it also picks the character and reads its history,
/// which starts as soon as you leave the log step and runs while you read the rest.
/// </summary>
public partial class WelcomeWindow : Window
{
    /// <summary>Bump when the tour gains something every existing copy should see once.</summary>
    public const int TourVersion = 1;

    private readonly App _app;
    private readonly PathfinderStore _store;
    private readonly AppSettings _settings;
    private readonly TrackerOptions _options;
    private readonly WelcomeMode _mode;
    private readonly DispatcherTimer _poll;
    private IReadOnlyList<CharacterLogs> _chars = [];
    private string _dir;
    private string? _selected;
    private TrackerService? _svc;
    private Task? _import;
    private CancellationTokenSource? _importCts;
    private bool _ready, _finished;
    private int _lastPercent = -1;

    /// <summary>The character's tracker, history read (FirstRun and Change, after Finish).</summary>
    public TrackerService? Service { get; private set; }

    private string UsagePrefix => _mode switch { WelcomeMode.FirstRun => "setup", WelcomeMode.Change => "change", _ => "tour" };

    public WelcomeWindow(App app, PathfinderStore store, AppSettings settings, TrackerOptions options, WelcomeMode mode)
    {
        InitializeComponent();
        Chrome.Apply(this);
        Chrome.FillWordmark(TitleWordmark);
        TitleClose.Click += (_, _) => Close();
        TitleMin.Click += (_, _) => WindowState = WindowState.Minimized;
        TitleNote.Text = mode == WelcomeMode.Change ? "change character" : "welcome";
        _app = app;
        _store = store;
        _settings = settings;
        _options = options;
        _mode = mode;
        _dir = settings.LogsDir is { } d && Directory.Exists(d) ? d : LogDiscovery.FindLogsDir() ?? "";
        _selected = mode == WelcomeMode.Tour ? app.Service?.Logs.Key : settings.Character;
        if (mode == WelcomeMode.Tour) _svc = app.Service;

        // The log step watches for the log to appear or start moving, so typing /log on shows up here within seconds.
        _poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _poll.Tick += (_, _) => SendCharacters();
        Loaded += async (_, _) => await InitAsync();
        Closing += (_, _) => OnClosing();
    }

    private async Task InitAsync()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(AppSettings.DataDir, "WebView2"));
            await Web.EnsureCoreWebView2Async(env);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            Web.Visibility = Visibility.Collapsed;
            Fallback.Visibility = Visibility.Visible;
            Fallback.Text = "Pathfinder needs the Microsoft Edge WebView2 Runtime, which comes with Windows 11. " +
                            "Install it from https://developer.microsoft.com/microsoft-edge/webview2/ and start Pathfinder again.";
            return;
        }
        var core = Web.CoreWebView2;
        WebViewSecurity.Configure(core, "welcome.html", OnMessage);
        if (Environment.GetEnvironmentVariable("EQLPATHFINDER_SELFTEST") == "1")
            core.NavigationCompleted += async (_, _) => await core.ExecuteScriptAsync(SelfTestScript.Replace("__USAGE__", Environment.GetEnvironmentVariable("EQLPATHFINDER_SELFTEST_USAGE") is "yes" or "quit" ? Environment.GetEnvironmentVariable("EQLPATHFINDER_SELFTEST_USAGE")! : "no"));
        core.Navigate(WebViewPolicy.AppUrl("welcome.html"));
    }

    /// <summary>
    /// Developer check (EQLPATHFINDER_SELFTEST=1 with EQLPATHFINDER_DATA): clicks through the whole tour like a new user,
    /// adding one Berserker reincarnation, then starts tracking. EQLPATHFINDER_SELFTEST_USAGE=yes|quit answers the Usage step Yes, or stops on it unanswered (No otherwise). App checks the result and writes selftest.json.
    /// </summary>
    private const string SelfTestScript = """
        (async () => {
          const wait = ms => new Promise(r => setTimeout(r, ms));
          const until = async (f, t = 180000) => { const s = Date.now(); while (!f()) { if (Date.now() - s > t) throw new Error('timeout'); await wait(200); } };
          const next = () => document.querySelector('#next').click();
          await until(() => chars && chars.characters.length && chars.selected);
          next(); await wait(300);                       // Welcome -> Usage
          if ('__USAGE__' === 'quit') return;               // stays on step 2, unanswered: the developer closes the window
          document.querySelector('[data-usage=__USAGE__]').click(); await wait(300);
          next(); await wait(300);                       // Usage (No) -> Your log
          next(); await wait(300);                       // Use <character>: starts reading -> Reincarnations
          await until(() => read.done);
          document.querySelector('[data-answer=yes]').click(); await wait(200);
          document.querySelector('[data-count="class:BER"].add').click(); await wait(1500);
          next(); await wait(200);                       // Overlay
          document.querySelector('[data-size=Compact]').click(); await wait(200);
          next(); await wait(200);                       // Ready
          await until(() => !document.querySelector('#next').disabled);
          next();                                        // Start tracking
        })();
        """;

    // ------------------------------------------------------------------ to the page

    private void Post(object o)
    {
        if (!_ready || Web.CoreWebView2 == null) return;
        try { Web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(o, PageJson.Options)); }
        catch (Exception ex) { AppSettings.LogError("welcome post", ex); }
    }

    /// <summary>While sharing is invite-only, the "Together" step asks for the code with the choice.</summary>
    private async Task SendShareConfigAsync()
    {
        if (_settings.ShareEnabled) return;
        if (await SharingService.RequiresCodeAsync(_settings) == true) Post(new { kind = "shareConfig", requireCode = true });
    }

    private void SendInit()
    {
        _ready = true;
        Post(new
        {
            kind = "init",
            mode = _mode.ToString().ToLowerInvariant(),
            version = _app.AppVersion,
            settings = new
            {
                overlaySize = _settings.Overlay(OverlayKinds.Crawl).Option("size", "Compact"),
                overlayVisible = _settings.Overlay(OverlayKinds.Crawl).Visible,
                beta = _settings.Beta,
                shareEnabled = _settings.ShareEnabled
            },
            usage = _app.Usage?.Step(),
        });
        SendCharacters();
        _poll.Start();
        _ = SendShareConfigAsync();
        if (_mode == WelcomeMode.Tour && _svc != null) _ = SendRosterAsync(_svc);
    }

    /// <summary>Which Usage answer is chosen (none while unanswered) and whether No's line deletes: the engine's rule, the page shows it.</summary>
    private void SendUsage() => Post(new { kind = "usage", usage = _app.Usage?.Step() });

    private void SendCharacters()
    {
        var dir = _dir.Trim();
        bool exists = dir.Length > 0 && Directory.Exists(dir);
        _chars = exists ? SafeFind(dir) : [];
        if (_mode == WelcomeMode.Tour) _chars = _chars.Where(c => c.Key.Equals(_selected, StringComparison.OrdinalIgnoreCase)).ToList();
        // Default to the character played most recently.
        if (_selected == null || !_chars.Any(c => c.Key.Equals(_selected, StringComparison.OrdinalIgnoreCase)))
            _selected = _chars.OrderByDescending(LastWrite).FirstOrDefault()?.Key;
        var now = DateTime.Now;
        Post(new
        {
            kind = "chars",
            logsDir = dir,
            dirOk = exists,
            gameRunning = GameWatcher.IsRunning(),
            selected = _selected,
            locked = _mode == WelcomeMode.Tour,
            characters = _chars.OrderByDescending(LastWrite).Select(c =>
            {
                var last = LastWrite(c);
                return new
                {
                    key = c.Key,
                    name = c.Name,
                    server = c.Server.Length > 0 ? char.ToUpperInvariant(c.Server[0]) + c.Server[1..] : "",
                    ageSeconds = last == DateTime.MinValue ? (double?)null : Math.Max(0, (now - last).TotalSeconds),
                    mb = Math.Round(c.Files.Sum(SafeLength) / 1e6, 1),
                    files = c.Files.Count,
                };
            }).ToList(),
        });
    }

    private static DateTime LastWrite(CharacterLogs c)
    {
        try { return File.Exists(c.LivePath) ? File.GetLastWriteTime(c.LivePath) : DateTime.MinValue; }
        catch (IOException) { return DateTime.MinValue; }
    }

    private static long SafeLength(string f) { try { return new FileInfo(f).Length; } catch (IOException) { return 0; } }

    private static IReadOnlyList<CharacterLogs> SafeFind(string dir)
    {
        try { return LogDiscovery.FindCharacters(dir); } catch (IOException) { return []; } catch (UnauthorizedAccessException) { return []; }
    }

    private async Task SendRosterAsync(TrackerService svc)
    {
        try
        {
            var data = await svc.Query(s => new { roster = s.RosterCounts(), visits = s.History(100_000).Count });
            Post(new { kind = "imported", ok = true, character = svc.Logs.Name, data.roster, data.visits });
        }
        catch (Exception ex) { AppSettings.LogError("welcome roster", ex); }
    }

    // ------------------------------------------------------------------ from the page

    private void OnMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var m = doc.RootElement;
            if (m.ValueKind == JsonValueKind.String)
            {
                if (m.GetString() == "ready") SendInit();
                return;
            }
            switch (m.GetProperty("type").GetString())
            {
                case "browse": Browse(); break;
                case "select" when _mode != WelcomeMode.Tour:
                    _selected = m.GetProperty("key").GetString();
                    SendCharacters();
                    break;
                case "import": StartImport(); break;
                case "counts": SaveCounts(m.GetProperty("value")); break;
                case "countsReset": SaveCounts(null); break;
                case "setting":
                    var settingKey = m.GetProperty("key").GetString() ?? "";
                    _app.ApplySetting(settingKey, m.GetProperty("value"));
                    if (settingKey == "usage") SendUsage();
                    break;
                // The Usage step's "See exactly what's sent": the app's own preview, as Settings shows it. A message of this window, never core.js's.
                case "usagePreview": Post(new { kind = "usagePreview", text = _app.Usage?.PreviewText() ?? "" }); break;
                // The Usage step counts only while counts aren't off: a first run's is held, and kept only by a Yes.
                case "step" when m.TryGetProperty("key", out var uk) && uk.GetString() == "usage":
                    if (_app.Usage?.Start != EqlPathfinder.Engine.Usage.UsageStart.Off) UsageService.Metrics.Feature(UsagePrefix + "-usage");
                    break;
                case "copy":
                    try { Clipboard.SetText(m.GetProperty("value").GetString() ?? ""); } catch (Exception) { /* clipboard busy */ }
                    break;
                case "share": _app.ChooseSharing(m.TryGetProperty("code", out var sc) && sc.ValueKind == JsonValueKind.String ? sc.GetString() : null); break;
                // Usage counts: how far a setup or tour gets ("setup-log", "tour-share"…), its steps' own keys only.
                case "step" when m.TryGetProperty("key", out var sk) && sk.GetString() is "welcome" or "log" or "reinc" or "overlay" or "goal" or "share" or "ready":
                    UsageService.Metrics.Feature(UsagePrefix + "-" + sk.GetString());
                    break;
                case "finish":
                    UsageService.Metrics.Feature(UsagePrefix + "-done");
                    Dispatcher.BeginInvoke(Finish);
                    break;
                case "changeCharacter":
                    // Deferred: closing this window inside WebView2's own callback isn't allowed.
                    Dispatcher.BeginInvoke(() => { Close(); _app.ChangeCharacter(); });
                    break;
            }
        }
        catch (Exception ex) { AppSettings.LogError("welcome message", ex); }
    }

    private void Browse()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Pick the EverQuest Legends folder (or its Logs folder)",
            InitialDirectory = Directory.Exists(_dir) ? _dir : null,
        };
        if (dlg.ShowDialog(this) != true) return;
        var logs = Path.Combine(dlg.FolderName, "Logs");
        _dir = Directory.Exists(logs) ? logs : dlg.FolderName;
        _selected = null;
        SendCharacters();
    }

    /// <summary>Reads the chosen character's history in the background while you go through the rest of the tour.</summary>
    private void StartImport()
    {
        if (_mode == WelcomeMode.Tour || _selected is not { } key) return;
        var c = _chars.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (c == null) return;
        if (_svc != null && _svc.Logs.Key.Equals(key, StringComparison.OrdinalIgnoreCase) && _import != null)
        {
            // Same character as before (you went back and forward again): keep the read that's running or done.
            if (_import.IsCompleted) _ = SendRosterAsync(_svc);
            return;
        }
        CancelImport();
        _options.EnteredCounts = _settings.CountsFor(key);
        var svc = _svc = new TrackerService(_store, c, _options);
        _importCts = new CancellationTokenSource();
        _lastPercent = -1;
        var progress = new Progress<double>(p =>
        {
            if ((int)p == _lastPercent) return;
            _lastPercent = (int)p;
            Post(new { kind = "progress", percent = p, character = c.Name });
        });
        Post(new { kind = "progress", percent = 0.0, character = c.Name });
        _import = svc.ImportHistoryAsync(progress, _importCts.Token);
        _import.ContinueWith(t => Dispatcher.BeginInvoke(() =>
        {
            if (svc != _svc) return;
            if (t.IsFaulted)
            {
                AppSettings.LogError("import", t.Exception!.GetBaseException());
                Post(new { kind = "imported", ok = false, error = t.Exception!.GetBaseException().Message });
            }
            else if (!t.IsCanceled) _ = SendRosterAsync(svc);
        }), TaskScheduler.Default);
    }

    private void CancelImport()
    {
        if (_svc == null || _mode == WelcomeMode.Tour) return;
        try
        {
            _importCts?.Cancel();
            _import?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException) { /* cancelled */ }
        _svc.Dispose();
        _svc = null;
        _import = null;
    }

    /// <summary>Your reincarnation totals so far ("class:WAR": 2), or null to go back to the log alone.</summary>
    private void SaveCounts(JsonElement? changes)
    {
        var key = _svc?.Logs.Key ?? _selected;
        if (key == null) return;
        var counts = _settings.CountsFor(key);
        if (changes is not { } ch) counts.Clear();
        else
            foreach (var p in ch.EnumerateObject())
                counts[p.Name] = new EnteredCount(Math.Clamp(p.Value.GetInt32(), 0, 99), DateTime.Now);
        _app.SaveCounts(key, counts, _svc);
        if (_svc != null && _import?.IsCompleted != false) _ = SendRosterAsync(_svc);
    }

    /// <summary>Home's one-time notices the tour itself tells (web/js/notices.js): someone who went through it doesn't get them again.</summary>
    private static readonly string[] ToldByTour = ["usage-counts"];

    /// <summary>
    /// What finishing the tour leaves in the settings: the tour seen, the notices it told, and no sharing card on Home.
    /// <see cref="Finish"/> runs it, and so does the --snapshot preview, whose pages are those of a new player who took
    /// the tour: a list of its own there would miss the next notice the tour tells.
    /// </summary>
    internal static void MarkTourFinished(AppSettings settings)
    {
        settings.WelcomeSeen = TourVersion;
        foreach (var id in ToldByTour) if (!settings.NoticesSeen.Contains(id)) settings.NoticesSeen.Add(id);
        settings.ShareIntroSeen = true;   // the tour asked about sharing and the beta: no Home card
    }

    private async void Finish()
    {
        MarkTourFinished(_settings);
        if (_mode == WelcomeMode.Tour)
        {
            _settings.Save();
            _finished = true;
            Close();
            return;
        }
        if (_svc == null || _import == null) return;
        try { await _import; } catch (Exception) { return; }
        if (_import.IsFaulted || _import.IsCanceled) return;
        _settings.LogsDir = _dir.Trim();
        _settings.Character = _svc.Logs.Key;
        Service = _svc;
        _finished = true;
        DialogResult = true;
    }

    private void OnClosing()
    {
        _poll.Stop();
        // Leaving setup without finishing: stop reading and let go of the half-read character.
        if (!_finished && _mode != WelcomeMode.Tour) CancelImport();
        if (_mode == WelcomeMode.Tour && !_finished)
        {
            _settings.WelcomeSeen = TourVersion;
            _settings.ShareIntroSeen = true;   // the tour asked about sharing and the beta: no Home card
            _settings.Save();
        }
    }
}
