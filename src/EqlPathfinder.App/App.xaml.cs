using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Threading;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.App;

public partial class App : Application
{
    private AppSettings _settings = null!;
    private PathfinderStore? _store;
    private TrackerService? _service;
    private OverlayWindow? _overlay;
    private MainWindow? _main;
    private TrayIcon? _tray;
    private Updater? _updater;
    private SharingService? _sharing;
    private CommunityFeed? _community;
    private UsageService? _usage;
    private WelcomeWindow? _tour;
    private GameWatcher? _game;
    private EventWaitHandle? _showSignal;
    private RegisteredWaitHandle? _showWait;
    private DispatcherTimer? _autoUpdate;
    private ClientDebugLog? _clientLog;
    private bool _rebuilding;
    private bool _logOff;
    private bool _logOffWarned;
    private DateTime _lastChime;
    private DateTime _gameClosedAt = DateTime.Now;
    private readonly TrackerOptions _options = new();

    /// <summary>How long the game must have been closed (or Pathfinder running without it) before an update installs itself.</summary>
    private static readonly TimeSpan QuietBeforeUpdate = TimeSpan.FromMinutes(10);

    /// <summary>Set by Velopack's restart hook when this launch follows an in-app update.</summary>
    internal static string? UpdatedTo { get; set; }
    /// <summary>"Tray" or "Minimized" when this start follows an update Pathfinder installed by itself (see MaybeInstallUpdate).</summary>
    internal static string? StartOverride { get; set; }
    internal const string StartAfterUpdateArg = "--start-after-update";

    internal AppSettings Settings => _settings;
    internal TrackerService? Service => _service;
    internal SharingService? Sharing => _sharing;
    /// <summary>Usage counts, for the Settings preview.</summary>
    internal UsageService? Usage => _usage;
    internal LiveSnapshot? LastSnapshot { get; private set; }
    internal bool GameRunning => _game?.Running == true;
    internal bool UpdatesEnabled => _updater?.Enabled == true;
    internal bool OnBetaBuild => _updater?.OnBetaBuild == true;
    internal string? AppVersion => _updater?.CurrentVersion;
    internal string? PendingUpdate => _updater?.Pending?.Version.ToString();

    /// <summary>
    /// Certainly off: the game's dbg.txt shows the tracked character (name and server) logged in and finished loading a
    /// minute ago, and their log has had no line since. Anything less (login, character select, another character or
    /// server, no dbg.txt) never says so.
    /// </summary>
    internal bool LogOff => _logOff;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, ex) => { AppSettings.LogError("ui", ex.Exception); ex.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            AppSettings.LogError("fatal", (Exception)ex.ExceptionObject);
            _usage?.Save();   // the crash is counted before the app goes down
        };

        _settings = AppSettings.Load();
        _usage = new UsageService(this, _settings);
        _usage.Listen();   // from here, so a first run's setup steps count too
        _options.FragmentValue = _settings.FragmentValueNow;
        // Migrate legacy RouteMetric setting into the app-wide Goal.
        if (_settings.Goal == "balanced" && _settings.RouteMetric is "speed" or "motes") _settings.Goal = _settings.RouteMetric;
        _options.Goal = _settings.Goal;
        _options.FarmDifficulties = _settings.FarmDifficulties;
        _options.FarmRateMode = _settings.FarmRateMode;
        _options.FarmRateManual = _settings.FarmRate;
        _options.Plan = (_settings.Plan ?? new PlanPrefs()).Normalized();
        try { _store = new PathfinderStore(AppSettings.DatabasePath); }
        catch (Exception ex)
        {
            AppSettings.LogError("opening database", ex);
            MessageBox.Show($"EQL Pathfinder couldn't open its database:\n{ex.Message}", "EQL Pathfinder");
            Shutdown();
            return;
        }

        var svc = TryResume();
        bool setUp = svc == null;
        if (setUp && !RunSetup(WelcomeMode.FirstRun, out svc)) { _usage.SetupQuit(); _usage.Dispose(); Shutdown(); return; }
        StartTracking(svc!);
        StartUpdater();
        _sharing = new SharingService(this, _settings);
        _sharing.Changed += () => Dispatcher.BeginInvoke(() => _main?.SendShareStatus());
        if (_settings.SharePending) ChooseSharing(_settings.SharePendingCode);   // chosen in the welcome tour, before sharing could start
        _community = new CommunityFeed(this, _settings, AppVersion ?? "development");
        _community.Changed += () => Dispatcher.BeginInvoke(() =>
        {
            if (_service != null) _service.Community = _community.Current;
            _main?.RefreshAsk();
        });
        _community.LoadCached();
        _usage.Begin(AppVersion ?? "development", firstRun: setUp);
        ListenForSecondLaunch();
        switch (StartOverride ?? _settings.StartWindow)
        {
            case "Tray": break;
            case "Minimized": OpenMain(minimized: true); break;
            default: OpenMain(); break;
        }
        // Copies installed before the tour existed see it once (the friend whose log was off, for one).
        if (!setUp && _settings.WelcomeSeen < WelcomeWindow.TourVersion)
            Dispatcher.BeginInvoke(ShowTour, DispatcherPriority.ApplicationIdle);
        StartSelfTestIfRequested();
    }

    /// <summary>The welcome tour over the running copy (Help → Show the welcome tour).</summary>
    internal void ShowTour()
    {
        if (_service == null) return;
        if (_tour != null) { _tour.Activate(); return; }
        UsageService.Metrics.Feature("tour");
        _tour = new WelcomeWindow(this, _store!, _settings, _options, WelcomeMode.Tour);
        if (_main is { IsVisible: true } m) { _tour.Owner = m; _tour.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
        _tour.Closed += (_, _) => _tour = null;
        _tour.Show();
    }

    // ------------------------------------------------------------------ is the log on?

    private void CheckLog()
    {
        bool off = _game is { Running: true, StartedAt: { } st } && _service is { } svc && LastSnapshot is { Importing: false } s &&
                   LogOffCheck.IsOff(DateTime.Now, st, ReadClientLog(), svc.Logs.Name, svc.Logs.Server, s.LogTime);
        if (off == _logOff) return;
        _logOff = off;
        _main?.UpdateChrome();
        _tray?.Refresh();
        if (_overlay != null)
        {
            _overlay.LogOff = off;
            if (LastSnapshot != null && _overlay.IsVisible) _overlay.Update(LastSnapshot);
        }
        if (off && !_logOffWarned)
        {
            _logOffWarned = true;
            _tray?.Notify("Your EverQuest log is off",
                $"Pathfinder can't see {_service?.Logs.Name ?? "your character"} play until it's on. In EverQuest, type /log on (it stays on).");
        }
    }

    /// <summary>The game's dbg.txt, next to the tracked log: who entered the world this run, and when.</summary>
    private ClientSession? ReadClientLog()
    {
        if (Path.GetDirectoryName(_service?.Logs.LivePath) is not { Length: > 0 } dir) return null;
        var path = Path.Combine(dir, ClientDebugLog.FileName);
        if (_clientLog?.Path != path) _clientLog = new ClientDebugLog(path);
        return _clientLog.Read();
    }

    /// <summary>Launching Pathfinder again (the desktop shortcut) brings up this copy's window instead of a second copy.</summary>
    private void ListenForSecondLaunch()
    {
        try
        {
            _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ShowSignalName);
            _showWait = ThreadPool.RegisterWaitForSingleObject(_showSignal, (_, _) => Dispatcher.BeginInvoke(() => OpenMain()), null, -1, false);
        }
        catch (Exception ex) { AppSettings.LogError("second-launch signal", ex); }
    }

    /// <summary>The configured character, if its history is already imported.</summary>
    private TrackerService? TryResume()
    {
        if (_settings.LogsDir is not { } dir || _settings.Character is not { } key || !Directory.Exists(dir)) return null;
        var logs = LogDiscovery.FindCharacters(dir).FirstOrDefault(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (logs == null) return null;
        _options.EnteredCounts = _settings.CountsFor(logs.Key);
        var svc = new TrackerService(_store!, logs, _options);
        // A re-read clears the history first and saves at the end, so closing the app part way leaves none: a copy that
        // was set up before finishes reading (StartTracking) instead of greeting its player as new.
        if (svc.HasHistory || _settings.WelcomeSeen > 0) return svc;
        svc.Dispose();
        return null;
    }

    private bool RunSetup(WelcomeMode mode, out TrackerService? svc)
    {
        var w = new WelcomeWindow(this, _store!, _settings, _options, mode);
        bool ok = w.ShowDialog() == true && w.Service != null;
        svc = w.Service;
        if (ok) _settings.Save();
        return ok;
    }

    private void StartTracking(TrackerService svc)
    {
        _service = svc;
        svc.ModeSetting = _settings.Overlay(OverlayKinds.Crawl).Option("mode", "Auto");
        svc.Community = _community?.Current;
        svc.SharedFrom = () => _sharing?.SharedFrom;   // when you share, your own play leaves the community numbers you read
        _overlay ??= new OverlayWindow(_settings);
        _game ??= new GameWatcher();
        _game.Changed -= OnGameChanged;
        _game.Changed += OnGameChanged;
        svc.GameRunning = _game.Running;
        svc.SnapshotReady += snap =>
        {
            var before = LastSnapshot;
            LastSnapshot = snap;
            Dispatcher.BeginInvoke(CheckLog);
            // The fight meter labels fights with the app's trio (this runs on the tracker's thread, where facts are read).
            if (!svc.Importing && _meter is { } meter && svc.TrioTimeline() is var timeline && !ReferenceEquals(timeline, _sentTimeline))
            {
                _sentTimeline = timeline;
                meter.SetTrioTimeline(timeline);
            }
            // A hidden overlay isn't redrawn; it catches up when it shows.
            if (_overlay?.IsVisible == true) Dispatcher.BeginInvoke(() => _overlay?.Update(snap));
            // A ding (in or out of an instance) or a loadout change redraws the whole window, not just its title.
            if (_main != null && (before?.Level != snap.Level || before?.Trio != snap.Trio || before?.Race != snap.Race || before?.Loadout != snap.Loadout))
                Dispatcher.BeginInvoke(() => { if (_main?.IsVisible == true) _main.Refresh(); else _main?.UpdateChrome(); });
        };
        svc.HighTierDropped += drop => Dispatcher.BeginInvoke(() =>
        {
            _overlay?.ShowDrop(drop, _options.FragmentValue);
            // One chime per chest, not one per mote in it.
            if (_settings.SoundOnHighTier && (DateTime.Now - _lastChime).TotalSeconds > 2)
            {
                _lastChime = DateTime.Now;
                SystemSounds.Asterisk.Play();
            }
        });
        svc.Error += ex => AppSettings.LogError("tracker", ex);
        _tray ??= new TrayIcon(this);
        _tray.Refresh();
        if (svc.NeedsRebuild || !svc.HasHistory) _ = RebuildThenStartAsync(svc);
        else svc.Start();
        StartMeter();
        ApplyOverlayVisibility();
        if (!_game.Running) Dispatcher.BeginInvoke(GameWatcher.TrimMemory, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>History read by an older version is missing what this one tracks: re-read it once, then follow the log.</summary>
    private async Task RebuildThenStartAsync(TrackerService svc)
    {
        _rebuilding = true;
        if (!svc.HasHistory)
            _tray?.Notify("Reading your logs", "The last read was closed before it finished, so it's starting again. A few minutes.");
        else
            _tray?.Notify(UpdatedTo is { } v ? $"Updated to {v}: updating your history" : "Updating your history",
                "This version tracks more from your logs, so it's re-reading them once. A few minutes.");
        try { await svc.ImportHistoryAsync(); }
        catch (Exception ex) { AppSettings.LogError("rebuilding history", ex); }
        _rebuilding = false;
        svc.Start();
        _tray?.Notify("History updated", "EQL Pathfinder is tracking again.");
        _main?.Refresh();
    }

    private void OnGameChanged(bool running)
    {
        if (_service != null) _service.GameRunning = running;
        if (_meter != null) _meter.GameRunning = running;
        // A new game session gets its own one-time "log is off" balloon.
        if (!running) _logOffWarned = false;
        if (!running) _gameClosedAt = DateTime.Now;
        CheckLog();
        ApplyOverlayVisibility();
        _tray?.Refresh();
        _main?.UpdateChrome();
        if (!running) GameWatcher.TrimMemory();
    }

    /// <summary>The tray's "Send feedback…": the main window on Help, the feedback box ready to type in.</summary>
    internal void OpenFeedback()
    {
        OpenMain();
        _main?.Go("help", "fbText");
    }

    internal void OpenMain(bool minimized = false)
    {
        if (_service == null) return;
        if (_main == null)
        {
            _main = new MainWindow(this);
            _main.Closed += (_, _) => _main = null;
            if (minimized)
            {
                _main.ShowActivated = false;
                _main.WindowState = WindowState.Minimized;
            }
        }
        _main.Show();
        if (minimized) return;
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    internal void ChangeCharacter()
    {
        _main?.Close();
        StopMeter();
        _service?.Dispose();
        _service = null;
        if (RunSetup(WelcomeMode.Change, out var svc)) StartTracking(svc!);
        else if (TryResume() is { } again) StartTracking(again);
    }

    internal void OpenDataFolder()
    {
        Directory.CreateDirectory(AppSettings.DataDir);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppSettings.DataDir}\"") { UseShellExecute = true });
    }

    // ------------------------------------------------------------------ updates that install themselves

    private void StartUpdater()
    {
        _updater = new Updater(_settings.UpdateFeedUrl) { Beta = _settings.Beta };
        _updater.Downloaded += asset => Dispatcher.BeginInvoke(() =>
        {
            _tray?.Refresh();
            _main?.Refresh();
            var head = Updater.Headline(asset.NotesMarkdown);
            _tray?.Notify($"Update {asset.Version} is ready",
                (head.Length > 0 ? head + "\n" : "") + "It installs by itself once the game is closed and Pathfinder is in the background, or right-click the compass → Restart to update.");
        });
        _updater.Start();
        _autoUpdate = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _autoUpdate.Tick += (_, _) => MaybeInstallUpdate();
        _autoUpdate.Start();
        if (UpdatedTo is { } v && !_rebuilding)
        {
            var head = Updater.Headline(Updater.NotesFor(v));
            _tray?.Notify($"Updated to {v}", head.Length > 0 ? head : "EQL Pathfinder is up to date.");
        }
    }

    internal async void CheckForUpdates()
    {
        if (_updater is not { Enabled: true })
        {
            MessageBox.Show(_updater?.Feed is { Length: > 0 }
                    ? "This copy wasn't installed with Setup, so it can't update itself."
                    : "This copy has no update source.", "EQL Pathfinder");
            return;
        }
        switch (await _updater.CheckAsync(interactive: true))
        {
            case Updater.Outcome.UpToDate:
                _tray?.Notify("You're up to date", $"EQL Pathfinder {_updater.CurrentVersion} is the latest version.");
                break;
            case Updater.Outcome.Ready:
                _tray?.Refresh();
                var p = _updater.Pending!;
                var head = Updater.Headline(p.NotesMarkdown);
                if (MessageBox.Show($"Version {p.Version} is ready.{(head.Length > 0 ? "\n\n" + head : "")}\n\n" +
                                    "Restart EQL Pathfinder now to install it? Otherwise it installs by itself once the game is closed and Pathfinder is in the background.",
                        "EQL Pathfinder", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    RestartToUpdate();
                break;
            case Updater.Outcome.Failed:
                MessageBox.Show($"Couldn't check for updates:\n{_updater.LastError}", "EQL Pathfinder");
                break;
        }
    }

    /// <summary>Closes cleanly, then Velopack installs the downloaded update and starts the new version.</summary>
    internal void RestartToUpdate()
    {
        if (_updater?.Pending == null) return;
        CloseDown();
        _updater.RestartToUpdate();
        Shutdown();
    }

    /// <summary>
    /// Pathfinder lives in the tray for days, and a downloaded update only installed at the next start, so copies fell
    /// a dozen versions behind. Once a minute: if an update is waiting, the game has been closed for
    /// <see cref="QuietBeforeUpdate"/>, the window is hidden or minimized, and nothing is half done (reading history or
    /// past fights, a tour or setup, an upload, arranging overlays), it installs the update and comes back the same way:
    /// in the tray, or minimized. The "Updated to" balloon says what happened.
    /// </summary>
    private void MaybeInstallUpdate()
    {
        if (_updater is not { Enabled: true, Pending: not null }) return;
        if (GameRunning || DateTime.Now - _gameClosedAt < QuietBeforeUpdate) return;
        if (_main is { IsVisible: true, WindowState: not WindowState.Minimized }) return;
        if (_rebuilding || _service?.Importing == true || _meter?.Frame?.ImportPercent != null) return;
        if (_sharing?.Busy == true || _arranging) return;
        if (Windows.OfType<WelcomeWindow>().Any(w => w.IsVisible)) return;
        var how = _main is { IsVisible: true } ? "Minimized" : "Tray";
        UsageService.Metrics.Feature("autoupdate");
        _autoUpdate?.Stop();
        CloseDown();
        _updater.RestartToUpdate([StartAfterUpdateArg, how]);
        Shutdown();
    }

    internal void Quit()
    {
        CloseDown();
        Shutdown();
    }

    private void CloseDown()
    {
        foreach (var c in OverlayChromes()) c.SavePosition();
        _meter?.Dispose();
        _main?.SavePlacement();
        _settings.Save();
        _showWait?.Unregister(null);
        _showSignal?.Dispose();
        _updater?.Dispose();
        _sharing?.Dispose();
        _community?.Dispose();
        _usage?.Dispose();
        _game?.Dispose();
        _tray?.Dispose();
        _service?.Dispose();
        _store?.Dispose();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
